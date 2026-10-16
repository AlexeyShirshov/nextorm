```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=64  
IterationCount=5  LaunchCount=1  RunStrategy=Throughput  
UnrollFactor=1  WarmupCount=3  Categories=t218-preparation  

```
| Method                            | Mean     | Error    | StdDev   | Gen0    | Allocated |
|---------------------------------- |---------:|---------:|---------:|--------:|----------:|
| Prepare_Control_Patindex_Uncached | 2.961 ms | 5.398 ms | 1.402 ms | 93.7500 | 789.53 KB |
