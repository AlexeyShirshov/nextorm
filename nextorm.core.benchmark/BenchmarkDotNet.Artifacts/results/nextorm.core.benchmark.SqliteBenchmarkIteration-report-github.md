```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method              | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| NextormCached       | 135.1 μs | 0.96 μs | 0.90 μs |  1.00 |    0.00 | 0.9766 |   2.27 KB |        1.00 |
| NextormToListCached | 134.7 μs | 0.87 μs | 0.81 μs |  1.00 |    0.01 | 1.2207 |   2.59 KB |        1.14 |
| Nextorm             | 154.7 μs | 1.38 μs | 1.22 μs |  1.14 |    0.01 | 1.9531 |   4.23 KB |        1.86 |
| NextormToList       | 157.5 μs | 2.10 μs | 1.76 μs |  1.17 |    0.02 | 2.1973 |   4.55 KB |        2.00 |
| EFCore              | 203.4 μs | 1.05 μs | 0.87 μs |  1.51 |    0.01 | 5.1270 |  10.49 KB |        4.62 |
| Dapper              | 136.8 μs | 1.60 μs | 1.41 μs |  1.01 |    0.01 | 0.7324 |   1.88 KB |        0.83 |
