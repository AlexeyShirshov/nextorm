```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev   | Gen0   | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  87.70 μs |  6.125 μs | 0.336 μs | 0.4883 |      - |   4.52 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 100.49 μs |  8.661 μs | 0.475 μs | 0.9766 |      - |   8.54 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 135.25 μs | 31.454 μs | 1.724 μs | 2.9297 |      - |  24.66 KB |
| Dapper_Scalar_FirstOrDefault                  | 139.09 μs | 11.030 μs | 0.605 μs | 1.9531 |      - |  16.48 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 146.20 μs | 42.287 μs | 2.318 μs | 4.8828 |      - |  40.79 KB |
| Dapper_Entity_FirstOrDefault                  | 156.34 μs | 12.211 μs | 0.669 μs | 2.1973 |      - |  19.24 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 165.57 μs | 70.287 μs | 3.853 μs | 5.1270 |      - |  43.17 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 338.88 μs | 66.693 μs | 3.656 μs | 9.7656 | 4.8828 |  79.81 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 380.47 μs | 46.084 μs | 2.526 μs | 9.7656 | 4.8828 |   83.2 KB |
