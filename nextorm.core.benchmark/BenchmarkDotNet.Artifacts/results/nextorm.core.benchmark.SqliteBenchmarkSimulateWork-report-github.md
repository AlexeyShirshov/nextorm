```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3636/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                        | Mean      | Median    | Ratio | Gen0      | Allocated | Alloc Ratio |
|------------------------------ |----------:|----------:|------:|----------:|----------:|------------:|
| NextormCompiledToList         | 659.99 ms | 661.31 ms |  1.00 | 5000.0000 |  11.77 MB |        1.00 |
| NextormCachedWithParamsToList | 664.39 ms | 660.14 ms |  1.01 | 5000.0000 |  11.78 MB |        1.00 |
| EFCoreCompiled                |  67.97 ms |  66.22 ms |  0.11 | 5666.6667 |  11.78 MB |        1.00 |
| Dapper                        | 672.13 ms | 669.81 ms |  1.02 | 4000.0000 |   9.59 MB |        0.81 |
