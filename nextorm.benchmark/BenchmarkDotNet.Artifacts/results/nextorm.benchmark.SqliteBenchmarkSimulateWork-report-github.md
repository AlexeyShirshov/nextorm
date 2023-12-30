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
| NextormCompiledToList         | 160.5 ms |  1.00 | 1250.0000 |         - |   6.21 MB |        1.00 |
| NextormCachedWithParamsToList | 162.5 ms |  1.01 | 1250.0000 |         - |   6.23 MB |        1.00 |
| Dapper                        | 208.8 ms |  1.30 | 2000.0000 |         - |   9.44 MB |        1.52 |
| EFCoreCompiled                | 298.9 ms |  1.86 | 6000.0000 | 2000.0000 |  27.03 MB |        4.35 |
