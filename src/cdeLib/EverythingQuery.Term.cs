using System;
using System.Collections.Generic;
using System.Text;
using cdeLib.Entities.Columnar;
using cdeLib.Infrastructure;
using Serilog;

namespace cdeLib;

// A word or phrase of the query: a substring, a wildcard pattern or a regular expression.
public sealed partial class EverythingQuery
{
    private sealed class Term : Node
    {
        private readonly string _text;
        private readonly int _position;
        private byte[] _needle; // plain term: the ASCII bytes (lower-cased unless case:), or null if non-ASCII
        private WildcardPattern _wildcard; // null for a plain substring term
        private BoundedRegex _regex; // set by regex:, which wins over wildcards
        private bool _regexRequested;
        private bool _matchCase;
        private bool _catalogMatch; // for a term in catalog target: whether the catalog's file name matches
        private Target _target = Target.Default;

        public Term(string text, int position)
        {
            _text = text;
            _position = position;
        }

        // The modifiers are final once parsing is done, so the matcher is built here, not in the constructor.
        public override void Bind(Binder binder)
        {
            BuildMatcher(binder);
            SetCatalog(string.Empty); // no catalog until the search says which one
        }

        public override void SetCatalog(string fileName)
        {
            if (_target != Target.Catalog) return;
            var timeouts = _regex?.Timeouts ?? 0;
            _catalogMatch = Contains(fileName.AsSpan());

            // The name is tested once, so a timeout would exclude the catalog without the three-timeout warning.
            if (_regex is not null && _regex.Timeouts > timeouts)
                Log.Warning("Regex {Pattern} timed out on the catalog file name {Catalog}; that catalog is treated as not matching",
                    _text, fileName);
        }

        public override bool? Constant => _target == Target.Catalog ? _catalogMatch : null;

        private void BuildMatcher(Binder binder)
        {
            if (_regexRequested)
            {
                try { _regex = new BoundedRegex(_text, ignoreCase: !_matchCase); }
                catch (ArgumentException ex)
                {
                    throw new EverythingQueryException($"Not a valid regular expression: {ex.Message}", _position);
                }

                binder.Regexes.Add(_regex);
                return;
            }

            if (WildcardPattern.IsWildcard(_text))
            {
                _wildcard = WildcardPattern.Parse(_text, ignoreCase: !_matchCase);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(_text);
            if (!Ascii.IsValid(bytes)) return;
            if (!_matchCase)
                for (var i = 0; i < bytes.Length; i++) bytes[i] = Utf8Matcher.ToLower(bytes[i]);
            _needle = bytes;
        }

        public bool UsesPath(bool defaultMatchesPath) =>
            _target == Target.Default ? defaultMatchesPath : _target == Target.Path;

        public bool Contains(ReadOnlySpan<byte> utf8)
        {
            if (_regex is not null || _wildcard is not null) return DecodedMatches(utf8);
            var ascii = Ascii.IsValid(utf8);
            if (_needle is not null && ascii)
                return _matchCase ? utf8.IndexOf(_needle) >= 0 : Utf8Matcher.AsciiContainsFolded(utf8, _needle);
            if (_matchCase && ascii) return false; // a non-ASCII term cannot be in ASCII text
            // Rare path: non-ASCII somewhere. Same decoded compare as Utf8Matcher, so results agree.
            return Encoding.UTF8.GetString(utf8).Contains(_text, Comparison);
        }

        private StringComparison Comparison => _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        public bool Contains(ReadOnlySpan<char> text) =>
            _regex is not null || _wildcard is not null
                ? MatchesPattern(text)
                : text.Contains(_text, Comparison);

        private bool MatchesPattern(ReadOnlySpan<char> text) =>
            _regex is not null ? _regex.IsMatch(text) : _wildcard.IsMatch(text);

        // Decode with the same replacement rules as Encoding.UTF8.GetString, so this agrees with the char path.
        private bool DecodedMatches(ReadOnlySpan<byte> utf8)
        {
            if (utf8.Length <= StackChars)
            {
                Span<char> buffer = stackalloc char[StackChars];
                var written = Encoding.UTF8.GetChars(utf8, buffer);
                return MatchesPattern(buffer[..written]);
            }

            var rented = System.Buffers.ArrayPool<char>.Shared.Rent(utf8.Length); // chars <= bytes
            try
            {
                var written = Encoding.UTF8.GetChars(utf8, rented);
                return MatchesPattern(rented.AsSpan(0, written));
            }
            finally { System.Buffers.ArrayPool<char>.Shared.Return(rented); }
        }

        private const int StackChars = 256;

        public override bool Eval(in Context c) => _target == Target.Catalog
            ? _catalogMatch
            : c.Source switch
            {
                Kind.SingleBytes => Contains(c.Bytes),
                Kind.SingleChars => Contains(c.Chars),
                Kind.Utf8Entry => Contains(UsesPath(c.DefaultMatchesPath) ? c.Utf8Entry.Path : c.Utf8Entry.Name),
                _ => Contains(UsesPath(c.DefaultMatchesPath) ? c.CharEntry.Path : c.CharEntry.Name),
            };

        public override Node Apply(Modifier modifier)
        {
            if (_target == Target.Default) _target = modifier.Target;
            _regexRequested |= modifier.Regex;
            _matchCase |= modifier.Case;
            return this;
        }

        public override Use Uses() => _target switch
        {
            Target.Path => Use.Path,
            Target.Name => Use.Name,
            Target.Catalog => Use.None, // the catalog's file name, not any text of the entry
            _ => Use.Default,
        };
    }
}
