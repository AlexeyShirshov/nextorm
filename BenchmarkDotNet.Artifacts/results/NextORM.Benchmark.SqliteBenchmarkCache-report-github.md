```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Iterations | Mean         | Error      | StdDev      | Median       |
|----------------- |----------- |-------------:|-----------:|------------:|-------------:|
| NextormPrepared  | 1          |     9.626 μs |  0.1878 μs |   0.3086 μs |     9.562 μs |
| Linq2Db_Compiled | 1          |    16.693 μs |  0.3602 μs |   1.0565 μs |    16.451 μs |
| Linq2Db          | 1          |    30.157 μs |  0.6010 μs |   1.4966 μs |    30.045 μs |
| NextormPrepared  | 3          |    30.663 μs |  0.3711 μs |   0.3290 μs |    30.653 μs |
| NextormPrepared  | 5          |    52.619 μs |  0.5739 μs |   0.4792 μs |    52.602 μs |
| Linq2Db_Compiled | 3          |    53.039 μs |  0.5643 μs |   0.5003 μs |    52.999 μs |
| NextormCached    | 1          |    53.154 μs |  1.0487 μs |   1.9439 μs |    52.917 μs |
| Linq2Db_Compiled | 5          |    89.087 μs |  1.2118 μs |   1.0119 μs |    89.036 μs |
| Linq2Db          | 3          |    96.709 μs |  0.9898 μs |   0.8266 μs |    96.452 μs |
| NextormPrepared  | 10         |   103.120 μs |  1.9377 μs |   3.8699 μs |   102.725 μs |
| NextormCached    | 3          |   107.834 μs |  1.2294 μs |   1.0899 μs |   107.558 μs |
| NextormPrepared  | 15         |   161.994 μs |  3.1626 μs |   5.7029 μs |   161.578 μs |
| Linq2Db          | 5          |   164.796 μs |  3.2835 μs |   3.2249 μs |   164.691 μs |
| NextormCached    | 5          |   167.312 μs |  3.2198 μs |   4.0721 μs |   166.387 μs |
| Linq2Db_Compiled | 10         |   184.324 μs |  4.3628 μs |  12.3766 μs |   179.504 μs |
| NextormPrepared  | 20         |   205.863 μs |  1.0472 μs |   0.9283 μs |   205.985 μs |
| Dapper           | 1          |   266.143 μs |  5.2538 μs |   7.7009 μs |   264.900 μs |
| Linq2Db_Compiled | 15         |   270.249 μs |  5.2564 μs |   7.8676 μs |   268.509 μs |
| NextormCached    | 10         |   282.192 μs |  5.5480 μs |   8.3040 μs |   281.977 μs |
| NextormPrepared  | 30         |   318.081 μs |  6.2888 μs |   6.7289 μs |   316.746 μs |
| Linq2Db          | 10         |   323.304 μs |  6.0581 μs |   5.3704 μs |   321.296 μs |
| Linq2Db_Compiled | 20         |   355.429 μs |  3.3908 μs |   2.6473 μs |   355.372 μs |
| NextormCached    | 15         |   437.822 μs |  8.7550 μs |  10.4222 μs |   436.207 μs |
| Linq2Db          | 15         |   542.026 μs | 11.1908 μs |  32.1084 μs |   539.352 μs |
| NextormCached    | 20         |   566.225 μs | 11.1169 μs |  12.3564 μs |   566.990 μs |
| Linq2Db_Compiled | 30         |   568.691 μs | 10.9907 μs |  13.0837 μs |   570.369 μs |
| Linq2Db          | 20         |   655.682 μs |  7.9681 μs |   6.6537 μs |   653.839 μs |
| Dapper           | 3          |   779.479 μs | 14.9295 μs |  15.9744 μs |   775.566 μs |
| Dapper           | 5          |   826.967 μs | 19.2279 μs |  54.2327 μs |   820.114 μs |
| NextormCached    | 30         |   857.996 μs | 16.4919 μs |  18.3307 μs |   853.419 μs |
| Dapper           | 10         |   931.354 μs | 28.6305 μs |  82.6055 μs |   907.899 μs |
| Dapper           | 20         | 1,053.616 μs | 20.2196 μs |  28.3451 μs | 1,050.490 μs |
| Linq2Db          | 30         | 1,185.117 μs | 75.2941 μs | 218.4418 μs | 1,095.446 μs |
| Dapper           | 15         | 1,254.669 μs | 76.2583 μs | 224.8495 μs | 1,171.713 μs |
| Dapper           | 30         | 1,473.438 μs | 54.7095 μs | 160.4536 μs | 1,442.344 μs |
