```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method  | Mean     | Error    | StdDev   | Gen0     | Gen1     | Allocated  |
|-------- |---------:|---------:|---------:|---------:|---------:|-----------:|
| Nextorm | 72.55 ms | 0.584 ms | 0.488 ms | 857.1429 | 714.2857 | 1812.87 KB |
| EFCore  | 22.31 ms | 0.216 ms | 0.202 ms | 500.0000 |        - | 1059.04 KB |
| Dapper  | 13.20 ms | 0.071 ms | 0.066 ms |  78.1250 |        - |  188.92 KB |
