```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCached | 135.9 μs | 1.58 μs | 1.32 μs |  1.00 |    0.00 | 0.9766 |    2.2 KB |        1.00 |
| Nextorm       | 160.0 μs | 1.32 μs | 1.17 μs |  1.18 |    0.01 | 1.9531 |   4.27 KB |        1.94 |
| EFCore        | 206.3 μs | 0.77 μs | 0.64 μs |  1.52 |    0.02 | 5.1270 |  10.49 KB |        4.76 |
| Dapper        | 138.1 μs | 1.95 μs | 1.82 μs |  1.02 |    0.02 | 0.7324 |   1.88 KB |        0.85 |
