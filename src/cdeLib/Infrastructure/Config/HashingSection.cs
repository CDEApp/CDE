namespace cdeLib.Infrastructure.Config
{
    public class HashingSection
    {
        public int FirstPassSizeInBytes { get; set; } = 1024;
        public int DegreesOfParallelism { get; set; } = 1;
    }
}