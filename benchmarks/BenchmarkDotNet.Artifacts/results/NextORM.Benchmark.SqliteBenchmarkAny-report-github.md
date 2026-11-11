```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error       | StdDev    | Gen0     | Gen1    | Allocated  |
|----------------- |----------- |-----------:|------------:|----------:|---------:|--------:|-----------:|
| Nextorm_Prepared |            |   945.5 μs |   199.67 μs |  10.94 μs |   9.7656 |       - |   84.38 KB |
| Nextorm_Cached   | acceptance | 1,226.3 μs |    83.31 μs |   4.57 μs |  23.4375 |  5.8594 |  193.78 KB |
| Dapper           |            | 1,443.5 μs |   397.30 μs |  21.78 μs |  15.6250 |       - |  138.29 KB |
| Linq2Db_Compiled |            | 1,526.2 μs |   271.99 μs |  14.91 μs |  27.3438 |       - |  232.82 KB |
| Linq2Db          |            | 2,574.7 μs | 2,705.84 μs | 148.32 μs |  46.8750 |       - |  400.02 KB |
| EFCore_Compiled  |            | 3,401.7 μs |   416.93 μs |  22.85 μs |  89.8438 | 42.9688 |   763.3 KB |
| EFCore           |            | 5,651.7 μs | 1,191.79 μs |  65.33 μs | 140.6250 | 62.5000 | 1163.38 KB |
