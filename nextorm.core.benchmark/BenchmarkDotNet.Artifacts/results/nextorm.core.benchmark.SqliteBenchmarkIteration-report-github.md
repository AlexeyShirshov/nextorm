```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCompiled       | 131.6 μs | 1.15 μs | 1.07 μs |  1.00 |    0.00 | 0.9766 |   2.13 KB |        1.00 |
| NextormCompiledToList | 135.9 μs | 1.12 μs | 1.04 μs |  1.03 |    0.01 | 0.9766 |   2.45 KB |        1.15 |
| NextormCompiledFetch  | 205.7 μs | 3.99 μs | 4.10 μs |  1.56 |    0.03 | 1.9531 |   3.99 KB |        1.88 |
| NextormCached         | 153.1 μs | 1.53 μs | 1.43 μs |  1.16 |    0.01 | 2.1973 |   4.55 KB |        2.14 |
| NextormCachedToList   | 155.3 μs | 1.26 μs | 1.05 μs |  1.18 |    0.01 | 2.1973 |   4.88 KB |        2.29 |
| NextormCachedFetch    | 236.9 μs | 4.39 μs | 4.32 μs |  1.80 |    0.04 | 2.9297 |   6.42 KB |        3.02 |
| EFCore                | 202.2 μs | 1.51 μs | 1.34 μs |  1.54 |    0.01 | 5.1270 |  10.49 KB |        4.94 |
| Dapper                | 133.6 μs | 2.19 μs | 1.94 μs |  1.01 |    0.02 | 0.7324 |   1.88 KB |        0.89 |
