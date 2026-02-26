# CDE Performance Benchmarks

This project contains BenchmarkDotNet benchmarks for the CDE (Catalog Directory Entries) project. CDE is a high-performance file system cataloging utility that handles billions of entries, so performance optimizations have massive impact.

## Quick Start

```bash
# Run all benchmarks
dotnet run -c Release

# Run specific benchmark class
dotnet run -c Release --filter *Hash16*
dotnet run -c Release --filter *DirEntry*
dotnet run -c Release --filter *Pooling*

# Run specific benchmark method
dotnet run -c Release --filter *Hash16Benchmarks.CreateFromByteArray*
```

### .NET 10 Compatibility Note

This project targets .NET 10.0. BenchmarkDotNet 0.14.0 doesn't officially support .NET 10 yet, but it works by using `[SimpleJob]` without specifying a RuntimeMoniker, which allows BenchmarkDotNet to auto-detect the runtime. The generated boilerplate code will target the current runtime (.NET 10.0.3).

## Benchmark Classes

### 1. Hash16Benchmarks
**Purpose**: Measure Hash16 struct performance (previously a class)

**Key Scenarios**:
- Creating Hash16 from byte arrays (MD5 deserialization)
- Equality comparisons (duplicate detection)
- Dictionary operations (hash-based grouping)
- Memory allocation patterns

**Expected Impact**:
- 240+ MB memory savings for 10M hashed files
- Faster equality checks due to value semantics
- Reduced GC pressure

**Related Implementation Plan Item**: M1 (HIGHEST IMPACT)

### 2. DirEntryBenchmarks
**Purpose**: Measure core entity operations called millions of times

**Key Scenarios**:
- Path comparison (sorting and searching)
- Size-based sorting with directory handling
- Modified date comparisons
- Path property setter with string interning
- Full sort operations

**Expected Impact**:
- Optimized comparisons improve search/sort responsiveness
- String interning trade-off measurement (300MB saved but slower load)

**Related Implementation Plan Items**: A6, B4, B8

### 3. PoolingBenchmarks
**Purpose**: Measure object pooling effectiveness

**Key Scenarios**:
- BufferPool rent/return cycles (file hashing buffers)
- CollectionPool for DirEntry lists (directory children)
- CollectionPool for PairDirEntry lists (search results)
- Stack pooling for tree traversal
- Pooled vs unpooled allocation comparison

**Expected Impact**:
- Reduced GC pressure with billions of entries
- Lower allocation rates during catalog operations
- Better memory locality

**Related Implementation Plan Items**: M2 (Pool DirEntry.Children), M7 (Pool enumerator stacks)

## Understanding Results

### Memory Diagnoser Output
```
|              Method | Mean     | Allocated |
|-------------------- |---------:|----------:|
| BufferPoolRentReturn|  5.234 μs|     128 B | <- Pooled (low allocation)
| DirectAllocation    | 45.678 μs| 640,128 B | <- Not pooled (high allocation)
```

**Key Metrics**:
- **Mean**: Average execution time (lower is better)
- **Allocated**: Total memory allocated (lower is better)
- **Gen0/Gen1/Gen2**: GC collections (lower is better)

### Baseline Comparisons
Benchmarks marked with `[Benchmark(Baseline = true)]` show ratio comparisons:
```
|              Method | Mean  | Ratio |
|-------------------- |------:|------:|
| Baseline            | 100ms |  1.00 |
| Optimized           |  75ms |  0.75 | <- 25% faster
```

### Parameterized Benchmarks
Many benchmarks use `[Params]` to test different scales:
```csharp
[Params(1000, 10000, 100000)]
public int IterationCount { get; set; }
```
This shows how performance scales with input size.

## Performance Context

**From CDE README** (memory usage with real data):

| File/Folder Count | Architecture | Memory Usage | File Size |
|-------------------|--------------|--------------|-----------|
| 8,000 entries     | 32-bit       | 19MB         | 500KB     |
| 500M entries      | 32-bit       | 96MB         | 22MB      |
| 1.5B entries      | 32-bit       | 275MB        | 65MB      |
| 11B entries       | 64-bit       | 2.5GB        | 500MB     |

These benchmarks help optimize operations at this massive scale.

## Best Practices for Running Benchmarks

### ✅ DO
- **Always run in Release mode**: `-c Release` flag is mandatory
- **Close other applications**: Reduce background noise
- **Run multiple iterations**: BenchmarkDotNet does this automatically
- **Check memory diagnostics**: Use `[MemoryDiagnoser]` to verify allocation improvements
- **Compare baselines**: Mark reference implementations with `[Benchmark(Baseline = true)]`

### ❌ DON'T
- **Don't run in Debug mode**: Results will be misleading (10-100x slower)
- **Don't benchmark with debugger attached**: Disables optimizations
- **Don't have console output in benchmarks**: Affects timing
- **Don't use synchronous blocking in async benchmarks**: Skews results
- **Don't measure in methods less than 100ns**: Measurement overhead dominates

## Advanced Usage

### Memory Profiling
```bash
# Export memory allocation details
dotnet run -c Release --filter *Hash16* --memory
```

### Compare Multiple Runtimes
```bash
# Compare .NET 8 vs .NET 9 (requires both SDKs installed)
dotnet run -c Release --runtimes net8.0 net9.0
```

### Export Results
Results are automatically saved to `BenchmarkDotNet.Artifacts/`:
- `results/` - Detailed results in multiple formats (HTML, CSV, Markdown)
- `reports/` - Summary reports

### Custom Configuration
Create `BenchmarkConfig.cs` for custom settings:
```csharp
[Config(typeof(CustomConfig))]
public class MyBenchmarks
{
    private class CustomConfig : ManualConfig
    {
        public CustomConfig()
        {
            AddJob(Job.Default.WithWarmupCount(3).WithIterationCount(10));
            AddExporter(HtmlExporter.Default);
            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }
}
```

## Integration with CI/CD

### Regression Detection
Store baseline results and compare:
```bash
# Save baseline
dotnet run -c Release --exporters json

# Compare against baseline (in CI pipeline)
dotnet run -c Release --baseline baseline-results.json
```

### Performance Gates
Fail build if performance regresses:
```bash
# Exit code 1 if slower than baseline by >10%
dotnet run -c Release --threshold 10
```

## Related Documentation

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [CDE Implementation Plan](../../implementation_plan.md) - See optimization opportunities
- [CDE README](../../README.md) - Project overview and performance characteristics

## Contributing

When adding new benchmarks:

1. **Focus on hot paths**: Benchmark operations called millions of times
2. **Include memory diagnostics**: Add `[MemoryDiagnoser]` attribute
3. **Use realistic data**: Setup should simulate real usage patterns
4. **Document expectations**: Add XML comments explaining expected results
5. **Add baseline comparisons**: Compare optimized vs unoptimized implementations
6. **Use parameterization**: Test different scales with `[Params]`

## Troubleshooting

**Benchmark runs very slowly**
- Ensure you're using `-c Release` mode
- Check if anti-virus is scanning the executable
- Close resource-intensive applications

**Results are inconsistent**
- Run on a less busy machine
- Increase warmup and iteration counts
- Check for thermal throttling on laptops

**Out of memory errors**
- Reduce `[Params]` values
- Run fewer benchmarks at once
- Increase available system memory

**Permission errors**
- Run as administrator (Windows) or with sudo (Linux)
- Check anti-virus exclusions
