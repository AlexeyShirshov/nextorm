```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-CLZGZP : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=20  

```
| Method          | Iterations | Mean       | Error    | StdDev   |
|---------------- |----------- |-----------:|---------:|---------:|
| Dapper          | 1          |   251.9 μs |  4.55 μs |  5.24 μs |
| NextormCached   | 1          |   319.1 μs |  7.78 μs |  8.64 μs |
| NextormPrepared | 1          |   324.9 μs |  7.04 μs |  8.11 μs |
| NextormPrepared | 2          |   358.3 μs |  8.17 μs |  8.39 μs |
| NextormCached   | 2          |   388.7 μs | 10.73 μs | 12.35 μs |
| NextormPrepared | 3          |   398.9 μs | 10.95 μs | 12.61 μs |
| NextormPrepared | 4          |   434.2 μs | 11.95 μs | 13.76 μs |
| NextormCached   | 3          |   452.7 μs | 12.29 μs | 14.15 μs |
| NextormPrepared | 5          |   496.5 μs | 34.99 μs | 40.30 μs |
| NextormCached   | 4          |   515.8 μs | 18.52 μs | 20.59 μs |
| NextormCached   | 5          |   565.0 μs | 21.65 μs | 24.06 μs |
| NextormPrepared | 10         |   633.2 μs | 17.98 μs | 20.71 μs |
| Dapper          | 2          |   738.1 μs | 15.68 μs | 17.43 μs |
| NextormPrepared | 15         |   793.5 μs | 25.52 μs | 28.37 μs |
| Dapper          | 3          |   806.3 μs | 13.95 μs | 14.33 μs |
| NextormCached   | 10         |   849.2 μs | 25.00 μs | 28.79 μs |
| Dapper          | 4          |   851.6 μs | 23.45 μs | 26.06 μs |
| NextormPrepared | 20         |   940.5 μs | 28.28 μs | 31.44 μs |
| Dapper          | 5          |   945.8 μs | 36.74 μs | 42.31 μs |
| NextormCached   | 15         | 1,150.4 μs | 40.45 μs | 44.96 μs |
| Dapper          | 10         | 1,155.2 μs | 27.60 μs | 31.78 μs |
| NextormCached   | 20         | 1,407.7 μs | 48.26 μs | 55.58 μs |
| Dapper          | 15         | 1,418.2 μs | 33.96 μs | 39.11 μs |
| Dapper          | 20         | 1,664.7 μs | 45.32 μs | 52.19 μs |
