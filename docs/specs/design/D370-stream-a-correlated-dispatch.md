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

## S10 progress: two of the reflection sites are gone, the marker remains

S10 removed two of the four runtime-reflection sites on the correlated path and recorded the third as a
design blocker.

### Fixed

- **Builder → command coercion.** `CorrelatedQueryExpressionVisitor` built
  `Expression.Convert(builder, typeof(QueryCommand))`, which makes `Expression` resolve the builder's
  implicit conversion operator through reflection; a trimmed image drops the operator and the conversion
  throws `No coercion operator is defined`. The visitor now casts to `IQueryCommandSource` (an internal
  interface the builder implements explicitly) and reads its `Command` property — a built-in reference
  conversion plus a statically known property getter, producing the same command.
- **Aggregate projection dispatch.** `AggregateTerminalRewriter.SelectMethodFor` reflected
  `EntityBuilder<T>.Select<TResult>` over the runtime-closed builder type and closed it with
  `MakeGenericMethod`, and the caller then reflected `First` on the resulting `QueryCommand<TResult>`.
  The rewriter now calls `IAggregateProjectionHost.SelectAggregateProjection(LambdaExpression, Type)`,
  which the builder implements by building an untyped `QueryCommand` with the aggregate result type as
  its runtime `ResultType`; the caller applies the Limit-1 shape directly. No closed generic is created.

Registry at the unchanged 12-scenario harness: **205 unique / 211 raw → 201 unique / 207 raw** (−4/−4).
The four removed rows are `.GetMethod("First")` (`IL2075`, `CorrelatedQueryExpressionVisitor`),
`SelectMethodFor` (`IL2070`, `AggregateTerminalRewriter`), and the two diagnostics of its
`MakeGenericMethod` site (`IL2060` and `IL3050`, old `AggregateTerminalRewriter.cs:86`); every other
add/remove pair in the registry delta is a line shift.

### Remaining blocker: the outer-reference marker

Both correlated scenarios still fail natively at the same place:

```text
Instance property 'Ref' is not defined for type 'NextORM.Core.OuterRefMarker`1[System.Int32]'
  at System.Linq.Expressions.Expression.Property(Expression, String)
  at NextORM.Core.ReplaceConstantsExpressionVisitor.VisitMember(MemberExpression)
```

`ReplaceConstantsExpressionVisitor` represents an outer reference as
`OuterRefMarker<T>(idx).Ref`, building the member access with
`Expression.Property(Expression.New(GetOuterRefMarkerCI(type), idx), "Ref")`. For a type closed at run
time, the `Ref` getter is not present in the native image, and the string overload resolves it with
`Type.GetProperty`, which returns `null`.

Constraints that rule out the obvious shortcuts:

- `OuterRefMarker<T>.Ref.get` is part of the shipped public API, so it cannot become a field;
- the issue forbids rooting, `DynamicDependency` and `rd.xml`;
- `[DynamicallyAccessedMembers]` cannot flow through `typeof(OuterRefMarker<>).MakeGenericType(type)`;
- the marker shape (`new OuterRefMarker<T>(idx).Ref`, followed by the projected member) is what the SQL
  translator, the in-memory correlated plan and the APPLY detectors recognise.

The available fixes are therefore a design change, not a local edit:

1. **Non-generic marker + recogniser update.** Emit an internal non-generic carrier holding the index and
   the key type, and update the four recogniser sites (`MemberTranslator` twice,
   `InMemoryCorrelatedPlan.TryGetMarker`, the two marker detectors) to unwrap it. This keeps the public
   API but touches every correlated translation path.
2. **Compile-time marker factory.** Move the reflection into generic code so the property is statically
   referenced for each instantiation, and invoke the factory through a runtime generic creation — which
   re-introduces the runtime-generic dependency this campaign is removing.

Either is a Stream A decision; S10 does not take it. The two correlated scenarios stay out of the native
harness until it is resolved, and this record is the AC1 input for that decision. The measured failures are
preserved: `.pdca/370/rv1/attempts/s10-correlated-fail-any.log` (both scenarios) and
`s10-correlated-fail-count.log` (`correlated-count` alone, full stack through `ReplaceConstantsExpressionVisitor`).
