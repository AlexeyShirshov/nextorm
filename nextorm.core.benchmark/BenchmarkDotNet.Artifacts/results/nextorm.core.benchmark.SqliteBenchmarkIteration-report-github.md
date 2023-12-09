```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3636/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                       | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCompiledAsync         | 133.7 μs | 2.15 μs | 2.80 μs |  1.01 |    0.03 | 0.9766 |   2.12 KB |        0.94 |
| NextormCompiled              | 132.4 μs | 2.54 μs | 2.71 μs |  1.00 |    0.00 | 0.9766 |   2.26 KB |        1.00 |
| NextormCompiledToList        | 131.9 μs | 2.64 μs | 2.59 μs |  1.00 |    0.03 | 0.9766 |   2.48 KB |        1.10 |
| NextormCompiledManualToList  | 132.4 μs | 2.17 μs | 2.03 μs |  1.00 |    0.03 | 0.9766 |   2.48 KB |        1.10 |
| NextormCached                | 144.1 μs | 2.52 μs | 2.11 μs |  1.09 |    0.03 | 2.1973 |   4.55 KB |        2.01 |
| NextormCachedToList          | 144.3 μs | 1.77 μs | 1.65 μs |  1.09 |    0.02 | 2.1973 |   4.77 KB |        2.11 |
| NextormManualSQLCachedToList | 144.8 μs | 2.15 μs | 2.01 μs |  1.09 |    0.03 | 2.1973 |   4.89 KB |        2.17 |
| EFCore                       | 193.7 μs | 3.78 μs | 4.20 μs |  1.46 |    0.05 | 4.8828 |  10.53 KB |        4.66 |
| EFCoreAny                    | 192.0 μs | 3.66 μs | 4.77 μs |  1.45 |    0.05 | 3.9063 |   8.78 KB |        3.89 |
| EFCoreStream                 | 190.6 μs | 3.21 μs | 3.00 μs |  1.44 |    0.04 | 4.8828 |  10.14 KB |        4.49 |
| EFCoreCompiled               | 161.4 μs | 1.92 μs | 1.79 μs |  1.22 |    0.03 | 3.4180 |   7.16 KB |        3.17 |
| Dapper                       | 137.6 μs | 1.57 μs | 1.47 μs |  1.04 |    0.02 | 0.7324 |   1.91 KB |        0.85 |
| DapperUnbuffered             | 137.7 μs | 2.07 μs | 1.73 μs |  1.04 |    0.03 | 0.7324 |   1.83 KB |        0.81 |
