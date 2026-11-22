```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.31GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  41.64 μs | 0.0610 |      - |   6.48 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        |  70.02 μs | 0.1221 |      - |  13.36 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        |  71.39 μs | 0.1221 |      - |  15.86 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 210.85 μs | 0.7324 | 0.4883 |  74.61 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             |  46.53 μs | 0.0610 |      - |   7.34 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 112.09 μs | 0.1221 |      - |  17.66 KB |
| A_Dapper_Cte                    | A_Cte             | 112.67 μs | 0.1221 |      - |  15.63 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        |  53.27 μs | 0.0610 |      - |   6.48 KB |
| A_Dapper_Distinct               | A_Distinct        |  81.54 μs | 0.1221 |      - |  14.45 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        |  81.73 μs | 0.1221 |      - |  15.86 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 229.75 μs | 0.7324 | 0.4883 |  72.27 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Except       | A_Except          |  57.01 μs | 0.0610 |      - |   6.64 KB |
| A_Dapper_Except                 | A_Except          |  96.84 μs | 0.1221 |      - |   14.3 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 110.19 μs | 0.1221 |      - |  16.72 KB |
| A_EFCore_Compiled_Except        | A_Except          | 261.10 μs | 0.9766 | 0.4883 |  83.75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_ListContains | A_InList          |  64.58 μs | 0.1221 |      - |  15.47 KB |
| A_Nextorm_Prepared_In           | A_InList          |  64.80 μs | 0.1221 |      - |  15.47 KB |
| A_Dapper_In                     | A_InList          |  88.08 μs | 0.1221 |      - |  12.58 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 161.73 μs | 1.4648 |      - | 136.33 KB |
| A_EFCore_Compiled_In            | A_InList          | 293.44 μs | 1.4648 | 0.4883 | 143.36 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       |  48.09 μs | 0.0610 |      - |   6.48 KB |
| A_Dapper_Intersect              | A_Intersect       |  85.10 μs | 0.1221 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       |  98.18 μs | 0.1221 |      - |  16.25 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 241.24 μs | 0.7324 | 0.2441 |     75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           |  79.12 μs | 0.1221 |      - |  10.31 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 139.91 μs | 0.2441 |      - |  20.63 KB |
| A_Dapper_Join4                  | A_Join4           | 168.33 μs | 0.2441 |      - |  20.16 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 301.18 μs | 0.9766 | 0.4883 |  80.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        |  75.74 μs | 0.1221 |      - |  11.64 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 122.30 μs | 0.2441 |      - |  22.11 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 122.43 μs | 0.2441 |      - |  23.13 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 266.26 μs | 0.9766 | 0.4883 |  94.85 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    |  64.83 μs |      - |      - |   6.56 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 127.16 μs |      - |      - |  15.24 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 133.52 μs |      - |      - |  17.74 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  38.09 μs | 0.0610 |      - |   6.41 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  38.92 μs | 0.0610 |      - |   6.72 KB |
| A_Dapper_ToUpper                | A_StringFunctions |  65.17 μs | 0.1221 |      - |  12.74 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions |  67.84 μs | 0.1221 |      - |  16.33 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions |  73.02 μs | 0.1221 |      - |  16.09 KB |
| A_Dapper_Contains               | A_StringFunctions |  93.38 μs | 0.1221 |      - |  17.11 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 204.46 μs | 0.7324 | 0.2441 |  70.55 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 210.65 μs | 0.7324 | 0.2441 |  70.31 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  38.90 μs | 0.0610 |      - |   6.72 KB |
| A_Dapper_Udf                    | A_Udf             |  64.55 μs | 0.1221 |      - |  12.74 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             |  67.90 μs | 0.1221 |      - |  16.33 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 206.15 μs | 0.7324 | 0.2441 |  70.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_SumOver      | A_Window          |  80.93 μs |      - |      - |   7.74 KB |
| A_Nextorm_Prepared_RowNumber    | A_Window          |  83.89 μs |      - |      - |   7.89 KB |
| A_Dapper_SumOver                | A_Window          | 146.01 μs |      - |      - |  15.39 KB |
| A_Dapper_RowNumber              | A_Window          | 147.80 μs |      - |      - |  15.31 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 150.95 μs |      - |      - |  17.34 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 151.84 μs |      - |      - |  17.27 KB |
