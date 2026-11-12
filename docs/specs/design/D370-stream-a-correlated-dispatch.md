# D370 — correlated subqueries under NativeAOT (new defect, S8 finding)

Status: product defect discovered during S8; **not fixed in S8**. Input to the AC1 residual
justification and to a later Stream A step (reflection-free builder dispatch or the generator).

## How it was found

S8 added two NativeAOT harness scenarios that exercise correlated subqueries over a captured context:

```csharp
// correlated-any
context.From<SimpleEntity>()
    .Where(row => context.From<IComplexEntity>().Where(inner => inner.Int == row.Id).Any())
    .OrderBy(row => row.Id).Select(row => row.Id).ToList();

// correlated-count
context.From<SimpleEntity>()
    .Select(row => new { row.Id, N = context.From<IComplexEntity>().Where(inner => inner.Int == row.Id).Count() })
    .ToList();
```

The JIT control passes both (`correlated-any` → `1`; `correlated-count` → `1:2;2:0;…;10:0`). The
**published native binary fails both**.

## Measured failures

| Scenario | Native exception | Frame |
|---|---|---|
| `correlated-any` | `InvalidOperationException: No coercion operator is defined between types 'NextORM.Core.EntityBuilder\`1[IComplexEntity]' and 'NextORM.Core.QueryCommand'` | `System.Linq.Expressions.Expression.GetUserDefinedCoercionOrThrow` → `NextORM.Core.CorrelatedQueryExpressionVisitor.VisitMethodCall` |
| `correlated-count` | `NotSupportedException: Cannot find the 'Select' projection method on 'EntityBuilder\`1'` | `NextORM.Core.AggregateTerminalRewriter.SelectMethodFor` → `AggregateTerminalRewriter.Rewrite` → `CorrelatedQueryExpressionVisitor.ReplaceAggregateTerminal` |

Evidence: `.pdca/370/rv1/attempts/s8-aot-native-run-correlated-fail.log` (native, with stacks) and
`s8-aot-harness-correlated-fail.log` (JIT control, both passing). Both scenarios were removed from the S8
candidate; this record is the deliverable for the finding.

## Assessment

- `correlated-count`: `AggregateTerminalRewriter.SelectMethodFor` resolves
  `EntityBuilder<T>.Select<TResult>` through `builderType.GetMethods(Public|Instance)` on a **run-time
  closed** builder type. The trimmer cannot see that reflection, so the methods are removed and the
  lookup returns no match. This is the same class as the S3/S5/S7 work but the type is not
  statically known here (the receiver is a runtime-closed `EntityBuilder<T>`), so no compile-time
  delegate or cached `MethodInfo` on a literal type is available without a non-generic projection entry
  point or generation.
- `correlated-any`: the failure is a coercion of an `EntityBuilder<T>` expression to `QueryCommand` inside
  `VisitMethodCall`. The JIT and native runs take different branches here; the exact mechanism is not yet
  established (candidate: method-identity/overload resolution for the extension `Any` under trimming, or
  a branch guarded by a reflection-derived decision). Treat the root cause as **unverified** until a
  dedicated investigation.
- Both are **native-only**: the managed query pipeline and SQL generation are correct, and the failure is
  a preparation-time exception before any SQL is sent.

## Disposition

Not fixed in S8 (the step's bounded scope was two `Expression.Lambda` sites). The two scenarios are
removed from the harness so the S8 increment stays honest; the defect is recorded here, referenced from
the campaign status, and belongs to the Stream A/generator work that replaces reflection over
runtime-closed builder types with a static projection path.
