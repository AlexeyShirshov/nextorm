```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method              | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCached       | 132.5 μs | 1.57 μs | 1.47 μs |  1.00 |    0.00 | 0.9766 |    2.3 KB |        1.00 |
| NextormToListCached | 135.0 μs | 1.40 μs | 1.25 μs |  1.02 |    0.02 | 1.2207 |   2.62 KB |        1.14 |
| Nextorm             | 151.6 μs | 0.50 μs | 0.42 μs |  1.14 |    0.01 | 1.9531 |   4.26 KB |        1.85 |
| NextormToList       | 155.4 μs | 1.72 μs | 1.61 μs |  1.17 |    0.02 | 2.1973 |   4.58 KB |        1.99 |
| EFCore              | 200.3 μs | 2.13 μs | 1.99 μs |  1.51 |    0.02 | 5.1270 |  10.49 KB |        4.57 |
| Dapper              | 133.6 μs | 1.89 μs | 1.68 μs |  1.01 |    0.02 | 0.7324 |   1.88 KB |        0.82 |
