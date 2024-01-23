```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method               | Iterations | Mean       | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|--------------------- |----------- |-----------:|----------:|----------:|-------:|-------:|----------:|
| DontCacheExpressions | 1          |   7.439 μs | 0.1445 μs | 0.2073 μs | 1.2207 |      - |   5.81 KB |
| DontCacheExpressions | 2          |  14.842 μs | 0.2931 μs | 0.5055 μs | 2.4414 |      - |  11.59 KB |
| DontCacheExpressions | 3          |  22.016 μs | 0.4121 μs | 0.4232 μs | 3.6621 |      - |  17.37 KB |
| DontCacheExpressions | 5          |  36.127 μs | 0.7055 μs | 0.8399 μs | 6.1035 |      - |  28.93 KB |
| CacheExpressions     | 1          |  96.232 μs | 1.9218 μs | 2.2132 μs | 2.1973 | 1.9531 |  10.92 KB |
| CacheExpressions     | 2          | 106.085 μs | 2.0952 μs | 2.7243 μs | 3.4180 | 3.1738 |   16.7 KB |
| CacheExpressions     | 3          | 113.380 μs | 2.0344 μs | 1.9029 μs | 4.8828 | 0.4883 |  22.48 KB |
| CacheExpressions     | 5          | 131.104 μs | 2.3987 μs | 2.2438 μs | 7.3242 | 0.4883 |  34.04 KB |
