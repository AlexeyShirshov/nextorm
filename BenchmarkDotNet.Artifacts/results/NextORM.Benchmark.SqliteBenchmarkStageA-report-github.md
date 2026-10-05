```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  Categories=stage-a  

```
| Method                   | Mean       | Median     | Ratio | Gen0     | Allocated  | Alloc Ratio |
|------------------------- |-----------:|-----------:|------:|---------:|-----------:|------------:|
| Where_Construct          |   116.3 μs |   116.1 μs |  1.00 |  30.2734 |  248.44 KB |        1.00 |
| Where_Prepare_NoHash     |   250.8 μs |   248.6 μs |  2.16 |  45.8984 |  378.91 KB |        1.53 |
| Where_Prepare_Hash       |   359.1 μs |   355.7 μs |  3.09 |  50.7813 |  425.01 KB |        1.71 |
| Where_CachedHit_PlanOnly |   399.7 μs |   394.2 μs |  3.44 |  54.6875 |  447.67 KB |        1.80 |
| Join_Construct           |   475.4 μs |   473.4 μs |  4.09 |  72.2656 |  596.91 KB |        2.40 |
| Join_Prepare_NoHash      |   770.8 μs |   760.7 μs |  6.63 | 101.5625 |  835.99 KB |        3.36 |
| Where_Prepared_ToList    |   903.7 μs |   903.4 μs |  7.78 |   7.8125 |   76.13 KB |        0.31 |
| Join_Prepared_ToList     |   955.1 μs |   955.0 μs |  8.22 |   7.8125 |   78.34 KB |        0.32 |
| Join_Prepare_Hash        | 1,386.1 μs |   988.1 μs | 11.93 | 101.5625 |  871.15 KB |        3.51 |
| Where_CachedHit_ToList   | 1,505.3 μs | 1,500.3 μs | 12.96 |  62.5000 |  519.11 KB |        2.09 |
| Join_CachedHit_PlanOnly  | 2,161.6 μs | 1,432.1 μs | 18.61 | 125.0000 | 1145.38 KB |        4.61 |
| Join_CachedHit_ToList    | 4,306.6 μs | 3,208.2 μs | 37.07 | 140.6250 | 1219.04 KB |        4.91 |
