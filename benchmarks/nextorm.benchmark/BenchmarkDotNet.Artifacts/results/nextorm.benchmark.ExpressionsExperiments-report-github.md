```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host] : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Toolchain=InProcessNoEmitToolchain  

```
| Method                    | Mean     | Error    | StdDev   | Gen0   | Allocated |
|-------------------------- |---------:|---------:|---------:|-------:|----------:|
| ExpressionVisitor         | 42.13 ns | 0.337 ns | 0.315 ns | 0.0051 |      24 B |
| ReadonlyExpressionVisitor | 43.51 ns | 0.233 ns | 0.182 ns | 0.0051 |      24 B |
