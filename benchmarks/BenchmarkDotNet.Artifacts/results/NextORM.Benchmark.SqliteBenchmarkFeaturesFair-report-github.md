```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0    | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        | 100.37 μs |  0.7324 |      - |   6.56 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 148.58 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 161.87 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 352.96 μs |  9.2773 | 4.3945 |  78.99 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             | 101.21 μs |  0.8545 |      - |   7.03 KB |
| A_Dapper_Cte                    | A_Cte             | 217.79 μs |  1.7090 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 225.03 μs |  1.9531 |      - |  17.74 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 117.11 μs |  0.7324 |      - |   6.56 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 182.88 μs |  1.7090 |      - |  15.94 KB |
| A_Dapper_Distinct               | A_Distinct        | 194.33 μs |  1.7090 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 422.33 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 115.20 μs |  0.7324 |      - |   6.72 KB |
| A_Dapper_Except                 | A_Except          | 187.03 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 216.29 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | A_Except          | 453.43 μs | 10.7422 | 5.3711 |  88.13 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_In           | A_InList          | 129.58 μs |  1.7090 |      - |  15.63 KB |
| A_Nextorm_Prepared_ListContains | A_InList          | 145.63 μs |  1.7090 |      - |  15.63 KB |
| A_Dapper_In                     | A_InList          | 198.05 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 503.85 μs | 18.5547 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | A_InList          | 639.51 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 107.97 μs |  0.7324 |      - |   6.56 KB |
| A_Dapper_Intersect              | A_Intersect       | 172.65 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 211.92 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 438.80 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 135.04 μs |  0.9766 |      - |   9.85 KB |
| A_Dapper_Join4                  | A_Join4           | 287.53 μs |  2.4414 |      - |  20.24 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 335.78 μs |  2.4414 |      - |  20.71 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 538.28 μs |  9.7656 | 4.8828 |  84.93 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 136.27 μs |  1.2207 |      - |  11.33 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 219.10 μs |  2.6855 |      - |   23.2 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 229.60 μs |  2.6855 |      - |  22.19 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 463.83 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 126.45 μs |  0.7324 |      - |   6.64 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 263.38 μs |  1.4648 |      - |  15.32 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 277.07 μs |  1.9531 |      - |  17.82 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  98.72 μs |  0.7324 |      - |    6.8 KB |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  99.57 μs |  0.7324 |      - |   6.49 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 156.37 μs |  1.9531 |      - |  16.41 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 176.10 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 179.95 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | A_StringFunctions | 202.63 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 375.61 μs |  8.7891 | 4.3945 |  74.92 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 396.03 μs |  8.7891 | 4.3945 |  74.69 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             | 101.35 μs |  0.7324 |      - |    6.8 KB |
| A_Dapper_Udf                    | A_Udf             | 146.89 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 171.18 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 443.77 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 150.42 μs |  0.7324 |      - |   7.58 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 158.52 μs |  0.7324 |      - |   7.81 KB |
| A_Dapper_RowNumber              | A_Window          | 319.29 μs |  1.4648 |      - |  15.39 KB |
| A_Dapper_SumOver                | A_Window          | 330.15 μs |  1.4648 |      - |  15.47 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 343.97 μs |  1.9531 |      - |  17.42 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 375.04 μs |  1.9531 |      - |  17.35 KB |
