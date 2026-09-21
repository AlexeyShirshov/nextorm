```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Mean       | Gen0     | Gen1    | Allocated  |
|---------------------------------- |-----------:|---------:|--------:|-----------:|
| Nextorm_Prepared_AsyncStream      |   942.3 μs |  10.7422 |       - |   92.43 KB |
| Nextorm_Prepared_ToListAsync      |   950.1 μs |  11.7188 |       - |  100.61 KB |
| Nextorm_CachedForLoop_ToListAsync | 1,009.8 μs |  11.7188 |       - |  105.87 KB |
| Dapper_Async                      | 1,385.0 μs |  21.4844 |       - |  180.72 KB |
| Dapper_AsyncStream                | 1,398.6 μs |  23.4375 |       - |     204 KB |
| Nextorm_Cached_ToListAsync        | 1,699.7 μs |  58.5938 |       - |   485.8 KB |
| Nextorm_Cached_AsyncStream        | 1,703.8 μs |  56.6406 |       - |  477.62 KB |
| Linq2Db_ToListAsync               | 2,759.0 μs |  66.4063 |       - |  551.98 KB |
| Linq2Db_AsyncStream               | 2,926.6 μs |  66.4063 |       - |  549.25 KB |
| EFCore_Compiled_AsyncStream       | 3,344.6 μs |  93.7500 | 46.8750 |  789.17 KB |
| EFCore_AsyncStream                | 7,808.1 μs | 171.8750 | 46.8750 | 1424.45 KB |
| EFCore_ToListAsync                | 8,012.9 μs | 171.8750 | 46.8750 | 1437.47 KB |
