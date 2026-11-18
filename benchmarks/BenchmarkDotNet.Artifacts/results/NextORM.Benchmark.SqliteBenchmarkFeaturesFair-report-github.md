```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Toolchain=InProcessEmitToolchain  

```
| Method                          | Categories        | Mean     | Median   | Gen0    | Gen1   | Allocated |
|-------------------------------- |------------------ |---------:|---------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        | 108.3 μs | 108.2 μs |  0.7324 |      - |   6.48 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 159.5 μs | 158.9 μs |  1.4648 |      - |  13.36 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 166.8 μs | 166.9 μs |  1.7090 |      - |  15.86 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 347.9 μs | 348.8 μs |  8.7891 | 3.9063 |  74.62 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             | 114.2 μs | 114.1 μs |  0.8545 |      - |   7.34 KB |
| A_Dapper_Cte                    | A_Cte             | 230.7 μs | 230.5 μs |  1.7090 |      - |  15.63 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 236.9 μs | 237.0 μs |  1.9531 |      - |  17.66 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 127.7 μs | 127.6 μs |  0.7324 |      - |   6.49 KB |
| A_Dapper_Distinct               | A_Distinct        | 190.0 μs | 189.9 μs |  1.7090 |      - |  14.45 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 192.2 μs | 192.1 μs |  1.7090 |      - |  15.86 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 401.7 μs | 401.3 μs |  8.7891 | 4.3945 |  72.27 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 124.8 μs | 124.8 μs |  0.7324 |      - |   6.64 KB |
| A_Dapper_Except                 | A_Except          | 186.9 μs | 186.8 μs |  1.7090 |      - |   14.3 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 222.3 μs | 221.5 μs |  1.9531 |      - |  16.72 KB |
| A_EFCore_Compiled_Except        | A_Except          | 426.3 μs | 425.6 μs |  9.7656 | 4.8828 |  83.75 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_ListContains | A_InList          | 138.9 μs | 138.9 μs |  1.7090 |      - |  15.47 KB |
| A_Nextorm_Prepared_In           | A_InList          | 139.2 μs | 138.9 μs |  1.7090 |      - |  15.47 KB |
| A_Dapper_In                     | A_InList          | 199.8 μs | 199.6 μs |  1.4648 |      - |  12.58 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 445.6 μs | 445.3 μs | 16.6016 |      - | 136.33 KB |
| A_EFCore_Compiled_In            | A_InList          | 608.6 μs | 609.1 μs | 16.6016 | 4.8828 | 143.37 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 118.9 μs | 118.9 μs |  0.7324 |      - |   6.49 KB |
| A_Dapper_Intersect              | A_Intersect       | 178.1 μs | 177.2 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 218.2 μs | 217.6 μs |  1.9531 |      - |  16.25 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 475.7 μs | 474.2 μs |  8.7891 | 3.9063 |     75 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 143.1 μs | 142.9 μs |  1.2207 |      - |  10.31 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 252.1 μs | 249.7 μs |  2.4414 |      - |  20.63 KB |
| A_Dapper_Join4                  | A_Join4           | 271.8 μs | 272.5 μs |  2.4414 |      - |  20.16 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 470.8 μs | 470.6 μs |  9.7656 | 4.8828 |  80.55 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 140.7 μs | 140.5 μs |  1.2207 |      - |  11.64 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 218.7 μs | 218.7 μs |  2.6855 |      - |  22.11 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 224.2 μs | 224.6 μs |  2.6855 |      - |  23.13 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 432.8 μs | 427.7 μs | 10.7422 | 4.8828 |  94.85 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 139.3 μs | 139.2 μs |  0.7324 |      - |   6.56 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 261.7 μs | 261.2 μs |  1.4648 |      - |  15.24 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 280.2 μs | 279.7 μs |  1.9531 |      - |  17.74 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions | 106.8 μs | 106.7 μs |  0.7324 |      - |   6.41 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions | 107.1 μs | 107.0 μs |  0.7324 |      - |   6.72 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 154.8 μs | 154.9 μs |  1.4648 |      - |  12.74 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 163.5 μs | 163.7 μs |  1.9531 |      - |  16.33 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 177.1 μs | 176.4 μs |  1.9531 |      - |  16.09 KB |
| A_Dapper_Contains               | A_StringFunctions | 208.2 μs | 208.5 μs |  1.9531 |      - |  17.11 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 350.2 μs | 351.0 μs |  8.3008 | 3.9063 |  70.55 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 369.1 μs | 369.5 μs |  8.3008 | 3.9063 |  70.31 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             | 108.7 μs | 108.8 μs |  0.7324 |      - |   6.72 KB |
| A_Dapper_Udf                    | A_Udf             | 152.4 μs | 152.4 μs |  1.4648 |      - |  12.74 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 162.1 μs | 162.1 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 382.7 μs | 360.2 μs |  8.3008 | 3.9063 |  70.55 KB |
|                                 |                   |          |          |         |        |           |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 159.8 μs | 159.6 μs |  0.7324 |      - |   7.89 KB |
| A_Nextorm_Prepared_SumOver      | A_Window          | 187.0 μs | 188.6 μs |  0.7324 |      - |   7.73 KB |
| A_Dapper_RowNumber              | A_Window          | 317.5 μs | 317.4 μs |  1.4648 |      - |  15.32 KB |
| A_Dapper_SumOver                | A_Window          | 322.3 μs | 320.3 μs |  1.4648 |      - |  15.39 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 340.0 μs | 338.2 μs |  1.9531 |      - |  17.27 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 345.8 μs | 346.0 μs |  1.9531 |      - |  17.35 KB |
