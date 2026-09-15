```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method          | Iterations | Mean       | Error     | StdDev   |
|---------------- |----------- |-----------:|----------:|---------:|
| Dapper          | 1          |   228.1 μs |   4.46 μs |  0.24 μs |
| NextormCached   | 1          |   268.6 μs |  19.30 μs |  1.06 μs |
| NextormPrepared | 1          |   271.4 μs |  21.09 μs |  1.16 μs |
| NextormPrepared | 3          |   293.7 μs |  33.23 μs |  1.82 μs |
| NextormPrepared | 5          |   315.6 μs |   6.62 μs |  0.36 μs |
| NextormCached   | 3          |   336.3 μs |  50.19 μs |  2.75 μs |
| NextormPrepared | 10         |   375.6 μs |  74.04 μs |  4.06 μs |
| NextormCached   | 5          |   386.6 μs |  54.61 μs |  2.99 μs |
| NextormPrepared | 15         |   412.4 μs |  23.10 μs |  1.27 μs |
| NextormPrepared | 20         |   445.9 μs |  50.07 μs |  2.74 μs |
| NextormCached   | 10         |   510.5 μs | 180.55 μs |  9.90 μs |
| NextormPrepared | 30         |   539.4 μs |  69.71 μs |  3.82 μs |
| NextormCached   | 15         |   651.4 μs | 128.48 μs |  7.04 μs |
| Dapper          | 3          |   707.5 μs |  42.54 μs |  2.33 μs |
| Dapper          | 5          |   740.0 μs | 101.92 μs |  5.59 μs |
| NextormCached   | 20         |   747.1 μs | 203.87 μs | 11.17 μs |
| Dapper          | 10         |   869.6 μs |  76.61 μs |  4.20 μs |
| Dapper          | 15         |   991.2 μs | 463.74 μs | 25.42 μs |
| NextormCached   | 30         | 1,003.9 μs | 255.82 μs | 14.02 μs |
| Dapper          | 20         | 1,052.7 μs | 197.35 μs | 10.82 μs |
| Dapper          | 30         | 1,301.0 μs | 896.83 μs | 49.16 μs |
