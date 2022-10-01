﻿using CommandLine;
using JetBrains.Annotations;

// ReSharper disable ClassNeverInstantiated.Global

namespace cde.CommandLine;

[Verb("scan", HelpText = "Scans path and creates a cache file.")]
public class ScanOptions
{
    [Value(0, HelpText = "Path to scan")]
    public string Path { get; [UsedImplicitly] set; }

    [Option("desc", HelpText = "Description to set")]
    public string Description { get; [UsedImplicitly] set; }
}

[Verb("find", HelpText = "Uses all cache files available searches for <string>")]
public class FindOptions
{
    [Value(0, Required = true, HelpText = "Value to search for")]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("grep", HelpText = "uses all cache files available searches for <regex> as regex match on file name.")]
public class GrepOptions
{
    [Value(0, Required = true, HelpText = "string to search for")]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("greppath",
    HelpText = "uses all cache files available searches for <regex> as regex match on full path to file name.")]
public class GrepPathOptions
{
    [Value(0, Required = true, HelpText = "string to search for")]
    public string Value { get; [UsedImplicitly] set; }
}

[Verb("findpath",
    HelpText = "uses all cache files available searches for <string> as regex match on full path to file name.")]
public class FindPathOptions
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

[Verb("hash", HelpText = "Hash all catalogs in current directory")]
public class HashOptions
{
}

[Verb("dupes", HelpText = "List all duplicate files (that already have hash computed")]
public class DupesOptions
{
}

[Verb("treedump1")]
public class TreeDumpOptions
{
}

[Verb("loadwait")]
public class LoadWaitOptions
{
}

[Verb("repl", HelpText = "Start repl console. Enter readline mode")]
public class ReplOptions
{
}

[Verb("PopulousFolders", HelpText = "Show folders with <count> number of files")]
public class PopulousFoldersOptions
{
    [Value(0, Required = true, HelpText = "output folders containing more than <minimumCount> entries.")]
    public int Count { get; [UsedImplicitly] set; }
}

[Verb("upgrade", HelpText = "Upgrade catalogues to v4 structure")]
public class UpgradeOptions
{
}