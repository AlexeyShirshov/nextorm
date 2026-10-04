```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  

```
| Method                            | Categories | Mean       | Gen0     | Gen1    | Allocated  |
|---------------------------------- |----------- |-----------:|---------:|--------:|-----------:|
| Nextorm_Prepared_AsyncStream      |            |   958.1 μs |  10.7422 |       - |   92.42 KB |
| Nextorm_Prepared_ToListAsync      |            |   975.7 μs |  12.6953 |       - |  107.63 KB |
| Nextorm_CachedForLoop_ToListAsync |            | 1,080.0 μs |  13.6719 |       - |  114.67 KB |
| Dapper_Async                      |            | 1,353.1 μs |  21.4844 |       - |   180.7 KB |
| Dapper_AsyncStream                |            | 1,693.2 μs |  23.4375 |       - |  203.98 KB |
| Linq2Db_Compiled_ToList           |            | 1,946.3 μs |  27.3438 |       - |  253.52 KB |
| Nextorm_Cached_AsyncStream        |            | 2,087.6 μs |  89.8438 |       - |  734.65 KB |
| EFCore_Compiled_AsyncStream       |            | 3,544.6 μs |  93.7500 | 46.8750 |  789.14 KB |
| Linq2Db_ToListAsync               |            | 3,638.5 μs |  62.5000 |       - |  551.95 KB |
| Linq2Db_AsyncStream               |            | 3,776.5 μs |  62.5000 |       - |  549.22 KB |
| EFCore_AsyncStream                |            | 7,145.7 μs | 156.2500 | 31.2500 | 1424.33 KB |
| EFCore_ToListAsync                |            | 7,918.2 μs | 156.2500 | 31.2500 | 1437.36 KB |
|                                   |            |            |          |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 2,546.7 μs |  85.9375 |       - |  731.11 KB |
