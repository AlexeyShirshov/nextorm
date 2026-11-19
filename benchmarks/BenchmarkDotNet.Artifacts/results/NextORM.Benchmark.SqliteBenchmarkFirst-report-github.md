```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.30GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev    | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|----------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  74.11 μs |  15.39 μs |  0.844 μs |  0.4883 |      - |   8.55 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        |  79.55 μs |  18.00 μs |  0.987 μs |  0.7324 |      - |  12.77 KB |
| Dapper_Scalar_FirstOrDefault                  |  94.52 μs |  28.22 μs |  1.547 μs |  0.9766 |      - |  16.12 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault |  97.66 μs |  15.74 μs |  0.863 μs |  1.9531 |      - |  34.33 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 101.32 μs |  23.55 μs |  1.291 μs |  3.0518 |      - |  51.08 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 102.24 μs |  13.65 μs |  0.748 μs |  2.6855 |      - |  45.06 KB |
| Dapper_Entity_FirstOrDefault                  | 104.45 μs |  11.61 μs |  0.637 μs |  1.0986 |      - |  19.17 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 150.02 μs |  25.05 μs |  1.373 μs |  9.7656 |      - | 159.77 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 179.01 μs | 233.04 μs | 12.774 μs | 10.4980 |      - | 172.59 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 191.92 μs | 195.84 μs | 10.735 μs | 11.7188 |      - | 192.96 KB |
| Linq2Db_Entity_FirstOrDefault                 | 194.28 μs | 137.08 μs |  7.514 μs | 11.7188 |      - | 191.98 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 200.65 μs | 123.21 μs |  6.754 μs |  4.6387 | 2.1973 |  76.02 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 206.71 μs |  21.52 μs |  1.179 μs |  4.6387 | 2.1973 |  79.42 KB |
