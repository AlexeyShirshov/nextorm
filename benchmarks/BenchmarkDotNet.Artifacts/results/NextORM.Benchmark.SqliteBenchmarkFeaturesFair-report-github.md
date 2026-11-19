```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.24GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  97.47 μs | 0.3662 |      - |   6.48 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 149.32 μs | 0.7324 |      - |  13.36 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 158.42 μs | 0.7324 |      - |  15.86 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 381.56 μs | 4.3945 | 1.9531 |  74.61 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             | 103.39 μs | 0.3662 |      - |   7.34 KB |
| A_Dapper_Cte                    | A_Cte             | 228.64 μs | 0.7324 |      - |  15.63 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 236.17 μs | 0.9766 |      - |  17.66 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 116.31 μs | 0.3662 |      - |   6.49 KB |
| A_Dapper_Distinct               | A_Distinct        | 186.11 μs | 0.7324 |      - |  14.45 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 190.67 μs | 0.7324 |      - |  15.86 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 438.19 μs | 4.3945 | 1.9531 |  72.27 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 117.37 μs | 0.3662 |      - |   6.64 KB |
| A_Dapper_Except                 | A_Except          | 188.61 μs | 0.7324 |      - |   14.3 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 227.05 μs | 0.9766 |      - |  16.72 KB |
| A_EFCore_Compiled_Except        | A_Except          | 478.12 μs | 4.8828 | 2.4414 |  83.75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_In           | A_InList          | 131.80 μs | 0.7324 |      - |  15.47 KB |
| A_Nextorm_Prepared_ListContains | A_InList          | 133.17 μs | 0.7324 |      - |  15.47 KB |
| A_Dapper_In                     | A_InList          | 192.42 μs | 0.7324 |      - |  12.58 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 475.55 μs | 8.3008 |      - | 136.33 KB |
| A_EFCore_Compiled_In            | A_InList          | 684.38 μs | 7.8125 | 3.9063 | 143.37 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 107.44 μs | 0.3662 |      - |   6.49 KB |
| A_Dapper_Intersect              | A_Intersect       | 179.12 μs | 0.7324 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 220.25 μs | 0.9766 |      - |  16.25 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 453.63 μs | 4.3945 | 1.9531 |     75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 143.17 μs | 0.4883 |      - |  10.31 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 271.03 μs | 0.9766 |      - |  20.63 KB |
| A_Dapper_Join4                  | A_Join4           | 289.69 μs | 0.9766 |      - |  20.16 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 519.89 μs | 4.8828 | 1.9531 |  80.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 136.65 μs | 0.4883 |      - |  11.64 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 218.44 μs | 1.2207 |      - |  22.11 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 225.50 μs | 1.2207 |      - |  23.13 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 473.06 μs | 5.3711 | 2.4414 |  94.85 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 130.50 μs | 0.2441 |      - |   6.56 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 265.64 μs | 0.4883 |      - |  15.24 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 289.45 μs | 0.9766 |      - |  17.74 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  94.93 μs | 0.3662 |      - |   6.41 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  95.61 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 151.22 μs | 0.7324 |      - |  12.74 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 162.38 μs | 0.9766 |      - |  16.33 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 167.58 μs | 0.9766 |      - |  16.09 KB |
| A_Dapper_Contains               | A_StringFunctions | 204.10 μs | 0.9766 |      - |  17.11 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 381.05 μs | 3.9063 | 1.9531 |  70.55 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 392.06 μs | 3.9063 | 1.9531 |  70.31 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  96.83 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_Udf                    | A_Udf             | 149.55 μs | 0.7324 |      - |  12.74 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 160.31 μs | 0.9766 |      - |  16.33 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 380.15 μs | 3.9063 | 1.9531 |  70.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_SumOver      | A_Window          | 155.16 μs | 0.2441 |      - |   7.74 KB |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 156.63 μs | 0.2441 |      - |   7.89 KB |
| A_Dapper_SumOver                | A_Window          | 334.23 μs | 0.4883 |      - |  15.39 KB |
| A_Dapper_RowNumber              | A_Window          | 337.09 μs | 0.4883 |      - |  15.31 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 352.98 μs | 0.9766 |      - |  17.35 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 362.25 μs | 0.9766 |      - |  17.27 KB |
