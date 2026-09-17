```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   130.4 μs |  2.9297 |      - |  25.55 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   151.5 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   257.8 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   612.0 μs | 13.6719 | 6.8359 | 119.54 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   218.5 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   273.2 μs | 10.2539 |      - |  87.04 KB |
| B_Linq2Db_Cte                             | B_Cte             |   598.1 μs | 10.7422 |      - |  90.63 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   155.4 μs |  3.1738 |      - |  26.56 KB |
| B_Dapper_Distinct                         | B_Distinct        |   187.3 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   315.4 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        |   671.5 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |   181.7 μs |  5.3711 |      - |  45.63 KB |
| B_Dapper_Except                           | B_Except          |   190.3 μs |  1.7090 |      - |  14.38 KB |
| B_Linq2Db_Except                          | B_Except          |   422.4 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          |   820.0 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   208.3 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   296.9 μs |  7.8125 |      - |  65.55 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   302.7 μs |  8.3008 |      - |  68.75 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   321.8 μs |  8.3008 |      - |  67.89 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   327.6 μs |  8.3008 |      - |   71.1 KB |
| B_Linq2Db_In                              | B_InList          |   778.5 μs | 22.4609 |      - | 189.15 KB |
| B_EFCore_In                               | B_InList          | 1,179.3 μs | 27.3438 | 7.8125 | 226.12 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   168.3 μs |  5.3711 |      - |  45.47 KB |
| B_Dapper_Intersect                        | B_Intersect       |   184.0 μs |  1.4648 |      - |  12.89 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   440.2 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       |   867.2 μs | 17.5781 | 5.8594 | 144.93 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   293.2 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   342.9 μs | 11.2305 |      - |  93.68 KB |
| B_Linq2Db_Join4                           | B_Join4           |   794.9 μs | 16.6016 |      - | 140.55 KB |
| B_EFCore_Join4                            | B_Join4           | 1,394.6 μs | 29.2969 | 7.8125 | 247.75 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   231.6 μs |  6.1035 |      - |  51.72 KB |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   250.5 μs |  2.4414 |      - |  22.19 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   906.9 μs |  9.7656 |      - |  87.11 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 1,115.9 μs | 23.4375 | 7.8125 | 202.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   308.1 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   377.3 μs | 10.7422 |      - |  88.91 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   756.1 μs |  9.7656 |      - |  84.69 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |   149.3 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   151.7 μs |  4.3945 |      - |  36.17 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   154.4 μs |  4.3945 |      - |  36.64 KB |
| B_Dapper_Contains                         | B_StringFunctions |   203.9 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   331.8 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   506.7 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   701.0 μs | 15.6250 | 4.8828 | 132.74 KB |
| B_EFCore_Contains                         | B_StringFunctions |   742.5 μs | 16.6016 | 4.8828 | 140.09 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   153.6 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   155.1 μs |  4.3945 |      - |   37.5 KB |
| B_Linq2Db_Udf                             | B_Udf             |   512.4 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             |   726.7 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_SumOver                          | B_Window          |   329.5 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | B_Window          |   333.3 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   351.3 μs |  5.3711 |      - |   47.5 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   358.9 μs |  5.8594 |      - |  48.91 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   681.2 μs |  6.8359 |      - |  59.85 KB |
| B_Linq2Db_SumOver                         | B_Window          |   696.3 μs |  6.8359 |      - |  58.21 KB |
