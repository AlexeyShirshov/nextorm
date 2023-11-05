```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method              | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCached       | 131.1 μs | 1.88 μs | 1.57 μs |  1.00 |    0.00 | 0.9766 |   2.13 KB |        1.00 |
| NextormToListCached | 133.3 μs | 1.63 μs | 1.28 μs |  1.02 |    0.01 | 0.9766 |   2.45 KB |        1.15 |
| Nextorm             | 152.7 μs | 0.91 μs | 0.81 μs |  1.17 |    0.02 | 1.9531 |   4.45 KB |        2.09 |
| NextormToList       | 155.5 μs | 1.62 μs | 1.52 μs |  1.19 |    0.02 | 2.1973 |   4.77 KB |        2.24 |
| EFCore              | 201.7 μs | 2.17 μs | 2.03 μs |  1.54 |    0.02 | 5.1270 |  10.49 KB |        4.94 |
| Dapper              | 134.2 μs | 1.48 μs | 1.39 μs |  1.02 |    0.02 | 0.7324 |   1.88 KB |        0.89 |
