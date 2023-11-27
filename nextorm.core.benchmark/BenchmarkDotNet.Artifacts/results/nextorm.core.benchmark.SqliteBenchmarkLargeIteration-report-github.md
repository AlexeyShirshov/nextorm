```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method                 | Job      | Categories | Mean      | Ratio | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------- |--------- |----------- |----------:|------:|---------:|---------:|---------:|----------:|------------:|
| NextormCompiledToList  | .NET 7.0 | Buffered   | 12.183 ms |  1.09 | 390.6250 | 296.8750 |        - |   2.21 MB |        1.00 |
| NextormCachedToList    | .NET 7.0 | Buffered   | 12.585 ms |  1.13 | 390.6250 | 265.6250 |        - |   2.22 MB |        1.00 |
| EFCore                 | .NET 7.0 | Buffered   | 18.296 ms |  1.64 | 750.0000 | 375.0000 | 125.0000 |   4.23 MB |        1.91 |
| Dapper                 | .NET 7.0 | Buffered   | 14.658 ms |  1.31 | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |        1.29 |
| AdoWithDelegate        | .NET 7.0 | Buffered   | 13.131 ms |  1.17 | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |        1.08 |
| AdoTupleToList         | .NET 7.0 | Buffered   | 11.404 ms |  1.02 | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |        1.21 |
| AdoClassToListWithInit | .NET 7.0 | Buffered   | 11.173 ms |  1.00 | 375.0000 | 296.8750 |        - |   2.21 MB |        1.00 |
|                        |          |            |           |       |          |          |          |           |             |
| NextormCompiled        | .NET 7.0 | Stream     | 12.449 ms |  1.16 | 468.7500 |        - |        - |   2.14 MB |        1.27 |
| NextormCached          | .NET 7.0 | Stream     | 13.413 ms |  1.25 | 468.7500 |        - |        - |   2.14 MB |        1.28 |
| EFCoreStream           | .NET 7.0 | Stream     | 13.334 ms |  1.24 | 875.0000 |        - |        - |   3.98 MB |        2.37 |
| EFCoreCompiled         | .NET 7.0 | Stream     | 15.524 ms |  1.45 | 968.7500 |        - |        - |   4.43 MB |        2.64 |
| DapperUnbuffered       | .NET 7.0 | Stream     | 13.016 ms |  1.21 | 578.1250 |        - |        - |   2.59 MB |        1.55 |
| AdoTupleIteration      | .NET 7.0 | Stream     | 10.724 ms |  1.00 | 359.3750 |        - |        - |   1.68 MB |        1.00 |
|                        |          |            |           |       |          |          |          |           |             |
| NextormCompiledToList  | .NET 8.0 | Buffered   | 10.207 ms |     ? | 375.0000 | 328.1250 |        - |   2.21 MB |           ? |
| NextormCachedToList    | .NET 8.0 | Buffered   | 10.699 ms |     ? | 390.6250 | 281.2500 |        - |   2.22 MB |           ? |
| EFCore                 | .NET 8.0 | Buffered   | 16.819 ms |     ? | 687.5000 | 343.7500 | 156.2500 |   4.23 MB |           ? |
| Dapper                 | .NET 8.0 | Buffered   | 14.340 ms |     ? | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |           ? |
| AdoWithDelegate        | .NET 8.0 | Buffered   | 12.677 ms |     ? | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |           ? |
| AdoTupleToList         | .NET 8.0 | Buffered   | 10.468 ms |     ? | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |           ? |
| AdoClassToListWithInit | .NET 8.0 | Buffered   | 10.287 ms |     ? | 375.0000 | 312.5000 |        - |   2.21 MB |           ? |
|                        |          |            |           |       |          |          |          |           |             |
| NextormCompiled        | .NET 8.0 | Stream     | 10.407 ms |     ? | 468.7500 |        - |        - |   2.14 MB |           ? |
| NextormCached          | .NET 8.0 | Stream     | 10.367 ms |     ? | 468.7500 |        - |        - |   2.14 MB |           ? |
| EFCoreStream           | .NET 8.0 | Stream     | 11.869 ms |     ? | 875.0000 |        - |        - |   3.98 MB |           ? |
| EFCoreCompiled         | .NET 8.0 | Stream     | 13.514 ms |     ? | 984.3750 |        - |        - |   4.43 MB |           ? |
| DapperUnbuffered       | .NET 8.0 | Stream     | 11.015 ms |     ? | 578.1250 |        - |        - |   2.59 MB |           ? |
| AdoTupleIteration      | .NET 8.0 | Stream     |  9.530 ms |     ? | 359.3750 |        - |        - |   1.68 MB |           ? |
