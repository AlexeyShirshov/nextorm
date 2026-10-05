```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Iterations | Mean        | Error      | StdDev     | Median      |
|----------------- |----------- |------------:|-----------:|-----------:|------------:|
| NextormPrepared  | 1          |    11.57 μs |   0.218 μs |   0.387 μs |    11.44 μs |
| Linq2Db_Compiled | 1          |    20.06 μs |   0.391 μs |   0.326 μs |    20.08 μs |
| Linq2Db          | 1          |    38.84 μs |   0.715 μs |   1.215 μs |    38.67 μs |
| NextormPrepared  | 3          |    39.17 μs |   0.749 μs |   0.700 μs |    39.10 μs |
| NextormPrepared  | 5          |    64.67 μs |   1.242 μs |   1.479 μs |    64.52 μs |
| Linq2Db_Compiled | 3          |    67.71 μs |   1.354 μs |   1.663 μs |    67.58 μs |
| Linq2Db_Compiled | 5          |   113.76 μs |   2.221 μs |   2.181 μs |   113.50 μs |
| Linq2Db          | 3          |   130.16 μs |   2.574 μs |   6.265 μs |   128.86 μs |
| NextormPrepared  | 10         |   132.54 μs |   2.562 μs |   3.241 μs |   131.80 μs |
| NextormPrepared  | 15         |   200.02 μs |   3.727 μs |   3.303 μs |   199.26 μs |
| Linq2Db          | 5          |   212.22 μs |   4.098 μs |  10.356 μs |   210.47 μs |
| Linq2Db_Compiled | 10         |   234.41 μs |   4.239 μs |   8.269 μs |   233.00 μs |
| NextormPrepared  | 20         |   274.04 μs |   5.098 μs |   6.806 μs |   272.66 μs |
| NextormCached    | 1          |   327.38 μs |  22.919 μs |  66.855 μs |   290.88 μs |
| Dapper           | 1          |   338.80 μs |  13.410 μs |  37.159 μs |   321.77 μs |
| Linq2Db_Compiled | 15         |   343.25 μs |   6.746 μs |  10.502 μs |   342.88 μs |
| NextormPrepared  | 30         |   409.06 μs |   6.543 μs |   7.000 μs |   407.96 μs |
| Linq2Db          | 10         |   455.20 μs |  17.113 μs |  44.782 μs |   443.62 μs |
| Linq2Db_Compiled | 20         |   470.76 μs |   9.373 μs |  14.028 μs |   474.13 μs |
| NextormCached    | 3          |   498.10 μs |  57.173 μs | 168.576 μs |   376.85 μs |
| NextormCached    | 5          |   592.23 μs |  77.996 μs | 229.974 μs |   448.78 μs |
| Linq2Db          | 15         |   679.62 μs |  11.579 μs |  14.221 μs |   681.07 μs |
| Linq2Db_Compiled | 30         |   691.82 μs |  12.068 μs |  15.692 μs |   690.51 μs |
| Linq2Db          | 20         |   887.46 μs |  18.037 μs |  48.455 μs |   885.86 μs |
| NextormCached    | 10         |   991.74 μs | 152.754 μs | 450.398 μs |   675.29 μs |
| Dapper           | 3          | 1,016.57 μs |  41.933 μs | 123.640 μs |   948.91 μs |
| Dapper           | 5          | 1,084.10 μs |  45.340 μs | 133.686 μs | 1,014.44 μs |
| Dapper           | 10         | 1,205.63 μs |  44.029 μs | 126.328 μs | 1,145.88 μs |
| Linq2Db          | 30         | 1,288.44 μs |  23.308 μs |  42.619 μs | 1,288.73 μs |
| Dapper           | 20         | 1,378.01 μs |  23.572 μs |  54.161 μs | 1,371.52 μs |
| Dapper           | 15         | 1,732.49 μs |  33.294 μs |  32.699 μs | 1,730.50 μs |
| Dapper           | 30         | 1,869.71 μs | 127.799 μs | 376.819 μs | 1,643.76 μs |
| NextormCached    | 15         | 2,315.77 μs |  38.963 μs |  36.446 μs | 2,319.19 μs |
| NextormCached    | 20         | 3,044.97 μs |  35.050 μs |  29.268 μs | 3,040.96 μs |
| NextormCached    | 30         | 4,335.28 μs |  83.586 μs |  74.097 μs | 4,329.83 μs |
