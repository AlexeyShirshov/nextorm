```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.113
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                 | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------- |---------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| NextormCompiledToList  | 11.93 ms | 0.167 ms | 0.156 ms |  1.18 |    0.02 | 390.6250 | 281.2500 |        - |   2.21 MB |        1.32 |
| NextormCached          | 11.37 ms | 0.138 ms | 0.129 ms |  1.12 |    0.02 | 468.7500 |        - |        - |   2.14 MB |        1.28 |
| NextormCachedToList    | 12.21 ms | 0.112 ms | 0.104 ms |  1.21 |    0.02 | 390.6250 | 281.2500 |        - |   2.22 MB |        1.32 |
| Dapper                 | 14.24 ms | 0.164 ms | 0.154 ms |  1.41 |    0.02 | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |        1.69 |
| AdoWithDelegate        | 11.88 ms | 0.193 ms | 0.181 ms |  1.17 |    0.03 | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |        1.42 |
| AdoTupleToList         | 11.10 ms | 0.144 ms | 0.135 ms |  1.10 |    0.03 | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |        1.60 |
| AdoTupleIteration      | 10.13 ms | 0.161 ms | 0.151 ms |  1.00 |    0.00 | 359.3750 |        - |        - |   1.68 MB |        1.00 |
| AdoClassToListWithInit | 11.68 ms | 0.177 ms | 0.166 ms |  1.15 |    0.03 | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |        1.42 |
