```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Categories        | Mean      | Gen0    | Gen1   | Allocated |
|-------------------------------- |------------------ |----------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | A_CaseWhen        |  96.37 μs |  0.7324 |      - |   6.48 KB |
| A_Dapper_CaseWhen               | A_CaseWhen        | 140.72 μs |  1.4648 |      - |  13.36 KB |
| A_Linq2Db_Compiled_CaseWhen     | A_CaseWhen        | 154.73 μs |  1.7090 |      - |  15.86 KB |
| A_EFCore_Compiled_CaseWhen      | A_CaseWhen        | 337.00 μs |  8.7891 | 4.3945 |  74.61 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Cte          | A_Cte             | 101.28 μs |  0.7324 |      - |   6.95 KB |
| A_Dapper_Cte                    | A_Cte             | 215.98 μs |  1.7090 |      - |  15.63 KB |
| A_Linq2Db_Compiled_Cte          | A_Cte             | 220.39 μs |  1.9531 |      - |  17.66 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | A_Distinct        | 115.41 μs |  0.7324 |      - |   6.49 KB |
| A_Dapper_Distinct               | A_Distinct        | 178.39 μs |  1.7090 |      - |  14.45 KB |
| A_Linq2Db_Compiled_Distinct     | A_Distinct        | 181.63 μs |  1.7090 |      - |  15.86 KB |
| A_EFCore_Compiled_Distinct      | A_Distinct        | 389.03 μs |  8.7891 | 4.3945 |  72.27 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Except       | A_Except          | 111.56 μs |  0.7324 |      - |   6.64 KB |
| A_Dapper_Except                 | A_Except          | 173.19 μs |  1.7090 |      - |   14.3 KB |
| A_Linq2Db_Compiled_Except       | A_Except          | 211.98 μs |  1.9531 |      - |  16.72 KB |
| A_EFCore_Compiled_Except        | A_Except          | 426.13 μs |  9.7656 | 4.8828 |  83.75 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_In           | A_InList          | 128.99 μs |  1.8311 |      - |  15.47 KB |
| A_Nextorm_Prepared_ListContains | A_InList          | 131.24 μs |  1.7090 |      - |  15.47 KB |
| A_Dapper_In                     | A_InList          | 181.47 μs |  1.4648 |      - |  12.58 KB |
| A_Linq2Db_Compiled_In           | A_InList          | 430.83 μs | 16.6016 |      - | 136.33 KB |
| A_EFCore_Compiled_In            | A_InList          | 590.01 μs | 16.6016 | 4.8828 | 143.37 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | A_Intersect       | 101.17 μs |  0.7324 |      - |   6.49 KB |
| A_Dapper_Intersect              | A_Intersect       | 166.63 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Intersect    | A_Intersect       | 203.33 μs |  1.9531 |      - |  16.25 KB |
| A_EFCore_Compiled_Intersect     | A_Intersect       | 394.80 μs |  8.7891 | 4.3945 |     75 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Join4        | A_Join4           | 129.82 μs |  0.9766 |      - |   9.77 KB |
| A_Linq2Db_Compiled_Join4        | A_Join4           | 241.92 μs |  2.4414 |      - |  20.63 KB |
| A_Dapper_Join4                  | A_Join4           | 258.44 μs |  2.4414 |      - |  20.16 KB |
| A_EFCore_Compiled_Join4         | A_Join4           | 453.79 μs |  9.7656 | 4.8828 |  80.55 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | A_LeftJoin        | 126.81 μs |  1.2207 |      - |  11.25 KB |
| A_Dapper_LeftJoin               | A_LeftJoin        | 200.04 μs |  2.6855 |      - |  22.11 KB |
| A_Linq2Db_Compiled_LeftJoin     | A_LeftJoin        | 209.59 μs |  2.6855 |      - |  23.13 KB |
| A_EFCore_Compiled_LeftJoin      | A_LeftJoin        | 404.89 μs | 11.2305 | 5.3711 |  94.85 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | A_RecursiveCte    | 124.86 μs |  0.7324 |      - |   6.56 KB |
| A_Dapper_RecursiveCte           | A_RecursiveCte    | 241.75 μs |  1.7090 |      - |  15.24 KB |
| A_Linq2Db_Compiled_RecursiveCte | A_RecursiveCte    | 270.72 μs |  1.9531 |      - |  17.74 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Contains     | A_StringFunctions |  94.69 μs |  0.7324 |      - |   6.41 KB |
| A_Nextorm_Prepared_ToUpper      | A_StringFunctions |  96.43 μs |  0.7324 |      - |   6.72 KB |
| A_Dapper_ToUpper                | A_StringFunctions | 148.62 μs |  1.4648 |      - |  12.74 KB |
| A_Linq2Db_Compiled_ToUpper      | A_StringFunctions | 163.69 μs |  1.9531 |      - |  16.33 KB |
| A_Linq2Db_Compiled_Contains     | A_StringFunctions | 166.14 μs |  1.9531 |      - |  16.09 KB |
| A_Dapper_Contains               | A_StringFunctions | 200.57 μs |  1.9531 |      - |  17.11 KB |
| A_EFCore_Compiled_ToUpper       | A_StringFunctions | 332.12 μs |  8.3008 | 3.9063 |  70.55 KB |
| A_EFCore_Compiled_Contains      | A_StringFunctions | 344.14 μs |  8.3008 | 3.9063 |  70.32 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_Udf          | A_Udf             |  95.36 μs |  0.7324 |      - |   6.72 KB |
| A_Dapper_Udf                    | A_Udf             | 141.24 μs |  1.4648 |      - |  12.74 KB |
| A_Linq2Db_Compiled_Udf          | A_Udf             | 151.33 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Udf           | A_Udf             | 341.58 μs |  8.3008 | 3.9063 |  70.55 KB |
|                                 |                   |           |         |        |           |
| A_Nextorm_Prepared_SumOver      | A_Window          | 144.54 μs |  0.7324 |      - |   7.74 KB |
| A_Nextorm_Prepared_RowNumber    | A_Window          | 148.17 μs |  0.7324 |      - |    7.5 KB |
| A_Dapper_RowNumber              | A_Window          | 296.95 μs |  1.4648 |      - |  15.32 KB |
| A_Linq2Db_Compiled_SumOver      | A_Window          | 319.77 μs |  1.9531 |      - |  17.35 KB |
| A_Dapper_SumOver                | A_Window          | 321.21 μs |  1.4648 |      - |  15.39 KB |
| A_Linq2Db_Compiled_RowNumber    | A_Window          | 326.70 μs |  1.9531 |      - |  17.27 KB |
