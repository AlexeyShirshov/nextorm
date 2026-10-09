```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                         | Categories                | Mean     | Gen0     | Gen1     | Gen2    | Allocated |
|------------------------------- |-------------------------- |---------:|---------:|---------:|--------:|----------:|
| A_Nextorm_Prepared_ToListAsync | A_Projection              | 3.653 ms | 160.1563 | 117.1875 |       - |    1.3 MB |
| A_EFCore_Compiled_ToListAsync  | A_Projection              | 4.904 ms | 414.0625 |        - |       - |   3.36 MB |
| A_Linq2Db_Compiled_ToList      | A_Projection              | 5.522 ms | 218.7500 | 210.9375 | 54.6875 |   1.47 MB |
|                                |                           |          |          |          |         |           |
| A_Dapper_ToListAsync           | A_Projection,B_Projection | 6.464 ms | 257.8125 | 250.0000 | 62.5000 |    1.7 MB |
|                                |                           |          |          |          |         |           |
| B_Nextorm_Cached_ToListAsync   | B_Projection              | 3.799 ms | 160.1563 | 109.3750 |       - |    1.3 MB |
| B_Linq2Db_ToListAsync          | B_Projection              | 6.472 ms | 218.7500 | 210.9375 | 54.6875 |   1.48 MB |
| B_EFCore_ToListAsync           | B_Projection              | 8.533 ms | 515.6250 | 312.5000 | 78.1250 |   3.62 MB |
