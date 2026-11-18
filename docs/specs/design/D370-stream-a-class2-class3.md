# D370 S5 — constructor and runtime-compilation defect classes

Status: implemented in S5. Covers the two classes S4 deferred: class 2 (`System.Tuple` / `PivotValues`
constructors) and class 3 (runtime `Expression<...>` compilation). The 2026-11-12 AOT probes are the
instrument: PostgreSQL 7/10, SQL Server 7/11, ClickHouse 0/9 before this step.

## Class 3 — key lambdas closed at run time (`SelectGroupComponent`)

### Symptom

Five probe queries failed at preparation with
`NotSupportedException: 'System.Linq.Expressions.Expression``1[System.Func``2[TEntity, TKey]]' is missing native code or metadata`.
The native stack (S5 attempt, live PostgreSQL container) pinned it to one line:

```text
Expression.CreateLambda -> RuntimeTypeInfo.get_TypeHandle
NextORM.Extensions.SelectGroupComponent`1.Prepare
```

### Root cause

`SelectGroupComponent<TEntity>.Prepare` wrapped each inferred grouping-key body in the **non-generic**
`Expression.Lambda(body, projection.Parameters)` overload. That overload infers the delegate type from the
body and parameter types **at run time**, then closes `Expression<Func<TEntity, TKey>>` — a generic
instantiation the NativeAOT compiler cannot see, so the closed type has no metadata.

### Fix

Close the delegate type at compile time with the component's own `TEntity`:

```csharp
Expression.Lambda<Func<TEntity, object?>>(Expression.Convert(k, typeof(object)), projection.Parameters)
```

The box-convert is the same shape window order keys already use (`Expression<Func<object?>>`), and the
SQL renderer unwraps it; no run-time generic type is created. No public API and no behavioural change
under JIT.

The same component had two sibling sites with the identical defect, both on the `SelectGroupHaving` path:
`SelectGroupConditionBinder.Bind` rebound the condition onto the entity with the non-generic
`Expression.Lambda`, and `SelectGroupComponent.CombinePredicates` combined the existing `HAVING` with it
the same way. Both now close `Expression<Func<TEntity, bool>>` (`Bind<TEntity>`, and the component's own
`TEntity`). They were not in the failing probe set because no probe uses `SelectGroupHaving`, but the
canonical instrument surfaced them as new `IL3050` rows once the harness began exercising `SelectGroup`;
after the fix the instrument lists no `src/` row for either site.

### Alternatives refuted

- *Precompiled delegate table*: the key type is user-declared and open-ended; a table cannot cover it.
- *Changing `QueryPreparationContribution` to carry key bodies*: a public-contract change for the same
  result; not needed.

## Class 2a — pivot marker constructed through `Activator` (`PivotProjectionRewriter`)

### Symptom

SQL Server `QuarterlyPivot` failed with
`MissingMethodException: No parameterless constructor defined for type 'NextORM.Core.PivotValues'`.

### Root cause

`PivotProjectionRewriter.Rewrite` created the captured `PivotValues`/`UnpivotValues` instance with
`Activator.CreateInstance(markerType)`, where `markerType` is a run-time `Type` argument. The trimmer
cannot see the constructor behind that lookup.

### Fix

The typed caller now supplies a compile-time factory (`static () => new PivotValues()` /
`static () => new UnpivotValues()`), and `Rewrite` invokes it. The `Type` is still passed for member
comparisons (that use is statically safe). No public API change; the marker instance and its role in the
rewritten projection are unchanged.

## Class 2b — ClickHouse tuples (`ClickHouse.Driver` 1.5.0)

S4 attributed six ClickHouse failures to `System.Tuple` construction. S5 measured two **distinct** causes,
both in the driver:

1. **Parameter type resolution (all parameterized queries).**
   `ClickHouse.Driver.Types.TypeConverter`'s static constructor registers the driver's CLR mappings,
   including a `Tuple<double, double>` entry for the native `Point` shape, through
   `Activator.CreateInstance<T>()` → `TupleType.BuildTupleFactory` → `GetConstructor(...)`. Trimming
   removes that constructor; the static constructor throws and the CLR caches the failure, so **every**
   parameterized ClickHouse query fails with an opaque `TypeInitializationException` — including the three
   queries that have no tuple at all.
   Fix: `ClickHouseTrimContracts.PreserveParameterTupleRegistration` declares the dependency with
   `[DynamicallyAccessedMembers(PublicConstructors)]` on a parameter receiving the literal
   `typeof(Tuple<double, double>)`, called from `ClickHouseDataContext`'s static constructor. This is the
   same annotation mechanism as the decimal-operator contract: no suppression, no `NoWarn`, no rooting of
   an assembly or member, no consumer configuration, one closed BCL type.
2. **Tuple-shaped result columns.** A `Tuple(...)` column of any element types is constructed per row by
   `TupleType.Read` → `Activator.CreateInstance` on a closed type built from the column's element types.
   Only the literal `Tuple<double, double>` registration is preserved, so other tuple columns cannot be
   read natively. `ClickHouseDataContext.MapColumnExpression` now refuses them **before the reader opens**
   with an actionable `NotSupportedException` (names the driver path and the remediation: project
   `tupleElement(...)` columns or read `toString(...)`). Under JIT the guard never fires.

### Evidence

Probe run against the same live containers (direct DLL references to `src/`, no nextorm packages):
PostgreSQL **10/10**, SQL Server **11/11**, ClickHouse **9/9** — 30/30, zero failures. The fail-closed
guard is separately demonstrated natively on the `Retention` query (a real tuple column, not part of the
probe): preparation throws the actionable message with the `MapColumnExpression` frame.

Native harness: nine scenarios (`selectgroup-keyed` and `selectgroup-having` added), all passing with
JIT-equal values on the published binary.
