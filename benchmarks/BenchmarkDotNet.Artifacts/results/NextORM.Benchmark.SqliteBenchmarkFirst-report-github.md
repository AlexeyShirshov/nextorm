```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  87.72 μs |  4.017 μs | 0.220 μs |  0.4883 |      - |   8.63 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        |  96.90 μs |  2.717 μs | 0.149 μs |  0.7324 |      - |  12.85 KB |
| Dapper_Scalar_FirstOrDefault                  | 118.99 μs |  2.808 μs | 0.154 μs |  0.9766 |      - |  16.48 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 122.28 μs |  5.331 μs | 0.292 μs |  1.9531 |      - |  34.91 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 129.96 μs |  3.547 μs | 0.194 μs |  3.1738 |      - |  54.28 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 130.57 μs | 26.405 μs | 1.447 μs |  2.6855 |      - |  46.08 KB |
| Dapper_Entity_FirstOrDefault                  | 132.75 μs |  8.808 μs | 0.483 μs |  0.9766 |      - |  19.24 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 223.82 μs | 10.788 μs | 0.591 μs | 11.4746 |      - | 187.58 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 246.84 μs |  4.508 μs | 0.247 μs |  4.8828 | 2.4414 |   80.4 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 256.70 μs | 20.592 μs | 1.129 μs | 12.2070 |      - | 201.35 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 268.07 μs | 11.248 μs | 0.617 μs |  4.8828 | 2.4414 |  83.79 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 334.40 μs | 21.100 μs | 1.157 μs | 13.1836 |      - | 221.01 KB |
| Linq2Db_Entity_FirstOrDefault                 | 349.05 μs |  8.616 μs | 0.472 μs | 13.1836 |      - | 220.81 KB |
