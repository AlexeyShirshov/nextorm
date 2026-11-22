```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.54GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Categories | Mean       | Gen0    | Gen1    | Allocated  |
|---------------------------------- |----------- |-----------:|--------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |            |   380.0 μs |  0.9766 |       - |  102.58 KB |
| Nextorm_Prepared_AsyncStream      |            |   383.7 μs |  0.9766 |       - |  117.42 KB |
| Nextorm_CachedForLoop_ToListAsync |            |   414.3 μs |  0.9766 |  0.4883 |  114.27 KB |
| Dapper_Async                      |            |   630.0 μs |  1.9531 |       - |  179.22 KB |
| Dapper_AsyncStream                |            |   648.1 μs |  1.9531 |       - |   202.5 KB |
| Linq2Db_Compiled_ToList           |            |   667.8 μs |  2.9297 |       - |  252.74 KB |
| Nextorm_Cached_AsyncStream        |            |   777.6 μs |  6.8359 |       - |  591.98 KB |
| Linq2Db_ToListAsync               |            | 1,017.0 μs |  5.8594 |       - |  548.06 KB |
| Linq2Db_AsyncStream               |            | 1,092.4 μs |  5.8594 |       - |   546.1 KB |
| EFCore_Compiled_AsyncStream       |            | 2,053.2 μs |  7.8125 |  3.9063 |  745.41 KB |
| EFCore_AsyncStream                |            | 3,085.3 μs | 15.6250 | 11.7188 | 1382.15 KB |
| EFCore_ToListAsync                |            | 3,122.1 μs | 15.6250 | 11.7188 | 1395.18 KB |
|                                   |            |            |         |         |            |
| Nextorm_Cached_ToListAsync        | acceptance |   762.7 μs |  3.9063 |  0.9766 |  385.73 KB |
