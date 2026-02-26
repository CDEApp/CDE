# CDE Benchmarks Overview

This document provides a technical overview of the benchmark suite created for the CDE (Catalog Directory Entries) project.

## Project Structure

```
cdeBenchmarks/
├── cdeBenchmarks.csproj         # Project file
├── Program.cs                   # Entry point with BenchmarkSwitcher
├── Hash16Benchmarks.cs          # Hash16 struct performance tests
├── DirEntryBenchmarks.cs        # DirEntry entity operation tests
├── PoolingBenchmarks.cs         # Object pooling efficiency tests
├── README.md                    # Comprehensive documentation
├── QUICKSTART.md                # 2-minute quick start guide
└── BENCHMARKS_OVERVIEW.md       # This file (technical overview)
```

## Benchmark Classes

### 1. Hash16Benchmarks.cs

**Purpose**: Measure the performance impact of converting Hash16 from class to struct.

**Context**: Hash16 is used for millions of file entries. The implementation plan identifies this as the HIGHEST IMPACT optimization (M1), potentially saving 240+ MB for 10M hashed files.

**Benchmarks**:

| Method | Description | Key Metric |
|--------|-------------|------------|
| `CreateFromByteArray` | Create Hash16 from MD5 byte arrays | Allocation: struct should be stack-allocated (0 bytes) |
| `EqualityComparison` | Compare Hash16 instances for equality | Speed: struct equality should be inlined |
| `DictionaryLookup` | Use Hash16 as dictionary keys | Speed: GetHashCode should be inlined |
| `DictionaryInsertion` | Build hash maps for duplicate detection | Memory: struct keys reduce overhead |
| `CheckIsSet` | Check if hash is set (non-zero) | Speed: property access should be inlined |

**BenchmarkDotNet Configuration**:
- `[MemoryDiagnoser]` - Measures allocations and GC collections
- `[Orderer(SummaryOrderPolicy.FastestToSlowest)]` - Sorts results by speed
- `[SimpleJob(RuntimeMoniker.Net90)]` - Targets .NET 9.0
- `[Params(1000, 10000, 100000)]` - Tests at different scales

**Expected Results**:
- Struct creation: 0 bytes allocated (stack allocation)
- Class creation: 40 bytes per instance (heap allocation + object header)
- At 10M files: 400 MB difference

### 2. DirEntryBenchmarks.cs

**Purpose**: Measure performance-critical DirEntry operations called millions of times during catalog operations.

**Context**: DirEntry is the core entity representing files and directories. Operations like comparison and sorting are hot paths during search and display.

**Benchmarks**:

| Method | Description | Hot Path |
|--------|-------------|----------|
| `PathComparison` | Compare paths using Span<char> | Search result sorting |
| `PathComparisonWithDirectory` | Compare with directory priority | Directory-first display |
| `SizeComparison` | Compare by size with directory handling | Large file detection |
| `ModifiedComparison` | Compare by modification date | Time-based sorting |
| `PathSetterWithInterning` | Set path with string interning | Catalog creation |
| `SortBySize` | Full Array.Sort operation | Real-world sorting scenario |
| `CreateDirEntries` | Instantiate DirEntry with pooling | Catalog scanning |

**BenchmarkDotNet Configuration**:
- `[MemoryDiagnoser]` - Tracks allocations in comparison methods
- `[Params(1000, 10000)]` - Tests with realistic entry counts

**Expected Results**:
- Path comparison: 0 allocations (Span-based, no string allocation)
- String interning: Higher initial cost, but memory savings over time
- Sorting: Efficient with sealed classes enabling devirtualization

**Related Optimizations**:
- Recent commits show DirEntry was sealed for devirtualization
- Properties were converted to auto-properties where possible
- Span-based comparison was already implemented

### 3. PoolingBenchmarks.cs

**Purpose**: Measure the effectiveness of object pooling infrastructure in reducing GC pressure.

