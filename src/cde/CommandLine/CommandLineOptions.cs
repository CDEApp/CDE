using CommandLine;
using JetBrains.Annotations;

// ReSharper disable ClassNeverInstantiated.Global

namespace cde.CommandLine;

[Verb("scan", HelpText = "Scans path and creates a cache file.")]
public class ScanOptions
{
    [Value(0, Required = true, HelpText = "Path to scan")]
    public string Path { get; [UsedImplicitly] set; }

    [Option("desc", HelpText = "Description to set")]
    public string Description { get; [UsedImplicitly] set; }

    [Option("follow-junctions",
        Default = false,
        HelpText = "[DANGER NOT EXTENSIVELY TESTED] Descend into directory junctions / symbolic links. Off by default to avoid cycles and duplicate content.")]
    public bool FollowJunctions { get; [UsedImplicitly] set; }

    [Option("exclude",
        HelpText = "Folder path to skip for this scan only; repeat for several. Adds to the config and default excludes. A trailing /* skips the contents but keeps the folder.")]
    public System.Collections.Generic.IEnumerable<string> Exclude { get; [UsedImplicitly] set; }

    [Option('v', "verbose",
        Default = false,
        HelpText = "List the folders that could not be read (access denied or an IO error) after the scan.")]
    public bool Verbose { get; [UsedImplicitly] set; }
}

[Verb("find",
    HelpText = "Uses all cache files available searches for <string>. See also: cde everything, for Everything-style queries.")]
public class FindOptions : MatchOptions
{
    [Value(0, Required = true, HelpText = "Value to search for")]
    public string Value { get; [UsedImplicitly] set; }
}

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

[Verb("grep", HelpText = "uses all cache files available searches for <regex> as regex match on file name.")]
public class GrepOptions : MatchOptions
{
    [Value(0, Required = true, HelpText = "string to search for")]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("greppath",
    HelpText = "uses all cache files available searches for <regex> as regex match on full path to file name.")]
public class GrepPathOptions : MatchOptions
{
    [Value(0, Required = true, HelpText = "string to search for")]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("findpath",
    HelpText = "uses all cache files available searches for <string> as regex match on full path to file name.")]
public class FindPathOptions : MatchOptions
{
    [Value(0, Required = true, HelpText = "string to search for")]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("replgreppath", HelpText = "<regex>")]
public class ReplGrepPathOptions
{
    [Value(0)]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("replgrep", HelpText = "<regex>")]
public class ReplGrepOptions
{
    [Value(0)]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("replfind", HelpText = "<string>")]
public class ReplFindOptions
{
    [Value(0, HelpText = "Value to search for")]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("migrate",
    HelpText = "Convert MessagePack .cde catalogs to the zero-copy columnar .cdex format (one-way).")]
public class MigrateOptions
{
    [Value(0, Required = false,
        HelpText = "Specific .cde file to convert. If omitted, converts every catalog in the current " +
                   "directory and one level down, writing a .cdex beside each.")]
    public string Path { get; [UsedImplicitly] set; }
}

[Verb("hash", HelpText = "Hash all catalogs in current directory, or one catalog")]
public class HashOptions : MachineOutputOptions
{
    [Value(0, Required = false,
        HelpText = "Specific .cdex catalog to hash. If omitted, hashes every catalog in the current directory " +
                   "and one level down. Only files that share a size with another file in the same catalog are hashed.")]
    public string Path { get; [UsedImplicitly] set; }
}

[Verb("dupes", HelpText = "List all duplicate files (that already have hash computed")]
public class DupesOptions : MachineOutputOptions;

[Verb("treedump1")]
public class TreeDumpOptions;

[Verb("loadwait")]
public class LoadWaitOptions;

[Verb("repl", HelpText = "Start repl console. Enter readline mode")]
public class ReplOptions;

[Verb("PopulousFolders", HelpText = "Show folders with <count> number of files")]
public class PopulousFoldersOptions
{
    [Value(0, Required = true, HelpText = "output folders containing more than <minimumCount> entries.")]
    public int Count { get; [UsedImplicitly] set; }
}
