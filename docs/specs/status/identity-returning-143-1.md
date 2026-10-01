# PDCA 01 — Issue #143 identity joined Returning

- collection: `1.0.9-b-2`, task 1, group-1, branch `1.0.9-b`
- cycle: N=1, revision r=1, attempt n=1
- mode: autonomous + auto-commit; single group → current worktree, no worktree/branch/merge
- issue: #143 (https://github.com/AlexeyShirshov/nextorm/issues/143)
- design spec: `docs/specs/design/issue-143-identity-returning.md` (approved; user requested the run)

## Goal
Enable parameterless `.Returning()` and equivalent `.Returning(p => p)` for PostgreSQL joined UPDATE and DELETE over built-in `Projection<T1..Tn>`, arities 2–8, through BOTH standalone materialization and mutation-CTE consumption. The essential objective is a logical returned-column address (slot + mapped-member identity + stored unique alias) preserved across preparation, SQL generation, wrappers and materialization — not merely removing the identity guard.

## Acceptance criteria
| ID | Observable success | Negative case |
|---|---|---|
| A1 | Both APIs return the declared `Projection<…>` preserving item order; `Item1` stays the physical target. | No `.Returning()` call must not introduce returning behavior. |
| A2 | Every returnable mapped property materialized with mapping, converter, provider-type, duration metadata. UPDATE → post-update target values; DELETE → deleted target values. | `NotMapped`/dynamic excluded; unsupported multi-column Range explicitly rejected, never silently omitted. |
| A3 | Identical member names and repeated CLR types stay distinct by slot in standalone and CTE results. | Unknown/ambiguous slot/property addresses fail diagnostically; no first-match fallback. |
| A4 | CTE full-result reads, separate selection/filter of `Item1.Id`/`Item2.Id`, and supported derived wrappers use correct returned columns. | A required property missing from a joined-source shape fails with slot/member info before DB execution. |
| A5 | All 28 arity × operation × route combinations have SQL/core and real-PG evidence; both identity spellings exercised. | Zero-row mutations return the established empty-result behavior without fabricated items. |
| A6 | Existing explicit scalar/anonymous/ctor/member-init projections, single-table Returning, ordinary joins and #113 alias behavior remain compatible. | Existing non-PG and unsupported join-form rejections unchanged. |
| A7 | Build zero warnings/errors; coverage line ≥85%, branch ≥75%; required perf and integration gates pass. | Skipped providers, weakened assertions or unexplained regressions are not passing evidence. |
| A8 | EN/RU guides + relevant XML describe the feature and limits. | Public docs contain no links to internal specs. |

## Ordered DO tasks (all fix-now; none deferred)
- D1 Capture baseline and exact contracts: via scout/roslyn record internal storage/copy boundaries, identity detection, mapping identity, source-shape validation, existing error behavior, project registers; capture pre-change build/test counts, coverage and the seven-case acceptance benchmark baseline. No production edits before baseline capture.
- D2 Isolated throwaway PoC (gate): smallest transient patch proving the four proofs below against real PG; store patch/SQL/evidence outside tracked production files; restore the exact pre-probe tree selectively afterward. Passing PoC is necessary, not production completion.
- D3 Production shape/address contract: internal immutable returned-column binding (zero-based slot + canonical mapped-member identity + deterministic unique SQL alias + materialization ordinal + existing property/conversion metadata); prepared-shape-scope lookup; whole-output alias allocation; persist through shape copies; expand all mapped returnable scalars in slot order; reject missing required source property / unsupported Range with useful diagnostics. Unit tests accompany.
- D4 Translation and materialization propagation: preserve address/alias metadata through supported copies/wrappers; emit identity aliases; slot-aware member resolution; materialize every item. Explicit projections keep existing behavior.
- D5 Public API and identity normalization: parameterless UPDATE `Returning()` + DELETE extensions arities 2–8; `p => p` normalized to same identity path for built-in projections; preserve every existing signature and no-call behavior; no provider capability expansion.
- D6 Complete regression matrix: core `JoinReturningBuilderTests.cs`; new `JoinReturningIdentityShapeTests.cs`; new PG `JoinReturningIdentitySqlGenerationTests.cs`; existing provider `UpdateJoinSqlGenerationTests.cs`; PG `PostgresSpecificTests.cs`, `PostgresJoinAritiesTests.cs`; existing `CommonTestSuite.Update/Delete/Cte.cs`. Run affected unit/provider + real container integration suites.
- D7 Documentation: six guide pages + XML at D5 public API sites, same change.
- D8 Final quality/perf evidence: rebuild, coverage + branch delta, targeted mutation checks, seven-case acceptance comparison, rerun integration, CRLF/diff hygiene. Supply CHECK evidence.

## D2 PoC gate — four required proofs
Fixture: two-item self-join of the same entity type with identical property names and deliberately different row values; deterministic matches.
1. Address/alias proof: generated aliases unique; slot/member bindings survive mutation-CTE shape creation and a supported wrapper.
2. Standalone proof: identity UPDATE and DELETE both materialize complete, distinct `Item1`/`Item2` objects with correct post-update/deleted values.
3. CTE proof: both mutations support full Projection materialization and separate reads/filters of `Item1.Id`/`Item2.Id` with intended values.
4. Joined-source proof: a representative allowed derived/read-CTE joined-side source with a complete shape works with correct bindings and actual PG values.
Failure blocks D3–D8 and returns to PLAN for architectural reassessment; it does not authorize narrowing scope.

