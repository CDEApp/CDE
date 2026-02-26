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
}