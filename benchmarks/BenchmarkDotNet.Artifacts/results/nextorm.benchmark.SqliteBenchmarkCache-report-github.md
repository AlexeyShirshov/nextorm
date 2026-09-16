```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Iterations | Mean          | Error       | StdDev        | Median        |
|----------------- |----------- |--------------:|------------:|--------------:|--------------:|
| Linq2Db_Compiled | 10         |            NA |          NA |            NA |            NA |
| NextormCached    | 15         |            NA |          NA |            NA |            NA |
| NextormPrepared  | 15         |            NA |          NA |            NA |            NA |
| Dapper           | 15         |            NA |          NA |            NA |            NA |
| Linq2Db          | 15         |            NA |          NA |            NA |            NA |
| Linq2Db_Compiled | 15         |            NA |          NA |            NA |            NA |
| NextormCached    | 20         |            NA |          NA |            NA |            NA |
| NextormPrepared  | 20         |            NA |          NA |            NA |            NA |
| Dapper           | 20         |            NA |          NA |            NA |            NA |
| Linq2Db          | 20         |            NA |          NA |            NA |            NA |
| Linq2Db_Compiled | 20         |            NA |          NA |            NA |            NA |
| NextormCached    | 30         |            NA |          NA |            NA |            NA |
| NextormPrepared  | 30         |            NA |          NA |            NA |            NA |
| Dapper           | 30         |            NA |          NA |            NA |            NA |
| Linq2Db          | 30         |            NA |          NA |            NA |            NA |
| Linq2Db_Compiled | 30         |            NA |          NA |            NA |            NA |
| NextormPrepared  | 1          |      9.205 μs |   0.1381 μs |     0.1224 μs |      9.257 μs |
| Linq2Db_Compiled | 1          |     15.634 μs |   0.2701 μs |     0.2653 μs |     15.625 μs |
| NextormPrepared  | 3          |     29.685 μs |   0.3402 μs |     0.3016 μs |     29.615 μs |
| Linq2Db          | 1          |     32.780 μs |   0.6472 μs |     1.5000 μs |     32.947 μs |
| NextormCached    | 1          |     37.187 μs |   0.7390 μs |     0.7258 μs |     36.899 μs |
| Linq2Db_Compiled | 3          |     78.603 μs |   1.5536 μs |     2.7615 μs |     77.870 μs |
| NextormCached    | 3          |     88.652 μs |   1.7372 μs |     2.0681 μs |     88.382 μs |
| NextormCached    | 5          |    100.216 μs |   2.5010 μs |     7.2954 μs |     97.463 μs |
| Linq2Db          | 3          |    129.892 μs |   2.5550 μs |     5.0433 μs |    128.036 μs |
| Dapper           | 1          |    246.584 μs |   4.7508 μs |     6.1774 μs |    244.441 μs |
| Dapper           | 3          |    712.860 μs |  13.2080 μs |    10.3119 μs |    710.599 μs |
| NextormPrepared  | 5          |  1,799.623 μs |  35.9852 μs |    75.9051 μs |  1,768.272 μs |
| NextormPrepared  | 10         |  2,926.125 μs |  39.9907 μs |    33.3940 μs |  2,929.928 μs |
| Dapper           | 5          |  3,934.959 μs | 144.4604 μs |   402.6978 μs |  3,830.013 μs |
| NextormCached    | 10         |  4,819.554 μs |  66.8981 μs |    59.3034 μs |  4,810.226 μs |
| Linq2Db_Compiled | 5          |  4,893.581 μs |  97.2796 μs |   108.1260 μs |  4,866.251 μs |
| Linq2Db          | 5          |  5,484.123 μs | 357.8577 μs | 1,032.5014 μs |  4,985.813 μs |
| Linq2Db          | 10         | 47,512.537 μs | 944.0922 μs |   927.2248 μs | 47,348.178 μs |
| Dapper           | 10         | 51,291.684 μs | 741.1337 μs |   618.8802 μs | 51,236.588 μs |

Benchmarks with issues:
  SqliteBenchmarkCache.Linq2Db_Compiled: DefaultJob [Iterations=10]
  SqliteBenchmarkCache.NextormCached: DefaultJob [Iterations=15]
  SqliteBenchmarkCache.NextormPrepared: DefaultJob [Iterations=15]
  SqliteBenchmarkCache.Dapper: DefaultJob [Iterations=15]
  SqliteBenchmarkCache.Linq2Db: DefaultJob [Iterations=15]
  SqliteBenchmarkCache.Linq2Db_Compiled: DefaultJob [Iterations=15]
  SqliteBenchmarkCache.NextormCached: DefaultJob [Iterations=20]
  SqliteBenchmarkCache.NextormPrepared: DefaultJob [Iterations=20]
  SqliteBenchmarkCache.Dapper: DefaultJob [Iterations=20]
  SqliteBenchmarkCache.Linq2Db: DefaultJob [Iterations=20]
  SqliteBenchmarkCache.Linq2Db_Compiled: DefaultJob [Iterations=20]
  SqliteBenchmarkCache.NextormCached: DefaultJob [Iterations=30]
  SqliteBenchmarkCache.NextormPrepared: DefaultJob [Iterations=30]
  SqliteBenchmarkCache.Dapper: DefaultJob [Iterations=30]
  SqliteBenchmarkCache.Linq2Db: DefaultJob [Iterations=30]
  SqliteBenchmarkCache.Linq2Db_Compiled: DefaultJob [Iterations=30]
