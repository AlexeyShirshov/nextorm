# Security verification — string constant reachability into partition/sort rendering (T211 r1 n2/3)

- Anchors: `src/nextorm.core/DataContext/SqlBuilder.cs:728,744` (`MakePerParentRankExpression` partition
  and sort rendering) → `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:769` (`TryEmitValue` inlines
  a string as `'<value>'` with no apostrophe escaping).
- Question: can an untrusted `Expression.Constant(string)` reach the partition/sort rendering and alter
  the SQL?

## Verdict: only reachable from trusted compile-time metadata; untrusted runtime values are parameterized.

Probe: `tests/nextorm.sqlite.tests.EagerLoadPerParentPagingEdgeTests.Split_CapturedRuntimeStringSortKey_IsParameterizedNotInlined`
(green, `Feature=D211`). It renders both the eager per-parent rank source and the ordinary non-eager
query with an apostrophe-bearing string reaching the `ORDER BY`, and writes the statements under
`check/d211-security-*.txt`:

- Captured runtime variable (`OrderBy(x => marker)`, `marker = "cap'--"`):
  - eager: `... row_number() over (partition by parent_id order by $marker) ...`
  - plain: `select id from edge_child order by $marker`
  → bound as a **parameter** (`$marker`), never inlined. The regression test asserts the raw
  `'cap'--'` sequence is absent and the parameter is present.
- Compile-time literal (`OrderBy(x => "o'ops--")`):
  → `... order by 'o'ops--' ...` (inlined without escaping).

## Reachability argument

`Expression.Constant(string)` inside an `ORDER BY` / key selector arises only from a **string literal
written in source code** (trusted). A captured variable, property or other runtime value is emitted as
a query parameter (`$marker`), proven on both the new per-parent path and the ordinary path — the
renderer's fast-parameter recipe covers runtime values. The public API takes `Expression<Func<...>>`
compiled from code; there is no path that converts an end-user string into an `Expression.Constant` at
runtime. Therefore an untrusted value cannot reach the unescaped inline. The T211 change reuses the
same `BaseExpressionVisitor.TryEmitValue` as every other renderer; it introduces no new injection reach.

## Defect candidate (not fixed in r1 scope)

`BaseExpressionVisitor.TryEmitValue` does not escape `'` in an inlined string literal, so a (trusted)
compile-time sort/key literal containing an apostrophe renders invalid SQL. This is a pre-existing,
general renderer behavior (the same on the non-eager path), outside the declared T211 footprint (no
production file beyond `EntityBuilder.cs`/`EntityBuilderEagerLoading.cs`/`SqlBuilder.cs`), and not
reachable from untrusted input. Recorded for a separate security-hardening task rather than
scope-expanded here.
