```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0     | Allocated | Alloc Ratio |
|-------------- |---------:|--------:|--------:|------:|--------:|---------:|----------:|------------:|
| NextormCached | 323.2 μs | 3.39 μs | 2.83 μs |  1.00 |    0.00 | 114.7461 | 234.57 KB |        1.00 |
| Nextorm       | 639.8 μs | 5.29 μs | 4.42 μs |  1.98 |    0.02 | 118.1641 |  242.3 KB |        1.03 |
| Linq          | 188.8 μs | 2.53 μs | 2.12 μs |  0.58 |    0.01 | 114.7461 | 234.45 KB |        1.00 |
