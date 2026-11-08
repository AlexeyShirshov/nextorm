```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V74 3.68GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  88.23 μs | 0.3662 |      - |   6.56 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 121.96 μs | 0.7324 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 125.98 μs | 0.7324 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 243.03 μs | 4.3945 | 1.9531 |  78.99 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             |  91.72 μs | 0.3662 |      - |   7.03 KB |
| A_Dapper_Cte                    | A_Cte             | 165.98 μs | 0.7324 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 166.36 μs | 0.9766 |      - |  17.74 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 101.87 μs | 0.3662 |      - |   6.56 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 134.96 μs | 0.7324 |      - |  15.94 KB |
| A_Dapper_Distinct               | A_Distinct        | 138.67 μs | 0.7324 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 263.48 μs | 4.3945 | 1.9531 |  76.64 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 102.36 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_Except                 | A_Except          | 145.39 μs | 0.7324 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 161.59 μs | 0.9766 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | A_Except          | 293.81 μs | 5.3711 | 2.4414 |  88.13 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_In           | A_InList          | 111.74 μs | 0.8545 |      - |  15.63 KB |
| A_Nextorm_Prepared_ListContains | A_InList          | 111.94 μs | 0.8545 |      - |  15.63 KB |
| A_Dapper_In                     | A_InList          | 140.01 μs | 0.7324 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 247.96 μs | 9.2773 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | A_InList          | 381.56 μs | 8.7891 | 4.3945 | 147.74 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       |  95.74 μs | 0.3662 |      - |   6.56 KB |
| A_Dapper_Intersect              | A_Intersect       | 135.77 μs | 0.7324 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 151.52 μs | 0.9766 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 279.80 μs | 4.3945 | 1.9531 |  79.38 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 119.52 μs | 0.4883 |      - |   9.84 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 188.66 μs | 1.2207 |      - |   20.7 KB |
| A_Dapper_Join4                  | A_Join4           | 211.81 μs | 1.2207 |      - |  20.24 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 330.39 μs | 4.8828 | 2.4414 |  84.92 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 115.16 μs | 0.6104 |      - |  11.33 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 167.88 μs | 1.2207 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 169.89 μs | 1.2207 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 293.02 μs | 5.8594 | 2.9297 |  99.22 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 110.81 μs | 0.3662 |      - |   6.64 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 178.79 μs | 0.7324 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 189.13 μs | 0.9766 |      - |  17.81 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  87.20 μs | 0.3662 |      - |   6.49 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  87.94 μs | 0.3662 |      - |    6.8 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 119.39 μs | 0.7324 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 125.22 μs | 0.9766 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 129.17 μs | 0.9766 |      - |  16.17 KB |
| A_Dapper_Contains               | A_StringFunctions | 148.32 μs | 0.9766 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 241.40 μs | 4.3945 | 2.1973 |  74.92 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 251.13 μs | 4.3945 | 1.9531 |  74.69 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  87.35 μs | 0.3662 |      - |    6.8 KB |
| A_Dapper_Udf                    | A_Udf             | 119.51 μs | 0.7324 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 125.10 μs | 0.9766 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 241.16 μs | 4.3945 | 2.1973 |  74.92 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 124.55 μs | 0.2441 |      - |   7.58 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 126.88 μs | 0.2441 |      - |   7.81 KB |
| A_Dapper_SumOver                | A_Window          | 204.32 μs | 0.7324 |      - |  15.47 KB |
| A_Dapper_RowNumber              | A_Window          | 205.75 μs | 0.7324 |      - |  15.39 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 211.33 μs | 0.9766 |      - |  17.42 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 213.01 μs | 0.9766 |      - |  17.34 KB |
