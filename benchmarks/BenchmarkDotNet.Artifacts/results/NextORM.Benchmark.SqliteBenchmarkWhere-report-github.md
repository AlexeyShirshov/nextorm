```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Categories | Mean       | Gen0     | Gen1    | Allocated  |
|---------------------------------- |----------- |-----------:|---------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |            |   951.1 μs |  11.7188 |       - |  103.36 KB |
| Nextorm_Prepared_AsyncStream      |            |   954.3 μs |  13.6719 |       - |  118.98 KB |
| Nextorm_CachedForLoop_ToListAsync |            | 1,033.5 μs |  13.6719 |  3.9063 |  114.48 KB |
| Dapper_Async                      |            | 1,408.6 μs |  21.4844 |       - |  180.71 KB |
| Linq2Db_Compiled_ToList           |            | 1,493.5 μs |  29.2969 |       - |  253.53 KB |
| Dapper_AsyncStream                |            | 1,556.2 μs |  23.4375 |       - |  203.99 KB |
| Nextorm_Cached_AsyncStream        |            | 1,823.1 μs |  76.1719 |       - |  626.84 KB |
| Linq2Db_ToListAsync               |            | 2,710.7 μs |  66.4063 |       - |  551.97 KB |
| Linq2Db_AsyncStream               |            | 2,989.9 μs |  66.4063 |       - |  549.22 KB |
| EFCore_Compiled_AsyncStream       |            | 3,467.2 μs |  93.7500 | 46.8750 |  789.16 KB |
| EFCore_AsyncStream                |            | 7,125.1 μs | 171.8750 | 54.6875 | 1424.32 KB |
| EFCore_ToListAsync                |            | 7,243.4 μs | 171.8750 | 54.6875 | 1437.39 KB |
|                                   |            |            |          |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 2,056.6 μs |  78.1250 |       - |  645.59 KB |
