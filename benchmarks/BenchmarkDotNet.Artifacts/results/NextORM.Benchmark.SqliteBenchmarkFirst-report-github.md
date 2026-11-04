```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  91.35 μs | 11.643 μs | 0.638 μs |  0.9766 |      - |   8.63 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 101.19 μs |  3.152 μs | 0.173 μs |  1.4648 |      - |  12.22 KB |
| Dapper_Scalar_FirstOrDefault                  | 146.41 μs | 25.317 μs | 1.388 μs |  1.9531 |      - |  16.48 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 165.09 μs | 84.321 μs | 4.622 μs |  3.9063 |      - |  33.63 KB |
| Dapper_Entity_FirstOrDefault                  | 165.51 μs | 25.601 μs | 1.403 μs |  2.1973 |      - |  19.24 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 169.01 μs | 10.850 μs | 0.595 μs |  6.1035 |      - |  50.77 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 178.27 μs | 13.756 μs | 0.754 μs |  5.1270 |      - |  43.42 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 351.03 μs | 61.211 μs | 3.355 μs |  9.7656 | 4.8828 |   80.4 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 388.41 μs | 41.216 μs | 2.259 μs | 22.9492 |      - | 187.58 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 393.59 μs | 83.003 μs | 4.550 μs | 10.2539 | 4.8828 |  83.79 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 439.41 μs | 21.376 μs | 1.172 μs | 24.4141 |      - | 201.35 KB |
| Linq2Db_Entity_FirstOrDefault                 | 646.36 μs | 36.314 μs | 1.990 μs | 26.3672 |      - | 220.82 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 647.67 μs | 71.540 μs | 3.921 μs | 26.3672 |      - |  222.5 KB |
