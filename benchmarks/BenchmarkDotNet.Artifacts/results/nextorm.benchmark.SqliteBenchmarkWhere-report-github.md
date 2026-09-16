```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  

```
| Method                            | Mean       | Gen0     | Gen1    | Allocated  |
|---------------------------------- |-----------:|---------:|--------:|-----------:|
| Nextorm_Prepared_AsyncStream      |   899.3 μs |  10.7422 |       - |   92.42 KB |
| Nextorm_Prepared_ToListAsync      |   903.8 μs |  11.7188 |       - |   100.6 KB |
| Nextorm_CachedForLoop_ToListAsync | 1,107.3 μs |  11.7188 |  1.9531 |  111.45 KB |
| Dapper_Async                      | 1,334.3 μs |  21.4844 |       - |   180.7 KB |
| Dapper_AsyncStream                | 1,348.2 μs |  23.4375 |       - |  203.98 KB |
| Nextorm_Cached_ToListAsync        | 1,410.5 μs |  58.5938 |       - |  488.92 KB |
| Nextorm_Cached_AsyncStream        | 1,424.3 μs |  58.5938 |       - |  480.74 KB |
| Linq2Db_ToListAsync               | 2,503.1 μs |  62.5000 |       - |  551.95 KB |
| Linq2Db_AsyncStream               | 2,549.3 μs |  62.5000 |       - |  549.22 KB |
| EFCore_Compiled_AsyncStream       | 3,131.4 μs |  93.7500 | 46.8750 |  789.14 KB |
| EFCore_AsyncStream                | 6,527.7 μs | 156.2500 | 31.2500 | 1424.33 KB |
| EFCore_ToListAsync                | 6,573.6 μs | 156.2500 | 31.2500 | 1437.36 KB |
