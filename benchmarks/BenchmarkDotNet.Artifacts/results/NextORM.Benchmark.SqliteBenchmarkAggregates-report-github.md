```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories                | Mean        | Gen0   | Gen1   | Allocated |
|-------------------------------- |-------------------------- |------------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_Count        | A_Aggregates              |    13.77 μs | 0.0458 |      - |     408 B |
| A_Linq2Db_Compiled_Count        | A_Aggregates              |    18.02 μs | 0.1526 |      - |    1280 B |
| A_EFCore_Compiled_Count         | A_Aggregates              |    38.62 μs | 0.9155 | 0.4272 |    7744 B |
| A_Nextorm_Prepared_Sum          | A_Aggregates              |   277.57 μs |      - |      - |     410 B |
| A_Linq2Db_Compiled_Sum          | A_Aggregates              |   291.81 μs |      - |      - |    1290 B |
| A_EFCore_Compiled_Sum           | A_Aggregates              |   319.95 μs | 0.4883 |      - |    7770 B |
| A_Nextorm_Prepared_GroupByCount | A_Aggregates              | 1,281.91 μs |      - |      - |    4737 B |
| A_Linq2Db_Compiled_GroupByCount | A_Aggregates              | 1,309.32 μs |      - |      - |    7025 B |
| A_EFCore_Compiled_GroupByCount  | A_Aggregates              | 1,343.09 μs | 3.9063 |      - |   33089 B |
|                                 |                           |             |        |        |           |
| Dapper_Count                    | A_Aggregates,B_Aggregates |    17.30 μs | 0.0916 |      - |     944 B |
| Dapper_Sum                      | A_Aggregates,B_Aggregates |   291.59 μs |      - |      - |     954 B |
| Dapper_GroupByCount             | A_Aggregates,B_Aggregates | 1,317.46 μs |      - |      - |   12329 B |
|                                 |                           |             |        |        |           |
| B_Nextorm_Cached_Count          | B_Aggregates              |    19.31 μs | 0.4272 |      - |    3824 B |
| B_Linq2Db_Count                 | B_Aggregates              |    24.56 μs | 0.3357 |      - |    2864 B |
| B_EFCore_Count                  | B_Aggregates              |    48.88 μs | 1.1597 | 0.5493 |   10080 B |
| B_Nextorm_Cached_Sum            | B_Aggregates              |   286.43 μs | 0.4883 |      - |    4226 B |
| B_Linq2Db_Sum                   | B_Aggregates              |   304.90 μs |      - |      - |    3490 B |
| B_EFCore_Sum                    | B_Aggregates              |   338.05 μs | 0.9766 | 0.4883 |   10978 B |
| B_Nextorm_Cached_GroupByCount   | B_Aggregates              | 1,314.02 μs |      - |      - |   10577 B |
| B_Linq2Db_GroupByCount          | B_Aggregates              | 1,393.44 μs |      - |      - |   11553 B |
| B_EFCore_GroupByCount           | B_Aggregates              | 1,407.25 μs | 3.9063 |      - |   42641 B |
