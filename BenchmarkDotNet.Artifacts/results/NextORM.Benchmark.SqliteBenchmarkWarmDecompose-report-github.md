```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Runtime=.NET 10.0  

```
| Method                         | Mean         | Median       | Ratio | Gen0     | Allocated | Alloc Ratio |
|------------------------------- |-------------:|-------------:|------:|---------:|----------:|------------:|
| Join4_Warm_Reused              |     3.846 μs |     3.844 μs | 0.006 |        - |         - |        0.00 |
| RecursiveCte_Warm_Reused       |     4.265 μs |     4.268 μs | 0.007 |        - |         - |        0.00 |
| Cte_Warm_Reused                |     4.343 μs |     4.341 μs | 0.007 |        - |         - |        0.00 |
| InAtIn_Inline_Warm_Reused      |    54.250 μs |    54.264 μs | 0.091 |   7.8125 |   67200 B |        0.08 |
| InAtIn_Captured_Construct      |   184.366 μs |   184.700 μs | 0.310 |  38.0859 |  321601 B |        0.38 |
| InAtIn_Inline_Construct        |   198.024 μs |   196.174 μs | 0.333 |  38.0859 |  326400 B |        0.38 |
| InAtIn_Captured_Prepare_NoHash |   314.200 μs |   320.363 μs | 0.528 |  52.7344 |  441602 B |        0.52 |
| InAtIn_Inline_Prepare_NoHash   |   324.069 μs |   322.055 μs | 0.545 |  54.6875 |  460800 B |        0.54 |
| InAtIn_Captured_Prepare_Hash   |   446.032 μs |   444.281 μs | 0.750 |  66.4063 |  558400 B |        0.66 |
| RecursiveCte_Construct         |   486.679 μs |   486.060 μs | 0.818 | 121.0938 | 1022421 B |        1.20 |
| InAtIn_Inline_Prepare_Hash     |   497.942 μs |   482.906 μs | 0.837 |  66.4063 |  568000 B |        0.67 |
| InAtIn_Inline_Warm_PlanOnly    |   585.775 μs |   581.516 μs | 0.985 |  66.4063 |  584000 B |        0.69 |
| Cte_Construct                  |   594.769 μs |   592.898 μs | 1.000 | 101.5625 |  852070 B |        1.00 |
| Join4_Construct                |   711.653 μs |   713.380 μs | 1.197 | 105.4688 |  892090 B |        1.05 |
| RecursiveCte_Prepare_NoHash    |   817.971 μs |   805.588 μs | 1.376 | 156.2500 | 1339227 B |        1.57 |
| Join4_Prepare_NoHash           |   991.396 μs |   979.937 μs | 1.667 | 132.8125 | 1120918 B |        1.32 |
| RecursiveCte_Prepare_Hash      | 1,006.214 μs |   993.171 μs | 1.692 | 171.8750 | 1468830 B |        1.72 |
| Cte_Prepare_NoHash             | 1,044.866 μs | 1,003.480 μs | 1.757 | 140.6250 | 1197700 B |        1.41 |
| Cte_Prepare_Hash               | 1,220.667 μs | 1,209.794 μs | 2.053 | 148.4375 | 1289705 B |        1.51 |
| Join4_Prepare_Hash             | 1,255.292 μs | 1,122.798 μs | 2.111 | 132.8125 | 1132118 B |        1.33 |
| RecursiveCte_Warm_PlanOnly     | 1,270.144 μs | 1,262.063 μs | 2.136 | 171.8750 | 1477630 B |        1.73 |
| Join4_Warm_PlanOnly            | 1,357.656 μs | 1,355.549 μs | 2.283 | 132.8125 | 1145718 B |        1.34 |
| Cte_Warm_PlanOnly              | 2,974.397 μs | 1,579.763 μs | 5.002 | 140.6250 | 1295305 B |        1.52 |
