```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.114
  [Host]     : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2


```
| Method                        | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1       | Allocated | Alloc Ratio |
|------------------------------ |------------:|----------:|----------:|------:|--------:|-----------:|-----------:|----------:|------------:|
| NextormCompiledToList         |   209.12 ms |  2.311 ms |  2.049 ms |  1.00 |    0.00 |  2333.3333 |          - |  11.65 MB |        1.00 |
| NextormCachedToList           |   402.24 ms |  7.953 ms | 11.657 ms |  1.92 |    0.05 |  7000.0000 |  2000.0000 |  35.48 MB |        3.05 |
| NextormCachedWithParamsToList |   216.80 ms |  4.251 ms |  5.527 ms |  1.05 |    0.03 |  2333.3333 |          - |  11.67 MB |        1.00 |
| EFCore                        | 1,771.40 ms | 34.995 ms | 47.902 ms |  8.61 |    0.24 | 18000.0000 | 17000.0000 |  81.61 MB |        7.00 |
| EFCoreStream                  | 1,398.55 ms |  9.425 ms |  8.816 ms |  6.69 |    0.07 | 14000.0000 | 13000.0000 |  66.48 MB |        5.70 |
| EFCoreCompiled                |    64.29 ms |  1.183 ms |  1.107 ms |  0.31 |    0.01 |  2625.0000 |          - |  12.16 MB |        1.04 |
| Dapper                        |   235.60 ms |  2.576 ms |  2.409 ms |  1.13 |    0.02 |  2333.3333 |          - |  11.48 MB |        0.99 |
