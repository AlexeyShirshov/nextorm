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
| NextormCompiledToList         | 280.30 ms |  1.00 | 7000.0000 |  31.79 MB |        1.00 |
| NextormCachedWithParamsToList | 207.11 ms |  0.74 | 2500.0000 |  11.63 MB |        0.37 |
| EFCoreCompiled                |  45.40 ms |  0.16 | 2636.3636 |  12.16 MB |        0.38 |
| Dapper                        | 219.96 ms |  0.79 | 2500.0000 |  11.48 MB |        0.36 |
