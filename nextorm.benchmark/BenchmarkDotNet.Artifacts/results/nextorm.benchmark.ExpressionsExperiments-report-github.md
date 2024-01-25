```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2


```
| Method         | Mean      | Error     | StdDev    | Allocated |
|--------------- |----------:|----------:|----------:|----------:|
| Explicit       | 17.448 ns | 0.2307 ns | 0.2045 ns |         - |
| Boxing         | 18.392 ns | 0.1798 ns | 0.1594 ns |         - |
| PatternMathing |  2.352 ns | 0.0670 ns | 0.0627 ns |         - |
