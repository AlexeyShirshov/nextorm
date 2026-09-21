```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=InMemoryNew  

```
| Method                      | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0       | Gen1       | Allocated | Alloc Ratio |
|---------------------------- |----------:|----------:|---------:|------:|--------:|-----------:|-----------:|----------:|------------:|
| Linq_GroupByCount           |  12.61 ms |  1.740 ms | 0.095 ms |  0.22 |    0.00 |  3125.0000 |  1031.2500 |  25.11 MB |        0.50 |
| Nextorm_GroupByCount        |  58.35 ms |  3.306 ms | 0.181 ms |  1.00 |    0.00 |  6222.2222 |  2000.0000 |  49.88 MB |        1.00 |
| EFCoreInMemory_GroupByCount | 116.42 ms | 33.353 ms | 1.828 ms |  2.00 |    0.03 | 22200.0000 | 13000.0000 | 178.63 MB |        3.58 |
