using System;
using System.Collections.Generic;
using System.Text;
using cdeLib.Entities.Columnar;
using cdeLib.Infrastructure;
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

    /// <summary>The size in bytes. Only read when the query has a size: term, so override it to support one.</summary>
    long Size => 0;

    /// <summary>The modified time as local ticks. Only read when the query has a date-modified: term.</summary>
    long ModifiedTicks => 0;

    /// <summary>The child and descendant counts. Only read when the query has a count function or empty:.</summary>
    EntryCounts Counts => default;
}

/// <summary>Same as <see cref="IEntryText"/> but as characters.</summary>
public interface IEntryChars
{
    ReadOnlySpan<char> Name { get; }
    ReadOnlySpan<char> Path { get; }
    bool IsFolder { get; }

    /// <summary>See <see cref="IEntryText.Size"/>.</summary>
    long Size => 0;

    /// <summary>See <see cref="IEntryText.ModifiedTicks"/>.</summary>
    long ModifiedTicks => 0;

    /// <summary>See <see cref="IEntryText.Counts"/>.</summary>
    EntryCounts Counts => default;
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
/// <c>path:</c> (match the full path), <c>no-path:</c> (match the name only) and <c>regex:</c> (the text is a
/// regular expression, case-insensitive, matching anywhere in the text, so use a phrase for one with a space or
/// <c>|</c>), <c>case:</c> (match case; terms, wildcards and <c>regex:</c> ignore case otherwise) and
/// <c>diacritics:</c> (accepted for Everything compatibility; diacritics always count here, so it changes nothing).
/// A modifier goes directly in front of one word, phrase or group, with no space; modifiers chain
/// (<c>path:regex:^c:</c>); the one nearest the term wins, and inside a group the innermost modifier wins. The filters
/// <c>file:</c> and <c>folder:</c> are stand-alone tests on the entry kind (not modifiers): they join the AND, OR, NOT
/// and group tree like a term, and the text after the colon is its own term. So are the numeric functions
/// <c>size:</c> and <c>date-modified:</c> (alias <c>dm:</c>, dashes in the name are optional), whose value is
/// parsed by <see cref="NumericCriterion"/>. They need the entry's size and modified time:
/// see <see cref="UsesProperties"/>. With no filter, files and folders match. A term with no modifier
/// matches the name, or the full path when the caller says the default is the path (a caller option; the app passes
/// false, because Everything mode uses path: instead of the Include Path toggle).
/// Precedence from tightest: <c>!</c>, groups, space, <c>|</c>. A term is a case-insensitive substring; modifier
/// names are case-insensitive. A term with a wildcard (<c>*</c> any characters except <c>\</c>, <c>**</c> any
/// characters including <c>\</c>, <c>?</c> one character except <c>\</c>, also inside quotes) must match the WHOLE
/// text it targets, so <c>*.txt</c> is a suffix match (see <see cref="WildcardPattern"/>). An empty query matches
/// everything. Backslash is an ordinary character outside wildcards. The <c>\:</c> alias and the global
/// <c>?path:</c> form are not supported yet.
/// See https://www.voidtools.com/support/everything/search_syntax/
/// </summary>
public sealed partial class EverythingQuery
{
    /// <summary>Deepest allowed nesting of <c>&lt; &gt;</c>, so a hostile query cannot overflow the stack.</summary>
    public const int MaxDepth = 32;

    private readonly Node _root; // null for an empty query
    private readonly BoundedRegex[] _regexes;

    private EverythingQuery(Node root, Binder binder)
    {
        _root = root;
        _regexes = binder?.Regexes.ToArray() ?? [];
        var needs = root?.Needs ?? Needs.None;
        UsesProperties = (needs & Needs.Properties) != 0;
        UsesCounts = (needs & (Needs.ChildCounts | Needs.DescendantCounts)) != 0;
        UsesDescendantCounts = (needs & Needs.DescendantCounts) != 0;
    }

