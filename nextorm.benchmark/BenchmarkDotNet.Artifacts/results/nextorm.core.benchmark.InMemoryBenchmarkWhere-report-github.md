```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|--------------------- |----------:|----------:|----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| NextormCompiledParam |  6.825 ms | 0.0668 ms | 0.0625 ms |  1.00 |    0.00 |        - |       - |   11.72 KB |        1.00 |
| NextormCachedParam   |  7.036 ms | 0.0512 ms | 0.0427 ms |  1.03 |    0.01 |  15.6250 |       - |   78.61 KB |        6.71 |
| NextormCached        | 37.291 ms | 0.6866 ms | 0.6423 ms |  5.46 |    0.11 | 357.1429 | 71.4286 | 1639.35 KB |      139.86 |
| Linq                 |  2.889 ms | 0.0373 ms | 0.0349 ms |  0.42 |    0.01 |        - |       - |   17.28 KB |        1.47 |
