```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.94GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  94.47 μs |  5.899 μs | 0.323 μs |  0.4883 |      - |   8.56 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 105.26 μs | 12.071 μs | 0.662 μs |  0.7324 |      - |  12.77 KB |
| Dapper_Scalar_FirstOrDefault                  | 143.47 μs | 10.591 μs | 0.581 μs |  0.9766 |      - |  16.12 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 161.66 μs | 10.905 μs | 0.598 μs |  1.9531 |      - |  34.22 KB |
| Dapper_Entity_FirstOrDefault                  | 164.55 μs | 17.005 μs | 0.932 μs |  0.9766 |      - |  19.17 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 177.99 μs | 11.466 μs | 0.628 μs |  2.6855 |      - |  43.97 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 183.55 μs | 23.989 μs | 1.315 μs |  2.9297 |      - |  51.08 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 361.97 μs | 43.443 μs | 2.381 μs |  9.7656 |      - | 159.77 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 363.39 μs | 68.742 μs | 3.768 μs |  4.3945 | 1.9531 |  76.03 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 406.74 μs | 33.318 μs | 1.826 μs |  4.3945 | 1.9531 |  79.42 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 429.14 μs | 47.324 μs | 2.594 μs | 10.2539 |      - |  172.6 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 619.93 μs | 45.712 μs | 2.506 μs | 11.7188 |      - | 192.97 KB |
| Linq2Db_Entity_FirstOrDefault                 | 626.50 μs | 31.514 μs | 1.727 μs | 11.7188 |      - | 191.99 KB |
