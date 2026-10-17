```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|---------:|---------:|---------:|----------:|
| Nextorm_Prepared_AsyncStream |  8.331 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Prepared_ToListAsync |  8.344 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| AdoTupleToList               |  8.389 ms | 437.5000 | 406.2500 | 218.7500 |   2.68 MB |
| Nextorm_Cached_ToListAsync   |  8.425 ms | 265.6250 | 203.1250 |        - |   2.22 MB |
| Nextorm_Cached_AsyncStream   |  8.509 ms | 265.6250 |        - |        - |   2.14 MB |
| Dapper_AsyncStream           |  8.680 ms | 312.5000 |        - |        - |   2.59 MB |
| Linq2Db_AsyncStream          |  9.366 ms | 265.6250 |        - |        - |   2.14 MB |
| EFCore_Compiled_AsyncStream  |  9.771 ms | 531.2500 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           |  9.922 ms | 531.2500 |        - |        - |   4.28 MB |
| AdoWithDelegate              | 10.460 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| Linq2Db_Compiled_ToList      | 11.083 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| Dapper_Async                 | 11.502 ms | 406.2500 | 296.8750 |  78.1250 |   2.85 MB |
| Linq2Db_ToListAsync          | 11.742 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| EFCore_ToListAsync           | 13.880 ms | 640.6250 | 375.0000 |  93.7500 |   4.53 MB |
