```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  

```
| Method                     | Mean       | Error     | StdDev    | Median     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|--------------------------- |-----------:|----------:|----------:|-----------:|------:|---------:|--------:|-----------:|------------:|
| Construct_Only             |   112.0 μs |   2.22 μs |   2.88 μs |   111.6 μs |  0.09 |  20.5078 |       - |  167.97 KB |        2.21 |
| RePrepare_PlanOnly_Param   |   163.3 μs |   3.21 μs |   3.69 μs |   162.7 μs |  0.14 |   9.5215 |       - |   78.13 KB |        1.03 |
| Cached_PlanOnly_NoParam    |   276.5 μs |   5.31 μs |  13.51 μs |   276.5 μs |  0.23 |  31.2500 |       - |  265.63 KB |        3.49 |
| Cached_PlanOnly_Param      |   356.4 μs |   7.03 μs |  10.08 μs |   355.6 μs |  0.30 |  35.1563 |       - |  300.01 KB |        3.94 |
| Build_Sql                  |   509.2 μs |  10.18 μs |  23.78 μs |   507.4 μs |  0.43 |  54.6875 | 50.7813 |  453.92 KB |        5.96 |
| M12_NoCache_PlanOnly_Param |   535.6 μs |  18.47 μs |  53.57 μs |   523.3 μs |  0.45 |  54.6875 | 52.7344 |  455.48 KB |        5.98 |
| Prepared_ToList            | 1,193.7 μs |  23.12 μs |  22.71 μs | 1,199.8 μs |  1.00 |   7.8125 |       - |   76.13 KB |        1.00 |
| Build_Sql_Join             | 1,424.5 μs |  28.06 μs |  46.11 μs | 1,425.8 μs |  1.19 | 140.6250 | 85.9375 | 1152.42 KB |       15.14 |
| Cached_ToList              | 1,755.5 μs |  35.07 μs |  95.42 μs | 1,733.9 μs |  1.47 |  39.0625 |       - |  376.14 KB |        4.94 |
| M12_NoCache_ToList         | 4,205.7 μs | 153.31 μs | 449.63 μs | 4,031.8 μs |  3.52 |  62.5000 | 46.8750 |  548.84 KB |        7.21 |
