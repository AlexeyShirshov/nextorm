```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                             | RowCount | Mean         | Median       | Ratio | Gen0       | Gen1      | Gen2     | Allocated    | Alloc Ratio |
|----------------------------------- |--------- |-------------:|-------------:|------:|-----------:|----------:|---------:|-------------:|------------:|
| WriteJson_Array_Scalar             | 1000     |     299.9 μs |     297.5 μs |  0.72 |     0.9766 |    0.4883 |        - |      9.71 KB |        0.12 |
| WriteJsonAsync_Array_Scalar        | 1000     |     325.5 μs |     324.7 μs |  0.79 |     0.9766 |    0.4883 |        - |      9.87 KB |        0.12 |
| ToList_Dto                         | 1000     |     414.6 μs |     414.9 μs |  1.00 |     9.7656 |    1.4648 |        - |      80.6 KB |        1.00 |
| Nextorm_ToList_Dto_Json            | 1000     |     502.8 μs |     502.6 μs |  1.21 |     9.7656 |    1.9531 |        - |      80.9 KB |        1.00 |
| Linq2Db_ToList_Json                | 1000     |     549.7 μs |     549.5 μs |  1.33 |    10.7422 |    1.9531 |        - |     90.37 KB |        1.12 |
| Dapper_ToList_Json                 | 1000     |     554.4 μs |     553.7 μs |  1.34 |    13.6719 |    1.9531 |        - |    119.21 KB |        1.48 |
| WriteJson_Array_Dto                | 1000     |     594.0 μs |     595.9 μs |  1.43 |     5.8594 |    0.9766 |        - |     51.49 KB |        0.64 |
| WriteJson_Array_ScalarPayload      | 1000     |     633.6 μs |     628.6 μs |  1.53 |   140.6250 |    0.9766 |        - |   1155.99 KB |       14.34 |
| EFCore_ToList_Json                 | 1000     |     641.6 μs |     641.1 μs |  1.55 |    38.0859 |    7.8125 |        - |    316.18 KB |        3.92 |
| WriteJsonAsync_Array_ScalarPayload | 1000     |     668.5 μs |     662.5 μs |  1.61 |   140.6250 |    0.9766 |        - |   1156.15 KB |       14.34 |
| WriteJson_Array_WideDto            | 1000     |     858.1 μs |     857.3 μs |  2.07 |   141.6016 |    0.9766 |        - |   1158.84 KB |       14.38 |
|                                    |          |              |              |       |            |           |          |              |             |
| WriteJson_Array_Scalar             | 10000    |   4,780.7 μs |   4,776.1 μs |  0.74 |          - |         - |        - |      9.76 KB |        0.01 |
| WriteJsonAsync_Array_Scalar        | 10000    |   5,113.1 μs |   5,086.3 μs |  0.79 |          - |         - |        - |      9.92 KB |        0.01 |
| ToList_Dto                         | 10000    |   6,446.5 μs |   6,464.4 μs |  1.00 |    93.7500 |   54.6875 |        - |    783.76 KB |        1.00 |
| Nextorm_ToList_Dto_Json            | 10000    |   7,419.7 μs |   7,432.0 μs |  1.15 |    93.7500 |   54.6875 |        - |    784.07 KB |        1.00 |
| WriteJson_Array_Dto                | 10000    |   7,880.6 μs |   7,831.3 μs |  1.22 |    46.8750 |         - |        - |    403.18 KB |        0.51 |
| Linq2Db_ToList_Json                | 10000    |   8,379.2 μs |   8,398.8 μs |  1.30 |   125.0000 |  109.3750 |  31.2500 |    963.72 KB |        1.23 |
| WriteJson_Array_ScalarPayload      | 10000    |   8,707.5 μs |   8,748.2 μs |  1.35 |  1406.2500 |         - |        - |  11493.13 KB |       14.66 |
| WriteJsonAsync_Array_ScalarPayload | 10000    |   9,174.3 μs |   9,159.0 μs |  1.42 |  1406.2500 |         - |        - |  11493.28 KB |       14.66 |
| Dapper_ToList_Json                 | 10000    |   9,493.9 μs |   9,455.2 μs |  1.47 |   156.2500 |  109.3750 |  31.2500 |   1273.76 KB |        1.63 |
| EFCore_ToList_Json                 | 10000    |  10,812.4 μs |  10,797.9 μs |  1.68 |   406.2500 |  171.8750 |  46.8750 |   3158.23 KB |        4.03 |
| WriteJson_Array_WideDto            | 10000    |  10,993.8 μs |  11,030.9 μs |  1.71 |  1406.2500 |         - |        - |  11495.97 KB |       14.67 |
|                                    |          |              |              |       |            |           |          |              |             |
| WriteJson_Array_Scalar             | 100000   |  47,689.2 μs |  47,728.3 μs |  0.64 |          - |         - |        - |     10.28 KB |       0.001 |
| WriteJsonAsync_Array_Scalar        | 100000   |  51,512.7 μs |  51,362.4 μs |  0.69 |          - |         - |        - |     10.49 KB |       0.001 |
| ToList_Dto                         | 100000   |  74,700.7 μs |  72,185.3 μs |  1.00 |  1000.0000 |  875.0000 | 250.0000 |   7817.84 KB |       1.000 |
| WriteJson_Array_Dto                | 100000   |  75,870.6 μs |  75,718.9 μs |  1.02 |   428.5714 |         - |        - |   3919.77 KB |       0.501 |
| Nextorm_ToList_Dto_Json            | 100000   |  79,427.8 μs |  79,475.5 μs |  1.07 |  1142.8571 | 1000.0000 | 285.7143 |   7818.32 KB |       1.000 |
| Linq2Db_ToList_Json                | 100000   |  84,620.5 μs |  83,906.5 μs |  1.14 |  1142.8571 | 1000.0000 | 285.7143 |    9086.6 KB |       1.162 |
| WriteJson_Array_ScalarPayload      | 100000   |  85,462.7 μs |  85,757.0 μs |  1.15 | 14000.0000 |         - |        - | 114852.12 KB |      14.691 |
| WriteJsonAsync_Array_ScalarPayload | 100000   |  90,119.1 μs |  89,725.4 μs |  1.21 | 14000.0000 |         - |        - | 114851.35 KB |      14.691 |
| Dapper_ToList_Json                 | 100000   |  92,108.3 μs |  92,095.7 μs |  1.24 |  1166.6667 |  666.6667 | 166.6667 |  12207.91 KB |       1.562 |
| EFCore_ToList_Json                 | 100000   | 105,295.6 μs | 105,370.8 μs |  1.42 |  3800.0000 | 1400.0000 | 400.0000 |   30969.4 KB |       3.961 |
| WriteJson_Array_WideDto            | 100000   | 108,588.8 μs | 108,142.9 μs |  1.46 | 14000.0000 |         - |        - | 114855.15 KB |      14.691 |
