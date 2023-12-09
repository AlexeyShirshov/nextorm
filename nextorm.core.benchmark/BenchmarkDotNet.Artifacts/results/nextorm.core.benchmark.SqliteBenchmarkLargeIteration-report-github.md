```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3636/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                 | Categories | Mean     | Ratio | Gen0      | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------- |----------- |---------:|------:|----------:|---------:|---------:|----------:|------------:|
| NextormCompiledToList  | Buffered   | 17.89 ms |  1.14 |  406.2500 | 312.5000 |        - |   2.21 MB |        1.00 |
| NextormCachedToList    | Buffered   | 17.43 ms |  1.12 |  437.5000 | 312.5000 |        - |   2.22 MB |        1.00 |
| EFCore                 | Buffered   | 23.74 ms |  1.53 |  718.7500 | 375.0000 | 187.5000 |   4.23 MB |        1.91 |
| Dapper                 | Buffered   | 19.88 ms |  1.27 |  468.7500 | 281.2500 | 125.0000 |   2.84 MB |        1.29 |
| AdoWithDelegate        | Buffered   | 19.14 ms |  1.22 |  375.0000 | 218.7500 |  93.7500 |   2.39 MB |        1.08 |
| AdoTupleToList         | Buffered   | 15.62 ms |  1.00 |  437.5000 | 281.2500 | 281.2500 |   2.68 MB |        1.21 |
| AdoClassToListWithInit | Buffered   | 15.63 ms |  1.00 |  421.8750 | 328.1250 |        - |   2.21 MB |        1.00 |
|                        |            |          |       |           |          |          |           |             |
| NextormCompiled        | Stream     | 16.60 ms |  1.14 | 1062.5000 |        - |        - |   2.14 MB |        1.27 |
| NextormCached          | Stream     | 16.44 ms |  1.12 | 1062.5000 |        - |        - |   2.14 MB |        1.28 |
| EFCoreStream           | Stream     | 18.45 ms |  1.25 | 1937.5000 |        - |        - |   3.98 MB |        2.37 |
| EFCoreCompiled         | Stream     | 20.46 ms |  1.39 | 2218.7500 |        - |        - |   4.43 MB |        2.64 |
| DapperUnbuffered       | Stream     | 17.40 ms |  1.18 | 1281.2500 |        - |        - |   2.59 MB |        1.55 |
| AdoTupleIteration      | Stream     | 14.72 ms |  1.00 |  828.1250 |        - |        - |   1.68 MB |        1.00 |
