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
| NextormCompiledStream | 33.16 μs | 0.637 μs | 0.596 μs | 0.3052 |      - |   1.46 KB |
| NextormCompiledToList | 33.43 μs | 0.502 μs | 0.469 μs | 0.3662 |      - |   1.73 KB |
| NextormCachedToList   | 36.97 μs | 0.631 μs | 0.590 μs | 0.7324 |      - |   3.56 KB |
| Dapper                | 44.63 μs | 0.795 μs | 0.705 μs | 0.3662 |      - |   1.88 KB |
| DapperUnbuffered      | 44.66 μs | 0.783 μs | 0.732 μs | 0.3662 |      - |    1.8 KB |
| EFCoreCompiled        | 57.49 μs | 0.672 μs | 0.628 μs | 1.5259 | 0.4883 |   7.19 KB |
| EFCore                | 62.31 μs | 1.221 μs | 1.082 μs | 1.8311 | 0.3662 |   8.63 KB |
