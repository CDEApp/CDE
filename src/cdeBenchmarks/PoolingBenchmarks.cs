using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Infrastructure;

namespace cdeBenchmarks;

/// <summary>
/// Benchmarks for object pooling infrastructure.
/// Pooling is critical in CDE to reduce GC pressure when handling billions of entries.
///
/// Key scenarios benchmarked:
/// 1. BufferPool rent/return cycles (for file hashing)
/// 2. CollectionPool list operations (for directory children)
/// 3. Pooled vs unpooled allocation patterns
/// 4. Pool contention under parallel load
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob]  // Let BenchmarkDotNet auto-detect runtime (.NET 10)
public class PoolingBenchmarks
{
    private BufferPool _bufferPool = null!;
    private const int BufferSize = 64 * 1024; // Standard buffer size used in CDE

    [Params(100, 1000, 10000)]
    public int OperationCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _bufferPool = new BufferPool();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _bufferPool.Clear();
        CollectionPool.ClearAll();
    }

    /// <summary>
    /// Benchmark: BufferPool rent and return cycles.
    /// This simulates the pattern during file hashing where buffers are rented for reading chunks.
    ///
    /// Expected: Pool should significantly reduce allocations after warmup.
    /// Memory diagnostic will show the difference clearly.
    /// </summary>
    [Benchmark(Baseline = true, Description = "BufferPool rent/return")]
    public byte[] BufferPoolRentReturn()
    {
        byte[]? lastBuffer = null;
        for (var i = 0; i < OperationCount; i++)
        {
            var buffer = _bufferPool.Rent();
            // Simulate some work
            buffer[0] = (byte)i;
            _bufferPool.Return(buffer);
            lastBuffer = buffer;
        }
        return lastBuffer!;
    }

    /// <summary>
    /// Benchmark: Direct byte array allocation (no pooling).
    /// This is the baseline comparison to show pooling benefit.
    ///
    /// Expected: Heavy GC pressure and allocations.
    /// </summary>
    [Benchmark(Description = "Direct allocation (no pooling)")]
    public byte[] DirectAllocation()
    {
        byte[]? lastBuffer = null;
        for (var i = 0; i < OperationCount; i++)
        {
            var buffer = new byte[BufferSize];
            buffer[0] = (byte)i;
            lastBuffer = buffer;
        }
        return lastBuffer!;
    }

    /// <summary>
    /// Benchmark: CollectionPool for DirEntry lists.
    /// This simulates directory child list creation during catalog building.
    ///
    /// Expected: Pool should reduce List allocations significantly.
    /// </summary>
    [Benchmark(Description = "CollectionPool DirEntry lists")]
    public int DirEntryListPooling()
    {
        var totalCount = 0;
        for (var i = 0; i < OperationCount; i++)
        {
            var list = CollectionPool.GetDirEntryList();

            // Simulate adding children
            for (var j = 0; j < 5; j++)
            {
                var entry = new DirEntry(false);
                entry.SetPath($"file_{j}.txt");
                list.Add(entry);
            }

            totalCount += list.Count;
            CollectionPool.ReturnDirEntryList(list);
        }
        return totalCount;
    }

    /// <summary>
    /// Benchmark: Direct List allocation (no pooling).
    /// Comparison baseline for DirEntry list pooling.
    ///
    /// Expected: More allocations and GC pressure.
    /// </summary>
    [Benchmark(Description = "Direct List allocation")]
    public int DirectListAllocation()
    {
        var totalCount = 0;
        for (var i = 0; i < OperationCount; i++)
        {
            var list = new List<DirEntry>(4); // Same capacity as pool

            for (var j = 0; j < 5; j++)
            {
                var entry = new DirEntry(false);
                entry.SetPath($"file_{j}.txt");
                list.Add(entry);
            }

            totalCount += list.Count;
        }
        return totalCount;
    }

    /// <summary>
    /// Benchmark: CollectionPool for PairDirEntry lists.
    /// Used heavily during search and duplicate detection operations.
    ///
    /// Expected: Pool effectiveness for larger lists (capacity 100).
    /// </summary>
    [Benchmark(Description = "CollectionPool PairDirEntry lists")]
    public int PairDirEntryListPooling()
    {
        var totalCount = 0;
        for (var i = 0; i < OperationCount; i++)
        {
            var list = CollectionPool.GetPairDirEntryList();

            // Simulate search results
            var parentEntry = new DirEntry(true);
            parentEntry.SetPath("parent");

            for (var j = 0; j < 20; j++)
            {
                var entry = new DirEntry(false);
                entry.SetPath($"result_{j}.txt");
                var pair = new PairDirEntry(parentEntry, entry);
                list.Add(pair);
            }

            totalCount += list.Count;
            CollectionPool.ReturnPairDirEntryList(list);
        }
        return totalCount;
    }

    /// <summary>
    /// Benchmark: Stack pooling for tree traversal.
    /// Used in EntryHelper for recursive tree operations.
    ///
    /// Expected: Stack pooling reduces allocations during traversal.
    /// </summary>
    [Benchmark(Description = "CollectionPool Stack operations")]
    public int StackPooling()
    {
        var totalCount = 0;
        for (var i = 0; i < OperationCount; i++)
        {
            var stack = CollectionPool.GetCommonEntryStack();

            // Simulate tree traversal
            var root = new DirEntry(true);
            root.SetPath("root");
            stack.Push(root);

            for (var j = 0; j < 10; j++)
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                    totalCount++;

                    // Push more items
                    var child = new DirEntry(false);
                    child.SetPath($"child_{j}");
                    stack.Push(child);
                }
            }

            CollectionPool.ReturnCommonEntryStack(stack);
        }
        return totalCount;
    }

    /// <summary>
    /// Benchmark: Buffer pool with realistic usage pattern.
    /// Simulates reading file data in chunks for hashing.
    ///
    /// Expected: Shows pooling benefit in realistic scenario.
    /// </summary>
    [Benchmark(Description = "Realistic buffer usage pattern")]
    public long RealisticBufferUsage()
    {
        long totalBytes = 0;

        // Simulate hashing multiple files
        for (var fileIndex = 0; fileIndex < OperationCount / 10; fileIndex++)
        {
            // Rent buffer for this file
            var buffer = _bufferPool.Rent();

            // Simulate reading file in chunks
            for (var chunk = 0; chunk < 10; chunk++)
            {
                // Simulate processing data
                for (var i = 0; i < Math.Min(1000, buffer.Length); i++)
                {
                    buffer[i] = (byte)(fileIndex + chunk + i);
                }
                totalBytes += BufferSize;
            }

            // Return buffer when done with file
            _bufferPool.Return(buffer);
        }

        return totalBytes;
    }

    /// <summary>
    /// Benchmark: String list pooling.
    /// Used for path manipulation and result collection.
    ///
    /// Expected: Pool benefits for frequently used string lists.
    /// </summary>
    [Benchmark(Description = "CollectionPool String lists")]
    public int StringListPooling()
    {
        var totalCount = 0;
        for (var i = 0; i < OperationCount; i++)
        {
            var list = CollectionPool.GetStringList();

            for (var j = 0; j < 10; j++)
            {
                list.Add($"path\\to\\file_{j}.txt");
            }

            totalCount += list.Count;
            CollectionPool.ReturnStringList(list);
        }
        return totalCount;
    }
}
