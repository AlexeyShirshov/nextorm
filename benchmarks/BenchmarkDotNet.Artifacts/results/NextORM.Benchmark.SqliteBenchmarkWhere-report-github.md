```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Categories | Mean       | Gen0    | Gen1    | Allocated  |
|---------------------------------- |----------- |-----------:|--------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |            |   868.8 μs |  5.8594 |       - |  103.36 KB |
| Nextorm_Prepared_AsyncStream      |            |   876.0 μs |  6.8359 |       - |  118.98 KB |
| Nextorm_CachedForLoop_ToListAsync |            |   912.6 μs |  6.8359 |  0.9766 |  114.48 KB |
| Dapper_Async                      |            | 1,156.3 μs |  9.7656 |       - |  180.71 KB |
| Dapper_AsyncStream                |            | 1,170.9 μs | 11.7188 |       - |  203.99 KB |
| Linq2Db_Compiled_ToList           |            | 1,199.4 μs | 13.6719 |       - |  253.52 KB |
| Nextorm_Cached_AsyncStream        |            | 1,335.2 μs | 37.1094 |       - |  626.84 KB |
| Linq2Db_ToListAsync               |            | 1,604.7 μs | 33.2031 |       - |  551.96 KB |
| Linq2Db_AsyncStream               |            | 1,725.4 μs | 33.2031 |       - |  549.23 KB |
| EFCore_Compiled_AsyncStream       |            | 2,345.1 μs | 46.8750 | 23.4375 |  789.16 KB |
| EFCore_AsyncStream                |            | 4,072.2 μs | 85.9375 | 39.0625 | 1424.36 KB |
| EFCore_ToListAsync                |            | 4,146.6 μs | 85.9375 | 39.0625 | 1437.39 KB |
|                                   |            |            |         |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 1,400.2 μs | 39.0625 |       - |  645.58 KB |
