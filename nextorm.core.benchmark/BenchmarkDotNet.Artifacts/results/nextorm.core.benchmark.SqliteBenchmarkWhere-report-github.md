```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCached | 171.1 μs | 3.39 μs | 4.76 μs |  1.00 |    0.00 | 0.7324 |   1.93 KB |        1.00 |
| Nextorm       | 215.9 μs | 3.91 μs | 5.73 μs |  1.27 |    0.05 | 2.1973 |   4.96 KB |        2.57 |
| EFCore        | 299.3 μs | 5.16 μs | 5.94 μs |  1.75 |    0.06 | 4.8828 |  10.36 KB |        5.37 |
| Dapper        | 177.7 μs | 3.46 μs | 3.71 μs |  1.04 |    0.03 | 0.7324 |    1.9 KB |        0.98 |
