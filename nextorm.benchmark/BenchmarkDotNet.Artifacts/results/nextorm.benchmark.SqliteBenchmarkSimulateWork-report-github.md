```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean       | Ratio | Gen0      | Allocated | Alloc Ratio |
|------------------------------ |-----------:|------:|----------:|----------:|------------:|
| NextormCompiled               |   6.441 ms |  0.04 | 1382.8125 |   6.21 MB |        1.00 |
| DapperUnbuffered              |  30.582 ms |  0.19 | 3125.0000 |  14.08 MB |        2.27 |
| NextormCached                 |  57.052 ms |  0.36 | 7000.0000 |  31.51 MB |        5.07 |
| NextormCompiledToList         | 159.353 ms |  1.00 | 1250.0000 |   6.21 MB |        1.00 |
| NextormCachedWithParamsToList | 160.275 ms |  1.01 | 1250.0000 |   6.23 MB |        1.00 |
| Dapper                        | 215.981 ms |  1.36 | 3000.0000 |  14.09 MB |        2.27 |
| NextormCachedToList           | 246.113 ms |  1.54 | 6000.0000 |  31.17 MB |        5.02 |
| EFCoreCompiled                | 248.290 ms |  1.56 | 4000.0000 |  20.41 MB |        3.29 |
