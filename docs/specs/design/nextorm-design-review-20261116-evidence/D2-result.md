# D2 implementation result

## Scope and extraction

Implemented only D2 in `ShardQuery.cs`, the shared internal helper, and a new unit-test file under
`tests/nextorm.core.tests/Sharding/FanOut/`. The concrete consumers are `ShardQuery<TEntity>` and
`ShardQuery<TEntity, TResult>`.

`ShardQueryExecution` now owns the common `Scoped`, `RunEach`, `RunEachAsync`,
`DegreeOfParallelism`, and `BucketsByShard` policy. Both facades pass their source and callbacks
directly; no composed `ShardQuery` execution object, interface, public API, package, scheduler
change, or allocation optimization was introduced.

The implementation retains per-call grouping, `StringComparer.Ordinal`, bucket positions and
result-vector ordering, the `Math.Clamp` defaults, `Parallel.ForEach` options, one `Task.Run` per
group with the original cancellation token, same-owner sequential buckets, `WaitAsync` before the
`try`, release only after gate acquisition, and `Task.WhenAll` exception/await semantics.
Projected selector use and streaming bucket iteration order remain at their prior facade boundaries.

The new tests exercise the shared executor directly with an in-memory `EntityBuilder` mapping-variant
seam, plus synchronous, asynchronous, projected, unprojected, and streaming facade smoke tests on
`InMemoryDataContext` (no database). They cover vector positions, same-owner sequencing, Ordinal
owner identity, bounded concurrency and clamp defaults, empty input, precanceled and queued
cancellation, token forwarding, sync aggregate versus async original exceptions, gate cleanup, and
waiting for other shard work. Gates use completion sources with safety timeouts, not sleeps.

## Commands and evidence

Commands below are reported as argument arrays; all runs were in DO. Logs are full raw output under
this directory.

| Command | Exit | Result | Full log |
|---|---:|---|---|
| `["dotnet","build","src/nextorm.core/nextorm.core.csproj","-c","Debug"]` | 0 | 0 warnings, 0 errors; initial extracted implementation | `D2-core-build.log` |
| `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--filter","FullyQualifiedName~ShardQueryExecutionTests"]` | 1 | Build stopped before test execution: 8 xUnit1051 analyzer errors for timeout awaits missing the test cancellation token | `D2-focused-tests.log` |
| `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--filter","FullyQualifiedName~ShardQueryExecutionTests"]` | 0 | 14 passed, 0 failed, 0 skipped; before adding the Ordinal-owner case | `D2-focused-tests-retry.log` |
| `["dotnet","build","src/nextorm.core/nextorm.core.csproj","-c","Debug"]` | 0 | Final product-code build: 0 warnings, 0 errors | `D2-core-build-final.log` |
| `["dotnet","test","tests/nextorm.core.tests","-c","Debug"]` | 0 | 4,325 passed, 0 failed, 0 skipped; before adding the Ordinal-owner case | `D2-core-tests.log` |
| `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--filter","FullyQualifiedName~ShardQueryExecutionTests"]` | 0 | Final focused run: 15 passed, 0 failed, 0 skipped | `D2-focused-tests-final.log` |
| `["dotnet","test","tests/nextorm.core.tests","-c","Debug"]` | 0 | Final full core unit run: 4,326 passed, 0 failed, 0 skipped | `D2-core-tests-final.log` |

The analyzer failure was fixed by passing `TestContext.Current.CancellationToken` to timeout waits.
No integration, benchmark, DocFX, or solution-wide build/test was run. The solution-build issue
documented by D1 remains outside this step and is not claimed as resolved.

## Async scheduling regression coverage

Added `ShardQueryAsyncExecutionTests.cs` without changing product code or existing tests. The new
tests suspend actual `RunEachAsync` callbacks on completion sources: one verifies owner-a's bucket
sequence while its first bucket overlaps a gated owner-b callback; the other gates five distinct
owner groups and observes the configured two-callback maximum. Both tests release every callback
gate in `finally`, use safety timeouts with `TestContext.Current.CancellationToken`, and make no
assumptions about `Task.Run` group scheduling order.

| Command | Exit | Result | Full log |
|---|---:|---|---|
| `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--filter","FullyQualifiedName~NextORM.Core.Tests.Sharding.FanOut"]` | 0 | Both test classes: 17 passed, 0 failed, 0 skipped | `D2-async-focused.log` |
| `["dotnet","test","tests/nextorm.core.tests","-c","Debug"]` | 0 | Full core unit suite: 4,328 passed, 0 failed, 0 skipped | `D2-async-core-tests.log` |

No build, solution, integration, benchmark, or DocFX run was performed for this follow-up.
