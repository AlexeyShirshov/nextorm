```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=T216  

```
| Method                  | Mean     | Error     | StdDev   | Gen0     | Gen1   | Allocated |
|------------------------ |---------:|----------:|---------:|---------:|-------:|----------:|
| NoLock_Prepared         | 410.2 μs |  72.17 μs |  3.96 μs |  86.9141 | 0.4883 | 712.51 KB |
| NoLock_Cached           | 425.5 μs |  89.12 μs |  4.89 μs |  62.9883 | 0.4883 |  517.2 KB |
| SourceLockJoin_Cached   | 720.4 μs |  53.12 μs |  2.91 μs |  82.0313 |      - | 671.13 KB |
| WholeLockJoin_Cached    | 740.4 μs |  40.63 μs |  2.23 μs |  85.9375 |      - | 703.94 KB |
| SourceLockJoin_Prepared | 911.1 μs | 184.15 μs | 10.09 μs | 119.1406 | 0.9766 | 975.04 KB |
| WholeLockJoin_Prepared  | 937.4 μs |  38.64 μs |  2.12 μs | 120.1172 | 0.9766 | 982.86 KB |
