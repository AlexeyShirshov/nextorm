```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |---------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault | 102.7 μs |   5.58 μs |  0.31 μs |  1.00 |    0.00 |  0.9766 |      - |   8.36 KB |        1.00 |
| Dapper_SingleOrDefault           | 157.2 μs |  17.40 μs |  0.95 μs |  1.53 |    0.01 |  1.9531 |      - |   16.4 KB |        1.96 |
| Nextorm_Cached_SingleOrDefault   | 200.2 μs | 127.37 μs |  6.98 μs |  1.95 |    0.06 |  5.8594 |      - |  55.55 KB |        6.65 |
| EFCore_Compiled_SingleOrDefault  | 401.2 μs | 641.57 μs | 35.17 μs |  3.91 |    0.30 |  9.7656 | 4.8828 |   80.4 KB |        9.62 |
| Linq2Db_Compiled_SingleOrDefault | 407.9 μs |  51.67 μs |  2.83 μs |  3.97 |    0.03 | 22.9492 |      - | 187.58 KB |       22.44 |
| Linq2Db_SingleOrDefault          | 652.2 μs |  76.80 μs |  4.21 μs |  6.35 |    0.04 | 26.3672 |      - | 221.01 KB |       26.44 |
| EFCore_SingleOrDefault           | 755.1 μs | 103.33 μs |  5.66 μs |  7.35 |    0.05 | 17.5781 | 5.8594 | 149.65 KB |       17.90 |
