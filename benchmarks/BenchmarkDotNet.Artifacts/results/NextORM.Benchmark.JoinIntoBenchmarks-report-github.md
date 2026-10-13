```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                     | Mean     | Ratio | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|--------------------------- |---------:|------:|---------:|---------:|---------:|----------:|------------:|
| JoinInto_OneToMany_ToList  | 12.27 ms |  1.00 | 437.5000 | 218.7500 | 125.0000 |   3.94 MB |        1.00 |
| JoinInto_ManyToMany_ToList | 14.91 ms |  1.22 | 468.7500 | 437.5000 | 218.7500 |   4.26 MB |        1.08 |
