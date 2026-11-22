```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Toolchain=InProcessEmitToolchain  

```
| Method                            | Categories | Mean     | Gen0     | Gen1    | Allocated  |
|---------------------------------- |----------- |---------:|---------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |            | 1.043 ms |  11.7188 |       - |  102.59 KB |
| Nextorm_CachedForLoop_ToListAsync |            | 1.134 ms |  13.6719 |  1.9531 |  114.16 KB |
| Linq2Db_ToListAsync               |            | 2.903 ms |  66.4063 |       - |  548.07 KB |
| EFCore_ToListAsync                |            | 7.685 ms | 156.2500 | 46.8750 | 1384.32 KB |
|                                   |            |          |          |         |            |
| Nextorm_Cached_ToListAsync        | acceptance | 1.722 ms |  44.9219 |  9.7656 |   374.8 KB |
