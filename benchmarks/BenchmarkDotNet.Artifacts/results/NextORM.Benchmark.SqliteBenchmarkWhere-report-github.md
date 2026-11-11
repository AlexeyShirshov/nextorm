```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.19GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Categories | Mean       | Gen0    | Gen1    | Allocated  |
|---------------------------------- |----------- |-----------:|--------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |            |   915.0 μs |  5.8594 |       - |  102.58 KB |
| Nextorm_Prepared_AsyncStream      |            |   925.8 μs |  6.8359 |       - |  117.42 KB |
| Nextorm_CachedForLoop_ToListAsync |            |   991.2 μs |  5.8594 |  1.9531 |  113.22 KB |
| Dapper_Async                      |            | 1,378.8 μs |  9.7656 |       - |  179.23 KB |
| Dapper_AsyncStream                |            | 1,390.0 μs | 11.7188 |       - |  202.51 KB |
| Linq2Db_Compiled_ToList           |            | 1,432.3 μs | 13.6719 |       - |  252.74 KB |
| Nextorm_Cached_AsyncStream        |            | 1,769.7 μs | 35.1563 |       - |  576.05 KB |
| Linq2Db_ToListAsync               |            | 2,588.9 μs | 31.2500 |       - |  548.06 KB |
| Linq2Db_AsyncStream               |            | 2,802.5 μs | 31.2500 |       - |  546.11 KB |
| EFCore_Compiled_AsyncStream       |            | 3,540.1 μs | 42.9688 | 19.5313 |  745.41 KB |
| EFCore_ToListAsync                |            | 7,451.4 μs | 62.5000 | 31.2500 | 1384.38 KB |
| EFCore_AsyncStream                |            | 7,479.3 μs | 78.1250 | 39.0625 | 1371.23 KB |
|                                   |            |            |         |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 1,520.4 μs | 21.4844 |  5.8594 |  370.58 KB |
