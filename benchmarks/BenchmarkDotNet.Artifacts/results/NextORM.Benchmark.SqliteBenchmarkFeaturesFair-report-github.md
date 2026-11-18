```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.75GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  97.54 μs | 0.3662 |      - |   6.48 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 147.91 μs | 0.7324 |      - |  13.36 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 155.14 μs | 0.7324 |      - |  15.86 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 357.60 μs | 4.3945 | 1.9531 |  74.61 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             | 103.02 μs | 0.3662 |      - |   7.34 KB |
| A_Dapper_Cte                    | A_Cte             | 226.83 μs | 0.7324 |      - |  15.63 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 236.24 μs | 0.9766 |      - |  17.66 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 117.20 μs | 0.3662 |      - |   6.49 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 185.14 μs | 0.7324 |      - |  15.86 KB |
| A_Dapper_Distinct               | A_Distinct        | 186.26 μs | 0.7324 |      - |  14.45 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 421.69 μs | 4.3945 | 1.9531 |  72.27 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 116.80 μs | 0.3662 |      - |   6.64 KB |
| A_Dapper_Except                 | A_Except          | 184.77 μs | 0.7324 |      - |   14.3 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 222.92 μs | 0.9766 |      - |  16.72 KB |
| A_EFCore_Compiled_Except        | A_Except          | 446.25 μs | 4.8828 | 2.4414 |  83.75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_ListContains | A_InList          | 129.89 μs | 0.7324 |      - |  15.47 KB |
| A_Nextorm_Prepared_In           | A_InList          | 134.08 μs | 0.7324 |      - |  15.47 KB |
| A_Dapper_In                     | A_InList          | 193.50 μs | 0.7324 |      - |  12.58 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 463.30 μs | 8.3008 |      - | 136.33 KB |
| A_EFCore_Compiled_In            | A_InList          | 659.04 μs | 7.8125 | 3.9063 | 143.37 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 109.19 μs | 0.3662 |      - |   6.49 KB |
| A_Dapper_Intersect              | A_Intersect       | 174.44 μs | 0.7324 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 207.99 μs | 0.9766 |      - |  16.25 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 430.23 μs | 4.3945 | 1.9531 |     75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 139.23 μs | 0.4883 |      - |  10.31 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 254.47 μs | 0.9766 |      - |  20.63 KB |
| A_Dapper_Join4                  | A_Join4           | 282.95 μs | 0.9766 |      - |  20.16 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 501.16 μs | 4.8828 | 1.9531 |  80.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 134.45 μs | 0.4883 |      - |  11.64 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 216.34 μs | 1.2207 |      - |  22.11 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 221.54 μs | 1.2207 |      - |  23.13 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 439.16 μs | 5.3711 | 2.4414 |  94.85 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 131.49 μs | 0.2441 |      - |   6.56 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 264.76 μs | 0.4883 |      - |  15.24 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 281.37 μs | 0.9766 |      - |  17.74 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  95.72 μs | 0.3662 |      - |   6.41 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  96.15 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 148.25 μs | 0.7324 |      - |  12.74 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 154.19 μs | 0.9766 |      - |  16.33 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 167.78 μs | 0.9766 |      - |  16.09 KB |
| A_Dapper_Contains               | A_StringFunctions | 204.27 μs | 0.9766 |      - |  17.11 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 357.11 μs | 3.9063 | 1.9531 |  70.55 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 380.36 μs | 3.9063 | 1.9531 |  70.31 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  95.34 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_Udf                    | A_Udf             | 146.84 μs | 0.7324 |      - |  12.74 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 155.98 μs | 0.9766 |      - |  16.33 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 360.36 μs | 3.9063 | 1.9531 |  70.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 155.16 μs | 0.2441 |      - |   7.89 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 158.17 μs | 0.2441 |      - |   7.74 KB |
| A_Dapper_SumOver                | A_Window          | 331.56 μs | 0.4883 |      - |  15.39 KB |
| A_Dapper_RowNumber              | A_Window          | 331.61 μs | 0.4883 |      - |  15.31 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 350.61 μs | 0.9766 |      - |  17.35 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 355.54 μs | 0.9766 |      - |  17.27 KB |
