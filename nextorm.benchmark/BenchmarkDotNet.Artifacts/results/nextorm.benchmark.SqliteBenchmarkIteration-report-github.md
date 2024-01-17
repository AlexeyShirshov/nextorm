```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                         | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormCompiledManualSqlToList | 33.14 μs | 0.542 μs | 0.507 μs | 0.3052 |      - |   1.66 KB |
| NextormCompiledToList          | 33.24 μs | 0.530 μs | 0.495 μs | 0.3052 |      - |   1.66 KB |
| NextormCompiledStream          | 33.76 μs | 0.654 μs | 0.895 μs | 0.3052 |      - |   1.48 KB |
| NextormCachedToList            | 35.58 μs | 0.558 μs | 0.522 μs | 0.6104 |      - |   2.89 KB |
| NextormCachedManualSqlToList   | 35.62 μs | 0.620 μs | 0.580 μs | 0.6104 |      - |   3.01 KB |
| Dapper                         | 41.96 μs | 0.353 μs | 0.295 μs | 0.3662 |      - |   1.88 KB |
| DapperUnbuffered               | 42.81 μs | 0.846 μs | 0.831 μs | 0.3662 |      - |    1.8 KB |
| EFCoreCompiled                 | 58.04 μs | 1.054 μs | 1.171 μs | 1.5259 | 0.4883 |   7.19 KB |
