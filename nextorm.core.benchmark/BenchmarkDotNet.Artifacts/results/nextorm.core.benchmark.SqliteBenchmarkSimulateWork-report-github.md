```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Gen0       | Gen1       | Allocated | Alloc Ratio |
|---------------------- |-----------:|---------:|---------:|-----------:|------:|--------:|-----------:|-----------:|----------:|------------:|
| NextormCompiled       |   293.8 ms |  4.60 ms |  3.84 ms |   295.5 ms |  0.45 |    0.01 | 11000.0000 |  3000.0000 |  34.36 MB |        4.73 |
| NextormCompiledFetch  |   936.5 ms | 10.98 ms | 10.27 ms |   935.5 ms |  1.43 |    0.02 | 12000.0000 |  3000.0000 |  34.37 MB |        4.73 |
| NextormCompiledToList |   942.9 ms | 15.58 ms | 19.71 ms |   936.6 ms |  1.45 |    0.03 | 12000.0000 |  3000.0000 |  34.37 MB |        4.73 |
| EFCore                | 3,051.3 ms | 48.28 ms | 94.16 ms | 3,012.2 ms |  4.70 |    0.14 | 39000.0000 | 19000.0000 |  77.88 MB |       10.72 |
| Dapper                |   652.1 ms | 12.53 ms | 12.87 ms |   648.9 ms |  1.00 |    0.00 |  3000.0000 |          - |   7.26 MB |        1.00 |
