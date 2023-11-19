```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method                        | Job      | Runtime  | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1       | Allocated | Alloc Ratio |
|------------------------------ |--------- |--------- |------------:|----------:|----------:|------:|--------:|-----------:|-----------:|----------:|------------:|
| NextormCompiledToList         | .NET 7.0 | .NET 7.0 |   211.95 ms |  4.016 ms |  3.756 ms |  1.00 |    0.00 |  2333.3333 |          - |  11.65 MB |        1.00 |
| NextormCachedToList           | .NET 7.0 | .NET 7.0 |   340.87 ms |  4.936 ms |  4.618 ms |  1.61 |    0.03 |  7000.0000 |          - |  34.13 MB |        2.93 |
| NextormCachedWithParamsToList | .NET 7.0 | .NET 7.0 |   212.47 ms |  2.281 ms |  2.022 ms |  1.00 |    0.02 |  2333.3333 |          - |  11.67 MB |        1.00 |
| EFCore                        | .NET 7.0 | .NET 7.0 | 1,807.78 ms | 32.635 ms | 43.567 ms |  8.54 |    0.30 | 18000.0000 | 17000.0000 |  80.92 MB |        6.94 |
| EFCoreStream                  | .NET 7.0 | .NET 7.0 | 1,440.21 ms | 24.314 ms | 22.743 ms |  6.80 |    0.19 | 14000.0000 | 13000.0000 |  65.18 MB |        5.59 |
| EFCoreCompiled                | .NET 7.0 | .NET 7.0 |    62.01 ms |  1.124 ms |  1.717 ms |  0.29 |    0.01 |  2666.6667 |          - |  12.16 MB |        1.04 |
| Dapper                        | .NET 7.0 | .NET 7.0 |   240.81 ms |  4.733 ms |  6.154 ms |  1.14 |    0.03 |  2333.3333 |          - |  11.48 MB |        0.99 |
| NextormCompiledToList         | .NET 8.0 | .NET 8.0 |   208.26 ms |  4.161 ms |  7.287 ms |  0.98 |    0.03 |  2500.0000 |          - |  11.65 MB |        1.00 |
| NextormCachedToList           | .NET 8.0 | .NET 8.0 |   316.19 ms |  3.978 ms |  3.527 ms |  1.49 |    0.03 |  7000.0000 |          - |  33.45 MB |        2.87 |
| NextormCachedWithParamsToList | .NET 8.0 | .NET 8.0 |   208.26 ms |  3.808 ms |  5.700 ms |  0.98 |    0.04 |  2500.0000 |          - |  11.67 MB |        1.00 |
| EFCore                        | .NET 8.0 | .NET 8.0 | 1,593.50 ms | 30.690 ms | 44.985 ms |  7.47 |    0.28 | 18000.0000 | 17000.0000 |   81.8 MB |        7.02 |
| EFCoreStream                  | .NET 8.0 | .NET 8.0 | 1,251.84 ms | 23.057 ms | 22.645 ms |  5.91 |    0.16 | 14000.0000 | 13000.0000 |  65.53 MB |        5.62 |
| EFCoreCompiled                | .NET 8.0 | .NET 8.0 |    46.71 ms |  0.920 ms |  1.432 ms |  0.22 |    0.01 |  2636.3636 |          - |  12.16 MB |        1.04 |
| Dapper                        | .NET 8.0 | .NET 8.0 |   217.50 ms |  4.293 ms |  4.015 ms |  1.03 |    0.03 |  2500.0000 |          - |  11.48 MB |        0.99 |
