```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error      | StdDev    | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |----------:|-----------:|----------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  96.31 μs |   5.374 μs |  0.295 μs |  0.9766 |      - |   8.56 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 108.99 μs |  36.231 μs |  1.986 μs |  1.4648 |      - |  12.77 KB |
| Dapper_Scalar_FirstOrDefault                  | 140.89 μs |  22.160 μs |  1.215 μs |  1.9531 |      - |  16.12 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 160.94 μs |  71.307 μs |  3.909 μs |  4.1504 |      - |  33.98 KB |
| Dapper_Entity_FirstOrDefault                  | 166.43 μs |  44.655 μs |  2.448 μs |  2.1973 |      - |  19.17 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 179.36 μs |  24.085 μs |  1.320 μs |  5.1270 |      - |  43.66 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 187.59 μs |  55.479 μs |  3.041 μs |  6.1035 |      - |  50.69 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 356.68 μs |  48.749 μs |  2.672 μs | 19.5313 |      - | 159.77 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 365.25 μs | 189.192 μs | 10.370 μs |  9.2773 | 4.3945 |  76.03 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 427.89 μs | 481.056 μs | 26.368 μs |  9.2773 | 4.3945 |  79.42 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 468.24 μs | 982.511 μs | 53.855 μs | 20.9961 |      - | 172.59 KB |
| Linq2Db_Entity_FirstOrDefault                 | 592.16 μs | 182.618 μs | 10.010 μs | 23.4375 |      - | 191.99 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 597.39 μs |  85.113 μs |  4.665 μs | 23.4375 |      - | 192.96 KB |
