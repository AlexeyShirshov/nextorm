```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  

```
| Method                       | Mean     | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |---------:|---------:|---------:|---------:|----------:|
| Nextorm_Prepared_AsyncStream | 10.85 ms | 265.6250 |        - |        - |   2.14 MB |
| AdoTupleToList               | 11.28 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Nextorm_Prepared_ToListAsync | 11.29 ms | 250.0000 | 125.0000 |        - |   2.21 MB |
| Nextorm_Cached_AsyncStream   | 11.40 ms | 250.0000 |        - |        - |   2.14 MB |
| Nextorm_Cached_ToListAsync   | 11.53 ms | 250.0000 | 156.2500 |        - |   2.22 MB |
| Linq2Db_AsyncStream          | 12.32 ms | 250.0000 |        - |        - |   2.14 MB |
| Dapper_AsyncStream           | 12.82 ms | 266.6667 |        - |        - |   2.59 MB |
| EFCore_Compiled_AsyncStream  | 12.82 ms | 531.2500 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           | 13.29 ms | 531.2500 |        - |        - |   4.28 MB |
| AdoWithDelegate              | 14.01 ms | 328.1250 | 265.6250 |  93.7500 |   2.39 MB |
| Linq2Db_Compiled_ToList      | 14.60 ms | 343.7500 | 250.0000 |  62.5000 |   2.39 MB |
| Dapper_Async                 | 14.92 ms | 343.7500 | 218.7500 |  93.7500 |   2.84 MB |
| Linq2Db_ToListAsync          | 15.26 ms | 343.7500 | 250.0000 |  62.5000 |   2.39 MB |
| EFCore_ToListAsync           | 17.35 ms | 562.5000 | 312.5000 |  62.5000 |   4.53 MB |
