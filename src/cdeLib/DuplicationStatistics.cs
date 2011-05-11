namespace cdeLib
{
    /// <summary>
    /// Simple statistics holder for Duplication
    /// </summary>
    public class DuplicationStatistics
    {
        public DuplicationStatistics()
        {
            PartialHashes = 0;
            FullHashes = 0;
            BytesProcessed = 0;
            FailedToHash = 0;
        }

        public long PartialHashes { get; set; }
        public long FullHashes { get; set; }
        public long BytesProcessed { get; set; }
        public long FailedToHash { get; set; }
    }
}