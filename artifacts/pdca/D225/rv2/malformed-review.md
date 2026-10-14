# T225 / rv2 — malformed-key negatives (E06, AC03-negative)

Tree `0ed8e01dae44122276056f9f89676d913891ff09`; contract rv=2.

The delivered composite-junction M:N support is **not** an unconditional "all composite keys
accepted" claim. Malformed metadata is still rejected fail-closed, before any connection or SQL.

## Guards in the implementation (Roslyn-resolved)

| Guard | Site | Rejection |
|---|---|---|
| Junction FK under-cover | `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs:371-373` | `NotSupportedException` — the junction foreign keys "do not cover every composite key component". |
| Contradictory component order | `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs:382-384` | `NotSupportedException` — "declares its key components in an order that contradicts the junction's declared key order" (`#223`). |
| Component type mismatch | `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs:388-390` | `NotSupportedException` — a key component type that does not match its junction FK component. |
| Empty parent/child FK list | `src/nextorm.core/Builders/Joins/CompositeJunctionLinkFactory.cs:82-83` | `NotSupportedException` — "A composite-junction link needs at least one parent and one child foreign-key component." |
| Unmappable component type | `src/nextorm.core/Builders/Joins/CompositeJunctionLinkFactory.cs:105-113` | `NotSupportedException` — pointer/by-ref/by-ref-like/open-generic component. |
| Missing junction metadata | `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs:198-200` | `NotSupportedException` — the relationship "has no junction metadata". |
| Relationship-level under-cover | `src/nextorm.core/Builders/EntityBuilder.cs:976-980` | `NotSupportedException` — junction FK does not cover every composite key component. |

## Executed negative oracles

Invocations (arg arrays; run as two in-scope selectors so each is a plain, non-compound filter —
`artifacts/pdca/D225/rv2/logs/composite-oracles.log` records the equivalent combined run):

1. `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinIntoCompositeJunctionRejectionTests"]`
   → exit 0; total 4; succeeded 4; failed 0; skipped 0.
2. `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinIntoCompositeRemainderTests"]`
   → exit 0; total 17; succeeded 17; failed 0; skipped 0.

- `JoinIntoCompositeJunctionRejectionTests:113` — `act.Should().NotThrow(...)` for the supported
  composite-junction declaration (support, not rejection).
- `JoinIntoCompositeJunctionRejectionTests:226` — `Throw<NotSupportedException>` with
  "component correspondence cannot be established" (contradictory order).
- `JoinIntoCompositeJunctionRejectionTests:308` — `Throw<NotSupportedException>` with
  "do not cover every composite key component".
- `JoinIntoCompositeRemainderTests` — positive composite assertions at `:483`, `:599`, `:648`,
  `:803` (arity variants) all pass.

Full logs: `artifacts/pdca/D225/rv2/logs/composite-rejection.log`,
`artifacts/pdca/D225/rv2/logs/composite-remainder.log` (combined-equivalent:
`artifacts/pdca/D225/rv2/logs/composite-oracles.log`, total 21 / 21 passed / 0 skipped).

## Documentation consequence

The docs state the delivered support and the remaining malformed-metadata rejection separately.
Neither the removed `#223` message nor an unconditional acceptance claim appears in the delta.
