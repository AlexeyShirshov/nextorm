```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| NextormCompiledAsync  | 41.65 μs | 0.505 μs | 0.472 μs |  1.02 |    0.02 | 0.4272 |      - |   2.12 KB |        0.94 |
| NextormCompiled       | 40.89 μs | 0.443 μs | 0.415 μs |  1.00 |    0.00 | 0.4883 |      - |   2.26 KB |        1.00 |
| NextormCompiledToList | 40.58 μs | 0.725 μs | 0.678 μs |  0.99 |    0.02 | 0.4883 |      - |   2.39 KB |        1.06 |
| NextormCached         | 48.56 μs | 0.937 μs | 0.877 μs |  1.19 |    0.03 | 1.0986 |      - |   5.45 KB |        2.41 |
| NextormCachedToList   | 48.92 μs | 0.975 μs | 1.043 μs |  1.20 |    0.02 | 1.0986 |      - |   5.58 KB |        2.47 |
| EFCore                | 77.25 μs | 0.954 μs | 0.892 μs |  1.89 |    0.03 | 2.1973 | 0.4883 |  10.53 KB |        4.66 |
| EFCoreAny             | 76.66 μs | 1.189 μs | 1.113 μs |  1.88 |    0.04 | 1.7090 | 0.2441 |   8.78 KB |        3.89 |
| EFCoreStream          | 75.56 μs | 0.943 μs | 0.836 μs |  1.85 |    0.03 | 2.1973 | 0.4883 |  10.14 KB |        4.49 |
| EFCoreCompiled        | 56.39 μs | 0.652 μs | 0.610 μs |  1.38 |    0.02 | 1.5259 | 0.4883 |   7.16 KB |        3.17 |
| Dapper                | 41.66 μs | 0.758 μs | 0.709 μs |  1.02 |    0.02 | 0.3662 |      - |   1.88 KB |        0.83 |
| DapperUnbuffered      | 41.84 μs | 0.577 μs | 0.539 μs |  1.02 |    0.02 | 0.3662 |      - |    1.8 KB |        0.80 |
