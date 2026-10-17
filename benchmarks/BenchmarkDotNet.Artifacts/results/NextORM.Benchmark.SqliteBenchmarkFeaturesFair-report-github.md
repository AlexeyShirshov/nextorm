```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Toolchain              | Categories        | Mean      | Median    | Gen0    | Gen1   | Allocated |
|-------------------------------- |----------------------- |------------------ |----------:|----------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | Default                | A_CaseWhen        |  98.16 μs |  97.05 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | Default                | A_CaseWhen        | 146.41 μs | 143.46 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | Default                | A_CaseWhen        | 149.86 μs | 148.71 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | Default                | A_CaseWhen        | 363.36 μs | 363.34 μs |  9.2773 | 4.3945 |  78.98 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Cte          | Default                | A_Cte             |  98.16 μs |  98.08 μs |  0.8545 |      - |   7.73 KB |
| A_Dapper_Cte                    | Default                | A_Cte             | 210.01 μs | 209.41 μs |  1.7090 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | Default                | A_Cte             | 211.13 μs | 211.07 μs |  1.9531 |      - |  17.73 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | Default                | A_Distinct        | 113.71 μs | 113.43 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Distinct               | Default                | A_Distinct        | 179.64 μs | 179.36 μs |  1.7090 |      - |  15.55 KB |
| A_Linq2Db_Compiled_Distinct     | Default                | A_Distinct        | 184.73 μs | 185.08 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_Distinct      | Default                | A_Distinct        | 394.69 μs | 393.14 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Except       | Default                | A_Except          | 113.20 μs | 112.60 μs |  0.8545 |      - |   7.42 KB |
| A_Dapper_Except                 | Default                | A_Except          | 181.07 μs | 182.89 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | Default                | A_Except          | 220.50 μs | 218.23 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | Default                | A_Except          | 428.37 μs | 427.90 μs | 10.7422 | 5.3711 |  88.13 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_In           | Default                | A_InList          | 124.94 μs | 124.33 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_ListContains | Default                | A_InList          | 126.09 μs | 125.48 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | Default                | A_InList          | 181.19 μs | 180.98 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | Default                | A_InList          | 414.07 μs | 412.75 μs | 18.0664 |      - | 151.09 KB |
| A_EFCore_Compiled_In            | Default                | A_InList          | 583.36 μs | 579.77 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | Default                | A_Intersect       | 116.82 μs | 112.27 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Intersect              | Default                | A_Intersect       | 161.86 μs | 159.87 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | Default                | A_Intersect       | 196.30 μs | 196.11 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | Default                | A_Intersect       | 439.55 μs | 444.33 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Join4        | Default                | A_Join4           | 129.58 μs | 129.50 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | Default                | A_Join4           | 236.00 μs | 233.23 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | Default                | A_Join4           | 276.47 μs | 284.11 μs |  2.4414 |      - |  20.23 KB |
| A_EFCore_Compiled_Join4         | Default                | A_Join4           | 465.15 μs | 464.88 μs |  9.7656 | 4.8828 |  84.92 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | Default                | A_LeftJoin        | 138.33 μs | 137.32 μs |  1.4648 |      - |  12.03 KB |
| A_Dapper_LeftJoin               | Default                | A_LeftJoin        | 259.39 μs | 267.36 μs |  1.9531 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | Default                | A_LeftJoin        | 279.86 μs | 275.74 μs |  2.4414 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | Default                | A_LeftJoin        | 421.33 μs | 422.15 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | Default                | A_RecursiveCte    | 124.08 μs | 123.96 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | Default                | A_RecursiveCte    | 238.27 μs | 238.29 μs |  1.7090 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | Default                | A_RecursiveCte    | 261.13 μs | 258.67 μs |  1.9531 |      - |  17.81 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Contains     | Default                | A_StringFunctions |  94.78 μs |  94.44 μs |  0.8545 |      - |   7.19 KB |
| A_Nextorm_Prepared_ToUpper      | Default                | A_StringFunctions |  94.99 μs |  94.84 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_ToUpper                | Default                | A_StringFunctions | 147.26 μs | 144.65 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | Default                | A_StringFunctions | 154.62 μs | 154.79 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | Default                | A_StringFunctions | 157.13 μs | 157.42 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | Default                | A_StringFunctions | 199.12 μs | 196.55 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | Default                | A_StringFunctions | 362.00 μs | 349.47 μs |  8.7891 | 4.3945 |  74.92 KB |
| A_EFCore_Compiled_Contains      | Default                | A_StringFunctions | 378.05 μs | 377.59 μs |  8.7891 | 4.3945 |  74.69 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Udf          | Default                | A_Udf             |  98.04 μs |  98.26 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_Udf                    | Default                | A_Udf             | 143.71 μs | 143.39 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | Default                | A_Udf             | 148.12 μs | 147.86 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | Default                | A_Udf             | 354.41 μs | 352.72 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_SumOver      | Default                | A_Window          | 145.09 μs | 145.09 μs |  0.9766 |      - |   8.52 KB |
| A_Nextorm_Prepared_RowNumber    | Default                | A_Window          | 146.76 μs | 146.94 μs |  0.9766 |      - |   8.28 KB |
| A_Dapper_SumOver                | Default                | A_Window          | 295.02 μs | 293.95 μs |  1.4648 |      - |  15.47 KB |
| A_Dapper_RowNumber              | Default                | A_Window          | 298.64 μs | 297.80 μs |  1.4648 |      - |  15.39 KB |
| A_Linq2Db_Compiled_SumOver      | Default                | A_Window          | 307.89 μs | 308.69 μs |  1.9531 |      - |  17.42 KB |
| A_Linq2Db_Compiled_RowNumber    | Default                | A_Window          | 309.44 μs | 309.70 μs |  1.9531 |      - |  17.34 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_CaseWhen     | InProcessEmitToolchain | A_CaseWhen        |  93.41 μs |  93.39 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | InProcessEmitToolchain | A_CaseWhen        | 142.62 μs | 142.49 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | InProcessEmitToolchain | A_CaseWhen        | 149.08 μs | 149.43 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | InProcessEmitToolchain | A_CaseWhen        | 345.74 μs | 346.63 μs |  9.2773 | 4.3945 |  78.99 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Cte          | InProcessEmitToolchain | A_Cte             |  96.41 μs |  96.10 μs |  0.8545 |      - |   7.74 KB |
| A_Dapper_Cte                    | InProcessEmitToolchain | A_Cte             | 207.03 μs | 207.29 μs |  1.7090 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | InProcessEmitToolchain | A_Cte             | 217.37 μs | 215.27 μs |  1.9531 |      - |  17.74 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Distinct     | InProcessEmitToolchain | A_Distinct        | 112.02 μs | 111.99 μs |  0.8545 |      - |   7.27 KB |
| A_Linq2Db_Compiled_Distinct     | InProcessEmitToolchain | A_Distinct        | 178.95 μs | 178.40 μs |  1.7090 |      - |  15.94 KB |
| A_Dapper_Distinct               | InProcessEmitToolchain | A_Distinct        | 187.21 μs | 187.60 μs |  1.7090 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | InProcessEmitToolchain | A_Distinct        | 409.94 μs | 411.86 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Except       | InProcessEmitToolchain | A_Except          | 109.73 μs | 109.77 μs |  0.8545 |      - |   7.42 KB |
| A_Dapper_Except                 | InProcessEmitToolchain | A_Except          | 170.20 μs | 170.19 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | InProcessEmitToolchain | A_Except          | 220.74 μs | 218.60 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | InProcessEmitToolchain | A_Except          | 514.12 μs | 437.97 μs | 10.7422 | 5.3711 |  88.13 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_In           | InProcessEmitToolchain | A_InList          | 124.16 μs | 124.10 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_ListContains | InProcessEmitToolchain | A_InList          | 125.97 μs | 125.09 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | InProcessEmitToolchain | A_InList          | 264.49 μs | 184.81 μs |  0.9766 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | InProcessEmitToolchain | A_InList          | 771.85 μs | 816.75 μs | 18.5547 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | InProcessEmitToolchain | A_InList          | 907.04 μs | 933.39 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Intersect    | InProcessEmitToolchain | A_Intersect       | 104.33 μs | 104.45 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Intersect              | InProcessEmitToolchain | A_Intersect       | 159.19 μs | 159.39 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | InProcessEmitToolchain | A_Intersect       | 193.29 μs | 193.38 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | InProcessEmitToolchain | A_Intersect       | 423.36 μs | 424.12 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Join4        | InProcessEmitToolchain | A_Join4           | 149.13 μs | 153.75 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | InProcessEmitToolchain | A_Join4           | 240.64 μs | 236.77 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | InProcessEmitToolchain | A_Join4           | 330.10 μs | 336.84 μs |  2.4414 |      - |  20.23 KB |
| A_EFCore_Compiled_Join4         | InProcessEmitToolchain | A_Join4           | 555.82 μs | 479.30 μs |  9.7656 | 4.8828 |  84.93 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | InProcessEmitToolchain | A_LeftJoin        | 133.06 μs | 125.81 μs |  1.4648 |      - |  12.03 KB |
| A_Linq2Db_Compiled_LeftJoin     | InProcessEmitToolchain | A_LeftJoin        | 203.64 μs | 203.50 μs |  2.6855 |      - |   23.2 KB |
| A_Dapper_LeftJoin               | InProcessEmitToolchain | A_LeftJoin        | 205.70 μs | 204.49 μs |  2.6855 |      - |  22.19 KB |
| A_EFCore_Compiled_LeftJoin      | InProcessEmitToolchain | A_LeftJoin        | 451.98 μs | 446.93 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | InProcessEmitToolchain | A_RecursiveCte    | 128.16 μs | 126.59 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | InProcessEmitToolchain | A_RecursiveCte    | 245.59 μs | 245.44 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | InProcessEmitToolchain | A_RecursiveCte    | 277.44 μs | 273.87 μs |  1.9531 |      - |  17.81 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Contains     | InProcessEmitToolchain | A_StringFunctions |  90.94 μs |  91.05 μs |  0.8545 |      - |   7.19 KB |
| A_Nextorm_Prepared_ToUpper      | InProcessEmitToolchain | A_StringFunctions |  94.35 μs |  94.52 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_ToUpper                | InProcessEmitToolchain | A_StringFunctions | 139.70 μs | 138.83 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | InProcessEmitToolchain | A_StringFunctions | 153.98 μs | 154.30 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | InProcessEmitToolchain | A_StringFunctions | 164.07 μs | 164.35 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | InProcessEmitToolchain | A_StringFunctions | 190.98 μs | 190.68 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | InProcessEmitToolchain | A_StringFunctions | 362.17 μs | 364.17 μs |  8.7891 | 4.3945 |  74.92 KB |
| A_EFCore_Compiled_Contains      | InProcessEmitToolchain | A_StringFunctions | 386.47 μs | 384.95 μs |  8.7891 | 4.3945 |  74.69 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_Udf          | InProcessEmitToolchain | A_Udf             |  93.13 μs |  93.07 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_Udf                    | InProcessEmitToolchain | A_Udf             | 142.58 μs | 142.44 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | InProcessEmitToolchain | A_Udf             | 152.77 μs | 152.30 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | InProcessEmitToolchain | A_Udf             | 347.09 μs | 347.22 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                        |                   |           |           |         |        |           |
| A_Nextorm_Prepared_RowNumber    | InProcessEmitToolchain | A_Window          | 141.44 μs | 141.39 μs |  0.9766 |      - |   8.28 KB |
| A_Nextorm_Prepared_SumOver      | InProcessEmitToolchain | A_Window          | 142.89 μs | 141.22 μs |  0.9766 |      - |   8.52 KB |
| A_Dapper_SumOver                | InProcessEmitToolchain | A_Window          | 307.00 μs | 300.04 μs |  1.4648 |      - |  15.47 KB |
| A_Linq2Db_Compiled_SumOver      | InProcessEmitToolchain | A_Window          | 308.17 μs | 308.08 μs |  1.9531 |      - |  17.42 KB |
| A_Dapper_RowNumber              | InProcessEmitToolchain | A_Window          | 308.24 μs | 305.72 μs |  1.4648 |      - |  15.39 KB |
| A_Linq2Db_Compiled_RowNumber    | InProcessEmitToolchain | A_Window          | 315.86 μs | 316.28 μs |  1.9531 |      - |  17.35 KB |
