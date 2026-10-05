```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  Categories=stage-a  

```
| Method                   | Mean       | Median     | Gen0     | Allocated  |
|------------------------- |-----------:|-----------:|---------:|-----------:|
| Where_CachedHit_PlanOnly |   405.0 μs |   395.1 μs |  54.6875 |  447.67 KB |
| Where_CachedHit_ToList   | 1,502.1 μs | 1,481.8 μs |  62.5000 |  519.11 KB |
| Join_CachedHit_PlanOnly  | 1,908.4 μs | 1,411.4 μs | 125.0000 | 1145.38 KB |
| Join_CachedHit_ToList    | 3,320.5 μs | 3,209.7 μs | 140.6250 | 1219.04 KB |
