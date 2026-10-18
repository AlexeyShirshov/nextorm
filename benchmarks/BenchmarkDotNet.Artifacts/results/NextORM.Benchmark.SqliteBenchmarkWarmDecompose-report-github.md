```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Toolchain              | Mean         | Median       | Ratio  | Gen0     | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------------------- |-------------:|-------------:|-------:|---------:|-------:|----------:|------------:|
| Gen_P0_From                      | Default                |     2.527 μs |     2.691 μs |  0.004 |   4.7798 |      - |   40000 B |        0.05 |
| InAtIn_Inline_Warm_Reused        | Default                |     4.037 μs |     4.035 μs |  0.007 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused                | Default                |     4.637 μs |     4.642 μs |  0.008 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused         | Default                |     5.091 μs |     5.090 μs |  0.008 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                  | Default                |     5.551 μs |     5.596 μs |  0.009 |        - |      - |         - |        0.00 |
| Gen_P1_Where                     | Default                |    85.888 μs |    85.494 μs |  0.142 |  20.9961 |      - |  176004 B |        0.21 |
| RecursiveCte_P0_Anchor           | Default                |   125.133 μs |   126.317 μs |  0.207 |  31.4941 |      - |  264005 B |        0.31 |
| Gen_P2_Select                    | Default                |   132.811 μs |   134.742 μs |  0.220 |  39.7949 |      - |  333607 B |        0.40 |
| InAtIn_Captured_Construct        | Default                |   174.032 μs |   173.225 μs |  0.288 |  34.6680 |      - |  291200 B |        0.35 |
| InAtIn_Inline_Construct          | Default                |   187.567 μs |   190.778 μs |  0.310 |  37.1094 |      - |  310400 B |        0.37 |
| RecursiveCte_P1_Step             | Default                |   191.097 μs |   189.733 μs |  0.316 |  45.1660 |      - |  379208 B |        0.45 |
| Cte_P1_With                      | Default                |   224.920 μs |   215.136 μs |  0.372 |  40.0391 |      - |  338414 B |        0.40 |
| Cte_P0_Body                      | Default                |   237.071 μs |   232.836 μs |  0.392 |  38.0859 |      - |  326413 B |        0.39 |
| Cte_P2_From                      | Default                |   237.528 μs |   237.877 μs |  0.393 |  44.9219 |      - |  378415 B |        0.45 |
| InAtIn_Captured_Prepare_NoHash   | Default                |   337.908 μs |   332.902 μs |  0.559 |  50.7813 |      - |  428800 B |        0.51 |
| Cte_P3_Join                      | Default                |   350.230 μs |   350.497 μs |  0.579 |  64.4531 |      - |  542433 B |        0.64 |
| RecursiveCte_P2_Operands         | Default                |   356.989 μs |   356.224 μs |  0.590 |  78.1250 |      - |  654413 B |        0.78 |
| InAtIn_Inline_Prepare_NoHash     | Default                |   385.658 μs |   385.880 μs |  0.638 |  55.1758 |      - |  462400 B |        0.55 |
| RecursiveCte_P3_UnionAll         | Default                |   388.219 μs |   385.909 μs |  0.642 |  87.8906 |      - |  739215 B |        0.88 |
| RecursiveCte_Construct           | Default                |   425.570 μs |   422.494 μs |  0.704 | 112.7930 | 0.4883 |  945619 B |        1.12 |
| RecursiveCte_P4_Declared         | Default                |   483.444 μs |   488.293 μs |  0.799 | 115.2344 | 0.9766 |  964819 B |        1.15 |
| InAtIn_Captured_Prepare_Hash     | Default                |   490.703 μs |   489.579 μs |  0.811 |  65.4297 | 0.9766 |  553600 B |        0.66 |
| Cte_P4_Select                    | Default                |   555.709 μs |   555.149 μs |  0.919 |  97.6563 |      - |  828068 B |        0.98 |
| InAtIn_Inline_Prepare_Hash       | Default                |   562.570 μs |   562.996 μs |  0.930 |  66.4063 |      - |  562400 B |        0.67 |
| Join4_Construct                  | Default                |   601.926 μs |   603.465 μs |  0.995 | 101.5625 |      - |  854486 B |        1.01 |
| Cte_Construct                    | Default                |   606.475 μs |   600.917 μs |  1.003 |  97.6563 |      - |  842468 B |        1.00 |
| InAtIn_Inline_Warm_PlanOnly      | Default                |   779.112 μs |   780.237 μs |  1.288 |  70.3125 |      - |  633600 B |        0.75 |
| Cte_RawHit_P2_Comparers          | Default                |   916.857 μs |   927.633 μs |  1.516 | 117.1875 |      - | 1006484 B |        1.19 |
| Cte_RawHit_P0_Capture            | Default                |   929.956 μs |   947.618 μs |  1.538 | 117.1875 |      - |  984884 B |        1.17 |
| Cte_RawHit                       | Default                |   947.864 μs |   977.782 μs |  1.567 | 117.1875 |      - |  984884 B |        1.17 |
| RecursiveCte_Prepare_NoHash      | Default                |   959.666 μs |   965.231 μs |  1.587 | 140.6250 |      - | 1197625 B |        1.42 |
| RecursiveCte_RawHit              | Default                |   987.112 μs |   981.358 μs |  1.632 | 148.4375 |      - | 1256026 B |        1.49 |
| RecursiveCte_RawHit_P0_Capture   | Default                | 1,026.395 μs | 1,034.647 μs |  1.697 | 148.4375 |      - | 1256026 B |        1.49 |
| Join4_Prepare_NoHash             | Default                | 1,028.916 μs | 1,019.920 μs |  1.701 | 125.0000 |      - | 1088912 B |        1.29 |
| RecursiveCte_RawHit_P2_Comparers | Default                | 1,088.651 μs | 1,121.852 μs |  1.800 | 148.4375 |      - | 1277626 B |        1.52 |
| Cte_Prepare_NoHash               | Default                | 1,112.746 μs | 1,113.096 μs |  1.840 | 125.0000 |      - | 1098489 B |        1.30 |
| Cte_RawHit_Lookup                | Default                | 1,181.279 μs | 1,176.620 μs |  1.953 | 117.1875 |      - | 1025684 B |        1.22 |
| RecursiveCte_Warm_PlanOnly       | Default                | 1,545.002 μs | 1,563.871 μs |  2.554 | 140.6250 |      - | 1299226 B |        1.54 |
| RecursiveCte_PlanKey             | Default                | 1,738.050 μs | 1,796.885 μs |  2.874 | 140.6250 |      - | 1302426 B |        1.55 |
| Join4_Warm_PlanOnly              | Default                | 1,824.943 μs | 1,817.857 μs |  3.017 | 109.3750 |      - | 1008905 B |        1.20 |
| RecursiveCte_RawHit_Lookup       | Default                | 1,844.905 μs | 1,812.468 μs |  3.050 | 156.2500 |      - | 1367229 B |        1.62 |
| Cte_Warm_PlanOnly                | Default                | 2,139.203 μs | 2,226.068 μs |  3.537 | 109.3750 |      - | 1036084 B |        1.23 |
| Cte_PlanKey                      | Default                | 2,174.567 μs | 1,788.856 μs |  3.595 | 109.3750 |      - | 1036084 B |        1.23 |
| RecursiveCte_Prepare_Hash        | Default                | 2,210.228 μs | 1,702.054 μs |  3.654 | 156.2500 |      - | 1344829 B |        1.60 |
| Cte_Prepare_Hash                 | Default                | 2,296.302 μs | 2,515.629 μs |  3.797 | 140.6250 |      - | 1190505 B |        1.41 |
| Join4_Prepare_Hash               | Default                | 2,690.731 μs | 2,738.057 μs |  4.449 | 125.0000 |      - | 1163318 B |        1.38 |
| Cte_Warm_EndToEnd                | Default                | 3,110.853 μs | 3,116.062 μs |  5.143 | 125.0000 |      - | 1124094 B |        1.33 |
| RecursiveCte_Warm_EndToEnd       | Default                | 6,170.467 μs | 6,233.974 μs | 10.202 | 156.2500 |      - | 1360032 B |        1.61 |
|                                  |                        |              |              |        |          |        |           |             |
| Gen_P0_From                      | InProcessEmitToolchain |     2.664 μs |     2.757 μs |  0.005 |   4.7798 |      - |   40000 B |        0.05 |
| InAtIn_Inline_Warm_Reused        | InProcessEmitToolchain |     5.007 μs |     5.013 μs |  0.009 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused                | InProcessEmitToolchain |     5.065 μs |     5.057 μs |  0.009 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                  | InProcessEmitToolchain |     7.078 μs |     7.068 μs |  0.012 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused         | InProcessEmitToolchain |     7.399 μs |     7.365 μs |  0.013 |        - |      - |         - |        0.00 |
| Gen_P1_Where                     | InProcessEmitToolchain |    66.142 μs |    66.856 μs |  0.113 |  19.2871 |      - |  161604 B |        0.19 |
| RecursiveCte_P0_Anchor           | InProcessEmitToolchain |   113.460 μs |   113.061 μs |  0.193 |  31.4941 |      - |  264006 B |        0.31 |
| Gen_P2_Select                    | InProcessEmitToolchain |   137.418 μs |   136.918 μs |  0.234 |  39.7949 |      - |  333608 B |        0.40 |
| InAtIn_Captured_Construct        | InProcessEmitToolchain |   164.453 μs |   163.913 μs |  0.280 |  34.6680 |      - |  291201 B |        0.35 |
| InAtIn_Inline_Construct          | InProcessEmitToolchain |   185.761 μs |   185.669 μs |  0.317 |  37.1094 |      - |  310401 B |        0.37 |
| RecursiveCte_P1_Step             | InProcessEmitToolchain |   187.180 μs |   186.634 μs |  0.319 |  45.1660 | 0.2441 |  379209 B |        0.45 |
| Cte_P0_Body                      | InProcessEmitToolchain |   208.636 μs |   207.387 μs |  0.356 |  38.8184 |      - |  326414 B |        0.39 |
| Cte_P1_With                      | InProcessEmitToolchain |   215.950 μs |   217.022 μs |  0.368 |  40.2832 |      - |  338415 B |        0.40 |
| Cte_P2_From                      | InProcessEmitToolchain |   220.281 μs |   221.229 μs |  0.375 |  45.1660 |      - |  378416 B |        0.45 |
| RecursiveCte_P2_Operands         | InProcessEmitToolchain |   319.543 μs |   317.717 μs |  0.545 |  76.6602 |      - |  643215 B |        0.77 |
| Cte_P3_Join                      | InProcessEmitToolchain |   349.185 μs |   350.610 μs |  0.595 |  64.4531 |      - |  542435 B |        0.65 |
| RecursiveCte_P3_UnionAll         | InProcessEmitToolchain |   350.480 μs |   352.013 μs |  0.597 |  86.4258 | 0.4883 |  724817 B |        0.86 |
| InAtIn_Captured_Prepare_NoHash   | InProcessEmitToolchain |   386.965 μs |   386.740 μs |  0.660 |  50.7813 |      - |  428802 B |        0.51 |
| InAtIn_Inline_Prepare_NoHash     | InProcessEmitToolchain |   407.395 μs |   409.193 μs |  0.694 |  53.2227 |      - |  448002 B |        0.53 |
| RecursiveCte_Construct           | InProcessEmitToolchain |   444.142 μs |   444.238 μs |  0.757 | 112.7930 | 0.4883 |  945621 B |        1.13 |
| RecursiveCte_P4_Declared         | InProcessEmitToolchain |   451.283 μs |   453.594 μs |  0.769 | 113.2813 | 0.4883 |  948822 B |        1.13 |
| Cte_P4_Select                    | InProcessEmitToolchain |   559.734 μs |   566.738 μs |  0.954 |  99.6094 |      - |  839272 B |        1.00 |
| InAtIn_Captured_Prepare_Hash     | InProcessEmitToolchain |   573.053 μs |   573.293 μs |  0.977 |  63.4766 |      - |  538405 B |        0.64 |
| Cte_Construct                    | InProcessEmitToolchain |   586.653 μs |   585.457 μs |  1.000 |  97.6563 |      - |  839268 B |        1.00 |
| Join4_Construct                  | InProcessEmitToolchain |   658.924 μs |   653.555 μs |  1.123 | 101.5625 |      - |  854491 B |        1.02 |
| InAtIn_Inline_Prepare_Hash       | InProcessEmitToolchain |   691.595 μs |   658.775 μs |  1.179 |  66.4063 |      - |  562405 B |        0.67 |
| Cte_RawHit_P0_Capture            | InProcessEmitToolchain |   893.652 μs |   898.260 μs |  1.523 | 119.1406 | 1.9531 | 1004086 B |        1.20 |
| Cte_RawHit                       | InProcessEmitToolchain |   910.053 μs |   920.369 μs |  1.551 | 119.1406 | 1.9531 | 1004086 B |        1.20 |
| Cte_RawHit_P2_Comparers          | InProcessEmitToolchain |   912.351 μs |   916.014 μs |  1.555 | 122.0703 | 2.9297 | 1025688 B |        1.22 |
| InAtIn_Inline_Warm_PlanOnly      | InProcessEmitToolchain |   994.096 μs |   992.306 μs |  1.695 |  74.2188 |      - |  633610 B |        0.75 |
| RecursiveCte_Prepare_NoHash      | InProcessEmitToolchain | 1,003.166 μs |   998.241 μs |  1.710 | 142.5781 |      - | 1197634 B |        1.43 |
| RecursiveCte_RawHit_P0_Capture   | InProcessEmitToolchain | 1,043.334 μs | 1,029.394 μs |  1.778 | 148.4375 | 3.9063 | 1256034 B |        1.50 |
| RecursiveCte_RawHit              | InProcessEmitToolchain | 1,054.447 μs | 1,053.493 μs |  1.797 | 148.4375 | 3.9063 | 1256035 B |        1.50 |
| Join4_Prepare_NoHash             | InProcessEmitToolchain | 1,074.504 μs | 1,075.606 μs |  1.832 | 128.9063 |      - | 1092120 B |        1.30 |
| RecursiveCte_RawHit_P2_Comparers | InProcessEmitToolchain | 1,095.230 μs | 1,060.763 μs |  1.867 | 152.3438 | 3.9063 | 1277636 B |        1.52 |
| Cte_Prepare_NoHash               | InProcessEmitToolchain | 1,164.454 μs | 1,165.007 μs |  1.985 | 132.8125 |      - | 1117700 B |        1.33 |
| Cte_RawHit_Lookup                | InProcessEmitToolchain | 1,191.375 μs | 1,181.581 μs |  2.031 | 123.0469 | 1.9531 | 1036894 B |        1.24 |
| Join4_Warm_PlanOnly              | InProcessEmitToolchain | 1,199.884 μs | 1,199.045 μs |  2.045 | 117.1875 |      - |  984910 B |        1.17 |
| Cte_PlanKey                      | InProcessEmitToolchain | 1,229.575 μs | 1,229.308 μs |  2.096 | 123.0469 | 1.9531 | 1044094 B |        1.24 |
| Join4_Prepare_Hash               | InProcessEmitToolchain | 1,262.809 μs | 1,258.955 μs |  2.153 | 132.8125 | 1.9531 | 1123323 B |        1.34 |
| RecursiveCte_Prepare_Hash        | InProcessEmitToolchain | 1,331.873 μs | 1,335.529 μs |  2.270 | 160.1563 | 1.9531 | 1341636 B |        1.60 |
| Cte_Warm_PlanOnly                | InProcessEmitToolchain | 1,340.352 μs | 1,344.518 μs |  2.285 | 125.0000 |      - | 1052095 B |        1.25 |
| RecursiveCte_RawHit_Lookup       | InProcessEmitToolchain | 1,382.743 μs | 1,383.353 μs |  2.357 | 152.3438 | 1.9531 | 1277636 B |        1.52 |
| RecursiveCte_PlanKey             | InProcessEmitToolchain | 1,397.589 μs | 1,397.209 μs |  2.382 | 152.3438 |      - | 1284836 B |        1.53 |
| RecursiveCte_Warm_PlanOnly       | InProcessEmitToolchain | 1,431.973 μs | 1,432.587 μs |  2.441 | 152.3438 |      - | 1284835 B |        1.53 |
| Cte_Prepare_Hash                 | InProcessEmitToolchain | 1,438.777 μs | 1,436.890 μs |  2.453 | 144.5313 | 1.9531 | 1209708 B |        1.44 |
| Cte_Warm_EndToEnd                | InProcessEmitToolchain | 3,214.744 μs | 3,216.013 μs |  5.480 | 132.8125 |      - | 1131292 B |        1.35 |
| RecursiveCte_Warm_EndToEnd       | InProcessEmitToolchain | 3,643.894 μs | 3,642.234 μs |  6.211 | 160.1563 |      - | 1360047 B |        1.62 |
