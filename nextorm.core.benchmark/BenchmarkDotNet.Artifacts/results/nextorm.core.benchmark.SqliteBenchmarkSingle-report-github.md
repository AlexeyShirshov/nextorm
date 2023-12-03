```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                         | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| NextormSingleCompiled          | 128.7 μs |  2.53 μs |  2.24 μs | 128.7 μs |  1.00 |    0.00 | 0.7324 |   1.99 KB |        1.00 |
| NextormSingleOrDefaultCompiled | 128.5 μs |  2.51 μs |  2.79 μs | 129.1 μs |  1.00 |    0.04 | 0.7324 |   1.99 KB |        1.00 |
| NextormSingleCached            | 146.1 μs |  2.28 μs |  2.13 μs | 146.4 μs |  1.13 |    0.03 | 2.4414 |   5.45 KB |        2.74 |
| NextormSingleOrDefaultCached   | 154.3 μs |  3.32 μs |  9.48 μs | 151.5 μs |  1.15 |    0.04 | 2.4414 |   5.45 KB |        2.74 |
| EFCoreSingle                   | 205.0 μs |  3.98 μs |  5.84 μs | 203.7 μs |  1.61 |    0.06 | 4.8828 |  10.31 KB |        5.17 |
| EFCoreSingleOrDefault          | 256.0 μs | 15.42 μs | 45.48 μs | 243.8 μs |  1.62 |    0.07 | 4.8828 |  10.62 KB |        5.33 |
| EFCoreSingleCompiled           | 156.3 μs |  2.87 μs |  7.25 μs | 153.8 μs |  1.25 |    0.06 | 2.1973 |   4.97 KB |        2.49 |
| EFCoreSingleOrDefaultCompiled  | 150.0 μs |  2.10 μs |  1.96 μs | 150.6 μs |  1.17 |    0.03 | 2.1973 |   4.97 KB |        2.49 |
| DapperSingle                   | 134.2 μs |  1.99 μs |  1.86 μs | 134.7 μs |  1.04 |    0.03 | 0.4883 |   1.08 KB |        0.54 |
| DapperSingleOrDefault          | 141.9 μs |  3.51 μs |  9.78 μs | 138.8 μs |  1.13 |    0.09 | 0.4883 |   1.18 KB |        0.59 |
