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
| NextormCompiledToList         | 210.47 ms |  1.00 | 2500.0000 |  11.77 MB |        1.00 |
| NextormCachedWithParamsToList | 209.26 ms |  0.99 | 2500.0000 |  11.79 MB |        1.00 |
| EFCoreCompiled                |  44.07 ms |  0.21 | 2583.3333 |  11.78 MB |        1.00 |
| Dapper                        | 213.01 ms |  1.01 | 2000.0000 |   9.59 MB |        0.81 |
