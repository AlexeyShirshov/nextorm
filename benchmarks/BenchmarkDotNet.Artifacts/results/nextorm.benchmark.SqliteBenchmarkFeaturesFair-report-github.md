```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0    | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        | 105.08 μs |  0.7324 |      - |   6.56 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 153.58 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 163.74 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 400.79 μs |  9.2773 | 4.3945 |  78.99 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             | 166.39 μs |  0.8545 |      - |   7.03 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 233.06 μs |  1.9531 |      - |  17.74 KB |
| A_Dapper_Cte                    | A_Cte             | 254.54 μs |  1.7090 |      - |   15.7 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 129.11 μs |  0.7324 |      - |   6.56 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 193.58 μs |  1.7090 |      - |  15.94 KB |
| A_Dapper_Distinct               | A_Distinct        | 203.50 μs |  1.7090 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 441.23 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 115.87 μs |  0.7324 |      - |   6.72 KB |
| A_Dapper_Except                 | A_Except          | 207.30 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 241.04 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | A_Except          | 469.44 μs | 10.7422 | 5.3711 |  88.13 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_ListContains | A_InList          | 134.31 μs |  1.7090 |      - |     15 KB |
| A_Nextorm_Prepared_In           | A_InList          | 138.44 μs |  1.7090 |      - |     15 KB |
| A_Dapper_In                     | A_InList          | 240.10 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 559.55 μs | 18.5547 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | A_InList          | 744.18 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 114.58 μs |  0.7324 |      - |   6.56 KB |
| A_Dapper_Intersect              | A_Intersect       | 170.37 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 204.90 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 479.94 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 133.28 μs |  0.9766 |      - |   9.84 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 250.84 μs |  2.4414 |      - |  20.71 KB |
| A_Dapper_Join4                  | A_Join4           | 272.30 μs |  2.4414 |      - |  20.24 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 507.66 μs |  9.7656 | 4.8828 |  84.93 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 127.57 μs |  1.2207 |      - |  11.33 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 214.37 μs |  2.6855 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 218.72 μs |  2.6855 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 465.98 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 131.30 μs |  0.7324 |      - |   6.64 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 265.03 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 279.22 μs |  1.9531 |      - |  17.81 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  97.95 μs |  0.7324 |      - |    6.8 KB |
| A_Nextorm_Prepared_Contains     | A_StringFunctions | 116.60 μs |  0.7324 |      - |   6.49 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 161.47 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 176.94 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 179.18 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | A_StringFunctions | 215.23 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 400.27 μs |  8.7891 | 4.3945 |  74.92 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 429.44 μs |  8.7891 | 4.3945 |  74.69 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             | 100.50 μs |  0.7324 |      - |    6.8 KB |
| A_Dapper_Udf                    | A_Udf             | 163.01 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 171.03 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 406.13 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 160.91 μs |  0.7324 |      - |   7.58 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 163.28 μs |  0.7324 |      - |   7.81 KB |
| A_Dapper_SumOver                | A_Window          | 340.07 μs |  1.4648 |      - |  15.47 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 350.80 μs |  1.9531 |      - |  17.42 KB |
| A_Dapper_RowNumber              | A_Window          | 356.76 μs |  1.4648 |      - |  15.39 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 377.52 μs |  1.9531 |      - |  17.35 KB |
