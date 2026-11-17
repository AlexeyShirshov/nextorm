```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Toolchain=InProcessEmitToolchain  

```
| Method                            | Categories | Mean     | Gen0     | Gen1    | Allocated  |
|---------------------------------- |----------- |---------:|---------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |            | 1.063 ms |  11.7188 |       - |   102.9 KB |
| Nextorm_Prepared_AsyncStream      |            | 1.069 ms |  13.6719 |       - |  117.73 KB |
| Nextorm_CachedForLoop_ToListAsync |            | 1.145 ms |  13.6719 |       - |  114.16 KB |
| Dapper_Async                      |            | 1.503 ms |  21.4844 |       - |  179.23 KB |
| Dapper_AsyncStream                |            | 1.514 ms |  23.4375 |       - |  202.51 KB |
| Linq2Db_Compiled_ToList           |            | 1.553 ms |  29.2969 |       - |  252.74 KB |
| Nextorm_Cached_AsyncStream        |            | 1.940 ms |  70.3125 |       - |  580.29 KB |
| Linq2Db_ToListAsync               |            | 2.730 ms |  66.4063 |       - |  548.07 KB |
| Linq2Db_AsyncStream               |            | 2.954 ms |  66.4063 |       - |  546.11 KB |
| EFCore_Compiled_AsyncStream       |            | 3.386 ms |  89.8438 | 42.9688 |  745.41 KB |
| EFCore_AsyncStream                |            | 6.949 ms | 164.0625 | 54.6875 | 1371.24 KB |
| EFCore_ToListAsync                |            | 6.972 ms | 164.0625 | 54.6875 | 1384.26 KB |
|                                   |            |          |          |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 1.664 ms |  44.9219 |  9.7656 |  374.02 KB |
