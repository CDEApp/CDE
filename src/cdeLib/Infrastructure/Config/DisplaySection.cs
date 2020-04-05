namespace cdeLib.Infrastructure.Config
{
    public class DisplaySection
    {
        public int ProgressUpdateInterval { get; set; } = 5000;
        public bool ConsoleLogToSeq { get; set; } = true;
    }
}