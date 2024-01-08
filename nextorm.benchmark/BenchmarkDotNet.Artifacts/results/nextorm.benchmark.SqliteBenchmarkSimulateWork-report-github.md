```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean     | Ratio | Gen0      | Gen1      | Allocated | Alloc Ratio |
|------------------------------ |---------:|------:|----------:|----------:|----------:|------------:|
| Dapper                        | 203.4 ms |  0.96 | 2000.0000 |         - |   9.44 MB |        0.91 |
| NextormCachedWithParamsToList | 207.9 ms |  0.98 | 2000.0000 |         - |  10.42 MB |        1.00 |
| NextormCompiledToList         | 211.8 ms |  1.00 | 2000.0000 |         - |   10.4 MB |        1.00 |
| EFCoreCompiled                | 288.7 ms |  1.36 | 6000.0000 | 2000.0000 |  27.03 MB |        2.60 |
