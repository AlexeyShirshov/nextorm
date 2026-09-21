```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                         | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------------- |------------:|------------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| LinqToList                     |    98.82 μs |    28.19 μs |  1.545 μs |  0.72 |    0.06 |  38.2080 |  12.6953 |  312.63 KB |        1.33 |
| Linq                           |   103.32 μs |    42.15 μs |  2.310 μs |  0.75 |    0.07 |  28.6865 |        - |  234.45 KB |        1.00 |
| NextormPreparedSync            |   138.33 μs |   267.54 μs | 14.665 μs |  1.01 |    0.13 |  28.5645 |        - |  234.38 KB |        1.00 |
| NextormPreparedSyncToList      |   201.11 μs |   166.31 μs |  9.116 μs |  1.46 |    0.14 |  38.0859 |  12.6953 |  312.56 KB |        1.33 |
| EFCoreInMemory_Compiled        | 2,062.72 μs |   338.39 μs | 18.548 μs | 15.02 |    1.32 | 417.9688 | 207.0313 | 3439.27 KB |       14.67 |
| EFCoreInMemory_Compiled_ToList | 2,384.24 μs | 1,719.65 μs | 94.260 μs | 17.36 |    1.63 | 433.5938 | 207.0313 | 3567.59 KB |       15.22 |
