```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method              | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCached       | 138.6 μs | 2.38 μs | 2.11 μs |  1.00 |    0.00 | 0.9766 |   2.21 KB |        1.00 |
| NextormToListCached | 144.1 μs | 2.81 μs | 4.21 μs |  1.04 |    0.03 | 1.2207 |   2.53 KB |        1.14 |
| Nextorm             | 158.4 μs | 3.03 μs | 2.53 μs |  1.14 |    0.03 | 1.9531 |   4.32 KB |        1.95 |
| NextormToList       | 160.3 μs | 1.39 μs | 1.23 μs |  1.16 |    0.02 | 2.1973 |   4.64 KB |        2.10 |
| EFCore              | 204.8 μs | 1.71 μs | 1.51 μs |  1.48 |    0.03 | 5.1270 |  10.49 KB |        4.75 |
| Dapper              | 137.0 μs | 1.74 μs | 1.36 μs |  0.99 |    0.02 | 0.7324 |   1.88 KB |        0.85 |
