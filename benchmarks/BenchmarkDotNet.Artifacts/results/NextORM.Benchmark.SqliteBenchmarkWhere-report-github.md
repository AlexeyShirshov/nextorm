```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  

```
| Method                            | Mean       | Gen0     | Gen1    | Allocated  |
|---------------------------------- |-----------:|---------:|--------:|-----------:|
| Nextorm_Prepared_AsyncStream      |   925.8 μs |  10.7422 |       - |   92.42 KB |
| Nextorm_Prepared_ToListAsync      |   941.2 μs |  12.6953 |       - |  107.63 KB |
| Nextorm_CachedForLoop_ToListAsync |   957.7 μs |  13.6719 |       - |  114.08 KB |
| Dapper_AsyncStream                | 1,336.0 μs |  23.4375 |       - |  203.98 KB |
| Dapper_Async                      | 1,362.5 μs |  21.4844 |       - |   180.7 KB |
| Linq2Db_Compiled_ToList           | 1,387.4 μs |  29.2969 |       - |  253.52 KB |
| Nextorm_Cached_AsyncStream        | 1,576.3 μs |  78.1250 |       - |   640.9 KB |
| Nextorm_Cached_ToListAsync        | 1,622.0 μs |  80.0781 |       - |  656.11 KB |
| Linq2Db_ToListAsync               | 2,398.0 μs |  62.5000 |       - |  551.95 KB |
| Linq2Db_AsyncStream               | 2,498.6 μs |  62.5000 |       - |  549.22 KB |
| EFCore_Compiled_AsyncStream       | 3,219.5 μs |  93.7500 | 46.8750 |  789.14 KB |
| EFCore_ToListAsync                | 6,560.1 μs | 156.2500 | 31.2500 | 1437.36 KB |
| EFCore_AsyncStream                | 6,683.7 μs | 156.2500 | 31.2500 | 1424.34 KB |
