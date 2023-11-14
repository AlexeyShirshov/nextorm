```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.113
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| NextormCompiledAsync  |  4.129 ms | 0.0546 ms | 0.0484 ms |  0.99 |    0.02 |  39.0625 |       - |   211.8 KB |        0.94 |
| NextormCompiled       |  4.185 ms | 0.0831 ms | 0.0777 ms |  1.00 |    0.00 |  46.8750 |       - |  225.86 KB |        1.00 |
| NextormCompiledToList |  4.177 ms | 0.0403 ms | 0.0377 ms |  1.00 |    0.02 |  46.8750 |       - |  233.26 KB |        1.03 |
| NextormCached         |  6.128 ms | 0.1218 ms | 0.1626 ms |  1.47 |    0.05 |  78.1250 |  7.8125 |  392.37 KB |        1.74 |
| NextormCachedToList   |  6.070 ms | 0.1204 ms | 0.1182 ms |  1.45 |    0.04 |  85.9375 |  7.8125 |  399.71 KB |        1.77 |
| EFCore                | 10.000 ms | 0.1746 ms | 0.1633 ms |  2.39 |    0.06 | 218.7500 | 46.8750 | 1076.15 KB |        4.76 |
| Dapper                |  4.267 ms | 0.0835 ms | 0.0894 ms |  1.02 |    0.03 |  39.0625 |       - |  188.91 KB |        0.84 |
