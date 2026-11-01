using SlimMessageBus;

namespace cdeLib.Catalog;

public record CreateCacheCommand : IRequest
{
    public CreateCacheCommand(string path)
    {
        Path = path;
    }

    public string Path { get; }
    public string Description { get; set; }

    /// <summary>
    /// When false (default), directory reparse points (junctions / symbolic links) are recorded
    /// in the catalog but not descended into, avoiding cycles and duplicate content.
    /// </summary>
    public bool FollowJunctions { get; set; }

    /// <summary>Extra exclude patterns for this scan only (--exclude); they add to the configured and default excludes.</summary>
    public System.Collections.Generic.IReadOnlyList<string> ExcludePaths { get; set; }
}