```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                         | Toolchain              | Mean         | Ratio | Gen0     | Gen1   | Allocated | Alloc Ratio |
|------------------------------- |----------------------- |-------------:|------:|---------:|-------:|----------:|------------:|
| Join4_Warm_Reused              | Default                |     4.284 μs | 0.007 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused       | Default                |     4.349 μs | 0.007 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                | Default                |     4.491 μs | 0.007 |        - |      - |         - |        0.00 |
| InAtIn_Inline_Warm_Reused      | Default                |    60.296 μs | 0.092 |   7.9956 |      - |   67200 B |        0.08 |
| InAtIn_Captured_Construct      | Default                |   179.980 μs | 0.275 |  36.6211 |      - |  307200 B |        0.36 |
| InAtIn_Inline_Construct        | Default                |   194.090 μs | 0.296 |  38.8184 |      - |  326400 B |        0.38 |
| InAtIn_Captured_Prepare_NoHash | Default                |   316.224 μs | 0.483 |  52.7344 |      - |  441601 B |        0.52 |
| InAtIn_Inline_Prepare_NoHash   | Default                |   317.961 μs | 0.486 |  54.6875 |      - |  460800 B |        0.54 |
| InAtIn_Captured_Prepare_Hash   | Default                |   460.346 μs | 0.703 |  64.4531 |      - |  544000 B |        0.64 |
| InAtIn_Inline_Prepare_Hash     | Default                |   498.255 μs | 0.761 |  66.4063 |      - |  568000 B |        0.67 |
| RecursiveCte_Construct         | Default                |   532.817 μs | 0.814 | 121.0938 |      - | 1022421 B |        1.20 |
| Cte_Construct                  | Default                |   657.560 μs | 1.004 | 101.5625 |      - |  852071 B |        1.00 |
| Join4_Construct                | Default                |   740.054 μs | 1.130 | 105.4688 |      - |  896092 B |        1.05 |
| InAtIn_Inline_Warm_PlanOnly    | Default                |   882.241 μs | 1.348 |  70.3125 |      - |  626400 B |        0.74 |
| Join4_Prepare_NoHash           | Default                | 1,066.654 μs | 1.629 | 132.8125 |      - | 1116918 B |        1.31 |
| RecursiveCte_Prepare_Hash      | Default                | 1,086.011 μs | 1.659 | 171.8750 |      - | 1472030 B |        1.73 |
| RecursiveCte_Prepare_NoHash    | Default                | 1,086.112 μs | 1.659 | 156.2500 |      - | 1328028 B |        1.56 |
| Cte_Prepare_NoHash             | Default                | 1,270.338 μs | 1.940 | 140.6250 |      - | 1197700 B |        1.41 |
| Join4_Prepare_Hash             | Default                | 1,275.957 μs | 1.949 | 132.8125 |      - | 1144118 B |        1.34 |
| RecursiveCte_Warm_PlanOnly     | Default                | 1,826.630 μs | 2.790 | 171.8750 |      - | 1516032 B |        1.78 |
| Cte_Warm_PlanOnly              | Default                | 2,053.163 μs | 3.136 | 156.2500 |      - | 1349716 B |        1.58 |
| Join4_Warm_PlanOnly            | Default                | 2,405.024 μs | 3.673 | 140.6250 |      - | 1224931 B |        1.44 |
| Cte_Prepare_Hash               | Default                | 2,775.384 μs | 4.239 | 140.6250 |      - | 1289705 B |        1.51 |
|                                |                        |              |       |          |        |           |             |
| Join4_Warm_Reused              | InProcessEmitToolchain |     5.969 μs | 0.008 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused       | InProcessEmitToolchain |     6.963 μs | 0.010 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                | InProcessEmitToolchain |     8.286 μs | 0.012 |        - |      - |         - |        0.00 |
| InAtIn_Inline_Warm_Reused      | InProcessEmitToolchain |   107.835 μs | 0.151 |   7.9346 |      - |   67201 B |        0.08 |
| InAtIn_Captured_Construct      | InProcessEmitToolchain |   224.680 μs | 0.315 |  36.6211 |      - |  307202 B |        0.36 |
| InAtIn_Inline_Construct        | InProcessEmitToolchain |   244.251 μs | 0.343 |  38.8184 |      - |  326402 B |        0.38 |
| InAtIn_Captured_Prepare_NoHash | InProcessEmitToolchain |   351.131 μs | 0.493 |  52.7344 |      - |  441603 B |        0.52 |
| InAtIn_Inline_Prepare_NoHash   | InProcessEmitToolchain |   405.089 μs | 0.569 |  54.6875 |      - |  460803 B |        0.54 |
| InAtIn_Captured_Prepare_Hash   | InProcessEmitToolchain |   534.181 μs | 0.750 |  64.4531 |      - |  544007 B |        0.64 |
| RecursiveCte_Construct         | InProcessEmitToolchain |   588.192 μs | 0.826 | 120.1172 |      - | 1008027 B |        1.18 |
| InAtIn_Inline_Prepare_Hash     | InProcessEmitToolchain |   643.107 μs | 0.903 |  67.3828 |      - |  568007 B |        0.67 |
| Cte_Construct                  | InProcessEmitToolchain |   712.296 μs | 1.000 | 101.5625 |      - |  852075 B |        1.00 |
| Join4_Construct                | InProcessEmitToolchain |   775.248 μs | 1.088 | 103.5156 |      - |  868894 B |        1.02 |
| InAtIn_Inline_Warm_PlanOnly    | InProcessEmitToolchain |   789.231 μs | 1.108 |  74.2188 | 0.9766 |  626407 B |        0.74 |
| RecursiveCte_Prepare_NoHash    | InProcessEmitToolchain |   994.385 μs | 1.396 | 158.2031 |      - | 1328040 B |        1.56 |
| Join4_Prepare_NoHash           | InProcessEmitToolchain | 1,186.518 μs | 1.666 | 130.8594 |      - | 1104125 B |        1.30 |
| RecursiveCte_Prepare_Hash      | InProcessEmitToolchain | 1,302.674 μs | 1.829 | 175.7813 | 1.9531 | 1472043 B |        1.73 |
| Cte_Prepare_NoHash             | InProcessEmitToolchain | 1,343.259 μs | 1.886 | 142.5781 |      - | 1197710 B |        1.41 |
| Join4_Prepare_Hash             | InProcessEmitToolchain | 1,454.356 μs | 2.042 | 134.7656 | 1.9531 | 1135328 B |        1.33 |
| Cte_Prepare_Hash               | InProcessEmitToolchain | 1,653.775 μs | 2.322 | 152.3438 | 1.9531 | 1289717 B |        1.51 |
| RecursiveCte_Warm_PlanOnly     | InProcessEmitToolchain | 1,653.989 μs | 2.322 | 177.7344 | 1.9531 | 1498443 B |        1.76 |
| Join4_Warm_PlanOnly            | InProcessEmitToolchain | 1,792.218 μs | 2.516 | 144.5313 | 1.9531 | 1212936 B |        1.42 |
| Cte_Warm_PlanOnly              | InProcessEmitToolchain | 2,077.583 μs | 2.917 | 156.2500 |      - | 1337734 B |        1.57 |