    /// <summary>
    /// True if the query has a size: or date-modified: term, so the caller must pass the entry's size and modified
    /// time to <c>Matches</c>. A caller without them can skip reading them when this is false.
    /// </summary>
    public bool UsesProperties { get; }

    /// <summary>
    /// True if the query has a child-count:, descendant-count: or empty: function, so the caller must pass the
    /// entry's <see cref="EntryCounts"/> (build them once per catalog with <see cref="EntryCountTable"/>).
    /// </summary>
    public bool UsesCounts { get; }

    /// <summary>True if the counts must include <see cref="EntryCounts.DescendantFiles"/> and the descendant folders.</summary>
    public bool UsesDescendantCounts { get; }

    /// <summary>True once a <c>regex:</c> term has timed out too often: the caller should stop scanning.</summary>
    public bool RegexExhausted
    {
        get
        {
            foreach (var r in _regexes)
                if (r.Exhausted) return true;
            return false;
        }
    }

    /// <summary>If a regex timed out too often, logs once that the results are incomplete. Call after the scan.</summary>
    public void ReportRegexExhausted()
    {
        foreach (var r in _regexes) r.ReportIfExhausted();
    }

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
    public bool Matches(ReadOnlySpan<byte> utf8, bool isFolder = false, long size = 0, long modifiedTicks = 0,
        EntryCounts counts = default)
    {
        if (_root is null) return true;
        var context = new Context(utf8, isFolder, size, modifiedTicks, counts);
        return _root.Eval(in context);
    }

    /// <summary>Test one text that stands for both the name and the path; identical to the UTF-8 overload.</summary>
    public bool Matches(ReadOnlySpan<char> text, bool isFolder = false, long size = 0, long modifiedTicks = 0,
        EntryCounts counts = default)
    {
        if (_root is null) return true;
        var context = new Context(text, isFolder, size, modifiedTicks, counts);
        return _root.Eval(in context);
    }

    /// <summary>Test one text that stands for both the name and the path.</summary>
    public bool Matches(string text, bool isFolder = false, long size = 0, long modifiedTicks = 0,
        EntryCounts counts = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Matches(text.AsSpan(), isFolder, size, modifiedTicks, counts);
    }

    /// <summary>Test an entry's name and full path. Unmarked terms use the path if <paramref name="defaultMatchesPath"/>.</summary>
    public bool Matches(string name, string fullPath, bool defaultMatchesPath, bool isFolder = false,
        long size = 0, long modifiedTicks = 0, EntryCounts counts = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fullPath);
        return Matches(new StringEntry(name, fullPath, isFolder, size, modifiedTicks, counts), defaultMatchesPath);
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
    public static EverythingQuery Parse(string query) => Parse(query, TimeProvider.System);

    /// <summary>
    /// Parse a query. <paramref name="timeProvider"/> fixes "now" for <c>today</c> and spans such as <c>3days</c>,
    /// in its local time zone.
    /// </summary>
    public static EverythingQuery Parse(string query, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (string.IsNullOrWhiteSpace(query)) return new EverythingQuery(null, null);

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

        var binder = new Binder(timeProvider.GetLocalNow().DateTime);
        result.Value.Bind(binder);
        return new EverythingQuery(result.Value, binder);
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
            // The < and > in a function value (size:>10mb) are operators, not group brackets.
            if (char.IsLetter(query[i]) && (i == 0 || char.IsWhiteSpace(query[i - 1]) || query[i - 1] is '|' or '<' or '>' or '!' || AfterModifier(query, i)))
            {
                var length = FunctionLength(query.AsSpan(i));
                if (length > 0)
                {
                    i += length - 1;
                    continue;
                }
            }

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

            if (t.Kind is not (Tok.PathModifier or Tok.NoPathModifier or Tok.RegexModifier or Tok.CaseModifier
                or Tok.DiacriticsModifier)) continue;

            var attached = i + 1 < list.Count
                           && list[i + 1].Position.Absolute == t.Position.Absolute + t.Span.Length
                           && list[i + 1].Kind is Tok.Word or Tok.Phrase or Tok.Open
                               or Tok.PathModifier or Tok.NoPathModifier or Tok.RegexModifier or Tok.CaseModifier
                               or Tok.DiacriticsModifier or Tok.Function;
            if (attached && list[i + 1].Kind == Tok.Function) CheckFunctionAfterModifier(t, list[i + 1]);
            if (!attached)
                throw new EverythingQueryException(
                    $"'{t.ToStringValue()}' must be directly followed by a term, phrase, group or another modifier, with no space",
                    t.Position.Absolute);
        }
    }

