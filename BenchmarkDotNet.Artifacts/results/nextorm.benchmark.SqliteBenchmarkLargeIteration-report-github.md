```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Method                       | Mean      | Median    | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|----------:|---------:|---------:|---------:|----------:|
| AdoTupleToList               |  9.399 ms |  9.384 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Dapper_AsyncStream           |  9.488 ms |  9.463 ms | 312.5000 |        - |        - |   2.59 MB |
| EFCore_Compiled_AsyncStream  | 10.185 ms | 10.025 ms | 484.3750 |        - |        - |   3.97 MB |
| AdoWithDelegate              | 10.428 ms | 10.423 ms | 312.5000 | 265.6250 | 109.3750 |   2.39 MB |
| EFCore_AsyncStream           | 10.624 ms | 10.549 ms | 484.3750 |        - |        - |   3.98 MB |
| Nextorm_Cached_ToListAsync   | 11.292 ms | 11.273 ms | 265.6250 | 203.1250 |        - |   2.22 MB |
| Nextorm_Prepared_ToListAsync | 11.300 ms | 11.276 ms | 265.6250 | 203.1250 |        - |   2.21 MB |
| Nextorm_Cached_AsyncStream   | 11.608 ms | 11.601 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Prepared_AsyncStream | 11.649 ms | 11.657 ms | 265.6250 |        - |        - |   2.14 MB |
| Dapper_Async                 | 12.427 ms | 12.369 ms | 375.0000 | 250.0000 |  93.7500 |   2.84 MB |
| EFCore_ToListAsync           | 13.656 ms | 13.626 ms | 531.2500 | 281.2500 |  93.7500 |   4.23 MB |
