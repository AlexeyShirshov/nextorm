```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Iterations | Mean         | Error      | StdDev     |
|----------------- |----------- |-------------:|-----------:|-----------:|
| NextormPrepared  | 1          |     8.953 μs |  0.0342 μs |  0.0303 μs |
| Linq2Db_Compiled | 1          |    14.765 μs |  0.0639 μs |  0.0598 μs |
| Linq2Db          | 1          |    27.999 μs |  0.3208 μs |  0.3000 μs |
| NextormPrepared  | 3          |    29.167 μs |  0.0670 μs |  0.0627 μs |
| Linq2Db_Compiled | 3          |    49.046 μs |  0.3507 μs |  0.3281 μs |
| NextormPrepared  | 5          |    50.375 μs |  0.3261 μs |  0.3051 μs |
| Linq2Db_Compiled | 5          |    83.897 μs |  0.6792 μs |  0.6354 μs |
| Linq2Db          | 3          |    95.992 μs |  0.4738 μs |  0.3956 μs |
| NextormPrepared  | 10         |   100.392 μs |  0.3702 μs |  0.3463 μs |
| NextormPrepared  | 15         |   152.222 μs |  0.6557 μs |  0.6133 μs |
| Linq2Db          | 5          |   156.730 μs |  1.3281 μs |  1.1773 μs |
| Linq2Db_Compiled | 10         |   171.108 μs |  1.0591 μs |  0.9907 μs |
| NextormPrepared  | 20         |   204.293 μs |  0.6765 μs |  0.6328 μs |
| Dapper           | 1          |   236.499 μs |  3.3815 μs |  3.1631 μs |
| Linq2Db_Compiled | 15         |   255.918 μs |  1.8649 μs |  1.6532 μs |
| NextormCached    | 1          |   264.941 μs |  3.2518 μs |  3.0418 μs |
| NextormPrepared  | 30         |   305.427 μs |  2.8892 μs |  2.4127 μs |
| NextormCached    | 3          |   318.031 μs |  3.0432 μs |  2.8466 μs |
| Linq2Db          | 10         |   319.676 μs |  1.6710 μs |  1.3046 μs |
| Linq2Db_Compiled | 20         |   345.987 μs |  5.3612 μs |  4.4769 μs |
| NextormCached    | 5          |   364.041 μs |  3.5237 μs |  3.2961 μs |
| NextormCached    | 10         |   471.220 μs |  2.5741 μs |  2.2819 μs |
| Linq2Db          | 15         |   477.992 μs |  4.8767 μs |  4.5617 μs |
| Linq2Db_Compiled | 30         |   514.823 μs |  2.0576 μs |  1.9247 μs |
| NextormCached    | 15         |   593.096 μs |  5.1539 μs |  4.8210 μs |
| Linq2Db          | 20         |   672.549 μs |  3.2149 μs |  2.6846 μs |
| NextormCached    | 20         |   700.814 μs |  8.5527 μs |  7.5818 μs |
| Dapper           | 3          |   711.189 μs |  5.7799 μs |  5.4065 μs |
| Dapper           | 5          |   744.157 μs |  5.4839 μs |  4.8613 μs |
| Dapper           | 10         |   814.778 μs |  4.1034 μs |  3.6375 μs |
| NextormCached    | 30         |   906.086 μs | 15.6221 μs | 14.6129 μs |
| Dapper           | 15         |   915.075 μs | 10.7658 μs | 10.0703 μs |
| Linq2Db          | 30         |   967.865 μs | 12.8830 μs | 12.0508 μs |
| Dapper           | 20         |   995.216 μs |  4.8313 μs |  4.5192 μs |
| Dapper           | 30         | 1,186.104 μs |  7.6458 μs |  6.7778 μs |
