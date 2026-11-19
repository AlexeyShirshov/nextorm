```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.16GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  74.77 μs | 0.3662 |      - |   6.48 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        |  94.92 μs | 0.7324 |      - |  13.36 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        |  97.57 μs | 0.8545 |      - |  15.86 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 197.12 μs | 4.3945 | 2.1973 |  74.61 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             |  77.85 μs | 0.3662 |      - |   7.34 KB |
| A_Dapper_Cte                    | A_Cte             | 123.30 μs | 0.7324 |      - |  15.63 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 123.90 μs | 0.9766 |      - |  17.66 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        |  83.21 μs | 0.3662 |      - |   6.48 KB |
| A_Dapper_Distinct               | A_Distinct        | 102.45 μs | 0.8545 |      - |  14.45 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 105.82 μs | 0.8545 |      - |  15.86 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 207.12 μs | 4.3945 | 2.1973 |  72.27 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Except       | A_Except          |  88.01 μs | 0.3662 |      - |   6.64 KB |
| A_Dapper_Except                 | A_Except          | 117.12 μs | 0.8545 |      - |   14.3 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 126.47 μs | 0.9766 |      - |  16.72 KB |
| A_EFCore_Compiled_Except        | A_Except          | 234.53 μs | 4.8828 | 2.4414 |  83.75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_In           | A_InList          |  90.91 μs | 0.8545 |      - |  15.47 KB |
| A_Nextorm_Prepared_ListContains | A_InList          |  91.11 μs | 0.8545 |      - |  15.47 KB |
| A_Dapper_In                     | A_InList          | 107.66 μs | 0.7324 |      - |  12.58 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 161.24 μs | 8.3008 |      - | 136.33 KB |
| A_EFCore_Compiled_In            | A_InList          | 241.17 μs | 8.5449 | 4.1504 | 143.36 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       |  79.42 μs | 0.3662 |      - |   6.49 KB |
| A_Dapper_Intersect              | A_Intersect       | 104.46 μs | 0.7324 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 117.05 μs | 0.9766 |      - |  16.25 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 210.81 μs | 4.3945 | 2.1973 |     75 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 100.10 μs | 0.6104 |      - |  10.31 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 146.37 μs | 1.2207 |      - |  20.63 KB |
| A_Dapper_Join4                  | A_Join4           | 160.34 μs | 1.2207 |      - |  20.16 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 250.57 μs | 4.8828 | 2.4414 |  80.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        |  99.76 μs | 0.6104 |      - |  11.64 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 130.22 μs | 1.2207 |      - |  23.13 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 130.25 μs | 1.2207 |      - |  22.11 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 221.61 μs | 5.6152 | 2.6855 |  94.84 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    |  90.77 μs | 0.3662 |      - |   6.56 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 134.40 μs | 0.7324 |      - |  15.24 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 139.85 μs | 0.9766 |      - |  17.74 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  71.27 μs | 0.3662 |      - |   6.41 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  74.37 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_ToUpper                | A_StringFunctions |  92.31 μs | 0.7324 |      - |  12.74 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions |  92.69 μs | 0.9766 |      - |  16.33 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions |  98.25 μs | 0.9766 |      - |  16.09 KB |
| A_Dapper_Contains               | A_StringFunctions | 114.64 μs | 0.9766 |      - |  17.11 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 182.28 μs | 4.1504 | 1.9531 |  70.55 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 185.99 μs | 4.1504 | 1.9531 |  70.31 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  72.17 μs | 0.3662 |      - |   6.72 KB |
| A_Dapper_Udf                    | A_Udf             |  93.36 μs | 0.7324 |      - |  12.74 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             |  95.48 μs | 0.9766 |      - |  16.33 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 185.73 μs | 4.1504 | 1.9531 |  70.55 KB |
|                                 |                   |           |        |        |           |
| A_Nextorm_Prepared_SumOver      | A_Window          | 102.30 μs | 0.3662 |      - |   7.74 KB |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 103.55 μs | 0.3662 |      - |   7.89 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 145.96 μs | 0.9766 |      - |  17.27 KB |
| A_Dapper_SumOver                | A_Window          | 146.54 μs | 0.7324 |      - |  15.39 KB |
| A_Dapper_RowNumber              | A_Window          | 146.82 μs | 0.7324 |      - |  15.31 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 149.94 μs | 0.9766 |      - |  17.34 KB |
