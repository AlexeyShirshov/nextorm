```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.114
  [Host]     : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2


```
| Method                 | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------- |---------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| NextormCompiled        | 11.56 ms | 0.224 ms | 0.209 ms |  1.11 |    0.02 | 468.7500 |        - |        - |   2.14 MB |        1.27 |
| NextormCompiledToList  | 11.94 ms | 0.095 ms | 0.089 ms |  1.15 |    0.02 | 390.6250 | 281.2500 |        - |   2.21 MB |        1.32 |
| NextormCached          | 11.73 ms | 0.163 ms | 0.153 ms |  1.13 |    0.03 | 468.7500 |        - |        - |   2.14 MB |        1.28 |
| NextormCachedToList    | 12.07 ms | 0.185 ms | 0.164 ms |  1.16 |    0.02 | 390.6250 | 281.2500 |        - |   2.22 MB |        1.32 |
| EFCore                 | 17.69 ms | 0.342 ms | 0.407 ms |  1.71 |    0.05 | 750.0000 | 375.0000 | 125.0000 |   4.23 MB |        2.52 |
| EFCoreStream           | 12.31 ms | 0.205 ms | 0.192 ms |  1.18 |    0.03 | 875.0000 |        - |        - |   3.98 MB |        2.37 |
| EFCoreCompiled         | 15.08 ms | 0.218 ms | 0.203 ms |  1.45 |    0.03 | 984.3750 |        - |        - |   4.43 MB |        2.64 |
| Dapper                 | 14.50 ms | 0.288 ms | 0.283 ms |  1.39 |    0.04 | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |        1.70 |
| DapperUnbuffered       | 12.44 ms | 0.238 ms | 0.283 ms |  1.20 |    0.03 | 578.1250 |        - |        - |   2.59 MB |        1.55 |
| AdoWithDelegate        | 12.36 ms | 0.240 ms | 0.257 ms |  1.19 |    0.02 | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |        1.42 |
| AdoTupleToList         | 11.17 ms | 0.133 ms | 0.125 ms |  1.07 |    0.02 | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |        1.60 |
| AdoTupleIteration      | 10.40 ms | 0.161 ms | 0.151 ms |  1.00 |    0.00 | 359.3750 |        - |        - |   1.68 MB |        1.00 |
| AdoClassToListWithInit | 10.67 ms | 0.151 ms | 0.141 ms |  1.03 |    0.02 | 390.6250 | 296.8750 |        - |   2.21 MB |        1.32 |
