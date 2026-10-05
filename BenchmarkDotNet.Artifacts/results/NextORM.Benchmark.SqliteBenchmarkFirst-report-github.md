```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                                        | Mean     | Error    | StdDev   | Median   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |---------:|---------:|---------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Entity_FirstOrDefault        | 105.6 μs |  1.15 μs |  1.02 μs | 105.5 μs |  1.4648 |      - |  12.22 KB |
| Nextorm_Prepared_Scalar_FirstOrDefault        | 123.4 μs |  2.46 μs |  5.02 μs | 122.7 μs |  0.9766 |      - |   8.63 KB |
| Dapper_Scalar_FirstOrDefault                  | 196.0 μs |  3.85 μs |  6.84 μs | 194.8 μs |  1.9531 |      - |  16.48 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 200.0 μs |  4.49 μs | 11.51 μs | 198.9 μs |  3.9063 |      - |  35.11 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 207.8 μs |  3.83 μs |  8.65 μs | 206.8 μs |  5.8594 |      - |  52.17 KB |
| Dapper_Entity_FirstOrDefault                  | 215.7 μs |  4.29 μs |  4.77 μs | 216.7 μs |  1.9531 |      - |  19.24 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 219.7 μs |  8.51 μs | 22.57 μs | 212.4 μs |  4.8828 |      - |  44.82 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 500.5 μs | 10.64 μs | 31.20 μs | 492.0 μs | 21.4844 |      - | 187.58 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 506.9 μs |  8.88 μs |  9.87 μs | 509.5 μs |  9.7656 | 3.9063 |   80.4 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 553.3 μs |  7.97 μs | 14.16 μs | 551.9 μs |  9.7656 | 3.9063 |  83.79 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 560.4 μs | 13.62 μs | 36.82 μs | 550.4 μs | 23.4375 |      - | 201.34 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 788.0 μs | 15.75 μs | 38.93 μs | 785.2 μs | 23.4375 |      - | 221.01 KB |
| Linq2Db_Entity_FirstOrDefault                 | 809.2 μs | 31.68 μs | 82.35 μs | 784.3 μs | 23.4375 |      - | 220.81 KB |
