```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0        | Gen1      | Allocated | Alloc Ratio |
|---------------------- |-----------:|----------:|----------:|------:|--------:|------------:|----------:|----------:|------------:|
| NextormCompiled       | 1,161.9 ms | 120.05 ms | 353.96 ms |  5.05 |    0.37 | 117000.0000 | 3000.0000 | 248.45 MB |      162.49 |
| NextormCompiledFetch  | 1,251.1 ms | 128.15 ms | 377.86 ms |  5.42 |    0.39 | 109000.0000 | 3000.0000 | 231.36 MB |      151.31 |
| NextormCompiledToList | 1,218.6 ms | 124.35 ms | 366.65 ms |  5.30 |    0.41 | 109000.0000 | 3000.0000 | 229.47 MB |      150.08 |
| EFCore                |   593.6 ms |   9.11 ms |   8.52 ms |  4.54 |    0.08 |   7000.0000 | 3000.0000 |  15.59 MB |       10.20 |
| Dapper                |   130.7 ms |   1.25 ms |   1.17 ms |  1.00 |    0.00 |    750.0000 |         - |   1.53 MB |        1.00 |
