```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                          | Categories                | Mean        | Gen0   | Gen1   | Allocated |
|-------------------------------- |-------------------------- |------------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_Count        | A_Aggregates              |    15.27 μs | 0.0305 |      - |     400 B |
| A_Linq2Db_Compiled_Count        | A_Aggregates              |    19.70 μs | 0.1221 |      - |    1272 B |
| A_EFCore_Compiled_Count         | A_Aggregates              |    39.61 μs | 0.8545 | 0.4272 |    7296 B |
| A_Nextorm_Prepared_Sum          | A_Aggregates              |   282.65 μs |      - |      - |     403 B |
| A_Linq2Db_Compiled_Sum          | A_Aggregates              |   294.11 μs |      - |      - |    1283 B |
| A_EFCore_Compiled_Sum           | A_Aggregates              |   314.81 μs | 0.4883 |      - |    7323 B |
| A_Nextorm_Prepared_GroupByCount | A_Aggregates              | 1,338.19 μs |      - |      - |    4698 B |
| A_Linq2Db_Compiled_GroupByCount | A_Aggregates              | 1,369.71 μs |      - |      - |    7018 B |
| A_EFCore_Compiled_GroupByCount  | A_Aggregates              | 1,401.89 μs | 1.9531 |      - |   32642 B |
|                                 |                           |             |        |        |           |
| Dapper_Count                    | A_Aggregates,B_Aggregates |    18.55 μs | 0.0916 |      - |     936 B |
| Dapper_Sum                      | A_Aggregates,B_Aggregates |   295.30 μs |      - |      - |     947 B |
| Dapper_GroupByCount             | A_Aggregates,B_Aggregates | 1,486.63 μs |      - |      - |   12322 B |
|                                 |                           |             |        |        |           |
| B_Nextorm_Cached_Count          | B_Aggregates              |    15.41 μs | 0.0305 |      - |     400 B |
| B_Linq2Db_Count                 | B_Aggregates              |    26.00 μs | 0.3357 |      - |    2848 B |
| B_EFCore_Count                  | B_Aggregates              |    47.36 μs | 1.0986 | 0.5493 |    9616 B |
| B_Nextorm_Cached_Sum            | B_Aggregates              |   279.90 μs |      - |      - |     963 B |
| B_Linq2Db_Sum                   | B_Aggregates              |   306.10 μs |      - |      - |    3475 B |
| B_EFCore_Sum                    | B_Aggregates              |   325.55 μs | 0.9766 | 0.4883 |   10515 B |
| B_Nextorm_Cached_GroupByCount   | B_Aggregates              | 1,340.93 μs |      - |      - |    8730 B |
| B_Linq2Db_GroupByCount          | B_Aggregates              | 1,391.61 μs |      - |      - |   11738 B |
| B_EFCore_GroupByCount           | B_Aggregates              | 1,454.86 μs | 3.9063 |      - |   42155 B |
