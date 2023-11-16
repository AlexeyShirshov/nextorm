```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.114
  [Host]     : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2


```
| Method               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|--------------------- |----------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| NextormCompiledParam |  9.493 ms | 0.0805 ms | 0.0753 ms |  1.00 |    0.00 |        - |        - |   26.62 KB |        1.00 |
| NextormCachedParam   | 10.852 ms | 0.1473 ms | 0.1378 ms |  1.14 |    0.01 |        - |        - |   52.83 KB |        1.98 |
| NextormCached        | 22.827 ms | 0.4325 ms | 0.6474 ms |  2.44 |    0.07 | 375.0000 | 343.7500 | 1808.78 KB |       67.95 |
| Linq                 |  4.876 ms | 0.0405 ms | 0.0338 ms |  0.51 |    0.01 |        - |        - |   17.29 KB |        0.65 |
