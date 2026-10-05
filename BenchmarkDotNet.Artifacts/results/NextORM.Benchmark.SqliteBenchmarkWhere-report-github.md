```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  

```
| Method                            | Categories | Mean      | Median   | Gen0     | Gen1    | Allocated  |
|---------------------------------- |----------- |----------:|---------:|---------:|--------:|-----------:|
| Nextorm_Prepared_AsyncStream      |            |  1.207 ms | 1.206 ms |   9.7656 |       - |   92.42 KB |
| Nextorm_Prepared_ToListAsync      |            |  1.208 ms | 1.210 ms |  11.7188 |       - |  107.63 KB |
| Nextorm_CachedForLoop_ToListAsync |            |  1.280 ms | 1.267 ms |  13.6719 |       - |  114.94 KB |
| Dapper_Async                      |            |  1.773 ms | 1.765 ms |  19.5313 |       - |   180.7 KB |
| Dapper_AsyncStream                |            |  1.792 ms | 1.801 ms |  23.4375 |       - |  203.98 KB |
| Linq2Db_Compiled_ToList           |            |  1.887 ms | 1.874 ms |  23.4375 |       - |  253.52 KB |
| Linq2Db_ToListAsync               |            |  3.493 ms | 3.446 ms |  62.5000 |       - |  551.95 KB |
| Linq2Db_AsyncStream               |            |  3.515 ms | 3.502 ms |  62.5000 |       - |  549.22 KB |
| Nextorm_Cached_AsyncStream        |            |  4.039 ms | 2.245 ms |  62.5000 |       - |  569.02 KB |
| EFCore_Compiled_AsyncStream       |            |  4.466 ms | 4.426 ms |  93.7500 | 46.8750 |  789.14 KB |
| EFCore_ToListAsync                |            |  9.461 ms | 9.020 ms | 156.2500 | 31.2500 | 1451.42 KB |
| EFCore_AsyncStream                |            | 11.723 ms | 9.079 ms | 156.2500 | 31.2500 | 1424.33 KB |
|                                   |            |           |          |          |         |            |
| Nextorm_Cached_ToListAsync        | acceptance |  3.836 ms | 2.255 ms |  62.5000 |       - |  565.48 KB |
