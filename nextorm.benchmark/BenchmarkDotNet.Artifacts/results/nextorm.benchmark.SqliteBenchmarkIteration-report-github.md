```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|---------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormCompiledToList | 41.05 μs | 0.387 μs | 0.302 μs | 0.4883 |      - |   2.39 KB |
| NextormCompiledStream | 41.38 μs | 0.797 μs | 0.853 μs | 0.4272 |      - |   2.12 KB |
| Dapper                | 42.83 μs | 0.844 μs | 1.155 μs | 0.3662 |      - |   1.88 KB |
| NextormCachedToList   | 45.65 μs | 0.640 μs | 0.599 μs | 0.8545 |      - |   4.08 KB |
| DapperUnbuffered      | 49.22 μs | 0.818 μs | 1.200 μs | 0.3662 |      - |    1.8 KB |
| EFCoreCompiled        | 57.95 μs | 1.054 μs | 0.880 μs | 1.5259 | 0.4883 |   7.19 KB |
| EFCore                | 63.49 μs | 0.948 μs | 0.792 μs | 1.8311 | 0.3662 |   8.63 KB |
