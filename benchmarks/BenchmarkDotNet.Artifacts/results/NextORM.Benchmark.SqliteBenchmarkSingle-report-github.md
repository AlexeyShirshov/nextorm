```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|-----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  99.09 μs |  30.036 μs |  1.646 μs |  1.00 |    0.02 |  0.9766 |      - |   8.28 KB |        1.00 |
| Dapper_SingleOrDefault           | 140.28 μs |  10.710 μs |  0.587 μs |  1.42 |    0.02 |  1.9531 |      - |  16.04 KB |        1.94 |
| Nextorm_Cached_SingleOrDefault   | 168.39 μs |   4.196 μs |  0.230 μs |  1.70 |    0.02 |  5.8594 |      - |  50.79 KB |        6.13 |
| EFCore_Compiled_SingleOrDefault  | 350.29 μs |  64.638 μs |  3.543 μs |  3.54 |    0.06 |  9.2773 | 4.3945 |  76.03 KB |        9.18 |
| Linq2Db_Compiled_SingleOrDefault | 361.22 μs |  41.553 μs |  2.278 μs |  3.65 |    0.06 | 19.5313 |      - | 159.77 KB |       19.29 |
| Linq2Db_SingleOrDefault          | 579.88 μs | 126.169 μs |  6.916 μs |  5.85 |    0.10 | 23.4375 |      - | 192.96 KB |       23.30 |
| EFCore_SingleOrDefault           | 681.23 μs | 480.785 μs | 26.353 μs |  6.88 |    0.25 | 17.5781 | 5.8594 | 148.32 KB |       17.91 |
