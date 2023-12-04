```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------ |---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| NextormFirstCompiled          | 39.33 μs | 0.553 μs | 0.517 μs |  1.00 |    0.00 | 0.4272 |      - |   2.01 KB |        1.00 |
| NextormFirstOrDefaultCompiled | 39.59 μs | 0.490 μs | 0.434 μs |  1.01 |    0.01 | 0.4272 |      - |   2.01 KB |        1.00 |
| NextormFirstCached            | 47.89 μs | 0.743 μs | 0.695 μs |  1.22 |    0.02 | 0.9766 |      - |   4.56 KB |        2.27 |
| NextormFirstOrDefaultCached   | 46.84 μs | 0.900 μs | 1.170 μs |  1.19 |    0.03 | 0.9766 |      - |   4.56 KB |        2.27 |
| EFCoreFirst                   | 72.88 μs | 1.443 μs | 1.603 μs |  1.85 |    0.05 | 1.7090 | 0.2441 |   8.29 KB |        4.13 |
| EFCoreFirstOrDefault          | 73.51 μs | 1.393 μs | 1.303 μs |  1.87 |    0.04 | 1.7090 | 0.2441 |   8.29 KB |        4.13 |
| EFCoreFirstCompiled           | 51.85 μs | 0.607 μs | 0.538 μs |  1.32 |    0.02 | 1.0376 | 0.3052 |   4.95 KB |        2.46 |
| EFCoreFirstOrDefaultCompiled  | 52.28 μs | 0.904 μs | 0.846 μs |  1.33 |    0.02 | 1.0376 | 0.3052 |   4.95 KB |        2.46 |
| DapperFirst                   | 42.06 μs | 0.644 μs | 0.603 μs |  1.07 |    0.02 | 0.1831 |      - |   1.06 KB |        0.53 |
| DapperFirstOrDefault          | 43.37 μs | 0.846 μs | 0.830 μs |  1.10 |    0.02 | 0.2441 |      - |   1.16 KB |        0.58 |