    // Only case: and diacritics: may stand in front of a function, and only one that has text to compare (ext:).
    private static void CheckFunctionAfterModifier(Token<Tok> modifier, Token<Tok> function)
    {
        var token = function.ToStringValue();
        IsFunction(token.AsSpan(0, token.IndexOf(':')), out var kind);
        if (modifier.Kind is Tok.CaseModifier or Tok.DiacriticsModifier && SupportsCase(kind)) return;
        throw new EverythingQueryException(
            $"'{modifier.ToStringValue()}' cannot be used in front of '{token[..(token.IndexOf(':') + 1)]}'",
            modifier.Position.Absolute);
    }

    // ---- grammar ----

    private enum Tok { Pipe, Bang, Open, Close, Phrase, Word, PathModifier, NoPathModifier, RegexModifier, CaseModifier, DiacriticsModifier, FileFilter, FolderFilter, Function, Unsupported }

    /// <summary>Which text a term is tested against.</summary>
    private enum Target { Default, Path, Name }

    /// <summary>What one modifier asks of the terms under it; a term keeps what it already has.</summary>
    private readonly record struct Modifier(Target Target, bool Regex, bool Case = false);

    /// <summary>State for the pass after parsing, which needs the clock and collects the regexes.</summary>
    private sealed class Binder(DateTime now)
    {
        public DateTime Now { get; } = now;
        public List<BoundedRegex> Regexes { get; } = [];
    }

    [Flags]
    private enum Needs { None = 0, Properties = 1, ChildCounts = 2, DescendantCounts = 4 }

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
    private static readonly TextParser<Unit> RegexText = Span.EqualToIgnoreCase("regex:").Value(Unit.Value);
    private static readonly TextParser<Unit> CaseText = Span.EqualToIgnoreCase("case:").Value(Unit.Value);
    private static readonly TextParser<Unit> DiacriticsText = Span.EqualToIgnoreCase("diacritics:").Value(Unit.Value);
    private static readonly TextParser<Unit> FileText = Span.EqualToIgnoreCase("file:").Value(Unit.Value);
    private static readonly TextParser<Unit> FolderText = Span.EqualToIgnoreCase("folder:").Value(Unit.Value);

    // '?path:' (global modifier) and the '\:' alias for path: are seen so that they can be refused clearly.
    private static readonly TextParser<Unit> UnsupportedText =
        Character.EqualTo('?').IgnoreThen(NoPathText.Try().Or(PathText))
            .Or(Span.EqualTo("\\:").Value(Unit.Value));

    // A function is one token, name and value together, because its value may start with < or >, which the
    // lexer otherwise reads as group brackets: size:>10mb. A leading run of operator characters belongs to the value.
    private static Result<Unit> FunctionToken(TextSpan input)
    {
        var length = FunctionLength(input.Source.AsSpan(input.Position.Absolute, input.Length));
        return length == 0 ? Result.Empty<Unit>(input) : Result.Value(Unit.Value, input, input.Skip(length));
    }

