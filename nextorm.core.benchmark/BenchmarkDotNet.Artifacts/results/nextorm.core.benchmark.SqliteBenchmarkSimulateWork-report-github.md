```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.113
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


```
| Method                        | Mean     | Error   | StdDev  | Gen0      | Gen1      | Allocated |
|------------------------------ |---------:|--------:|--------:|----------:|----------:|----------:|
| NextormCompiledToList         | 214.6 ms | 4.19 ms | 5.45 ms | 2500.0000 |         - |  12.03 MB |
| NextormCachedToList           | 406.9 ms | 7.78 ms | 8.96 ms | 7000.0000 | 2000.0000 |   35.9 MB |
| NextormCachedWithParamsToList | 313.6 ms | 6.17 ms | 8.02 ms | 3000.0000 | 1000.0000 |  19.62 MB |
| Dapper                        | 236.2 ms | 4.61 ms | 6.00 ms | 2333.3333 |         - |  11.48 MB |
