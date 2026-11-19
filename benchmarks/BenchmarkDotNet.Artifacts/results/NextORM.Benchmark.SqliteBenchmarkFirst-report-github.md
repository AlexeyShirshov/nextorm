```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.20GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  95.27 μs |  4.508 μs | 0.247 μs |  0.4883 |      - |   8.56 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 108.79 μs | 11.038 μs | 0.605 μs |  0.7324 |      - |  12.77 KB |
| Dapper_Scalar_FirstOrDefault                  | 141.48 μs | 20.643 μs | 1.131 μs |  0.9766 |      - |  16.12 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 165.96 μs | 38.488 μs | 2.110 μs |  1.9531 |      - |  34.22 KB |
| Dapper_Entity_FirstOrDefault                  | 173.91 μs |  9.111 μs | 0.499 μs |  0.9766 |      - |  19.17 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 178.65 μs | 30.069 μs | 1.648 μs |  2.9297 |      - |  51.08 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 186.98 μs | 22.739 μs | 1.246 μs |  2.6855 |      - |  43.97 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 373.45 μs | 25.625 μs | 1.405 μs |  4.3945 | 1.9531 |  76.03 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 383.95 μs | 83.717 μs | 4.589 μs |  9.7656 |      - | 159.77 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 410.94 μs | 26.193 μs | 1.436 μs |  4.3945 | 1.9531 |  79.42 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 458.94 μs | 47.252 μs | 2.590 μs | 10.2539 |      - |  172.6 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 648.40 μs | 69.855 μs | 3.829 μs | 11.7188 |      - | 192.97 KB |
| Linq2Db_Entity_FirstOrDefault                 | 656.64 μs |  9.393 μs | 0.515 μs | 11.7188 |      - | 191.99 KB |
