```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method          | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0     | Allocated  | Alloc Ratio |
|---------------- |---------:|---------:|---------:|------:|--------:|---------:|-----------:|------------:|
| NextormCompiled | 13.50 ms | 0.141 ms | 0.110 ms |  1.00 |    0.00 |  93.7500 |  194.62 KB |        1.00 |
| NextormCached   | 17.27 ms | 0.218 ms | 0.194 ms |  1.28 |    0.02 | 281.2500 |  582.98 KB |        3.00 |
| EFCore          | 21.93 ms | 0.191 ms | 0.179 ms |  1.62 |    0.02 | 500.0000 | 1061.37 KB |        5.45 |
| Dapper          | 13.19 ms | 0.228 ms | 0.190 ms |  0.98 |    0.02 |  78.1250 |  188.92 KB |        0.97 |
