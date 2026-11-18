```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                             | InvocationCount | UnrollFactor | Categories                      | Mean         | Median       | Gen0     | Gen1     | Gen2    | Allocated |
|----------------------------------- |---------------- |------------- |-------------------------------- |-------------:|-------------:|---------:|---------:|--------:|----------:|
| A_Nextorm_Prepared_ToList_Dto      | Default         | 16           | A_BufferedDto                   |  5,217.99 μs |  5,187.05 μs | 156.2500 | 109.3750 |       - | 1360688 B |
| Linq2Db_Compiled_ToList_Dto        | Default         | 16           | A_BufferedDto                   |  6,867.59 μs |  6,882.18 μs | 218.7500 | 210.9375 | 54.6875 | 1544580 B |
| A_EFCore_Compiled_ToList_Dto       | Default         | 16           | A_BufferedDto                   | 10,303.59 μs | 10,229.68 μs | 515.6250 | 296.8750 | 78.1250 | 3790111 B |
|                                    |                 |              |                                 |              |              |          |          |         |           |
| Dapper_ToList_Dto                  | Default         | 16           | A_BufferedDto,B_BufferedDto     |  7,831.00 μs |  7,785.22 μs | 257.8125 | 250.0000 | 62.5000 | 1784116 B |
|                                    |                 |              |                                 |              |              |          |          |         |           |
| A_Nextorm_Prepared_AsyncStream_Dto | Default         | 16           | A_UnbufferedDto                 |  5,102.05 μs |  5,097.71 μs | 148.4375 |        - |       - | 1280890 B |
| A_EFCore_Compiled_AsyncStream_Dto  | Default         | 16           | A_UnbufferedDto                 |  6,390.56 μs |  6,366.41 μs | 414.0625 |        - |       - | 3527002 B |
|                                    |                 |              |                                 |              |              |          |          |         |           |
| Dapper_AsyncStream_Dto             | Default         | 16           | A_UnbufferedDto,B_UnbufferedDto |  5,615.85 μs |  5,616.76 μs | 179.6875 |        - |       - | 1521474 B |
|                                    |                 |              |                                 |              |              |          |          |         |           |
| B_Nextorm_Cached_ToList_Dto        | Default         | 16           | B_BufferedDto                   |  5,110.60 μs |  5,059.42 μs | 156.2500 | 109.3750 |       - | 1362858 B |
| B_Linq2Db_ToList_Dto               | Default         | 16           | B_BufferedDto                   |  7,003.57 μs |  6,987.93 μs | 218.7500 | 210.9375 | 54.6875 | 1547645 B |
| B_EFCore_ToList_Dto                | Default         | 16           | B_BufferedDto                   | 10,053.18 μs |  9,997.79 μs | 515.6250 | 312.5000 | 78.1250 | 3794208 B |
|                                    |                 |              |                                 |              |              |          |          |         |           |
| B_Nextorm_Cached_AsyncStream_Dto   | Default         | 16           | B_UnbufferedDto                 |  5,489.86 μs |  5,473.85 μs | 148.4375 |        - |       - | 1284226 B |
| B_Linq2Db_AsyncStream_Dto          | Default         | 16           | B_UnbufferedDto                 |  5,632.32 μs |  5,586.93 μs | 148.4375 |        - |       - | 1284642 B |
| B_EFCore_AsyncStream_Dto           | Default         | 16           | B_UnbufferedDto                 |  6,309.38 μs |  6,283.16 μs | 421.8750 |   7.8125 |       - | 3531154 B |
|                                    |                 |              |                                 |              |              |          |          |         |           |
| A_Nextorm_Prepared_ToDataReader    | 1               | 1            | A_RawReader                     |  5,302.54 μs |  5,083.15 μs |        - |        - |       - |  972072 B |
|                                    |                 |              |                                 |              |              |          |          |         |           |
| Linq2Db_ToDataReader               | 1               | 1            | B_RawReader                     |  4,204.47 μs |  4,204.19 μs |        - |        - |       - |  966296 B |
| Provider_Direct_ToDataReader       | 1               | 1            | B_RawReader                     |  4,258.22 μs |  4,243.64 μs |        - |        - |       - |  966032 B |
| Dapper_ToDataReader                | 1               | 1            | B_RawReader                     |  4,597.43 μs |  4,500.05 μs |        - |        - |       - |  966064 B |
| B_Nextorm_ToDataReader             | 1               | 1            | B_RawReader                     |  5,008.46 μs |  4,825.69 μs |        - |        - |       - |  974752 B |
|                                    |                 |              |                                 |              |              |          |          |         |           |
| Provider_Direct_ToDataReader_Empty | 1               | 1            | B_RawReader_Empty               |     79.50 μs |     77.19 μs |        - |        - |       - |         - |
| Dapper_ToDataReader_Empty          | 1               | 1            | B_RawReader_Empty               |     92.57 μs |     89.70 μs |        - |        - |       - |         - |
| Linq2Db_ToDataReader_Empty         | 1               | 1            | B_RawReader_Empty               |     92.69 μs |     90.17 μs |        - |        - |       - |         - |
| B_Nextorm_ToDataReader_Empty       | 1               | 1            | B_RawReader_Empty               |    220.89 μs |    220.11 μs |        - |        - |       - |   17208 B |
