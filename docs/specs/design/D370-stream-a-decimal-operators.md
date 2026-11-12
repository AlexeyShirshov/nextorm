# D370 S4 — decimal operators in the binary-node rebuild

Status: implemented by the S4 increment of the campaign for GitHub issue #370. Scope:
`src/nextorm.core/Visitors/NumericOperators.cs` (new), `src/nextorm.core/Visitors/TypeFacts.cs`,
`tests/nextorm.aot.runner/Program.cs`, `tests/nextorm.integration.tests/Aot/AotNativeRunTests.cs`.

## Problem

The 2026-11-12 AOT probe reports on #370 recorded five PostgreSQL, eight SQL Server and nine
ClickHouse native failures. Ten of them are the same defect:

```
InvalidOperationException: The binary operator Divide/Multiply/LessThanOrEqual/GreaterThan
is not defined for the types 'System.Decimal' and 'System.Decimal'
(or 'System.Nullable`1[System.Decimal]')
```

The pipeline rebuilds binary expressions from operands whose types are decided at run time — the
correlated-query rewriter and the aggregate terminals call `Expression.MakeBinary(nodeType, left,
right)`. For a `decimal` operand that call resolves the operator by reflecting over the operand type.
The library never names the operators itself (the operands are expression-tree nodes, not values), so
the trimmer removes them, the reflection lookup finds nothing, and query preparation fails before any
SQL is sent. JIT keeps them and never fails.

Reproduced deterministically in the canonical harness without a database: a native run of
`tests/nextorm.aot.runner` reported
`decimalOperatorResolution=missing; The binary operator Divide is not defined for the types
'System.Nullable`1[System.Decimal]' and 'System.Nullable`1[System.Decimal]'`.

## Decision

Declare the trimming requirement the rebuild actually has, instead of leaving it implicit:

```csharp
internal static class NumericOperators
{
    internal static void Preserve() => _ = PublicMethods(typeof(decimal));

    private static MethodInfo[] PublicMethods(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
        => type.GetMethods();
}
```

called from `TypeFacts`'s static initializer, which every query preparation reaches.

`DynamicallyAccessedMembers` is the trimming **contract** mechanism: it states which members the code
requires so the trimmer keeps them. It silences no diagnostic, roots no assembly or member wholesale,
adds no `NoWarn`/`#pragma`/`rd.xml`/`DynamicDependency`, and changes nothing for the consumer. The
literal `typeof(decimal)` at the call site is what makes the requirement statically analysable.

## Alternatives tested and rejected

- **Call the operators from reachable library code** (`_ = a / b;` over runtime-derived decimals).
  Refuted empirically: the calls are removed as dead computations (the results are unobservable), and
  even a static-field sink did not keep the operators in the reflection map — the native probe still
  reported `missing`.
- **Build the node with an explicit operator captured from a compiler-emitted tree.** Refuted: the
  compiler-emitted decimal tree itself throws in the trimmed image, because the compiler emits
  `Expression.Divide(a, b)` (runtime lookup) rather than an operator token for `decimal`.
- **Any rooting/suppression shortcut.** Forbidden by the issue's constraints and unnecessary: the
  contract above is a statement of a real requirement, and it is verified by the probe.

## Verification

- Native harness scenario `aot-numeric-operator` now reports
  `decimalOperatorResolution=available;divide=1.5` on a clean forced-ILC publish; it failed before the
  change and is now a regression gate (`AotNativeRunTests` asserts the scenario passes and the JIT
  oracle matches).
- AOT diagnostics are unchanged: 212 unique / 218 raw, with zero per-file/per-code deltas against the
  accepted S3 registry.
- Build 0 warnings / 0 errors; core suites green; full regression evidence recorded in the campaign
  status.

## Scope boundary

Defect class 2 (`System.Tuple<T1,T2>` constructor resolution in `RawMapperFactory`) and class 3
(runtime `Expression.Lambda` compilation) are **not** fixed here. The tuple path needs a trimming
contract on a public type-carrying member — a public-API change under AC4 — or compile-time
generation; class 3 needs generated projections. Both are recorded for the next boundary.
