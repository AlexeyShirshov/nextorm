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
| Nextorm_Prepared | 3.099 ms | 0.0377 ms | 0.0353 ms |  27.3438 |       - | 128.91 KB |
| Dapper           | 4.124 ms | 0.0643 ms | 0.0602 ms |  31.2500 |       - |  146.1 KB |
| Nextorm_Cached   | 4.219 ms | 0.0724 ms | 0.0677 ms |  78.1250 |       - |  372.7 KB |
| EFCore_Compiled  | 5.529 ms | 0.0893 ms | 0.0792 ms | 117.1875 | 39.0625 |  546.1 KB |
| EFCore           | 8.233 ms | 0.1644 ms | 0.1759 ms | 187.5000 | 31.2500 | 904.77 KB |
