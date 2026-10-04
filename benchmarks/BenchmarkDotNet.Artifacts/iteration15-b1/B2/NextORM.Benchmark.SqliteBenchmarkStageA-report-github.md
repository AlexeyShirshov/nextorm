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
| Where_Construct          |   143.1 μs |  1.00 |  31.7383 |      - |  259.38 KB |        1.00 |
| Where_Prepare_NoHash     |   280.9 μs |  1.96 |  47.3633 |      - |  389.85 KB |        1.50 |
| Where_Prepare_Hash       |   371.8 μs |  2.60 |  53.2227 | 0.4883 |  435.95 KB |        1.68 |
| Join_Construct           |   552.8 μs |  3.86 |  71.2891 |      - |  585.98 KB |        2.26 |
| Where_CachedHit_PlanOnly |   703.1 μs |  4.91 |  74.2188 |      - |  606.27 KB |        2.34 |
| Where_Prepared_ToList    |   948.0 μs |  6.63 |   7.8125 |      - |   76.14 KB |        0.29 |
| Join_Prepare_NoHash      | 1,006.4 μs |  7.03 | 101.5625 |      - |   831.3 KB |        3.20 |
| Join_Prepared_ToList     | 1,305.8 μs |  9.13 |   7.8125 |      - |   78.34 KB |        0.30 |
| Join_Prepare_Hash        | 1,310.8 μs |  9.16 | 105.4688 |      - |  874.28 KB |        3.37 |
| Join_CachedHit_PlanOnly  | 1,978.3 μs | 13.83 | 138.6719 | 1.9531 | 1147.73 KB |        4.42 |
| Where_CachedHit_ToList   | 2,055.6 μs | 14.37 |  82.0313 |      - |  677.72 KB |        2.61 |
| Join_CachedHit_ToList    | 5,217.5 μs | 36.47 | 148.4375 |      - | 1221.42 KB |        4.71 |
