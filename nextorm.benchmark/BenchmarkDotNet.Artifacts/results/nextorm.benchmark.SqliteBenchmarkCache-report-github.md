```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-XSIDBE : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=20  

```
| Method           | Iterations | Mean       | Error     | StdDev    |
|----------------- |----------- |-----------:|----------:|----------:|
| Dapper           | 1          |   249.5 μs |   5.35 μs |   5.94 μs |
| NextormNonCached | 1          |   315.7 μs |   5.10 μs |   5.45 μs |
| NextormCached    | 1          |   316.7 μs |   4.30 μs |   4.78 μs |
| NextormPrepared  | 1          |   327.2 μs |   7.50 μs |   8.64 μs |
| NextormPrepared  | 2          |   368.0 μs |   9.45 μs |  10.88 μs |
| NextormCached    | 2          |   386.2 μs |   8.88 μs |   9.51 μs |
| NextormPrepared  | 3          |   404.5 μs |  13.19 μs |  15.19 μs |
| NextormPrepared  | 4          |   426.0 μs |  10.12 μs |  10.83 μs |
| NextormCached    | 3          |   454.8 μs |   9.78 μs |  10.87 μs |
| NextormPrepared  | 5          |   466.0 μs |  15.02 μs |  17.30 μs |
| NextormCached    | 4          |   526.7 μs |  28.31 μs |  30.29 μs |
| NextormCached    | 5          |   570.3 μs |   9.96 μs |  11.07 μs |
| NextormPrepared  | 10         |   640.1 μs |  11.94 μs |  12.26 μs |
| NextormNonCached | 2          |   644.0 μs |  10.27 μs |  11.42 μs |
| NextormPrepared  | 15         |   750.0 μs |   7.26 μs |   8.36 μs |
| Dapper           | 2          |   754.5 μs |  13.65 μs |  14.61 μs |
| Dapper           | 3          |   843.5 μs |  51.63 μs |  57.38 μs |
| NextormCached    | 10         |   846.5 μs |  24.52 μs |  28.24 μs |
| Dapper           | 4          |   861.6 μs |  26.23 μs |  30.21 μs |
| Dapper           | 5          |   917.7 μs |  29.57 μs |  34.05 μs |
| NextormNonCached | 3          |   966.3 μs |  18.72 μs |  19.23 μs |
| NextormCached    | 15         | 1,093.4 μs |  17.65 μs |  19.62 μs |
| Dapper           | 10         | 1,136.5 μs |  26.75 μs |  30.81 μs |
| NextormNonCached | 4          | 1,318.4 μs |  43.23 μs |  46.25 μs |
| Dapper           | 15         | 1,418.6 μs |  66.21 μs |  76.25 μs |
| NextormNonCached | 5          | 1,635.0 μs |  44.47 μs |  51.22 μs |
| NextormNonCached | 10         | 3,248.6 μs | 112.32 μs | 129.35 μs |
| NextormNonCached | 15         | 4,603.3 μs |  61.64 μs |  70.99 μs |
