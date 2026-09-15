```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Method           | Mean     | Error    | StdDev   | Allocated  |
|----------------- |---------:|---------:|---------:|-----------:|
| Nextorm_Prepared | 87.13 ms | 1.646 ms | 1.374 ms |   41.41 KB |
| Dapper           | 89.84 ms | 1.598 ms | 1.902 ms |  139.06 KB |
| Nextorm_Cached   | 90.50 ms | 0.832 ms | 0.695 ms |  310.21 KB |
| EFCore_Compiled  | 93.87 ms | 0.718 ms | 0.560 ms |  800.78 KB |
| EFCore           | 97.43 ms | 0.661 ms | 0.586 ms | 1205.59 KB |
