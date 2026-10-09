```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                             | InvocationCount | UnrollFactor | Categories                      | Mean     | Gen0     | Gen1     | Gen2    | Allocated  |
|----------------------------------- |---------------- |------------- |-------------------------------- |---------:|---------:|---------:|--------:|-----------:|
| A_Nextorm_Prepared_ToList_Dto      | Default         | 16           | A_BufferedDto                   | 3.743 ms | 160.1563 | 117.1875 |       - | 1328.84 KB |
| Linq2Db_Compiled_ToList_Dto        | Default         | 16           | A_BufferedDto                   | 5.637 ms | 218.7500 | 210.9375 | 54.6875 | 1508.25 KB |
| A_EFCore_Compiled_ToList_Dto       | Default         | 16           | A_BufferedDto                   | 8.012 ms | 515.6250 | 296.8750 | 78.1250 | 3701.54 KB |
|                                    |                 |              |                                 |          |          |          |         |            |
| Dapper_ToList_Dto                  | Default         | 16           | A_BufferedDto,B_BufferedDto     | 6.864 ms | 257.8125 | 250.0000 | 62.5000 | 1742.31 KB |
|                                    |                 |              |                                 |          |          |          |         |            |
| A_Nextorm_Prepared_AsyncStream_Dto | Default         | 16           | A_UnbufferedDto                 | 3.663 ms | 152.3438 |        - |       - | 1250.56 KB |
| A_EFCore_Compiled_AsyncStream_Dto  | Default         | 16           | A_UnbufferedDto                 | 5.047 ms | 414.0625 |        - |       - | 3444.77 KB |
|                                    |                 |              |                                 |          |          |          |         |            |
| Dapper_AsyncStream_Dto             | Default         | 16           | A_UnbufferedDto,B_UnbufferedDto | 4.591 ms | 179.6875 |        - |       - | 1485.82 KB |
|                                    |                 |              |                                 |          |          |          |         |            |
| B_Nextorm_Cached_ToList_Dto        | Default         | 16           | B_BufferedDto                   | 3.786 ms | 160.1563 | 109.3750 |       - | 1332.39 KB |
| B_Linq2Db_ToList_Dto               | Default         | 16           | B_BufferedDto                   | 8.148 ms | 218.7500 | 210.9375 | 54.6875 |  1511.3 KB |
| B_EFCore_ToList_Dto                | Default         | 16           | B_BufferedDto                   | 8.594 ms | 500.0000 | 250.0000 | 62.5000 | 3705.59 KB |
|                                    |                 |              |                                 |          |          |          |         |            |
| B_Nextorm_Cached_AsyncStream_Dto   | Default         | 16           | B_UnbufferedDto                 | 4.019 ms | 148.4375 |        - |       - |  1254.3 KB |
| B_Linq2Db_AsyncStream_Dto          | Default         | 16           | B_UnbufferedDto                 | 4.595 ms | 148.4375 |        - |       - | 1254.55 KB |
| B_EFCore_AsyncStream_Dto           | Default         | 16           | B_UnbufferedDto                 | 5.105 ms | 421.8750 |   7.8125 |       - | 3448.89 KB |
|                                    |                 |              |                                 |          |          |          |         |            |
| A_Nextorm_Prepared_ToDataReader    | 1               | 1            | A_RawReader                     | 4.302 ms |        - |        - |       - |   944.8 KB |
|                                    |                 |              |                                 |          |          |          |         |            |
| Dapper_ToDataReader                | 1               | 1            | B_RawReader                     | 3.761 ms |        - |        - |       - |  942.77 KB |
| Linq2Db_ToDataReader               | 1               | 1            | B_RawReader                     | 3.762 ms |        - |        - |       - |     943 KB |
