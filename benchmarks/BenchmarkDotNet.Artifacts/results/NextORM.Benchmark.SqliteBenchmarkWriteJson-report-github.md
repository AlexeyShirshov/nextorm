```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                             | RowCount | Mean         | Median       | Ratio | Gen0       | Gen1      | Gen2     | Allocated    | Alloc Ratio |
|----------------------------------- |--------- |-------------:|-------------:|------:|-----------:|----------:|---------:|-------------:|------------:|
| WriteJson_Array_Scalar             | 1000     |     369.7 μs |     369.3 μs |  0.69 |          - |         - |        - |      9.71 KB |        0.12 |
| WriteJsonAsync_Array_Scalar        | 1000     |     422.0 μs |     417.4 μs |  0.79 |     0.9766 |    0.4883 |        - |      9.93 KB |        0.12 |
| ToList_Dto                         | 1000     |     532.7 μs |     530.2 μs |  1.00 |     9.7656 |    0.9766 |        - |      80.6 KB |        1.00 |
| Nextorm_ToList_Dto_Json            | 1000     |     651.0 μs |     647.8 μs |  1.22 |     9.7656 |    1.9531 |        - |      80.9 KB |        1.00 |
| Linq2Db_ToList_Json                | 1000     |     704.0 μs |     702.6 μs |  1.32 |    10.7422 |    1.9531 |        - |     90.37 KB |        1.12 |
| Dapper_ToList_Json                 | 1000     |     719.8 μs |     713.2 μs |  1.35 |    13.6719 |    1.9531 |        - |    119.21 KB |        1.48 |
| WriteJson_Array_ScalarPayload      | 1000     |     734.8 μs |     729.9 μs |  1.38 |   140.6250 |    0.9766 |        - |   1155.99 KB |       14.34 |
| WriteJson_Array_Dto                | 1000     |     761.5 μs |     759.9 μs |  1.43 |     5.8594 |    0.9766 |        - |     51.49 KB |        0.64 |
| EFCore_ToList_Json                 | 1000     |     810.0 μs |     804.8 μs |  1.52 |    38.0859 |    7.8125 |        - |    316.18 KB |        3.92 |
| WriteJsonAsync_Array_ScalarPayload | 1000     |     821.3 μs |     820.2 μs |  1.54 |   140.6250 |    0.9766 |        - |   1156.21 KB |       14.35 |
| WriteJson_Array_WideDto            | 1000     |   1,053.4 μs |   1,055.1 μs |  1.98 |   140.6250 |    1.9531 |        - |   1158.84 KB |       14.38 |
|                                    |          |              |              |       |            |           |          |              |             |
| WriteJsonAsync_Array_Scalar        | 10000    |   4,948.7 μs |   4,953.4 μs |  0.80 |          - |         - |        - |      9.97 KB |        0.01 |
| WriteJson_Array_Scalar             | 10000    |   5,721.1 μs |   5,710.1 μs |  0.92 |          - |         - |        - |     10.31 KB |        0.01 |
| ToList_Dto                         | 10000    |   6,215.8 μs |   6,253.6 μs |  1.00 |    93.7500 |   54.6875 |        - |    783.76 KB |        1.00 |
| Nextorm_ToList_Dto_Json            | 10000    |   7,040.5 μs |   6,994.3 μs |  1.13 |    93.7500 |   54.6875 |        - |    784.07 KB |        1.00 |
| WriteJson_Array_Dto                | 10000    |   7,424.2 μs |   7,455.3 μs |  1.20 |    46.8750 |         - |        - |    403.13 KB |        0.51 |
| WriteJson_Array_ScalarPayload      | 10000    |   8,201.0 μs |   8,144.7 μs |  1.32 |  1406.2500 |         - |        - |  11493.13 KB |       14.66 |
| Linq2Db_ToList_Json                | 10000    |   8,423.1 μs |   8,293.7 μs |  1.36 |   125.0000 |  109.3750 |  31.2500 |    963.75 KB |        1.23 |
| WriteJsonAsync_Array_ScalarPayload | 10000    |   8,612.0 μs |   8,621.4 μs |  1.39 |  1406.2500 |         - |        - |  11493.34 KB |       14.66 |
| Dapper_ToList_Json                 | 10000    |   8,835.7 μs |   8,821.7 μs |  1.42 |   156.2500 |  109.3750 |  31.2500 |   1273.72 KB |        1.63 |
| EFCore_ToList_Json                 | 10000    |  10,377.1 μs |  10,378.2 μs |  1.67 |   406.2500 |  171.8750 |  46.8750 |   3158.27 KB |        4.03 |
| WriteJson_Array_WideDto            | 10000    |  10,384.0 μs |  10,400.0 μs |  1.67 |  1406.2500 |         - |        - |  11495.97 KB |       14.67 |
|                                    |          |              |              |       |            |           |          |              |             |
| WriteJson_Array_Scalar             | 100000   |  46,063.7 μs |  46,064.3 μs |  0.66 |          - |         - |        - |     10.28 KB |       0.001 |
| WriteJsonAsync_Array_Scalar        | 100000   |  50,781.3 μs |  50,750.7 μs |  0.73 |          - |         - |        - |     10.54 KB |       0.001 |
| ToList_Dto                         | 100000   |  69,474.0 μs |  69,481.4 μs |  1.00 |  1142.8571 | 1000.0000 | 285.7143 |   7818.54 KB |       1.000 |
| Nextorm_ToList_Dto_Json            | 100000   |  76,282.3 μs |  76,341.6 μs |  1.10 |  1142.8571 | 1000.0000 | 285.7143 |   7818.78 KB |       1.000 |
| WriteJson_Array_Dto                | 100000   |  76,869.4 μs |  76,890.0 μs |  1.11 |   428.5714 |         - |        - |   3919.77 KB |       0.501 |
| WriteJson_Array_ScalarPayload      | 100000   |  79,143.3 μs |  78,722.8 μs |  1.14 | 14000.0000 |         - |        - | 114851.99 KB |      14.690 |
| Linq2Db_ToList_Json                | 100000   |  82,231.0 μs |  81,983.9 μs |  1.18 |  1142.8571 | 1000.0000 | 285.7143 |   9086.06 KB |       1.162 |
| WriteJsonAsync_Array_ScalarPayload | 100000   |  86,529.0 μs |  86,788.6 μs |  1.25 | 14000.0000 |         - |        - | 114852.33 KB |      14.690 |
| Dapper_ToList_Json                 | 100000   |  89,808.0 μs |  89,816.6 μs |  1.29 |  1166.6667 |  666.6667 | 166.6667 |  12208.25 KB |       1.561 |
| EFCore_ToList_Json                 | 100000   | 101,281.4 μs | 101,108.0 μs |  1.46 |  4000.0000 | 1500.0000 | 500.0000 |  30970.09 KB |       3.961 |
| WriteJson_Array_WideDto            | 100000   | 106,352.3 μs | 105,917.0 μs |  1.53 | 14000.0000 |         - |        - | 114855.15 KB |      14.690 |
