using System;
using System.Collections.Generic;
using System.Text;

namespace cdeLib;

// The Everything functions: tests on a property of the entry that stand alone in the AND, OR, NOT and group tree.
public sealed partial class EverythingQuery
{
    private enum Function
    {
        Size,
        DateModified,
        Ext,
        Empty,
        ChildCount,
        ChildFileCount,
        ChildFolderCount,
        DescendantCount,
        DescendantFileCount,
        DescendantFolderCount,
        Dupe,
    }

    /// <summary>Names are matched with the dashes removed, so <c>date-modified</c> and <c>datemodified</c> are the same.</summary>
    private const int MaxFunctionName = 24; // "descendant-folder-count" is the longest, dashes included

    private static bool IsFunction(ReadOnlySpan<char> name, out Function function)
    {
        function = Function.Size;
        if (name.Length > MaxFunctionName) return false;
        Span<char> folded = stackalloc char[MaxFunctionName];
        var n = 0;
        foreach (var c in name)
            if (c != '-') folded[n++] = char.ToLowerInvariant(c);

        ReadOnlySpan<char> key = folded[..n];
        switch (key)
        {
            case "size": function = Function.Size; return true;
            case "datemodified" or "dm": function = Function.DateModified; return true;
            case "ext": function = Function.Ext; return true;
            case "empty": function = Function.Empty; return true;
            case "childcount": function = Function.ChildCount; return true;
            case "childfilecount": function = Function.ChildFileCount; return true;
            case "childfoldercount": function = Function.ChildFolderCount; return true;
            case "descendantcount": function = Function.DescendantCount; return true;
            case "descendantfilecount": function = Function.DescendantFileCount; return true;
            case "descendantfoldercount": function = Function.DescendantFolderCount; return true;
            case "dupe": function = Function.Dupe; return true;
            default: return false;
        }
    }

    // The functions that can follow case: and diacritics:, which are not modifiers of a term but of the whole test.
    private static bool SupportsCase(Function function) => function is Function.Ext or Function.Dupe;

    private static Node CreateFunction(string token, int position)
    {
        var colon = token.IndexOf(':');
        IsFunction(token.AsSpan(0, colon), out var function);
        var value = token[(colon + 1)..];

        switch (function)
        {
            case Function.Ext:
                return new ExtFilter(value, position);
            case Function.Dupe:
                return new DupeFilter(value, position);
            case Function.Empty:
                if (value.Length != 0) throw new EverythingQueryException("'empty:' takes no value", position);
                return new CountFilter(CountKind.Children, "0", position);
            default:
                if (value.Length == 0)
                    throw new EverythingQueryException($"'{token}' needs a value after the colon", position);
                return function switch
                {
                    Function.Size or Function.DateModified => new PropertyFilter(function, value, position),
                    Function.ChildCount => new CountFilter(CountKind.Children, value, position),
                    Function.ChildFileCount => new CountFilter(CountKind.ChildFiles, value, position),
                    Function.ChildFolderCount => new CountFilter(CountKind.ChildFolders, value, position),
                    Function.DescendantCount => new CountFilter(CountKind.Descendants, value, position),
                    Function.DescendantFileCount => new CountFilter(CountKind.DescendantFiles, value, position),
                    _ => new CountFilter(CountKind.DescendantFolders, value, position),
                };
        }
    }

    // size: or date-modified:. The value is parsed in Bind, which has the clock.
    private sealed class PropertyFilter(Function function, string value, int position) : Node
    {
        private NumericCriterion _criterion;

        public override void Bind(Binder binder)
        {
            try
            {
                _criterion = function == Function.Size
                    ? NumericCriterion.ParseSize(value)
                    : NumericCriterion.ParseDate(value, binder.Now);
            }
            catch (FormatException ex)
            {
                throw new EverythingQueryException(ex.Message, position);
            }
        }

        public override Needs Needs => Needs.Properties;

