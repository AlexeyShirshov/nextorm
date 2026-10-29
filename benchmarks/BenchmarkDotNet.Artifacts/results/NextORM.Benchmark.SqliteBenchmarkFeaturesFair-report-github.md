```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean     | Gen0    | Gen1   | Allocated |
|-------------------------------- |------------------ |---------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        | 154.0 μs |  0.7324 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 223.4 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 235.2 μs |  0.9766 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 654.8 μs |  7.8125 | 3.9063 |  78.99 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             | 153.7 μs |  0.7324 |      - |   7.74 KB |
| A_Dapper_Cte                    | A_Cte             | 325.7 μs |  1.4648 |      - |  15.71 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 350.8 μs |  1.9531 |      - |  17.74 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 172.5 μs |  0.7324 |      - |   7.27 KB |
| A_Dapper_Distinct               | A_Distinct        | 278.4 μs |  1.4648 |      - |  15.55 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 292.4 μs |  1.4648 |      - |  15.94 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 585.7 μs |  8.7891 | 3.9063 |  76.65 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 156.2 μs |  0.7324 |      - |   7.42 KB |
| A_Dapper_Except                 | A_Except          | 262.0 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 287.1 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | A_Except          | 662.2 μs | 10.7422 | 4.8828 |  88.13 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_ListContains | A_InList          | 197.8 μs |  1.9531 |      - |  16.33 KB |
| A_Nextorm_Prepared_In           | A_InList          | 199.0 μs |  1.9531 |      - |  16.33 KB |
| A_Dapper_In                     | A_InList          | 297.9 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 669.7 μs | 18.5547 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | A_InList          | 859.6 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 164.8 μs |  0.7324 |      - |   7.27 KB |
| A_Dapper_Intersect              | A_Intersect       | 272.4 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 333.4 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 640.9 μs |  8.7891 | 3.9063 |  79.38 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 211.4 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 388.2 μs |  2.4414 |      - |  20.71 KB |
| A_Dapper_Join4                  | A_Join4           | 395.6 μs |  2.4414 |      - |  20.24 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 743.8 μs |  9.7656 | 4.8828 |  84.93 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 186.5 μs |  1.4648 |      - |  12.03 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 320.7 μs |  2.4414 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 331.9 μs |  2.4414 |      - |  23.21 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 665.7 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 186.2 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 381.8 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 407.0 μs |  1.9531 |      - |  17.81 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions | 142.2 μs |  0.7324 |      - |   7.19 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions | 142.6 μs |  0.7324 |      - |    7.5 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 217.3 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 239.0 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 255.7 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | A_StringFunctions | 316.5 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 555.4 μs |  8.7891 | 3.9063 |  74.93 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 572.2 μs |  8.7891 | 3.9063 |  74.69 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             | 139.4 μs |  0.7324 |      - |    7.5 KB |
| A_Dapper_Udf                    | A_Udf             | 223.2 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 244.6 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 536.7 μs |  8.7891 | 3.9063 |  74.93 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_SumOver      | A_Window          | 208.2 μs |  0.9766 |      - |   8.52 KB |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 231.5 μs |  0.9766 |      - |   8.28 KB |
| A_Dapper_SumOver                | A_Window          | 436.8 μs |  1.4648 |      - |  15.47 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 439.4 μs |  1.9531 |      - |  17.42 KB |
| A_Dapper_RowNumber              | A_Window          | 450.6 μs |  1.4648 |      - |  15.39 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 473.3 μs |  1.9531 |      - |  17.35 KB |
