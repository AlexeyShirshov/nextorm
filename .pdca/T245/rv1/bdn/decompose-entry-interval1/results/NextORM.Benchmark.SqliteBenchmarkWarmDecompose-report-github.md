```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                           | Categories     | Mean         | Ratio | Gen0     | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |--------------- |-------------:|------:|---------:|-------:|----------:|------------:|
| Gen_P0_From                      |                |     2.768 μs | 0.005 |   4.7798 |      - |   40000 B |        0.05 |
| Except_Warm_Reused               | Warm_Except    |     5.170 μs | 0.009 |        - |      - |         - |        0.00 |
| Intersect_Warm_Reused            | Warm_Intersect |     5.192 μs | 0.009 |        - |      - |         - |        0.00 |
| InAtIn_Inline_Warm_Reused        |                |     5.318 μs | 0.009 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused                |                |     6.065 μs | 0.011 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                  |                |     6.908 μs | 0.012 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused         |                |     7.344 μs | 0.013 |        - |      - |         - |        0.00 |
| Gen_P1_Where                     |                |    66.125 μs | 0.118 |  19.2871 |      - |  161604 B |        0.20 |
| RecursiveCte_P0_Anchor           |                |   117.163 μs | 0.209 |  31.4941 |      - |  264007 B |        0.32 |
| Intersect_Construct              | Warm_Intersect |   124.483 μs | 0.222 |  41.9922 | 0.2441 |  352015 B |        0.43 |
| Except_Construct                 | Warm_Except    |   125.458 μs | 0.224 |  41.9922 | 0.2441 |  352015 B |        0.43 |
| Gen_P2_Select                    |                |   139.759 μs | 0.249 |  39.7949 |      - |  333608 B |        0.40 |
| InAtIn_Captured_Construct        |                |   183.570 μs | 0.327 |  34.6680 |      - |  291201 B |        0.35 |
| InAtIn_Inline_Construct          |                |   195.219 μs | 0.348 |  37.1094 |      - |  310401 B |        0.37 |
| Cte_P0_Body                      |                |   212.939 μs | 0.380 |  38.8184 |      - |  326414 B |        0.39 |
| RecursiveCte_P1_Step             |                |   245.247 μs | 0.437 |  45.1660 | 0.2441 |  379209 B |        0.46 |
| Cte_P2_From                      |                |   253.523 μs | 0.452 |  44.9219 |      - |  378420 B |        0.46 |
| Cte_P1_With                      |                |   315.606 μs | 0.563 |  40.2832 |      - |  338415 B |        0.41 |
| Cte_P3_Join                      |                |   337.986 μs | 0.603 |  64.4531 |      - |  542435 B |        0.66 |
| RecursiveCte_P2_Operands         |                |   360.647 μs | 0.643 |  76.6602 |      - |  643215 B |        0.78 |
| RecursiveCte_P3_UnionAll         |                |   379.632 μs | 0.677 |  86.4258 | 0.4883 |  724817 B |        0.88 |
| Except_Prepare_NoHash            | Warm_Except    |   380.662 μs | 0.679 |  62.5000 | 0.4883 |  526424 B |        0.64 |
| Intersect_Prepare_NoHash         | Warm_Intersect |   385.412 μs | 0.687 |  62.5000 | 0.4883 |  526424 B |        0.64 |
| InAtIn_Captured_Prepare_NoHash   |                |   394.708 μs | 0.704 |  50.7813 |      - |  428802 B |        0.52 |
| InAtIn_Inline_Prepare_NoHash     |                |   406.659 μs | 0.725 |  53.2227 |      - |  448002 B |        0.54 |
| Intersect_Warm_PlanOnly          | Warm_Intersect |   445.029 μs | 0.794 |  68.8477 | 0.4883 |  576826 B |        0.70 |
| RecursiveCte_Construct           |                |   452.809 μs | 0.807 | 112.7930 | 0.4883 |  945621 B |        1.14 |
| Except_Warm_PlanOnly             | Warm_Except    |   460.937 μs | 0.822 |  68.8477 | 0.4883 |  576826 B |        0.70 |
| RecursiveCte_P4_Declared         |                |   477.124 μs | 0.851 | 113.2813 | 0.4883 |  948822 B |        1.15 |
| Intersect_Prepare_Hash           | Warm_Intersect |   494.730 μs | 0.882 |  72.2656 | 0.9766 |  605629 B |        0.73 |
| Except_Prepare_Hash              | Warm_Except    |   514.599 μs | 0.918 |  72.2656 | 0.9766 |  605629 B |        0.73 |
| Cte_Construct                    |                |   560.815 μs | 1.000 |  97.6563 |      - |  828068 B |        1.00 |
| Cte_P4_Select                    |                |   570.983 μs | 1.018 |  98.6328 | 0.9766 |  828072 B |        1.00 |
| InAtIn_Captured_Prepare_Hash     |                |   590.304 μs | 1.053 |  63.4766 |      - |  538405 B |        0.65 |
| Join4_Construct                  |                |   671.354 μs | 1.197 | 101.5625 |      - |  854491 B |        1.03 |
| InAtIn_Inline_Prepare_Hash       |                |   686.551 μs | 1.224 |  66.4063 |      - |  562405 B |        0.68 |
| Cte_RawHit                       |                |   885.727 μs | 1.580 | 118.1641 | 1.9531 |  992885 B |        1.20 |
| Cte_RawHit_P0_Capture            |                |   915.650 μs | 1.633 | 118.1641 | 1.9531 |  992885 B |        1.20 |
| Cte_RawHit_P2_Comparers          |                |   975.547 μs | 1.740 | 121.0938 | 2.9297 | 1014487 B |        1.23 |
| InAtIn_Inline_Warm_PlanOnly      |                | 1,000.759 μs | 1.785 |  74.2188 |      - |  633610 B |        0.77 |
| RecursiveCte_Prepare_NoHash      |                | 1,010.280 μs | 1.802 | 142.5781 |      - | 1197634 B |        1.45 |
| RecursiveCte_RawHit_P0_Capture   |                | 1,110.583 μs | 1.980 | 148.4375 | 3.9063 | 1256035 B |        1.52 |
| RecursiveCte_RawHit              |                | 1,111.040 μs | 1.981 | 148.4375 | 3.9063 | 1256035 B |        1.52 |
| Cte_Prepare_NoHash               |                | 1,124.567 μs | 2.005 | 130.8594 |      - | 1106499 B |        1.34 |
| RecursiveCte_RawHit_P2_Comparers |                | 1,145.352 μs | 2.042 | 152.3438 | 3.9063 | 1277636 B |        1.54 |
| Join4_Prepare_NoHash             |                | 1,154.774 μs | 2.059 | 128.9063 |      - | 1092120 B |        1.32 |
| Join4_Warm_PlanOnly              |                | 1,240.215 μs | 2.212 | 117.1875 |      - |  984910 B |        1.19 |
| Join4_Prepare_Hash               |                | 1,283.842 μs | 2.289 | 132.8125 | 1.9531 | 1123323 B |        1.36 |
| Cte_PlanKey                      |                | 1,308.539 μs | 2.334 | 123.0469 | 1.9531 | 1032893 B |        1.25 |
| Cte_Warm_PlanOnly                |                | 1,347.931 μs | 2.404 | 123.0469 |      - | 1040894 B |        1.26 |
| RecursiveCte_Prepare_Hash        |                | 1,361.370 μs | 2.428 | 160.1563 | 1.9531 | 1341636 B |        1.62 |
| RecursiveCte_RawHit_Lookup       |                | 1,394.705 μs | 2.487 | 152.3438 | 1.9531 | 1277636 B |        1.54 |
| RecursiveCte_Warm_PlanOnly       |                | 1,460.816 μs | 2.605 | 152.3438 |      - | 1284836 B |        1.55 |
| Cte_Prepare_Hash                 |                | 1,467.121 μs | 2.616 | 142.5781 | 1.9531 | 1198507 B |        1.45 |
| Cte_RawHit_Lookup                |                | 1,905.627 μs | 3.398 | 121.0938 | 1.9531 | 1025692 B |        1.24 |
| RecursiveCte_PlanKey             |                | 2,759.506 μs | 4.921 | 152.3438 |      - | 1284830 B |        1.55 |
| Cte_Warm_EndToEnd                |                | 3,267.908 μs | 5.828 | 132.8125 |      - | 1120111 B |        1.35 |
| RecursiveCte_Warm_EndToEnd       |                | 4,211.506 μs | 7.510 | 156.2500 |      - | 1360067 B |        1.64 |
