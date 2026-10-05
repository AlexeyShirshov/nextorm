```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=stage-a  

```
| Method                   | Mean       | Ratio | Gen0     | Gen1   | Allocated  | Alloc Ratio |
|------------------------- |-----------:|------:|---------:|-------:|-----------:|------------:|
| Where_Construct          |   126.0 μs |  1.00 |  30.3955 |      - |  248.44 KB |        1.00 |
| Where_Prepare_NoHash     |   286.0 μs |  2.28 |  46.3867 |      - |  378.92 KB |        1.53 |
| Where_Prepare_Hash       |   341.1 μs |  2.71 |  51.7578 | 0.4883 |  425.01 KB |        1.71 |
| Where_CachedHit_PlanOnly |   647.4 μs |  5.15 |  72.2656 |      - |  595.33 KB |        2.40 |
| Join_Construct           |   719.5 μs |  5.72 |  72.2656 |      - |  593.79 KB |        2.39 |
| Join_Prepare_NoHash      |   974.9 μs |  7.75 | 101.5625 |      - |  839.12 KB |        3.38 |
| Where_Prepared_ToList    | 1,053.0 μs |  8.38 |   8.7891 |      - |   76.13 KB |        0.31 |
| Join_Prepared_ToList     | 1,111.4 μs |  8.84 |   7.8125 |      - |   78.34 KB |        0.32 |
| Join_Prepare_Hash        | 1,273.8 μs | 10.13 | 107.4219 |      - |  882.09 KB |        3.55 |
| Join_CachedHit_PlanOnly  | 1,827.3 μs | 14.53 | 140.6250 |      - | 1155.56 KB |        4.65 |
| Where_CachedHit_ToList   | 1,912.2 μs | 15.21 |  78.1250 |      - |  666.79 KB |        2.68 |
| Join_CachedHit_ToList    | 4,254.4 μs | 33.84 | 148.4375 |      - | 1229.23 KB |        4.95 |
