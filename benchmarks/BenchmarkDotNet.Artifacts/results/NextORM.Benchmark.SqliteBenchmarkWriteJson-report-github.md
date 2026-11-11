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
| WriteJson_Array_Scalar             | 1000     |     327.7 μs |  0.79 |     0.9766 |    0.4883 |        - |      9.79 KB |        0.12 |
| WriteJsonAsync_Array_Scalar        | 1000     |     351.0 μs |  0.85 |     0.9766 |    0.4883 |        - |      9.94 KB |        0.12 |
| ToList_Dto                         | 1000     |     415.1 μs |  1.00 |     9.7656 |    1.9531 |        - |     81.86 KB |        1.00 |
| Nextorm_ToList_Dto_Json            | 1000     |     506.7 μs |  1.22 |     9.7656 |    1.9531 |        - |     82.17 KB |        1.00 |
| Linq2Db_ToList_Json                | 1000     |     566.8 μs |  1.37 |    10.7422 |    1.9531 |        - |      90.5 KB |        1.11 |
| Dapper_ToList_Json                 | 1000     |     575.0 μs |  1.39 |    13.6719 |    1.9531 |        - |    119.22 KB |        1.46 |
| WriteJson_Array_Dto                | 1000     |     640.2 μs |  1.54 |     5.8594 |    0.9766 |        - |     51.57 KB |        0.63 |
| WriteJson_Array_ScalarPayload      | 1000     |     709.3 μs |  1.71 |   140.6250 |    0.9766 |        - |   1156.07 KB |       14.12 |
| EFCore_ToList_Json                 | 1000     |     724.4 μs |  1.75 |    38.0859 |    6.8359 |        - |    316.65 KB |        3.87 |
| WriteJsonAsync_Array_ScalarPayload | 1000     |     779.5 μs |  1.88 |   140.6250 |    0.9766 |        - |   1156.22 KB |       14.12 |
| WriteJson_Array_WideDto            | 1000     |     969.8 μs |  2.34 |   141.6016 |    0.9766 |        - |   1158.92 KB |       14.16 |
|                                    |          |              |       |            |           |          |              |             |
| WriteJsonAsync_Array_Scalar        | 10000    |   5,452.9 μs |  0.88 |          - |         - |        - |      9.98 KB |        0.01 |
| ToList_Dto                         | 10000    |   6,196.4 μs |  1.00 |    93.7500 |   54.6875 |        - |    785.02 KB |        1.00 |
| WriteJson_Array_Scalar             | 10000    |   6,294.1 μs |  1.02 |          - |         - |        - |      9.83 KB |        0.01 |
| Nextorm_ToList_Dto_Json            | 10000    |   7,261.4 μs |  1.17 |    93.7500 |   54.6875 |        - |    785.33 KB |        1.00 |
| WriteJson_Array_Dto                | 10000    |   8,302.0 μs |  1.34 |    46.8750 |         - |        - |    403.24 KB |        0.51 |
| Linq2Db_ToList_Json                | 10000    |   8,793.1 μs |  1.42 |   125.0000 |  109.3750 |  31.2500 |    963.73 KB |        1.23 |
| Dapper_ToList_Json                 | 10000    |   9,459.1 μs |  1.53 |   156.2500 |  109.3750 |  31.2500 |   1273.77 KB |        1.62 |
| WriteJsonAsync_Array_ScalarPayload | 10000    |   9,720.8 μs |  1.57 |  1406.2500 |         - |        - |  11493.34 KB |       14.64 |
| WriteJson_Array_ScalarPayload      | 10000    |  10,431.7 μs |  1.68 |  1406.2500 |         - |        - |  11493.19 KB |       14.64 |
| EFCore_ToList_Json                 | 10000    |  10,952.7 μs |  1.77 |   406.2500 |  171.8750 |  46.8750 |   3158.63 KB |        4.02 |
| WriteJson_Array_WideDto            | 10000    |  12,280.9 μs |  1.98 |  1406.2500 |         - |        - |  11496.04 KB |       14.64 |
|                                    |          |              |       |            |           |          |              |             |
| WriteJson_Array_Scalar             | 100000   |  50,285.1 μs |  0.69 |          - |         - |        - |     10.55 KB |       0.001 |
| WriteJsonAsync_Array_Scalar        | 100000   |  55,323.8 μs |  0.76 |          - |         - |        - |      10.7 KB |       0.001 |
| ToList_Dto                         | 100000   |  72,732.9 μs |  1.00 |  1142.8571 | 1000.0000 | 285.7143 |   7819.54 KB |       1.000 |
| Nextorm_ToList_Dto_Json            | 100000   |  82,436.1 μs |  1.13 |  1142.8571 | 1000.0000 | 285.7143 |   7819.17 KB |       1.000 |
| WriteJson_Array_Dto                | 100000   |  83,936.3 μs |  1.15 |   428.5714 |         - |        - |   3919.75 KB |       0.501 |
| Linq2Db_ToList_Json                | 100000   |  88,689.4 μs |  1.22 |  1166.6667 | 1000.0000 | 333.3333 |   9088.27 KB |       1.162 |
| WriteJson_Array_ScalarPayload      | 100000   |  89,048.6 μs |  1.22 | 14000.0000 |         - |        - | 114852.09 KB |      14.688 |
| WriteJsonAsync_Array_ScalarPayload | 100000   |  94,995.0 μs |  1.31 | 14000.0000 |         - |        - |  114852.4 KB |      14.688 |
| Dapper_ToList_Json                 | 100000   |  95,246.2 μs |  1.31 |  1200.0000 |  600.0000 | 200.0000 |  12208.61 KB |       1.561 |
| EFCore_ToList_Json                 | 100000   | 112,833.5 μs |  1.55 |  3800.0000 | 1400.0000 | 400.0000 |  30970.28 KB |       3.961 |
| WriteJson_Array_WideDto            | 100000   | 117,369.6 μs |  1.61 | 14000.0000 |         - |        - |  114855.1 KB |      14.688 |
