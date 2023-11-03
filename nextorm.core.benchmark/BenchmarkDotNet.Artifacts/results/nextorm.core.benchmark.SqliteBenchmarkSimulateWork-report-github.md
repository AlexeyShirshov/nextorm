```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method              | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0      | Gen1     | Allocated | Alloc Ratio |
|-------------------- |---------:|---------:|---------:|------:|--------:|----------:|---------:|----------:|------------:|
| NextormCached       | 659.1 ms | 32.50 ms | 95.32 ms |  1.00 |    0.00 | 2000.0000 |        - |  15.25 MB |        1.00 |
| NextormFetch        | 664.2 ms | 31.55 ms | 92.52 ms |  1.01 |    0.07 | 2000.0000 |        - |  15.27 MB |        1.00 |
| NextormToListCached | 664.6 ms | 31.43 ms | 92.67 ms |  1.01 |    0.08 | 2000.0000 |        - |  15.27 MB |        1.00 |
| EFCore              | 220.3 ms |  1.11 ms |  0.98 ms |  0.42 |    0.01 | 4000.0000 | 500.0000 |   9.35 MB |        0.61 |
| Dapper              | 134.2 ms |  1.20 ms |  1.06 ms |  0.25 |    0.01 |  750.0000 |        - |   1.62 MB |        0.11 |
