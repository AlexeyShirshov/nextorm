# Interface entities without user implementation classes

> **Status (2026-11-12):** written design approved by the user; builder review found no blocking
> questions. The next stage is preparation of an implementation plan. Implementation is **not
> authorized**. GitHub issue
> [#34](https://github.com/AlexeyShirshov/nextorm/issues/34), verified current milestone `1.0.10-a`.
> This document supersedes the prior runtime `Reflection.Emit` proposal as the current design.

## Goal

Users declare entity interfaces, not implementation classes; NextORM supplies generated objects
when queries materialize them. Source generation is the sole implementation path for ordinary
runtime and NativeAOT. There are no user-facing entity attributes, manual registration steps, or
public construction APIs (`New`/`Create`). The feature applies to existing operations that
materialize interface entities, including `From`, identity selection, `Returning`, and
`Projection<T1,T2>` (not an exhaustive list).

The returned object implements the declared interface; it is not a user-constructible entity
contract. Public interface metadata, including inherited property attributes, must be preserved.
Get-only, get/set, and get/init properties are in scope. Flat interface entities are in scope;
nested interface-valued entity properties are not. A composite result may contain multiple flat
interface entities.

## Historical proposal (superseded)

The earlier RFC proposed runtime `Reflection.Emit`, dynamic proxy caching, and staged delivery
(public interfaces first, then projections/Returning, then a generator). Those phases and its
runtime-emit acceptance criteria are historical, not current requirements. Useful observations
remain: interface inheritance needs explicit property traversal; get-only properties need generated
backing storage; conflicting same-name properties with different types need a diagnostic; and
property mappings should be prebound rather than resolved by name per row. The old proposal's
public `MaterializationType`, public `New`/`Create`, and access-bypass ideas are rejected/out of
scope.

## Agreed requirements and boundaries

### Interfaces and operations

- Support public interfaces from the consuming assembly and referenced assemblies.
- Internal interfaces are eligible only when accessible to the consuming compilation, including
  via `InternalsVisibleTo`. Do not bypass access controls.
- Preserve inherited properties and applicable interface/property metadata. Diagnose conflicting
  same-name properties with differing types.
- Property-only eligibility is an architect choice: unsupported non-property members should receive
  clear diagnostics; do not promise arbitrary method implementation.
- Abstract classes remain out of scope. Do not add user construction APIs.
- Do not extend support to providers or operation shapes that have not been established by the
  existing product contract; the planner must inventory those before implementation.

### Discovery and generated registration

Agreement: automatic discovery uses concrete interface types appearing in generic type arguments;
an unannotated helper such as `Load<IUser>()` must work without linking the helper body to NextORM.
Over-generation is acceptable. Do not require user annotations or manual registration.

Architect choice (not a user decision): semantic discovery recursively considers concrete interface
types nested in composed generic arguments, e.g. `Projection<IUser,IAddress>`, deduplicates them,
and assigns deterministic generated names. Generated artifact/runtime registration handshake is
internal and statically rooted: do not use assembly scanning or Reflection.Emit to find
implementations. Exact handshake implementation is for the implementation plan.

Do not claim complete discovery for arbitrary API shapes or runtime type flows. The implementation
plan must inventory actual result shapes and add discoverability fixtures. Unsupported shapes should
get actionable diagnostics where statically detectable. Do not diagnose every unrelated interface
generic argument merely because it is not a suitable entity.

### AOT boundary

Issue #34 supports interfaces in existing working NativeAOT scenarios and introduces no new AOT
limitations. This is not a claim of global AOT compatibility or support for additional providers.
Existing projection/runtime-compilation AOT blockers remain tracked by internal record
`docs/specs/design/D370-stream-a-runtime-compilation.md` (D370); this is not verified as GitHub
issue #370 and those gaps must not be counted as #34 failures or passes.

### Architect choices / implementation-plan details

Implementation details remain private. No public `MaterializationType` is promised. The plan must
document generated type/factory mapping ownership and lifetime, and use prebound property mappings
rather than repeated per-row name lookup. Exact generated naming, cache implementation,
initialization shape (constructor/factory and backing fields while preserving the getter surface),
analyzer packaging, diagnostic IDs, and test-file layout are left to the builder. These are not
additional user decisions.

## Acceptance criteria for the implementation plan

The plan should convert these into concrete fixtures and gates now that this design has received
builder review and user approval:

1. Operation matrix covering direct materialization and supported projection/identity/
   composite/Returning paths, across SQL and in-memory where the operation exists. It must inventory
   real API result shapes rather than infer support from examples here.
2. Property matrix: get-only, get/set, get/init; inherited properties and attributes; conflicting
   same-name/different-type properties; property-only diagnostics.
3. Accessibility matrix: public local and referenced interfaces; internal interfaces accessible to
   consuming compilation, including `InternalsVisibleTo`; inaccessible interfaces are not bypassed.
4. Discovery fixtures for direct generic use, helper `Load<IUser>()`, and nested/composed generic
   shapes such as `Projection<IUser,IAddress>`. Verify deterministic generation and diagnostics for
   statically detectable unsupported shapes.
5. Verify no user attributes/manual registration, no Reflection.Emit, and no public construction
   API are needed.
6. Ordinary runtime regression tests plus source-generator compilation, diagnostics, and
   determinism tests. Verify SQL/in-memory parity and cross-provider behavior only where supported.
7. Publish and execute under NativeAOT against an equivalent concrete entity in the established
   supported baseline. Do not conceal existing D370 gaps as successful #34 coverage. DB integration
   tests must use the repository's canonical capped runner and its skip-budget checks.

No tests are required for this documentation-only update.

## Non-goals

- Implementing the feature now; this written design's approval authorizes transition to planning
  only, not implementation.
- Runtime Reflection.Emit or runtime discovery/scanning of generated implementations.
- Abstract-class proxies, nested interface-valued entity properties, arbitrary interface method
  implementation, or public `New`/`Create` APIs.
- Expanding provider support or claiming universal NativeAOT compatibility.
- Solving existing runtime compilation/projection AOT limitations tracked by D370.

## Tracking and next stage

- Issue: [#34](https://github.com/AlexeyShirshov/nextorm/issues/34)
- Verified milestone: `1.0.10-a` (no milestone change requested or made).
- Design path: `docs/specs/roadmap/todo_interface_poco.md`.
- Next stage: prepare an implementation plan that inventories APIs/result shapes and tests the
  discovery boundary. This document does not authorize implementation.
