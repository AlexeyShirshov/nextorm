# T225 / rv2 — semantic review (E01, AC01–AC04)

Tree `0ed8e01dae44122276056f9f89676d913891ff09`; contract rv=2; docs-only r=3.

Independent audit of every authored claim against the current implementation and the existing
oracles. Symbols resolved Roslyn-first (`artifacts/pdca/D225/rv2/roslyn.json`); no symbol was
invented, and no claim was published that the implementation does not support.

## Source anchors (implementation)

- `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs:205-206` — a junction whose
  parent and/or child FK spans more than one component is routed to `BuildCompositeSource`
  (`:118-179`) instead of the two-member scalar link shape.
- `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs:198-200` — `NotSupportedException`
  there is only for a relationship with **no junction metadata**, not for a composite junction.
- `src/nextorm.core/Builders/Joins/CompositeJunctionLinkFactory.cs:78-98` — `Ensure` emits the
  flat, arity-general carrier; `:82-83` rejects an empty parent/child FK component list;
  `:105-113` (`ValidateComponent`) rejects an unmappable component type.
- `src/nextorm.core/Builders/EntityBuilder.cs:942-1002` (`ResolveJoinIntoRelationship`) —
  many-to-many is accepted when junction metadata is present and the junction FK covers every
  key component (`:976-980` rejects only an under-cover junction).
- `src/nextorm.core/Builders/EntityBuilder.cs:729` — "a composite principal/foreign key is
  supported componentwise" on the explicit `JoinInto` path.
- The literal `composite-junction M:N is not supported` is absent from `src/**`.

## C1 — scalar M:N `JoinInto`, standalone and `EagerLoadMode.SingleQuery`

- Standalone scalar M:N: `JoinIntoCompositeRemainderTests:681` (`ScalarManyToMany_Standalone…`),
  `:695`, `:708` (async), `:721` (non-list terminal), `:732` (repeated cached execution).
- M:N composed with single-query eager loading: `JoinIntoManyToManySingleQueryTests:20`
  (`ManyToManyJoinInto_WithLoadWithAndSingleQuery_ShouldStitchBothCollections`) and
  `JoinIntoCompositeRemainderTests:296`/`:311` (`ManyToMany_SingleQuery…`). The page/class
  documentation states the M:N declaration pairs with a single-query `LoadWith`.
- Verdict: neither mode may be called unsupported. The previous comparison-spec claim
  ("a many-to-many `JoinInto` under `AsSingleQuery` … rejected") is obsolete and named a
  non-existent API (`symbols AsSingleQuery` → not found).

## C2 — composite relationship keys (1:N / 1:1 and scalar M:N junction)

- `JoinIntoCompositeRemainderTests:97` (`CompositeOneToMany_ShouldGroupChildrenByFullKey`) and
  `:147` (`CompositeOneToOne_ShouldAssignByFullKey`) exercise multi-component FK/principal keys
  and assert the full-key match rejects the cross row.
- `docs/advanced/relationships.md:194` and `:292` (T223-authored, retained) state the component
  lists must come from relationship metadata while the fluent declaration helpers take a single
  key column.
- Verdict: the correct statement is "composite keys come from relationship metadata and are
  joined componentwise; the fluent declaration helpers declare a single key column". A blanket
  "composite keys are rejected" is false; a blanket "composite keys are always single-column" is
  also false.

## C3 — composite-junction M:N SQL support + malformed-key rejection

- Support: `JunctionLinkSourceFactory.cs:205-206` + `CompositeJunctionLinkFactory.cs:78-98`;
  `JoinIntoCompositeJunctionRejectionTests:113` asserts `NotThrow` ("the composite-junction M:N
  link source is supported again (#223)").
- Rejection (negative): see `malformed-review.md` (E06). The obsolete
  `NotSupportedException("composite-junction M:N is not supported; see #223")` no longer exists
  in `src/**` and is not documented.
- Verdict: describe delivered support; separately document the genuine malformed-key guards; do
  not claim an unconditional "all composite keys accepted".

## C4 — in-memory composite-junction stitching retained

- `JoinIntoCompositeRemainderTests:201` (`CompositeJunctionManyToMany_ShouldStitchByFullJunctionKey`)
  runs on `InMemoryDataContext` and stitches by the full junction key; arity variants at `:402`,
  `:454`, `:514`, `:570`, `:626`.
- Verdict: SQL support does not remove in-memory stitching; the documentation model keeps it.

## Documentation delta applied (five stale files)

| File | Change |
|---|---|
| `docs/guide/29-implicit-navigation.md:257` | Re-scoped the blanket composite-key limitation to the implicit-navigation path and pointed composite keys at the explicit `JoinInto`. |
| `docs/ru/guide/29-implicit-navigation.md:263` | EN/RU-equivalent re-scope. |
| `docs/specs/comparison/capability-matrix.md:110` | Removed the obsolete "composite keys / composite junction selector / M:N `JoinInto` under `AsSingleQuery` are rejected" clause; retained the valid convention-inference and single-column-fluent-helper limitations. |
| `docs/specs/comparison/linq2db-comparison.md:109` | Same correction against delivered behaviour. |
| `docs/specs/comparison/linq2db-backlog-gap-analysis.md:54,346,405,505` | Corrected every stale residual-gap statement consistently. |

The eleven other footprint files were audited and retain accurate T223/#223-corrections content
(no edit). No product code, API, test, TOC path, numbering or page move was changed.

## Out-of-scope observation (not edited)

`src/nextorm.core/Builders/EntityBuilder.cs:730-732` (product XML doc) still reads that a
"composite-junction many-to-many (carried by issue #223) is rejected with
`NotSupportedException` — use the explicit-key overload instead", which contradicts the delivered
behaviour asserted by `JoinIntoCompositeJunctionRejectionTests:113`. This is product source, out
of the documentation-only footprint, and is reported here rather than edited.
