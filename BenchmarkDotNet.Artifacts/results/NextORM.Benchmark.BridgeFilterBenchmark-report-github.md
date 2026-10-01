```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=bridge-filter  

```
| Method               | Mean     | Error      | StdDev    | Ratio | Gen0     | Allocated  | Alloc Ratio |
|--------------------- |---------:|-----------:|----------:|------:|---------:|-----------:|------------:|
| Native_Warm_Prepare  | 2.394 ms |  0.7315 ms | 0.0401 ms |  0.37 | 164.0625 | 1367.37 KB |        1.28 |
| Native_Rebind_ToList | 2.638 ms |  0.8238 ms | 0.0452 ms |  0.40 |  89.8438 |  759.48 KB |        0.71 |
| Native_Cached_ToList | 4.171 ms | 12.6676 ms | 0.6944 ms |  0.64 |  89.8438 |  755.58 KB |        0.71 |
| Bridge_Rebind_ToList | 4.578 ms | 17.0296 ms | 0.9334 ms |  0.70 | 125.0000 | 1072.14 KB |        1.00 |
| Bridge_Warm_Prepare  | 4.606 ms |  1.5057 ms | 0.0825 ms |  0.71 | 234.3750 | 1966.08 KB |        1.84 |
| Bridge_Cached_ToList | 6.596 ms | 16.4974 ms | 0.9043 ms |  1.01 | 125.0000 | 1070.55 KB |        1.00 |
