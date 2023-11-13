```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.113
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method        | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|-------------- |----------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| NextormCached | 11.567 ms | 0.1854 ms | 0.1734 ms |  1.18 |    0.02 | 468.7500 |        - |        - |   2.14 MB |        1.28 |
| Dapper        | 13.959 ms | 0.0781 ms | 0.0731 ms |  1.42 |    0.01 | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |        1.69 |
| AdoTupleIter  |  9.844 ms | 0.0586 ms | 0.0548 ms |  1.00 |    0.00 | 359.3750 |        - |        - |   1.68 MB |        1.00 |
