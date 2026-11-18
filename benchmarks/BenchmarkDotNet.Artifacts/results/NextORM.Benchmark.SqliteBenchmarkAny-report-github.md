```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method           | Categories | Mean     | Error     | StdDev    | Gen0     | Gen1    | Allocated  |
|----------------- |----------- |---------:|----------:|----------:|---------:|--------:|-----------:|
| Nextorm_Prepared |            | 1.066 ms | 0.0106 ms | 0.0099 ms |   9.7656 |       - |   84.38 KB |
| Nextorm_Cached   | acceptance | 1.364 ms | 0.0114 ms | 0.0106 ms |  23.4375 |  5.8594 |  194.56 KB |
| Dapper           |            | 1.574 ms | 0.0143 ms | 0.0127 ms |  15.6250 |       - |  138.29 KB |
| Linq2Db_Compiled |            | 1.695 ms | 0.0152 ms | 0.0143 ms |  27.3438 |       - |  232.82 KB |
| Linq2Db          |            | 2.737 ms | 0.0526 ms | 0.1394 ms |  46.8750 |       - |  400.02 KB |
| EFCore_Compiled  |            | 3.725 ms | 0.0474 ms | 0.0396 ms |  89.8438 | 42.9688 |   763.3 KB |
| EFCore           |            | 5.977 ms | 0.0914 ms | 0.0855 ms | 125.0000 | 62.5000 | 1163.46 KB |
