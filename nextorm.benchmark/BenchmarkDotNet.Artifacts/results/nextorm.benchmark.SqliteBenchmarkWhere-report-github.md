```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |---------:|------:|---------:|--------:|-----------:|------------:|
| NextormCompiledAsync  | 3.977 ms |  0.99 |  39.0625 |       - |  203.21 KB |        0.97 |
| NextormCompiledToList | 4.002 ms |  1.00 |  46.8750 |       - |  218.42 KB |        1.04 |
| NextormCompiled       | 4.032 ms |  1.00 |  39.0625 |       - |  210.24 KB |        1.00 |
| Dapper                | 4.208 ms |  1.08 |  39.0625 |       - |  185.39 KB |        0.88 |
| NextormCachedParam    | 4.473 ms |  1.11 |  46.8750 |       - |  221.27 KB |        1.05 |
| DapperUnbuffered      | 4.675 ms |  1.16 |  39.0625 |       - |  208.67 KB |        0.99 |
| NextormCachedToList   | 4.801 ms |  1.19 |  46.8750 |       - |  229.37 KB |        1.09 |
| NextormCached         | 5.395 ms |  1.34 | 140.6250 |       - |  661.05 KB |        3.14 |
| EFCoreCompiled        | 5.442 ms |  1.35 | 109.3750 | 31.2500 |  534.38 KB |        2.54 |
| EFCoreStream          | 8.724 ms |  2.17 | 218.7500 | 31.2500 | 1060.78 KB |        5.05 |
| EFCore                | 8.945 ms |  2.22 | 218.7500 | 31.2500 | 1071.48 KB |        5.10 |
