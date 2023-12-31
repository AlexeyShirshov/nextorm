```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean     | Gen0     | Gen1     | Gen2     | Allocated |
|------------------------------ |---------:|---------:|---------:|---------:|----------:|
| NextormCompiled               | 10.14 ms | 468.7500 |        - |        - |   2.14 MB |
| NextormCachedSelectList       | 10.16 ms | 468.7500 |        - |        - |   2.14 MB |
| NextormCached                 | 10.20 ms | 468.7500 |        - |        - |   2.14 MB |
| AdoTupleToList                | 10.21 ms | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |
| NextormCachedToList           | 10.50 ms | 390.6250 | 296.8750 |        - |   2.22 MB |
| NextormCompiledToList         | 10.57 ms | 390.6250 | 281.2500 |        - |   2.21 MB |
| NextormCachedSelectListToList | 10.71 ms | 390.6250 | 296.8750 |        - |   2.22 MB |
| DapperUnbuffered              | 11.22 ms | 578.1250 |        - |        - |   2.59 MB |
| EFCoreStream                  | 11.56 ms | 875.0000 |        - |        - |   3.98 MB |
| AdoWithDelegate               | 12.52 ms | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |
| EFCoreCompiled                | 12.85 ms | 984.3750 |        - |        - |   4.43 MB |
| Dapper                        | 13.74 ms | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |
| EFCore                        | 15.90 ms | 656.2500 | 312.5000 | 125.0000 |   4.23 MB |
