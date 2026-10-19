```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Toolchain              | Categories     | Mean          | Median        | Ratio  | Gen0     | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------------------- |--------------- |--------------:|--------------:|-------:|---------:|-------:|----------:|------------:|
| Gen_P0_From                      | Default                |                |      2.675 μs |      2.847 μs |  0.002 |   4.7798 |      - |   40000 B |        0.05 |
| Except_Warm_Reused               | Default                | Warm_Except    |      4.365 μs |      4.338 μs |  0.004 |        - |      - |         - |        0.00 |
| Intersect_Warm_Reused            | Default                | Warm_Intersect |      4.521 μs |      4.527 μs |  0.004 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                  | Default                |                |     10.634 μs |      8.729 μs |  0.009 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused         | Default                |                |     11.710 μs |     10.903 μs |  0.010 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused                | Default                |                |     12.883 μs |     11.978 μs |  0.011 |        - |      - |         - |        0.00 |
| InAtIn_Inline_Warm_Reused        | Default                |                |     13.566 μs |     11.324 μs |  0.011 |        - |      - |         - |        0.00 |
| Gen_P1_Where                     | Default                |                |     70.680 μs |     70.967 μs |  0.058 |  19.2871 |      - |  161603 B |        0.20 |
| Intersect_Construct              | Default                | Warm_Intersect |    119.513 μs |    121.302 μs |  0.098 |  41.9922 | 0.2441 |  352014 B |        0.43 |
| RecursiveCte_P0_Anchor           | Default                |                |    121.279 μs |    120.974 μs |  0.099 |  31.4941 |      - |  264005 B |        0.32 |
| Except_Construct                 | Default                | Warm_Except    |    121.312 μs |    120.538 μs |  0.099 |  41.9922 | 0.2441 |  352014 B |        0.43 |
| Gen_P2_Select                    | Default                |                |    134.300 μs |    135.330 μs |  0.110 |  39.7949 |      - |  333607 B |        0.40 |
| RecursiveCte_P1_Step             | Default                |                |    198.247 μs |    199.531 μs |  0.162 |  45.1660 |      - |  379208 B |        0.46 |
| InAtIn_Captured_Construct        | Default                |                |    204.350 μs |    203.929 μs |  0.167 |  34.6680 |      - |  291200 B |        0.35 |
| Cte_P2_From                      | Default                |                |    224.989 μs |    223.226 μs |  0.184 |  44.9219 |      - |  378415 B |        0.46 |
| Cte_P1_With                      | Default                |                |    226.772 μs |    227.664 μs |  0.186 |  40.2832 |      - |  338414 B |        0.41 |
| Cte_P0_Body                      | Default                |                |    293.789 μs |    292.297 μs |  0.241 |  37.1094 |      - |  326413 B |        0.39 |
| Cte_P3_Join                      | Default                |                |    345.019 μs |    343.144 μs |  0.283 |  64.4531 |      - |  542433 B |        0.66 |
| RecursiveCte_P2_Operands         | Default                |                |    378.774 μs |    378.512 μs |  0.310 |  76.6602 |      - |  643213 B |        0.78 |
| Intersect_Prepare_NoHash         | Default                | Warm_Intersect |    380.121 μs |    368.944 μs |  0.312 |  62.5000 |      - |  526422 B |        0.64 |
| RecursiveCte_P3_UnionAll         | Default                |                |    405.035 μs |    405.457 μs |  0.332 |  86.4258 | 0.9766 |  724815 B |        0.88 |
| InAtIn_Captured_Prepare_NoHash   | Default                |                |    407.249 μs |    406.575 μs |  0.334 |  50.7813 |      - |  428800 B |        0.52 |
| Except_Warm_PlanOnly             | Default                | Warm_Except    |    418.376 μs |    418.661 μs |  0.343 |  68.8477 | 0.4883 |  576823 B |        0.70 |
| Intersect_Warm_PlanOnly          | Default                | Warm_Intersect |    455.590 μs |    451.476 μs |  0.373 |  68.8477 | 0.4883 |  576823 B |        0.70 |
| Intersect_Prepare_Hash           | Default                | Warm_Intersect |    470.700 μs |    459.733 μs |  0.386 |  70.3125 |      - |  605625 B |        0.73 |
| InAtIn_Inline_Prepare_NoHash     | Default                |                |    486.680 μs |    452.661 μs |  0.399 |  46.8750 |      - |  448000 B |        0.54 |
| RecursiveCte_P4_Declared         | Default                |                |    488.568 μs |    489.833 μs |  0.400 | 113.2813 | 0.4883 |  948819 B |        1.15 |
| Except_Prepare_Hash              | Default                | Warm_Except    |    508.986 μs |    506.796 μs |  0.417 |  70.3125 |      - |  605625 B |        0.73 |
| InAtIn_Captured_Prepare_Hash     | Default                |                |    519.970 μs |    522.151 μs |  0.426 |  62.5000 |      - |  549600 B |        0.66 |
| Except_Prepare_NoHash            | Default                | Warm_Except    |    659.097 μs |    602.161 μs |  0.540 |  62.5000 |      - |  526422 B |        0.64 |
| Cte_P4_Select                    | Default                |                |    685.855 μs |    635.080 μs |  0.562 |  97.6563 |      - |  820068 B |        0.99 |
| RecursiveCte_Construct           | Default                |                |    706.276 μs |    725.219 μs |  0.579 | 109.3750 |      - |  945620 B |        1.14 |
| InAtIn_Inline_Construct          | Default                |                |    803.824 μs |    770.033 μs |  0.659 |  35.1563 |      - |  310400 B |        0.37 |
| Cte_RawHit_P0_Capture            | Default                |                |    924.648 μs |    925.473 μs |  0.758 | 117.1875 |      - |  984884 B |        1.19 |
| InAtIn_Inline_Warm_PlanOnly      | Default                |                |    979.519 μs |    999.045 μs |  0.803 |  70.3125 |      - |  633600 B |        0.77 |
| RecursiveCte_RawHit_P0_Capture   | Default                |                |  1,020.583 μs |    992.628 μs |  0.836 | 148.4375 |      - | 1267226 B |        1.53 |
| Cte_RawHit_P2_Comparers          | Default                |                |  1,027.007 μs |  1,017.863 μs |  0.842 | 117.1875 |      - | 1017684 B |        1.23 |
| RecursiveCte_RawHit_P2_Comparers | Default                |                |  1,072.112 μs |  1,061.362 μs |  0.879 | 148.4375 |      - | 1277626 B |        1.54 |
| InAtIn_Inline_Prepare_Hash       | Default                |                |  1,121.818 μs |  1,210.055 μs |  0.919 |  62.5000 |      - |  562400 B |        0.68 |
| Join4_Warm_PlanOnly              | Default                |                |  1,483.858 μs |  1,464.454 μs |  1.216 | 109.3750 |      - |  992905 B |        1.20 |
| Cte_Construct                    | Default                |                |  1,488.274 μs |  1,595.086 μs |  1.220 |  97.6563 |      - |  828068 B |        1.00 |
| RecursiveCte_RawHit              | Default                |                |  1,512.445 μs |  1,501.810 μs |  1.240 | 140.6250 |      - | 1283226 B |        1.55 |
| RecursiveCte_RawHit_Lookup       | Default                |                |  1,559.291 μs |  1,569.358 μs |  1.278 | 140.6250 |      - | 1277626 B |        1.54 |
| RecursiveCte_Prepare_NoHash      | Default                |                |  1,686.582 μs |  1,285.711 μs |  1.382 | 140.6250 |      - | 1197625 B |        1.45 |
| RecursiveCte_PlanKey             | Default                |                |  1,692.561 μs |  1,715.638 μs |  1.387 | 156.2500 |      - | 1308829 B |        1.58 |
| Cte_Warm_PlanOnly                | Default                |                |  1,697.650 μs |  1,653.677 μs |  1.391 | 121.0938 |      - | 1044084 B |        1.26 |
| Cte_Prepare_NoHash               | Default                |                |  1,737.031 μs |  1,578.499 μs |  1.424 | 125.0000 |      - | 1106489 B |        1.34 |
| Join4_Prepare_Hash               | Default                |                |  2,108.768 μs |  2,230.495 μs |  1.728 | 132.8125 |      - | 1120118 B |        1.35 |
| RecursiveCte_Prepare_Hash        | Default                |                |  2,429.156 μs |  2,474.333 μs |  1.991 | 156.2500 |      - | 1338429 B |        1.62 |
| Cte_PlanKey                      | Default                |                |  2,731.198 μs |  2,045.481 μs |  2.238 | 125.0000 |      - | 1048905 B |        1.27 |
| Cte_RawHit                       | Default                |                |  3,533.209 μs |  2,405.555 μs |  2.896 |  93.7500 |      - |  988084 B |        1.19 |
| Cte_RawHit_Lookup                | Default                |                |  3,633.533 μs |  2,001.518 μs |  2.978 | 109.3750 |      - | 1029684 B |        1.24 |
| Join4_Construct                  | Default                |                |  3,966.778 μs |  4,056.315 μs |  3.251 | 101.5625 |      - |  866492 B |        1.05 |
| RecursiveCte_Warm_EndToEnd       | Default                |                |  4,624.323 μs |  4,534.522 μs |  3.790 | 156.2500 |      - | 1371232 B |        1.66 |
| Cte_Prepare_Hash                 | Default                |                |  4,945.270 μs |  4,404.573 μs |  4.053 | 125.0000 |      - | 1205705 B |        1.46 |
| Cte_Warm_EndToEnd                | Default                |                |  5,175.474 μs |  4,438.730 μs |  4.242 | 125.0000 |      - | 1123305 B |        1.36 |
| Join4_Prepare_NoHash             | Default                |                | 12,710.439 μs | 12,387.288 μs | 10.417 | 125.0000 |      - | 1116158 B |        1.35 |
| RecursiveCte_Warm_PlanOnly       | Default                |                | 17,253.305 μs | 20,403.275 μs | 14.141 | 125.0000 |      - | 1367094 B |        1.65 |
|                                  |                        |                |               |               |        |          |        |           |             |
| Gen_P0_From                      | InProcessEmitToolchain |                |      3.699 μs |      3.533 μs |  0.006 |   4.7760 |      - |   40000 B |        0.05 |
| InAtIn_Inline_Warm_Reused        | InProcessEmitToolchain |                |      4.575 μs |      4.475 μs |  0.008 |        - |      - |         - |        0.00 |
| Except_Warm_Reused               | InProcessEmitToolchain | Warm_Except    |      4.593 μs |      4.593 μs |  0.008 |        - |      - |         - |        0.00 |
| Intersect_Warm_Reused            | InProcessEmitToolchain | Warm_Intersect |      4.605 μs |      4.602 μs |  0.008 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused                | InProcessEmitToolchain |                |      5.233 μs |      5.262 μs |  0.009 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                  | InProcessEmitToolchain |                |      7.760 μs |      7.780 μs |  0.013 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused         | InProcessEmitToolchain |                |      8.560 μs |      8.616 μs |  0.014 |        - |      - |         - |        0.00 |
| RecursiveCte_P0_Anchor           | InProcessEmitToolchain |                |    129.666 μs |    126.971 μs |  0.218 |  31.4941 |      - |  264007 B |        0.32 |
| Intersect_Construct              | InProcessEmitToolchain | Warm_Intersect |    144.643 μs |    147.672 μs |  0.244 |  41.9922 | 0.2441 |  352015 B |        0.42 |
| Gen_P1_Where                     | InProcessEmitToolchain |                |    160.638 μs |    160.945 μs |  0.271 |  19.2871 |      - |  161604 B |        0.19 |
| InAtIn_Captured_Construct        | InProcessEmitToolchain |                |    186.068 μs |    186.350 μs |  0.313 |  34.6680 |      - |  291201 B |        0.35 |
| InAtIn_Inline_Construct          | InProcessEmitToolchain |                |    202.817 μs |    202.352 μs |  0.342 |  37.1094 |      - |  310401 B |        0.37 |
| RecursiveCte_P1_Step             | InProcessEmitToolchain |                |    203.704 μs |    204.754 μs |  0.343 |  45.1660 | 0.2441 |  379209 B |        0.45 |
| Cte_P0_Body                      | InProcessEmitToolchain |                |    215.917 μs |    219.948 μs |  0.364 |  38.8184 |      - |  326414 B |        0.39 |
| Cte_P1_With                      | InProcessEmitToolchain |                |    220.254 μs |    219.996 μs |  0.371 |  40.2832 |      - |  338415 B |        0.41 |
| Cte_P2_From                      | InProcessEmitToolchain |                |    221.143 μs |    221.678 μs |  0.372 |  45.1660 |      - |  378416 B |        0.45 |
| Except_Construct                 | InProcessEmitToolchain | Warm_Except    |    254.151 μs |    218.272 μs |  0.428 |  41.9922 | 0.2441 |  352015 B |        0.42 |
| Gen_P2_Select                    | InProcessEmitToolchain |                |    284.760 μs |    298.270 μs |  0.480 |  41.5039 |      - |  348009 B |        0.42 |
| Except_Prepare_NoHash            | InProcessEmitToolchain | Warm_Except    |    397.448 μs |    396.021 μs |  0.669 |  62.5000 | 0.4883 |  526424 B |        0.63 |
| Cte_P3_Join                      | InProcessEmitToolchain |                |    407.378 μs |    382.041 μs |  0.686 |  66.4063 |      - |  557636 B |        0.67 |
| InAtIn_Captured_Prepare_NoHash   | InProcessEmitToolchain |                |    414.028 μs |    412.209 μs |  0.697 |  50.7813 |      - |  428802 B |        0.51 |
| RecursiveCte_P2_Operands         | InProcessEmitToolchain |                |    422.107 μs |    366.757 μs |  0.711 |  76.6602 |      - |  643215 B |        0.77 |
| InAtIn_Inline_Prepare_NoHash     | InProcessEmitToolchain |                |    424.848 μs |    425.978 μs |  0.716 |  53.2227 |      - |  448002 B |        0.54 |
| Intersect_Prepare_NoHash         | InProcessEmitToolchain | Warm_Intersect |    428.691 μs |    431.677 μs |  0.722 |  62.5000 | 0.4883 |  526424 B |        0.63 |
| Except_Warm_PlanOnly             | InProcessEmitToolchain | Warm_Except    |    457.929 μs |    458.830 μs |  0.771 |  68.8477 | 0.4883 |  576826 B |        0.69 |
| Intersect_Warm_PlanOnly          | InProcessEmitToolchain | Warm_Intersect |    474.360 μs |    471.868 μs |  0.799 |  68.8477 | 0.4883 |  576826 B |        0.69 |
| RecursiveCte_Construct           | InProcessEmitToolchain |                |    495.878 μs |    498.150 μs |  0.835 | 114.2578 |      - |  960024 B |        1.15 |
| Intersect_Prepare_Hash           | InProcessEmitToolchain | Warm_Intersect |    520.997 μs |    509.851 μs |  0.877 |  72.2656 | 0.9766 |  605629 B |        0.73 |
| Except_Prepare_Hash              | InProcessEmitToolchain | Warm_Except    |    550.224 μs |    549.407 μs |  0.927 |  72.2656 | 0.9766 |  605629 B |        0.73 |
| RecursiveCte_P3_UnionAll         | InProcessEmitToolchain |                |    577.282 μs |    506.077 μs |  0.972 |  85.9375 |      - |  724825 B |        0.87 |
| Cte_P4_Select                    | InProcessEmitToolchain |                |    587.502 μs |    589.567 μs |  0.989 |  99.6094 |      - |  835272 B |        1.00 |
| Cte_Construct                    | InProcessEmitToolchain |                |    593.825 μs |    597.037 μs |  1.000 |  99.6094 |      - |  835268 B |        1.00 |
| InAtIn_Captured_Prepare_Hash     | InProcessEmitToolchain |                |    627.018 μs |    624.883 μs |  1.056 |  63.4766 |      - |  538405 B |        0.64 |
| Join4_Construct                  | InProcessEmitToolchain |                |    677.640 μs |    677.388 μs |  1.141 | 101.5625 |      - |  854491 B |        1.02 |
| InAtIn_Inline_Prepare_Hash       | InProcessEmitToolchain |                |    692.056 μs |    696.530 μs |  1.166 |  66.4063 |      - |  562405 B |        0.67 |
| RecursiveCte_P4_Declared         | InProcessEmitToolchain |                |  1,004.861 μs |    982.474 μs |  1.692 | 113.2813 |      - |  963229 B |        1.15 |
| InAtIn_Inline_Warm_PlanOnly      | InProcessEmitToolchain |                |  1,020.893 μs |  1,009.799 μs |  1.719 |  74.2188 |      - |  633610 B |        0.76 |
| Cte_RawHit                       | InProcessEmitToolchain |                |  1,022.822 μs |  1,004.610 μs |  1.723 | 119.1406 | 1.9531 | 1000086 B |        1.20 |
| Cte_RawHit_P0_Capture            | InProcessEmitToolchain |                |  1,042.894 μs |  1,051.385 μs |  1.756 | 119.1406 | 1.9531 | 1000091 B |        1.20 |
| RecursiveCte_Prepare_NoHash      | InProcessEmitToolchain |                |  1,077.731 μs |  1,066.080 μs |  1.815 | 142.5781 |      - | 1208834 B |        1.45 |
| Join4_Prepare_NoHash             | InProcessEmitToolchain |                |  1,130.406 μs |  1,129.211 μs |  1.904 | 128.9063 |      - | 1092120 B |        1.31 |
| Cte_Prepare_NoHash               | InProcessEmitToolchain |                |  1,170.720 μs |  1,176.370 μs |  1.972 | 130.8594 |      - | 1110499 B |        1.33 |
| RecursiveCte_RawHit              | InProcessEmitToolchain |                |  1,223.521 μs |  1,222.676 μs |  2.061 | 150.3906 | 3.9063 | 1270435 B |        1.52 |
| RecursiveCte_RawHit_P0_Capture   | InProcessEmitToolchain |                |  1,242.611 μs |  1,243.870 μs |  2.093 | 150.3906 | 3.9063 | 1270435 B |        1.52 |
| Cte_RawHit_P2_Comparers          | InProcessEmitToolchain |                |  1,270.752 μs |  1,284.636 μs |  2.140 | 121.0938 | 1.9531 | 1021692 B |        1.22 |
| Join4_Warm_PlanOnly              | InProcessEmitToolchain |                |  1,273.620 μs |  1,259.414 μs |  2.145 | 117.1875 |      - |  984910 B |        1.18 |
| Cte_PlanKey                      | InProcessEmitToolchain |                |  1,315.899 μs |  1,308.468 μs |  2.216 | 123.0469 |      - | 1040094 B |        1.25 |
| Join4_Prepare_Hash               | InProcessEmitToolchain |                |  1,329.737 μs |  1,328.122 μs |  2.239 | 132.8125 | 1.9531 | 1123321 B |        1.34 |
| RecursiveCte_Prepare_Hash        | InProcessEmitToolchain |                |  1,373.775 μs |  1,349.167 μs |  2.314 | 160.1563 | 1.9531 | 1352837 B |        1.62 |
| RecursiveCte_RawHit_P2_Comparers | InProcessEmitToolchain |                |  1,380.690 μs |  1,403.967 μs |  2.325 | 154.2969 | 3.9063 | 1292036 B |        1.55 |
| Cte_RawHit_Lookup                | InProcessEmitToolchain |                |  1,397.211 μs |  1,358.043 μs |  2.353 | 123.0469 |      - | 1032894 B |        1.24 |
| Cte_Warm_PlanOnly                | InProcessEmitToolchain |                |  1,408.160 μs |  1,408.735 μs |  2.372 | 125.0000 |      - | 1048095 B |        1.25 |
| Cte_Prepare_Hash                 | InProcessEmitToolchain |                |  1,467.728 μs |  1,457.794 μs |  2.472 | 142.5781 | 1.9531 | 1202507 B |        1.44 |
| RecursiveCte_RawHit_Lookup       | InProcessEmitToolchain |                |  1,488.743 μs |  1,459.750 μs |  2.507 | 154.2969 | 1.9531 | 1292036 B |        1.55 |
| RecursiveCte_Warm_PlanOnly       | InProcessEmitToolchain |                |  1,526.846 μs |  1,526.731 μs |  2.571 | 154.2969 | 1.9531 | 1299236 B |        1.56 |
| RecursiveCte_PlanKey             | InProcessEmitToolchain |                |  1,543.925 μs |  1,574.434 μs |  2.600 | 154.2969 | 1.9531 | 1299236 B |        1.56 |
| Cte_Warm_EndToEnd                | InProcessEmitToolchain |                |  3,311.929 μs |  3,297.107 μs |  5.578 | 132.8125 |      - | 1127292 B |        1.35 |
| RecursiveCte_Warm_EndToEnd       | InProcessEmitToolchain |                |  4,160.743 μs |  4,245.266 μs |  7.007 | 164.0625 |      - | 1374468 B |        1.65 |
