```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method                | Job      | Runtime  | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |--------- |--------- |---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| NextormCompiledAsync  | .NET 7.0 | .NET 7.0 | 43.91 μs | 0.656 μs | 0.613 μs |  1.02 |    0.03 | 0.4272 |      - |   2.13 KB |        0.94 |
| NextormCompiled       | .NET 7.0 | .NET 7.0 | 42.95 μs | 0.752 μs | 0.704 μs |  1.00 |    0.00 | 0.4883 |      - |   2.27 KB |        1.00 |
| NextormCompiledToList | .NET 7.0 | .NET 7.0 | 42.41 μs | 0.825 μs | 0.771 μs |  0.99 |    0.02 | 0.4883 |      - |    2.4 KB |        1.06 |
| NextormCached         | .NET 7.0 | .NET 7.0 | 54.80 μs | 1.083 μs | 1.203 μs |  1.27 |    0.03 | 0.9766 |      - |   4.58 KB |        2.02 |
| NextormCachedToList   | .NET 7.0 | .NET 7.0 | 53.62 μs | 0.861 μs | 0.763 μs |  1.25 |    0.03 | 0.9766 |      - |   4.71 KB |        2.08 |
| EFCore                | .NET 7.0 | .NET 7.0 | 89.11 μs | 1.723 μs | 1.915 μs |  2.08 |    0.06 | 2.1973 | 0.4883 |  10.49 KB |        4.63 |
| EFCoreStream          | .NET 7.0 | .NET 7.0 | 90.12 μs | 1.723 μs | 1.527 μs |  2.10 |    0.06 | 2.1973 | 0.4883 |   10.1 KB |        4.46 |
| EFCoreCompiled        | .NET 7.0 | .NET 7.0 | 64.26 μs | 1.125 μs | 0.998 μs |  1.50 |    0.03 | 1.4648 | 0.4883 |   7.16 KB |        3.16 |
| Dapper                | .NET 7.0 | .NET 7.0 | 43.14 μs | 0.805 μs | 0.753 μs |  1.00 |    0.03 | 0.3662 |      - |   1.88 KB |        0.83 |
| DapperUnbuffered      | .NET 7.0 | .NET 7.0 | 43.26 μs | 0.865 μs | 0.809 μs |  1.01 |    0.03 | 0.3662 |      - |    1.8 KB |        0.80 |
| NextormCompiledAsync  | .NET 8.0 | .NET 8.0 | 39.81 μs | 0.792 μs | 0.813 μs |  0.93 |    0.03 | 0.4272 |      - |   2.13 KB |        0.94 |
| NextormCompiled       | .NET 8.0 | .NET 8.0 | 39.80 μs | 0.387 μs | 0.362 μs |  0.93 |    0.01 | 0.4883 |      - |   2.27 KB |        1.00 |
| NextormCompiledToList | .NET 8.0 | .NET 8.0 | 40.30 μs | 0.395 μs | 0.350 μs |  0.94 |    0.02 | 0.4883 |      - |    2.4 KB |        1.06 |
| NextormCached         | .NET 8.0 | .NET 8.0 | 51.58 μs | 0.743 μs | 0.659 μs |  1.20 |    0.03 | 0.9766 |      - |   4.58 KB |        2.02 |
| NextormCachedToList   | .NET 8.0 | .NET 8.0 | 49.95 μs | 0.543 μs | 0.482 μs |  1.16 |    0.02 | 0.9766 |      - |   4.71 KB |        2.08 |
| EFCore                | .NET 8.0 | .NET 8.0 | 75.14 μs | 0.590 μs | 0.552 μs |  1.75 |    0.04 | 2.1973 | 0.4883 |  10.53 KB |        4.65 |
| EFCoreStream          | .NET 8.0 | .NET 8.0 | 74.12 μs | 0.843 μs | 0.704 μs |  1.73 |    0.04 | 2.1973 | 0.4883 |  10.14 KB |        4.48 |
| EFCoreCompiled        | .NET 8.0 | .NET 8.0 | 56.22 μs | 0.921 μs | 0.769 μs |  1.31 |    0.03 | 1.5259 | 0.4883 |   7.16 KB |        3.16 |
| Dapper                | .NET 8.0 | .NET 8.0 | 42.19 μs | 0.767 μs | 0.942 μs |  0.98 |    0.03 | 0.3662 |      - |   1.88 KB |        0.83 |
| DapperUnbuffered      | .NET 8.0 | .NET 8.0 | 44.16 μs | 0.878 μs | 2.251 μs |  1.03 |    0.06 | 0.3662 |      - |    1.8 KB |        0.79 |
