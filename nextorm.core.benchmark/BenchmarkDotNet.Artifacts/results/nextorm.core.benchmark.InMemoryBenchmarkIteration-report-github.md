```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error   | StdDev   | Ratio | RatioSD | Gen0     | Allocated | Alloc Ratio |
|-------------- |---------:|--------:|---------:|------:|--------:|---------:|----------:|------------:|
| NextormCached | 371.4 μs | 7.97 μs | 22.62 μs |  1.00 |    0.00 | 114.7461 | 234.41 KB |        1.00 |
| Nextorm       | 392.5 μs | 7.78 μs | 16.58 μs |  1.05 |    0.08 | 115.7227 | 236.33 KB |        1.01 |
| Linq          | 231.1 μs | 4.61 μs | 12.15 μs |  0.62 |    0.05 | 114.7461 | 234.45 KB |        1.00 |
