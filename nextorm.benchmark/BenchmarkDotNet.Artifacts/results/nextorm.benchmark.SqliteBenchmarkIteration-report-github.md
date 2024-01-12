```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|---------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormCompiledToList | 32.68 μs | 0.463 μs | 0.433 μs | 0.3052 |      - |   1.66 KB |
| NextormCompiledStream | 33.24 μs | 0.349 μs | 0.310 μs | 0.3052 |      - |   1.48 KB |
| NextormCachedToList   | 35.71 μs | 0.437 μs | 0.388 μs | 0.6104 |      - |   2.91 KB |
| DapperUnbuffered      | 44.68 μs | 0.608 μs | 0.569 μs | 0.3662 |      - |    1.8 KB |
| Dapper                | 47.17 μs | 0.529 μs | 0.495 μs | 0.3662 |      - |   1.88 KB |
| EFCoreCompiled        | 57.19 μs | 0.867 μs | 0.769 μs | 1.5259 | 0.4883 |   7.19 KB |
