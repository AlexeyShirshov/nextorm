```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.114
  [Host]     : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2


```
| Method              | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|-------------------- |----------:|---------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
| NextormCompiled     | 195.64 μs | 3.093 μs | 2.893 μs |  1.00 |    0.00 | 51.0254 |       - | 234.56 KB |        1.00 |
| NextormCached       | 152.49 μs | 2.973 μs | 3.304 μs |  0.78 |    0.02 | 51.5137 |       - | 237.01 KB |        1.01 |
| NextormCachedSync   | 151.70 μs | 3.005 μs | 5.644 μs |  0.75 |    0.02 | 51.5137 |       - | 236.87 KB |        1.01 |
| NextormCachedAsync  | 203.70 μs | 3.939 μs | 7.399 μs |  1.07 |    0.03 | 51.5137 |       - | 236.87 KB |        1.01 |
| NextormCachedToList | 201.01 μs | 3.731 μs | 3.490 μs |  1.03 |    0.03 | 63.4766 | 27.0996 | 315.13 KB |        1.34 |
| Linq                | 122.11 μs | 2.403 μs | 2.360 μs |  0.63 |    0.01 | 51.0254 |       - | 234.45 KB |        1.00 |
| LinqToList          |  98.37 μs | 1.931 μs | 3.063 μs |  0.51 |    0.02 | 61.1572 | 20.5078 | 312.63 KB |        1.33 |
