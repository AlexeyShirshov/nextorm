```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2


```
| Method         | Mean     | Error     | StdDev    | Allocated |
|--------------- |---------:|----------:|----------:|----------:|
| SystemHashCode | 7.153 μs | 0.1061 μs | 0.0993 μs |         - |
| CustomHashCode | 6.992 μs | 0.0400 μs | 0.0355 μs |         - |
