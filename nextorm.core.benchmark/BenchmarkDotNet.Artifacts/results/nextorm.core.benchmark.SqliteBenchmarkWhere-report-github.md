```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0     | Allocated  | Alloc Ratio |
|-------------- |---------:|---------:|---------:|------:|--------:|---------:|-----------:|------------:|
| NextormCached | 13.27 ms | 0.108 ms | 0.096 ms |  1.00 |    0.00 |  93.7500 |   208.6 KB |        1.00 |
| Nextorm       | 16.94 ms | 0.217 ms | 0.203 ms |  1.28 |    0.02 | 250.0000 |  523.11 KB |        2.51 |
| EFCore        | 22.66 ms | 0.186 ms | 0.165 ms |  1.71 |    0.02 | 500.0000 | 1082.48 KB |        5.19 |
| Dapper        | 13.43 ms | 0.091 ms | 0.076 ms |  1.01 |    0.01 |  78.1250 |  188.91 KB |        0.91 |
