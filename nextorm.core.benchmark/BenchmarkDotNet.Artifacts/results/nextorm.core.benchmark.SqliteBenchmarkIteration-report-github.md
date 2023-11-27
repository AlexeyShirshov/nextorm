```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| NextormCompiledAsync  | 42.55 μs | 0.491 μs | 0.459 μs |  1.02 |    0.02 | 0.4272 |      - |   2.12 KB |        0.94 |
| NextormCompiled       | 41.93 μs | 0.820 μs | 0.767 μs |  1.00 |    0.00 | 0.4883 |      - |   2.26 KB |        1.00 |
| NextormCompiledToList | 41.56 μs | 0.829 μs | 0.815 μs |  0.99 |    0.02 | 0.4883 |      - |   2.39 KB |        1.06 |
| NextormCached         | 49.53 μs | 0.973 μs | 1.195 μs |  1.18 |    0.03 | 1.0986 |      - |   5.29 KB |        2.34 |
| NextormCachedToList   | 48.78 μs | 0.821 μs | 0.977 μs |  1.17 |    0.03 | 1.0986 |      - |   5.42 KB |        2.40 |
| EFCore                | 79.10 μs | 1.511 μs | 1.740 μs |  1.88 |    0.05 | 2.1973 | 0.4883 |  10.53 KB |        4.66 |
| EFCoreAny             | 77.50 μs | 1.533 μs | 1.574 μs |  1.85 |    0.06 | 1.7090 | 0.2441 |   8.78 KB |        3.89 |
| EFCoreStream          | 77.23 μs | 1.455 μs | 1.215 μs |  1.85 |    0.05 | 2.1973 | 0.4883 |  10.14 KB |        4.49 |
| EFCoreCompiled        | 58.44 μs | 1.114 μs | 1.144 μs |  1.40 |    0.04 | 1.5259 | 0.4883 |   7.16 KB |        3.17 |
| Dapper                | 42.36 μs | 0.814 μs | 0.969 μs |  1.01 |    0.03 | 0.3662 |      - |   1.88 KB |        0.83 |
| DapperUnbuffered      | 42.50 μs | 0.721 μs | 0.674 μs |  1.01 |    0.02 | 0.3662 |      - |    1.8 KB |        0.80 |
