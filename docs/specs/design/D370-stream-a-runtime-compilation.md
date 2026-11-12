# D370 S4 — runtime expression-tree compilation (defect class 3)

Status: analysis and disposition for the S4 increment. No product change yet.

## Symptom

The 2026-11-12 AOT probes report, for five queries, a failure at query preparation:

```
NotSupportedException: 'System.Linq.Expressions.Expression`1[System.Func`2[...]]'
is missing native code or metadata
```

- PostgreSQL `RollingRevenueMetrics`;
- SQL Server `RollingKpi`;
- ClickHouse `Funnel`, `Sessions`, `SessionDepth`.

The query is never sent; the failure is in building a compiled delegate for a shape the pipeline
composes at run time (a windowed/rolling projection over a generated alias type).

## Where it comes from

The canonical instrument attributes the same capability to the `IL3050` `Expression.Lambda` group:

| Site | Rows |
|---|---:|
| `Visitors/Core/CorrelatedQueryExpressionVisitor.cs` (`Expression.Lambda` in the rewrite/correlation paths) | 16 |
| `Visitors/Aggregates/AggregateTerminalRewriter.cs` | 8 |
| `Builders/Entity/MetadataEagerLoadBinding.cs:1633` (`MakeGenericMethod` on the junction loader) | 2 |
| `Expressions/SelectExpression.cs` (removed in S3 for the reader accessors) | 0 remaining |

`Expression.Lambda<TDelegate>(...)` then `Compile()` needs the CLR to produce native code for the
closed delegate type. NativeAOT supports the interpreter path for expression trees, but a generic
instantiation that the compiler cannot see (the delegate type is built from run-time types) has no
metadata/native code, which is exactly the probe message.

## Options

1. **Compile-time generation (source generator).** Emit the materializer/projection delegates for the
   shapes the projection declares. Removes the run-time `Compile` for those shapes and satisfies AC2
   at the same time. Cost: a real generator in `src/nextorm.core.sourcegenerator` (currently an empty
   stub), a discovery/registration contract, and an opt-in path for dynamically composed queries.
   This is the only option that removes the row class rather than relocating it.
2. **Precompiled delegate table.** Cache the `Func<...>` instantiations for the closed shapes the
   library itself composes (window/rolling aggregations). Bounded, but does not cover user-defined
   projections and leaves the interpreter in the path.
3. **Fail closed with a clear preparation error.** Detect that the shape needs run-time compilation in
   a native image and throw a `QueryPreparationException` naming the query and the required shape.
   Honest, cheap, and turns an opaque `NotSupportedException` into an actionable one, but does not
   make the feature work.

## Recommendation

Option 1 is the campaign path (AC2 needs the same machinery), with option 3 as the interim behaviour
for shapes the generator does not cover. Option 2 is not a substitute.

The increment is deliberately not attempted in S4: it changes the query pipeline's composition model
and needs the approved registration/discovery design that the issue requires **before**
implementation (AC4/AC2). The next boundary should decide between (a) the generator spike and (b) the
fail-closed interim, and must not mix it with a diagnostic-row reduction step.
