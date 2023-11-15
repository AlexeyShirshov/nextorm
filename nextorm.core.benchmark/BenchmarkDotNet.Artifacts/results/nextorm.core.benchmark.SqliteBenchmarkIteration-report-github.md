```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.114
  [Host]     : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2


```
| Method                | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| NextormCompiledAsync  | 43.82 μs | 0.864 μs | 0.888 μs |  1.03 |    0.03 | 0.4272 |      - |   2.13 KB |        0.94 |
| NextormCompiled       | 42.68 μs | 0.814 μs | 0.761 μs |  1.00 |    0.00 | 0.4883 |      - |   2.27 KB |        1.00 |
| NextormCompiledToList | 42.23 μs | 0.822 μs | 0.844 μs |  0.99 |    0.03 | 0.4883 |      - |   2.44 KB |        1.08 |
| NextormCached         | 55.62 μs | 1.078 μs | 1.058 μs |  1.30 |    0.03 | 0.9766 |      - |   4.69 KB |        2.07 |
| NextormCachedToList   | 54.54 μs | 1.018 μs | 0.952 μs |  1.28 |    0.03 | 1.0376 |      - |   4.86 KB |        2.14 |
| EFCore                | 86.85 μs | 1.553 μs | 1.377 μs |  2.03 |    0.05 | 2.1973 | 0.4883 |  10.49 KB |        4.63 |
| EFCoreStream          | 86.53 μs | 1.542 μs | 1.515 μs |  2.03 |    0.06 | 2.1973 | 0.4883 |   10.1 KB |        4.46 |
| EFCoreCompiled        | 64.03 μs | 0.978 μs | 0.915 μs |  1.50 |    0.04 | 1.4648 | 0.4883 |   7.16 KB |        3.16 |
| Dapper                | 43.87 μs | 0.591 μs | 0.524 μs |  1.03 |    0.02 | 0.3662 |      - |   1.88 KB |        0.83 |
| DapperUnbuffered      | 44.45 μs | 0.849 μs | 0.753 μs |  1.04 |    0.03 | 0.3662 |      - |    1.8 KB |        0.80 |
