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
| Nextorm_Prepared_ToListAsync      | 3.089 ms |  27.3438 |       - |  133.26 KB |
| Nextorm_Prepared_AsyncStream      | 3.132 ms |  27.3438 |       - |  139.14 KB |
| Nextorm_CachedForLoop_ToListAsync | 3.421 ms |  31.2500 |       - |  145.11 KB |
| Nextorm_Cached_AsyncStream        | 4.304 ms | 109.3750 |       - |  531.37 KB |
| Dapper_AsyncStream                | 4.439 ms |  39.0625 |       - |  208.67 KB |
| Dapper_Async                      | 4.444 ms |  39.0625 |       - |  185.39 KB |
| Nextorm_Cached_ToListAsync        | 4.960 ms | 109.3750 |       - |  525.49 KB |
| EFCore_Compiled_AsyncStream       | 5.444 ms | 109.3750 | 31.2500 |  527.89 KB |
| EFCore_AsyncStream                | 8.796 ms | 218.7500 | 31.2500 | 1058.46 KB |
| EFCore_ToListAsync                | 8.807 ms | 218.7500 | 31.2500 | 1071.48 KB |
