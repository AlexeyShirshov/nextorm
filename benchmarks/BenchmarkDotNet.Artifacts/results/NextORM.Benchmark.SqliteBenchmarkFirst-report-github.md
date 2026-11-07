```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean     | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |---------:|----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        | 103.8 μs |   7.74 μs |  0.42 μs |  0.9766 |      - |   8.63 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 113.6 μs |   8.10 μs |  0.44 μs |  1.4648 |      - |  12.85 KB |
| Dapper_Scalar_FirstOrDefault                  | 165.9 μs |  22.09 μs |  1.21 μs |  1.9531 |      - |  16.48 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 173.8 μs |   6.48 μs |  0.36 μs |  4.1504 |      - |  35.46 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 190.8 μs |  20.65 μs |  1.13 μs |  5.6152 |      - |  46.71 KB |
| Dapper_Entity_FirstOrDefault                  | 191.1 μs | 154.02 μs |  8.44 μs |  2.1973 |      - |  19.24 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 194.2 μs |  69.72 μs |  3.82 μs |  6.5918 |      - |  54.99 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 401.6 μs | 106.31 μs |  5.83 μs |  9.7656 | 4.8828 |   80.4 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 421.8 μs | 198.58 μs | 10.88 μs | 22.9492 |      - | 187.58 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 445.4 μs | 360.42 μs | 19.76 μs | 10.2539 | 4.8828 |  83.79 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 478.7 μs |  77.88 μs |  4.27 μs | 24.4141 |      - | 201.35 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 667.4 μs | 336.66 μs | 18.45 μs | 26.3672 |      - | 221.01 KB |
| Linq2Db_Entity_FirstOrDefault                 | 683.2 μs |  88.62 μs |  4.86 μs | 26.3672 |      - | 221.99 KB |