## Variant matrix
Baseline: every arity 2–8 × {UPDATE, DELETE} × {standalone, mutation CTE} = `P` (core/SQL test AND real-PG test, both `.Returning()` and `p=>p`).
Feature rows (all mandatory): repeated same CLR type/identical names → test all baseline; different CLR types colliding names → core/SQL all + PG 2,3,8; mixed repeated/nonadjacent → PG+SQL 3,8; converters/provider/duration → metadata all + PG 2,3,8 both routes; physical names ≠ CLR + alias collisions → all arities SQL/shape + PG 2,3,8; NotMapped/dynamic exclusion + identity/computed inclusion → all + PG 2,3,8; multi-column Range in any slot → guard + negative test arities 2–8 both routes, fail before SQL; complete derived/read-CTE joined-side → all + PG 2,3,8; missing required source property → guard + negative test; CTE full Projection + slot selection/filter + derived read wrapper → all/full, detailed at 2,3,8; zero rows + default/null values → all baseline; no-Returning + explicit forms → test, preserve SQL snapshots; non-PG providers (SQLite, SQL Server, MySQL, MariaDB, ClickHouse) → guard + negative test, keep rejection; outer/cross/unsupported join forms → guard; custom/dynamic projections, extra Range, alternate target slots, #113 alias-only → guard, do not route into identity (deferred until separately approved scope); cache/store flags + prepared reuse → test repeated execution, no sticky command-state mutation.

## Test strategy
- Core unit: API/result types, identity normalization, canonical address construction, metadata propagation, source-shape validation, exclusion/error branches, no-call behavior.
- SQL generation: exact source bindings, unique deterministic aliases, slot-aware CTE references, wrapper propagation, provider gates, unchanged explicit SQL snapshots.
- Real PG integration: actual complete object values, target identity, converters, empty results, mutation-CTE reads, permitted sources.
- Regression integration: SQLite + PostgreSQL + SQL Server + MySQL + ClickHouse container suites per `running-integration-tests` skill; skipped providers are unresolved evidence.
- Named cases: `IdentityReturning_AllArities_AllItems`, `IdentityReturning_ParameterlessEqualsIdentityLambda`, `IdentityReturning_SelfJoin_CteSlotSelectionAndFilter`, `IdentityReturning_MixedRepeatedTypes_PreservesSlots`, `IdentityReturning_DerivedAndReadCteSources`, `IdentityReturning_MappingAndConverters`, `IdentityReturning_MissingSourceMember_Throws`, `IdentityReturning_RangeInAnySlot_Throws`, `NoReturning_DoesNotAutoReturn`, `ExplicitReturning_SqlAndMaterializationUnchanged`.
- Coverage: line ≥85%, branch ≥75% (existing scope); report before/after + changed-method branch delta.
- Mutation checks (temporary, restored): kill mutants for removing slot from key; dropping stored alias during copy; first-match; skipping missing-property/Range validation; swapping target slot; dropping converter metadata; enabling Returning without a call.

## Docs plan
Update EN+RU: `docs/guide/08-cte.md`, `docs/guide/17-update-statement.md`, `docs/guide/16-delete-statement.md` and `docs/ru/guide/` twins; XML for new overloads. Describe both identity spellings, whole mapped-item results, item order, target semantics, PG/INNER/arity limits, supported CTE/derived paths, explicit rejections. Remove identity prohibition only. No public rename, no guide renumbering, no links to `docs/specs`.

## Decisions
- Perf measurement: REQUIRED. Affects preparation, cached translation, materialization (`MemberTranslator.cs:531-615`, `QueryCommand.QueryPreparer.cs:599-681`, `RowMaterializerBuilder.cs:110-187,226-254`). Capture pre-change + final Release seven-case `--anyCategories=acceptance` vs `docs/specs/performance/acceptance-benchmarks.md`; add focused prepared identity standalone/CTE measurements at arities 2 and 8.
- Reconnaissance: REQUIRED. D1 bounded contract verification; D2 approved experimental PoC gate.
- Unit execution mode: sequential, one current worktree; no worktree/branch/merge. Shared contract = D3 slot/member identity + alias + metadata.
- Order: D1 → D2 (gate) → D3 → D4 → D5 → D6 → D7 → D8.

## Risks
- Exact private copy/storage sites + additional project registers not supplied → D1 scout verifies via roslyn with `file:line`.
- Canonical mapped-member identity representation → use existing canonical mapping identity; D1 proves stability across copies; else add internal stable identity (no public interface requirement).
- Perf case names/thresholds + baseline coverage absent → D1 captures before edits.
- SQL alias truncation/collision → short ASCII deterministic whole-output allocation + uniqueness tests.
- Broad translator changes regress ordinary queries → activate slot-aware lookup only for annotated identity shapes; preserve/test existing paths.
- PoC cleanup could damage existing work → snapshot pre-probe contents; selective restore only.
- Infrastructure/coverage/mutation evidence unavailable → attempt documented recovery; report precise unresolved evidence; never count skips.

## Progress log
- 2026-10-01 PLAN(r1): plan recorded by coder; DO starting autonomously.
- Done:
- Verified:
- Incomplete:
