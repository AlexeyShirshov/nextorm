```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Categories        | Mean       | Median     | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   137.5 μs |   137.0 μs |  2.4414 |      - |  20.63 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   156.2 μs |   155.9 μs |  1.4648 |      - |  13.36 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   252.7 μs |   252.9 μs |  4.3945 |      - |  37.74 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   585.7 μs |   587.5 μs | 13.6719 | 5.8594 | 114.62 KB |
|                                           |                   |            |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   233.2 μs |   232.7 μs |  1.7090 |      - |  15.63 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   334.0 μs |   334.5 μs | 11.2305 |      - |  94.54 KB |
| B_Linq2Db_Cte                             | B_Cte             |   586.2 μs |   587.6 μs |  9.7656 |      - |  90.17 KB |
|                                           |                   |            |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   169.5 μs |   168.8 μs |  3.6621 |      - |  30.55 KB |
| B_Dapper_Distinct                         | B_Distinct        |   190.8 μs |   191.1 μs |  1.7090 |      - |  14.45 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   307.9 μs |   307.2 μs |  4.3945 |      - |  38.75 KB |
| B_EFCore_Distinct                         | B_Distinct        |   618.5 μs |   620.4 μs | 13.6719 | 6.8359 | 118.13 KB |
|                                           |                   |            |            |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |   186.7 μs |   187.0 μs |  5.1270 |      - |  42.34 KB |
| B_Dapper_Except                           | B_Except          |   189.2 μs |   189.2 μs |  1.7090 |      - |   14.3 KB |
| B_Linq2Db_Except                          | B_Except          |   387.7 μs |   388.5 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Except                           | B_Except          |   707.0 μs |   707.1 μs | 17.5781 | 5.8594 | 147.35 KB |
|                                           |                   |            |            |         |        |           |
| B_Dapper_In                               | B_InList          |   201.8 μs |   198.3 μs |  1.4648 |      - |  12.58 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   360.0 μs |   360.8 μs |  8.3008 |      - |  70.86 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   371.0 μs |   371.1 μs | 10.7422 |      - |  89.54 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   394.1 μs |   390.6 μs |  8.7891 |      - |   73.6 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   410.8 μs |   408.8 μs | 11.2305 |      - |   92.9 KB |
| B_Linq2Db_In                              | B_InList          |   727.1 μs |   726.1 μs | 20.5078 |      - | 172.82 KB |
| B_EFCore_In                               | B_InList          | 1,066.0 μs | 1,066.4 μs | 25.3906 | 7.8125 |  221.9 KB |
|                                           |                   |            |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   174.5 μs |   174.3 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   181.7 μs |   182.0 μs |  5.1270 |      - |  42.19 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   375.2 μs |   374.8 μs |  5.8594 |      - |  49.61 KB |
| B_EFCore_Intersect                        | B_Intersect       |   714.4 μs |   716.7 μs | 16.6016 | 4.8828 | 138.05 KB |
|                                           |                   |            |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   266.4 μs |   266.4 μs |  2.4414 |      - |  20.16 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   372.7 μs |   369.3 μs | 12.2070 |      - | 102.12 KB |
| B_Linq2Db_Join4                           | B_Join4           |   665.6 μs |   664.6 μs | 16.6016 |      - |  141.5 KB |
| B_EFCore_Join4                            | B_Join4           | 1,145.7 μs | 1,141.9 μs | 29.2969 | 9.7656 | 243.84 KB |
|                                           |                   |            |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   214.5 μs |   215.0 μs |  2.4414 |      - |  22.11 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   256.2 μs |   255.0 μs |  6.8359 |      - |  57.11 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   700.1 μs |   695.1 μs | 10.7422 |      - |  90.47 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        |   948.2 μs |   949.0 μs | 23.4375 | 7.8125 | 199.08 KB |
|                                           |                   |            |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   267.5 μs |   266.7 μs |  1.4648 |      - |  15.24 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   355.8 μs |   351.7 μs | 12.2070 |      - | 101.02 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   619.6 μs |   620.2 μs |  9.7656 |      - |  83.13 KB |
|                                           |                   |            |            |         |        |           |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   154.5 μs |   154.5 μs |  3.4180 |      - |  28.13 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   156.1 μs |   155.9 μs |  3.4180 |      - |  28.67 KB |
| B_Dapper_ToUpper                          | B_StringFunctions |   157.3 μs |   157.4 μs |  1.4648 |      - |  12.74 KB |
| B_Dapper_Contains                         | B_StringFunctions |   207.7 μs |   207.8 μs |  1.9531 |      - |  17.11 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   318.2 μs |   318.1 μs |  5.3711 |      - |  47.42 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   498.6 μs |   496.8 μs |  7.8125 |      - |  67.51 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   668.2 μs |   657.4 μs | 14.6484 | 4.8828 | 127.43 KB |
| B_EFCore_Contains                         | B_StringFunctions |   685.8 μs |   684.9 μs | 15.6250 | 4.8828 | 134.77 KB |
|                                           |                   |            |            |         |        |           |
| B_Nextorm_Cached_Udf                      | B_Udf             |   156.8 μs |   155.8 μs |  3.4180 |      - |  28.99 KB |
| B_Dapper_Udf                              | B_Udf             |   158.8 μs |   158.9 μs |  1.4648 |      - |  12.74 KB |
| B_Linq2Db_Udf                             | B_Udf             |   494.2 μs |   494.1 μs |  7.8125 |      - |  67.51 KB |
| B_EFCore_Udf                              | B_Udf             |   654.6 μs |   655.1 μs | 14.6484 | 4.8828 | 127.43 KB |
|                                           |                   |            |            |         |        |           |
| B_Nextorm_Cached_RowNumber                | B_Window          |   308.9 μs |   308.3 μs |  4.8828 |      - |  40.86 KB |
| B_Dapper_RowNumber                        | B_Window          |   327.6 μs |   325.9 μs |  1.4648 |      - |  15.32 KB |
| B_Dapper_SumOver                          | B_Window          |   353.8 μs |   344.9 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   360.8 μs |   352.2 μs |  4.8828 |      - |  41.88 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   626.0 μs |   626.1 μs |  6.8359 |      - |  59.54 KB |
| B_Linq2Db_SumOver                         | B_Window          |   688.9 μs |   685.9 μs |  6.8359 |      - |   57.9 KB |
