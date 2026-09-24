```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Iterations | Mean         | Error      | StdDev     | Median       |
|----------------- |----------- |-------------:|-----------:|-----------:|-------------:|
| NextormPrepared  | 1          |     9.232 μs |  0.1403 μs |  0.1244 μs |     9.209 μs |
| Linq2Db_Compiled | 1          |    15.718 μs |  0.3131 μs |  0.3727 μs |    15.622 μs |
| Linq2Db          | 1          |    29.385 μs |  0.5677 μs |  0.7959 μs |    29.285 μs |
| NextormPrepared  | 3          |    30.163 μs |  0.4144 μs |  0.3460 μs |    30.297 μs |
| NextormCached    | 1          |    43.018 μs |  0.8368 μs |  1.4209 μs |    42.907 μs |
| NextormPrepared  | 5          |    52.204 μs |  0.9380 μs |  1.1863 μs |    52.351 μs |
| Linq2Db_Compiled | 3          |    52.772 μs |  0.6935 μs |  0.6148 μs |    52.537 μs |
| Linq2Db_Compiled | 5          |    86.620 μs |  0.7446 μs |  0.6965 μs |    86.665 μs |
| Linq2Db          | 3          |    96.398 μs |  1.0654 μs |  0.9966 μs |    96.207 μs |
| NextormCached    | 3          |   102.387 μs |  2.0335 μs |  3.9179 μs |   102.011 μs |
| NextormPrepared  | 10         |   104.154 μs |  2.0381 μs |  2.3471 μs |   103.780 μs |
| NextormCached    | 5          |   141.684 μs |  2.8314 μs |  3.0295 μs |   141.328 μs |
| NextormPrepared  | 15         |   160.180 μs |  3.1641 μs |  5.7056 μs |   158.531 μs |
| Linq2Db          | 5          |   165.603 μs |  3.2934 μs |  4.3966 μs |   163.515 μs |
| Linq2Db_Compiled | 10         |   178.780 μs |  2.2547 μs |  2.1090 μs |   178.878 μs |
| NextormPrepared  | 20         |   202.554 μs |  0.9029 μs |  0.8004 μs |   202.620 μs |
| Dapper           | 1          |   250.813 μs |  3.3691 μs |  2.9866 μs |   251.700 μs |
| Linq2Db_Compiled | 15         |   264.861 μs |  1.9860 μs |  1.7606 μs |   264.911 μs |
| NextormCached    | 10         |   270.590 μs |  5.2701 μs |  4.4007 μs |   269.890 μs |
| NextormPrepared  | 30         |   305.826 μs |  2.3132 μs |  2.0506 μs |   306.012 μs |
| Linq2Db          | 10         |   329.942 μs |  5.4811 μs |  5.1270 μs |   327.138 μs |
| Linq2Db_Compiled | 20         |   350.522 μs |  4.8782 μs |  4.5630 μs |   349.774 μs |
| NextormCached    | 15         |   385.227 μs |  5.4026 μs |  4.5114 μs |   385.664 μs |
| Linq2Db          | 15         |   478.574 μs |  5.8043 μs |  5.1454 μs |   477.421 μs |
| NextormCached    | 20         |   498.032 μs |  4.2268 μs |  3.9537 μs |   497.258 μs |
| Linq2Db_Compiled | 30         |   523.827 μs |  4.4109 μs |  4.1260 μs |   522.760 μs |
| Linq2Db          | 20         |   640.666 μs |  6.7815 μs |  6.0116 μs |   641.339 μs |
| NextormCached    | 30         |   718.955 μs | 10.7374 μs | 10.0437 μs |   720.724 μs |
| Dapper           | 3          |   772.487 μs | 14.8851 μs | 22.2793 μs |   768.407 μs |
| Dapper           | 5          |   777.583 μs | 15.3979 μs | 33.7987 μs |   764.292 μs |
| Dapper           | 10         |   937.641 μs | 19.1855 μs | 55.9650 μs |   932.910 μs |
| Dapper           | 15         |   965.958 μs |  8.6308 μs |  8.0732 μs |   968.323 μs |
| Linq2Db          | 30         | 1,008.287 μs | 13.8450 μs | 12.2733 μs | 1,007.527 μs |
| Dapper           | 20         | 1,028.284 μs |  6.2737 μs |  5.8684 μs | 1,028.496 μs |
| Dapper           | 30         | 1,199.595 μs | 11.3652 μs |  9.4904 μs | 1,203.006 μs |
