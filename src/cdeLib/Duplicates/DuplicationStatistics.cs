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
            TotalFileBytes = 0;
            BytesProcessed = 0;
            BytesNotProcessed = 0;
            FailedToHash = 0;
            FilesToCheckForDuplicatesCount = 0;
            ListOfDuplicatesProcessed = 0;
            AllreadyDonePartials = 0;
            AllreadyDoneFulls = 0;
            LargestFileSize = 0;
            SmallestFileSize = long.MaxValue;
        }

        public long PartialHashes { get; set; }
        public long FullHashes { get; set; }
        public long TotalFileBytes { get; set; }
        public long BytesProcessed { get; set; }
        public long BytesNotProcessed { get; set; }
        public long FailedToHash { get; set; }
        public long FilesToCheckForDuplicatesCount { get; set; }
        public long ListOfDuplicatesProcessed { get; set; }
        public long AllreadyDonePartials { get; set; }
        public long AllreadyDoneFulls { get; set; }
        public long LargestFileSize { get; private set; }
        public long SmallestFileSize { get; private set; }

        public void SeenFileSize(long value)
        {
            //Console.WriteLine("seen {0}", value);
            LargestFileSize = value > LargestFileSize ? value : LargestFileSize;
            SmallestFileSize = value < SmallestFileSize ? value : SmallestFileSize;
        }

        public long FilesProcessed
        {
            get
            {
                return PartialHashes + FullHashes + AllreadyDonePartials + AllreadyDoneFulls + FailedToHash;
            }
        }
    }
}