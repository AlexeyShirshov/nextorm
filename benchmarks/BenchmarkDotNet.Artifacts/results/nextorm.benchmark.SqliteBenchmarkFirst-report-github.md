```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                                        | Mean      | Error     | StdDev    | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|----------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  92.74 μs |  0.731 μs |  0.684 μs |  0.9766 |      - |   8.56 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 103.15 μs |  0.664 μs |  0.621 μs |  1.3428 |      - |  11.59 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 132.05 μs |  2.230 μs |  2.086 μs |  2.9297 |      - |  27.81 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 138.15 μs |  1.067 μs |  0.891 μs |  5.3711 |      - |  44.37 KB |
| Dapper_Scalar_FirstOrDefault                  | 145.83 μs |  1.057 μs |  0.989 μs |  1.9531 |      - |  16.48 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 155.81 μs |  1.716 μs |  1.433 μs |  4.8828 |      - |  45.75 KB |
| Dapper_Entity_FirstOrDefault                  | 161.98 μs |  1.284 μs |  1.201 μs |  2.1973 |      - |  19.24 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 348.44 μs |  5.069 μs |  4.742 μs |  9.7656 | 4.8828 |   80.4 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 388.22 μs |  3.121 μs |  2.606 μs | 10.2539 | 4.8828 |  83.79 KB |
| Linq2Db_Entity_FirstOrDefault                 | 606.78 μs |  8.111 μs |  7.587 μs | 23.4375 |      - | 220.81 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 607.75 μs | 10.838 μs | 10.138 μs | 23.4375 |      - | 221.01 KB |
