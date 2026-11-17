```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Toolchain=InProcessEmitToolchain  Categories=csv  

```
| Method                            | Mean      | Ratio | Gen0     | Gen1     | Gen2    | Allocated  | Alloc Ratio |
|---------------------------------- |----------:|------:|---------:|---------:|--------:|-----------:|------------:|
| Nextorm_StringProjection_ToList   |  2.763 ms |  0.33 | 121.0938 |  70.3125 |       - | 1018.12 KB |        0.45 |
| Nextorm_StringProjection_WriteCsv |  3.469 ms |  0.42 | 113.2813 |        - |       - |   949.1 KB |        0.42 |
| Nextorm_ToList                    |  8.336 ms |  1.00 | 265.6250 | 156.2500 |       - | 2266.74 KB |        1.00 |
| Nextorm_ToList_ManualCsv          |  9.694 ms |  1.16 | 531.2500 | 453.1250 |       - | 4375.43 KB |        1.93 |
| Nextorm_WriteCsv                  | 10.095 ms |  1.21 | 203.1250 |        - |       - | 1733.92 KB |        0.76 |
| Linq2Db_ToList_ManualCsv          | 12.945 ms |  1.55 | 562.5000 | 281.2500 | 78.1250 | 4087.58 KB |        1.80 |
| Dapper_ToList_ManualCsv           | 13.250 ms |  1.59 | 687.5000 | 359.3750 | 93.7500 | 5100.61 KB |        2.25 |
| EFCore_ToList_ManualCsv           | 14.824 ms |  1.78 | 890.6250 | 359.3750 | 93.7500 | 6751.01 KB |        2.98 |
