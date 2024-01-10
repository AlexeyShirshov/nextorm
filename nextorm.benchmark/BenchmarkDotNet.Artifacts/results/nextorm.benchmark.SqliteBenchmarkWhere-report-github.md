```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |---------:|------:|---------:|--------:|-----------:|------------:|
| NextormCompiledToList | 3.297 ms |  1.00 |  27.3438 |       - |  140.29 KB |        1.06 |
| NextormCompiled       | 3.297 ms |  1.00 |  27.3438 |       - |  132.11 KB |        1.00 |
| NextormCompiledAsync  | 3.308 ms |  1.00 |  23.4375 |       - |  125.08 KB |        0.95 |
| NextormCachedParam    | 3.569 ms |  1.08 |  31.2500 |       - |  143.91 KB |        1.09 |
| NextormCachedToList   | 3.672 ms |  1.11 |  31.2500 |       - |  151.99 KB |        1.15 |
| DapperUnbuffered      | 4.298 ms |  1.30 |  39.0625 |       - |  208.67 KB |        1.58 |
| Dapper                | 4.323 ms |  1.31 |  39.0625 |       - |  185.39 KB |        1.40 |
| NextormCached         | 4.796 ms |  1.45 | 125.0000 |       - |  593.85 KB |        4.50 |
| EFCoreCompiled        | 5.643 ms |  1.71 | 109.3750 | 31.2500 |  537.82 KB |        4.07 |
| EFCoreStream          | 9.222 ms |  2.79 | 218.7500 | 31.2500 | 1064.21 KB |        8.06 |
| EFCore                | 9.293 ms |  2.82 | 218.7500 | 31.2500 | 1074.92 KB |        8.14 |
