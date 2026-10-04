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
| Where_Construct          |   119.7 μs |  1.00 |  30.2734 |      - |  248.44 KB |        1.00 |
| Where_Prepare_NoHash     |   252.9 μs |  2.12 |  46.3867 |      - |  378.92 KB |        1.53 |
| Where_Prepare_Hash       |   332.1 μs |  2.78 |  51.7578 | 0.4883 |  425.01 KB |        1.71 |
| Join_Construct           |   520.3 μs |  4.35 |  71.2891 |      - |  585.98 KB |        2.36 |
| Where_CachedHit_PlanOnly |   621.9 μs |  5.20 |  77.1484 |      - |  637.52 KB |        2.57 |
| Join_Prepare_NoHash      |   943.3 μs |  7.89 | 101.5625 |      - |  831.31 KB |        3.35 |
| Where_Prepared_ToList    |   987.3 μs |  8.26 |   8.7891 |      - |   76.14 KB |        0.31 |
| Join_Prepared_ToList     | 1,023.3 μs |  8.56 |   8.7891 |      - |   78.34 KB |        0.32 |
| Join_Prepare_Hash        | 1,114.8 μs |  9.32 | 105.4688 |      - |  874.28 KB |        3.52 |
| Join_CachedHit_PlanOnly  | 1,790.6 μs | 14.98 | 144.5313 | 1.9531 | 1189.92 KB |        4.79 |
| Where_CachedHit_ToList   | 1,832.1 μs | 15.32 |  85.9375 |      - |  708.97 KB |        2.85 |
| Join_CachedHit_ToList    | 4,110.0 μs | 34.37 | 148.4375 |      - |  1263.6 KB |        5.09 |
