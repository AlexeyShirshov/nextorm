```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                             | InvocationCount | UnrollFactor | Categories                      | Mean        | Gen0     | Gen1     | Gen2    | Allocated |
|----------------------------------- |---------------- |------------- |-------------------------------- |------------:|---------:|---------:|--------:|----------:|
| A_Nextorm_Prepared_ToList_Dto      | Default         | 16           | A_BufferedDto                   | 4,119.13 μs | 156.2500 | 109.3750 |       - | 1360688 B |
| Linq2Db_Compiled_ToList_Dto        | Default         | 16           | A_BufferedDto                   | 5,720.83 μs | 218.7500 | 210.9375 | 54.6875 | 1544457 B |
| A_EFCore_Compiled_ToList_Dto       | Default         | 16           | A_BufferedDto                   | 8,678.39 μs | 515.6250 | 312.5000 | 78.1250 | 3789917 B |
|                                    |                 |              |                                 |             |          |          |         |           |
| Dapper_ToList_Dto                  | Default         | 16           | A_BufferedDto,B_BufferedDto     | 6,793.55 μs | 257.8125 | 250.0000 | 62.5000 | 1784191 B |
|                                    |                 |              |                                 |             |          |          |         |           |
| A_Nextorm_Prepared_AsyncStream_Dto | Default         | 16           | A_UnbufferedDto                 | 4,115.10 μs | 148.4375 |        - |       - | 1280890 B |
| A_EFCore_Compiled_AsyncStream_Dto  | Default         | 16           | A_UnbufferedDto                 | 5,106.04 μs | 414.0625 |        - |       - | 3527002 B |
|                                    |                 |              |                                 |             |          |          |         |           |
| Dapper_AsyncStream_Dto             | Default         | 16           | A_UnbufferedDto,B_UnbufferedDto | 4,655.43 μs | 179.6875 |        - |       - | 1521474 B |
|                                    |                 |              |                                 |             |          |          |         |           |
| B_Nextorm_Cached_ToList_Dto        | Default         | 16           | B_BufferedDto                   | 4,165.29 μs | 156.2500 | 109.3750 |       - | 1362858 B |
| B_Linq2Db_ToList_Dto               | Default         | 16           | B_BufferedDto                   | 5,937.36 μs | 218.7500 | 210.9375 | 54.6875 | 1547617 B |
| B_EFCore_ToList_Dto                | Default         | 16           | B_BufferedDto                   | 8,780.73 μs | 515.6250 | 312.5000 | 78.1250 | 3794231 B |
|                                    |                 |              |                                 |             |          |          |         |           |
| B_Nextorm_Cached_AsyncStream_Dto   | Default         | 16           | B_UnbufferedDto                 | 4,542.01 μs | 148.4375 |        - |       - | 1284226 B |
| B_Linq2Db_AsyncStream_Dto          | Default         | 16           | B_UnbufferedDto                 | 4,660.90 μs | 148.4375 |        - |       - | 1284642 B |
| B_EFCore_AsyncStream_Dto           | Default         | 16           | B_UnbufferedDto                 | 5,159.78 μs | 421.8750 |   7.8125 |       - | 3531154 B |
|                                    |                 |              |                                 |             |          |          |         |           |
| A_Nextorm_Prepared_ToDataReader    | 1               | 1            | A_RawReader                     | 4,340.06 μs |        - |        - |       - |  972152 B |
|                                    |                 |              |                                 |             |          |          |         |           |
| Linq2Db_ToDataReader               | 1               | 1            | B_RawReader                     | 3,682.80 μs |        - |        - |       - |  966296 B |
| Provider_Direct_ToDataReader       | 1               | 1            | B_RawReader                     | 3,708.38 μs |        - |        - |       - |  966032 B |
| Dapper_ToDataReader                | 1               | 1            | B_RawReader                     | 3,770.33 μs |        - |        - |       - |  966064 B |
| B_Nextorm_ToDataReader             | 1               | 1            | B_RawReader                     | 4,190.48 μs |        - |        - |       - |  974752 B |
|                                    |                 |              |                                 |             |          |          |         |           |
| Provider_Direct_ToDataReader_Empty | 1               | 1            | B_RawReader_Empty               |    82.39 μs |        - |        - |       - |         - |
| Dapper_ToDataReader_Empty          | 1               | 1            | B_RawReader_Empty               |    92.60 μs |        - |        - |       - |         - |
| Linq2Db_ToDataReader_Empty         | 1               | 1            | B_RawReader_Empty               |    92.66 μs |        - |        - |       - |         - |
| B_Nextorm_ToDataReader_Empty       | 1               | 1            | B_RawReader_Empty               |   225.84 μs |        - |        - |       - |   17208 B |
