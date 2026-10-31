using System;
using System.Collections.Generic;
using System.Text;
using cdeLib.Entities.Columnar;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace cdeLib;

/// <summary>The text of an Everything-style query is not valid. <see cref="Position"/> is the 0-based character offset.</summary>
public sealed class EverythingQueryException(string reason, int position)
    : FormatException($"{reason} (at position {position})")
{
    public int Position { get; } = position;
}

/// <summary>
/// The text of one entry for an <see cref="EverythingQuery"/>, as UTF-8. <see cref="Path"/> is only read when the
/// query needs it, so an implementation can build it lazily. Implementations are classes (the query keeps no copy).
/// </summary>
public interface IEntryText
{
    /// <summary>The entry name (name plus extension).</summary>
    ReadOnlySpan<byte> Name { get; }

    /// <summary>The full path, including the name.</summary>
    ReadOnlySpan<byte> Path { get; }

    /// <summary>True for a folder, false for a file. Only read when the query has a file: or folder: term.</summary>
    bool IsFolder { get; }
}

/// <summary>Same as <see cref="IEntryText"/> but as characters.</summary>
public interface IEntryChars
{
    ReadOnlySpan<char> Name { get; }
    ReadOnlySpan<char> Path { get; }
    bool IsFolder { get; }
}

/// <summary>Which of an entry's texts an <see cref="EverythingQuery"/> reads.</summary>
public enum EverythingTexts
{
    /// <summary>Only the name (also the answer for an empty query).</summary>
    Name,

    /// <summary>Only the full path.</summary>
    Path,

    /// <summary>Both: some terms read the name and others the path.</summary>
    Both,
}

/// <summary>
/// An Everything-style (voidtools) search query, parsed once so testing an entry does no parsing. Supported:
/// space is AND, <c>|</c> is OR, <c>!</c> is NOT (it applies to the next term, group or phrase), <c>&lt; &gt;</c>
/// group, <c>"..."</c> is a literal phrase (operators inside quotes are plain text), and the modifiers
/// <c>path:</c> (match the full path) and <c>no-path:</c> (match the name only). A modifier goes directly in front
/// of one word, phrase or group, with no space; inside a group the innermost modifier wins. The filters
/// <c>file:</c> and <c>folder:</c> are stand-alone tests on the entry kind (not modifiers): they join the AND, OR, NOT
/// and group tree like a term, and the text after the colon is its own term. With no filter, files and folders match. A term with no modifier
/// matches the name, or the full path when the caller says the default is the path (a caller option; the app passes
/// false, because Everything mode uses path: instead of the Include Path toggle).
/// Precedence from tightest: <c>!</c>, groups, space, <c>|</c>. A term is a case-insensitive substring; modifier
/// names are case-insensitive. An empty query matches everything. Backslash is an ordinary character. The
/// <c>\:</c> alias and the global <c>?path:</c> form are not supported yet.
/// See https://www.voidtools.com/support/everything/search_syntax/
/// </summary>
public sealed class EverythingQuery
{
    /// <summary>Deepest allowed nesting of <c>&lt; &gt;</c>, so a hostile query cannot overflow the stack.</summary>
    public const int MaxDepth = 32;

    private readonly Node _root; // null for an empty query

    private EverythingQuery(Node root) => _root = root;

    /// <summary>True for an empty (or all-whitespace) query, which matches everything.</summary>
    public bool IsEmpty => _root is null;

    /// <summary>
    /// Which texts the query reads. A caller whose answer is <see cref="EverythingTexts.Name"/> or
    /// <see cref="EverythingTexts.Path"/> can pass just that one text to <see cref="Matches(ReadOnlySpan{byte})"/>,
    /// which is faster than an entry object. Only <see cref="EverythingTexts.Both"/> needs an entry.
    /// </summary>
    public EverythingTexts TextsRead(bool defaultMatchesPath)
    {
        var uses = _root?.Uses() ?? Use.None;
        var path = (uses & Use.Path) != 0 || ((uses & Use.Default) != 0 && defaultMatchesPath);
        var name = (uses & Use.Name) != 0 || ((uses & Use.Default) != 0 && !defaultMatchesPath);
        return path && name ? EverythingTexts.Both : path ? EverythingTexts.Path : EverythingTexts.Name;
    }

