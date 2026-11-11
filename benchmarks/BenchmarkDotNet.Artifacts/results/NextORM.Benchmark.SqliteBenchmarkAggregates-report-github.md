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
| A_Nextorm_Prepared_Count        | A_Aggregates              |    14.65 μs | 0.0458 |      - |     408 B |
| A_Linq2Db_Compiled_Count        | A_Aggregates              |    19.26 μs | 0.1526 |      - |    1280 B |
| A_EFCore_Compiled_Count         | A_Aggregates              |    40.92 μs | 0.9155 | 0.4272 |    7744 B |
| A_Nextorm_Prepared_Sum          | A_Aggregates              |   298.17 μs |      - |      - |     410 B |
| A_Linq2Db_Compiled_Sum          | A_Aggregates              |   316.45 μs |      - |      - |    1290 B |
| A_EFCore_Compiled_Sum           | A_Aggregates              |   334.54 μs | 0.4883 |      - |    7770 B |
| A_Nextorm_Prepared_GroupByCount | A_Aggregates              | 1,468.98 μs |      - |      - |    4666 B |
| A_EFCore_Compiled_GroupByCount  | A_Aggregates              | 1,501.91 μs | 3.9063 |      - |   33090 B |
| A_Linq2Db_Compiled_GroupByCount | A_Aggregates              | 1,511.01 μs |      - |      - |    7026 B |
|                                 |                           |             |        |        |           |
| Dapper_Count                    | A_Aggregates,B_Aggregates |    18.23 μs | 0.0916 |      - |     944 B |
| Dapper_Sum                      | A_Aggregates,B_Aggregates |   317.79 μs |      - |      - |     954 B |
| Dapper_GroupByCount             | A_Aggregates,B_Aggregates | 1,477.65 μs |      - |      - |   12330 B |
|                                 |                           |             |        |        |           |
| B_Nextorm_Cached_Count          | B_Aggregates              |    15.39 μs | 0.0458 |      - |     408 B |
| B_Linq2Db_Count                 | B_Aggregates              |    29.52 μs | 0.3357 |      - |    3008 B |
| B_EFCore_Count                  | B_Aggregates              |    64.48 μs | 1.1597 | 0.5493 |   10080 B |
| B_Nextorm_Cached_Sum            | B_Aggregates              |   314.20 μs |      - |      - |     970 B |
| B_Linq2Db_Sum                   | B_Aggregates              |   367.16 μs |      - |      - |    3490 B |
| B_EFCore_Sum                    | B_Aggregates              |   371.48 μs | 0.9766 | 0.4883 |   10978 B |
| B_Nextorm_Cached_GroupByCount   | B_Aggregates              | 1,554.85 μs |      - |      - |   10146 B |
| B_Linq2Db_GroupByCount          | B_Aggregates              | 1,675.00 μs |      - |      - |   11554 B |
| B_EFCore_GroupByCount           | B_Aggregates              | 1,886.06 μs | 3.9063 |      - |   42754 B |
