```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                                        | Mean     | Error   | StdDev  | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |---------:|--------:|--------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        | 107.0 μs | 0.57 μs | 0.50 μs |  0.9766 |      - |   8.56 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 116.5 μs | 0.59 μs | 0.55 μs |  1.4648 |      - |  12.77 KB |
| Dapper_Scalar_FirstOrDefault                  | 150.8 μs | 1.09 μs | 1.02 μs |  1.9531 |      - |  16.12 KB |
| Dapper_Entity_FirstOrDefault                  | 173.6 μs | 1.82 μs | 1.52 μs |  2.1973 |      - |  19.17 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 176.7 μs | 1.53 μs | 1.35 μs |  4.1504 |      - |  33.99 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 189.4 μs | 0.38 μs | 0.35 μs |  6.1035 |      - |  50.61 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 192.5 μs | 1.01 μs | 0.84 μs |  5.1270 |      - |  43.74 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 344.6 μs | 2.16 μs | 1.80 μs |  9.2773 | 4.3945 |  76.03 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 368.8 μs | 4.00 μs | 3.54 μs | 19.5313 |      - | 159.77 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 382.8 μs | 1.29 μs | 1.00 μs |  9.2773 | 4.3945 |  79.42 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 430.5 μs | 5.57 μs | 5.21 μs | 20.9961 |      - |  172.6 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 611.1 μs | 6.90 μs | 6.46 μs | 23.4375 |      - | 192.97 KB |
| Linq2Db_Entity_FirstOrDefault                 | 612.2 μs | 5.92 μs | 5.25 μs | 23.4375 |      - | 191.99 KB |
