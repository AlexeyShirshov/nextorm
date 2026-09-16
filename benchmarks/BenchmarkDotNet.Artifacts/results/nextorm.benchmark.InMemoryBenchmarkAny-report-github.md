```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                  | Mean           | Error           | StdDev         | Gen0    | Gen1    | Allocated |
|------------------------ |---------------:|----------------:|---------------:|--------:|--------:|----------:|
| Linq                    |       2.877 ns |       0.9022 ns |      0.0495 ns |       - |       - |         - |
| NextormPrepared         |      30.524 ns |      51.4381 ns |      2.8195 ns |       - |       - |         - |
| EFCoreInMemory_Compiled | 384,633.727 ns | 472,543.5822 ns | 25,901.7084 ns | 57.1289 | 22.4609 |  481811 B |
