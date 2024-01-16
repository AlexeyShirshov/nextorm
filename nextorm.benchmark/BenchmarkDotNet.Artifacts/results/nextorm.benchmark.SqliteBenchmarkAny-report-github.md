```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method          | Mean     | Error     | StdDev    | Gen0     | Gen1    | Allocated |
|---------------- |---------:|----------:|----------:|---------:|--------:|----------:|
| NextormCompiled | 3.118 ms | 0.0509 ms | 0.0476 ms |  27.3438 |       - | 128.91 KB |
| Dapper          | 4.047 ms | 0.0798 ms | 0.0980 ms |  31.2500 |       - |  146.1 KB |
| NextormCached   | 4.260 ms | 0.0645 ms | 0.0603 ms |  93.7500 |       - | 464.17 KB |
| EFCoreCompiled  | 5.540 ms | 0.0781 ms | 0.0730 ms | 117.1875 | 39.0625 |  546.1 KB |
| EFCore          | 8.347 ms | 0.1607 ms | 0.1578 ms | 187.5000 | 31.2500 | 904.77 KB |
