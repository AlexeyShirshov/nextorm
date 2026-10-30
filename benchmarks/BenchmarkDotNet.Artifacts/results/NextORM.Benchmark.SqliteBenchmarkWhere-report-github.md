```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Categories | Mean       | Gen0    | Gen1    | Allocated  |
|---------------------------------- |----------- |-----------:|--------:|--------:|-----------:|
| Nextorm_Prepared_AsyncStream      |            |   855.5 μs |  6.8359 |       - |  118.98 KB |
| Nextorm_Prepared_ToListAsync      |            |   868.2 μs |  5.8594 |       - |   110.4 KB |
| Nextorm_CachedForLoop_ToListAsync |            |   924.4 μs |  6.8359 |  0.9766 |  121.44 KB |
| Dapper_Async                      |            | 1,153.7 μs |  9.7656 |       - |  180.71 KB |
| Dapper_AsyncStream                |            | 1,164.1 μs | 11.7188 |       - |  203.99 KB |
| Linq2Db_Compiled_ToList           |            | 1,198.2 μs | 13.6719 |       - |  253.52 KB |
| Nextorm_Cached_AsyncStream        |            | 1,332.7 μs | 37.1094 |       - |   619.8 KB |
| Linq2Db_ToListAsync               |            | 1,620.8 μs | 33.2031 |       - |  551.96 KB |
| Linq2Db_AsyncStream               |            | 1,712.5 μs | 33.2031 |       - |  549.23 KB |
| EFCore_Compiled_AsyncStream       |            | 2,468.9 μs | 46.8750 | 23.4375 |  789.16 KB |
| EFCore_AsyncStream                |            | 4,160.4 μs | 85.9375 | 39.0625 | 1424.36 KB |
| EFCore_ToListAsync                |            | 4,238.0 μs | 85.9375 | 39.0625 | 1437.38 KB |
|                                   |            |            |         |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 1,335.4 μs | 35.1563 |       - |   594.8 KB |
