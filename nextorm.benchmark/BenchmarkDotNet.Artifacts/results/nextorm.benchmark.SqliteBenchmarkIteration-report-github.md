```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                       | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|----------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormCompiledStream        | 32.69 μs | 0.531 μs | 0.497 μs | 0.3052 |      - |   1.48 KB |
| NextormCompiledToList        | 33.03 μs | 0.499 μs | 0.443 μs | 0.3052 |      - |   1.66 KB |
| NextormCompiledManualToList  | 33.61 μs | 0.670 μs | 0.745 μs | 0.3052 |      - |   1.66 KB |
| NextormCachedToList          | 35.00 μs | 0.460 μs | 0.408 μs | 0.6104 |      - |   2.91 KB |
| NextormManualSQLCachedToList | 35.01 μs | 0.423 μs | 0.396 μs | 0.6104 |      - |   2.91 KB |
| Dapper                       | 42.42 μs | 0.650 μs | 0.608 μs | 0.3662 |      - |   1.88 KB |
| DapperUnbuffered             | 47.64 μs | 0.759 μs | 0.710 μs | 0.3662 |      - |    1.8 KB |
| EFCoreCompiled               | 57.62 μs | 0.893 μs | 0.835 μs | 1.5259 | 0.4883 |   7.19 KB |
