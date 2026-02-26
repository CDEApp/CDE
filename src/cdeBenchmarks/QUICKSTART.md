# Quick Start Guide - CDE Benchmarks

This is a 2-minute quick start to get you running benchmarks immediately.

## Run All Benchmarks

```bash
cd src/cdeBenchmarks
dotnet run -c Release
```

This will:
- Compile in Release mode (required for accurate results)
- Run all three benchmark classes
- Save results to `BenchmarkDotNet.Artifacts/results/`
- Display summary table in console

Expected runtime: 5-15 minutes for all benchmarks.

## Run Specific Benchmarks

```bash
# Only Hash16 benchmarks (fastest, ~2 minutes)
dotnet run -c Release --filter *Hash16*

# Only DirEntry benchmarks (~3 minutes)
dotnet run -c Release --filter *DirEntry*

# Only Pooling benchmarks (~4 minutes)
dotnet run -c Release --filter *Pooling*
```

## Understanding the Output

### Console Output
```
BenchmarkDotNet v0.14.0
Running benchmarks...

|              Method | IterationCount |      Mean |    Allocated |
|-------------------- |--------------- |----------:|-------------:|
| CreateFromByteArray |           1000 |   2.45 μs |         48 B |
| EqualityComparison  |           1000 |   0.95 μs |          0 B |
```

**Key Columns**:
- **Method**: Benchmark name
- **IterationCount**: Parameter value (size of test data)
- **Mean**: Average execution time (lower is better)
  - μs = microseconds (0.000001 seconds)
  - ms = milliseconds (0.001 seconds)
- **Allocated**: Total memory allocated (lower is better)
  - B = bytes
  - KB = kilobytes
  - MB = megabytes

### Memory Allocation Example
```
| BufferPoolRentReturn |  1000 |   5.2 μs |       128 B | <- Good (pooled)
| DirectAllocation     |  1000 | 45.7 μs | 64,000,128 B | <- Bad (not pooled)
```

The pooled version allocates 500,000x less memory!

## What Gets Benchmarked?

### 1. Hash16Benchmarks
**Why**: Hash16 was converted from class to struct. This measures the impact.

**Key Tests**:
- Creating hashes from byte arrays
- Comparing hashes for equality
- Using hashes as dictionary keys

**Expected Results**: Struct version should use ~40% less memory per hash.

### 2. DirEntryBenchmarks
**Why**: DirEntry operations happen millions of times during catalog operations.

**Key Tests**:
- Path comparisons (sorting)
- Size comparisons (finding large files)
- Date comparisons
- String interning cost

**Expected Results**: Span-based comparisons should be very fast with minimal allocations.

### 3. PoolingBenchmarks
**Why**: Object pooling is critical to reduce GC pressure with billions of entries.

**Key Tests**:
- Buffer pooling vs direct allocation
- List pooling vs direct allocation
- Stack pooling for tree traversal

**Expected Results**: Pooled versions should allocate 100-1000x less memory.

## Interpreting Results

### Good Performance Indicators
- Mean time in microseconds (μs) for hot paths
- Zero allocations for pooled operations after warmup
- Consistent performance across parameter values

### Performance Problems
- Mean time in milliseconds (ms) - might be too slow
- High allocation counts (MB range)
- Gen0/Gen1/Gen2 collections happening frequently

## Next Steps

1. **View detailed results**: Check `BenchmarkDotNet.Artifacts/results/` folder
2. **Compare before/after**: Run benchmarks before and after code changes
3. **Export to HTML**: Results include HTML reports with charts
4. **Read full README**: See `README.md` for advanced usage

## Troubleshooting

**"Please run as Administrator"**
- BenchmarkDotNet needs elevated permissions on Windows
- Right-click terminal and "Run as Administrator"

**"Build failed" errors**
- Make sure you're in the `src/cdeBenchmarks` directory
- Ensure `cdeLib` project builds successfully first

**Very slow performance**
- Did you use `-c Release`? Debug mode is 10-100x slower
- Close other applications to reduce noise
- Unplug laptop to avoid thermal throttling

**Inconsistent results**
- BenchmarkDotNet runs multiple warmup and actual iterations
- If results vary widely, your system is too busy
- Close background applications and try again

## Example Session

```bash
cd src/cdeBenchmarks

# Quick test - run Hash16 benchmarks only
dotnet run -c Release --filter *Hash16*

# Output:
# BenchmarkDotNet v0.14.0
# Running 5 benchmarks...
# [████████████████████] 100%
#
# |              Method | IterationCount |      Mean | Allocated |
# |-------------------- |--------------- |----------:|----------:|
# | CreateFromByteArray |           1000 |   2.45 μs |      48 B |
# | EqualityComparison  |           1000 |   0.95 μs |       0 B |
# | DictionaryLookup    |           1000 |  12.34 μs |       0 B |
#
# Results saved to: BenchmarkDotNet.Artifacts/results/

# View detailed results
cd BenchmarkDotNet.Artifacts/results
ls
# Hash16Benchmarks-report.html  <- Open this in browser for charts
# Hash16Benchmarks-report.csv   <- Import into Excel
# Hash16Benchmarks-report.md    <- Markdown for documentation
```

## Performance Expectations

Based on typical modern hardware (3+ GHz CPU):

| Benchmark Category | Expected Mean Time | Expected Allocation |
|-------------------|-------------------|---------------------|
| Hash creation | 1-5 μs | 0-48 B |
| Path comparison | 0.5-2 μs | 0 B |
| Pooled operations | 2-10 μs | 0-128 B |
| Unpooled operations | 10-100 μs | 64+ KB |

If your results are significantly different, investigate:
- Are you in Release mode?
- Is your CPU throttling?
- Are background processes consuming resources?

## Getting Help

See the full `README.md` for:
- Detailed benchmark explanations
- Advanced BenchmarkDotNet features
- Integration with CI/CD
- Performance regression detection
- Custom benchmark configuration
