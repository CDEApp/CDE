using CommandLine;
using JetBrains.Annotations;

namespace cde.CommandLine;

[Verb("update", HelpText = "Update catalog metadata.")]
public class UpdateOptions
{
    [Value(0, HelpText = "Catalog to update")]
    public string FileName { get; [UsedImplicitly] set; }

    [Option("desc", HelpText = "Description to set")]
    public string Description { get; [UsedImplicitly] set; }
}