**Context**: CDE uses BufferPool and CollectionPool extensively to handle billions of entries without excessive allocations. Implementation plan identifies several pooling opportunities (M2, M7).

**Benchmarks**:

| Method | Description | Comparison |
|--------|-------------|------------|
| `BufferPoolRentReturn` | Pool 64KB buffers for file hashing | Baseline for comparison |
| `DirectAllocation` | Allocate buffers without pooling | Shows pooling benefit |
| `DirEntryListPooling` | Pool directory children lists | Catalog building |
| `DirectListAllocation` | Allocate lists without pooling | Comparison baseline |
| `PairDirEntryListPooling` | Pool search result lists | Search operations |
| `StackPooling` | Pool stacks for tree traversal | Tree walking |
| `RealisticBufferUsage` | Simulate file hashing pattern | Real-world scenario |
| `StringListPooling` | Pool string lists for paths | Path manipulation |

**BenchmarkDotNet Configuration**:
- `[MemoryDiagnoser]` - Critical for seeing allocation differences
- `[Benchmark(Baseline = true)]` - BufferPoolRentReturn is baseline
- `[Params(100, 1000, 10000)]` - Tests scaling behavior

**Expected Results**:
- Pooled: 100-1000x less allocation than direct allocation
- After warmup: Near-zero allocations for pooled operations
- Direct allocation: Heavy Gen0 collections

**Key Insight**: The "Allocated" column should show dramatic differences:
```
| BufferPoolRentReturn |  1000 |       128 B | <- Pool overhead only
| DirectAllocation     |  1000 | 64,000,128 B | <- Full allocation
```

## Design Decisions

### Why BenchmarkDotNet?

**Pros**:
- Industry-standard benchmarking framework for .NET
- Automatic warmup, iteration, and statistical analysis
- Memory diagnostics built-in
- Export to multiple formats (HTML, CSV, Markdown)
- Protects against common benchmarking mistakes

**Why Not Custom Benchmarks**:
- CDE already has custom harnesses for integration tests
- BenchmarkDotNet is better for micro/component benchmarks
- Provides statistical confidence intervals
- Handles JIT compilation warmup automatically

### Benchmark Scope

**Included**:
- Micro-benchmarks: Single method/operation measurement
- Component benchmarks: Class-level operations
- Memory allocation patterns

**Not Included** (use custom harnesses instead):
- Integration scenarios (full catalog loading)
- Long-running operations (>30 seconds)
- I/O-heavy operations (file system interaction)
- Multi-process coordination

### Parameterization Strategy

Benchmarks use `[Params]` to test at different scales:

- **Hash16**: 1K, 10K, 100K - Shows how performance scales
- **DirEntry**: 1K, 10K - Realistic search result sizes
- **Pooling**: 100, 1K, 10K - From warm pool to saturation

This reveals:
- O(1) operations remain constant
- O(n) operations scale linearly
- Pool effectiveness at different load levels

## Integration with Implementation Plan

These benchmarks directly support the performance optimization opportunities identified in `implementation_plan.md`:

| Benchmark | Implementation Plan Item | Impact |
|-----------|-------------------------|--------|
| Hash16Benchmarks | M1: Hash16 to struct | 240+ MB savings |
| DirEntryBenchmarks (PathSetter) | A6: Configurable interning | 300 MB vs speed trade-off |
| PoolingBenchmarks (DirEntry) | M2: Pool DirEntry.Children | 38 MB savings |
| PoolingBenchmarks (Stack) | M7: Pool enumerator stacks | 1-5 MB savings |

### Benchmark-Driven Development Workflow

1. **Baseline**: Run benchmarks before optimization
   ```bash
   dotnet run -c Release > baseline.txt
   ```

2. **Implement**: Make the optimization (e.g., seal class, pool objects)

3. **Measure**: Run benchmarks again
   ```bash
   dotnet run -c Release > optimized.txt
   ```

4. **Compare**: Check improvements
   - Memory: Check "Allocated" column
   - Speed: Check "Mean" column
   - GC: Check Gen0/Gen1/Gen2 columns

