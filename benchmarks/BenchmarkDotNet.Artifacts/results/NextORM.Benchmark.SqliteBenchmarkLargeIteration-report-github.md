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
| Nextorm_Prepared_AsyncStream |  9.188 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Prepared_ToListAsync |  9.322 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| Nextorm_Cached_AsyncStream   |  9.504 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Cached_ToListAsync   |  9.544 ms | 265.6250 | 203.1250 |        - |   2.22 MB |
| AdoTupleToList               | 10.065 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Dapper_AsyncStream           | 10.154 ms | 312.5000 |        - |        - |   2.59 MB |
| Linq2Db_AsyncStream          | 10.220 ms | 265.6250 |        - |        - |   2.14 MB |
| EFCore_Compiled_AsyncStream  | 10.756 ms | 531.2500 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           | 11.073 ms | 531.2500 |        - |        - |   4.28 MB |
| AdoWithDelegate              | 11.486 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| Linq2Db_Compiled_ToList      | 12.406 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| Dapper_Async                 | 12.473 ms | 375.0000 | 250.0000 |  78.1250 |   2.85 MB |
| Linq2Db_ToListAsync          | 12.525 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| EFCore_ToListAsync           | 14.843 ms | 625.0000 | 406.2500 |  93.7500 |   4.53 MB |
