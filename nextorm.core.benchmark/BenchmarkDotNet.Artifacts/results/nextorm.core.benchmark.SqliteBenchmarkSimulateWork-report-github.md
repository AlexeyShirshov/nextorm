```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.113
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0       | Gen1       | Allocated | Alloc Ratio |
|---------------------- |-----------:|---------:|---------:|------:|--------:|-----------:|-----------:|----------:|------------:|
| NextormCompiledAsync  |   205.5 ms |  3.18 ms |  2.97 ms |  1.03 |    0.02 |  7000.0000 |  2000.0000 |  35.96 MB |        0.96 |
| NextormCompiled       |   200.5 ms |  1.56 ms |  1.39 ms |  1.00 |    0.00 |  7333.3333 |  1666.6667 |  37.29 MB |        1.00 |
| NextormCompiledFetch  |   394.5 ms |  6.28 ms |  5.88 ms |  1.97 |    0.03 |  7000.0000 |  2000.0000 |  36.77 MB |        0.99 |
| NextormCompiledToList |   391.1 ms |  3.73 ms |  3.11 ms |  1.95 |    0.02 |  7000.0000 |  2000.0000 |  36.77 MB |        0.99 |
| NextormCached         |   195.7 ms |  0.99 ms |  0.83 ms |  0.98 |    0.01 |  7333.3333 |  2000.0000 |  36.65 MB |        0.98 |
| NextormCachedFetch    |   404.9 ms |  7.98 ms | 10.37 ms |  2.04 |    0.05 |  7000.0000 |  2000.0000 |  36.67 MB |        0.98 |
| NextormCachedToList   |   389.4 ms |  3.71 ms |  3.47 ms |  1.94 |    0.02 |  7000.0000 |  2000.0000 |  36.67 MB |        0.98 |
| EFCore                | 1,691.7 ms | 10.17 ms |  9.02 ms |  8.44 |    0.07 | 18000.0000 | 17000.0000 |  80.92 MB |        2.17 |
| Dapper                |   207.3 ms |  1.56 ms |  1.38 ms |  1.03 |    0.01 |  2000.0000 |          - |    9.2 MB |        0.25 |
