```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Mean     | Error     | StdDev  | Gen0    | Gen1   | Allocated |
|----------------- |---------:|----------:|--------:|--------:|-------:|----------:|
| Nextorm_Prepared | 105.5 μs |   8.20 μs | 0.45 μs |  1.0986 |      - |   9.26 KB |
| Dapper           | 193.0 μs |  20.08 μs | 1.10 μs |  2.4414 |      - |  21.45 KB |
| Nextorm_Cached   | 262.9 μs |  58.13 μs | 3.19 μs | 10.7422 |      - |  89.21 KB |
| EFCore_Compiled  | 496.9 μs |  48.33 μs | 2.65 μs | 14.6484 | 6.8359 | 120.71 KB |
| EFCore           | 836.4 μs | 109.22 μs | 5.99 μs | 21.4844 | 6.8359 | 178.68 KB |