    /// <summary>
    /// Test one UTF-8 text that stands for both the name and the path (a caller that has only one text).
    /// Allocation-free when the text and every term are ASCII.
    /// </summary>
    public bool Matches(ReadOnlySpan<byte> utf8, bool isFolder = false)
    {
        if (_root is null) return true;
        var context = new Context(utf8, isFolder);
        return _root.Eval(in context);
    }

    /// <summary>Test one text that stands for both the name and the path; identical to the UTF-8 overload.</summary>
    public bool Matches(ReadOnlySpan<char> text, bool isFolder = false)
    {
        if (_root is null) return true;
        var context = new Context(text, isFolder);
        return _root.Eval(in context);
    }

    /// <summary>Test one text that stands for both the name and the path.</summary>
    public bool Matches(string text, bool isFolder = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Matches(text.AsSpan(), isFolder);
    }

    /// <summary>Test an entry's name and full path. Unmarked terms use the path if <paramref name="defaultMatchesPath"/>.</summary>
    public bool Matches(string name, string fullPath, bool defaultMatchesPath, bool isFolder = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fullPath);
        return Matches(new StringEntry(name, fullPath, isFolder), defaultMatchesPath);
    }

    /// <summary>
    /// Test an entry given as UTF-8. <see cref="IEntryText.Path"/> is read only if a term needs it, and may be read
    /// more than once, so build it once and keep it. Allocation-free when everything is ASCII.
    /// </summary>
    public bool Matches(IEntryText entry, bool defaultMatchesPath)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_root is null) return true;
        var context = new Context(entry, defaultMatchesPath);
        return _root.Eval(in context);
    }

    /// <summary>Test an entry given as characters; the result is identical to the UTF-8 overload.</summary>
    public bool Matches(IEntryChars entry, bool defaultMatchesPath)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_root is null) return true;
        var context = new Context(entry, defaultMatchesPath);
        return _root.Eval(in context);
    }

    /// <summary>Parse a query. Throws <see cref="EverythingQueryException"/> (a <see cref="FormatException"/>) if it is malformed.</summary>
    public static EverythingQuery Parse(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query)) return new EverythingQuery(null);

        CheckQuotesAndBrackets(query);

        var tokens = Lexer.Tokenize(query);
        CheckModifiers(tokens);

        var result = Grammar.TryParse(tokens);
        if (!result.HasValue)
        {
            var position = result.Remainder.IsAtEnd
                ? LastTokenPosition(tokens)
                : result.ErrorPosition.Absolute;
            throw new EverythingQueryException(Describe(result.Remainder.IsAtEnd, query, position), position);
        }

        return new EverythingQuery(result.Value);
    }

    private static int LastTokenPosition(TokenList<Tok> tokens)
    {
        var last = 0;
        foreach (var t in tokens) last = t.Position.Absolute;
        return last;
    }

    private static string Describe(bool atEnd, string query, int position)
    {
        var c = query[position];
        if (atEnd)
            return c switch
            {
                '|' => "Missing a term after '|'",
                '!' => "Missing a term after '!'",
                _ => "The query ends too soon",
            };

        return c switch
        {
            '|' => "Unexpected '|': a term is missing before it",
            '!' => "Unexpected '!': '!' only applies to one term, group or phrase",
            '>' => "Empty group or unexpected '>'",
            _ => "Unexpected text",
        };
    }

    // Quotes and brackets are checked up front so that their errors point at the opening quote or bracket, and so
    // that nesting depth is limited before the recursive grammar runs. Operators inside quotes are plain text.
    private static void CheckQuotesAndBrackets(string query)
    {
        var opens = new Stack<int>();
        for (var i = 0; i < query.Length; i++)
        {
            switch (query[i])
            {
                case '"':
                    var close = query.IndexOf('"', i + 1);
                    if (close < 0) throw new EverythingQueryException("Missing closing quote", i);
                    if (close == i + 1) throw new EverythingQueryException("Empty phrase", i);
                    i = close;
                    break;
                case '<':
                    if (opens.Count == MaxDepth)
                        throw new EverythingQueryException($"Groups nested deeper than {MaxDepth}", i);
                    opens.Push(i);
                    break;
                case '>':
                    if (opens.Count == 0) throw new EverythingQueryException("Unexpected '>' with no matching '<'", i);
                    opens.Pop();
                    break;
            }
        }

        if (opens.Count > 0) throw new EverythingQueryException("Missing closing '>'", opens.Peek());
    }

    // The forms Everything has that are not supported yet, and the rule that a modifier is attached to what it
    // modifies: the very next character starts a word, a phrase or a group. Errors point at the modifier.
    private static void CheckModifiers(TokenList<Tok> tokens)
    {
        var list = new List<Token<Tok>>();
        foreach (var t in tokens) list.Add(t);

        for (var i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t.Kind == Tok.Unsupported)
                throw new EverythingQueryException(
                    "The '?' global modifier and the '\\:' alias are not supported yet; use path: or no-path: in front of a term",
                    t.Position.Absolute);

            if (t.Kind is not (Tok.PathModifier or Tok.NoPathModifier)) continue;

            var attached = i + 1 < list.Count
                           && list[i + 1].Position.Absolute == t.Position.Absolute + t.Span.Length
                           && list[i + 1].Kind is Tok.Word or Tok.Phrase or Tok.Open;
            if (!attached)
                throw new EverythingQueryException(
                    $"'{t.ToStringValue()}' must be directly followed by a term, phrase or group, with no space",
                    t.Position.Absolute);
        }
    }

    // ---- grammar ----

    private enum Tok { Pipe, Bang, Open, Close, Phrase, Word, PathModifier, NoPathModifier, FileFilter, FolderFilter, Unsupported }

    /// <summary>Which text a term is tested against.</summary>
    private enum Target { Default, Path, Name }

    [Flags]
    private enum Use { None = 0, Default = 1, Path = 2, Name = 4 }

    private static readonly TextParser<Unit> PhraseText =
        from open in Character.EqualTo('"')
        from text in Character.ExceptIn('"').Many()
        from close in Character.EqualTo('"')
        select Unit.Value;

    // A word runs to the next space or operator. A '!' inside a word is literal; only a leading '!' negates.
    private static readonly TextParser<Unit> WordText =
        Character.Matching(c => !char.IsWhiteSpace(c) && c is not ('|' or '<' or '>' or '"'), "word")
            .AtLeastOnce().Select(_ => Unit.Value);

    private static readonly TextParser<Unit> NoPathText = Span.EqualToIgnoreCase("no-path:").Value(Unit.Value);
    private static readonly TextParser<Unit> PathText = Span.EqualToIgnoreCase("path:").Value(Unit.Value);
    private static readonly TextParser<Unit> FileText = Span.EqualToIgnoreCase("file:").Value(Unit.Value);
    private static readonly TextParser<Unit> FolderText = Span.EqualToIgnoreCase("folder:").Value(Unit.Value);

    // '?path:' (global modifier) and the '\:' alias for path: are seen so that they can be refused clearly.
    private static readonly TextParser<Unit> UnsupportedText =
        Character.EqualTo('?').IgnoreThen(NoPathText.Try().Or(PathText))
            .Or(Span.EqualTo("\\:").Value(Unit.Value));

    private static readonly Tokenizer<Tok> Lexer = new TokenizerBuilder<Tok>()
        .Ignore(Span.WhiteSpace)
        .Match(Character.EqualTo('|'), Tok.Pipe)
        .Match(Character.EqualTo('!'), Tok.Bang)
        .Match(Character.EqualTo('<'), Tok.Open)
        .Match(Character.EqualTo('>'), Tok.Close)
        .Match(PhraseText, Tok.Phrase)
        .Match(UnsupportedText, Tok.Unsupported)
        .Match(NoPathText, Tok.NoPathModifier)
        .Match(PathText, Tok.PathModifier)
        .Match(FileText, Tok.FileFilter)
        .Match(FolderText, Tok.FolderFilter)
        .Match(WordText, Tok.Word)
        .Build();

    private static readonly TokenListParser<Tok, Node> Group =
        from open in Token.EqualTo(Tok.Open)
        from inner in Superpower.Parse.Ref(() => Expression)
        from close in Token.EqualTo(Tok.Close)
        select inner;

    private static readonly TokenListParser<Tok, Node> Operand =
        Token.EqualTo(Tok.Word).Select(t => (Node)new Term(t.ToStringValue()))
            .Or(Token.EqualTo(Tok.Phrase).Select(t =>
            {
                var s = t.ToStringValue();
                return (Node)new Term(s.Substring(1, s.Length - 2));
            }))
            .Or(Group);

    private static readonly TokenListParser<Tok, Target> Modifier =
        Token.EqualTo(Tok.PathModifier).Value(Target.Path)
            .Or(Token.EqualTo(Tok.NoPathModifier).Value(Target.Name));

    // file: and folder: are stand-alone tests on the entry, not modifiers: the text after them is its own term.
    private static readonly TokenListParser<Tok, Node> Filter =
        Token.EqualTo(Tok.FileFilter).Select(_ => (Node)new KindFilter(isFolder: false))
            .Or(Token.EqualTo(Tok.FolderFilter).Select(_ => (Node)new KindFilter(isFolder: true)));

    // A modifier fills in the terms under it that have none yet. Inner groups are built first, so the innermost
    // modifier wins.
    private static readonly TokenListParser<Tok, Node> Modified =
        (from modifier in Modifier
         from operand in Operand
         select operand.Apply(modifier))
        .Or(Operand)
        .Or(Filter);

    // '!' takes one operand only, so '!!a' is an error rather than a double negative. It goes in front of a modifier.
    private static readonly TokenListParser<Tok, Node> Unary =
        (from bang in Token.EqualTo(Tok.Bang)
         from operand in Modified
         select (Node)new Not(operand))
        .Or(Modified);

    private static readonly TokenListParser<Tok, Node> AndExpression =
        Unary.AtLeastOnce().Select(parts => parts.Length == 1 ? parts[0] : new And(parts));

    private static readonly TokenListParser<Tok, Node> Expression =
        AndExpression.AtLeastOnceDelimitedBy(Token.EqualTo(Tok.Pipe))
            .Select(parts => parts.Length == 1 ? parts[0] : new Or(parts));

    private static readonly TokenListParser<Tok, Node> Grammar = Expression.AtEnd();

    // ---- what a term is tested against ----

    // Either one text standing for both name and path, or an entry giving them separately. A plain struct passed to
    // a plain virtual Eval: a generic virtual method here was measurably slower on the hot scan loop.
    private readonly ref struct Context
    {
        public readonly ReadOnlySpan<byte> Bytes;
        public readonly ReadOnlySpan<char> Chars;
        public readonly IEntryText Utf8Entry;
        public readonly IEntryChars CharEntry;
        public readonly bool DefaultMatchesPath;
        public readonly bool IsFolder; // only for the single-text sources; an entry answers for itself
        public readonly Kind Source;

        public Context(ReadOnlySpan<byte> bytes, bool isFolder)
        {
            Bytes = bytes;
            IsFolder = isFolder;
            Source = Kind.SingleBytes;
        }

        public Context(ReadOnlySpan<char> chars, bool isFolder)
        {
            Chars = chars;
            IsFolder = isFolder;
            Source = Kind.SingleChars;
        }

        public Context(IEntryText entry, bool defaultMatchesPath)
        {
            Utf8Entry = entry;
            DefaultMatchesPath = defaultMatchesPath;
            Source = Kind.Utf8Entry;
        }

        public Context(IEntryChars entry, bool defaultMatchesPath)
        {
            CharEntry = entry;
            DefaultMatchesPath = defaultMatchesPath;
            Source = Kind.CharEntry;
        }
    }

    private enum Kind { SingleBytes, SingleChars, Utf8Entry, CharEntry }

    private sealed class StringEntry(string name, string path, bool isFolder) : IEntryChars
    {
        public ReadOnlySpan<char> Name => name;
        public ReadOnlySpan<char> Path => path;
        public bool IsFolder => isFolder;
    }

    // ---- syntax tree ----

    private abstract class Node
    {
        public abstract bool Eval(in Context context);

        /// <summary>Give every term that has no path: or no-path: yet this target. Returns this node.</summary>
        public abstract Node Apply(Target target);

        /// <summary>The kinds of target the terms under this node have.</summary>
        public abstract Use Uses();
    }

    private sealed class Term : Node
    {
        private readonly string _text;
        private readonly byte[] _lowerAscii; // null if the term has non-ASCII text
        private Target _target = Target.Default;

        public Term(string text)
        {
            _text = text;
            var bytes = Encoding.UTF8.GetBytes(text);
            if (!Ascii.IsValid(bytes)) return;
            for (var i = 0; i < bytes.Length; i++) bytes[i] = Utf8Matcher.ToLower(bytes[i]);
            _lowerAscii = bytes;
        }

        public bool UsesPath(bool defaultMatchesPath) =>
            _target == Target.Default ? defaultMatchesPath : _target == Target.Path;

        public bool Contains(ReadOnlySpan<byte> utf8)
        {
            if (_lowerAscii is not null && Ascii.IsValid(utf8))
                return Utf8Matcher.AsciiContainsFolded(utf8, _lowerAscii);
            // Rare path: non-ASCII somewhere. Same decoded compare as Utf8Matcher, so results agree.
            return Encoding.UTF8.GetString(utf8).Contains(_text, StringComparison.OrdinalIgnoreCase);
        }

        public bool Contains(ReadOnlySpan<char> text) => text.Contains(_text, StringComparison.OrdinalIgnoreCase);

        public override bool Eval(in Context c) => c.Source switch
        {
            Kind.SingleBytes => Contains(c.Bytes),
            Kind.SingleChars => Contains(c.Chars),
            Kind.Utf8Entry => Contains(UsesPath(c.DefaultMatchesPath) ? c.Utf8Entry.Path : c.Utf8Entry.Name),
            _ => Contains(UsesPath(c.DefaultMatchesPath) ? c.CharEntry.Path : c.CharEntry.Name),
        };

        public override Node Apply(Target target)
        {
            if (_target == Target.Default) _target = target;
            return this;
        }

        public override Use Uses() => _target switch
        {
            Target.Path => Use.Path,
            Target.Name => Use.Name,
            _ => Use.Default,
        };
    }

    private sealed class KindFilter(bool isFolder) : Node
    {
        public override bool Eval(in Context c) => c.Source switch
        {
            Kind.SingleBytes or Kind.SingleChars => c.IsFolder == isFolder,
            Kind.Utf8Entry => c.Utf8Entry.IsFolder == isFolder,
            _ => c.CharEntry.IsFolder == isFolder,
        };

        public override Use Uses() => Use.None;

        public override Node Apply(Target target) => this;
    }

    private sealed class Not(Node inner) : Node
    {
        public override bool Eval(in Context context) => !inner.Eval(in context);

        public override Use Uses() => inner.Uses();

        public override Node Apply(Target target)
        {
            inner.Apply(target);
            return this;
        }
    }

    private sealed class And(Node[] parts) : Node
    {
        public override Use Uses()
        {
            var uses = Use.None;
            foreach (var p in parts) uses |= p.Uses();
            return uses;
        }

        public override bool Eval(in Context context)
        {
            foreach (var p in parts)
                if (!p.Eval(in context)) return false;
            return true;
        }

        public override Node Apply(Target target)
        {
            foreach (var p in parts) p.Apply(target);
            return this;
        }
    }

    private sealed class Or(Node[] parts) : Node
    {
        public override Use Uses()
        {
            var uses = Use.None;
            foreach (var p in parts) uses |= p.Uses();
            return uses;
        }

        public override bool Eval(in Context context)
        {
            foreach (var p in parts)
                if (p.Eval(in context)) return true;
            return false;
        }

        public override Node Apply(Target target)
        {
            foreach (var p in parts) p.Apply(target);
            return this;
        }
    }
}
