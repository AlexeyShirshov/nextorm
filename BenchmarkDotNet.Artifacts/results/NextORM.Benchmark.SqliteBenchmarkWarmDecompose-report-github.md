```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                         | Toolchain              | Mean          | Ratio | Gen0     | Gen1   | Allocated | Alloc Ratio |
|------------------------------- |----------------------- |--------------:|------:|---------:|-------:|----------:|------------:|
| RecursiveCte_Warm_Reused       | Default                |      4.601 μs | 0.002 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused              | Default                |      4.645 μs | 0.002 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                | Default                |      5.001 μs | 0.002 |        - |      - |         - |        0.00 |
| InAtIn_Inline_Warm_Reused      | Default                |     67.378 μs | 0.031 |   7.8125 |      - |   67200 B |        0.08 |
| InAtIn_Captured_Construct      | Default                |    658.671 μs | 0.302 |  36.1328 |      - |  307200 B |        0.36 |
| InAtIn_Inline_Construct        | Default                |    699.044 μs | 0.321 |  38.0859 |      - |  326400 B |        0.38 |
| InAtIn_Captured_Prepare_NoHash | Default                |  1,273.758 μs | 0.585 |  52.7344 |      - |  441601 B |        0.52 |
| InAtIn_Inline_Prepare_NoHash   | Default                |  1,565.999 μs | 0.719 |  56.6406 |      - |  476000 B |        0.56 |
| Cte_Construct                  | Default                |  2,178.702 μs | 1.000 | 101.5625 |      - |  855271 B |        1.00 |
| RecursiveCte_Construct         | Default                |  2,240.188 μs | 1.028 | 119.1406 | 1.9531 | 1011220 B |        1.18 |
| InAtIn_Captured_Prepare_Hash   | Default                |  2,375.003 μs | 1.090 |  66.4063 |      - |  556000 B |        0.65 |
| Join4_Construct                | Default                |  2,571.929 μs | 1.181 | 101.5625 |      - |  868889 B |        1.02 |
| InAtIn_Inline_Prepare_Hash     | Default                |  3,025.557 μs | 1.389 |  66.4063 |      - |  568000 B |        0.66 |
| RecursiveCte_Prepare_NoHash    | Default                |  4,835.120 μs | 2.220 | 156.2500 | 3.9063 | 1331227 B |        1.56 |
| Join4_Prepare_NoHash           | Default                |  5,084.233 μs | 2.334 | 132.8125 |      - | 1122518 B |        1.31 |
| InAtIn_Inline_Warm_PlanOnly    | Default                |  5,096.524 μs | 2.340 |  66.4063 |      - |  584000 B |        0.68 |
| Cte_Prepare_NoHash             | Default                |  5,189.506 μs | 2.382 | 140.6250 |      - | 1207300 B |        1.41 |
| RecursiveCte_Prepare_Hash      | Default                |  7,268.310 μs | 3.336 | 171.8750 |      - | 1475230 B |        1.72 |
| Join4_Prepare_Hash             | Default                |  7,377.881 μs | 3.387 | 132.8125 |      - | 1149718 B |        1.34 |
| Cte_Prepare_Hash               | Default                |  7,822.143 μs | 3.591 | 148.4375 |      - | 1299305 B |        1.52 |
| Join4_Warm_PlanOnly            | Default                |  8,557.456 μs | 3.928 | 125.0000 |      - | 1155318 B |        1.35 |
| RecursiveCte_Warm_PlanOnly     | Default                | 10,383.582 μs | 4.766 | 171.8750 |      - | 1480830 B |        1.73 |
| Cte_Warm_PlanOnly              | Default                | 10,879.647 μs | 4.994 | 156.2500 |      - | 1316116 B |        1.54 |
|                                |                        |               |       |          |        |           |             |
| RecursiveCte_Warm_Reused       | InProcessEmitToolchain |      6.699 μs | 0.009 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused              | InProcessEmitToolchain |      7.031 μs | 0.009 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                | InProcessEmitToolchain |      8.432 μs | 0.011 |        - |      - |         - |        0.00 |
| InAtIn_Inline_Warm_Reused      | InProcessEmitToolchain |    118.987 μs | 0.153 |   7.9346 |      - |   67200 B |        0.08 |
| InAtIn_Captured_Construct      | InProcessEmitToolchain |    229.633 μs | 0.294 |  36.6211 |      - |  307200 B |        0.35 |
| InAtIn_Inline_Construct        | InProcessEmitToolchain |    241.983 μs | 0.310 |  38.5742 |      - |  326400 B |        0.37 |
| InAtIn_Captured_Prepare_NoHash | InProcessEmitToolchain |    385.571 μs | 0.494 |  52.7344 |      - |  441600 B |        0.50 |
| InAtIn_Inline_Prepare_NoHash   | InProcessEmitToolchain |    418.567 μs | 0.537 |  54.6875 |      - |  460800 B |        0.52 |
| InAtIn_Captured_Prepare_Hash   | InProcessEmitToolchain |    552.339 μs | 0.708 |  64.4531 |      - |  544000 B |        0.62 |
| RecursiveCte_Construct         | InProcessEmitToolchain |    608.216 μs | 0.780 | 120.1172 |      - | 1008020 B |        1.15 |
| InAtIn_Inline_Prepare_Hash     | InProcessEmitToolchain |    732.691 μs | 0.940 |  67.3828 |      - |  568000 B |        0.65 |
| Cte_Construct                  | InProcessEmitToolchain |    779.937 μs | 1.000 | 104.4922 |      - |  879271 B |        1.00 |
| Join4_Construct                | InProcessEmitToolchain |    847.368 μs | 1.087 | 103.5156 |      - |  868888 B |        0.99 |
| InAtIn_Inline_Warm_PlanOnly    | InProcessEmitToolchain |    910.970 μs | 1.168 |  69.3359 |      - |  584000 B |        0.66 |
| RecursiveCte_Prepare_NoHash    | InProcessEmitToolchain |  1,007.572 μs | 1.292 | 158.2031 |      - | 1328027 B |        1.51 |
| RecursiveCte_Prepare_Hash      | InProcessEmitToolchain |  1,265.230 μs | 1.622 | 175.7813 | 1.9531 | 1472030 B |        1.67 |
| Cte_Prepare_NoHash             | InProcessEmitToolchain |  1,286.941 μs | 1.650 | 144.5313 |      - | 1224898 B |        1.39 |
| Join4_Prepare_NoHash           | InProcessEmitToolchain |  1,347.624 μs | 1.728 | 130.8594 |      - | 1104112 B |        1.26 |
| Join4_Prepare_Hash             | InProcessEmitToolchain |  1,503.231 μs | 1.928 | 134.7656 | 1.9531 | 1135315 B |        1.29 |
| RecursiveCte_Warm_PlanOnly     | InProcessEmitToolchain |  1,567.890 μs | 2.011 | 175.7813 | 1.9531 | 1477630 B |        1.68 |
| Cte_Prepare_Hash               | InProcessEmitToolchain |  1,727.452 μs | 2.215 | 156.2500 | 1.9531 | 1316906 B |        1.50 |
| Join4_Warm_PlanOnly            | InProcessEmitToolchain |  1,963.511 μs | 2.518 | 136.7188 | 1.9531 | 1148916 B |        1.31 |
| Cte_Warm_PlanOnly              | InProcessEmitToolchain |  2,125.878 μs | 2.726 | 156.2500 |      - | 1322508 B |        1.50 |
