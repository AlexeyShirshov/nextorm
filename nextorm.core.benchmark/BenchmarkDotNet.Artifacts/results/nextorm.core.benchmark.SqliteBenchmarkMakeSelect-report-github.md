```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method     | Mean       | Error    | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------- |-----------:|---------:|----------:|------:|--------:|-------:|----------:|------------:|
| MakeParams |   999.2 ns | 17.39 ns |  15.42 ns |  1.00 |    0.00 | 0.4311 |     904 B |        1.00 |
| MakeSelect | 4,406.1 ns | 88.11 ns | 131.88 ns |  4.42 |    0.15 | 0.9537 |    2008 B |        2.22 |
