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
| Nextorm_Prepared_ToListAsync      | 3.024 ms |  1.00 |  27.3438 |       - |  133.26 KB |        1.01 |
| Nextorm_Prepared_AsyncStream      | 3.026 ms |  1.00 |  23.4375 |       - |  125.08 KB |        0.95 |
| Nextorm_Prepared_StreamAsync      | 3.035 ms |  1.00 |  27.3438 |       - |  132.11 KB |        1.00 |
| Nextorm_CachedForLoop_ToListAsync | 3.268 ms |  1.08 |  31.2500 |  3.9063 |  144.96 KB |        1.10 |
| Dapper_Async                      | 3.932 ms |  1.30 |  39.0625 |       - |  185.39 KB |        1.40 |
| Dapper_AsyncStream                | 3.988 ms |  1.32 |  42.9688 |       - |  208.67 KB |        1.58 |
| Nextorm_Cached_StreamAsync        | 4.336 ms |  1.43 | 125.0000 |       - |  585.26 KB |        4.43 |
| Nextorm_Cached_ToListAsync        | 4.376 ms |  1.44 | 125.0000 |       - |  589.54 KB |        4.46 |
| EFCore_Compiled_AsyncStream       | 5.137 ms |  1.69 | 109.3750 | 31.2500 |   527.9 KB |        4.00 |
| EFCore_ToListAsync                | 8.885 ms |  2.93 | 218.7500 | 31.2500 | 1102.73 KB |        8.35 |
| EFCore_AsyncStream                | 8.968 ms |  2.95 | 218.7500 | 31.2500 | 1084.21 KB |        8.21 |
