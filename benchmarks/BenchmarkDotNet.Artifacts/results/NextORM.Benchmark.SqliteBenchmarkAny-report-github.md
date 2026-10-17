```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error       | StdDev   | Gen0     | Gen1    | Allocated  |
|----------------- |----------- |-----------:|------------:|---------:|---------:|--------:|-----------:|
| Nextorm_Prepared |            |   889.9 μs |    37.86 μs |  2.07 μs |   9.7656 |       - |   85.16 KB |
| Dapper           |            | 1,399.7 μs |   182.27 μs |  9.99 μs |  15.6250 |       - |  139.07 KB |
| Linq2Db_Compiled |            | 1,498.9 μs |   118.96 μs |  6.52 μs |  27.3438 |       - |   233.6 KB |
| Nextorm_Cached   | acceptance | 1,839.6 μs |   149.21 μs |  8.18 μs |  64.4531 |       - |  533.63 KB |
| Linq2Db          |            | 2,672.7 μs |   526.45 μs | 28.86 μs |  46.8750 |       - |  401.58 KB |
| EFCore_Compiled  |            | 3,620.0 μs |   855.35 μs | 46.88 μs |  97.6563 | 46.8750 |  807.05 KB |
| EFCore           |            | 6,133.7 μs | 1,046.21 μs | 57.35 μs | 140.6250 | 46.8750 | 1211.83 KB |
