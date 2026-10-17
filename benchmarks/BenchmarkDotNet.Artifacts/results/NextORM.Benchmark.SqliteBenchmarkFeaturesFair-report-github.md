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
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  92.56 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 145.99 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 150.78 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 356.08 μs |  9.2773 | 4.3945 |  78.99 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             |  94.92 μs |  0.8545 |      - |   7.74 KB |
| A_Dapper_Cte                    | A_Cte             | 214.31 μs |  1.7090 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 215.21 μs |  1.9531 |      - |  17.74 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 110.14 μs |  0.8545 |      - |   7.27 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 174.13 μs |  1.7090 |      - |  15.94 KB |
| A_Dapper_Distinct               | A_Distinct        | 185.47 μs |  1.7090 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 406.01 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 110.69 μs |  0.8545 |      - |   7.42 KB |
| A_Dapper_Except                 | A_Except          | 177.18 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 207.97 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | A_Except          | 434.70 μs | 10.7422 | 5.3711 |  88.13 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_ListContains | A_InList          | 123.25 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_In           | A_InList          | 124.24 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | A_InList          | 186.52 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 435.32 μs | 18.5547 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | A_InList          | 606.98 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 103.61 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Intersect              | A_Intersect       | 167.73 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 196.58 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 417.51 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 126.27 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 232.67 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | A_Join4           | 255.22 μs |  2.4414 |      - |  20.24 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 468.19 μs | 10.2539 | 4.8828 |  84.92 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 120.89 μs |  1.4648 |      - |  12.03 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 198.44 μs |  2.6855 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 202.90 μs |  2.6855 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 416.43 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 126.38 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 242.36 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 257.26 μs |  1.9531 |      - |  17.81 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  89.62 μs |  0.8545 |      - |   7.19 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  92.62 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 139.21 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 151.82 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 160.28 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | A_StringFunctions | 195.50 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 350.82 μs |  8.7891 | 4.3945 |  74.92 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 366.51 μs |  8.7891 | 4.3945 |  74.69 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  92.66 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_Udf                    | A_Udf             | 140.82 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 150.54 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 348.23 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 144.44 μs |  0.9766 |      - |   8.28 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 148.17 μs |  0.9766 |      - |   8.52 KB |
| A_Dapper_RowNumber              | A_Window          | 300.39 μs |  1.4648 |      - |  15.39 KB |
| A_Dapper_SumOver                | A_Window          | 301.80 μs |  1.4648 |      - |  15.47 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 318.43 μs |  1.9531 |      - |  17.42 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 322.25 μs |  1.9531 |      - |  17.35 KB |
