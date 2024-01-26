```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method           | Mean     | Error     | StdDev    | Gen0     | Gen1    | Allocated |
|----------------- |---------:|----------:|----------:|---------:|--------:|----------:|
| Nextorm_Prepared | 3.126 ms | 0.0320 ms | 0.0300 ms |  27.3438 |       - | 128.91 KB |
| Nextorm_Cached   | 4.124 ms | 0.0528 ms | 0.0468 ms |  78.1250 |       - | 378.17 KB |
| Dapper           | 4.601 ms | 0.0821 ms | 0.0768 ms |  31.2500 |       - |  146.1 KB |
| EFCore_Compiled  | 5.496 ms | 0.0820 ms | 0.0767 ms | 117.1875 | 39.0625 |  546.1 KB |
| EFCore           | 8.119 ms | 0.1343 ms | 0.1256 ms | 187.5000 | 31.2500 | 904.78 KB |
