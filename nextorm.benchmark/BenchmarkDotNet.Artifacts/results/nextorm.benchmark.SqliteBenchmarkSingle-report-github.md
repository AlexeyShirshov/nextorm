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
| DapperSingleOrDefault          | 40.06 μs | 0.801 μs | 0.890 μs |  0.89 |    0.03 | 0.2441 |      - |   1.15 KB |        0.60 |
| DapperSingle                   | 40.95 μs | 0.818 μs | 1.795 μs |  0.89 |    0.03 | 0.1831 |      - |   1.05 KB |        0.55 |
| NextormSingleCompiled          | 44.96 μs | 0.536 μs | 0.501 μs |  1.00 |    0.00 | 0.3662 |      - |   1.91 KB |        1.00 |
| NextormSingleOrDefaultCompiled | 45.55 μs | 0.900 μs | 1.036 μs |  1.01 |    0.03 | 0.3662 |      - |   1.91 KB |        1.00 |
| EFCoreSingleCompiled           | 53.10 μs | 0.908 μs | 0.849 μs |  1.18 |    0.02 | 1.0376 | 0.3052 |   4.97 KB |        2.61 |
| EFCoreSingleOrDefaultCompiled  | 54.41 μs | 1.052 μs | 0.984 μs |  1.21 |    0.02 | 1.0376 | 0.3052 |   4.97 KB |        2.61 |
| NextormSingleCached            | 57.42 μs | 1.143 μs | 1.404 μs |  1.28 |    0.04 | 1.0986 |      - |   5.09 KB |        2.67 |
| NextormSingleOrDefaultCached   | 57.73 μs | 1.147 μs | 1.645 μs |  1.30 |    0.04 | 1.0986 |      - |   5.09 KB |        2.67 |
| EFCoreSingleOrDefault          | 82.54 μs | 1.646 μs | 1.761 μs |  1.84 |    0.06 | 2.1973 | 0.4883 |   10.6 KB |        5.56 |
| EFCoreSingle                   | 84.11 μs | 1.660 μs | 2.100 μs |  1.87 |    0.06 | 2.1973 | 0.4883 |  10.31 KB |        5.41 |
