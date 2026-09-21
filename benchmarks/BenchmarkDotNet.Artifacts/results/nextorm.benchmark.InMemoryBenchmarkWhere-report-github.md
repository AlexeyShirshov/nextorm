```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                  | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|------------------------ |----------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| NextormPreparedParam    |  3.282 ms |   1.964 ms | 0.1076 ms |  1.00 |    0.04 |         - |         - |    22.68 KB |        1.00 |
| Linq                    |  3.720 ms |   5.081 ms | 0.2785 ms |  1.13 |    0.08 |         - |         - |    17.33 KB |        0.76 |
| EFCoreInMemory_Compiled | 67.805 ms | 110.962 ms | 6.0822 ms | 20.68 |    1.71 | 5714.2857 | 2142.8571 | 47122.07 KB |    2,077.45 |
