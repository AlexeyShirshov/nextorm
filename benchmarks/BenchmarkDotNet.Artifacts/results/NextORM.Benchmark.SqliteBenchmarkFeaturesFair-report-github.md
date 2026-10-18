```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Toolchain              | Categories        | Mean      | Gen0    | Gen1   | Allocated |
|-------------------------------- |----------------------- |------------------ |----------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | Default                | A_CaseWhen        | 109.00 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | Default                | A_CaseWhen        | 144.47 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | Default                | A_CaseWhen        | 151.03 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | Default                | A_CaseWhen        | 384.74 μs |  8.7891 | 3.9063 |  78.98 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Cte          | Default                | A_Cte             |  99.98 μs |  0.8545 |      - |   7.73 KB |
| A_Dapper_Cte                    | Default                | A_Cte             | 212.86 μs |  1.7090 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | Default                | A_Cte             | 231.29 μs |  1.9531 |      - |  17.73 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | Default                | A_Distinct        | 115.04 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Distinct               | Default                | A_Distinct        | 181.59 μs |  1.7090 |      - |  15.55 KB |
| A_Linq2Db_Compiled_Distinct     | Default                | A_Distinct        | 182.21 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_Distinct      | Default                | A_Distinct        | 411.32 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Except       | Default                | A_Except          | 123.75 μs |  0.7324 |      - |   7.42 KB |
| A_Dapper_Except                 | Default                | A_Except          | 198.19 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | Default                | A_Except          | 209.42 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | Default                | A_Except          | 442.50 μs | 10.7422 | 5.3711 |  88.13 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_ListContains | Default                | A_InList          | 127.50 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_In           | Default                | A_InList          | 135.10 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | Default                | A_InList          | 181.45 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | Default                | A_InList          | 414.41 μs | 18.0664 |      - | 151.09 KB |
| A_EFCore_Compiled_In            | Default                | A_InList          | 584.16 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | Default                | A_Intersect       | 106.48 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Intersect              | Default                | A_Intersect       | 166.53 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | Default                | A_Intersect       | 198.90 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | Default                | A_Intersect       | 411.31 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Join4        | Default                | A_Join4           | 134.79 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | Default                | A_Join4           | 243.71 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | Default                | A_Join4           | 252.56 μs |  2.4414 |      - |  20.23 KB |
| A_EFCore_Compiled_Join4         | Default                | A_Join4           | 467.43 μs | 10.2539 | 4.8828 |  84.92 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | Default                | A_LeftJoin        | 126.65 μs |  1.4648 |      - |  12.03 KB |
| A_Dapper_LeftJoin               | Default                | A_LeftJoin        | 203.26 μs |  2.6855 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | Default                | A_LeftJoin        | 219.53 μs |  2.6855 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | Default                | A_LeftJoin        | 430.05 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | Default                | A_RecursiveCte    | 135.07 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | Default                | A_RecursiveCte    | 277.61 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | Default                | A_RecursiveCte    | 288.61 μs |  1.9531 |      - |  17.81 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_ToUpper      | Default                | A_StringFunctions | 104.26 μs |  0.8545 |      - |    7.5 KB |
| A_Nextorm_Prepared_Contains     | Default                | A_StringFunctions | 105.05 μs |  0.8545 |      - |   7.19 KB |
| A_Dapper_ToUpper                | Default                | A_StringFunctions | 139.10 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | Default                | A_StringFunctions | 173.18 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | Default                | A_StringFunctions | 175.43 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | Default                | A_StringFunctions | 207.50 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | Default                | A_StringFunctions | 370.97 μs |  8.7891 | 4.3945 |  74.92 KB |
| A_EFCore_Compiled_Contains      | Default                | A_StringFunctions | 404.25 μs |  8.7891 | 4.3945 |  74.69 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Udf          | Default                | A_Udf             |  95.74 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_Udf                    | Default                | A_Udf             | 145.21 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | Default                | A_Udf             | 152.75 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | Default                | A_Udf             | 344.31 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_RowNumber    | Default                | A_Window          | 147.83 μs |  0.9766 |      - |   8.28 KB |
| A_Nextorm_Prepared_SumOver      | Default                | A_Window          | 151.94 μs |  0.9766 |      - |   8.52 KB |
| A_Dapper_RowNumber              | Default                | A_Window          | 296.46 μs |  1.4648 |      - |  15.39 KB |
| A_Dapper_SumOver                | Default                | A_Window          | 299.50 μs |  1.4648 |      - |  15.47 KB |
| A_Linq2Db_Compiled_SumOver      | Default                | A_Window          | 317.83 μs |  1.9531 |      - |  17.42 KB |
| A_Linq2Db_Compiled_RowNumber    | Default                | A_Window          | 328.76 μs |  1.9531 |      - |  17.34 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_CaseWhen     | InProcessEmitToolchain | A_CaseWhen        |  94.97 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | InProcessEmitToolchain | A_CaseWhen        | 147.70 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | InProcessEmitToolchain | A_CaseWhen        | 158.06 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | InProcessEmitToolchain | A_CaseWhen        | 354.02 μs |  9.2773 | 4.3945 |  78.99 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Cte          | InProcessEmitToolchain | A_Cte             |  98.99 μs |  0.8545 |      - |   7.74 KB |
| A_Dapper_Cte                    | InProcessEmitToolchain | A_Cte             | 222.36 μs |  1.7090 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | InProcessEmitToolchain | A_Cte             | 226.29 μs |  1.9531 |      - |  17.74 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | InProcessEmitToolchain | A_Distinct        | 117.41 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Distinct               | InProcessEmitToolchain | A_Distinct        | 194.07 μs |  1.7090 |      - |  15.55 KB |
| A_Linq2Db_Compiled_Distinct     | InProcessEmitToolchain | A_Distinct        | 200.69 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_Distinct      | InProcessEmitToolchain | A_Distinct        | 507.14 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Except       | InProcessEmitToolchain | A_Except          | 113.54 μs |  0.8545 |      - |   7.42 KB |
| A_Dapper_Except                 | InProcessEmitToolchain | A_Except          | 179.30 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | InProcessEmitToolchain | A_Except          | 288.08 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | InProcessEmitToolchain | A_Except          | 589.94 μs | 10.7422 | 5.3711 |  88.13 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_In           | InProcessEmitToolchain | A_InList          | 125.74 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_ListContains | InProcessEmitToolchain | A_InList          | 127.47 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | InProcessEmitToolchain | A_InList          | 192.44 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | InProcessEmitToolchain | A_InList          | 516.27 μs | 18.5547 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | InProcessEmitToolchain | A_InList          | 636.03 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | InProcessEmitToolchain | A_Intersect       | 104.85 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Intersect              | InProcessEmitToolchain | A_Intersect       | 170.41 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | InProcessEmitToolchain | A_Intersect       | 201.96 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | InProcessEmitToolchain | A_Intersect       | 429.33 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Join4        | InProcessEmitToolchain | A_Join4           | 132.73 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | InProcessEmitToolchain | A_Join4           | 248.56 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | InProcessEmitToolchain | A_Join4           | 264.78 μs |  2.4414 |      - |  20.24 KB |
| A_EFCore_Compiled_Join4         | InProcessEmitToolchain | A_Join4           | 489.34 μs |  9.7656 | 4.8828 |  84.93 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | InProcessEmitToolchain | A_LeftJoin        | 127.89 μs |  1.4648 |      - |  12.03 KB |
| A_Linq2Db_Compiled_LeftJoin     | InProcessEmitToolchain | A_LeftJoin        | 219.62 μs |  2.6855 |      - |   23.2 KB |
| A_Dapper_LeftJoin               | InProcessEmitToolchain | A_LeftJoin        | 229.43 μs |  2.6855 |      - |  22.19 KB |
| A_EFCore_Compiled_LeftJoin      | InProcessEmitToolchain | A_LeftJoin        | 442.22 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | InProcessEmitToolchain | A_RecursiveCte    | 152.82 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | InProcessEmitToolchain | A_RecursiveCte    | 285.84 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | InProcessEmitToolchain | A_RecursiveCte    | 339.45 μs |  1.9531 |      - |  17.81 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Contains     | InProcessEmitToolchain | A_StringFunctions |  97.86 μs |  0.8545 |      - |   7.19 KB |
| A_Nextorm_Prepared_ToUpper      | InProcessEmitToolchain | A_StringFunctions | 101.59 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_ToUpper                | InProcessEmitToolchain | A_StringFunctions | 146.15 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | InProcessEmitToolchain | A_StringFunctions | 160.29 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | InProcessEmitToolchain | A_StringFunctions | 167.98 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | InProcessEmitToolchain | A_StringFunctions | 200.56 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_Contains      | InProcessEmitToolchain | A_StringFunctions | 375.54 μs |  8.7891 | 4.3945 |  74.69 KB |
| A_EFCore_Compiled_ToUpper       | InProcessEmitToolchain | A_StringFunctions | 412.97 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_Udf          | InProcessEmitToolchain | A_Udf             |  94.48 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_Udf                    | InProcessEmitToolchain | A_Udf             | 142.45 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | InProcessEmitToolchain | A_Udf             | 161.33 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | InProcessEmitToolchain | A_Udf             | 359.06 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                        |                   |           |         |        |           |
| A_Nextorm_Prepared_RowNumber    | InProcessEmitToolchain | A_Window          | 148.80 μs |  0.9766 |      - |   8.28 KB |
| A_Nextorm_Prepared_SumOver      | InProcessEmitToolchain | A_Window          | 153.43 μs |  0.9766 |      - |   8.52 KB |
| A_Dapper_SumOver                | InProcessEmitToolchain | A_Window          | 311.81 μs |  1.4648 |      - |  15.47 KB |
| A_Dapper_RowNumber              | InProcessEmitToolchain | A_Window          | 317.99 μs |  1.4648 |      - |  15.39 KB |
| A_Linq2Db_Compiled_SumOver      | InProcessEmitToolchain | A_Window          | 330.14 μs |  1.9531 |      - |  17.42 KB |
| A_Linq2Db_Compiled_RowNumber    | InProcessEmitToolchain | A_Window          | 335.75 μs |  1.9531 |      - |  17.35 KB |
