```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean            | Error          | StdDev        | Ratio | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------------:|---------------:|--------------:|------:|--------:|-------:|----------:|------------:|
| Generic_ParamsReadOnlySpan       |       0.7768 ns |      0.0314 ns |     0.0017 ns | 0.000 |       - |      - |         - |        0.00 |
| Generic_ParamsArray              |       0.9693 ns |      0.0040 ns |     0.0002 ns | 0.000 |       - |      - |         - |        0.00 |
| GetDbCommand_1Arg_ReusedArray    |   1,406.9119 ns |    695.9630 ns |    38.1481 ns | 0.001 |  0.2861 |      - |    2400 B |        0.03 |
| GetDbCommand_1Arg_Params         |   1,486.5062 ns |    210.2515 ns |    11.5246 ns | 0.002 |  0.6695 |      - |    5600 B |        0.06 |
| EntityAnyCommand_Build           |   1,607.2081 ns |    397.7486 ns |    21.8019 ns | 0.002 |  0.2842 |      - |    2384 B |        0.03 |
| EntityAnyCommand_BuildAndPrepare |   5,903.8146 ns |    688.6075 ns |    37.7449 ns | 0.006 |  0.6332 | 0.0076 |    5305 B |        0.06 |
| Nextorm_EntityAny_1Arg_Span      |  13,012.7038 ns |    451.8499 ns |    24.7674 ns | 0.014 |  0.3204 |      - |    2800 B |        0.03 |
| Nextorm_EntityAny_1Arg_Array     |  13,114.2902 ns |  4,761.7563 ns |   261.0079 ns | 0.014 |  0.3357 |      - |    2832 B |        0.03 |
| Nextorm_EntityAny_3Arg_Array     |  15,919.5728 ns |  1,423.7004 ns |    78.0378 ns | 0.017 |  0.5493 |      - |    4800 B |        0.06 |
| Nextorm_EntityAny_3Arg_Span      |  16,010.2988 ns |  1,369.0167 ns |    75.0404 ns | 0.017 |  0.5493 |      - |    4752 B |        0.05 |
| Nextorm_Any_1Arg_ReusedArray     | 942,202.2205 ns | 39,937.7287 ns | 2,189.1217 ns | 0.995 |  9.7656 |      - |   84008 B |        0.96 |
| Nextorm_Any_1Arg_Params          | 946,686.6616 ns | 78,683.2998 ns | 4,312.8972 ns | 1.000 |  9.7656 |      - |   87208 B |        1.00 |
| Nextorm_Any_2Arg_Params          | 981,416.7656 ns | 89,646.0333 ns | 4,913.8016 ns | 1.037 | 13.6719 |      - |  124015 B |        1.42 |
| Nextorm_Any_2Arg_ReusedArray     | 984,691.2585 ns | 66,117.7479 ns | 3,624.1369 ns | 1.040 | 13.6719 |      - |  120015 B |        1.38 |
