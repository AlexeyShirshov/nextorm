```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean     | Ratio | Gen0      | Allocated | Alloc Ratio |
|------------------------------ |---------:|------:|----------:|----------:|------------:|
| NextormCachedWithParamsToList | 160.4 ms |  1.00 | 1250.0000 |   6.23 MB |        1.00 |
| NextormCompiledToList         | 160.8 ms |  1.00 | 1250.0000 |   6.21 MB |        1.00 |
| Dapper                        | 213.2 ms |  1.33 | 3000.0000 |  14.09 MB |        2.27 |
| EFCoreCompiled                | 246.1 ms |  1.54 | 4000.0000 |  20.41 MB |        3.29 |
