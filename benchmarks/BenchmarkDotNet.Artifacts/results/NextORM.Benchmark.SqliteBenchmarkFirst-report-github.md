```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                                        | Mean     | Error   | StdDev  | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |---------:|--------:|--------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        | 107.1 μs | 0.62 μs | 0.52 μs |  0.9766 |      - |   8.56 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 119.3 μs | 2.35 μs | 2.42 μs |  1.4648 |      - |  12.77 KB |
| Dapper_Scalar_FirstOrDefault                  | 154.6 μs | 2.63 μs | 2.46 μs |  1.9531 |      - |  16.12 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 169.8 μs | 2.56 μs | 2.40 μs |  4.1504 |      - |  34.36 KB |
| Dapper_Entity_FirstOrDefault                  | 173.1 μs | 1.19 μs | 1.05 μs |  2.1973 |      - |  19.17 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 189.5 μs | 1.85 μs | 1.55 μs |  6.1035 |      - |  51.08 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 196.5 μs | 3.24 μs | 3.03 μs |  5.3711 |      - |  43.97 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 355.7 μs | 4.72 μs | 4.18 μs |  9.2773 | 4.3945 |  76.03 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 366.3 μs | 5.56 μs | 4.93 μs | 19.5313 |      - | 159.77 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 391.0 μs | 7.40 μs | 7.60 μs |  9.2773 | 4.3945 |  79.42 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 424.9 μs | 3.41 μs | 2.84 μs | 20.9961 |      - |  172.6 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 598.1 μs | 4.01 μs | 3.35 μs | 23.4375 |      - | 192.97 KB |
| Linq2Db_Entity_FirstOrDefault                 | 601.2 μs | 3.67 μs | 3.25 μs | 23.4375 |      - | 191.99 KB |
