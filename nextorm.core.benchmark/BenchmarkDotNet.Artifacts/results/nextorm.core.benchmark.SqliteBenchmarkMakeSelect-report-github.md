```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method     | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| MakeParams | 4.000 μs | 0.0769 μs | 0.0944 μs |  1.00 |    0.00 | 0.8011 |   1.64 KB |        1.00 |
| MakeSelect | 4.400 μs | 0.0608 μs | 0.0539 μs |  1.10 |    0.03 | 0.9537 |   1.96 KB |        1.20 |
