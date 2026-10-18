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
| Gen_P0_From                      |                |     3.550 μs | 0.007 |   4.7798 |      - |   40000 B |        0.05 |
| InAtIn_Inline_Warm_Reused        |                |     4.816 μs | 0.009 |        - |      - |         - |        0.00 |
| Intersect_Warm_Reused            | Warm_Intersect |     4.950 μs | 0.009 |        - |      - |         - |        0.00 |
| Join4_Warm_Reused                |                |     5.406 μs | 0.010 |        - |      - |         - |        0.00 |
| Except_Warm_Reused               | Warm_Except    |     5.436 μs | 0.010 |        - |      - |         - |        0.00 |
| RecursiveCte_Warm_Reused         |                |     7.401 μs | 0.014 |        - |      - |         - |        0.00 |
| Cte_Warm_Reused                  |                |     8.747 μs | 0.016 |        - |      - |         - |        0.00 |
| Gen_P1_Where                     |                |    77.099 μs | 0.143 |  19.2871 |      - |  161604 B |        0.20 |
| RecursiveCte_P0_Anchor           |                |   114.699 μs | 0.212 |  31.4941 |      - |  264006 B |        0.32 |
| Intersect_Construct              | Warm_Intersect |   167.071 μs | 0.309 |  43.2129 | 0.2441 |  363216 B |        0.44 |
| Gen_P2_Select                    |                |   167.656 μs | 0.311 |  39.7949 |      - |  333608 B |        0.41 |
| Except_Construct                 | Warm_Except    |   169.053 μs | 0.313 |  43.2129 | 0.2441 |  363216 B |        0.44 |
| RecursiveCte_P1_Step             |                |   189.997 μs | 0.352 |  45.1660 | 0.2441 |  379209 B |        0.46 |
| InAtIn_Captured_Construct        |                |   195.596 μs | 0.362 |  34.6680 |      - |  291201 B |        0.36 |
| InAtIn_Inline_Construct          |                |   213.286 μs | 0.395 |  37.1094 |      - |  310401 B |        0.38 |
| Cte_P0_Body                      |                |   223.670 μs | 0.414 |  38.8184 |      - |  326414 B |        0.40 |
| Cte_P2_From                      |                |   245.105 μs | 0.454 |  45.1660 |      - |  378416 B |        0.46 |
| Cte_P1_With                      |                |   246.592 μs | 0.457 |  40.2832 |      - |  338415 B |        0.41 |
| Cte_P3_Join                      |                |   354.725 μs | 0.657 |  64.4531 |      - |  542435 B |        0.66 |
| RecursiveCte_P2_Operands         |                |   380.075 μs | 0.704 |  76.1719 |      - |  643218 B |        0.78 |
| Intersect_Prepare_NoHash         | Warm_Intersect |   398.319 μs | 0.738 |  63.9648 |      - |  537624 B |        0.66 |
| RecursiveCte_P3_UnionAll         |                |   409.073 μs | 0.758 |  86.4258 | 0.4883 |  724817 B |        0.88 |
| InAtIn_Captured_Prepare_NoHash   |                |   419.142 μs | 0.776 |  50.7813 |      - |  428802 B |        0.52 |
| Except_Prepare_NoHash            | Warm_Except    |   429.992 μs | 0.796 |  63.9648 |      - |  537624 B |        0.66 |
| InAtIn_Inline_Prepare_NoHash     |                |   447.217 μs | 0.828 |  53.2227 |      - |  448002 B |        0.55 |
| RecursiveCte_Construct           |                |   453.625 μs | 0.840 | 112.7930 | 0.4883 |  945621 B |        1.15 |
| Intersect_Warm_PlanOnly          | Warm_Intersect |   475.191 μs | 0.880 |  69.8242 | 0.4883 |  588026 B |        0.72 |
| Except_Warm_PlanOnly             | Warm_Except    |   499.244 μs | 0.925 |  69.3359 |      - |  588028 B |        0.72 |
| Intersect_Prepare_Hash           | Warm_Intersect |   516.511 μs | 0.957 |  73.2422 | 0.9766 |  616830 B |        0.75 |
| RecursiveCte_P4_Declared         |                |   517.819 μs | 0.959 | 113.2813 | 0.4883 |  948822 B |        1.16 |
| Cte_Construct                    |                |   539.875 μs | 1.000 |  97.6563 |      - |  820068 B |        1.00 |
| Except_Prepare_Hash              | Warm_Except    |   542.973 μs | 1.006 |  73.2422 | 0.9766 |  616830 B |        0.75 |
| Cte_P4_Select                    |                |   561.891 μs | 1.041 |  97.6563 |      - |  820071 B |        1.00 |
| InAtIn_Captured_Prepare_Hash     |                |   614.116 μs | 1.138 |  63.4766 |      - |  538405 B |        0.66 |
| Join4_Construct                  |                |   690.979 μs | 1.280 | 101.5625 |      - |  854491 B |        1.04 |
| InAtIn_Inline_Prepare_Hash       |                |   722.373 μs | 1.338 |  66.4063 |      - |  562405 B |        0.69 |
| Cte_RawHit                       |                |   858.371 μs | 1.590 | 117.1875 | 1.9531 |  984884 B |        1.20 |
| Cte_RawHit_P0_Capture            |                |   950.664 μs | 1.761 | 117.1875 | 1.9531 |  984884 B |        1.20 |
| Cte_RawHit_P2_Comparers          |                |   960.346 μs | 1.779 | 120.1172 | 2.9297 | 1006486 B |        1.23 |
| InAtIn_Inline_Warm_PlanOnly      |                | 1,049.412 μs | 1.944 |  74.2188 |      - |  633610 B |        0.77 |
| Cte_Prepare_NoHash               |                | 1,062.603 μs | 1.968 | 130.8594 |      - | 1098499 B |        1.34 |
| RecursiveCte_RawHit              |                | 1,139.981 μs | 2.112 | 148.4375 | 3.9063 | 1256035 B |        1.53 |
| RecursiveCte_RawHit_P2_Comparers |                | 1,174.979 μs | 2.176 | 152.3438 | 3.9063 | 1277636 B |        1.56 |
| Join4_Prepare_NoHash             |                | 1,181.482 μs | 2.189 | 128.9063 |      - | 1092119 B |        1.33 |
| RecursiveCte_RawHit_P0_Capture   |                | 1,182.526 μs | 2.190 | 148.4375 | 3.9063 | 1256035 B |        1.53 |
| Cte_RawHit_Lookup                |                | 1,187.852 μs | 2.200 | 121.0938 |      - | 1017687 B |        1.24 |
| Cte_PlanKey                      |                | 1,195.016 μs | 2.214 | 121.0938 | 1.9531 | 1024892 B |        1.25 |
| Cte_Warm_PlanOnly                |                | 1,293.582 μs | 2.396 | 123.0469 |      - | 1032894 B |        1.26 |
| RecursiveCte_Prepare_NoHash      |                | 1,321.339 μs | 2.448 | 142.5781 |      - | 1197634 B |        1.46 |
| Cte_Prepare_Hash                 |                | 1,334.123 μs | 2.471 | 140.6250 | 1.9531 | 1190506 B |        1.45 |
| RecursiveCte_Prepare_Hash        |                | 1,377.236 μs | 2.551 | 160.1563 | 1.9531 | 1341637 B |        1.64 |
| Join4_Warm_PlanOnly              |                | 1,377.329 μs | 2.551 | 117.1875 |      - |  984910 B |        1.20 |
| Join4_Prepare_Hash               |                | 1,389.670 μs | 2.574 | 132.8125 | 1.9531 | 1123323 B |        1.37 |
| RecursiveCte_PlanKey             |                | 1,406.345 μs | 2.605 | 152.3438 |      - | 1284836 B |        1.57 |
| RecursiveCte_RawHit_Lookup       |                | 1,449.876 μs | 2.686 | 152.3438 | 1.9531 | 1277636 B |        1.56 |
| RecursiveCte_Warm_PlanOnly       |                | 1,488.649 μs | 2.757 | 152.3438 |      - | 1284836 B |        1.57 |
| Cte_Warm_EndToEnd                |                | 3,513.503 μs | 6.508 | 132.8125 |      - | 1112111 B |        1.36 |
| RecursiveCte_Warm_EndToEnd       |                | 3,801.131 μs | 7.041 | 160.1563 |      - | 1360028 B |        1.66 |
