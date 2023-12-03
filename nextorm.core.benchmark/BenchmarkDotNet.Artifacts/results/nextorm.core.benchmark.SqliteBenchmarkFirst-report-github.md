```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------ |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormFirstCompiled          | 129.1 μs | 2.42 μs | 2.38 μs |  1.00 |    0.00 | 0.7324 |   1.94 KB |        1.00 |
| NextormFirstOrDefaultCompiled | 128.3 μs | 2.41 μs | 2.26 μs |  0.99 |    0.03 | 0.7324 |   1.94 KB |        1.00 |
| NextormFirstCached            | 141.0 μs | 1.83 μs | 1.71 μs |  1.09 |    0.03 | 2.1973 |   4.51 KB |        2.33 |
| NextormFirstOrDefaultCached   | 141.5 μs | 2.28 μs | 1.91 μs |  1.09 |    0.03 | 2.1973 |   4.51 KB |        2.33 |
| EFCoreFirst                   | 189.1 μs | 3.76 μs | 4.89 μs |  1.46 |    0.05 | 3.9063 |   8.29 KB |        4.28 |
| EFCoreFirstOrDefault          | 189.1 μs | 3.64 μs | 4.34 μs |  1.47 |    0.04 | 3.9063 |   8.29 KB |        4.28 |
| EFCoreFirstCompiled           | 149.7 μs | 2.82 μs | 2.63 μs |  1.16 |    0.03 | 2.1973 |   4.95 KB |        2.55 |
| EFCoreFirstOrDefaultCompiled  | 151.4 μs | 3.01 μs | 3.81 μs |  1.17 |    0.03 | 2.1973 |   4.95 KB |        2.55 |
| DapperFirst                   | 135.2 μs | 2.53 μs | 2.81 μs |  1.05 |    0.03 | 0.4883 |   1.06 KB |        0.55 |
| DapperFirstOrDefault          | 138.0 μs | 2.58 μs | 4.85 μs |  1.08 |    0.05 | 0.4883 |   1.16 KB |        0.60 |
