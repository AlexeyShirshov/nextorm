```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method                 | Mean      | Ratio | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------- |----------:|------:|---------:|---------:|---------:|----------:|------------:|
| NextormCompiledToList  | 12.568 ms |     ? | 390.6250 | 281.2500 |        - |   2.21 MB |           ? |
| NextormCachedToList    | 12.263 ms |     ? | 390.6250 | 281.2500 |        - |   2.22 MB |           ? |
| EFCore                 | 18.164 ms |     ? | 750.0000 | 375.0000 | 125.0000 |   4.23 MB |           ? |
| Dapper                 | 14.545 ms |     ? | 468.7500 | 281.2500 | 125.0000 |   2.84 MB |           ? |
| AdoWithDelegate        | 12.811 ms |     ? | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |           ? |
| AdoTupleToList         | 11.716 ms |     ? | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |           ? |
| AdoClassToListWithInit | 11.467 ms |     ? | 390.6250 | 281.2500 |        - |   2.21 MB |           ? |
|                        |           |       |          |          |          |           |             |
| NextormCompiled        | 11.552 ms |  1.10 | 468.7500 |        - |        - |   2.14 MB |        1.27 |
| NextormCached          | 11.591 ms |  1.11 | 468.7500 |        - |        - |   2.14 MB |        1.28 |
| EFCoreStream           | 12.617 ms |  1.20 | 875.0000 |        - |        - |   3.98 MB |        2.37 |
| EFCoreCompiled         | 14.961 ms |  1.42 | 984.3750 |        - |        - |   4.43 MB |        2.64 |
| DapperUnbuffered       | 12.346 ms |  1.17 | 578.1250 |        - |        - |   2.59 MB |        1.55 |
| AdoTupleIteration      | 10.539 ms |  1.00 | 359.3750 |        - |        - |   1.68 MB |        1.00 |
|                        |           |       |          |          |          |           |             |
| NextormCompiledToList  | 10.304 ms |     ? | 390.6250 | 328.1250 |        - |   2.21 MB |           ? |
| NextormCachedToList    | 10.235 ms |     ? | 375.0000 | 312.5000 |        - |   2.22 MB |           ? |
| EFCore                 | 15.193 ms |     ? | 687.5000 | 312.5000 | 156.2500 |   4.23 MB |           ? |
| Dapper                 | 13.090 ms |     ? | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |           ? |
| AdoWithDelegate        | 11.605 ms |     ? | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |           ? |
| AdoTupleToList         |  9.964 ms |     ? | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |           ? |
| AdoClassToListWithInit |  9.793 ms |     ? | 375.0000 | 312.5000 |        - |   2.21 MB |           ? |
|                        |           |       |          |          |          |           |             |
| NextormCompiled        | 10.274 ms |     ? | 468.7500 |        - |        - |   2.14 MB |           ? |
| NextormCached          | 10.218 ms |     ? | 468.7500 |        - |        - |   2.14 MB |           ? |
| EFCoreStream           | 11.757 ms |     ? | 875.0000 |        - |        - |   3.98 MB |           ? |
| EFCoreCompiled         | 12.941 ms |     ? | 984.3750 |        - |        - |   4.43 MB |           ? |
| DapperUnbuffered       | 10.989 ms |     ? | 578.1250 |        - |        - |   2.59 MB |           ? |
| AdoTupleIteration      |  9.280 ms |     ? | 359.3750 |        - |        - |   1.68 MB |           ? |
