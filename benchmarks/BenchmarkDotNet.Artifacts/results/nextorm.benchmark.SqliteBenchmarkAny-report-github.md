```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Mean       | Error     | StdDev   | Gen0     | Gen1    | Allocated  |
|----------------- |-----------:|----------:|---------:|---------:|--------:|-----------:|
| Nextorm_Prepared |   943.2 μs |  47.11 μs |  2.58 μs |   9.7656 |       - |   85.16 KB |
| Dapper           | 1,401.8 μs | 315.67 μs | 17.30 μs |  15.6250 |       - |  139.08 KB |
| Nextorm_Cached   | 1,438.4 μs |  77.61 μs |  4.25 μs |  42.9688 |       - |  352.38 KB |
| Linq2Db          | 2,805.2 μs | 429.20 μs | 23.53 μs |  46.8750 |       - |  401.59 KB |
| EFCore_Compiled  | 3,653.8 μs | 521.93 μs | 28.61 μs |  97.6563 | 46.8750 |  807.06 KB |
| EFCore           | 6,786.3 μs | 368.19 μs | 20.18 μs | 148.4375 | 46.8750 | 1223.52 KB |
