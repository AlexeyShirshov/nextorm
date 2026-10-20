```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=json-stream-phase2  

```
| Method                            | RowCount | Mean        | Ratio | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|---------------------------------- |--------- |------------:|------:|---------:|---------:|---------:|-----------:|------------:|
| Flat_MaterializeStj               | 1000     |    439.0 μs |  1.00 |  10.7422 |   0.9766 |        - |   88.41 KB |        1.00 |
| ConditionalNullArm_MaterializeStj | 1000     |    483.0 μs |  1.10 |  17.0898 |   2.9297 |        - |  141.72 KB |        1.60 |
| ByteArrayBase64_MaterializeStj    | 1000     |    501.2 μs |  1.14 |  28.3203 |  28.3203 |  28.3203 |  186.32 KB |        2.11 |
| Nested_MaterializeStj             | 1000     |    636.5 μs |  1.45 |  22.4609 |   4.8828 |        - |   189.1 KB |        2.14 |
| ByteArrayBase64_Stream            | 1000     |    810.4 μs |  1.85 |  11.7188 |   3.9063 |        - |  101.03 KB |        1.14 |
| JoinedSlots_MaterializeStj        | 1000     |    844.3 μs |  1.92 |  17.5781 |        - |        - |  148.59 KB |        1.68 |
| Flat_Stream                       | 1000     |  2,146.1 μs |  4.89 |   7.8125 |   3.9063 |        - |   64.24 KB |        0.73 |
| ConditionalNullArm_Stream         | 1000     |  2,858.9 μs |  6.52 |   3.9063 |        - |        - |   50.68 KB |        0.57 |
| Nested_Stream                     | 1000     |  3,568.1 μs |  8.13 |  11.7188 |   7.8125 |        - |   96.62 KB |        1.09 |
| JoinedSlots_Stream                | 1000     |  5,703.7 μs | 13.00 |  15.6250 |   7.8125 |        - |  159.71 KB |        1.81 |
|                                   |          |             |       |          |          |          |            |             |
| ByteArrayBase64_MaterializeStj    | 10000    |  4,371.9 μs |  0.91 | 296.8750 | 250.0000 | 187.5000 | 1831.45 KB |        2.13 |
| ByteArrayBase64_Stream            | 10000    |  4,725.3 μs |  0.98 | 101.5625 |        - |        - |  874.52 KB |        1.02 |
| Flat_MaterializeStj               | 10000    |  4,840.3 μs |  1.01 | 132.8125 |  93.7500 |  62.5000 |  858.15 KB |        1.00 |
| ConditionalNullArm_MaterializeStj | 10000    |  4,949.1 μs |  1.03 | 203.1250 | 148.4375 |  70.3125 | 1386.17 KB |        1.62 |
| Nested_MaterializeStj             | 10000    |  7,108.9 μs |  1.48 | 281.2500 | 226.5625 | 117.1875 | 1864.69 KB |        2.17 |
| Flat_Stream                       | 10000    |  7,278.3 μs |  1.51 |  23.4375 |   7.8125 |        - |   240.1 KB |        0.28 |
| ConditionalNullArm_Stream         | 10000    |  7,898.1 μs |  1.64 |        - |        - |        - |   50.77 KB |        0.06 |
| JoinedSlots_MaterializeStj        | 10000    |  8,511.6 μs |  1.77 | 218.7500 | 171.8750 | 109.3750 | 1466.17 KB |        1.71 |
| Nested_Stream                     | 10000    | 10,168.7 μs |  2.11 |  31.2500 |  15.6250 |        - |  272.55 KB |        0.32 |
| JoinedSlots_Stream                | 10000    | 13,908.6 μs |  2.89 |  62.5000 |  15.6250 |        - |  577.86 KB |        0.67 |