        public override bool Eval(in Context c)
        {
            var number = function == Function.Size
                ? c.Source switch
                {
                    Kind.SingleBytes or Kind.SingleChars => c.Size,
                    Kind.Utf8Entry => c.Utf8Entry.Size,
                    _ => c.CharEntry.Size,
                }
                : c.Source switch
                {
                    Kind.SingleBytes or Kind.SingleChars => c.ModifiedTicks,
                    Kind.Utf8Entry => c.Utf8Entry.ModifiedTicks,
                    _ => c.CharEntry.ModifiedTicks,
                };
            return _criterion.Matches(number);
        }

        public override Use Uses() => Use.None;

        public override Node Apply(Modifier modifier) => this;
    }

    private enum CountKind { Children, ChildFiles, ChildFolders, Descendants, DescendantFiles, DescendantFolders }

    // empty:, child-count:, child-file-count:, child-folder-count: and the descendant forms. They test folders only.
    private sealed class CountFilter(CountKind kind, string value, int position) : Node
    {
        private NumericCriterion _criterion;

        public override void Bind(Binder binder)
        {
            try { _criterion = NumericCriterion.ParseCount(value); }
            catch (FormatException ex) { throw new EverythingQueryException(ex.Message, position); }
        }

        public override Needs Needs =>
            kind is CountKind.Descendants or CountKind.DescendantFiles or CountKind.DescendantFolders
                ? Needs.DescendantCounts
                : Needs.ChildCounts;

        public override bool Eval(in Context c)
        {
            var isFolder = c.Source switch
            {
                Kind.SingleBytes or Kind.SingleChars => c.IsFolder,
                Kind.Utf8Entry => c.Utf8Entry.IsFolder,
                _ => c.CharEntry.IsFolder,
            };
            if (!isFolder) return false;

            var counts = c.Source switch
            {
                Kind.SingleBytes or Kind.SingleChars => c.Counts,
                Kind.Utf8Entry => c.Utf8Entry.Counts,
                _ => c.CharEntry.Counts,
            };
            var number = kind switch
            {
                CountKind.Children => counts.Children,
                CountKind.ChildFiles => counts.ChildFiles,
                CountKind.ChildFolders => counts.ChildFolders,
                CountKind.Descendants => counts.Descendants,
                CountKind.DescendantFiles => counts.DescendantFiles,
                _ => counts.DescendantFolders,
            };
            return _criterion.Matches(number);
        }

        public override Use Uses() => Use.None;

        public override Node Apply(Modifier modifier) => this;
    }

    // dupe:. True for an entry that shares its key (the properties named) with another entry of any loaded catalog.
    // The scan loops count the keys first (DuplicateIndex) and hand each entry its answer in the DuplicateMask.
    private sealed class DupeFilter(string value, int position) : Node
    {
        private bool _matchCase;
        private int _index;

        public override void Bind(Binder binder)
        {
            var properties = DupeProperties.None;
            foreach (var raw in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                properties |= raw.Replace("-", string.Empty).ToLowerInvariant() switch
                {
                    "name" => DupeProperties.Name,
                    "path" => DupeProperties.Path,
                    "ext" or "extension" => DupeProperties.Extension,
                    "size" => DupeProperties.Size,
                    "datemodified" or "dm" => DupeProperties.DateModified,
                    "hash" => DupeProperties.Hash,
                    _ => throw new EverythingQueryException(
                        $"'{raw}' is not a dupe: property; use name, path, ext, size, date-modified or hash", position),
                };
            }

            if (properties == DupeProperties.None) properties = DupeProperties.Name;
            // Case only matters for text, so size:/dm:/hash: functions with and without case: are the same function.
            var comparesText = (properties & (DupeProperties.Name | DupeProperties.Path | DupeProperties.Extension)) != 0;
            _index = binder.AddDupe(new DupeSpec(properties, _matchCase && comparesText), position);
        }

