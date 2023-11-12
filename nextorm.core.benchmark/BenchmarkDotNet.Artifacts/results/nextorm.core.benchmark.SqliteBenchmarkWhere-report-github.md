```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method          | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0     | Allocated  | Alloc Ratio |
|---------------- |---------:|---------:|---------:|------:|--------:|---------:|-----------:|------------:|
| NextormCompiled | 13.59 ms | 0.119 ms | 0.111 ms |  1.00 |    0.00 |  93.7500 |  194.62 KB |        1.00 |
| NextormCached   | 17.07 ms | 0.202 ms | 0.179 ms |  1.26 |    0.02 | 281.2500 |  604.85 KB |        3.11 |
| EFCore          | 22.18 ms | 0.356 ms | 0.298 ms |  1.63 |    0.02 | 500.0000 | 1061.36 KB |        5.45 |
| Dapper          | 13.32 ms | 0.259 ms | 0.379 ms |  0.99 |    0.04 |  78.1250 |  188.92 KB |        0.97 |
