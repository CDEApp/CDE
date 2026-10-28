using CommandLine;
using JetBrains.Annotations;

namespace cde.CommandLine;

/// <summary>The output option shared by find, findpath, grep and greppath.</summary>
public abstract class TemplateOptions
{
    [Option("template",
        HelpText = "Format of each match line. Placeholders: {catalog} {fullpath} {filename} {directory} {size} {modified}. " +
                   "{size} takes auto (default), B, KB, MB, GB or TB, e.g. {size:MB}. " +
                   "{modified} takes a format, e.g. {modified:yyyy-MM-dd HH:mm} (yyyy MM dd HH mm ss). " +
                   "Use {{ and }} for literal braces, \\t and \\n for a tab and newline. " +
                   "Default is the full path. Example: --template \"{catalog} {fullpath}\"")]
    public string Template { get; [UsedImplicitly] set; }
}
