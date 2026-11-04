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
| A_Nextorm_Prepared_Count        | A_Aggregates              |    13.72 μs | 0.0458 |      - |     408 B |
| A_Linq2Db_Compiled_Count        | A_Aggregates              |    18.14 μs | 0.1526 |      - |    1280 B |
| A_EFCore_Compiled_Count         | A_Aggregates              |    36.89 μs | 0.9155 | 0.4272 |    7744 B |
| A_Nextorm_Prepared_Sum          | A_Aggregates              |   277.32 μs |      - |      - |     410 B |
| A_Linq2Db_Compiled_Sum          | A_Aggregates              |   291.06 μs |      - |      - |    1290 B |
| A_EFCore_Compiled_Sum           | A_Aggregates              |   312.23 μs | 0.4883 |      - |    7770 B |
| A_Nextorm_Prepared_GroupByCount | A_Aggregates              | 1,259.60 μs |      - |      - |    4737 B |
| A_Linq2Db_Compiled_GroupByCount | A_Aggregates              | 1,288.37 μs |      - |      - |    7025 B |
| A_EFCore_Compiled_GroupByCount  | A_Aggregates              | 1,333.68 μs | 3.9063 |      - |   33089 B |
|                                 |                           |             |        |        |           |
| Dapper_Count                    | A_Aggregates,B_Aggregates |    17.07 μs | 0.0916 |      - |     944 B |
| Dapper_Sum                      | A_Aggregates,B_Aggregates |   297.01 μs |      - |      - |     954 B |
| Dapper_GroupByCount             | A_Aggregates,B_Aggregates | 1,290.35 μs |      - |      - |   12329 B |
|                                 |                           |             |        |        |           |
| B_Nextorm_Cached_Count          | B_Aggregates              |    18.47 μs | 0.4272 |      - |    3824 B |
| B_Linq2Db_Count                 | B_Aggregates              |    24.37 μs | 0.3357 |      - |    2864 B |
| B_EFCore_Count                  | B_Aggregates              |    47.10 μs | 1.1597 | 0.5493 |   10080 B |
| B_Nextorm_Cached_Sum            | B_Aggregates              |   286.23 μs | 0.4883 |      - |    4226 B |
| B_Linq2Db_Sum                   | B_Aggregates              |   305.35 μs |      - |      - |    3490 B |
| B_EFCore_Sum                    | B_Aggregates              |   345.24 μs | 0.9766 | 0.4883 |   10978 B |
| B_Nextorm_Cached_GroupByCount   | B_Aggregates              | 1,275.88 μs |      - |      - |   10577 B |
| B_EFCore_GroupByCount           | B_Aggregates              | 1,397.49 μs | 3.9063 |      - |   42945 B |
| B_Linq2Db_GroupByCount          | B_Aggregates              | 1,415.67 μs |      - |      - |   11544 B |
