```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                 | Categories | Mean      | Ratio | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------- |----------- |----------:|------:|---------:|---------:|---------:|----------:|------------:|
| NextormCompiledToList  | Buffered   | 10.284 ms |  1.01 | 375.0000 | 328.1250 |        - |   2.21 MB |        1.00 |
| NextormCachedToList    | Buffered   | 10.691 ms |  1.05 | 390.6250 | 265.6250 |        - |   2.22 MB |        1.00 |
| EFCore                 | Buffered   | 15.505 ms |  1.52 | 718.7500 | 343.7500 | 125.0000 |   4.23 MB |        1.91 |
| Dapper                 | Buffered   | 12.791 ms |  1.25 | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |        1.29 |
| AdoWithDelegate        | Buffered   | 12.013 ms |  1.18 | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |        1.08 |
| AdoTupleToList         | Buffered   | 10.272 ms |  1.02 | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |        1.21 |
| AdoClassToListWithInit | Buffered   | 10.214 ms |  1.00 | 375.0000 | 312.5000 |        - |   2.21 MB |        1.00 |
|                        |            |           |       |          |          |          |           |             |
| NextormCompiled        | Stream     | 10.554 ms |  1.16 | 468.7500 |        - |        - |   2.14 MB |        1.27 |
| NextormCached          | Stream     | 10.259 ms |  1.17 | 468.7500 |        - |        - |   2.14 MB |        1.28 |
| EFCoreStream           | Stream     | 11.255 ms |  1.26 | 875.0000 |        - |        - |   3.98 MB |        2.37 |
| EFCoreCompiled         | Stream     | 13.096 ms |  1.45 | 984.3750 |        - |        - |   4.43 MB |        2.64 |
| DapperUnbuffered       | Stream     | 10.837 ms |  1.19 | 578.1250 |        - |        - |   2.59 MB |        1.55 |
| AdoTupleIteration      | Stream     |  9.090 ms |  1.00 | 359.3750 |        - |        - |   1.68 MB |        1.00 |
