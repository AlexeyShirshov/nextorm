```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                            | Mean     | Median   | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------------------- |---------:|---------:|------:|---------:|--------:|-----------:|------------:|
| Nextorm_Prepared_AsyncStream      | 3.103 ms | 3.107 ms |  1.00 |  23.4375 |       - |  125.08 KB |        0.95 |
| Nextorm_Prepared_StreamAsync      | 3.114 ms | 3.115 ms |  1.00 |  27.3438 |       - |  132.11 KB |        1.00 |
| Nextorm_Prepared_ToListAsync      | 3.121 ms | 3.112 ms |  1.00 |  27.3438 |       - |  133.26 KB |        1.01 |
| Nextorm_CachedForLoop_ToListAsync | 3.401 ms | 3.412 ms |  1.09 |  31.2500 |  3.9063 |  144.82 KB |        1.10 |
| Dapper_AsyncStream                | 4.239 ms | 4.304 ms |  1.34 |  39.0625 |       - |  208.67 KB |        1.58 |
| Nextorm_Cached_StreamAsync        | 4.307 ms | 4.296 ms |  1.38 | 117.1875 |       - |  561.82 KB |        4.25 |
| Nextorm_Cached_ToListAsync        | 4.316 ms | 4.314 ms |  1.39 | 117.1875 |       - |  562.97 KB |        4.26 |
| Dapper_Async                      | 4.740 ms | 4.801 ms |  1.53 |  39.0625 |       - |  185.39 KB |        1.40 |
| EFCore_Compiled_AsyncStream       | 5.404 ms | 5.400 ms |  1.74 | 109.3750 | 31.2500 |   527.9 KB |        4.00 |
| EFCore_AsyncStream                | 8.760 ms | 8.729 ms |  2.82 | 218.7500 | 31.2500 | 1060.78 KB |        8.03 |
| EFCore_ToListAsync                | 8.827 ms | 8.856 ms |  2.83 | 218.7500 | 31.2500 | 1071.48 KB |        8.11 |
