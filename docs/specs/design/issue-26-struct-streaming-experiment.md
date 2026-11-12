# Issue 26: struct streaming diagnostic experiment

Issue: [#26](https://github.com/AlexeyShirshov/nextorm/issues/26), milestone 1.0.10-a.

This is an isolated diagnostic prototype, not an approved API or evidence that struct projection is faster in production. It compares limited class/struct projections and a custom matched streaming materializer; the latter uses 32/128/256-byte payloads, 1,000 rows, and class wrapper, value return, and `Fill(ref T)` paths. It is not the production materializer.

## Prerequisites and commands

Use the pinned .NET SDK from `global.json` (11.0.100, latest feature roll-forward). From repository root, build the benchmark project, then run these narrow Release jobs:

```sh
dotnet build benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Release
dotnet run --project benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Release -- --filter '*Issue26StructProjectionBenchmark*'
dotnet run --project benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Release -- --filter '*SqliteBenchmarkStreamProjection*'
dotnet run --project benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Release -- --filter '*Issue26StreamingMaterializerBenchmark*'
```

BenchmarkDotNet 0.15.8 out-of-process validation can fail for these cases; the Issue26 materializer and SQLite streaming benchmarks explicitly use the current `InProcessEmitToolchain`. The focused projection benchmark uses `[InProcess]`. These configurations are experiment-specific; do not infer a general benchmark policy from them.

Artifacts are emitted under the benchmark project's `BenchmarkDotNet.Artifacts/` directory (and diagnostic test output is in the test runner log). Preserve full logs and artifacts when reporting results.

## SQLite integration diagnostics

The live tests are in `tests/nextorm.integration.tests/Sqlite/StructProjectionLiveTests.cs`. Select only the diagnostic allocation test, for example:

```sh
dotnet test tests/nextorm.integration.tests/nextorm.integration.tests.csproj -c Debug --filter 'FullyQualifiedName~Measure_ConstructorProjectionAllocations'
```

The mutable-struct member-init projection is intentionally expected to fail on the current implementation with `Cannot get ctor`; it is a recorded diagnostic, not a green acceptance test. Select the passing constructor-projection coverage independently when desired. Do not claim all struct projections work based on the diagnostic-only selection.

For meaningful performance comparisons, use an idle, stable machine and repeat runs. Treat results as exploratory; do not assert a proven speedup from a single run or extrapolate the prototype's matched materializer to production behavior.
