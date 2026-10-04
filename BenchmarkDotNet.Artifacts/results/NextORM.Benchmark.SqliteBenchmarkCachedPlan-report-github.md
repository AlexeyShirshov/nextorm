```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  

```
| Method                     | Categories | Mean       | Error    | StdDev    | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|--------------------------- |----------- |-----------:|---------:|----------:|------:|---------:|--------:|-----------:|------------:|
| Construct_Only             |            |   152.2 μs |  3.03 μs |   8.39 μs |  0.16 |  32.2266 |       - |  267.19 KB |        3.51 |
| RePrepare_PlanOnly_Param   |            |   284.8 μs |  5.37 μs |   5.27 μs |  0.29 |  22.4609 |       - |  184.38 KB |        2.42 |
| Cached_PlanOnly_NoParam    |            |   428.3 μs |  8.48 μs |  14.40 μs |  0.44 |  54.6875 |       - |  471.88 KB |        6.20 |
| Cached_PlanOnly_Param      | acceptance |   556.5 μs | 12.09 μs |  35.08 μs |  0.57 |  58.5938 |       - |  506.26 KB |        6.65 |
| M12_NoCache_PlanOnly_Param |            |   656.0 μs | 14.59 μs |  42.34 μs |  0.67 |  78.1250 | 70.3125 |  680.48 KB |        8.94 |
| Build_Sql                  |            |   665.0 μs | 14.27 μs |  41.40 μs |  0.68 |  78.1250 | 70.3125 |  660.95 KB |        8.68 |
| Prepared_ToList            | acceptance |   978.2 μs | 19.21 μs |  32.10 μs |  1.00 |   7.8125 |       - |   76.13 KB |        1.00 |
| Build_Sql_Join             |            | 1,802.3 μs | 35.60 μs |  52.18 μs |  1.84 | 164.0625 | 85.9375 | 1344.62 KB |       17.66 |
| Cached_ToList              | acceptance | 1,874.7 μs | 37.42 μs |  54.85 μs |  1.92 |  70.3125 |       - |   582.4 KB |        7.65 |
| M12_NoCache_ToList         |            | 3,610.6 μs | 71.01 μs | 204.89 μs |  3.69 |  93.7500 | 78.1250 |  773.85 KB |       10.16 |
