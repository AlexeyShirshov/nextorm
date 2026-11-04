```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                             | RowCount | Mean         | Ratio | Gen0       | Gen1      | Gen2     | Allocated    | Alloc Ratio |
|----------------------------------- |--------- |-------------:|------:|-----------:|----------:|---------:|-------------:|------------:|
| ToList_Dto                         | 1000     |     399.9 μs |  1.00 |     9.7656 |    1.4648 |        - |     82.06 KB |        1.00 |
| Dapper_ToList_Json                 | 1000     |     555.5 μs |  1.39 |    13.6719 |    1.9531 |        - |    119.22 KB |        1.45 |
| Linq2Db_ToList_Json                | 1000     |     562.3 μs |  1.41 |    10.7422 |    1.9531 |        - |     90.39 KB |        1.10 |
| EFCore_ToList_Json                 | 1000     |     679.6 μs |  1.70 |    38.0859 |    6.8359 |        - |    316.65 KB |        3.86 |
| WriteJson_Array_Scalar             | 1000     |     788.0 μs |  1.97 |     3.9063 |    1.9531 |        - |     38.01 KB |        0.46 |
| WriteJsonAsync_Array_Scalar        | 1000     |     821.9 μs |  2.06 |     3.9063 |    2.9297 |        - |     38.24 KB |        0.47 |
| WriteJson_Array_ScalarPayload      | 1000     |   1,288.9 μs |  3.22 |   140.6250 |    5.8594 |        - |   1158.56 KB |       14.12 |
| WriteJsonAsync_Array_ScalarPayload | 1000     |   1,463.0 μs |  3.66 |   140.6250 |    5.8594 |        - |   1158.78 KB |       14.12 |
| WriteJson_Array_Dto                | 1000     |   2,132.1 μs |  5.33 |     7.8125 |    3.9063 |        - |     84.01 KB |        1.02 |
| WriteJson_Array_WideDto            | 1000     |   2,681.6 μs |  6.71 |   144.5313 |   19.5313 |        - |   1191.29 KB |       14.52 |
|                                    |          |              |       |            |           |          |              |             |
| WriteJson_Array_Scalar             | 10000    |   5,724.3 μs |  0.93 |          - |         - |        - |     38.04 KB |        0.05 |
| ToList_Dto                         | 10000    |   6,170.0 μs |  1.00 |    93.7500 |   54.6875 |        - |    785.21 KB |        1.00 |
| WriteJsonAsync_Array_Scalar        | 10000    |   6,620.2 μs |  1.07 |          - |         - |        - |     38.25 KB |        0.05 |
| Linq2Db_ToList_Json                | 10000    |   9,150.2 μs |  1.48 |   125.0000 |  109.3750 |  31.2500 |    963.56 KB |        1.23 |
| Dapper_ToList_Json                 | 10000    |   9,415.9 μs |  1.53 |   156.2500 |  109.3750 |  31.2500 |   1273.78 KB |        1.62 |
| WriteJson_Array_ScalarPayload      | 10000    |  10,341.2 μs |  1.68 |  1406.2500 |         - |        - |  11495.22 KB |       14.64 |
| WriteJson_Array_Dto                | 10000    |  10,351.9 μs |  1.68 |    46.8750 |         - |        - |    435.64 KB |        0.55 |
| WriteJsonAsync_Array_ScalarPayload | 10000    |  11,258.5 μs |  1.83 |  1406.2500 |         - |        - |  11495.38 KB |       14.64 |
| EFCore_ToList_Json                 | 10000    |  11,303.0 μs |  1.83 |   406.2500 |  171.8750 |  46.8750 |   3158.67 KB |        4.02 |
| WriteJson_Array_WideDto            | 10000    |  13,801.5 μs |  2.24 |  1406.2500 |   15.6250 |        - |  11527.94 KB |       14.68 |
|                                    |          |              |       |            |           |          |              |             |
| WriteJson_Array_Scalar             | 100000   |  49,154.6 μs |  0.69 |          - |         - |        - |     38.43 KB |       0.005 |
| WriteJsonAsync_Array_Scalar        | 100000   |  53,831.0 μs |  0.75 |          - |         - |        - |     38.89 KB |       0.005 |
| ToList_Dto                         | 100000   |  71,682.2 μs |  1.00 |  1142.8571 | 1000.0000 | 285.7143 |   7819.59 KB |       1.000 |
| WriteJson_Array_Dto                | 100000   |  82,891.0 μs |  1.16 |   428.5714 |         - |        - |   3951.92 KB |       0.505 |
| Linq2Db_ToList_Json                | 100000   |  86,796.9 μs |  1.21 |  1166.6667 | 1000.0000 | 333.3333 |   9087.21 KB |       1.162 |
| WriteJson_Array_ScalarPayload      | 100000   |  87,726.1 μs |  1.22 | 14000.0000 |         - |        - | 114854.06 KB |      14.688 |
| Dapper_ToList_Json                 | 100000   |  95,982.4 μs |  1.34 |  1166.6667 |  666.6667 | 166.6667 |   12208.2 KB |       1.561 |
| WriteJsonAsync_Array_ScalarPayload | 100000   |  98,459.2 μs |  1.37 | 14000.0000 |         - |        - | 114855.03 KB |      14.688 |
| EFCore_ToList_Json                 | 100000   | 109,588.0 μs |  1.53 |  3800.0000 | 1400.0000 | 400.0000 |  30968.74 KB |       3.960 |
| WriteJson_Array_WideDto            | 100000   | 121,776.9 μs |  1.70 | 14000.0000 |         - |        - | 114886.92 KB |      14.692 |
