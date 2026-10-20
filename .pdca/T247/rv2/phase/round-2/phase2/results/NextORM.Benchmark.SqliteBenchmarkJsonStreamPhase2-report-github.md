```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  Categories=json-stream-phase2  

```
| Method                            | RowCount | Mean        | Median      | Ratio | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|---------------------------------- |--------- |------------:|------------:|------:|---------:|---------:|---------:|-----------:|------------:|
| ConditionalNullArm_MaterializeStj | 1000     |    467.2 μs |    465.1 μs |  0.96 |  16.6016 |   1.9531 |        - |  140.99 KB |        1.61 |
| Flat_MaterializeStj               | 1000     |    494.9 μs |    476.9 μs |  1.02 |   9.7656 |   0.9766 |        - |   87.68 KB |        1.00 |
| ByteArrayBase64_MaterializeStj    | 1000     |    511.2 μs |    497.9 μs |  1.05 |  28.3203 |  28.3203 |  28.3203 |  185.36 KB |        2.11 |
| Nested_MaterializeStj             | 1000     |    653.6 μs |    655.2 μs |  1.34 |  19.5313 |   3.9063 |        - |  188.13 KB |        2.15 |
| ByteArrayBase64_Stream            | 1000     |    948.6 μs |    918.9 μs |  1.95 |  11.7188 |   3.9063 |        - |  100.37 KB |        1.14 |
| JoinedSlots_MaterializeStj        | 1000     |  1,148.4 μs |  1,060.3 μs |  2.36 |  15.6250 |        - |        - |  144.07 KB |        1.64 |
| ConditionalNullArm_Stream         | 1000     |  2,636.9 μs |  2,599.3 μs |  5.41 |   3.9063 |        - |        - |   50.91 KB |        0.58 |
| Flat_Stream                       | 1000     |  2,677.3 μs |  2,582.2 μs |  5.49 |   7.8125 |        - |        - |    65.1 KB |        0.74 |
| Nested_Stream                     | 1000     |  3,871.8 μs |  3,729.7 μs |  7.95 |   7.8125 |        - |        - |   97.24 KB |        1.11 |
| JoinedSlots_Stream                | 1000     |  4,794.1 μs |  4,749.9 μs |  9.84 |  15.6250 |   7.8125 |        - |  160.78 KB |        1.83 |
|                                   |          |             |             |       |          |          |          |            |             |
| Flat_MaterializeStj               | 10000    |  4,229.3 μs |  4,234.1 μs |  1.00 | 132.8125 |  93.7500 |  62.5000 |  857.04 KB |        1.00 |
| ByteArrayBase64_MaterializeStj    | 10000    |  4,814.1 μs |  4,832.7 μs |  1.14 | 304.6875 | 242.1875 | 195.3125 | 1829.23 KB |        2.13 |
| ByteArrayBase64_Stream            | 10000    |  4,935.5 μs |  4,917.7 μs |  1.17 |  93.7500 |        - |        - |  873.84 KB |        1.02 |
| ConditionalNullArm_MaterializeStj | 10000    |  5,020.8 μs |  5,008.8 μs |  1.19 | 203.1250 | 140.6250 |  70.3125 | 1385.03 KB |        1.62 |
| Nested_MaterializeStj             | 10000    |  6,862.8 μs |  6,837.7 μs |  1.62 | 281.2500 | 234.3750 | 117.1875 | 1862.85 KB |        2.17 |
| Flat_Stream                       | 10000    |  6,941.7 μs |  6,922.3 μs |  1.64 |  15.6250 |        - |        - |  240.71 KB |        0.28 |
| ConditionalNullArm_Stream         | 10000    |  7,121.4 μs |  7,172.7 μs |  1.68 |        - |        - |        - |   50.73 KB |        0.06 |
| JoinedSlots_MaterializeStj        | 10000    |  9,179.5 μs |  9,116.7 μs |  2.17 | 218.7500 | 171.8750 | 109.3750 | 1461.67 KB |        1.71 |
| Nested_Stream                     | 10000    |  9,943.5 μs |  9,879.6 μs |  2.35 |  31.2500 |        - |        - |  272.53 KB |        0.32 |
| JoinedSlots_Stream                | 10000    | 14,813.4 μs | 14,761.9 μs |  3.50 |  62.5000 |        - |        - |  579.19 KB |        0.68 |
