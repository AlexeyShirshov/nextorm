```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-UVQKOB : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=20  

```
| Method          | Iterations | Mean      | Error     | StdDev    |
|---------------- |----------- |----------:|----------:|----------:|
| Dapper          | 1          |  1.188 ms | 0.0439 ms | 0.0505 ms |
| NextormPrepared | 1          |  1.232 ms | 0.0079 ms | 0.0091 ms |
| NextormCached   | 1          |  1.265 ms | 0.0326 ms | 0.0335 ms |
| NextormCached   | 3          |  3.195 ms | 0.0165 ms | 0.0177 ms |
| NextormPrepared | 3          |  3.316 ms | 0.1683 ms | 0.1870 ms |
| Dapper          | 3          |  3.571 ms | 0.0172 ms | 0.0191 ms |
| NextormPrepared | 5          |  4.871 ms | 0.0648 ms | 0.0747 ms |
| NextormCached   | 5          |  5.100 ms | 0.0974 ms | 0.1082 ms |
| Dapper          | 5          |  5.639 ms | 0.0569 ms | 0.0655 ms |
| NextormPrepared | 10         |  9.811 ms | 0.3895 ms | 0.4486 ms |
| NextormCached   | 10         |  9.868 ms | 0.0926 ms | 0.1067 ms |
| Dapper          | 10         | 10.199 ms | 0.0777 ms | 0.0763 ms |
| NextormPrepared | 15         | 13.700 ms | 0.0530 ms | 0.0589 ms |
| NextormCached   | 15         | 14.554 ms | 0.2801 ms | 0.3225 ms |
| Dapper          | 15         | 14.723 ms | 0.0444 ms | 0.0456 ms |
| NextormCached   | 20         | 19.170 ms | 0.0721 ms | 0.0708 ms |
| NextormPrepared | 20         | 19.616 ms | 0.5235 ms | 0.6028 ms |
| Dapper          | 20         | 19.648 ms | 0.2046 ms | 0.2356 ms |
| NextormPrepared | 30         | 26.737 ms | 0.2239 ms | 0.2396 ms |
| Dapper          | 30         | 28.503 ms | 0.9988 ms | 1.1502 ms |
| NextormCached   | 30         | 29.353 ms | 1.0452 ms | 1.2036 ms |
