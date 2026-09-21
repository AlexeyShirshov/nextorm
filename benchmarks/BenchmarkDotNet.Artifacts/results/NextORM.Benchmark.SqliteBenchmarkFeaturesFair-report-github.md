```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories     | Mean      | Gen0    | Gen1   | Allocated |
|-------------------------------- |--------------- |----------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_Cte          | A_Cte          |  98.27 μs |  0.8545 |      - |   7.73 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte          | 230.97 μs |  1.9531 |      - |  17.73 KB |
| A_Dapper_Cte                    | A_Cte          | 234.96 μs |  1.7090 |      - |   15.7 KB |
|                                 |                |           |         |        |           |
| A_Nextorm_Prepared_ListContains | A_InList       | 132.15 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_In           | A_InList       | 135.44 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | A_InList       | 209.38 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | A_InList       | 515.55 μs | 17.5781 |      - |  151.1 KB |
| A_EFCore_Compiled_In            | A_InList       | 625.39 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                |           |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4        | 140.18 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4        | 270.32 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | A_Join4        | 297.88 μs |  2.4414 |      - |  20.23 KB |
| A_EFCore_Compiled_Join4         | A_Join4        | 552.66 μs |  9.7656 | 4.8828 |  84.93 KB |
|                                 |                |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte | 126.78 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte | 256.59 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte | 273.14 μs |  1.9531 |      - |  17.81 KB |
