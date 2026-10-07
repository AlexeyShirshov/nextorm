```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=acceptance  
Error=0.0433 ms  StdDev=0.0024 ms  

```
| Method                     | Mean     | Gen0    | Allocated |
|--------------------------- |---------:|--------:|----------:|
| Nextorm_Cached_ToListAsync | 2.035 ms | 74.2188 | 607.69 KB |
