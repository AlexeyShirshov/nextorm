```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Toolchain=InProcessEmitToolchain  

```
| Method                          | Categories        | Mean     | Gen0    | Gen1   | Allocated |
|-------------------------------- |------------------ |---------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        | 107.9 μs |  0.7324 |      - |   6.48 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 152.2 μs |  1.4648 |      - |  13.36 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 161.0 μs |  1.7090 |      - |  15.86 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 352.5 μs |  8.7891 | 3.9063 |  74.62 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             | 113.5 μs |  0.8545 |      - |   7.34 KB |
| A_Dapper_Cte                    | A_Cte             | 230.1 μs |  1.7090 |      - |  15.63 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 235.0 μs |  1.9531 |      - |  17.66 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 126.6 μs |  0.7324 |      - |   6.49 KB |
| A_Dapper_Distinct               | A_Distinct        | 188.1 μs |  1.7090 |      - |  14.45 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 188.5 μs |  1.7090 |      - |  15.86 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 404.3 μs |  8.7891 | 4.3945 |  72.27 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 125.6 μs |  0.7324 |      - |   6.64 KB |
| A_Dapper_Except                 | A_Except          | 185.2 μs |  1.7090 |      - |   14.3 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 215.0 μs |  1.9531 |      - |  16.72 KB |
| A_EFCore_Compiled_Except        | A_Except          | 425.2 μs |  9.7656 | 4.8828 |  83.75 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_In           | A_InList          | 141.1 μs |  1.7090 |      - |  15.47 KB |
| A_Nextorm_Prepared_ListContains | A_InList          | 143.0 μs |  1.7090 |      - |  15.47 KB |
| A_Dapper_In                     | A_InList          | 196.6 μs |  1.4648 |      - |  12.58 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 457.8 μs | 16.6016 |      - | 136.33 KB |
| A_EFCore_Compiled_In            | A_InList          | 629.1 μs | 16.6016 | 4.8828 | 143.37 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 121.3 μs |  0.7324 |      - |   6.49 KB |
| A_Dapper_Intersect              | A_Intersect       | 175.1 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 207.9 μs |  1.9531 |      - |  16.25 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 423.2 μs |  8.7891 | 4.3945 |     75 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 145.8 μs |  1.2207 |      - |  10.31 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 249.6 μs |  2.4414 |      - |  20.63 KB |
| A_Dapper_Join4                  | A_Join4           | 270.4 μs |  2.4414 |      - |  20.16 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 486.1 μs |  9.7656 | 4.8828 |  80.55 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 141.5 μs |  1.2207 |      - |  11.64 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 211.7 μs |  2.6855 |      - |  22.11 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 214.6 μs |  2.6855 |      - |  23.13 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 419.2 μs | 11.2305 | 5.3711 |  94.85 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 140.6 μs |  0.7324 |      - |   6.56 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 256.5 μs |  1.4648 |      - |  15.24 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 269.2 μs |  1.9531 |      - |  17.74 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions | 108.1 μs |  0.7324 |      - |   6.41 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions | 108.8 μs |  0.7324 |      - |   6.72 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 153.0 μs |  1.4648 |      - |  12.74 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 158.5 μs |  1.9531 |      - |  16.33 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 167.1 μs |  1.9531 |      - |  16.09 KB |
| A_Dapper_Contains               | A_StringFunctions | 193.5 μs |  1.9531 |      - |  17.11 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 348.6 μs |  8.3008 | 3.9063 |  70.55 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 366.3 μs |  8.3008 | 3.9063 |  70.32 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             | 108.4 μs |  0.7324 |      - |   6.72 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 155.3 μs |  1.9531 |      - |  16.33 KB |
| A_Dapper_Udf                    | A_Udf             | 157.8 μs |  1.4648 |      - |  12.74 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 344.5 μs |  8.3008 | 3.9063 |  70.55 KB |
|                                 |                   |          |         |        |           |
| A_Nextorm_Prepared_SumOver      | A_Window          | 157.5 μs |  0.7324 |      - |   7.74 KB |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 158.5 μs |  0.7324 |      - |   7.89 KB |
| A_Dapper_SumOver                | A_Window          | 301.9 μs |  1.4648 |      - |  15.39 KB |
| A_Dapper_RowNumber              | A_Window          | 309.9 μs |  1.4648 |      - |  15.32 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 327.6 μs |  1.9531 |      - |  17.35 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 331.1 μs |  1.9531 |      - |  17.27 KB |
