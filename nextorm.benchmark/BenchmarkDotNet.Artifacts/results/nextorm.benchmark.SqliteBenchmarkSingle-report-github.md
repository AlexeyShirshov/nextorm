```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                         | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| NextormSingleCompiled          | 31.52 μs | 0.610 μs | 0.627 μs |  1.00 |    0.00 | 0.2441 |      - |   1.23 KB |        1.00 |
| NextormSingleOrDefaultCompiled | 36.65 μs | 0.529 μs | 0.495 μs |  1.16 |    0.02 | 0.2441 |      - |   1.23 KB |        1.00 |
| DapperSingle                   | 38.91 μs | 0.665 μs | 0.622 μs |  1.23 |    0.03 | 0.1831 |      - |   1.05 KB |        0.85 |
| DapperSingleOrDefault          | 39.60 μs | 0.615 μs | 0.576 μs |  1.25 |    0.03 | 0.2441 |      - |   1.15 KB |        0.94 |
| NextormSingleCached            | 41.70 μs | 0.834 μs | 1.054 μs |  1.33 |    0.04 | 0.8545 |      - |   4.41 KB |        3.59 |
| NextormSingleOrDefaultCached   | 42.40 μs | 0.834 μs | 1.142 μs |  1.33 |    0.04 | 0.8545 |      - |   4.41 KB |        3.59 |
| EFCoreSingleCompiled           | 51.40 μs | 0.468 μs | 0.415 μs |  1.63 |    0.03 | 1.0376 | 0.3052 |   4.97 KB |        4.05 |
| EFCoreSingleOrDefaultCompiled  | 51.99 μs | 0.464 μs | 0.434 μs |  1.65 |    0.04 | 1.0376 | 0.3052 |   4.97 KB |        4.05 |
| EFCoreSingle                   | 82.47 μs | 1.463 μs | 1.368 μs |  2.61 |    0.07 | 2.1973 | 0.4883 |   10.3 KB |        8.40 |
| EFCoreSingleOrDefault          | 82.55 μs | 1.618 μs | 2.103 μs |  2.63 |    0.07 | 2.1973 | 0.4883 |   10.3 KB |        8.40 |
