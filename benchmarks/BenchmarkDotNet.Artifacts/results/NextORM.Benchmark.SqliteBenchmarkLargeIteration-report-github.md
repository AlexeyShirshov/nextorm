```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  

```
| Method                       | Mean      | Median    | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|----------:|---------:|---------:|---------:|----------:|
| Nextorm_Cached_AsyncStream   |  8.397 ms |  8.394 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Prepared_ToListAsync |  8.580 ms |  8.553 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| Nextorm_Cached_ToListAsync   |  8.598 ms |  8.607 ms | 265.6250 | 203.1250 |        - |   2.22 MB |
| AdoTupleToList               |  8.602 ms |  8.529 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Nextorm_Prepared_AsyncStream |  8.965 ms |  8.874 ms | 265.6250 |        - |        - |   2.14 MB |
| Dapper_AsyncStream           |  9.317 ms |  9.272 ms | 312.5000 |        - |        - |   2.59 MB |
| Linq2Db_AsyncStream          |  9.476 ms |  9.469 ms | 265.6250 |        - |        - |   2.14 MB |
| EFCore_Compiled_AsyncStream  | 10.308 ms | 10.281 ms | 531.2500 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           | 10.366 ms | 10.350 ms | 531.2500 |        - |        - |   4.28 MB |
| AdoWithDelegate              | 10.696 ms | 10.532 ms | 328.1250 | 265.6250 |  93.7500 |   2.39 MB |
| Linq2Db_Compiled_ToList      | 11.128 ms | 11.085 ms | 343.7500 | 265.6250 |  78.1250 |   2.39 MB |
| Dapper_Async                 | 11.483 ms | 11.395 ms | 375.0000 | 250.0000 |  93.7500 |   2.84 MB |
| Linq2Db_ToListAsync          | 11.692 ms | 11.685 ms | 343.7500 | 265.6250 |  78.1250 |   2.39 MB |
| EFCore_ToListAsync           | 14.489 ms | 14.541 ms | 625.0000 | 406.2500 |  93.7500 |   4.53 MB |
