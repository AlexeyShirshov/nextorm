```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  87.67 μs |  3.304 μs | 0.181 μs |  0.4883 |      - |   8.63 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        |  97.88 μs |  1.398 μs | 0.077 μs |  0.7324 |      - |  12.85 KB |
| Dapper_Scalar_FirstOrDefault                  | 121.11 μs |  2.198 μs | 0.120 μs |  0.9766 |      - |  16.48 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 123.48 μs |  5.713 μs | 0.313 μs |  1.9531 |      - |  35.46 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 129.78 μs |  8.880 μs | 0.487 μs |  3.1738 |      - |  54.99 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 130.24 μs |  2.893 μs | 0.159 μs |  2.6855 |      - |  46.71 KB |
| Dapper_Entity_FirstOrDefault                  | 134.14 μs |  6.232 μs | 0.342 μs |  0.9766 |      - |  19.24 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 220.07 μs |  3.359 μs | 0.184 μs | 11.4746 |      - | 187.58 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 242.83 μs | 19.293 μs | 1.057 μs |  4.8828 | 2.4414 |   80.4 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 251.38 μs |  5.532 μs | 0.303 μs | 12.2070 |      - | 201.35 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 263.05 μs |  4.237 μs | 0.232 μs |  4.8828 | 2.4414 |  83.79 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 331.71 μs | 18.466 μs | 1.012 μs | 13.1836 |      - | 221.79 KB |
| Linq2Db_Entity_FirstOrDefault                 | 345.08 μs | 11.899 μs | 0.652 μs | 13.1836 |      - | 220.81 KB |