    // True if the text before index i is one or more modifiers (path:, no-path:, regex:, case:, diacritics:) that start a term, so a
    // function after them is the mistake CheckModifiers reports. A colon inside an ordinary word does not count.
    private static bool AfterModifier(string query, int i)
    {
        while (i > 0 && query[i - 1] == ':')
        {
            var start = i - 1;
            while (start > 0 && (char.IsLetter(query[start - 1]) || query[start - 1] == '-')) start--;
            var name = query.AsSpan(start, i - 1 - start);
            if (!(name.Equals("path", StringComparison.OrdinalIgnoreCase)
                  || name.Equals("no-path", StringComparison.OrdinalIgnoreCase)
                  || name.Equals("regex", StringComparison.OrdinalIgnoreCase)
                  || name.Equals("case", StringComparison.OrdinalIgnoreCase)
                  || name.Equals("diacritics", StringComparison.OrdinalIgnoreCase)))
                return false;
            if (start == 0 || char.IsWhiteSpace(query[start - 1]) || query[start - 1] is '|' or '<' or '>' or '!') return true;
            i = start;
        }

        return false;
    }

    // The length of the function token at the start of text, or 0 if there is none.
    private static int FunctionLength(ReadOnlySpan<char> text)
    {
        var colon = 0;
        while (colon < text.Length && (char.IsLetter(text[colon]) || text[colon] == '-')) colon++;
        if (colon == 0 || colon >= text.Length || text[colon] != ':' || !IsFunction(text[..colon], out _)) return 0;

        var end = colon + 1;
        while (end < text.Length && text[end] is '<' or '>' or '=' or '!') end++;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ('|' or '<' or '>' or '"')) end++;
        return end;
    }

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
        .Match(RegexText, Tok.RegexModifier)
        .Match(CaseText, Tok.CaseModifier)
        .Match(DiacriticsText, Tok.DiacriticsModifier)
        .Match(FileText, Tok.FileFilter)
        .Match(FolderText, Tok.FolderFilter)
        .Match(FunctionToken, Tok.Function)
        .Match(WordText, Tok.Word)
        .Build();

    private static readonly TokenListParser<Tok, Node> Group =
        from open in Token.EqualTo(Tok.Open)
        from inner in Superpower.Parse.Ref(() => Expression)
        from close in Token.EqualTo(Tok.Close)
        select inner;

    private static readonly TokenListParser<Tok, Node> Operand =
        Token.EqualTo(Tok.Word).Select(t => (Node)new Term(t.ToStringValue(), t.Position.Absolute))
            .Or(Token.EqualTo(Tok.Phrase).Select(t =>
            {
                var s = t.ToStringValue();
                return (Node)new Term(s.Substring(1, s.Length - 2), t.Position.Absolute);
            }))
            .Or(Group);

    private static readonly TokenListParser<Tok, Modifier> ModifierToken =
        Token.EqualTo(Tok.PathModifier).Value(new Modifier(Target.Path, false))
            .Or(Token.EqualTo(Tok.NoPathModifier).Value(new Modifier(Target.Name, false)))
            .Or(Token.EqualTo(Tok.RegexModifier).Value(new Modifier(Target.Default, true)))
            .Or(Token.EqualTo(Tok.CaseModifier).Value(new Modifier(Target.Default, false, Case: true)))
            .Or(Token.EqualTo(Tok.DiacriticsModifier).Value(new Modifier(Target.Default, false)));

    // file: and folder: are stand-alone tests on the entry, not modifiers: the text after them is its own term.
    // size: and date-modified: are stand-alone tests too, with their value inside the token.
    private static readonly TokenListParser<Tok, Node> Filter =
        Token.EqualTo(Tok.FileFilter).Select(_ => (Node)new KindFilter(isFolder: false))
            .Or(Token.EqualTo(Tok.FolderFilter).Select(_ => (Node)new KindFilter(isFolder: true)))
            .Or(Token.EqualTo(Tok.Function).Select(t => CreateFunction(t.ToStringValue(), t.Position.Absolute)));

    // A modifier fills in the terms under it that have none yet. Inner groups are built first, so the innermost
    // modifier wins.
    private static readonly TokenListParser<Tok, Node> Modified =
        (from modifiers in ModifierToken.AtLeastOnce()
         from operand in Operand.Or(Filter)
         select ApplyAll(operand, modifiers))
        .Or(Operand)
        .Or(Filter);

    // The modifier nearest the term wins, so apply from the nearest outward; each only fills what is still unset.
    private static Node ApplyAll(Node operand, Modifier[] modifiers)
    {
        for (var i = modifiers.Length - 1; i >= 0; i--) operand.Apply(modifiers[i]);
        return operand;
    }

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
        public readonly long Size; // likewise
        public readonly long ModifiedTicks; // likewise
        public readonly EntryCounts Counts; // likewise
        public readonly Kind Source;

        public Context(ReadOnlySpan<byte> bytes, bool isFolder, long size, long modifiedTicks, EntryCounts counts)
        {
            Bytes = bytes;
            IsFolder = isFolder;
            Size = size;
            ModifiedTicks = modifiedTicks;
            Counts = counts;
            Source = Kind.SingleBytes;
        }

        public Context(ReadOnlySpan<char> chars, bool isFolder, long size, long modifiedTicks, EntryCounts counts)
        {
            Chars = chars;
            IsFolder = isFolder;
            Size = size;
            ModifiedTicks = modifiedTicks;
            Counts = counts;
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

    private sealed class StringEntry(string name, string path, bool isFolder, long size, long modifiedTicks, EntryCounts counts)
        : IEntryChars
    {
        public ReadOnlySpan<char> Name => name;
        public ReadOnlySpan<char> Path => path;
        public bool IsFolder => isFolder;
        public long Size => size;
        public long ModifiedTicks => modifiedTicks;
        public EntryCounts Counts => counts;
    }

    // ---- syntax tree ----

    private abstract class Node
    {
        public abstract bool Eval(in Context context);

        /// <summary>Give every term under this node what it has not been given yet by a nearer modifier. Returns this node.</summary>
        public abstract Node Apply(Modifier modifier);

        /// <summary>The kinds of target the terms under this node have.</summary>
        public abstract Use Uses();

        /// <summary>What the tests under this node need from the entry beyond its text.</summary>
        public virtual Needs Needs => Needs.None;

        /// <summary>The pass after parsing: finish the parts that need the clock, and collect the regexes.</summary>
        public virtual void Bind(Binder binder)
        {
        }
    }

    private sealed class Term : Node
    {
        private readonly string _text;
        private readonly int _position;
        private byte[] _needle; // plain term: the ASCII bytes (lower-cased unless case:), or null if non-ASCII
        private WildcardPattern _wildcard; // null for a plain substring term
        private BoundedRegex _regex; // set by regex:, which wins over wildcards
        private bool _regexRequested;
        private bool _matchCase;
        private Target _target = Target.Default;

        public Term(string text, int position)
        {
            _text = text;
            _position = position;
        }

        // The modifiers are final once parsing is done, so the matcher is built here, not in the constructor.
        public override void Bind(Binder binder)
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

        public override bool Eval(in Context c) => c.Source switch
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

        public override Node Apply(Modifier modifier) => this;
    }

    private sealed class Not(Node inner) : Node
    {
        public override bool Eval(in Context context) => !inner.Eval(in context);

        public override Use Uses() => inner.Uses();

        public override Needs Needs => inner.Needs;

        public override void Bind(Binder binder) => inner.Bind(binder);

        public override Node Apply(Modifier modifier)
        {
            inner.Apply(modifier);
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

        public override Needs Needs
        {
            get
            {
                var needs = Needs.None;
                foreach (var p in parts) needs |= p.Needs;
                return needs;
            }
        }

        public override void Bind(Binder binder)
        {
            foreach (var p in parts) p.Bind(binder);
        }

        public override Node Apply(Modifier modifier)
        {
            foreach (var p in parts) p.Apply(modifier);
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

        public override Needs Needs
        {
            get
            {
                var needs = Needs.None;
                foreach (var p in parts) needs |= p.Needs;
                return needs;
            }
        }

        public override void Bind(Binder binder)
        {
            foreach (var p in parts) p.Bind(binder);
        }

        public override Node Apply(Modifier modifier)
        {
            foreach (var p in parts) p.Apply(modifier);
            return this;
        }
    }
}
