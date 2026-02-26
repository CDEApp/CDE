```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2


```
| Method                 | IterationCount | Mean        | Error     | StdDev    | Allocated |
|----------------------- |--------------- |------------:|----------:|----------:|----------:|
| &#39;IsSet property check&#39; | 1000           |    233.6 ns |   4.06 ns |   3.60 ns |         - |
| &#39;IsSet property check&#39; | 10000          |  2,347.0 ns |  43.63 ns |  42.85 ns |         - |
| &#39;IsSet property check&#39; | 100000         | 23,302.3 ns | 296.32 ns | 247.44 ns |         - |
