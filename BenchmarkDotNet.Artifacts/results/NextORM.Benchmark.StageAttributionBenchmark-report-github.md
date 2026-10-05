```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                   | N    | Mean          | Error       | StdDev        | Gen0      | Allocated   |
|------------------------- |----- |--------------:|------------:|--------------:|----------:|------------:|
| CachedHit_PlanOnly_Param | 1    |      4.157 μs |   0.0823 μs |     0.2081 μs |    0.5188 |     4.48 KB |
| CachedHit_PlanOnly_Param | 16   |     67.882 μs |   1.1631 μs |     2.3496 μs |    8.3008 |    71.63 KB |
| CachedHit_PlanOnly_Param | 256  |  1,014.490 μs |  14.9233 μs |    31.1505 μs |  132.8125 |  1146.02 KB |
| CachedHit_PlanOnly_Param | 4096 | 16,932.342 μs | 672.3691 μs | 1,723.5393 μs | 2166.6667 | 18336.38 KB |
