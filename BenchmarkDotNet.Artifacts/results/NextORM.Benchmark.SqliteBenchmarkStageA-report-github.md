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
| Where_Construct          |   106.1 μs |  1.00 |  30.3955 |      - |  248.44 KB |        1.00 |
| Where_Prepare_NoHash     |   232.2 μs |  2.19 |  46.3867 |      - |  378.92 KB |        1.53 |
| Where_Prepare_Hash       |   328.1 μs |  3.09 |  51.7578 | 0.4883 |  425.01 KB |        1.71 |
| Join_Construct           |   453.4 μs |  4.27 |  71.2891 |      - |  585.97 KB |        2.36 |
| Where_CachedHit_PlanOnly |   507.3 μs |  4.78 |  64.4531 | 0.4883 |  527.36 KB |        2.12 |
| Where_Prepared_ToList    |   893.9 μs |  8.42 |   7.8125 |      - |   76.14 KB |        0.31 |
| Join_Prepare_NoHash      |   895.1 μs |  8.44 | 101.5625 |      - |   831.3 KB |        3.35 |
| Join_Prepared_ToList     | 1,033.1 μs |  9.74 |   7.8125 |      - |   78.34 KB |        0.32 |
| Join_Prepare_Hash        | 1,037.5 μs |  9.78 | 106.4453 | 0.9766 |  874.28 KB |        3.52 |
| Where_CachedHit_ToList   | 1,618.6 μs | 15.25 |  72.2656 |      - |  598.81 KB |        2.41 |
| Join_CachedHit_PlanOnly  | 1,785.1 μs | 16.82 | 144.5313 | 1.9531 | 1189.92 KB |        4.79 |
| Join_CachedHit_ToList    | 4,059.6 μs | 38.26 | 152.3438 |      - | 1263.59 KB |        5.09 |
