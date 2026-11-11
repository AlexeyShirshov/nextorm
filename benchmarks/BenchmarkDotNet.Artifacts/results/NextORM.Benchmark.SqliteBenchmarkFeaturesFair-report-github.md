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
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  94.48 μs | 0.3662 |      - |   6.48 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 146.42 μs | 0.7324 |      - |  13.36 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 154.28 μs | 0.7324 |      - |  15.86 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 369.07 μs | 4.3945 | 1.9531 |  74.61 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             |  98.88 μs | 0.3662 |      - |   6.95 KB |
| A_Dapper_Cte                    | A_Cte             | 222.46 μs | 0.7324 |      - |  15.63 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 237.52 μs | 0.9766 |      - |  17.66 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 114.42 μs | 0.3662 |      - |   6.49 KB |
| A_Dapper_Distinct               | A_Distinct        | 182.65 μs | 0.7324 |      - |  14.45 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 183.61 μs | 0.7324 |      - |  15.86 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 425.29 μs | 4.3945 | 1.9531 |  72.27 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 114.09 μs | 0.3662 |      - |   6.64 KB |
| A_Dapper_Except                 | A_Except          | 181.26 μs | 0.7324 |      - |   14.3 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 215.65 μs | 0.9766 |      - |  16.72 KB |
| A_EFCore_Compiled_Except        | A_Except          | 464.02 μs | 4.8828 | 2.4414 |  83.75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_ListContains | A_InList          | 127.94 μs | 0.7324 |      - |  15.47 KB |
| A_Nextorm_Prepared_In           | A_InList          | 128.82 μs | 0.7324 |      - |  15.47 KB |
| A_Dapper_In                     | A_InList          | 185.49 μs | 0.7324 |      - |  12.58 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 479.24 μs | 8.3008 |      - | 136.33 KB |
| A_EFCore_Compiled_In            | A_InList          | 662.43 μs | 7.8125 | 3.9063 | 143.37 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 105.93 μs | 0.3662 |      - |   6.49 KB |
| A_Dapper_Intersect              | A_Intersect       | 166.35 μs | 0.7324 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 203.31 μs | 0.9766 |      - |  16.25 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 440.71 μs | 4.3945 | 1.9531 |     75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 132.71 μs | 0.4883 |      - |   9.77 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 246.46 μs | 0.9766 |      - |  20.63 KB |
| A_Dapper_Join4                  | A_Join4           | 271.73 μs | 0.9766 |      - |  20.16 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 509.87 μs | 4.8828 | 1.9531 |  80.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 126.51 μs | 0.4883 |      - |  11.25 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 212.18 μs | 1.2207 |      - |  22.11 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 216.29 μs | 1.2207 |      - |  23.13 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 452.67 μs | 5.3711 | 2.4414 |  94.85 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 129.46 μs | 0.2441 |      - |   6.56 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 260.17 μs | 0.4883 |      - |  15.24 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 285.03 μs | 0.9766 |      - |  17.74 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  93.33 μs | 0.3662 |      - |   6.41 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  94.05 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 142.21 μs | 0.7324 |      - |  12.74 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 152.49 μs | 0.9766 |      - |  16.33 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 165.83 μs | 0.9766 |      - |  16.09 KB |
| A_Dapper_Contains               | A_StringFunctions | 197.43 μs | 0.9766 |      - |  17.11 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 368.88 μs | 3.9063 | 1.9531 |  70.55 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 393.53 μs | 3.9063 | 1.9531 |  70.31 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  94.26 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_Udf                    | A_Udf             | 142.82 μs | 0.7324 |      - |  12.74 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 153.11 μs | 0.9766 |      - |  16.33 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 362.40 μs | 3.9063 | 1.9531 |  70.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 149.91 μs | 0.2441 |      - |    7.5 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 150.24 μs | 0.2441 |      - |   7.74 KB |
| A_Dapper_RowNumber              | A_Window          | 318.40 μs | 0.4883 |      - |  15.31 KB |
| A_Dapper_SumOver                | A_Window          | 322.61 μs | 0.4883 |      - |  15.39 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 346.53 μs | 0.9766 |      - |  17.27 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 347.18 μs | 0.9766 |      - |  17.35 KB |
