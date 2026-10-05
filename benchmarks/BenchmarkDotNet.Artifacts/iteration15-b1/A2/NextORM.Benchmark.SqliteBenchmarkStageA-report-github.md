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
| Where_Construct          |   119.0 μs |  1.00 |  30.2734 |      - |  248.44 KB |        1.00 |
| Where_Prepare_NoHash     |   274.6 μs |  2.31 |  46.3867 |      - |  378.92 KB |        1.53 |
| Where_Prepare_Hash       |   335.6 μs |  2.82 |  51.7578 | 0.4883 |  425.01 KB |        1.71 |
| Join_Construct           |   562.9 μs |  4.73 |  71.2891 |      - |  585.98 KB |        2.36 |
| Where_CachedHit_PlanOnly |   697.3 μs |  5.86 |  77.1484 |      - |  637.52 KB |        2.57 |
| Join_Prepare_NoHash      | 1,109.9 μs |  9.33 | 101.5625 |      - |  831.31 KB |        3.35 |
| Where_Prepared_ToList    | 1,171.9 μs |  9.85 |   7.8125 |      - |   76.14 KB |        0.31 |
| Join_Prepare_Hash        | 1,253.9 μs | 10.54 | 105.4688 |      - |  874.28 KB |        3.52 |
| Join_Prepared_ToList     | 1,381.9 μs | 11.62 |   7.8125 |      - |   78.34 KB |        0.32 |
| Where_CachedHit_ToList   | 2,007.5 μs | 16.87 |  85.9375 |      - |  708.98 KB |        2.85 |
| Join_CachedHit_PlanOnly  | 2,230.4 μs | 18.75 | 144.5313 |      - | 1189.92 KB |        4.79 |
| Join_CachedHit_ToList    | 5,610.3 μs | 47.16 | 148.4375 |      - | 1263.57 KB |        5.09 |
