```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 7.0.403
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method            | Mean     | Error    | StdDev    | Gen0   | Gen1   | Allocated |
|------------------ |---------:|---------:|----------:|-------:|-------:|----------:|
| NextormQueryCache | 755.7 μs | 59.09 μs | 174.23 μs | 1.4648 | 0.4883 |  10.91 KB |
| EFCore            | 203.2 μs |  1.27 μs |   0.99 μs | 5.1270 |      - |  10.49 KB |