5. **Validate**: Ensure improvement matches expectations from implementation plan

## Running Benchmarks

### Minimum Command
```bash
dotnet run -c Release
```

### Recommended First Run
```bash
# Start with fastest benchmarks
dotnet run -c Release --filter *Hash16*
```

### Full Suite
```bash
# Run everything (takes 10-20 minutes)
dotnet run -c Release
```

### CI/CD Integration
```bash
# Export to machine-readable format
dotnet run -c Release --exporters json

# Compare against baseline (fail if regression >10%)
dotnet run -c Release --baseline baseline.json --threshold 10
```

## Interpreting Results

### Example Output
```
BenchmarkDotNet v0.14.0

|              Method | IterationCount |      Mean |  Allocated |
|-------------------- |--------------- |----------:|-----------:|
| CreateFromByteArray |          1,000 |   2.45 μs |       48 B |
| CreateFromByteArray |         10,000 |  24.50 μs |      480 B |
| CreateFromByteArray |        100,000 | 245.00 μs |    4,800 B |
```

**Analysis**:
- Linear scaling: 10x iteration count = 10x time
- Consistent allocation: 48 bytes per 1K iterations = stack spill, not heap
- O(n) complexity: As expected for creation

### Red Flags
```
|        Method |      Mean |  Allocated | Gen0  | Gen1 |
|-------------- |----------:|-----------:|------:|-----:|
| BadOperation  | 125.00 ms | 1,024.0 MB | 50.0  | 10.0 |
```

**Problems**:
- Mean in milliseconds (not microseconds) - too slow
- Allocated in MB range - excessive allocations
- Gen0/Gen1 collections - GC pressure

### Good Performance
```
|        Method |     Mean | Allocated | Gen0 |
|-------------- |---------:|----------:|-----:|
| GoodOperation | 2.45 μs  |       0 B |    - |
```

**Indicators**:
- Mean in microseconds - fast
- Zero allocations - pool working
- No GC collections - no pressure

## Extending the Benchmarks

### Adding New Benchmarks

1. Create new class in `cdeBenchmarks/` directory
2. Add BenchmarkDotNet attributes:
   ```csharp
   [MemoryDiagnoser]
   [SimpleJob(RuntimeMoniker.Net90)]
   public class MyNewBenchmarks
   {
       [GlobalSetup]
       public void Setup() { /* ... */ }

       [Benchmark]
       public void MyBenchmark() { /* ... */ }
   }
   ```

3. Run with `--filter *MyNew*`

### Benchmark Best Practices

**DO**:
- Use `[GlobalSetup]` for expensive initialization
- Use `[Params]` to test different scales
- Return benchmark results to prevent dead code elimination
- Use realistic data in setup

**DON'T**:
- Allocate in hot path unless testing allocation
- Use `Console.WriteLine` in benchmarks (affects timing)
- Forget `[MemoryDiagnoser]` when testing allocations
- Run in Debug mode (10-100x slower)

## Future Benchmark Opportunities

Based on implementation plan, potential future benchmarks:

1. **Serialization Benchmarks**
   - MessagePack vs FlatBuffers performance
   - Span<byte> streaming deserialization (A1)
   - Large file buffering strategies (A8)

2. **Tree Traversal Benchmarks**
   - Work-stealing queue efficiency (B7)
   - Recursive vs iterative traversal (A10)
   - Lazy loading cost measurement (A4)

3. **String Operations Benchmarks**
   - Interning strategies (A6, M4)
   - FullPath caching impact (M3)
   - Pattern matching performance (B3, B4)

4. **Memory Layout Benchmarks**
   - Tuple vs ValueTuple (M5)
   - Struct layout optimization
   - Cache line alignment

## References

- [BenchmarkDotNet Official Documentation](https://benchmarkdotnet.org/)
- [CDE Implementation Plan](../../implementation_plan.md)
- [CDE README](../../README.md)
- [Microsoft .NET Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/core/performance/)
