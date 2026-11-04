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
| Nextorm_Prepared_AsyncStream      |            |   886.7 μs |  10.7422 |       - |   92.43 KB |
| Nextorm_Prepared_ToListAsync      |            |   900.4 μs |  12.6953 |       - |  107.64 KB |
| Nextorm_CachedForLoop_ToListAsync |            |   980.1 μs |  13.6719 |  1.9531 |  114.69 KB |
| Dapper_Async                      |            | 1,369.6 μs |  21.4844 |       - |  180.72 KB |
| Dapper_AsyncStream                |            | 1,402.3 μs |  23.4375 |       - |     204 KB |
| Linq2Db_Compiled_ToList           |            | 1,412.3 μs |  29.2969 |       - |  253.53 KB |
| Nextorm_Cached_AsyncStream        |            | 1,946.1 μs |  66.4063 |       - |  554.96 KB |
| Linq2Db_ToListAsync               |            | 2,587.9 μs |  66.4063 |       - |  566.82 KB |
| Linq2Db_AsyncStream               |            | 2,874.8 μs |  66.4063 |       - |  564.09 KB |
| EFCore_Compiled_AsyncStream       |            | 3,199.6 μs |  93.7500 | 46.8750 |  789.17 KB |
| EFCore_AsyncStream                |            | 6,789.5 μs | 171.8750 | 54.6875 | 1454.06 KB |
| EFCore_ToListAsync                |            | 7,005.4 μs | 171.8750 | 54.6875 | 1467.09 KB |
|                                   |            |            |          |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 1,897.0 μs |  66.4063 |       - |  553.76 KB |
