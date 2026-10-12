# JoinInto: composite-junction M:N SQL link carrier

Follow-up carved out of #213 (JoinInto — composite keys, composite junction selectors, M:N under
single-query). T213 narrowed its accepted scope to **scalar M:N** (standalone and through
`SingleQuery`) plus composite relationship keys; the **composite-junction M:N** link carrier is
handed off to this issue, which keeps all of its inherited obligations. Milestone **1.0.9**.

## Why it was carved out

T213 tried three bounded carriers for a composite junction (a junction whose parent and/or child
foreign key has more than one component) and each failed closed. The failures are recorded under
`artifacts/pdca/T213/rv{1,2,3}/`; the deciding evidence is
`artifacts/pdca/T213/rv3/nested-projection/RESULT.md`.

1. **rv1 D02-P1 bounded carrier spike** — a derived `Projection<TJunction,long>` link item does
   **not** flow through the projection expander. `TryExpandEntityItem` has no metadata for the
   closed projection type, so `TryBuildProjectionSelectList` falls back to the parent entity's own
   columns and the shared materializer throws
   `ArgumentException: 'Id' is not a member of type Projection<...>` at
   `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:468`. Separately, a `MemberInit`
   entity-typed `Item1` is rejected as an unsupported property type at
   `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:465`.

2. **rv2 direction-B probe** — the composite `Projection<TJunction,long>` link source builds, but
   the componentwise ON predicate reading `link.Item1.<fk>` is not translatable:
   `Visitors/MemberTranslator.ResolveProjectionItemAliasIndex` throws `InvalidOperationException`
   at `src/nextorm.core/Visitors/MemberTranslator.cs:827` (reached from `VisitMember :487`,
   `SqlSourceRenderer.MakeWhere :999` / `MakeJoin :270`), because the `MemberInit`-derived link
   subquery emits an entity-typed `Item1` column, not slot-tagged junction columns.

3. **rv3 D04 gated nested-projection** — even with the single-level gate the nested carrier
   `Projection<Projection<TJunction,long>, TChild>` fails two ways: the nested access chain
   `pair.Item2.Item1.ChildId` misses alias resolution at
   `src/nextorm.core/Visitors/MemberTranslator.cs:847`, and nested `Projection<..>` materialization
   throws `ArgumentException` at `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:468`.
   The link **is** materialized in the in-memory/stitch path
   (`JoinIntoStitcher.cs:214` → `AssignChildrenFromLinks:241`), so the gap is nested-projection
   SQL generation + row materialization, not the stitch.

The scalar `JoinIntoLink<,>` path is unaffected: after the rv3 revert, filtered
`FullyQualifiedName~JoinInto` is green (core 143/143, sqlite 58/58) and the composite SQL case keeps
a deterministic `NotSupportedException` (fail-closed).

## Boundary

This issue owns a **strictly internal link carrier on the materializable entity-item path**, modeled
on the existing synthetic `JoinIntoLink<,>` (`src/nextorm.core/Builders/Joins/JoinIntoLink.cs`) and
carrying an occurrence component for multiplicity. It does **not** change the public
`LoadWith`/`JoinInto` surface, does not add inferred relationships, and does not regress scalar M:N.

Explicitly **decided separately** (not in this issue's scope): how many components the link carrier
may expose, and whether a second consumer justifies extracting a public/shared abstraction. Per the
T213 design checklist, no new runtime interface/type without a named second consumer; the carrier
stays internal and sealed unless a demonstrated consumer appears.

## Inherited obligations (from #213)

- Composite-junction M:N must work **standalone and through `SingleQuery`**, with full-component
  partitioning and occurrence preservation (former AC03).
- Provider coverage on **all six providers, including MariaDB** (no MySQL substitution): SQL
  generation, in-memory tests, and real integration (former AC05).
- Preserve: declared-only metadata resolution, occurrence semantics, cache isolation, paging scope,
  identity and alias uniqueness, and the existing unrelated restrictions.
- Green scalar controls and repeated cached execution must stay green.

## References

- Parent task: #213.
- Deciding evidence: `artifacts/pdca/T213/rv3/nested-projection/RESULT.md`.
- Carrier probes: `artifacts/pdca/T213/rv1/spike/RESULT.md`,
  `artifacts/pdca/T213/rv2/direction-b/RESULT.md`.
