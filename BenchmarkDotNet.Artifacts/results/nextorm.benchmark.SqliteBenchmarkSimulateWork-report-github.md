```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  

```
| Method                              | Mean       | Ratio | Gen0       | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|-----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   6.724 ms |  0.14 |   781.2500 |        - |   6.24 MB |        1.00 |
| Dapper_AsyncStream                  |  24.314 ms |  0.51 |  1093.7500 |        - |    8.9 MB |        1.42 |
| Nextorm_Cached_AsyncStream          |  41.347 ms |  0.88 |  3500.0000 |        - |   30.7 MB |        4.92 |
| Nextorm_Prepared_ToListAsync        |  47.217 ms |  1.00 |   727.2727 |  90.9091 |   6.25 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  48.340 ms |  1.02 |   727.2727 |  90.9091 |   6.26 MB |        1.00 |
| Dapper_Async                        |  67.660 ms |  1.43 |  1000.0000 | 125.0000 |   8.91 MB |        1.43 |
| Nextorm_Cached_ToList               |  90.800 ms |  1.92 |  3500.0000 |        - |  30.02 MB |        4.81 |
| EFCore_Compiled_ToListAsync         | 106.143 ms |  2.25 |  1600.0000 | 200.0000 |  13.68 MB |        2.19 |
| Linq2Db_ToListAsync                 | 364.654 ms |  7.72 | 15000.0000 |        - | 121.57 MB |       19.46 |
