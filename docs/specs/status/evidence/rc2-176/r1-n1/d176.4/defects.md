# D176.4 defect record

## Defect 1 — actual `Projection<T1,T2>` terminal failed (FIXED)

**Symptom (before fix).** `EntityBuilder<Projection<T1,T2>>.WriteJson` on a bare join
(`ctx.From<Parent>().LeftJoin(ctx.From<Child>(), ...)`) threw:

```
NotSupportedException: Duplicate JSON property name 'Id' after naming-policy conversion; JSON object members must be unique.
```

because the bare join never entered the JSON-only capture (it has no `NewExpression`/`MemberInit`
`_exp`), so its slot-tagged select list fell through to the flat phase-1 plan and the global
duplicate-name guard rejected the two `Id`/`Name` columns from `Item1`/`Item2`. R176.02 requires
the actual `Projection<T1,T2>` shape to emit top-level `Item1`/`Item2`.

**Fix (within D176.2 footprint).** `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`:
added `BuildJsonProjectionShape` (line 1118) which groups the already-expanded select list by
`ProjectionEntityItem.Slot` into `ItemN` objects carrying an any-column-not-null presence rule, and
invoked it from `TryBuildProjectionSelectList` when `JsonShapeMode` (lines 1240 and 1306). This is
the plan §4 mechanism ("Preserve `ProjectionEntityItem.Slot/Member` ... identify `Item1`/`Item2`
boundaries"). The clone-only hook leaves ordinary preparation and materialization untouched.

**Verification.** After the fix the bare projection emits exactly the same bytes as the explicit
`Select(p => new { p.Item1, p.Item2 })` form and the unmatched outer-join slot is JSON `null`
(`probe-d176.4-projection-after-fix.txt`, `JsonStreamingTests.BareProjection_*`).

## Defect 2 — null-valued conditional nested construction unsupported (DO→PLAN candidate, not fixed)

**Status.** Confirmed unsupported. Evidence: `probe-d176.4-conditional-collection.txt`
(`tests/nextorm.sqlite.tests/D176Probe3Tests.cs`, probe removed after the run).

```
COND_MEMBER_FAIL: NotSupportedException: Column 'Child' has type <>f__AnonymousType17`1[System.String] which is not supported by JSON streaming in phase 1; ...
COND_ROOT_FAIL: InvalidOperationException: JSON streaming requires an explicit Select projection; the query has no selected columns.
```

for `Select(x => new { x.Id, Child = x.Name == null ? null : new { x.Name } })` and
`Select(x => x.Name == null ? null : new { x.Name })`.

**Anchors.** `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:844` (`TryPrepareJsonShapeColumns`
requires `body is NewExpression or MemberInitExpression`) and `:901` (`JsonShapeNeedsCapture`
returns `false` for a `ConditionalExpression`). No hidden presence scalar is produced.

**Why it is a plan-scope change.** Plan §5 ("Explicit null-valued construction branch: write `null`.
Preserve the branch's presence decision through a hidden scalar presence expression") and the §10
row "Null-valued conditional nested construction with translatable predicate" require exactly this
case. Implementing it needs conditional-expression capture plus a lowered presence expression, which
is beyond a D176.4 test-only change. **Recommend DO→PLAN**; do not invent a fix under D176.4.

## Observation — `List<T>` member literal captured as `{}`

`Select(x => new { x.Id, Items = new List<int>() })` currently emits `[{"Id":1,"Items":{}}]`
(`probe-d176.4-conditional-collection.txt`, `LIST_OK`) because a non-root `NewExpression` is treated
as a nested object. The §10 collection guard is covered at the writer level for `List<int>[]`,
`Dictionary<,>[]` and `IEnumerable<int>[]` elements (`JsonShapeWriterTests`); the direct collection
member literal is an unguarded edge and is recorded here as a follow-up, not fixed in D176.4.
