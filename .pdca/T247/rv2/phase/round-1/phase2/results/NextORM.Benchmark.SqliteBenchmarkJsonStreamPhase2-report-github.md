```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  Categories=json-stream-phase2  

```
| Method                            | RowCount | Mean        | Ratio | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|---------------------------------- |--------- |------------:|------:|---------:|---------:|---------:|-----------:|------------:|
| Flat_MaterializeStj               | 1000     |    426.4 μs |  1.00 |   9.7656 |   0.9766 |        - |   87.68 KB |        1.00 |
| ConditionalNullArm_MaterializeStj | 1000     |    464.4 μs |  1.09 |  16.6016 |   1.9531 |        - |  140.99 KB |        1.61 |
| ByteArrayBase64_MaterializeStj    | 1000     |    485.2 μs |  1.14 |  28.3203 |  28.3203 |  28.3203 |  185.36 KB |        2.11 |
| Nested_MaterializeStj             | 1000     |    665.4 μs |  1.56 |  21.4844 |   3.9063 |        - |  188.13 KB |        2.15 |
| ByteArrayBase64_Stream            | 1000     |    789.6 μs |  1.85 |  11.7188 |   3.9063 |        - |  100.37 KB |        1.14 |
| JoinedSlots_MaterializeStj        | 1000     |    820.8 μs |  1.93 |  15.6250 |        - |        - |  144.07 KB |        1.64 |
| Flat_Stream                       | 1000     |  2,144.6 μs |  5.03 |   7.8125 |   3.9063 |        - |   64.21 KB |        0.73 |
| ConditionalNullArm_Stream         | 1000     |  2,641.5 μs |  6.20 |        - |        - |        - |   50.78 KB |        0.58 |
| Nested_Stream                     | 1000     |  3,226.6 μs |  7.57 |   7.8125 |        - |        - |   96.46 KB |        1.10 |
| JoinedSlots_Stream                | 1000     |  4,761.9 μs | 11.17 |  15.6250 |   7.8125 |        - |  160.62 KB |        1.83 |
|                                   |          |             |       |          |          |          |            |             |
| Flat_MaterializeStj               | 10000    |  4,297.7 μs |  1.00 | 132.8125 |  93.7500 |  62.5000 |  857.12 KB |        1.00 |
| ConditionalNullArm_MaterializeStj | 10000    |  4,668.0 μs |  1.09 | 203.1250 | 140.6250 |  70.3125 | 1384.94 KB |        1.62 |
| ByteArrayBase64_Stream            | 10000    |  5,132.8 μs |  1.19 |  93.7500 |        - |        - |  873.84 KB |        1.02 |
| ByteArrayBase64_MaterializeStj    | 10000    |  5,348.1 μs |  1.24 | 304.6875 | 234.3750 | 195.3125 |  1829.3 KB |        2.13 |
| Nested_MaterializeStj             | 10000    |  6,836.4 μs |  1.59 | 281.2500 | 234.3750 | 117.1875 | 1863.13 KB |        2.17 |
| Flat_Stream                       | 10000    |  7,045.5 μs |  1.64 |  15.6250 |        - |        - |  239.64 KB |        0.28 |
| ConditionalNullArm_Stream         | 10000    |  7,208.1 μs |  1.68 |        - |        - |        - |   50.81 KB |        0.06 |
| JoinedSlots_MaterializeStj        | 10000    |  9,107.1 μs |  2.12 | 218.7500 | 171.8750 | 109.3750 | 1461.55 KB |        1.71 |
| Nested_Stream                     | 10000    |  9,848.5 μs |  2.29 |  31.2500 |        - |        - |  271.89 KB |        0.32 |
| JoinedSlots_Stream                | 10000    | 14,222.1 μs |  3.31 |  62.5000 |        - |        - |  576.94 KB |        0.67 |
