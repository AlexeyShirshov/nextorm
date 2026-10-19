```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  Categories=B_Construct  

```
| Method                           | Toolchain              | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|--------------------------------- |----------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
| B_Nextorm_Construct_Cte          | InProcessEmitToolchain | 4.018 μs |  1.098 μs | 0.0602 μs | 0.8240 |      - |   6.81 KB |
| B_Nextorm_Construct_Cte          | Default                | 4.949 μs |  8.762 μs | 0.4803 μs | 0.8240 |      - |   6.97 KB |
| B_Nextorm_Construct_RecursiveCte | InProcessEmitToolchain | 5.547 μs |  4.245 μs | 0.2327 μs | 1.0071 | 0.0076 |   8.24 KB |
| B_Nextorm_Construct_RecursiveCte | Default                | 6.418 μs | 11.864 μs | 0.6503 μs | 1.0071 |      - |   8.46 KB |
