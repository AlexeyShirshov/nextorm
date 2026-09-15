```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  86.75 μs |  5.311 μs | 0.291 μs |  1.00 |    0.00 |  0.4883 |      - |    4.3 KB |        1.00 |
| Dapper_SingleOrDefault           | 135.49 μs | 26.494 μs | 1.452 μs |  1.56 |    0.02 |  1.9531 |      - |   16.4 KB |        3.82 |
| Nextorm_Cached_SingleOrDefault   | 137.49 μs | 46.721 μs | 2.561 μs |  1.58 |    0.03 |  4.8828 |      - |  40.58 KB |        9.44 |
| EFCore_Compiled_SingleOrDefault  | 333.16 μs | 69.313 μs | 3.799 μs |  3.84 |    0.04 |  9.7656 | 4.8828 |  79.81 KB |       18.57 |
| EFCore_SingleOrDefault           | 667.98 μs | 55.614 μs | 3.048 μs |  7.70 |    0.04 | 17.5781 | 5.8594 | 149.05 KB |       34.69 |
