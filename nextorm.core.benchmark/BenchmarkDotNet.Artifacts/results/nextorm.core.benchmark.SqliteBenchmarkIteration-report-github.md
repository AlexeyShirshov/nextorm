```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCompiledAsync  | 134.0 μs | 1.77 μs | 1.57 μs |  1.01 |    0.02 | 0.9766 |   2.13 KB |        0.93 |
| NextormCompiled       | 132.4 μs | 1.82 μs | 1.42 μs |  1.00 |    0.00 | 0.9766 |   2.29 KB |        1.00 |
| NextormCompiledToList | 133.9 μs | 2.09 μs | 1.75 μs |  1.01 |    0.02 | 1.2207 |   2.61 KB |        1.14 |
| NextormCached         | 154.5 μs | 1.50 μs | 1.25 μs |  1.17 |    0.02 | 2.1973 |   4.71 KB |        2.06 |
| NextormCachedToList   | 155.9 μs | 2.58 μs | 2.02 μs |  1.18 |    0.02 | 2.4414 |   5.03 KB |        2.20 |
| EFCore                | 203.0 μs | 2.47 μs | 2.06 μs |  1.53 |    0.02 | 5.1270 |  10.49 KB |        4.58 |
| Dapper                | 137.4 μs | 1.82 μs | 1.71 μs |  1.04 |    0.02 | 0.7324 |   1.88 KB |        0.82 |
