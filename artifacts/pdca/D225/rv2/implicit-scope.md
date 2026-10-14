# T225 / rv2 — explicit vs implicit navigation scope (E07, AC05-scope)

Tree `0ed8e01dae44122276056f9f89676d913891ff09`; contract rv=2.

The r=3 plan's claim guard: publish only behaviour proven by a resolved entry/call path; never
present an unresolved capability as fact. The gather below establishes both paths.

## Implicit navigation path (proven restrictive)

- Entry: `NextORM.Core.NavigationPathResolver` @ `src/nextorm.core/Visitors/NavigationPathResolver.cs:30`
  (Roslyn `types`/`members`).
- `Resolve` @ `:49` → `ResolveHop` @ `:211`; Roslyn `refs ResolveHop` → exactly one call site,
  `NavigationPathResolver.Resolve :70`.
- `RequireSingleKey` @ `:302-306`: rejects `ForeignKey.Count != 1 || PrincipalKey.Count != 1`
  with "only single-column relationship keys are supported" — reached for `OneToMany`,
  `ManyToOne` and `OneToOne` hops (`:222`, `:233`, `:246`).
- `ResolveJunctionHop` @ `:266-279`: rejects a composite junction key — "has a composite junction
  key; only single-column junction keys are supported" (`:272-279`).
- XML contract @ `:45`: a rejected path includes "a key is missing/composite".
- `tests/nextorm.core.tests/NavigationPathResolverTests.cs:795-801` records the composite-key
  fail-closed boundary as a documented guard (unreachable through the current single-column
  metadata constructor, hence recorded rather than forced through a fake relationship model).

Conclusion: the implicit-navigation restriction is **verified** and may be published, but it is
scoped to implicit navigation only — it must not read as a nextorm-wide key restriction.

## Explicit `JoinInto` path (proven permissive for composite keys)

- `JoinIntoCompositeRemainderTests:97` (1:N) and `:147` (1:1) exercise multi-component
  FK/principal keys with full-key matching asserted.
- `JoinIntoCompositeJunctionRejectionTests:113` asserts the composite-junction declaration
  prepares without throwing.
- `docs/advanced/relationships.md:194`/`:292` (T223-authored, retained) state the explicit path
  joins and groups on every declared component, while the component lists come from relationship
  metadata and the fluent helpers take a single key column.

## Applied wording (guide 29)

EN:257 / RU:263 now read that **implicit navigation** resolves only single-column
foreign/principal/junction keys and rejects a composite key, pointing composite keys at the
explicit `JoinInto`, "which joins and groups on every declared component". This asserts only the
two verified paths; it invents no implicit capability and keeps EN/RU equivalent.

## Claim ledger

| Assertion in the delta | Status | Evidence |
|---|---|---|
| Implicit navigation rejects a composite key | verified | `NavigationPathResolver.cs:302-306`, `:272-279`; `NavigationPathResolverTests.cs:795-801` |
| Explicit `JoinInto` joins/groups on every declared component | verified | `JoinIntoCompositeRemainderTests.cs:97`, `:147`; `relationships.md:194`, `:292` |
| Fluent declaration helpers take a single key column | verified | `relationships.md:194`; `EntityBuilder.cs:942-1002`; `todo_navigation_properties.md:31` |
| Composite-junction M:N SQL support delivered | verified | `JunctionLinkSourceFactory.cs:205-206`; `JoinIntoCompositeJunctionRejectionTests.cs:113` |
| M:N `JoinInto` under `EagerLoadMode.SingleQuery` supported | verified | `JoinIntoManyToManySingleQueryTests.cs:20`; `JoinIntoCompositeRemainderTests.cs:296` |

No unresolved capability is presented as fact.
