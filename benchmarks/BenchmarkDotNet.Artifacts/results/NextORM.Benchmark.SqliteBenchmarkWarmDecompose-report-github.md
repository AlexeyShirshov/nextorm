```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                         | Mean     | Gen0    | Allocated |
|------------------------------- |---------:|--------:|----------:|
| InAtIn_Inline_Warm_Reused      | 165.4 μs | 13.6719 |  112.5 KB |
| InAtIn_Captured_Construct      | 199.6 μs | 33.9355 | 277.34 KB |
| InAtIn_Inline_Construct        | 209.0 μs | 36.1328 | 296.09 KB |
| InAtIn_Inline_Prepare_NoHash   | 300.5 μs | 51.2695 | 419.53 KB |
| InAtIn_Captured_Prepare_NoHash | 306.6 μs | 48.8281 | 400.78 KB |
| InAtIn_Captured_Prepare_Hash   | 412.4 μs | 61.0352 | 500.78 KB |
| InAtIn_Inline_Prepare_Hash     | 527.8 μs | 63.4766 | 524.22 KB |
| InAtIn_Inline_Warm_PlanOnly    | 626.9 μs | 70.3125 | 581.25 KB |
