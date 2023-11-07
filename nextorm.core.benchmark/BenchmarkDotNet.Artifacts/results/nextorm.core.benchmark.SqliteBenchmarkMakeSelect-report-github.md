```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method     | Mean     | Error   | StdDev  | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------- |---------:|--------:|--------:|------:|-------:|-------:|----------:|------------:|
| MakeParams | 162.6 μs | 1.44 μs | 1.28 μs |  1.00 | 2.6855 | 2.4414 |   5.75 KB |        1.00 |
| MakeSelect | 165.8 μs | 1.37 μs | 1.28 μs |  1.02 | 2.9297 | 2.6855 |   6.07 KB |        1.06 |
