```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method           | Mean     | Error     | StdDev    | Median   | Gen0     | Gen1    | Allocated |
|----------------- |---------:|----------:|----------:|---------:|---------:|--------:|----------:|
| Nextorm_Prepared | 3.134 ms | 0.0312 ms | 0.0292 ms | 3.130 ms |  27.3438 |       - | 128.91 KB |
| Dapper           | 4.170 ms | 0.0832 ms | 0.1661 ms | 4.096 ms |  31.2500 |       - |  146.1 KB |
| Nextorm_Cached   | 4.220 ms | 0.0747 ms | 0.0699 ms | 4.217 ms |  85.9375 |       - | 423.54 KB |
| EFCore_Compiled  | 5.583 ms | 0.0722 ms | 0.0675 ms | 5.590 ms | 117.1875 | 39.0625 |  546.1 KB |
| EFCore           | 8.324 ms | 0.1586 ms | 0.1484 ms | 8.297 ms | 187.5000 | 31.2500 | 904.77 KB |
