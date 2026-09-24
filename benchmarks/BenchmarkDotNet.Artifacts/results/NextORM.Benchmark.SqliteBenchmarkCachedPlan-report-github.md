```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  

```
| Method                     | Mean       | Error     | StdDev      | Median     | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|--------------------------- |-----------:|----------:|------------:|-----------:|------:|---------:|---------:|-----------:|------------:|
| Construct_Only             |   141.5 μs |   5.54 μs |    15.98 μs |   136.4 μs |  0.16 |  28.3203 |        - |  239.07 KB |        3.14 |
| RePrepare_PlanOnly_Param   |   242.6 μs |   4.75 μs |     6.34 μs |   241.7 μs |  0.27 |  21.4844 |        - |  176.56 KB |        2.32 |
| Cached_PlanOnly_NoParam    |   335.8 μs |   6.20 μs |     5.80 μs |   336.9 μs |  0.38 |  50.7813 |        - |  435.95 KB |        5.73 |
| Cached_PlanOnly_Param      |   402.8 μs |   5.57 μs |     5.21 μs |   401.4 μs |  0.45 |  54.6875 |        - |  470.32 KB |        6.18 |
| Build_Sql                  |   584.5 μs |  12.95 μs |    37.37 μs |   590.6 μs |  0.66 |  74.2188 |  70.3125 |  607.84 KB |        7.98 |
| M12_NoCache_PlanOnly_Param |   664.1 μs |  17.08 μs |    49.27 μs |   651.4 μs |  0.75 |  74.2188 |  70.3125 |  627.37 KB |        8.24 |
| Prepared_ToList            |   887.5 μs |  12.06 μs |    11.28 μs |   892.3 μs |  1.00 |   8.7891 |        - |   76.13 KB |        1.00 |
| Build_Sql_Join             | 1,281.9 μs |  28.28 μs |    82.94 μs | 1,289.9 μs |  1.44 | 132.8125 | 125.0000 | 1088.38 KB |       14.30 |
| Cached_ToList              | 1,471.4 μs |  20.45 μs |    19.13 μs | 1,466.2 μs |  1.66 |  62.5000 |        - |  546.46 KB |        7.18 |
| M12_NoCache_ToList         | 4,950.9 μs | 737.85 μs | 2,163.99 μs | 3,809.3 μs |  5.58 |  78.1250 |  62.5000 |  720.74 KB |        9.47 |
