```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                        | Categories                | Mean       | Gen0    | Gen1    | Allocated |
|------------------------------ |-------------------------- |-----------:|--------:|--------:|----------:|
| Nextorm_Prepared_CaseWhen     | CaseWhen                  |   256.4 μs |  1.9531 |       - |  16.41 KB |
| Linq2Db_CaseWhen              | CaseWhen                  |   689.6 μs | 10.7422 |       - |  95.12 KB |
| EFCore_CaseWhen               | CaseWhen                  | 1,504.6 μs | 35.1563 | 15.6250 | 298.85 KB |
|                               |                           |            |         |         |           |
| Dapper_CaseWhen               | CaseWhen,DapperRaw        |   390.5 μs |  3.9063 |       - |   33.6 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_Cte          | Cte                       |   256.6 μs |  1.9531 |       - |  17.58 KB |
| Linq2Db_Cte                   | Cte                       | 1,599.0 μs | 27.3438 |       - | 232.24 KB |
|                               |                           |            |         |         |           |
| Dapper_Cte                    | Cte,DapperRaw             |   573.5 μs |  3.9063 |       - |  39.26 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_Distinct     | Distinct                  |   290.2 μs |  1.9531 |       - |  16.41 KB |
| Linq2Db_Distinct              | Distinct                  |   860.5 μs | 11.7188 |       - |  97.86 KB |
| EFCore_Distinct               | Distinct                  | 1,903.3 μs | 37.1094 | 11.7188 | 304.89 KB |
|                               |                           |            |         |         |           |
| Dapper_Distinct               | Distinct,DapperRaw        |   502.5 μs |  3.9063 |       - |  38.87 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_Except       | Except                    |   305.6 μs |  1.9531 |       - |   16.8 KB |
| Linq2Db_Except                | Except                    | 1,146.8 μs | 13.6719 |       - | 126.38 KB |
| EFCore_Except                 | Except                    | 2,027.6 μs | 42.9688 | 11.7188 | 382.63 KB |
|                               |                           |            |         |         |           |
| Dapper_Except                 | Except,DapperRaw          |   527.8 μs |  3.9063 |       - |  35.94 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_In           | InList                    |   315.6 μs |  4.3945 |       - |   37.5 KB |
| Nextorm_Prepared_ListContains | InList                    |   328.0 μs |  4.3945 |       - |   37.5 KB |
| Linq2Db_In                    | InList                    | 2,178.8 μs | 54.6875 |       - | 472.87 KB |
| EFCore_InContains             | InList                    | 3,107.6 μs | 66.4063 | 19.5313 | 563.59 KB |
|                               |                           |            |         |         |           |
| Dapper_In                     | InList,DapperRaw          |   515.4 μs |  2.9297 |       - |  31.65 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_Intersect    | Intersect                 |   267.0 μs |  1.9531 |       - |  16.41 KB |
| Linq2Db_Intersect             | Intersect                 | 1,034.2 μs | 13.6719 |       - |  125.2 KB |
| EFCore_Intersect              | Intersect                 | 2,027.1 μs | 42.9688 | 11.7188 | 359.39 KB |
|                               |                           |            |         |         |           |
| Dapper_Intersect              | Intersect,DapperRaw       |   467.9 μs |  3.9063 |       - |  32.23 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_Join4        | Join4                     |   348.8 μs |  2.9297 |       - |  24.61 KB |
| Linq2Db_Join4                 | Join4                     | 1,861.4 μs | 41.0156 |       - | 349.63 KB |
| EFCore_Join4                  | Join4                     | 3,226.0 μs | 74.2188 | 19.5313 |  618.4 KB |
|                               |                           |            |         |         |           |
| Dapper_Join4                  | Join4,DapperRaw           |   705.2 μs |  5.8594 |       - |  50.59 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_LeftJoin     | LeftJoin                  |   347.7 μs |  3.4180 |       - |  28.32 KB |
| Linq2Db_LeftJoin              | LeftJoin                  | 2,182.1 μs | 23.4375 |       - | 218.18 KB |
| EFCore_LeftJoin               | LeftJoin                  | 2,849.6 μs | 62.5000 | 19.5313 | 511.94 KB |
|                               |                           |            |         |         |           |
| Dapper_LeftJoin               | LeftJoin,DapperRaw        |   628.2 μs |  5.8594 |       - |  55.47 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_RecursiveCte | RecursiveCte              |   318.8 μs |  1.9531 |       - |   16.6 KB |
| Linq2Db_RecursiveCte          | RecursiveCte              | 1,669.6 μs | 25.3906 |       - | 209.78 KB |
|                               |                           |            |         |         |           |
| Dapper_RecursiveCte           | RecursiveCte,DapperRaw    |   688.7 μs |  3.9063 |       - |  38.29 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_ToUpper      | StringFunctions           |   253.1 μs |  1.9531 |       - |  16.99 KB |
| Nextorm_Prepared_Contains     | StringFunctions           |   265.8 μs |  1.9531 |       - |  16.21 KB |
| Linq2Db_Contains              | StringFunctions           |   893.6 μs | 13.6719 |       - | 119.54 KB |
| Linq2Db_ToUpper               | StringFunctions           | 1,412.4 μs | 19.5313 |       - | 159.77 KB |
| EFCore_ToUpper                | StringFunctions           | 1,955.1 μs | 39.0625 | 11.7188 | 331.84 KB |
| EFCore_Contains               | StringFunctions           | 1,973.1 μs | 39.0625 | 11.7188 | 350.22 KB |
|                               |                           |            |         |         |           |
| Dapper_ToUpper                | StringFunctions,DapperRaw |   400.3 μs |  3.9063 |       - |  32.03 KB |
| Dapper_Contains               | StringFunctions,DapperRaw |   558.5 μs |  4.8828 |       - |  42.97 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_Tvf          | Tvf                       |   263.0 μs |  1.9531 |       - |   16.6 KB |
|                               |                           |            |         |         |           |
| Dapper_Tvf                    | Tvf,DapperRaw             |   408.5 μs |  3.4180 |       - |  31.84 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_Udf          | Udf                       |   252.9 μs |  1.9531 |       - |  16.99 KB |
| Linq2Db_Udf                   | Udf                       | 1,510.5 μs | 19.5313 |       - | 159.77 KB |
| EFCore_Udf                    | Udf                       | 1,879.9 μs | 39.0625 | 11.7188 | 331.85 KB |
|                               |                           |            |         |         |           |
| Dapper_Udf                    | Udf,DapperRaw             |   468.0 μs |  3.9063 |       - |  32.03 KB |
|                               |                           |            |         |         |           |
| Nextorm_Prepared_RowNumber    | Window                    |   394.9 μs |  1.9531 |       - |  18.95 KB |
| Nextorm_Prepared_SumOver      | Window                    |   403.7 μs |  1.9531 |       - |  19.53 KB |
| Linq2Db_RowNumber             | Window                    | 1,863.6 μs | 17.5781 |       - | 152.55 KB |
| Linq2Db_SumOver               | Window                    | 1,993.0 μs | 17.5781 |       - | 145.52 KB |
|                               |                           |            |         |         |           |
| Dapper_RowNumber              | Window,DapperRaw          |   922.2 μs |  3.9063 |       - |  38.48 KB |
| Dapper_SumOver                | Window,DapperRaw          |   929.4 μs |  3.9063 |       - |  38.68 KB |
