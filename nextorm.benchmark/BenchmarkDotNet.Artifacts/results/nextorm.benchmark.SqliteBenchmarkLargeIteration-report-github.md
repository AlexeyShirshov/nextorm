```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean      | Gen0     | Gen1     | Gen2     | Allocated |
|---------------------- |----------:|---------:|---------:|---------:|----------:|
| NextormCompiled       |  9.884 ms | 468.7500 |        - |        - |   2.14 MB |
| NextormCached         |  9.985 ms | 468.7500 |        - |        - |   2.14 MB |
| NextormCompiledToList | 10.232 ms | 390.6250 | 328.1250 |        - |   2.21 MB |
| NextormCachedToList   | 10.405 ms | 390.6250 | 296.8750 |        - |   2.22 MB |
| AdoTupleToList        | 10.421 ms | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |
| DapperUnbuffered      | 11.077 ms | 578.1250 |        - |        - |   2.59 MB |
| EFCoreStream          | 11.342 ms | 875.0000 |        - |        - |   3.98 MB |
| AdoWithDelegate       | 11.873 ms | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |
| EFCoreCompiled        | 12.841 ms | 984.3750 |        - |        - |   4.43 MB |
| Dapper                | 13.600 ms | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |
| EFCore                | 15.062 ms | 687.5000 | 312.5000 | 156.2500 |   4.23 MB |
