namespace cdeLib.Infrastructure.Config
{
    public interface IConfiguration
    {
        int ProgressUpdateInterval { get; }
        int HashFirstPassSize { get; }
        int DegreesOfParallelism { get; }
    }
}