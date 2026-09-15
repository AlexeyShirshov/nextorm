```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Mean       | Error     | StdDev   | Gen0     | Gen1    | Allocated  |
|----------------- |-----------:|----------:|---------:|---------:|--------:|-----------:|
| Nextorm_Prepared |   866.1 μs |  71.13 μs |  3.90 μs |   4.8828 |       - |   41.41 KB |
| Nextorm_Cached   | 1,278.6 μs |  11.18 μs |  0.61 μs |  35.1563 |       - |   310.2 KB |
| Dapper           | 1,330.8 μs |  83.42 μs |  4.57 μs |  15.6250 |       - |  139.07 KB |
| EFCore_Compiled  | 3,548.2 μs | 138.48 μs |  7.59 μs |  97.6563 | 46.8750 |   800.8 KB |
| EFCore           | 6,137.7 μs | 388.00 μs | 21.27 μs | 140.6250 | 54.6875 | 1205.53 KB |
