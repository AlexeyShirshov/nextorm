```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                             | InvocationCount | UnrollFactor | Categories                      | Mean       | Gen0     | Gen1     | Gen2    | Allocated  |
|----------------------------------- |---------------- |------------- |-------------------------------- |-----------:|---------:|---------:|--------:|-----------:|
| A_Nextorm_Prepared_ToList_Dto      | Default         | 16           | A_BufferedDto                   | 3,861.9 μs | 156.2500 | 109.3750 |       - | 1328.84 KB |
| Linq2Db_Compiled_ToList_Dto        | Default         | 16           | A_BufferedDto                   | 5,905.6 μs | 218.7500 | 210.9375 | 54.6875 | 1508.23 KB |
| A_EFCore_Compiled_ToList_Dto       | Default         | 16           | A_BufferedDto                   | 8,623.7 μs | 515.6250 | 312.5000 | 78.1250 | 3701.68 KB |
|                                    |                 |              |                                 |            |          |          |         |            |
| Dapper_ToList_Dto                  | Default         | 16           | A_BufferedDto,B_BufferedDto     | 7,101.7 μs | 257.8125 | 250.0000 | 62.5000 |  1742.3 KB |
|                                    |                 |              |                                 |            |          |          |         |            |
| A_Nextorm_Prepared_AsyncStream_Dto | Default         | 16           | A_UnbufferedDto                 | 3,901.2 μs | 148.4375 |        - |       - | 1250.54 KB |
| A_EFCore_Compiled_AsyncStream_Dto  | Default         | 16           | A_UnbufferedDto                 | 5,306.8 μs | 414.0625 |        - |       - | 3444.77 KB |
|                                    |                 |              |                                 |            |          |          |         |            |
| Dapper_AsyncStream_Dto             | Default         | 16           | A_UnbufferedDto,B_UnbufferedDto | 4,886.5 μs | 179.6875 |        - |       - | 1485.82 KB |
|                                    |                 |              |                                 |            |          |          |         |            |
| B_Nextorm_Cached_ToList_Dto        | Default         | 16           | B_BufferedDto                   | 4,043.8 μs | 156.2500 | 101.5625 |       - | 1332.49 KB |
| B_Linq2Db_ToList_Dto               | Default         | 16           | B_BufferedDto                   | 6,053.1 μs | 218.7500 | 210.9375 | 54.6875 | 1511.28 KB |
| B_EFCore_ToList_Dto                | Default         | 16           | B_BufferedDto                   | 8,837.9 μs | 500.0000 | 250.0000 | 62.5000 | 3705.77 KB |
|                                    |                 |              |                                 |            |          |          |         |            |
| B_Nextorm_Cached_AsyncStream_Dto   | Default         | 16           | B_UnbufferedDto                 | 4,058.7 μs | 148.4375 |        - |       - | 1254.38 KB |
| B_Linq2Db_AsyncStream_Dto          | Default         | 16           | B_UnbufferedDto                 | 4,521.6 μs | 148.4375 |        - |       - | 1254.55 KB |
| B_EFCore_AsyncStream_Dto           | Default         | 16           | B_UnbufferedDto                 | 5,166.1 μs | 421.8750 |   7.8125 |       - | 3448.93 KB |
|                                    |                 |              |                                 |            |          |          |         |            |
| A_Nextorm_Prepared_ToDataReader    | 1               | 1            | A_RawReader                     | 4,753.7 μs |        - |        - |       - |   944.8 KB |
|                                    |                 |              |                                 |            |          |          |         |            |
| Linq2Db_ToDataReader               | 1               | 1            | B_RawReader                     | 3,941.5 μs |        - |        - |       - |     943 KB |
| Dapper_ToDataReader                | 1               | 1            | B_RawReader                     | 3,996.3 μs |        - |        - |       - |  942.77 KB |
| B_Nextorm_ToDataReader             | 1               | 1            | B_RawReader                     | 4,676.8 μs |        - |        - |       - |  947.98 KB |
|                                    |                 |              |                                 |            |          |          |         |            |
| Linq2Db_ToDataReader_Empty         | 1               | 1            | B_RawReader_Empty               |   109.2 μs |        - |        - |       - |    5.52 KB |
| Dapper_ToDataReader_Empty          | 1               | 1            | B_RawReader_Empty               |   126.7 μs |        - |        - |       - |    5.29 KB |
| B_Nextorm_ToDataReader_Empty       | 1               | 1            | B_RawReader_Empty               |   237.7 μs |        - |        - |       - |   12.74 KB |
