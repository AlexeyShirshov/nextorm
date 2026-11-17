```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method           | Categories | Mean     | Error     | StdDev    | Gen0     | Gen1    | Allocated  |
|----------------- |----------- |---------:|----------:|----------:|---------:|--------:|-----------:|
| Nextorm_Prepared |            | 1.059 ms | 0.0197 ms | 0.0193 ms |   9.7656 |       - |   84.38 KB |
| Nextorm_Cached   | acceptance | 1.389 ms | 0.0261 ms | 0.0257 ms |  23.4375 |  5.8594 |  194.56 KB |
| Dapper           |            | 1.509 ms | 0.0271 ms | 0.0253 ms |  15.6250 |       - |  138.29 KB |
| Linq2Db_Compiled |            | 1.663 ms | 0.0188 ms | 0.0176 ms |  27.3438 |       - |  232.82 KB |
| Linq2Db          |            | 2.607 ms | 0.0451 ms | 0.0421 ms |  46.8750 |       - |  400.02 KB |
| EFCore_Compiled  |            | 3.660 ms | 0.0411 ms | 0.0384 ms |  89.8438 | 42.9688 |   763.3 KB |
| EFCore           |            | 6.087 ms | 0.0577 ms | 0.0540 ms | 140.6250 | 62.5000 | 1163.38 KB |
