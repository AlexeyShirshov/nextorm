```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=InMemoryNew  

```
| Method                      | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0       | Gen1       | Gen2      | Allocated    | Alloc Ratio |
|---------------------------- |-------------:|------------:|------------:|------:|--------:|-----------:|-----------:|----------:|-------------:|------------:|
| Linq_ToArray                |     767.3 μs |    244.9 μs |    13.42 μs |  0.11 |    0.00 |   475.5859 |          - |         - |   3915.63 KB |       0.490 |
| Linq_Last                   |   2,745.0 μs |    836.5 μs |    45.85 μs |  0.39 |    0.01 |          - |          - |         - |     12.53 KB |       0.002 |
| Nextorm_ToArray             |   7,125.1 μs |  2,035.8 μs |   111.59 μs |  1.00 |    0.02 |   968.7500 |    62.5000 |         - |   7989.12 KB |       1.000 |
| Linq_ToDictionary           |  12,429.5 μs |  1,762.9 μs |    96.63 μs |  1.74 |    0.03 |  2375.0000 |  1906.2500 | 1890.6250 |   19776.9 KB |       2.475 |
| Nextorm_ToDictionary        |  17,636.6 μs |    273.4 μs |    14.98 μs |  2.48 |    0.03 |  3156.2500 |  2281.2500 | 2187.5000 |  23851.07 KB |       2.985 |
| EFCoreInMemory_Last         |  40,550.7 μs |  7,749.6 μs |   424.78 μs |  5.69 |    0.09 |  5769.2308 |  1923.0769 |         - |  47465.09 KB |       5.941 |
| EFCoreInMemory_ToArray      | 173,814.0 μs | 74,961.3 μs | 4,108.88 μs | 24.40 |    0.60 | 42333.3333 |   333.3333 |         - | 348155.55 KB |      43.579 |
| Nextorm_Last                | 197,713.4 μs | 55,665.6 μs | 3,051.22 μs | 27.75 |    0.53 |  5000.0000 |  1666.6667 |         - |  43261.77 KB |       5.415 |
| EFCoreInMemory_ToDictionary | 229,348.0 μs | 50,316.3 μs | 2,758.01 μs | 32.19 |    0.55 | 47666.6667 | 32333.3333 | 6333.3333 | 379337.72 KB |      47.482 |
