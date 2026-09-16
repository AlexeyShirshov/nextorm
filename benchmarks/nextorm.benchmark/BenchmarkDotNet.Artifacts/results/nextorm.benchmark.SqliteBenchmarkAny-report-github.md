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
| Nextorm_Prepared | 3.150 ms | 0.0423 ms | 0.0396 ms |  27.3438 |       - | 128.91 KB |
| Dapper           | 4.078 ms | 0.0757 ms | 0.0708 ms |  31.2500 |       - |  146.1 KB |
| Nextorm_Cached   | 4.176 ms | 0.0806 ms | 0.0791 ms |  78.1250 |       - | 396.92 KB |
| EFCore_Compiled  | 5.580 ms | 0.0869 ms | 0.0813 ms | 117.1875 | 39.0625 |  546.1 KB |
| EFCore           | 8.324 ms | 0.1629 ms | 0.1876 ms | 187.5000 | 31.2500 | 904.77 KB |
