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
| InAtIn_Inline_Warm_Reused      | Default                |     3.775 μs | 0.007 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused              | Default                |     4.333 μs | 0.008 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused       | Default                |     4.693 μs | 0.009 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                | Default                |     4.871 μs | 0.009 |        - |      - |         - |        0.00 |
| InAtIn_Captured_Construct      | Default                |   166.313 μs | 0.314 |  34.6680 |      - |  291200 B |        0.36 |
| InAtIn_Inline_Construct        | Default                |   177.767 μs | 0.335 |  37.1094 |      - |  310400 B |        0.38 |
| InAtIn_Captured_Prepare_NoHash | Default                |   326.456 μs | 0.615 |  50.7813 |      - |  428800 B |        0.52 |
| InAtIn_Inline_Prepare_NoHash   | Default                |   374.420 μs | 0.706 |  55.1758 |      - |  462400 B |        0.56 |
| RecursiveCte_Construct         | Default                |   439.208 μs | 0.828 | 114.7461 | 0.4883 |  960019 B |        1.17 |
| InAtIn_Captured_Prepare_Hash   | Default                |   464.514 μs | 0.876 |  63.9648 | 0.4883 |  538400 B |        0.66 |
| InAtIn_Inline_Prepare_Hash     | Default                |   503.825 μs | 0.950 |  66.4063 |      - |  562400 B |        0.69 |
| Cte_Construct                  | Default                |   530.426 μs | 1.000 |  97.6563 |      - |  820068 B |        1.00 |
| Join4_Construct                | Default                |   593.773 μs | 1.119 | 101.5625 |      - |  854486 B |        1.04 |
| InAtIn_Inline_Warm_PlanOnly    | Default                |   767.121 μs | 1.446 |  70.3125 |      - |  633600 B |        0.77 |
| RecursiveCte_Prepare_NoHash    | Default                |   975.822 μs | 1.840 | 140.6250 |      - | 1197625 B |        1.46 |
| Join4_Prepare_NoHash           | Default                |   991.429 μs | 1.869 | 125.0000 |      - | 1088912 B |        1.33 |
| Cte_Prepare_NoHash             | Default                | 1,091.062 μs | 2.057 | 125.0000 |      - | 1098489 B |        1.34 |
| RecursiveCte_Warm_PlanOnly     | Default                | 1,352.764 μs | 2.550 | 156.2500 |      - | 1313629 B |        1.60 |
| Cte_Warm_PlanOnly              | Default                | 1,840.750 μs | 3.470 | 109.3750 |      - | 1036084 B |        1.26 |
| Join4_Prepare_Hash             | Default                | 1,878.946 μs | 3.542 | 125.0000 |      - | 1140118 B |        1.39 |
| RecursiveCte_Prepare_Hash      | Default                | 2,084.684 μs | 3.930 | 156.2500 |      - | 1352829 B |        1.65 |
| Join4_Warm_PlanOnly            | Default                | 2,171.734 μs | 4.094 | 109.3750 |      - |  992905 B |        1.21 |
| Cte_Prepare_Hash               | Default                | 2,626.746 μs | 4.952 | 140.6250 |      - | 1187305 B |        1.45 |
|                                |                        |              |       |          |        |           |             |
| InAtIn_Inline_Warm_Reused      | InProcessEmitToolchain |     5.106 μs | 0.009 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused              | InProcessEmitToolchain |     6.194 μs | 0.010 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused       | InProcessEmitToolchain |     7.273 μs | 0.012 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                | InProcessEmitToolchain |     8.321 μs | 0.014 |        - |      - |         - |        0.00 |
| InAtIn_Captured_Construct      | InProcessEmitToolchain |   170.358 μs | 0.287 |  34.6680 |      - |  291202 B |        0.35 |
| InAtIn_Inline_Construct        | InProcessEmitToolchain |   200.390 μs | 0.338 |  37.1094 |      - |  310402 B |        0.37 |
| InAtIn_Captured_Prepare_NoHash | InProcessEmitToolchain |   372.675 μs | 0.628 |  51.2695 |      - |  428804 B |        0.52 |
| InAtIn_Inline_Prepare_NoHash   | InProcessEmitToolchain |   404.398 μs | 0.682 |  53.2227 |      - |  448003 B |        0.54 |
| RecursiveCte_Construct         | InProcessEmitToolchain |   444.346 μs | 0.749 | 112.7930 | 0.4883 |  945622 B |        1.14 |
| InAtIn_Captured_Prepare_Hash   | InProcessEmitToolchain |   550.659 μs | 0.928 |  63.4766 |      - |  538407 B |        0.65 |
| Cte_Construct                  | InProcessEmitToolchain |   593.385 μs | 1.000 |  98.6328 | 0.9766 |  828073 B |        1.00 |
| InAtIn_Inline_Prepare_Hash     | InProcessEmitToolchain |   639.297 μs | 1.077 |  66.4063 |      - |  562407 B |        0.68 |
| Join4_Construct                | InProcessEmitToolchain |   708.718 μs | 1.194 | 104.4922 |      - |  874495 B |        1.06 |
| InAtIn_Inline_Warm_PlanOnly    | InProcessEmitToolchain |   961.509 μs | 1.620 |  75.1953 | 0.9766 |  633607 B |        0.77 |
| RecursiveCte_Prepare_NoHash    | InProcessEmitToolchain | 1,037.703 μs | 1.749 | 142.5781 |      - | 1197637 B |        1.45 |
| Join4_Prepare_NoHash           | InProcessEmitToolchain | 1,186.580 μs | 2.000 | 132.8125 |      - | 1112126 B |        1.34 |
| Cte_Prepare_NoHash             | InProcessEmitToolchain | 1,288.320 μs | 2.171 | 130.8594 |      - | 1106502 B |        1.34 |
| Join4_Warm_PlanOnly            | InProcessEmitToolchain | 1,310.752 μs | 2.209 | 119.1406 |      - | 1004915 B |        1.21 |
| Join4_Prepare_Hash             | InProcessEmitToolchain | 1,350.230 μs | 2.276 | 134.7656 | 1.9531 | 1143328 B |        1.38 |
| RecursiveCte_Prepare_Hash      | InProcessEmitToolchain | 1,402.793 μs | 2.364 | 160.1563 | 1.9531 | 1341640 B |        1.62 |
| RecursiveCte_Warm_PlanOnly     | InProcessEmitToolchain | 1,409.027 μs | 2.375 | 152.3438 |      - | 1284839 B |        1.55 |
| Cte_Warm_PlanOnly              | InProcessEmitToolchain | 1,455.449 μs | 2.453 | 123.0469 |      - | 1040897 B |        1.26 |
| Cte_Prepare_Hash               | InProcessEmitToolchain | 1,676.369 μs | 2.825 | 142.5781 | 1.9531 | 1198510 B |        1.45 |
