```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error   | StdDev   | Ratio | RatioSD | Gen0     | Allocated | Alloc Ratio |
|-------------- |---------:|--------:|---------:|------:|--------:|---------:|----------:|------------:|
| NextormCached | 418.3 μs | 8.32 μs | 14.13 μs |  1.00 |    0.00 | 114.7461 | 234.41 KB |        1.00 |
| Nextorm       | 454.3 μs | 8.76 μs | 23.98 μs |  1.07 |    0.06 | 116.2109 | 237.34 KB |        1.01 |
| Linq          | 278.0 μs | 6.24 μs | 18.41 μs |  0.67 |    0.05 | 114.7461 | 234.52 KB |        1.00 |
