```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCompiled       | 131.6 μs | 0.98 μs | 0.87 μs |  1.00 |    0.00 | 0.9766 |   2.13 KB |        1.00 |
| NextormCompiledToList | 133.7 μs | 1.09 μs | 0.85 μs |  1.02 |    0.01 | 0.9766 |   2.45 KB |        1.15 |
| NextormCompiledFetch  | 206.2 μs | 3.00 μs | 2.81 μs |  1.57 |    0.02 | 1.9531 |   3.99 KB |        1.88 |
| NextormCached         | 152.1 μs | 1.45 μs | 1.29 μs |  1.16 |    0.01 | 2.1973 |   4.49 KB |        2.11 |
| NextormCachedToList   | 154.7 μs | 1.76 μs | 1.56 μs |  1.18 |    0.01 | 2.1973 |   4.81 KB |        2.26 |
| NextormCachedFetch    | 223.1 μs | 2.85 μs | 2.52 μs |  1.70 |    0.02 | 2.9297 |   6.36 KB |        2.99 |
| EFCore                | 201.3 μs | 1.62 μs | 1.44 μs |  1.53 |    0.02 | 5.1270 |  10.49 KB |        4.94 |
| Dapper                | 133.3 μs | 1.48 μs | 1.38 μs |  1.01 |    0.01 | 0.7324 |   1.88 KB |        0.89 |
