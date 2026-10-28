using SlimMessageBus;

namespace cdeLib.Hashing;

public class HashCatalogCommand : IRequest
{
    /// <summary>
    /// The one <c>.cdex</c> catalog to hash, or null for every catalog in the current directory and one level down
    /// (see <see cref="HashCatalogTargets"/>).
    /// </summary>
    public string CatalogPath { get; init; }
}
