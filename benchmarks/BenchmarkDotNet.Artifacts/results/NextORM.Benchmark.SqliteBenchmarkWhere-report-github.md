```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.29GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Categories | Mean       | Gen0    | Gen1    | Allocated  |
|---------------------------------- |----------- |-----------:|--------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |            |   705.0 μs |  5.8594 |       - |  102.58 KB |
| Nextorm_Prepared_AsyncStream      |            |   716.3 μs |  6.8359 |       - |  117.42 KB |
| Nextorm_CachedForLoop_ToListAsync |            |   737.4 μs |  6.8359 |  0.9766 |  114.16 KB |
| Dapper_Async                      |            |   896.1 μs | 10.7422 |       - |  179.22 KB |
| Dapper_AsyncStream                |            |   899.5 μs | 11.7188 |       - |   202.5 KB |
| Linq2Db_Compiled_ToList           |            |   926.8 μs | 14.6484 |       - |  252.74 KB |
| Nextorm_Cached_AsyncStream        |            | 1,010.0 μs | 35.1563 |       - |  580.27 KB |
| Linq2Db_ToListAsync               |            | 1,178.7 μs | 33.2031 |       - |  548.05 KB |
| Linq2Db_AsyncStream               |            | 1,196.2 μs | 33.2031 |       - |   546.1 KB |
| EFCore_Compiled_AsyncStream       |            | 1,924.2 μs | 44.9219 | 21.4844 |  745.39 KB |
| EFCore_ToListAsync                |            | 2,452.7 μs | 82.0313 | 39.0625 | 1384.22 KB |
| EFCore_AsyncStream                |            | 2,533.9 μs | 82.0313 | 39.0625 |  1371.2 KB |
|                                   |            |            |         |         |            |
| Nextorm_Cached_ToListAsync        | acceptance |   980.7 μs | 22.4609 |  4.8828 |  374.01 KB |
