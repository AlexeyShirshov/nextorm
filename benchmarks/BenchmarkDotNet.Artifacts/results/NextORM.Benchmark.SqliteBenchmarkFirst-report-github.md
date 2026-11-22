```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.09GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev   | Gen0   | Gen1   | Allocated |
|---------------------------------------------- |----------:|----------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        |  38.80 μs |  1.530 μs | 0.084 μs | 0.0610 |      - |   8.55 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        |  46.75 μs |  4.975 μs | 0.273 μs | 0.1221 |      - |  12.77 KB |
| Dapper_Scalar_FirstOrDefault                  |  64.78 μs |  1.991 μs | 0.109 μs | 0.1221 |      - |  16.12 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault |  70.00 μs | 12.152 μs | 0.666 μs | 0.3662 |      - |  34.23 KB |
| Nextorm_Cached_Entity_FirstOrDefault          |  74.34 μs | 13.332 μs | 0.731 μs | 0.4883 |      - |  44.05 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          |  74.94 μs | 10.495 μs | 0.575 μs | 0.6104 |      - |  51.16 KB |
| Dapper_Entity_FirstOrDefault                  |  80.28 μs |  3.614 μs | 0.198 μs | 0.1221 |      - |  19.16 KB |
| Linq2Db_Compiled_Scalar_FirstOrDefault        | 138.39 μs |  2.778 μs | 0.152 μs | 1.9531 |      - | 159.77 KB |
| Linq2Db_Compiled_Entity_FirstOrDefault        | 164.63 μs | 22.217 μs | 1.218 μs | 1.9531 |      - | 172.59 KB |
| Linq2Db_Scalar_FirstOrDefault                 | 194.86 μs | 14.739 μs | 0.808 μs | 2.1973 |      - | 192.96 KB |
| Linq2Db_Entity_FirstOrDefault                 | 199.45 μs | 13.357 μs | 0.732 μs | 2.1973 |      - | 191.99 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 201.20 μs | 13.019 μs | 0.714 μs | 0.7324 | 0.4883 |  76.02 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 221.70 μs |  4.635 μs | 0.254 μs | 0.7324 | 0.4883 |  79.42 KB |
