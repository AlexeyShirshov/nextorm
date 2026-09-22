```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  

```
| Method                          | Mean       | Error    | StdDev   | Ratio | Gen0    | Gen1    | Allocated | Alloc Ratio |
|-------------------------------- |-----------:|---------:|---------:|------:|--------:|--------:|----------:|------------:|
| Build_Sql_Property              |   263.4 μs |  5.05 μs | 13.91 μs |  1.00 | 39.0625 | 37.1094 | 329.69 KB |        1.00 |
| Build_Sql_Computed              |   277.8 μs |  5.52 μs | 15.48 μs |  1.06 | 41.0156 | 39.0625 | 342.98 KB |        1.04 |
| Build_Sql_ColumnByName          |   291.3 μs |  6.46 μs | 18.85 μs |  1.11 | 41.0156 | 39.0625 | 345.31 KB |        1.05 |
| Build_Sql_ColumnByName_Unmapped |   303.9 μs |  7.93 μs | 22.24 μs |  1.16 | 41.0156 | 39.0625 | 344.53 KB |        1.05 |
| Build_Sql_ColumnByName_Rename   |   372.0 μs | 10.41 μs | 30.37 μs |  1.42 | 46.8750 | 44.9219 | 388.28 KB |        1.18 |
| Build_Sql_ColumnByName_Join     |   747.5 μs |  9.21 μs |  8.16 μs |  2.85 | 85.9375 | 78.1250 |  731.3 KB |        2.22 |
| ToList_Property                 | 1,245.6 μs | 19.32 μs | 18.07 μs |  4.74 | 37.1094 |       - | 307.03 KB |        0.93 |
| ToList_ColumnByName             | 1,338.2 μs | 16.66 μs | 15.58 μs |  5.09 | 39.0625 |       - |    325 KB |        0.99 |
