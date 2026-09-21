```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2    | Allocated |
|----------------------------- |----------:|---------:|---------:|--------:|----------:|
| Nextorm_Cached_AsyncStream   |  8.592 ms | 265.6250 |        - |       - |   2.14 MB |
| Nextorm_Cached_ToListAsync   |  8.834 ms | 265.6250 | 171.8750 |       - |   2.22 MB |
| Nextorm_Prepared_ToListAsync |  9.103 ms | 265.6250 | 156.2500 |       - |   2.21 MB |
| Dapper_AsyncStream           |  9.226 ms | 312.5000 |        - |       - |   2.59 MB |
| Nextorm_Prepared_AsyncStream |  9.287 ms | 265.6250 |        - |       - |   2.14 MB |
| AdoTupleToList               |  9.764 ms | 281.2500 | 187.5000 | 62.5000 |   2.68 MB |
| Linq2Db_AsyncStream          | 10.350 ms | 265.6250 |        - |       - |   2.14 MB |
| EFCore_Compiled_AsyncStream  | 10.395 ms | 531.2500 |        - |       - |   4.28 MB |
| EFCore_AsyncStream           | 11.036 ms | 531.2500 |        - |       - |   4.28 MB |
| AdoWithDelegate              | 11.526 ms | 328.1250 | 218.7500 | 46.8750 |   2.39 MB |
| Dapper_Async                 | 12.086 ms | 375.0000 | 234.3750 | 46.8750 |   2.85 MB |
| Linq2Db_ToListAsync          | 12.346 ms | 328.1250 | 218.7500 | 46.8750 |   2.39 MB |
| EFCore_ToListAsync           | 14.713 ms | 609.3750 | 265.6250 | 62.5000 |   4.53 MB |
