```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean     | Error    | StdDev   | Gen0     | Gen1     | Allocated  |
|-------------- |---------:|---------:|---------:|---------:|---------:|-----------:|
| Nextorm       | 62.53 ms | 1.119 ms | 1.046 ms | 750.0000 | 625.0000 | 1721.76 KB |
| NextormCached | 38.38 ms | 0.464 ms | 0.387 ms | 428.5714 | 357.1429 |  971.66 KB |
