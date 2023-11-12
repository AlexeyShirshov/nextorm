```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method               | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Allocated  | Alloc Ratio |
|--------------------- |-------------:|-----------:|-----------:|------:|--------:|---------:|-----------:|------------:|
| NextormCompiledAsync | 13,593.10 μs |  86.653 μs |  76.816 μs | 1.000 |    0.00 |  93.7500 |  194.62 KB |        1.00 |
| NextormCompiled      |     16.58 μs |   0.193 μs |   0.171 μs | 0.001 |    0.00 |   8.0261 |   16.41 KB |        0.08 |
| NextormCached        |  1,192.40 μs |  22.036 μs |  45.013 μs | 0.089 |    0.00 | 207.0313 |  425.77 KB |        2.19 |
| EFCore               | 22,327.73 μs | 235.994 μs | 220.749 μs | 1.644 |    0.02 | 500.0000 | 1061.36 KB |        5.45 |
| Dapper               | 13,144.52 μs | 165.212 μs | 128.987 μs | 0.966 |    0.01 |  78.1250 |  188.91 KB |        0.97 |
