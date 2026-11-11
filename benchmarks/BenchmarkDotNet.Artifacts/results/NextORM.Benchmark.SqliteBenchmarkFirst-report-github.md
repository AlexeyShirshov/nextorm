```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  93.64 μs | 14.297 μs | 0.784 μs |  0.4883 |      - |   8.56 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 105.01 μs |  9.787 μs | 0.536 μs |  0.7324 |      - |  12.77 KB |
| Dapper_Scalar_FirstOrDefault                  | 148.96 μs | 60.731 μs | 3.329 μs |  0.9766 |      - |  16.12 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 151.69 μs | 18.221 μs | 0.999 μs |  1.9531 |      - |  33.98 KB |
| Dapper_Entity_FirstOrDefault                  | 168.56 μs | 17.955 μs | 0.984 μs |  0.9766 |      - |  19.17 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 176.87 μs | 11.876 μs | 0.651 μs |  2.4414 |      - |  43.66 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 180.06 μs | 11.241 μs | 0.616 μs |  2.9297 |      - |  50.69 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 363.31 μs |  3.289 μs | 0.180 μs |  9.7656 |      - | 159.77 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 368.44 μs | 16.199 μs | 0.888 μs |  4.3945 | 1.9531 |  76.03 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 410.61 μs | 32.760 μs | 1.796 μs |  4.3945 | 1.9531 |  79.42 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 424.58 μs | 64.380 μs | 3.529 μs | 10.2539 |      - |  172.6 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 622.34 μs | 58.995 μs | 3.234 μs | 11.7188 |      - | 192.97 KB |
| Linq2Db_Entity_FirstOrDefault                 | 634.97 μs | 51.233 μs | 2.808 μs | 11.7188 |      - | 191.99 KB |
