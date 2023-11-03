```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|-------------- |---------:|---------:|---------:|------:|--------:|---------:|---------:|-----------:|------------:|
| NextormCached | 13.26 ms | 0.138 ms | 0.122 ms |  1.00 |    0.00 |  93.7500 |        - |  208.61 KB |        1.00 |
| Nextorm       | 16.63 ms | 0.181 ms | 0.141 ms |  1.25 |    0.02 | 250.0000 |        - |  523.11 KB |        2.51 |
| EFCore        | 22.28 ms | 0.125 ms | 0.097 ms |  1.68 |    0.02 | 500.0000 | 250.0000 | 1059.04 KB |        5.08 |
| Dapper        | 13.73 ms | 0.271 ms | 0.278 ms |  1.04 |    0.02 |  78.1250 |        - |  188.92 KB |        0.91 |
