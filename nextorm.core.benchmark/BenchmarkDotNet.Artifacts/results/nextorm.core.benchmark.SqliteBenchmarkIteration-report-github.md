```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.113
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| NextormCompiledAsync  | 43.04 μs | 0.782 μs | 0.900 μs |  0.99 |    0.03 | 0.4272 |      - |   2.13 KB |        0.94 |
| NextormCompiled       | 43.24 μs | 0.738 μs | 0.690 μs |  1.00 |    0.00 | 0.4883 |      - |   2.27 KB |        1.00 |
| NextormCompiledToList | 42.00 μs | 0.804 μs | 0.826 μs |  0.97 |    0.03 | 0.4883 |      - |   2.44 KB |        1.08 |
| NextormCached         | 54.79 μs | 0.982 μs | 0.870 μs |  1.27 |    0.03 | 1.0376 |      - |   4.77 KB |        2.11 |
| NextormCachedToList   | 55.33 μs | 0.571 μs | 0.534 μs |  1.28 |    0.02 | 1.0376 |      - |   4.95 KB |        2.18 |
| EFCore                | 87.66 μs | 1.747 μs | 1.869 μs |  2.02 |    0.06 | 2.1973 | 0.4883 |  10.49 KB |        4.63 |
| Dapper                | 44.70 μs | 0.804 μs | 0.752 μs |  1.03 |    0.02 | 0.3662 |      - |   1.88 KB |        0.83 |
