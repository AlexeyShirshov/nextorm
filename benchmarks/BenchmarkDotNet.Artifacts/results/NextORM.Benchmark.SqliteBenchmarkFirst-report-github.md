```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                                        | Mean      | Error     | StdDev    | Median    | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|----------:|----------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  91.94 μs |  0.537 μs |  0.476 μs |  91.76 μs |  0.9766 |      - |   8.63 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 102.39 μs |  1.114 μs |  1.042 μs | 102.10 μs |  1.4648 |      - |  12.22 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 143.07 μs |  1.417 μs |  1.326 μs | 142.77 μs |  3.9063 |      - |  35.17 KB |
| Dapper_Scalar_FirstOrDefault                  | 148.21 μs |  2.921 μs |  3.587 μs | 147.33 μs |  1.9531 |      - |  16.48 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 162.58 μs |  3.228 μs |  4.198 μs | 160.95 μs |  7.8125 |      - |  64.28 KB |
| Dapper_Entity_FirstOrDefault                  | 164.44 μs |  3.064 μs |  5.679 μs | 161.82 μs |  2.1973 |      - |  19.24 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 174.87 μs |  1.800 μs |  1.596 μs | 174.43 μs |  6.8359 |      - |  60.53 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 346.23 μs |  6.867 μs |  9.399 μs | 344.41 μs |  9.7656 | 4.8828 |   80.4 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 374.76 μs |  7.284 μs |  8.671 μs | 371.39 μs | 22.9492 |      - | 187.58 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 382.43 μs |  6.878 μs |  6.434 μs | 380.61 μs | 10.2539 | 4.8828 |  83.79 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 434.78 μs |  8.651 μs | 13.969 μs | 430.18 μs | 24.4141 |      - | 201.34 KB |
| Linq2Db_Entity_FirstOrDefault                 | 598.52 μs | 11.847 μs | 14.549 μs | 593.28 μs | 23.4375 |      - | 220.81 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 607.65 μs | 11.176 μs |  9.907 μs | 606.09 μs | 23.4375 |      - | 221.01 KB |
