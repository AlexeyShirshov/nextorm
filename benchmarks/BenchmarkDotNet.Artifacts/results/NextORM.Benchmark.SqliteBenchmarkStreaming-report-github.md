```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                             | InvocationCount | UnrollFactor | Categories                      | Mean         | Gen0     | Gen1     | Gen2    | Allocated  |
|----------------------------------- |---------------- |------------- |-------------------------------- |-------------:|---------:|---------:|--------:|-----------:|
| A_Nextorm_Prepared_ToList_Dto      | Default         | 16           | A_BufferedDto                   |  3,956.46 μs | 156.2500 | 109.3750 |       - | 1328.77 KB |
| Linq2Db_Compiled_ToList_Dto        | Default         | 16           | A_BufferedDto                   |  5,960.94 μs | 218.7500 | 210.9375 | 54.6875 | 1508.22 KB |
| A_EFCore_Compiled_ToList_Dto       | Default         | 16           | A_BufferedDto                   | 10,629.30 μs | 515.6250 | 296.8750 | 78.1250 | 3701.53 KB |
|                                    |                 |              |                                 |              |          |          |         |            |
| Dapper_ToList_Dto                  | Default         | 16           | A_BufferedDto,B_BufferedDto     |  8,223.40 μs | 250.0000 | 234.3750 | 62.5000 | 1742.27 KB |
|                                    |                 |              |                                 |              |          |          |         |            |
| A_Nextorm_Prepared_AsyncStream_Dto | Default         | 16           | A_UnbufferedDto                 |  4,585.96 μs | 148.4375 |        - |       - | 1250.84 KB |
| A_EFCore_Compiled_AsyncStream_Dto  | Default         | 16           | A_UnbufferedDto                 |  5,292.02 μs | 414.0625 |        - |       - | 3444.77 KB |
|                                    |                 |              |                                 |              |          |          |         |            |
| Dapper_AsyncStream_Dto             | Default         | 16           | A_UnbufferedDto,B_UnbufferedDto |  4,785.98 μs | 179.6875 |        - |       - | 1485.82 KB |
|                                    |                 |              |                                 |              |          |          |         |            |
| B_Nextorm_Cached_ToList_Dto        | Default         | 16           | B_BufferedDto                   |  3,954.38 μs | 156.2500 | 101.5625 |       - | 1332.18 KB |
| B_Linq2Db_ToList_Dto               | Default         | 16           | B_BufferedDto                   |  6,128.13 μs | 218.7500 | 210.9375 | 54.6875 | 1511.21 KB |
| B_EFCore_ToList_Dto                | Default         | 16           | B_BufferedDto                   |  9,005.49 μs | 500.0000 | 250.0000 | 62.5000 | 3705.72 KB |
|                                    |                 |              |                                 |              |          |          |         |            |
| B_Nextorm_Cached_AsyncStream_Dto   | Default         | 16           | B_UnbufferedDto                 |  4,255.35 μs | 148.4375 |        - |       - | 1254.42 KB |
| B_Linq2Db_AsyncStream_Dto          | Default         | 16           | B_UnbufferedDto                 |  4,982.80 μs | 148.4375 |        - |       - | 1254.55 KB |
| B_EFCore_AsyncStream_Dto           | Default         | 16           | B_UnbufferedDto                 |  5,295.63 μs | 421.8750 |   7.8125 |       - | 3448.89 KB |
|                                    |                 |              |                                 |              |          |          |         |            |
| A_Nextorm_Prepared_ToDataReader    | 1               | 1            | A_RawReader                     |  4,453.12 μs |        - |        - |       - |  948.45 KB |
|                                    |                 |              |                                 |              |          |          |         |            |
| Dapper_ToDataReader                | 1               | 1            | B_RawReader                     |  3,954.36 μs |        - |        - |       - |   943.1 KB |
| Linq2Db_ToDataReader               | 1               | 1            | B_RawReader                     |  4,006.42 μs |        - |        - |       - |  943.33 KB |
| B_Nextorm_ToDataReader             | 1               | 1            | B_RawReader                     |  4,317.28 μs |        - |        - |       - |   951.3 KB |
| Provider_Direct_ToDataReader       | 1               | 1            | B_RawReader                     |  4,351.45 μs |        - |        - |       - |  943.07 KB |
|                                    |                 |              |                                 |              |          |          |         |            |
| Provider_Direct_ToDataReader_Empty | 1               | 1            | B_RawReader_Empty               |     88.48 μs |        - |        - |       - |    5.59 KB |
| Linq2Db_ToDataReader_Empty         | 1               | 1            | B_RawReader_Empty               |    123.39 μs |        - |        - |       - |    5.84 KB |
| Dapper_ToDataReader_Empty          | 1               | 1            | B_RawReader_Empty               |    131.77 μs |        - |        - |       - |    5.62 KB |
| B_Nextorm_ToDataReader_Empty       | 1               | 1            | B_RawReader_Empty               |    238.79 μs |        - |        - |       - |    16.3 KB |
