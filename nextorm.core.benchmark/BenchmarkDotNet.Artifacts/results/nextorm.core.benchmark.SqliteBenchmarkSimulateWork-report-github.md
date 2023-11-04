```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method              | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0      | Gen1      | Allocated | Alloc Ratio |
|-------------------- |---------:|--------:|--------:|------:|--------:|----------:|----------:|----------:|------------:|
| NextormCached       | 226.5 ms | 3.52 ms | 3.29 ms |  1.00 |    0.00 | 4500.0000 | 4000.0000 |   9.27 MB |        1.00 |
| NextormFetch        | 216.3 ms | 4.09 ms | 4.20 ms |  0.96 |    0.02 | 3500.0000 | 3000.0000 |   9.26 MB |        1.00 |
| NextormToListCached | 214.4 ms | 2.73 ms | 2.42 ms |  0.95 |    0.02 | 3000.0000 | 1500.0000 |   9.14 MB |        0.99 |
| EFCore              | 215.2 ms | 2.80 ms | 2.48 ms |  0.95 |    0.02 | 4000.0000 |  500.0000 |   9.35 MB |        1.01 |
| Dapper              | 131.9 ms | 1.61 ms | 1.43 ms |  0.58 |    0.01 |  750.0000 |         - |   1.62 MB |        0.17 |
