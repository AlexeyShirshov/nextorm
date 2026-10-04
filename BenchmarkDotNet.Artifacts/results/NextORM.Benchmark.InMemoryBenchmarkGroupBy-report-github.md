```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=acceptance,InMemoryNew  

```
| Method               | Mean     | Error    | StdDev   | Ratio | Gen0      | Gen1      | Allocated | Alloc Ratio |
|--------------------- |---------:|---------:|---------:|------:|----------:|----------:|----------:|------------:|
| Nextorm_GroupByCount | 57.13 ms | 3.550 ms | 0.195 ms |  1.00 | 6166.6667 | 2000.0000 |  50.05 MB |        1.00 |
