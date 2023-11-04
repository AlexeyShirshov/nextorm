```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|-------------- |---------:|---------:|---------:|------:|--------:|---------:|---------:|-----------:|------------:|
| NextormCached | 35.56 ms | 0.523 ms | 0.489 ms |  1.00 |    0.00 | 357.1429 | 285.7143 |  827.96 KB |        1.00 |
| Nextorm       | 37.37 ms | 0.338 ms | 0.299 ms |  1.05 |    0.02 | 500.0000 | 214.2857 | 1112.38 KB |        1.34 |
| EFCore        | 22.07 ms | 0.261 ms | 0.244 ms |  0.62 |    0.01 | 500.0000 |        - | 1059.04 KB |        1.28 |
| Dapper        | 13.26 ms | 0.106 ms | 0.099 ms |  0.37 |    0.01 |  78.1250 |        - |  188.92 KB |        0.23 |
