```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.68GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  86.57 μs |  3.443 μs | 0.189 μs |  1.00 |    0.00 |  0.4883 |      - |   8.36 KB |        1.00 |
| Dapper_SingleOrDefault           | 118.70 μs |  5.306 μs | 0.291 μs |  1.37 |    0.00 |  0.9766 |      - |   16.4 KB |        1.96 |
| Nextorm_Cached_SingleOrDefault   | 127.84 μs | 12.691 μs | 0.696 μs |  1.48 |    0.01 |  2.9297 |      - |  54.85 KB |        6.56 |
| Linq2Db_Compiled_SingleOrDefault | 228.30 μs | 27.374 μs | 1.500 μs |  2.64 |    0.02 | 11.4746 |      - | 187.58 KB |       22.44 |
| EFCore_Compiled_SingleOrDefault  | 246.69 μs | 39.981 μs | 2.192 μs |  2.85 |    0.02 |  4.8828 | 2.4414 |   80.4 KB |        9.62 |
| Linq2Db_SingleOrDefault          | 331.85 μs |  4.889 μs | 0.268 μs |  3.83 |    0.01 | 13.1836 |      - | 221.01 KB |       26.44 |
| EFCore_SingleOrDefault           | 396.09 μs | 47.425 μs | 2.600 μs |  4.58 |    0.03 |  8.7891 | 4.3945 | 149.64 KB |       17.90 |
