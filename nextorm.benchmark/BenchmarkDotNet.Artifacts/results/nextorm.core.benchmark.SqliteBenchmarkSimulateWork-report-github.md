```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean      | Ratio | Gen0      | Allocated | Alloc Ratio |
|------------------------------ |----------:|------:|----------:|----------:|------------:|
| NextormCompiledToList         | 210.59 ms |  1.00 | 2500.0000 |  11.73 MB |        1.00 |
| NextormCachedWithParamsToList | 218.97 ms |  1.04 | 2500.0000 |  11.75 MB |        1.00 |
| EFCoreCompiled                |  45.10 ms |  0.21 | 2545.4545 |  11.78 MB |        1.00 |
| Dapper                        | 216.95 ms |  1.03 | 2000.0000 |   9.59 MB |        0.82 |
