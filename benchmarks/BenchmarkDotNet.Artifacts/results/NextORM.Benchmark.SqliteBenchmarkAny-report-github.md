```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean     | Error      | StdDev    | Gen0     | Gen1    | Allocated  |
|----------------- |----------- |---------:|-----------:|----------:|---------:|--------:|-----------:|
| Nextorm_Prepared |            | 1.024 ms |  0.1588 ms | 0.0087 ms |   9.7656 |       - |   85.16 KB |
| Dapper           |            | 1.536 ms |  0.3240 ms | 0.0178 ms |  15.6250 |       - |  139.07 KB |
| Linq2Db_Compiled |            | 1.699 ms |  0.4269 ms | 0.0234 ms |  27.3438 |       - |   233.6 KB |
| Nextorm_Cached   | acceptance | 2.208 ms |  0.5449 ms | 0.0299 ms |  74.2188 |       - |  618.04 KB |
| Linq2Db          |            | 2.952 ms |  0.3190 ms | 0.0175 ms |  46.8750 |       - |  401.58 KB |
| EFCore_Compiled  |            | 4.213 ms |  2.1794 ms | 0.1195 ms |  93.7500 | 46.8750 |  807.06 KB |
| EFCore           |            | 7.671 ms | 25.7019 ms | 1.4088 ms | 125.0000 | 31.2500 | 1211.89 KB |
