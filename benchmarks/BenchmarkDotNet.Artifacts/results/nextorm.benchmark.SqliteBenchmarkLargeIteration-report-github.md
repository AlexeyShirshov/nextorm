```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|---------:|---------:|---------:|----------:|
| Nextorm_Cached_ToListAsync   |  8.410 ms | 265.6250 | 156.2500 |        - |   2.22 MB |
| Nextorm_Prepared_ToListAsync |  8.425 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| AdoTupleToList               |  8.531 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Nextorm_Cached_AsyncStream   |  8.612 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Prepared_AsyncStream |  8.675 ms | 265.6250 |        - |        - |   2.14 MB |
| Dapper_AsyncStream           |  8.986 ms | 312.5000 |        - |        - |   2.59 MB |
| Linq2Db_AsyncStream          |  9.305 ms | 265.6250 |        - |        - |   2.14 MB |
| EFCore_Compiled_AsyncStream  | 10.021 ms | 531.2500 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           | 10.037 ms | 531.2500 |        - |        - |   4.28 MB |
| AdoWithDelegate              | 10.608 ms | 328.1250 | 265.6250 |  93.7500 |   2.39 MB |
| Dapper_Async                 | 11.651 ms | 375.0000 | 250.0000 |  93.7500 |   2.84 MB |
| Linq2Db_ToListAsync          | 11.670 ms | 343.7500 | 265.6250 |  78.1250 |   2.39 MB |
| EFCore_ToListAsync           | 13.588 ms | 625.0000 | 406.2500 |  93.7500 |   4.53 MB |
