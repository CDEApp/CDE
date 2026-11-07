using SlimMessageBus;

namespace cdeLib.Hashing;

public class HashCatalogCommand : IRequest
{
    /// <summary>
    /// The one <c>.cdex</c> catalog to hash, or null for every catalog in the current directory and one level down
    /// (see <see cref="HashCatalogTargets"/>).
    /// </summary>
    public string CatalogPath { get; init; }

    /// <summary>Where each file hashed goes as a record (<c>--json</c> / <c>--csv</c>); null writes nothing.</summary>
    public Output.IRecordWriter Output { get; init; }

    /// <summary>Which files to hash (<c>--filter</c>, <c>--include-unique-sizes</c>); not saved.</summary>
    public HashSelection Selection { get; init; } = HashSelection.All;
}
