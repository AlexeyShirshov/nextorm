```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3636/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------ |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormFirstCompiled          | 129.4 μs | 2.01 μs | 1.78 μs |  1.00 |    0.00 | 0.9766 |   2.01 KB |        1.00 |
| NextormLargeFirstCompiled     | 135.8 μs | 2.39 μs | 2.24 μs |  1.05 |    0.01 | 0.9766 |   2.47 KB |        1.23 |
| NextormFirstOrDefaultCompiled | 129.3 μs | 2.00 μs | 1.87 μs |  1.00 |    0.01 | 0.9766 |   2.01 KB |        1.00 |
| NextormFirstCached            | 141.1 μs | 2.64 μs | 2.71 μs |  1.09 |    0.02 | 1.9531 |   4.05 KB |        2.02 |
| NextormFirstOrDefaultCached   | 144.0 μs | 2.31 μs | 5.39 μs |  1.15 |    0.07 | 1.9531 |   4.05 KB |        2.02 |
| EFCoreFirst                   | 184.8 μs | 3.53 μs | 3.31 μs |  1.43 |    0.04 | 3.9063 |   8.29 KB |        4.13 |
| EFCoreFirstOrDefault          | 186.2 μs | 2.19 μs | 1.95 μs |  1.44 |    0.03 | 3.9063 |   8.29 KB |        4.13 |
| EFCoreFirstCompiled           | 149.7 μs | 2.65 μs | 2.48 μs |  1.16 |    0.02 | 2.1973 |   4.95 KB |        2.46 |
| EFCoreLargeFirstCompiled      | 155.0 μs | 1.89 μs | 1.48 μs |  1.20 |    0.02 | 2.4414 |   5.27 KB |        2.62 |
| EFCoreFirstOrDefaultCompiled  | 149.4 μs | 2.65 μs | 2.35 μs |  1.15 |    0.02 | 2.1973 |   4.95 KB |        2.46 |
| DapperFirst                   | 131.8 μs | 1.84 μs | 1.72 μs |  1.02 |    0.01 | 0.4883 |   1.07 KB |        0.53 |
| DapperLargeFirst              | 159.2 μs | 3.17 μs | 8.89 μs |  1.16 |    0.10 | 0.4883 |   1.48 KB |        0.74 |
| DapperFirstOrDefault          | 156.0 μs | 3.00 μs | 3.68 μs |  1.20 |    0.03 | 0.4883 |   1.17 KB |        0.58 |
