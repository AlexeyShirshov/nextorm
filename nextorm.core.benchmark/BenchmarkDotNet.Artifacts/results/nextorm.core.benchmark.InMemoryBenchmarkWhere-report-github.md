```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error   | StdDev  | Ratio | Gen0     | Allocated | Alloc Ratio |
|-------------- |---------:|--------:|--------:|------:|---------:|----------:|------------:|
| NextormCached | 346.5 μs | 1.97 μs | 1.65 μs |  1.00 | 114.7461 | 234.53 KB |        1.00 |
| Nextorm       | 354.9 μs | 2.91 μs | 2.58 μs |  1.02 | 116.2109 | 237.48 KB |        1.01 |
| Linq          | 215.9 μs | 1.66 μs | 1.47 μs |  0.62 | 114.7461 | 234.52 KB |        1.00 |
