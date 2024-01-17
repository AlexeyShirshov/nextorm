```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean      | Median    | Gen0     | Gen1     | Gen2     | Allocated |
|---------------------- |----------:|----------:|---------:|---------:|---------:|----------:|
| NextormCachedStream   |  9.680 ms |  9.655 ms | 468.7500 |        - |        - |   2.14 MB |
| NextormCompiledStream |  9.787 ms |  9.815 ms | 468.7500 |        - |        - |   2.14 MB |
| NextormCompiledToList |  9.945 ms |  9.934 ms | 390.6250 | 296.8750 |        - |   2.21 MB |
| NextormCachedToList   | 10.059 ms | 10.117 ms | 390.6250 | 250.0000 |        - |   2.22 MB |
| AdoTupleToList        | 10.142 ms | 10.223 ms | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |
| EFCoreCompiled        | 10.821 ms | 10.721 ms | 875.0000 |        - |        - |   3.97 MB |
| DapperUnbuffered      | 10.873 ms | 10.913 ms | 578.1250 |        - |        - |   2.59 MB |
| EFCoreStream          | 11.352 ms | 11.318 ms | 875.0000 |        - |        - |   3.98 MB |
| AdoWithDelegate       | 11.859 ms | 11.867 ms | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |
| Dapper                | 13.182 ms | 13.145 ms | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |
| EFCore                | 15.598 ms | 15.352 ms | 687.5000 | 343.7500 | 156.2500 |   4.23 MB |
