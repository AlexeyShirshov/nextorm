```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  87.94 μs | 0.3662 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 122.13 μs | 0.7324 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 124.79 μs | 0.7324 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 245.78 μs | 4.3945 | 1.9531 |  78.99 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             |  91.28 μs | 0.3662 |      - |   7.74 KB |
| A_Dapper_Cte                    | A_Cte             | 166.01 μs | 0.7324 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 166.32 μs | 0.9766 |      - |  17.73 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 101.66 μs | 0.3662 |      - |   7.27 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 135.30 μs | 0.7324 |      - |  15.94 KB |
| A_Dapper_Distinct               | A_Distinct        | 138.35 μs | 0.7324 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 266.89 μs | 4.3945 | 1.9531 |  76.64 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 102.51 μs | 0.3662 |      - |   7.42 KB |
| A_Dapper_Except                 | A_Except          | 145.57 μs | 0.7324 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 159.94 μs | 0.9766 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | A_Except          | 289.94 μs | 5.3711 | 2.4414 |  88.13 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_In           | A_InList          | 111.52 μs | 0.9766 |      - |  16.33 KB |
| A_Nextorm_Prepared_ListContains | A_InList          | 111.62 μs | 0.9766 |      - |  16.33 KB |
| A_Dapper_In                     | A_InList          | 141.21 μs | 0.7324 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 251.27 μs | 9.2773 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | A_InList          | 367.07 μs | 8.7891 | 4.3945 | 147.74 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       |  96.00 μs | 0.3662 |      - |   7.27 KB |
| A_Dapper_Intersect              | A_Intersect       | 136.62 μs | 0.7324 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 150.47 μs | 0.9766 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 274.09 μs | 4.3945 | 1.9531 |  79.38 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 118.67 μs | 0.6104 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 187.94 μs | 1.2207 |      - |   20.7 KB |
| A_Dapper_Join4                  | A_Join4           | 212.90 μs | 1.2207 |      - |  20.24 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 330.37 μs | 4.8828 | 2.4414 |  84.92 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 114.97 μs | 0.7324 |      - |  12.03 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 167.40 μs | 1.2207 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 168.94 μs | 1.2207 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 296.24 μs | 5.8594 | 2.9297 |  99.22 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 111.14 μs | 0.3662 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 180.51 μs | 0.7324 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 188.60 μs | 0.9766 |      - |  17.81 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  87.30 μs | 0.3662 |      - |   7.19 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  87.40 μs | 0.3662 |      - |    7.5 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 119.18 μs | 0.7324 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 124.51 μs | 0.9766 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 130.01 μs | 0.9766 |      - |  16.17 KB |
| A_Dapper_Contains               | A_StringFunctions | 150.03 μs | 0.9766 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 245.64 μs | 4.3945 | 1.9531 |  74.92 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 251.20 μs | 4.3945 | 1.9531 |  74.69 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  87.34 μs | 0.3662 |      - |    7.5 KB |
| A_Dapper_Udf                    | A_Udf             | 119.76 μs | 0.7324 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 124.01 μs | 0.9766 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 239.35 μs | 4.3945 | 2.1973 |  74.92 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 125.02 μs | 0.4883 |      - |   8.28 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 126.29 μs | 0.4883 |      - |   8.52 KB |
| A_Dapper_SumOver                | A_Window          | 205.12 μs | 0.7324 |      - |  15.47 KB |
| A_Dapper_RowNumber              | A_Window          | 209.07 μs | 0.7324 |      - |  15.39 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 212.48 μs | 0.9766 |      - |  17.42 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 213.68 μs | 0.9766 |      - |  17.34 KB |
