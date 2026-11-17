```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                          | Categories                | Mean        | Gen0   | Gen1   | Allocated |
|-------------------------------- |-------------------------- |------------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_Count        | A_Aggregates              |    15.11 μs | 0.0458 |      - |     400 B |
| A_Linq2Db_Compiled_Count        | A_Aggregates              |    19.05 μs | 0.1221 |      - |    1272 B |
| A_EFCore_Compiled_Count         | A_Aggregates              |    36.69 μs | 0.8545 | 0.4272 |    7296 B |
| A_Nextorm_Prepared_Sum          | A_Aggregates              |   274.59 μs |      - |      - |     403 B |
| A_Linq2Db_Compiled_Sum          | A_Aggregates              |   297.32 μs |      - |      - |    1283 B |
| A_EFCore_Compiled_Sum           | A_Aggregates              |   315.38 μs | 0.4883 |      - |    7323 B |
| A_Nextorm_Prepared_GroupByCount | A_Aggregates              | 1,297.07 μs |      - |      - |    4698 B |
| A_Linq2Db_Compiled_GroupByCount | A_Aggregates              | 1,331.58 μs |      - |      - |    7018 B |
| A_EFCore_Compiled_GroupByCount  | A_Aggregates              | 1,419.58 μs | 1.9531 |      - |   32642 B |
|                                 |                           |             |        |        |           |
| Dapper_Count                    | A_Aggregates,B_Aggregates |    19.55 μs | 0.0916 |      - |     936 B |
| Dapper_Sum                      | A_Aggregates,B_Aggregates |   290.92 μs |      - |      - |     947 B |
| Dapper_GroupByCount             | A_Aggregates,B_Aggregates | 1,336.72 μs |      - |      - |   12322 B |
|                                 |                           |             |        |        |           |
| B_Nextorm_Cached_Count          | B_Aggregates              |    15.99 μs | 0.0305 |      - |     400 B |
| B_Linq2Db_Count                 | B_Aggregates              |    24.88 μs | 0.3357 |      - |    2848 B |
| B_EFCore_Count                  | B_Aggregates              |    49.82 μs | 1.0986 | 0.5493 |    9616 B |
| B_Nextorm_Cached_Sum            | B_Aggregates              |   282.05 μs |      - |      - |     963 B |
| B_Linq2Db_Sum                   | B_Aggregates              |   305.87 μs |      - |      - |    3475 B |
| B_EFCore_Sum                    | B_Aggregates              |   334.58 μs | 0.9766 | 0.4883 |   10515 B |
| B_Nextorm_Cached_GroupByCount   | B_Aggregates              | 1,342.22 μs |      - |      - |    8650 B |
| B_Linq2Db_GroupByCount          | B_Aggregates              | 1,377.85 μs |      - |      - |   11514 B |
| B_EFCore_GroupByCount           | B_Aggregates              | 1,426.02 μs | 3.9063 |      - |   42154 B |
