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

/// <summary>
/// An Everything-style (voidtools) search query, parsed once so testing an entry does no parsing. Supported:
/// space is AND, <c>|</c> is OR, <c>!</c> is NOT (it applies to the next term, group or phrase), <c>&lt; &gt;</c>
/// group, <c>"..."</c> is a literal phrase (operators inside quotes are plain text), and the modifiers
/// <c>path:</c> (match the full path), <c>no-path:</c> (match the name only) and <c>regex:</c> (the text is a
/// regular expression, case-insensitive, matching anywhere in the text, so use a phrase for one with a space or
/// <c>|</c>), <c>case:</c> (match case; terms, wildcards and <c>regex:</c> ignore case otherwise) and
/// <c>diacritics:</c> (accepted for Everything compatibility; diacritics always count here, so it changes nothing).
/// The functions are <c>size:</c>, <c>date-modified:</c> (<c>dm:</c>), <c>ext:</c>, <c>empty:</c>, the child and
/// descendant counts and <c>dupe:</c>; see <see cref="UsesProperties"/>, <see cref="UsesCounts"/> and
/// <see cref="DuplicateSpecs"/> for what a caller must hand to <c>Matches</c> for them.
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
        DuplicateSpecs = binder?.DupeSpecs.ToArray() ?? [];
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

    /// <summary>
    /// The dupe: functions of the query, equal ones once. If there are any, count their keys over all the catalogs
    /// (<see cref="DuplicateIndex"/>) before scanning, and pass each entry's <see cref="DuplicateIndex.MaskOf"/> to
    /// <c>Matches</c>; bit k answers <c>DuplicateSpecs[k]</c>.
    /// </summary>
    public IReadOnlyList<DupeSpec> DuplicateSpecs { get; }

    /// <summary>
    /// Say which catalog is about to be searched, by its file name on disk. Terms after <c>catalog:</c> are tested
    /// against it, and the answer is the same for every entry, so call this once before testing the entries of that
    /// catalog. Until it is called the file name is empty.
    /// </summary>
    public void ForCatalog(string fileName) => _root?.SetCatalog(fileName ?? string.Empty);

    /// <summary>
    /// True when, for the catalog set by <see cref="ForCatalog"/>, no entry can match, whatever the entry is: a
    /// <c>catalog:</c> term that fails on the file name inside an AND. The caller can skip the catalog without scanning.
    /// </summary>
    public bool MatchesNothing => _root?.Constant == false;

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
        EntryCounts counts = default, ulong duplicates = 0)
    {
        if (_root is null) return true;
        var context = new Context(utf8, isFolder, size, modifiedTicks, counts, duplicates);
        return _root.Eval(in context);
    }

    /// <summary>Test one text that stands for both the name and the path; identical to the UTF-8 overload.</summary>
    public bool Matches(ReadOnlySpan<char> text, bool isFolder = false, long size = 0, long modifiedTicks = 0,
        EntryCounts counts = default, ulong duplicates = 0)
    {
        if (_root is null) return true;
        var context = new Context(text, isFolder, size, modifiedTicks, counts, duplicates);
        return _root.Eval(in context);
    }

    /// <summary>Test one text that stands for both the name and the path.</summary>
    public bool Matches(string text, bool isFolder = false, long size = 0, long modifiedTicks = 0,
        EntryCounts counts = default, ulong duplicates = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Matches(text.AsSpan(), isFolder, size, modifiedTicks, counts, duplicates);
    }

    /// <summary>Test an entry's name and full path. Unmarked terms use the path if <paramref name="defaultMatchesPath"/>.</summary>
    public bool Matches(string name, string fullPath, bool defaultMatchesPath, bool isFolder = false,
        long size = 0, long modifiedTicks = 0, EntryCounts counts = default, ulong duplicates = 0)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fullPath);
        return Matches(new StringEntry(name, fullPath, isFolder, size, modifiedTicks, counts, duplicates), defaultMatchesPath);
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
        CheckTargetModifiers(list);

        for (var i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t.Kind == Tok.Unsupported)
                throw new EverythingQueryException(
                    "The '?' global modifier and the '\\:' alias are not supported yet; use path: or no-path: in front of a term",
                    t.Position.Absolute);

            if (!IsModifier(t.Kind)) continue;

            var attached = i + 1 < list.Count
                           && list[i + 1].Position.Absolute == t.Position.Absolute + t.Span.Length
                           && list[i + 1].Kind is Tok.Word or Tok.Phrase or Tok.Open
                               or Tok.PathModifier or Tok.NoPathModifier or Tok.RegexModifier or Tok.CatalogModifier
                               or Tok.CaseModifier or Tok.DiacriticsModifier or Tok.Function;
            if (attached && list[i + 1].Kind == Tok.Function) CheckFunctionAfterModifier(t, list[i + 1]);
            if (!attached)
                throw new EverythingQueryException(
                    $"'{t.ToStringValue()}' must be directly followed by a term, phrase, group or another modifier, with no space",
                    t.Position.Absolute);
        }
    }

    private static bool IsModifier(Tok kind) => kind is Tok.PathModifier or Tok.NoPathModifier or Tok.RegexModifier
        or Tok.CatalogModifier or Tok.CaseModifier or Tok.DiacriticsModifier;

    // A term is tested against its name, its path or its catalog's file name, never two of them. So catalog: cannot
    // share a run of attached modifiers with path: or no-path:, in either order.
    private static void CheckTargetModifiers(List<Token<Tok>> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (!IsModifier(list[i].Kind)) continue;
            var first = -1;
            var hasCatalog = false;
            var hasPath = false;
            var j = i;
            while (true)
            {
                var kind = list[j].Kind;
                if (kind == Tok.CatalogModifier) hasCatalog = true;
                if (kind is Tok.PathModifier or Tok.NoPathModifier) hasPath = true;
                if (first < 0 && (kind is Tok.CatalogModifier or Tok.PathModifier or Tok.NoPathModifier)) first = j;
                var next = j + 1 < list.Count
                           && list[j + 1].Position.Absolute == list[j].Position.Absolute + list[j].Span.Length
                           && IsModifier(list[j + 1].Kind);
                if (!next) break;
                j++;
            }

            if (hasCatalog && hasPath)
                throw new EverythingQueryException(
                    "'catalog:' cannot be combined with 'path:' or 'no-path:': a term is tested against the name, the path or the catalog file name",
                    list[first].Position.Absolute);
            i = j;
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
}
