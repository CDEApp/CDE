using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Hashing;
using Dawn;

namespace cdeLib.Duplicates;

public class Duplication
{
    private readonly IConfiguration _configuration;

    // Reduced pre-allocation to avoid LOH pressure (~262KB each on x64)
    // Dictionary will grow organically to actual size needed
    private readonly Dictionary<ICommonEntry, List<PairDirEntry>> _duplicateFile =
        new(new CommonEntryEqualityComparer());

    private readonly Dictionary<long, List<PairDirEntry>> _duplicateFileSize = new();

    private readonly HashSet<ICommonEntry> _dirEntriesRequiringFullHashing = [];

    protected readonly DuplicationStatistics _duplicationStatistics;

    // Cumulative FilesProcessed at the moment the full-hash phase begins, so that phase can report
    // its own 0-based progress rather than continuing the partial phase's running total.
    private long _processedAtFullHashStart;

    private readonly ILogger _logger;
    private readonly IApplicationDiagnostics _applicationDiagnostics;
    private readonly HashHelper _hashHelper;
    private readonly OperationCancellation _cancellation;

    public Duplication(ILogger logger, IConfiguration configuration, IApplicationDiagnostics applicationDiagnostics,
        OperationCancellation cancellation)
    {
        _logger = logger;
        _hashHelper = new HashHelper(logger);
        _configuration = configuration;
        _applicationDiagnostics = applicationDiagnostics;
        _cancellation = cancellation;
        _duplicationStatistics = new DuplicationStatistics();
        _logger.LogDebug("Dupe Constructor Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
    }

    /// <summary>
    /// Optional callback raised periodically during <see cref="ApplyHash"/> so a UI can render live progress.
    /// Arguments are (filesProcessed, filesToHash, phase). When null the progress is written to the log instead.
    /// </summary>
    public Action<long, long, string> ProgressEvent { get; set; }

    /// <summary>
    /// Optional callback raised with human readable status/summary lines during <see cref="ApplyHash"/>.
    /// When null the message is written to the log instead.
    /// </summary>
    public Action<string> StatusMessageEvent { get; set; }

    private void ReportStatus(string message)
    {
        if (StatusMessageEvent is not null)
            StatusMessageEvent(message);
        else
            _logger.LogInfo(message);
    }

    /// <summary>
    /// Apply an Hash Checksum to all rootEntries
    /// </summary>
    /// <param name="rootEntries">Collection of rootEntries</param>
    public async Task ApplyHash(IList<RootEntry> rootEntries)
    {
        _logger.LogDebug("PrePairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var newMatches = GetSizePairs(rootEntries);
        _logger.LogDebug("PostPairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        // Calculate all aggregations in single pass to avoid multiple enumerations
        long totalFilesInRootEntries = rootEntries.Aggregate<RootEntry, long>(0, (current, entry) => current + entry.FileEntryCount);

        int totalEntriesInSizeDupes = 0;
        int longestListLength = -1;
        long longestListSize = 0;
        foreach (var kvp in newMatches)
        {
            int count = kvp.Value.Count;
            totalEntriesInSizeDupes += count;
            if (count > longestListLength)
            {
                longestListLength = count;
                longestListSize = kvp.Key;
            }
        }
        ReportStatus($"Found {newMatches.Count} sets of files matched by file size");
        ReportStatus($"Total files processed for the file size matches is {totalFilesInRootEntries}");
        ReportStatus($"Total files found with at least 1 other file of same length {totalEntriesInSizeDupes}");
        ReportStatus($"Longest list of same sized files is {longestListLength} for size {longestListSize} ");

        // flatten - optimized without LINQ
        _logger.LogDebug("Flatten List..");
        var flatList = new List<PairDirEntry>(totalEntriesInSizeDupes);
        foreach (var kvp in newMatches)
        {
            flatList.AddRange(kvp.Value);
        }

        _logger.LogDebug("Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        // group by volume/network share
        _logger.LogDebug("GroupBy Volume/Share..");

        // QUOTE 
        // The IGrouping<TKey, TElement> objects are yielded in an order based on 
        // the order of the elements in source that produced the first key of each 
        // IGrouping<TKey, TElement>. Elements in a grouping are yielded in the 
        // order they appear in source.
        //
        // by ordering from largest to smallest the larger files are hashed first
        // so a break of process and then running of dupes is a win for larger files.
        // Optimized sorting without LINQ chains
        flatList.Sort((pde1, pde2) =>
        {
            var size1 = pde1.ChildDE.IsDirectory ? 0 : pde1.ChildDE.Size;
            var size2 = pde2.ChildDE.IsDirectory ? 0 : pde2.ChildDE.Size;
            return size2.CompareTo(size1); // descending
        });

        // Group by directory root without LINQ
        var groupedByDirectoryRoot = new Dictionary<string, List<PairDirEntry>>();
        foreach (var pde in flatList)
        {
            var root = System.IO.Directory.GetDirectoryRoot(pde.FullPath);
            if (!groupedByDirectoryRoot.TryGetValue(root, out var group))
            {
                group = [];
                groupedByDirectoryRoot[root] = group;
            }

            group.Add(pde);
        }

        _logger.LogDebug("Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        // parallel at the grouping level, hopefully this is one group per disk.
        _logger.LogDebug("Begin Hashing...");
        _logger.LogDebug("Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        var timer = new Stopwatch();
        timer.Start();

        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var outerOptions = new ParallelOptions { CancellationToken = token };
        _duplicationStatistics.FilesToCheckForDuplicatesCount = totalEntriesInSizeDupes;

        try
        {
            await Parallel.ForEachAsync(groupedByDirectoryRoot.Values, outerOptions, async (grp, _) =>
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = 2
                };

                // grp is already in descending size order (flatList was sorted before grouping), so
                // processing approximately in list order hashes the largest files first.
                // Hitting break when smallest log displays get down to a size you don't care about is viable.
                // Then the full hash phase will start, and you can hit break again to stop it after a while.
                // to be able to then run --dupes on the larger hashed files.
                // ForEachAsync (unlike the prior async-void ForEach) actually awaits each hash, so this
                // method does not return until every partial hash has been written.
                await Parallel.ForEachAsync(grp, parallelOptions, async (flatFile, _) =>
                {
                    _duplicationStatistics.SeenFileSize(flatFile.ChildDE.Size);
                    await CalculatePartialHashAsync(flatFile.FullPath, flatFile.ChildDE);
                    if (_cancellation.IsCancellationRequested)
                    {
                        Console.WriteLine("\n * Break key detected exiting hashing phase inner.");
                        await cts.CancelAsync();
                    }
                });
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // parallel cancellation. will be OperationCancelled or Aggregate Exception
            _logger.LogException(ex, "Error in {0}", nameof(ApplyHash));
            return;
        }

        ReportStatus("After initial partial hashing phase.");
        var perf =
            $"{_duplicationStatistics.BytesProcessed * (1000.0 / timer.ElapsedMilliseconds) / (1024.0 * 1024.0):F2} MB/s";
        var statsMessage =
            $"FullHash: {_duplicationStatistics.FullHashes}  PartialHash: {_duplicationStatistics.PartialHashes}  Processed: {_duplicationStatistics.BytesProcessed / (1024 * 1024):F2} MB  NotProcessed: {_duplicationStatistics.BytesNotProcessed / (1024 * 1024):F2} MB  Perf: {perf}\nTotal Data Encountered: {_duplicationStatistics.TotalFileBytes / (1024 * 1024):F2} MB\nFailedHash: {_duplicationStatistics.FailedToHash} (almost always because cannot open to read file)";
        ReportStatus(statsMessage);

        _cancellation.Reset(); // require you to press break again to stop the full hash phase.
        CheckDupesAndCompleteFullHash(rootEntries);

        ReportStatus("After hashing completed.");
        timer.Stop();
        perf =
            $"{_duplicationStatistics.BytesProcessed * (1000.0 / timer.ElapsedMilliseconds) / (1024.0 * 1024.0):F2} MB/s";
        statsMessage =
            $"FullHash: {_duplicationStatistics.FullHashes}  PartialHash: {_duplicationStatistics.PartialHashes}  Processed: {_duplicationStatistics.BytesProcessed / (1024 * 1024):F2} MB Perf: {perf}\nFailedHash: {_duplicationStatistics.FailedToHash} (almost always because cannot open to read file)";
        ReportStatus(statsMessage);
        await Task.CompletedTask;
    }

    public IDictionary<long, List<PairDirEntry>> GetSizePairs(IEnumerable<RootEntry> rootEntries)
    {
        EntryHelper.TraverseTreePair(rootEntries, FindMatchesOnFileSize2);
        _logger.LogDebug("Post TraverseMatchOnFileSize: {0}, dupeDictCount {1}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes(), _duplicateFileSize.Count);

        // Remove the single values from the dictionary - optimized without LINQ
        var keysToRemove = CollectionPool.GetLongList();
        try
        {
            foreach (var kvp in _duplicateFileSize)
            {
                if (kvp.Value.Count == 1)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // Remove keys in separate loop to avoid modification during enumeration
            foreach (var key in keysToRemove)
            {
                _duplicateFileSize.Remove(key);
            }
        }
        finally
        {
            CollectionPool.ReturnLongList(keysToRemove);
        }

        _logger.LogDebug("Deleted entries from dictionary: {0}, dupeDictCount {1}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes(), _duplicateFileSize.Count);
        return _duplicateFileSize;
    }

    private bool FindMatchesOnFileSize2(ICommonEntry ce, ICommonEntry de)
    {
        if (de.IsDirectory || de.Size == 0) // || dirEntry.Size < 4096)
        {
            return true;
        }

        // Create persistent PairDirEntry for storage in dictionary
        var flatDirEntry = new PairDirEntry(ce, de);
        if (_duplicateFileSize.TryGetValue(de.Size, out var value))
        {
            value.Add(flatDirEntry);
        }
        else
        {
            // Pre-allocate list with reasonable capacity for similar-sized files
            _duplicateFileSize[de.Size] = new List<PairDirEntry>(capacity: 4) { flatDirEntry };
        }

        return true;
    }

    private async Task CalculatePartialHashAsync(string fullPath, ICommonEntry de)
    {
        if (de.IsDirectory || de.IsHashDone)
        {
            _duplicationStatistics.AllreadyDonePartials++;
            return;
        }

        await CalculateHash(fullPath, de, true);
    }

    private void CheckDupesAndCompleteFullHash(IEnumerable<RootEntry> rootEntries)
    {
        _logger.LogDebug(string.Empty);
        _logger.LogDebug("Checking duplicates and completing full hash.");
        var commonEntries = rootEntries as RootEntry[] ?? rootEntries.ToArray();
        EntryHelper.TraverseTreePair(commonEntries, BuildDuplicateListIncludePartialHash);

        // Optimized duplicate detection without LINQ
        var foundDupes = new List<KeyValuePair<ICommonEntry, List<PairDirEntry>>>();
        var totalEntriesInDupes = 0;
        var longestListLength = 0;

        foreach (var kvp in _duplicateFile)
        {
            if (kvp.Value.Count > 1)
            {
                foundDupes.Add(kvp);
                totalEntriesInDupes += kvp.Value.Count;
                if (kvp.Value.Count > longestListLength)
                {
                    longestListLength = kvp.Value.Count;
                }
            }
        }

        ReportStatus($"Found {foundDupes.Count} duplication collections.");
        ReportStatus($"Total files found with at least 1 other file duplicate {totalEntriesInDupes}");
        ReportStatus($"Longest list of duplicate files is {longestListLength}");

        // Switch progress reporting to the full-hash phase: FilesProcessed is cumulative across both
        // passes, so capture a baseline here and re-target the denominator to this phase's own work.
        // Each phase then reports its own 0..100% progress (the phase label distinguishes them) and the
        // percentage can never exceed 100% as it did when the partial-pass denominator was reused.
        _processedAtFullHashStart = _duplicationStatistics.FilesProcessed;
        _duplicationStatistics.FilesToCheckForDuplicatesCount = totalEntriesInDupes;

        // Populate HashSet with entries requiring full hash
        foreach (var kvp in foundDupes)
        {
            var entries = kvp.Value;
            for (int i = 0; i < entries.Count; i++)
            {
                _dirEntriesRequiringFullHashing.Add(entries[i].ChildDE);
            }
        }

        // Process full hashing using async pattern (same as partial hash phase)
        ProcessFullHashAsync(foundDupes).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Process full hash calculations asynchronously for duplicate entries.
    /// Uses same pattern as partial hash phase to avoid blocking async calls.
    /// </summary>
    private async Task ProcessFullHashAsync(List<KeyValuePair<ICommonEntry, List<PairDirEntry>>> foundDupes)
    {
        if (foundDupes.Count == 0)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = 2
        };

        try
        {
            // Flatten the list of entries requiring full hashing
            var entriesToHash = new List<PairDirEntry>();
            foreach (var kvp in foundDupes)
            {
                entriesToHash.AddRange(kvp.Value);
            }

            // Process in parallel, awaiting each hash. The previous AsParallel().ForAll(async ...)
            // launched async-void lambdas that ForAll did not await, so this method could return (and
            // the catalog could be saved) while full hashes were still being computed - leaving entries
            // flagged as partial-only and breaking duplicate detection. ForEachAsync awaits completion.
            await Parallel.ForEachAsync(entriesToHash, parallelOptions, async (pde, _) =>
            {
                var dirEntry = pde.ChildDE;

                // Skip if already has full hash
                if (dirEntry.IsHashDone && !dirEntry.IsPartialHash)
                {
                    return;
                }

                // Only hash entries that are in the duplicate set
                if (_dirEntriesRequiringFullHashing.Contains(dirEntry))
                {
                    var fullPath = pde.FullPath;
                    await CalculateHash(fullPath, dirEntry, false);

                    if (_cancellation.IsCancellationRequested)
                    {
                        _logger.LogInfo("Break key detected, exiting full hash phase.");
                        await cts.CancelAsync();
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Full hash phase cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Error in {0}", nameof(ProcessFullHashAsync));
        }
    }

    private async Task CalculateHash(string fullPath, ICommonEntry de, bool doPartialHash)
    {
        var displayCounterInterval = _configuration.ProgressUpdateInterval > 1000
            ? _configuration.ProgressUpdateInterval / 10
            : _configuration.ProgressUpdateInterval;
        Guard.Argument(displayCounterInterval, nameof(displayCounterInterval)).GreaterThan(0);
        if (doPartialHash)
        {
            // don't recalculate.
            if (de.IsHashDone && de.IsPartialHash)
            {
                return;
            }

            var hashResponse = await _hashHelper.GetHashResponseFromFile(fullPath, _configuration.HashFirstPassSize);

            if (hashResponse != null)
            {
                de.SetHash(hashResponse.Hash);
                de.IsPartialHash = hashResponse.IsPartialHash;
                _duplicationStatistics.BytesProcessed += hashResponse.BytesHashed;
                _duplicationStatistics.TotalFileBytes += de.Size;
                _duplicationStatistics.BytesNotProcessed +=
                    de.Size <= hashResponse.BytesHashed ? 0 : de.Size - hashResponse.BytesHashed;
                if (de.IsPartialHash)
                    _duplicationStatistics.PartialHashes++;
                else
                    _duplicationStatistics.FullHashes++;
                if (_duplicationStatistics.FilesProcessed % displayCounterInterval == 0)
                {
                    if (ProgressEvent is not null)
                    {
                        ProgressEvent(_duplicationStatistics.FilesProcessed,
                            _duplicationStatistics.FilesToCheckForDuplicatesCount, "Partial hash");
                    }
                    else
                    {
                        _logger.LogInfo(
                            "Progress through duplicate files at {0} of {1} which is {2:F2}% Largest {3:F2} MB, Smallest {4:F2} MB",
                            _duplicationStatistics.FilesProcessed, _duplicationStatistics.FilesToCheckForDuplicatesCount,
                            100 * (1.0 * _duplicationStatistics.FilesProcessed /
                                   _duplicationStatistics.FilesToCheckForDuplicatesCount),
                            1.0 * _duplicationStatistics.LargestFileSize / (1024 * 1024),
                            1.0 * _duplicationStatistics.SmallestFileSize / (1024 * 1024));
                    }
                }
            }
            else
            {
                _duplicationStatistics.FailedToHash += 1;
            }
        }
        else
        {
            if (de.IsHashDone && !de.IsPartialHash)
            {
                _duplicationStatistics.AllreadyDoneFulls++;
                return;
            }

            var hashResponse = await _hashHelper.GetHashResponseFromFile(fullPath, null);
            if (hashResponse != null)
            {
                de.SetHash(hashResponse.Hash);
                de.IsPartialHash = hashResponse.IsPartialHash;
                _duplicationStatistics.FullHashes += 1;
                _duplicationStatistics.BytesProcessed += hashResponse.BytesHashed;
                if (_duplicationStatistics.FilesProcessed % displayCounterInterval == 0)
                {
                    // Report progress relative to the start of the full-hash phase so it reads 0..100%.
                    var processedThisPhase = _duplicationStatistics.FilesProcessed - _processedAtFullHashStart;
                    if (ProgressEvent is not null)
                    {
                        ProgressEvent(processedThisPhase,
                            _duplicationStatistics.FilesToCheckForDuplicatesCount, "Full hash");
                    }
                    else
                    {
                        _logger.LogInfo("Progress through duplicate files at {0} of {1} which is {2:.0}%",
                            processedThisPhase, _duplicationStatistics.FilesToCheckForDuplicatesCount,
                            100 * (1.0 * processedThisPhase /
                                   _duplicationStatistics.FilesToCheckForDuplicatesCount));
                    }
                }
            }
            else
            {
                _duplicationStatistics.FailedToHash += 1;
            }
        }
    }


    private bool BuildDuplicateListIncludePartialHash(ICommonEntry parentEntry, ICommonEntry dirEntry)
    {
        if (dirEntry.IsDirectory || !dirEntry.IsHashDone || dirEntry.Size == 0)
        {
            // TODO: how to deal with not calculated files?
            return true;
        }

        // Create persistent PairDirEntry for storage in dictionary
        var info = new PairDirEntry(parentEntry, dirEntry);
        if (_duplicateFile.TryGetValue(dirEntry, out var value))
        {
            value.Add(info);
        }
        else
        {
            // Pre-allocate list with reasonable capacity for duplicates
            _duplicateFile[dirEntry] = new List<PairDirEntry>(capacity: 2) { info };
        }

        return true;
    }

    private bool BuildDuplicateList(ICommonEntry parentEntry, ICommonEntry dirEntry)
    {
        if (!dirEntry.IsPartialHash)
        {
            BuildDuplicateListIncludePartialHash(parentEntry, dirEntry);
        }

        return true;
    }

    public void FindDuplicates(IEnumerable<RootEntry> rootEntries)
    {
        var dupePairs = GetDupePairs(rootEntries);

        // Sort by descending size without LINQ
        dupePairs.Sort((kvp1, kvp2) => kvp2.Key.Size.CompareTo(kvp1.Key.Size));

        foreach (var dupe in dupePairs)
        {
            _logger.LogInfo("-------------------------------------- {0}", dupe.Key.Size);
            var entries = dupe.Value;
            foreach (var t in entries)
            {
                Console.WriteLine("{0}", t.FullPath);
            }
        }
    }

    public List<KeyValuePair<ICommonEntry, List<PairDirEntry>>> GetDupePairs(IEnumerable<RootEntry> rootEntries)
    {
        EntryHelper.TraverseTreePair(rootEntries, BuildDuplicateList);

        // Optimized filtering without LINQ
        var moreThanOneFile = _duplicateFile.Where(kvp => kvp.Value.Count > 1).ToList();

        _logger.LogInfo("Count of list of all hashes of files with same sizes {0}", _duplicateFile.Count);
        _logger.LogInfo("Count of list of all hashes of files with same sizes where more than 1 of that hash {0}",
            moreThanOneFile.Count);
        return moreThanOneFile;
    }
}