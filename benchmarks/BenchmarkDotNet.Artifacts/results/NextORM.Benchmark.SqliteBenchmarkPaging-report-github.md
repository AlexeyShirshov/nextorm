```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                       | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|----------------------------- |------------------ |----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_PageAsync | A_Paging          |  88.62 μs | 1.7090 |      - |  13.99 KB |
| A_Linq2Db_Compiled_Page      | A_Paging          | 112.54 μs | 2.0752 |      - |  17.61 KB |
| A_EFCore_Compiled_PageAsync  | A_Paging          | 134.25 μs | 5.3711 | 0.9766 |  44.81 KB |
|                              |                   |           |        |        |           |
| A_Dapper_PageAsync           | A_Paging,B_Paging | 103.67 μs | 2.1973 |      - |  18.15 KB |
|                              |                   |           |        |        |           |
| B_Nextorm_Cached_PageAsync   | B_Paging          | 101.31 μs | 2.6855 |      - |  22.62 KB |
| B_Linq2Db_PageAsync          | B_Paging          | 140.91 μs | 2.6855 |      - |  22.65 KB |
| B_EFCore_PageAsync           | B_Paging          | 185.84 μs | 6.8359 | 1.2207 |   56.9 KB |
