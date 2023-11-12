```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method          | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0      | Allocated | Alloc Ratio |
|---------------- |---------:|---------:|---------:|------:|--------:|----------:|----------:|------------:|
| IterateManual   | 18.37 ms | 0.235 ms | 0.220 ms |  1.13 |    0.02 | 1062.5000 |   2.14 MB |        1.00 |
| AdoWithDelegate | 16.28 ms | 0.260 ms | 0.231 ms |  1.00 |    0.00 | 1062.5000 |   2.14 MB |        1.00 |
