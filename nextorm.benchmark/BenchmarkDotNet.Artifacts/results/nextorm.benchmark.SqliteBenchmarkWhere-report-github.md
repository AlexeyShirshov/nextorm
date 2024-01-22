```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                            | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------------------- |---------:|------:|---------:|--------:|-----------:|------------:|
| Nextorm_Prepared_ToListAsync      | 3.106 ms |  0.84 |  27.3438 |       - |  133.26 KB |        1.01 |
| Nextorm_CachedForLoop_ToListAsync | 3.362 ms |  0.91 |  31.2500 |  3.9063 |  144.99 KB |        1.10 |
| Nextorm_Prepared_StreamAsync      | 3.685 ms |  1.00 |  27.3438 |       - |  132.11 KB |        1.00 |
| Nextorm_Prepared_AsyncStream      | 3.715 ms |  1.01 |  23.4375 |       - |  125.08 KB |        0.95 |
| Dapper_AsyncStream                | 4.615 ms |  1.26 |  39.0625 |       - |  208.67 KB |        1.58 |
| Dapper_Async                      | 4.656 ms |  1.27 |  39.0625 |       - |  185.39 KB |        1.40 |
| Nextorm_Cached_ToListAsync        | 4.989 ms |  1.35 | 125.0000 |       - |  580.94 KB |        4.40 |
| Nextorm_Cached_StreamAsync        | 5.036 ms |  1.37 | 125.0000 |       - |  579.79 KB |        4.39 |
| EFCore_Compiled_AsyncStream       | 5.456 ms |  1.48 | 109.3750 | 31.2500 |  527.89 KB |        4.00 |
| EFCore_ToListAsync                | 8.716 ms |  2.36 | 218.7500 | 31.2500 | 1071.48 KB |        8.11 |
| EFCore_AsyncStream                | 8.808 ms |  2.38 | 218.7500 | 31.2500 | 1060.78 KB |        8.03 |
