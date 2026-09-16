```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error      | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|-----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  94.77 μs |   6.040 μs | 0.331 μs |  0.9766 |      - |   8.56 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 105.55 μs |  14.079 μs | 0.772 μs |  1.3428 |      - |  11.59 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 146.06 μs |  14.483 μs | 0.794 μs |  3.1738 |      - |  27.39 KB |
| Dapper_Scalar_FirstOrDefault                  | 146.92 μs |   8.213 μs | 0.450 μs |  1.9531 |      - |  16.48 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 159.12 μs |  16.112 μs | 0.883 μs |  5.3711 |      - |  44.06 KB |
| Dapper_Entity_FirstOrDefault                  | 166.79 μs |  39.154 μs | 2.146 μs |  2.1973 |      - |  19.24 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 176.94 μs |  10.092 μs | 0.553 μs |  5.3711 |      - |  45.44 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 359.09 μs | 113.930 μs | 6.245 μs |  9.7656 | 4.8828 |   80.4 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 402.82 μs |  47.011 μs | 2.577 μs | 10.2539 | 4.8828 |  83.79 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 674.83 μs |  82.825 μs | 4.540 μs | 26.3672 |      - | 222.27 KB |
| Linq2Db_Entity_FirstOrDefault                 | 704.08 μs | 161.258 μs | 8.839 μs | 26.3672 |      - | 222.07 KB |
