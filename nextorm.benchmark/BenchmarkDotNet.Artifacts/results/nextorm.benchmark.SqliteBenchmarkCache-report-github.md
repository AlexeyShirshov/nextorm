```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-STRUFF : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=20  

```
| Method           | Iterations | Mean       | Error     | StdDev    |
|----------------- |----------- |-----------:|----------:|----------:|
| Dapper           | 1          |   246.2 μs |   7.14 μs |   8.22 μs |
| NextormNonCached | 1          |   307.6 μs |   8.55 μs |   9.50 μs |
| NextormCached    | 1          |   316.6 μs |  12.75 μs |  14.68 μs |
| NextormPrepared  | 1          |   319.5 μs |   7.84 μs |   9.03 μs |
| NextormPrepared  | 2          |   358.1 μs |   8.92 μs |   9.91 μs |
| NextormCached    | 2          |   382.7 μs |  12.56 μs |  13.44 μs |
| NextormPrepared  | 3          |   392.1 μs |   9.44 μs |  10.88 μs |
| NextormPrepared  | 4          |   426.0 μs |  10.40 μs |  11.97 μs |
| NextormCached    | 3          |   451.1 μs |  14.46 μs |  15.48 μs |
| NextormPrepared  | 5          |   455.1 μs |  13.45 μs |  14.94 μs |
| NextormCached    | 4          |   512.3 μs |  13.92 μs |  16.03 μs |
| NextormCached    | 5          |   563.0 μs |  12.56 μs |  14.47 μs |
| NextormPrepared  | 10         |   629.6 μs |  13.08 μs |  15.06 μs |
| NextormNonCached | 2          |   631.6 μs |  17.06 μs |  18.96 μs |
| Dapper           | 2          |   730.8 μs |  17.68 μs |  19.66 μs |
| NextormPrepared  | 15         |   783.9 μs |  18.39 μs |  21.18 μs |
| Dapper           | 3          |   799.0 μs |  18.81 μs |  21.67 μs |
| Dapper           | 4          |   851.1 μs |  24.57 μs |  26.29 μs |
| NextormCached    | 10         |   863.7 μs |  27.15 μs |  31.26 μs |
| Dapper           | 5          |   888.8 μs |  24.21 μs |  27.88 μs |
| NextormNonCached | 3          |   956.1 μs |  16.72 μs |  19.26 μs |
| NextormCached    | 15         | 1,134.4 μs |  28.32 μs |  32.62 μs |
| Dapper           | 10         | 1,174.3 μs |  32.12 μs |  36.98 μs |
| NextormNonCached | 4          | 1,278.1 μs |  47.49 μs |  50.81 μs |
| Dapper           | 15         | 1,396.2 μs |  39.54 μs |  45.54 μs |
| NextormNonCached | 5          | 1,599.2 μs |  53.09 μs |  61.14 μs |
| NextormNonCached | 10         | 3,180.3 μs |  95.77 μs | 110.29 μs |
| NextormNonCached | 15         | 4,826.1 μs | 149.01 μs | 171.60 μs |
