```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error   | StdDev  | Median   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|--------:|--------:|---------:|------:|--------:|-------:|----------:|------------:|
| NextormCached | 135.5 μs | 0.72 μs | 0.60 μs | 135.6 μs |  1.00 |    0.00 | 0.9766 |   2.22 KB |        1.00 |
| Nextorm       | 157.6 μs | 0.65 μs | 0.58 μs | 157.6 μs |  1.16 |    0.01 | 1.9531 |   4.33 KB |        1.95 |
| EFCore        | 210.5 μs | 4.16 μs | 7.72 μs | 207.2 μs |  1.54 |    0.04 | 5.1270 |  10.49 KB |        4.73 |
| Dapper        | 137.8 μs | 0.95 μs | 0.84 μs | 137.9 μs |  1.02 |    0.01 | 0.7324 |   1.88 KB |        0.85 |
