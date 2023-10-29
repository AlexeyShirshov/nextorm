```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------- |---------:|--------:|--------:|------:|--------:|-------:|-------:|----------:|------------:|
| NextormCached | 128.7 μs | 1.36 μs | 1.21 μs |  1.00 |    0.00 | 0.9766 |      - |   2.21 KB |        1.00 |
| Nextorm       | 393.4 μs | 5.51 μs | 5.15 μs |  3.06 |    0.04 | 4.8828 | 4.3945 |  10.47 KB |        4.74 |
| EFCore        | 200.2 μs | 1.08 μs | 0.96 μs |  1.56 |    0.02 | 5.1270 |      - |  10.49 KB |        4.75 |
| Dapper        | 132.9 μs | 2.10 μs | 1.86 μs |  1.03 |    0.02 | 0.7324 |      - |   1.88 KB |        0.85 |