        public override bool Eval(in Context c)
        {
            var mask = c.Source switch
            {
                Kind.SingleBytes or Kind.SingleChars => c.Duplicates,
                Kind.Utf8Entry => c.Utf8Entry.Duplicates,
                _ => c.CharEntry.Duplicates,
            };
            return (mask >> _index & 1UL) != 0;
        }

        public override Use Uses() => Use.None;

        public override Node Apply(Modifier modifier)
        {
            _matchCase |= modifier.Case;
            return this;
        }
    }

    // ext:. Whole extension of a file name: the text after the last dot, when that dot is not the first character
    // (the same split the catalog uses, so .gitignore has no extension and a. has an empty one). Folders never match.
    private sealed class ExtFilter(string value, int position) : Node
    {
        private const int StackChars = 256;

        private bool _matchCase;
        private string[] _exact;
        private WildcardPattern[] _wildcards;
        private bool _any; // ext:* and the like: any file

        public override void Bind(Binder binder)
        {
            var exact = new List<string>();
            var wildcards = new List<WildcardPattern>();
            if (value.Length == 0) exact.Add(string.Empty); // ext: alone is files with no extension

            foreach (var raw in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var ext = raw.StartsWith("*.", StringComparison.Ordinal) ? raw[2..]
                    : raw[0] is '.' or '*' ? raw[1..]
                    : raw;
                if (ext.Length == 0) _any = true;
                else if (WildcardPattern.IsWildcard(ext)) wildcards.Add(WildcardPattern.Parse(ext, ignoreCase: !_matchCase));
                else exact.Add(ext);
            }

            if (exact.Count == 0 && wildcards.Count == 0 && !_any)
                throw new EverythingQueryException("'ext:' needs an extension, or none to match files with no extension", position);
            _exact = [.. exact];
            _wildcards = [.. wildcards];
        }

        private bool MatchesExtension(ReadOnlySpan<char> ext)
        {
            if (_any) return true;
            var comparison = _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (var e in _exact)
                if (ext.Equals(e, comparison)) return true;
            foreach (var w in _wildcards)
                if (w.IsMatch(ext)) return true;
            return false;
        }

        private static ReadOnlySpan<char> ExtensionOf(ReadOnlySpan<char> name)
        {
            var dot = name.LastIndexOf('.');
            return dot > 0 ? name[(dot + 1)..] : default;
        }

        private bool MatchesName(ReadOnlySpan<char> name) => MatchesExtension(ExtensionOf(name));

        private bool MatchesName(ReadOnlySpan<byte> utf8)
        {
            var dot = utf8.LastIndexOf((byte)'.'); // '.' is one byte and never part of a longer UTF-8 sequence
            var ext = dot > 0 ? utf8[(dot + 1)..] : default;
            if (ext.Length <= StackChars)
            {
                Span<char> buffer = stackalloc char[StackChars];
                return MatchesExtension(buffer[..Encoding.UTF8.GetChars(ext, buffer)]);
            }

            var rented = System.Buffers.ArrayPool<char>.Shared.Rent(ext.Length);
            try { return MatchesExtension(rented.AsSpan(0, Encoding.UTF8.GetChars(ext, rented))); }
            finally { System.Buffers.ArrayPool<char>.Shared.Return(rented); }
        }

        public override bool Eval(in Context c) => c.Source switch
        {
            Kind.SingleBytes => !c.IsFolder && MatchesName(c.Bytes),
            Kind.SingleChars => !c.IsFolder && MatchesName(c.Chars),
            Kind.Utf8Entry => !c.Utf8Entry.IsFolder && MatchesName(c.Utf8Entry.Name),
            _ => !c.CharEntry.IsFolder && MatchesName(c.CharEntry.Name),
        };

        public override Use Uses() => Use.Name;

        public override Node Apply(Modifier modifier)
        {
            _matchCase |= modifier.Case;
            return this;
        }
    }
}
