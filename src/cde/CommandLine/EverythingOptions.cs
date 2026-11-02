using CommandLine;
using JetBrains.Annotations;

// ReSharper disable ClassNeverInstantiated.Global

namespace cde.CommandLine;

[Verb("everything",
    HelpText = "Searches all catalogs with an Everything-style query (https://www.voidtools.com/support/everything/search_syntax/). " +
               "Space is AND, | is OR, ! is NOT, < > groups, \"...\" is a literal phrase. " +
               "path:term matches the full path and no-path:term the name; a plain term matches the name. " +
               "file: and folder: keep files or folders only (the default is both). " +
               "The query alone decides all of this, so there are no regex, path, files or folders options. " +
               "Quote the query, since < > | and ! mean something to a shell. " +
               "The query matches first, then --filter, then --template formats each line. " +
               "Example: cde everything \"folder:music <flac | mp3>\"")]
public class EverythingOptions : MatchOptions
{
    [Value(0, Required = true, HelpText = "Everything-style query, in quotes")]
    public string Value { get; [UsedImplicitly] set; }
}
