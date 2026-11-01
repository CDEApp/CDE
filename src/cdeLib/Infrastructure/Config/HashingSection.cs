namespace cdeLib.Infrastructure.Config;

public class HashingSection
{
    public int FirstPassSizeInBytes { get; set; } = 65536;
    public int DegreesOfParallelism { get; set; } = 1;
}