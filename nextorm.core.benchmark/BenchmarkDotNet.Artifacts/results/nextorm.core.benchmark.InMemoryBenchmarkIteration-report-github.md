```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method                    | Job      | Runtime  | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|-------------------------- |--------- |--------- |----------:|---------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
| NextormCompiled           | .NET 7.0 | .NET 7.0 | 189.27 μs | 1.378 μs | 1.289 μs |  1.25 |    0.01 | 50.7813 |       - | 234.41 KB |        0.99 |
| NextormCompiledSync       | .NET 7.0 | .NET 7.0 | 149.58 μs | 1.069 μs | 1.000 μs |  0.99 |    0.01 | 50.7813 |       - | 234.41 KB |        0.99 |
| NextormCompiledSyncToList | .NET 7.0 | .NET 7.0 | 208.85 μs | 1.740 μs | 1.627 μs |  1.38 |    0.01 | 61.2793 | 20.5078 | 312.59 KB |        1.32 |
| NextormCached             | .NET 7.0 | .NET 7.0 | 156.96 μs | 0.493 μs | 0.385 μs |  1.04 |    0.01 | 51.2695 |       - | 236.62 KB |        1.00 |
| NextormCachedSync         | .NET 7.0 | .NET 7.0 | 151.18 μs | 0.736 μs | 0.615 μs |  1.00 |    0.00 | 51.2695 |       - | 236.48 KB |        1.00 |
| Linq                      | .NET 7.0 | .NET 7.0 | 115.03 μs | 1.144 μs | 0.955 μs |  0.76 |    0.01 | 51.0254 |       - | 234.45 KB |        0.99 |
| LinqToList                | .NET 7.0 | .NET 7.0 |  90.76 μs | 0.493 μs | 0.412 μs |  0.60 |    0.00 | 61.1572 | 20.5078 | 312.63 KB |        1.32 |
| NextormCompiled           | .NET 8.0 | .NET 8.0 | 148.58 μs | 2.488 μs | 2.078 μs |  0.98 |    0.01 | 50.7813 |       - | 234.41 KB |        0.99 |
| NextormCompiledSync       | .NET 8.0 | .NET 8.0 |  92.35 μs | 1.017 μs | 0.849 μs |  0.61 |    0.01 | 50.9033 |       - | 234.41 KB |        0.99 |
| NextormCompiledSyncToList | .NET 8.0 | .NET 8.0 | 163.43 μs | 2.894 μs | 2.707 μs |  1.08 |    0.02 | 61.2793 | 20.5078 | 312.59 KB |        1.32 |
| NextormCached             | .NET 8.0 | .NET 8.0 |  97.52 μs | 1.702 μs | 3.515 μs |  0.66 |    0.02 | 51.2695 |       - | 236.62 KB |        1.00 |
| NextormCachedSync         | .NET 8.0 | .NET 8.0 |  97.85 μs | 1.729 μs | 1.618 μs |  0.65 |    0.01 | 51.2695 |       - | 236.48 KB |        1.00 |
| Linq                      | .NET 8.0 | .NET 8.0 |  64.51 μs | 1.276 μs | 2.364 μs |  0.43 |    0.01 | 51.0254 |       - | 234.45 KB |        0.99 |
| LinqToList                | .NET 8.0 | .NET 8.0 |  70.99 μs | 1.213 μs | 1.348 μs |  0.47 |    0.01 | 61.2793 | 20.3857 | 312.63 KB |        1.32 |
