```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Toolchain              | Categories        | Mean        | Median      | Gen0    | Gen1   | Allocated |
|-------------------------------- |----------------------- |------------------ |------------:|------------:|--------:|-------:|----------:|
| A_Nextorm_Prepared_CaseWhen     | Default                | A_CaseWhen        |    98.30 μs |    98.62 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_CaseWhen               | Default                | A_CaseWhen        |   171.52 μs |   164.49 μs |  1.4648 |      - |  13.44 KB |
| A_Linq2Db_Compiled_CaseWhen     | Default                | A_CaseWhen        |   284.47 μs |   287.11 μs |  1.7090 |      - |  15.94 KB |
| A_EFCore_Compiled_CaseWhen      | Default                | A_CaseWhen        |   368.54 μs |   369.97 μs |  9.2773 | 4.3945 |  78.98 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Cte          | Default                | A_Cte             |   144.98 μs |   147.81 μs |  0.7324 |      - |   7.73 KB |
| A_Dapper_Cte                    | Default                | A_Cte             |   220.27 μs |   219.08 μs |  1.7090 |      - |   15.7 KB |
| A_Linq2Db_Compiled_Cte          | Default                | A_Cte             |   234.76 μs |   232.77 μs |  1.9531 |      - |  17.73 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Distinct     | Default                | A_Distinct        |   137.83 μs |   140.25 μs |  0.7324 |      - |   7.27 KB |
| A_Linq2Db_Compiled_Distinct     | Default                | A_Distinct        |   214.54 μs |   204.84 μs |  1.7090 |      - |  15.94 KB |
| A_Dapper_Distinct               | Default                | A_Distinct        |   223.56 μs |   224.77 μs |  1.7090 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | Default                | A_Distinct        |   532.47 μs |   526.11 μs |  8.7891 | 3.9063 |  76.64 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Except       | Default                | A_Except          |   133.59 μs |   135.37 μs |  0.7324 |      - |   7.42 KB |
| A_Dapper_Except                 | Default                | A_Except          |   203.33 μs |   203.25 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | Default                | A_Except          |   248.53 μs |   256.40 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | Default                | A_Except          |   486.46 μs |   485.62 μs | 10.7422 | 4.8828 |  88.13 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_In           | Default                | A_InList          |   138.02 μs |   139.06 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_ListContains | Default                | A_InList          |   147.41 μs |   147.72 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | Default                | A_InList          |   216.49 μs |   220.86 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | Default                | A_InList          |   560.91 μs |   553.16 μs | 18.0664 |      - | 151.09 KB |
| A_EFCore_Compiled_In            | Default                | A_InList          |   775.37 μs |   736.85 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Intersect    | Default                | A_Intersect       |   124.55 μs |   124.11 μs |  0.8545 |      - |   7.27 KB |
| A_Dapper_Intersect              | Default                | A_Intersect       |   185.85 μs |   180.51 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | Default                | A_Intersect       |   221.49 μs |   219.12 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | Default                | A_Intersect       |   504.32 μs |   494.66 μs |  9.2773 | 4.3945 |  79.38 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Join4        | Default                | A_Join4           |   147.41 μs |   147.83 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | Default                | A_Join4           |   276.42 μs |   276.88 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | Default                | A_Join4           |   314.74 μs |   309.11 μs |  2.4414 |      - |  20.23 KB |
| A_EFCore_Compiled_Join4         | Default                | A_Join4           |   564.75 μs |   574.36 μs |  9.7656 | 4.8828 |  84.92 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | Default                | A_LeftJoin        |   134.81 μs |   135.32 μs |  1.4648 |      - |  12.03 KB |
| A_Dapper_LeftJoin               | Default                | A_LeftJoin        |   215.39 μs |   215.19 μs |  2.6855 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | Default                | A_LeftJoin        |   215.40 μs |   215.27 μs |  2.6855 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | Default                | A_LeftJoin        |   439.28 μs |   439.82 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | Default                | A_RecursiveCte    |   130.32 μs |   130.49 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | Default                | A_RecursiveCte    |   267.61 μs |   262.99 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | Default                | A_RecursiveCte    |   273.51 μs |   272.25 μs |  1.9531 |      - |  17.81 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Contains     | Default                | A_StringFunctions |    98.91 μs |    98.86 μs |  0.8545 |      - |   7.19 KB |
| A_Nextorm_Prepared_ToUpper      | Default                | A_StringFunctions |   100.52 μs |   101.65 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_ToUpper                | Default                | A_StringFunctions |   148.75 μs |   147.86 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_ToUpper      | Default                | A_StringFunctions |   160.44 μs |   159.93 μs |  1.9531 |      - |  16.41 KB |
| A_Linq2Db_Compiled_Contains     | Default                | A_StringFunctions |   167.69 μs |   167.68 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | Default                | A_StringFunctions |   202.10 μs |   202.58 μs |  1.9531 |      - |  17.19 KB |
| A_EFCore_Compiled_ToUpper       | Default                | A_StringFunctions |   356.04 μs |   356.50 μs |  8.7891 | 4.3945 |  74.92 KB |
| A_EFCore_Compiled_Contains      | Default                | A_StringFunctions |   395.91 μs |   388.87 μs |  8.7891 | 4.3945 |  74.69 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Udf          | Default                | A_Udf             |   101.26 μs |   101.59 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_Udf                    | Default                | A_Udf             |   146.08 μs |   144.01 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | Default                | A_Udf             |   155.40 μs |   155.67 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | Default                | A_Udf             |   371.77 μs |   372.11 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_RowNumber    | Default                | A_Window          |   325.29 μs |   303.26 μs |  0.9766 |      - |   8.28 KB |
| A_Nextorm_Prepared_SumOver      | Default                | A_Window          |   371.70 μs |   300.61 μs |  0.9766 |      - |   8.52 KB |
| A_Dapper_SumOver                | Default                | A_Window          |   380.01 μs |   358.40 μs |  0.9766 |      - |  15.47 KB |
| A_Dapper_RowNumber              | Default                | A_Window          |   410.56 μs |   360.71 μs |  0.9766 |      - |  15.39 KB |
| A_Linq2Db_Compiled_RowNumber    | Default                | A_Window          |   656.18 μs |   598.82 μs |  1.9531 |      - |  17.34 KB |
| A_Linq2Db_Compiled_SumOver      | Default                | A_Window          |   878.80 μs |   963.83 μs |  1.9531 |      - |  17.42 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_CaseWhen     | InProcessEmitToolchain | A_CaseWhen        |    99.86 μs |    99.74 μs |  0.8545 |      - |   7.27 KB |
| A_Linq2Db_Compiled_CaseWhen     | InProcessEmitToolchain | A_CaseWhen        |   163.02 μs |   162.49 μs |  1.7090 |      - |  15.94 KB |
| A_Dapper_CaseWhen               | InProcessEmitToolchain | A_CaseWhen        |   213.09 μs |   197.35 μs |  1.4648 |      - |  13.44 KB |
| A_EFCore_Compiled_CaseWhen      | InProcessEmitToolchain | A_CaseWhen        |   377.77 μs |   377.18 μs |  9.2773 | 4.3945 |  78.99 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Cte          | InProcessEmitToolchain | A_Cte             |   203.09 μs |   204.45 μs |  0.7324 |      - |   7.73 KB |
| A_Linq2Db_Compiled_Cte          | InProcessEmitToolchain | A_Cte             |   265.30 μs |   264.72 μs |  1.9531 |      - |  17.74 KB |
| A_Dapper_Cte                    | InProcessEmitToolchain | A_Cte             |   360.66 μs |   379.30 μs |  1.4648 |      - |  15.71 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Distinct     | InProcessEmitToolchain | A_Distinct        |   129.62 μs |   127.60 μs |  0.7324 |      - |   7.27 KB |
| A_Linq2Db_Compiled_Distinct     | InProcessEmitToolchain | A_Distinct        |   204.66 μs |   206.25 μs |  1.7090 |      - |  15.94 KB |
| A_Dapper_Distinct               | InProcessEmitToolchain | A_Distinct        |   215.57 μs |   214.06 μs |  1.7090 |      - |  15.55 KB |
| A_EFCore_Compiled_Distinct      | InProcessEmitToolchain | A_Distinct        |   502.08 μs |   512.06 μs |  9.2773 | 4.3945 |  76.64 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Except       | InProcessEmitToolchain | A_Except          |   129.56 μs |   125.37 μs |  0.7324 |      - |   7.42 KB |
| A_Dapper_Except                 | InProcessEmitToolchain | A_Except          |   209.50 μs |   207.73 μs |  1.7090 |      - |  14.38 KB |
| A_Linq2Db_Compiled_Except       | InProcessEmitToolchain | A_Except          |   280.99 μs |   240.21 μs |  1.9531 |      - |   16.8 KB |
| A_EFCore_Compiled_Except        | InProcessEmitToolchain | A_Except          |   535.39 μs |   538.43 μs | 10.7422 | 4.8828 |  88.13 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_In           | InProcessEmitToolchain | A_InList          |   146.75 μs |   143.42 μs |  1.7090 |      - |   15.7 KB |
| A_Nextorm_Prepared_ListContains | InProcessEmitToolchain | A_InList          |   147.36 μs |   138.94 μs |  1.7090 |      - |   15.7 KB |
| A_Dapper_In                     | InProcessEmitToolchain | A_InList          |   252.77 μs |   258.01 μs |  1.4648 |      - |  12.66 KB |
| A_Linq2Db_Compiled_In           | InProcessEmitToolchain | A_InList          |   537.02 μs |   512.68 μs | 18.5547 |      - | 152.35 KB |
| A_EFCore_Compiled_In            | InProcessEmitToolchain | A_InList          |   752.30 μs |   697.48 μs | 17.5781 | 5.8594 | 147.74 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Intersect    | InProcessEmitToolchain | A_Intersect       |   118.70 μs |   118.99 μs |  0.7324 |      - |   7.27 KB |
| A_Dapper_Intersect              | InProcessEmitToolchain | A_Intersect       |   186.41 μs |   187.07 μs |  1.4648 |      - |  12.89 KB |
| A_Linq2Db_Compiled_Intersect    | InProcessEmitToolchain | A_Intersect       |   230.64 μs |   229.18 μs |  1.9531 |      - |  16.33 KB |
| A_EFCore_Compiled_Intersect     | InProcessEmitToolchain | A_Intersect       |   524.62 μs |   522.60 μs |  8.7891 | 3.9063 |  79.38 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Join4        | InProcessEmitToolchain | A_Join4           |   151.16 μs |   141.39 μs |  1.2207 |      - |  10.55 KB |
| A_Linq2Db_Compiled_Join4        | InProcessEmitToolchain | A_Join4           |   260.99 μs |   261.11 μs |  2.4414 |      - |   20.7 KB |
| A_Dapper_Join4                  | InProcessEmitToolchain | A_Join4           |   273.10 μs |   273.82 μs |  2.4414 |      - |  20.24 KB |
| A_EFCore_Compiled_Join4         | InProcessEmitToolchain | A_Join4           |   534.21 μs |   539.28 μs |  9.7656 | 4.8828 |  84.93 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_LeftJoin     | InProcessEmitToolchain | A_LeftJoin        |   128.47 μs |   128.93 μs |  1.4648 |      - |  12.03 KB |
| A_Dapper_LeftJoin               | InProcessEmitToolchain | A_LeftJoin        |   216.09 μs |   215.19 μs |  2.6855 |      - |  22.19 KB |
| A_Linq2Db_Compiled_LeftJoin     | InProcessEmitToolchain | A_LeftJoin        |   225.19 μs |   225.35 μs |  2.6855 |      - |   23.2 KB |
| A_EFCore_Compiled_LeftJoin      | InProcessEmitToolchain | A_LeftJoin        |   466.20 μs |   464.22 μs | 11.7188 | 5.8594 |  99.22 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_RecursiveCte | InProcessEmitToolchain | A_RecursiveCte    |   146.26 μs |   145.25 μs |  0.7324 |      - |   7.34 KB |
| A_Dapper_RecursiveCte           | InProcessEmitToolchain | A_RecursiveCte    |   268.55 μs |   269.80 μs |  1.4648 |      - |  15.31 KB |
| A_Linq2Db_Compiled_RecursiveCte | InProcessEmitToolchain | A_RecursiveCte    |   276.25 μs |   274.09 μs |  1.9531 |      - |  17.81 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_ToUpper      | InProcessEmitToolchain | A_StringFunctions |   160.16 μs |   170.49 μs |  0.8545 |      - |    7.5 KB |
| A_Nextorm_Prepared_Contains     | InProcessEmitToolchain | A_StringFunctions |   208.38 μs |   165.56 μs |  0.7324 |      - |   7.19 KB |
| A_Linq2Db_Compiled_Contains     | InProcessEmitToolchain | A_StringFunctions |   212.70 μs |   200.00 μs |  1.9531 |      - |  16.17 KB |
| A_Dapper_Contains               | InProcessEmitToolchain | A_StringFunctions |   229.14 μs |   230.08 μs |  1.9531 |      - |  17.19 KB |
| A_Linq2Db_Compiled_ToUpper      | InProcessEmitToolchain | A_StringFunctions |   408.31 μs |   429.03 μs |  1.9531 |      - |  16.41 KB |
| A_Dapper_ToUpper                | InProcessEmitToolchain | A_StringFunctions |   459.27 μs |   371.45 μs |  1.4648 |      - |  12.81 KB |
| A_EFCore_Compiled_Contains      | InProcessEmitToolchain | A_StringFunctions |   587.01 μs |   623.52 μs |  8.7891 | 3.9063 |  74.69 KB |
| A_EFCore_Compiled_ToUpper       | InProcessEmitToolchain | A_StringFunctions | 1,084.36 μs | 1,019.80 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_Udf          | InProcessEmitToolchain | A_Udf             |   102.45 μs |    99.24 μs |  0.8545 |      - |    7.5 KB |
| A_Dapper_Udf                    | InProcessEmitToolchain | A_Udf             |   156.27 μs |   150.80 μs |  1.4648 |      - |  12.81 KB |
| A_Linq2Db_Compiled_Udf          | InProcessEmitToolchain | A_Udf             |   169.82 μs |   166.67 μs |  1.9531 |      - |  16.41 KB |
| A_EFCore_Compiled_Udf           | InProcessEmitToolchain | A_Udf             |   382.18 μs |   382.44 μs |  8.7891 | 4.3945 |  74.92 KB |
|                                 |                        |                   |             |             |         |        |           |
| A_Nextorm_Prepared_SumOver      | InProcessEmitToolchain | A_Window          |   194.12 μs |   179.21 μs |  0.9766 |      - |   8.52 KB |
| A_Linq2Db_Compiled_RowNumber    | InProcessEmitToolchain | A_Window          |   371.34 μs |   371.04 μs |  1.9531 |      - |  17.35 KB |
| A_Linq2Db_Compiled_SumOver      | InProcessEmitToolchain | A_Window          |   400.38 μs |   372.75 μs |  1.9531 |      - |  17.42 KB |
| A_Nextorm_Prepared_RowNumber    | InProcessEmitToolchain | A_Window          |   464.66 μs |   497.97 μs |  0.9766 |      - |   8.28 KB |
| A_Dapper_RowNumber              | InProcessEmitToolchain | A_Window          |   562.71 μs |   547.07 μs |  1.4648 |      - |  15.39 KB |
| A_Dapper_SumOver                | InProcessEmitToolchain | A_Window          |   574.58 μs |   565.27 μs |  1.4648 |      - |  15.47 KB |
