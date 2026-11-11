```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Categories | Mean       | Gen0     | Gen1    | Allocated  |
|---------------------------------- |----------- |-----------:|---------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |            |   927.9 μs |  11.7188 |       - |  102.58 KB |
| Nextorm_Prepared_AsyncStream      |            |   949.2 μs |  13.6719 |       - |  117.42 KB |
| Nextorm_CachedForLoop_ToListAsync |            | 1,088.4 μs |  13.6719 |  3.9063 |  113.21 KB |
| Dapper_Async                      |            | 1,353.2 μs |  21.4844 |       - |  179.23 KB |
| Dapper_AsyncStream                |            | 1,381.6 μs |  23.4375 |       - |  202.51 KB |
| Linq2Db_Compiled_ToList           |            | 1,403.5 μs |  29.2969 |       - |  252.74 KB |
| Nextorm_Cached_AsyncStream        |            | 1,749.9 μs |  70.3125 |       - |  576.06 KB |
| Linq2Db_ToListAsync               |            | 2,473.3 μs |  66.4063 |       - |  548.07 KB |
| Linq2Db_AsyncStream               |            | 2,657.7 μs |  66.4063 |       - |  546.11 KB |
| EFCore_Compiled_AsyncStream       |            | 3,179.1 μs |  89.8438 | 42.9688 |  745.41 KB |
| EFCore_AsyncStream                |            | 6,634.8 μs | 164.0625 | 54.6875 | 1371.24 KB |
| EFCore_ToListAsync                |            | 6,699.6 μs | 164.0625 | 54.6875 | 1384.26 KB |
|                                   |            |            |          |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 1,454.1 μs |  44.9219 |  9.7656 |  370.58 KB |
