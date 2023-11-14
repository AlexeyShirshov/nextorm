```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.113
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                 | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------- |----------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| NextormCached          | 11.586 ms | 0.2273 ms | 0.2706 ms |  1.18 |    0.03 | 468.7500 |        - |        - |   2.14 MB |        1.28 |
| NextormCachedToList    | 12.009 ms | 0.2064 ms | 0.1930 ms |  1.22 |    0.02 | 390.6250 | 281.2500 |        - |   2.22 MB |        1.32 |
| Dapper                 | 14.092 ms | 0.1452 ms | 0.1287 ms |  1.43 |    0.02 | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |        1.69 |
| AdoTupleToList         | 11.178 ms | 0.1298 ms | 0.1214 ms |  1.13 |    0.01 | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |        1.60 |
| AdoTupleIteration      |  9.874 ms | 0.0479 ms | 0.0448 ms |  1.00 |    0.00 | 359.3750 |        - |        - |   1.68 MB |        1.00 |
| AdoClassToListWithInit | 10.205 ms | 0.0828 ms | 0.0775 ms |  1.03 |    0.01 | 375.0000 | 296.8750 |        - |   2.21 MB |        1.32 |
