```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                            | Mean     | Gen0     | Gen1    | Allocated  |
|---------------------------------- |---------:|---------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      | 3.138 ms |  27.3438 |       - |  133.26 KB |
| Nextorm_Prepared_AsyncStream      | 3.143 ms |  27.3438 |       - |  139.14 KB |
| Nextorm_CachedForLoop_ToListAsync | 3.920 ms |  31.2500 |       - |  144.64 KB |
| Dapper_Async                      | 4.040 ms |  39.0625 |       - |  185.39 KB |
| Dapper_AsyncStream                | 4.041 ms |  39.0625 |       - |  208.67 KB |
| Nextorm_Cached_AsyncStream        | 4.340 ms | 109.3750 |       - |  535.25 KB |
| Nextorm_Cached_ToListAsync        | 4.872 ms | 109.3750 |       - |  551.25 KB |
| EFCore_Compiled_AsyncStream       | 5.283 ms | 109.3750 | 31.2500 |   527.9 KB |
| EFCore_AsyncStream                | 8.693 ms | 218.7500 | 31.2500 | 1060.78 KB |
| EFCore_ToListAsync                | 8.803 ms | 218.7500 | 31.2500 | 1071.48 KB |
