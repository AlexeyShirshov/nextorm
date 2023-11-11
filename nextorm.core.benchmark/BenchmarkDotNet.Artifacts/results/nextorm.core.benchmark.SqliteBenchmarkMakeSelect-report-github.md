```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method     | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| MakeParams | 6.251 μs | 0.1769 μs | 0.5217 μs |  1.00 |    0.00 | 0.7782 |    1.6 KB |        1.00 |
| MakeSelect | 7.043 μs | 0.1667 μs | 0.4862 μs |  1.13 |    0.14 | 0.9384 |   1.92 KB |        1.20 |
