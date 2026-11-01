using System;
using System.Collections.Generic;
using cdeLib.Output;
using CommandLine;
using JetBrains.Annotations;

namespace cde.CommandLine;

/// <summary>The output and filter options shared by find, findpath, grep and greppath.</summary>
public abstract class MatchOptions : MachineOutputOptions
{
    public override OutputFormat ResolveFormat()
    {
        var format = base.ResolveFormat();
        if (format != OutputFormat.Text && !string.IsNullOrEmpty(Template))
            throw new FormatException("--template sets the text lines and cannot be used with --json or --csv.");
        return format;
    }

    [Option("template",
        HelpText = "Format of each match line. Placeholders: {catalog} {fullpath} {filename} {directory} {size} {modified}. " +
                   "{size} takes auto (default), B, KB, MB, GB or TB, e.g. {size:MB}. " +
                   "{modified} takes a format, e.g. {modified:yyyy-MM-dd HH:mm} (yyyy MM dd HH mm ss). " +
                   "Use {{ and }} for literal braces, \\t and \\n for a tab and newline. " +
                   "Default is the full path. Example: --template \"{catalog} {fullpath}\"")]
    public string Template { get; [UsedImplicitly] set; }

    [Option("filter",
        HelpText = "Only show matches that pass this filter; repeat it to require all of them. " +
                   "{size:<op><value>} with an operator of < <= > >= = or !=, and a value such as 4MB or 1.5KB " +
                   "(B KB MB GB TB, 1024 per step; no unit is bytes). " +
                   "Quote it, since < and > redirect in a shell. Example: --filter \"{size:>10KB}\" --filter \"{size:<4MB}\"")]
    public IEnumerable<string> Filter { get; [UsedImplicitly] set; }
}
