using cdeLib.Output;
using SlimMessageBus;

namespace cdeLib.Duplicates;

public sealed class FindDuplicatesCommand : IRequest
{
    /// <summary>Where the duplicate files go as records (<c>--json</c> / <c>--csv</c>); null writes their paths to the console.</summary>
    public IRecordWriter Output { get; init; }
}
