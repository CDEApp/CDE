using System;
using System.Collections.Generic;
using System.Text;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace cdeLib;

// The text of a query to its syntax tree: the tokens, the lexer and the grammar.
public sealed partial class EverythingQuery
{
    private enum Tok { Pipe, Bang, Open, Close, Phrase, Word, PathModifier, NoPathModifier, RegexModifier, CaseModifier, DiacriticsModifier, FileFilter, FolderFilter, Function, Unsupported }


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
}
