```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Iterations | Mean         | Error         | StdDev     |
|----------------- |----------- |-------------:|--------------:|-----------:|
| NextormPrepared  | 1          |     9.164 μs |     0.2444 μs |  0.0134 μs |
| Linq2Db_Compiled | 1          |    15.563 μs |     1.8725 μs |  0.1026 μs |
| NextormPrepared  | 3          |    30.378 μs |     1.9877 μs |  0.1090 μs |
| Linq2Db          | 1          |    32.204 μs |     5.3072 μs |  0.2909 μs |
| NextormCached    | 1          |    43.835 μs |    12.4392 μs |  0.6818 μs |
| NextormPrepared  | 5          |    52.136 μs |     9.0199 μs |  0.4944 μs |
| Linq2Db_Compiled | 3          |    52.622 μs |     6.7732 μs |  0.3713 μs |
| Linq2Db_Compiled | 5          |    88.714 μs |    16.8176 μs |  0.9218 μs |
| NextormCached    | 3          |   103.030 μs |    10.7037 μs |  0.5867 μs |
| NextormPrepared  | 10         |   104.404 μs |     5.8850 μs |  0.3226 μs |
| Linq2Db          | 3          |   110.011 μs |    12.4773 μs |  0.6839 μs |
| NextormCached    | 5          |   163.210 μs |    34.3915 μs |  1.8851 μs |
| NextormPrepared  | 15         |   164.586 μs |    37.3439 μs |  2.0469 μs |
| Linq2Db_Compiled | 10         |   177.594 μs |    17.5823 μs |  0.9637 μs |
| Linq2Db          | 5          |   184.649 μs |    52.9308 μs |  2.9013 μs |
| NextormPrepared  | 20         |   214.728 μs |    22.6660 μs |  1.2424 μs |
| Dapper           | 1          |   253.575 μs |    35.1893 μs |  1.9288 μs |
| Linq2Db_Compiled | 15         |   280.407 μs |    22.3929 μs |  1.2274 μs |
| NextormCached    | 10         |   296.015 μs |     8.6652 μs |  0.4750 μs |
| Linq2Db_Compiled | 20         |   369.848 μs |   255.6037 μs | 14.0105 μs |
| Linq2Db          | 10         |   374.397 μs |     8.4252 μs |  0.4618 μs |
| NextormCached    | 15         |   434.704 μs |   121.7165 μs |  6.6717 μs |
| NextormPrepared  | 30         |   554.003 μs |   586.4664 μs | 32.1462 μs |
| Linq2Db_Compiled | 30         |   569.622 μs |    72.6627 μs |  3.9829 μs |
| NextormCached    | 20         |   587.418 μs |    24.3221 μs |  1.3332 μs |
| Linq2Db          | 15         |   644.165 μs |   670.7350 μs | 36.7653 μs |
| Linq2Db          | 20         |   751.142 μs |   100.1242 μs |  5.4881 μs |
| Dapper           | 3          |   765.959 μs |   189.0475 μs | 10.3623 μs |
| Dapper           | 5          |   800.842 μs |    84.9700 μs |  4.6575 μs |
| Dapper           | 10         |   867.769 μs |    29.5629 μs |  1.6204 μs |
| NextormCached    | 30         |   873.868 μs |   474.2683 μs | 25.9962 μs |
| Dapper           | 15         | 1,039.165 μs |   256.9594 μs | 14.0848 μs |
| Dapper           | 20         | 1,083.650 μs |   432.9126 μs | 23.7294 μs |
| Linq2Db          | 30         | 1,169.559 μs |   169.6940 μs |  9.3015 μs |
| Dapper           | 30         | 1,270.556 μs | 1,197.9788 μs | 65.6653 μs |
