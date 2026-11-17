```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   136.3 μs |  2.4414 |      - |  20.63 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   155.9 μs |  1.4648 |      - |  13.36 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   253.1 μs |  4.3945 |      - |  37.74 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   574.1 μs | 13.6719 | 5.8594 | 114.62 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   229.1 μs |  1.7090 |      - |  15.63 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   325.6 μs | 11.2305 |      - |  94.93 KB |
| B_Linq2Db_Cte                             | B_Cte             |   565.3 μs | 10.7422 |      - |  93.68 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   175.2 μs |  3.6621 |      - |  30.55 KB |
| B_Dapper_Distinct                         | B_Distinct        |   186.7 μs |  1.7090 |      - |  14.45 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   308.4 μs |  4.3945 |      - |  38.75 KB |
| B_EFCore_Distinct                         | B_Distinct        |   631.8 μs | 13.6719 | 6.8359 | 116.96 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |   183.4 μs |  5.1270 |      - |  42.34 KB |
| B_Dapper_Except                           | B_Except          |   189.2 μs |  1.7090 |      - |   14.3 KB |
| B_Linq2Db_Except                          | B_Except          |   383.0 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Except                           | B_Except          |   713.9 μs | 17.5781 | 5.8594 | 147.35 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   198.2 μs |  1.4648 |      - |  12.58 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   437.3 μs | 10.2539 |      - |  87.74 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   451.6 μs | 12.6953 |      - | 105.17 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   455.8 μs | 10.7422 |      - |  89.38 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   460.9 μs | 12.6953 |      - | 107.43 KB |
| B_Linq2Db_In                              | B_InList          |   682.9 μs | 20.5078 |      - | 172.82 KB |
| B_EFCore_In                               | B_InList          | 1,029.5 μs | 25.3906 | 7.8125 | 220.49 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   175.1 μs |  5.1270 |      - |  42.19 KB |
| B_Dapper_Intersect                        | B_Intersect       |   180.1 μs |  1.4648 |      - |  12.81 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   390.5 μs |  5.8594 |      - |  49.61 KB |
| B_EFCore_Intersect                        | B_Intersect       |   708.8 μs | 16.6016 | 4.8828 | 138.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   274.8 μs |  2.4414 |      - |  20.16 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   356.8 μs | 12.2070 |      - | 100.17 KB |
| B_Linq2Db_Join4                           | B_Join4           |   650.0 μs | 16.6016 |      - | 141.42 KB |
| B_EFCore_Join4                            | B_Join4           | 1,149.1 μs | 27.3438 | 7.8125 |    239 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   217.9 μs |  2.6855 |      - |  22.11 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   263.2 μs |  6.8359 |      - |  57.43 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   694.9 μs | 10.7422 |      - |  89.38 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        |   986.1 μs | 23.4375 | 7.8125 | 197.68 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   267.0 μs |  1.4648 |      - |  15.24 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   365.3 μs | 12.2070 |      - | 101.02 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   630.8 μs |  9.7656 |      - |  84.31 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   153.4 μs |  3.4180 |      - |  28.13 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   156.8 μs |  3.4180 |      - |  28.67 KB |
| B_Dapper_ToUpper                          | B_StringFunctions |   163.4 μs |  1.4648 |      - |  12.74 KB |
| B_Dapper_Contains                         | B_StringFunctions |   210.1 μs |  1.9531 |      - |  17.11 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   318.9 μs |  5.3711 |      - |  47.42 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   508.8 μs |  7.8125 |      - |  67.51 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   669.2 μs | 14.6484 | 4.8828 | 127.43 KB |
| B_EFCore_Contains                         | B_StringFunctions |   693.4 μs | 15.6250 | 4.8828 | 134.77 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Udf                      | B_Udf             |   157.1 μs |  3.4180 |      - |  28.99 KB |
| B_Dapper_Udf                              | B_Udf             |   162.6 μs |  1.4648 |      - |  12.74 KB |
| B_Linq2Db_Udf                             | B_Udf             |   500.7 μs |  7.8125 |      - |  67.51 KB |
| B_EFCore_Udf                              | B_Udf             |   668.5 μs | 14.6484 | 4.8828 | 127.43 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_RowNumber                | B_Window          |   310.1 μs |  4.8828 |      - |  40.86 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   313.7 μs |  4.8828 |      - |  41.88 KB |
| B_Dapper_RowNumber                        | B_Window          |   319.9 μs |  1.4648 |      - |  15.32 KB |
| B_Dapper_SumOver                          | B_Window          |   322.9 μs |  1.4648 |      - |  15.39 KB |
| B_Linq2Db_SumOver                         | B_Window          |   614.6 μs |  6.8359 |      - |   57.9 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   634.5 μs |  6.8359 |      - |  60.63 KB |
