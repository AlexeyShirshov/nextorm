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
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  95.81 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 143.19 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 150.01 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 354.14 μs |  9.2773 | 4.3945 |  78.99 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             |  97.68 μs |  0.8545 |      - |   7.74 KB |
| A_Dapper_Cte                    | A_Cte             | 217.86 μs |  1.7090 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 227.30 μs |  1.9531 |      - |  17.74 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 110.98 μs |  0.8545 |      - |   7.27 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 175.91 μs |  1.7090 |      - |  15.94 KB |
| A_Dapper_Distinct               | A_Distinct        | 182.78 μs |  1.7090 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 408.91 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 109.36 μs |  0.8545 |      - |   7.42 KB |
| A_Dapper_Except                 | A_Except          | 178.02 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 209.68 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | A_Except          | 434.20 μs | 10.7422 | 5.3711 |  88.13 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_ListContains | A_InList          | 123.64 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_In           | A_InList          | 124.04 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | A_InList          | 185.24 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 443.35 μs | 18.5547 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | A_InList          | 630.21 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 104.25 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Intersect              | A_Intersect       | 163.37 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 197.65 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 415.34 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 127.18 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 240.19 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | A_Join4           | 255.57 μs |  2.4414 |      - |  20.24 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 473.94 μs | 10.2539 | 4.8828 |  84.92 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 123.21 μs |  1.4648 |      - |  12.03 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 201.89 μs |  2.6855 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 209.97 μs |  2.6855 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 423.17 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 122.67 μs |  0.8545 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 245.77 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 261.24 μs |  1.9531 |      - |  17.81 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  92.55 μs |  0.8545 |      - |    7.5 KB |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  94.13 μs |  0.8545 |      - |   7.19 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 142.73 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 151.43 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 164.00 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | A_StringFunctions | 193.61 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 375.06 μs |  8.7891 | 4.3945 |  74.92 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 377.27 μs |  8.7891 | 4.3945 |  74.69 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  93.31 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_Udf                    | A_Udf             | 140.89 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 156.02 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 394.36 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 143.64 μs |  0.9766 |      - |   8.28 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 144.29 μs |  0.9766 |      - |   8.52 KB |
| A_Dapper_SumOver                | A_Window          | 300.39 μs |  1.4648 |      - |  15.47 KB |
| A_Dapper_RowNumber              | A_Window          | 309.38 μs |  1.4648 |      - |  15.39 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 320.39 μs |  1.9531 |      - |  17.42 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 322.27 μs |  1.9531 |      - |  17.35 KB |
