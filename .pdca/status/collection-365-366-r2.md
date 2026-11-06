# Collection status: 365-366-r2



- collection-id: `365-366-r2`

- form: `collection`

- plan_cap: `4`

- lane_cap: `1`

- cap_reason: strict T365 → T366 dependency, shared nextorm.slnx / Directory.Packages.props / docs surface, single exclusive branch collection/365-366 without merge, and a quiet single-threaded stand required by the AC6 and acceptance-benchmark measurements

- base_ref: `83c545767cdd46cb58d054445c3ce028d4d55b0d`

- branch: `collection/365-366`

- worktree: `/home/alex/sources/nextorm-365-366`



## Input



«пдца по задачам 365 366 автономно автокомит»



«пдца по задачам 365 366 автономно автокомит»



Ограничения: `по задачам 365 366`, `автономно`, `автокомит`.



### GitHub issue #365 (source: `gh issue view 365 --repo AlexeyShirshov/nextorm --json number,title,url,milestone,state,body`)



## Status and boundaries



Scope agreed in conversation after #362 closed `incomplete` (AC7 infeasible while the component was required to emit the AC4 rank-window shape against an aggregate+join baseline). No written spec and no implementation plan yet; product implementation is **not started**. This issue is the tracking record for the next slice; it stays in milestone `1.0.9-rc5`.



## Intent



Ship a **product** extension package built entirely on the public query-component layer, next to the frozen built-in operator:



- keep the current `SelectWhereMin` / `SelectWhereMax` implementation **untouched** as the compatibility path and as the performance **baseline**;

- add `SelectWhereMinEx` / `SelectWhereMaxEx` that are implemented **only** through public query components (`IQueryComponent` + `EntityBuilder.WithComponent`), with **zero** intersection with the legacy implementation;

- put the new implementation in a separate project/package `nextorm.extensions`.



The component layer was already proven end-to-end in #362 by an external no-IVT library; this issue is the productization of that proof, not a new design.



## Frozen legacy (must not change)



`SelectWhereMin` / `SelectWhereMax`, `SqlBuilder.MakeExtremeRowSelect`, `MakePortableExtremeRowSelect`, `MakePortableExtremeRowInner`, `MakeNativeExtremeRowSelect`, `ExtremeRowLowering`, `ExtremeRowCompatibility`, the `QueryCommand.ExtremeRow*` fields, and the `InMemoryExtremeRow` path. Verified unchanged by diff in the implementing cycle. They serve two purposes only: compatibility and the benchmark baseline.



## Design



- **Project**: `src/nextorm.extensions`, a separate NuGet package that references `nextorm.core` only (no provider references, no `InternalsVisibleTo`, no raw SQL, no global handler registration).

- **API**: `SelectWhereMinEx` / `SelectWhereMaxEx` as ordinary extension methods on `EntityBuilder<TEntity>`, attaching one query component with a stable slot; Min and Max share one slot (assignment replaces in place).

- **No shared infrastructure with legacy**: no `ExtremeRow*` command fields, no new branches in `SqlBuilder.MakeSelect` / `InMemoryQueryBuilder`, no new rows in `QueryPlanEqualityComparer`, no reuse of `ExtremeRowLowering` / `ExtremeRowCompatibility`.

- **SQL shape is free**: the component may emit the aggregate+join shape where it is faster (as legacy does for `All`) and a rank-window shape where that wins. The AC4 rank-window mandate from #362 was a *proof* constraint, not a product requirement, and it is **not** carried here.

- **Seed implementation**: the proven `ExternalSelectWhereMin/Max` library under `tests/nextorm.querycomponents` (52 assertions, semantic parity matrix, six-provider SQL generation and live coverage). The no-friend test assembly keeps proving the packaged surface through an ordinary package reference (no IVT).

- **Caching**: component state and semantics survive clones; structural hash **and** equality participate in the plan key; `BuildQuery` runs on the cache miss only and never on a confirmed warm hit; parameter rebinding (captured and positional) works with a warm cache and does not leak.



## Acceptance criteria



- **AC1 Packaging & surface** — `nextorm.extensions` builds and packs as its own package, references `nextorm.core` only, exposes `SelectWhereMinEx`/`SelectWhereMaxEx` and nothing else public beyond the component wiring; no IVT anywhere; no raw SQL; no global registration.

- **AC2 Semantic parity with the frozen legacy** — `One`/`All`, scalar/composite values, global/grouped, NULL keys and NULL group keys, input `Where`, ties, scalar/DTO/anonymous projections, `.Select(...)` after, outer `OrderBy`/`ThenBy`, paging, and the same rejection boundaries (distinct, set operations, aggregates, unsupported shapes, in-memory policy), verified by paired execution against legacy on the same database.

- **AC3 Component semantics** — multiple independent components, same-slot replacement (Min→Max, Max→Min) without nesting, explicit rejection of conflicting exclusive reshape slots, structural hash-collision discrimination via `Equals`, clone/`CloneForCache`/`ResetPreparation` parity, hidden-alias collision safety for user columns named `rn`/`rank`/`__nextorm_ext_rn`.

- **AC4 Plan cache & rebinding** — `BuildQuery` only on a cache miss, zero calls on a confirmed warm hit (counter-asserted), changing captured/positional values with a warm cache rebind without leakage, no `queryCommand.Cache = false`, no cache disable.

- **AC5 Providers** — SQL-generation tests plus a live integration matrix on PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse and SQLite; no component in the in-memory provider for integration; no-component regression tests for ordinary commands.

- **AC6 Performance vs frozen legacy** — for every mandatory scenario (`Min/Max × One/All × global/grouped × scalar/composite` plus no-component controls): `CI99_upper(T_ex / T_legacy) <= 1.0` (or throughput `CI99_lower >= 1.0`) **and** `Alloc_ex <= Alloc_legacy`. SQL shape is free, the threshold is not; an inconclusive interval counts as unmet.

- **AC7 Docs & hygiene** — EN + RU docs for the new package and API, no links to `docs/specs/**`, build `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors, CRLF, CPM, coverage thresholds 85/75 and 65/74 unchanged.

- **AC8 Legacy untouched** — a diff check proves none of the frozen legacy files changed; only `nextorm.extensions`, its tests, docs and the solution membership are touched.



## Out of scope



- Migrating, deprecating or removing the legacy `SelectWhereMin/Max`.

- Changing `#302` lowering.

- A public opaque-SQL-fragment primitive (separate user-level decision; see #362).

- Re-basing `#362`'s AC7 measurement.



## Related



- #362 — component proof; closed `incomplete` because AC4's rank-window mandate could not meet the aggregate+join baseline.

- #302 — frozen internal lowering.

- #153 — external no-IVT precedent.

- #319 — build-once candidate.





### GitHub issue #366 (source: `gh issue view 366 --repo AlexeyShirshov/nextorm --json number,title,url,milestone,state,body`)



## Goal / agreed requirements

Support nextorm under .NET 10 NativeAOT with functional parity for ALL currently supported nextorm features and provider/feature combinations, not just removal of the first MetadataToken crash. The user explicitly requires all capabilities to be covered; no SQLite-only or reduced-feature AOT support contract.



## Reproduction context

A .NET 10 file-based SQLite reporting application using nextorm/nextorm.sqlite 1.0.9-rc4 publishes as a small NativeAOT executable but fails on its first query with InvalidOperationException: There is no metadata token available for the given member, reported at NextORM.Core.MemberInfoExtensions.SameProperty. Its query shapes include interface entities, captured predicates, anonymous projections, nullable scalar mapping, SQLite json_extract, grouping and aggregates. The external application is ONLY an example; it is not a test fixture and its repository must not be modified.



## Agreed test architecture requirements

- Add a dedicated executable NativeAOT test project to the solution (not merely a normal JIT unit test).

- Reuse existing integration databases, schemas, seed data, connection resolution and container lifecycle. Do not invent a separate test database infrastructure.

- The integration host resolves/starts/seeds the existing provider once and passes the actual resolved connection to the native child process. In particular SQLite must use the parent's existing file rather than the child's process-derived default. The child must not independently reseed or manage containers.

- Publish with PublishAot=true and run the resulting native executable. Compare semantic results against existing expected values/JIT behavior; successful compilation alone is insufficient. Cover runtime execution and failures in automation/CI.

- Coverage must include every existing supported feature/provider combination and all public product surfaces, including core/in-memory, providers, nextorm.extensions and the EF integration surface. Preserve existing dialect capability differences; do not manufacture SQL features unsupported by a provider.

- Missing AOT functionality/driver support is a blocker to full acceptance, not grounds for silently skipping or reducing the requirement.



## Known investigation areas (facts, not a preapproved implementation)

- Token-based property identity appears in SameProperty, insert member comparison, query-filter validation and preparer filter-member keys. Preserve inherited/reflected/hidden/interface-property semantics and plan-cache correctness.

- Runtime Reflection.Emit for composite many-to-many junction carriers must have an AOT-capable implementation.

- Runtime generic construction, metadata preservation/reflection and JSON serialization paths require NativeAOT analysis and runtime tests.

- Expression.Compile is not categorically unavailable: .NET uses interpretation under NativeAOT. Source generation is a possible tool, not yet a universally required redesign.

- There is a real JoinAliasGenerator alongside the empty AnonymousClassGenerator stub; do not assume the generator project is entirely inactive.

- Native compatibility of each dependency/driver must be verified, not inferred from another driver's success.



## Acceptance

1. Repro-class queries and every supported public feature/provider combination execute correctly in published NativeAOT tests using existing integration infrastructure.

2. CRUD, query translation and materialization, joins/eager loading (including composite junctions), filters/cache behavior, converters/JSON/CSV/streaming, advanced query constructs and provider-specific operations retain JIT semantics wherever currently supported.

3. Normal JIT behavior, public API compatibility and existing test suites remain intact. Any required generation/registration contract must be explicitly designed and approved before implementation; no silent API narrowing.

4. AOT/trim diagnostics are accounted for and root causes addressed. No blanket warning suppression, disabling trimming, or rooting the entire dependency graph as a substitute for compatibility.

5. Automated gates run actual native binaries and report feature/provider evidence; unsupported dependencies or unexecuted cases are not reported green.

6. Public documentation and its Russian mirror accurately describe the accepted support contract.



## Open design questions

- How to replace runtime-emitted carriers and guarantee required closed generic instantiations while preserving public behavior.

- Whether source-generated metadata/materializers/registrations are required, and how they are discovered or explicitly declared for dynamically composed queries and external assemblies.

- Exact reusable shared test-support boundary and feature/provider coverage ledger.

- Third-party blockers, especially driver and EF integration behavior; no feature exclusions have been approved.



## Status / gates

Requirements above are agreed in conversation. Architecture/design and written specification are NOT approved. No implementation plan has been prepared; product implementation is NOT authorized. Issue creation is tracking only. Design draft: `docs/superpowers/specs/2026-11-04-nativeaot-full-compatibility-design.md` (local/uncommitted; builder check in progress; not approved). Implementation plan: not prepared. Milestone uses the verified current branch's exact matching existing milestone; this is not a promise of release readiness.





## Additional agreed requirement — zero-configuration migration



Added after the requirements above were agreed; it constrains *how* AOT support may be delivered. It does not relax or replace anything above.



- Existing applications must reach full NativeAOT support by enabling `PublishAot=true` **only**. No manual listing or registration of entity, projection or query shapes; no per-type rooting configuration; no separate hand-authored AOT-specific code paths in the consuming application.

- If source generation is needed, it must be delivered automatically through the nextorm package/analyzer (discovered and wired in as part of the normal package reference) and must require no new opt-in switch and no new consumer-written code.

- Existing public API and runtime-composed query behavior must be preserved to the extent already supported; this requirement does not authorize narrowing the supported surface.

- Any feasibility conflict with this zero-configuration constraint is an explicit **blocking finding** for the cycle/design, not permission to shrink the contract. It must be recorded and escalated, not silently resolved by requiring consumer configuration.

- This answers the discovery half of the earlier open question "Whether source-generated metadata/materializers/registrations are required, and how they are discovered or explicitly declared for dynamically composed queries and external assemblies": where required, discovery must be automatic and cover dynamically composed queries and external assemblies without consumer declaration. The architectural mechanism itself remains unapproved.



All previous requirements remain in force. No architecture, specification, plan or implementation is approved by this addition.







### Entry PLAN (decision record)



- form=collection

- items: `T365 (cycle, N=4, r=1)`; `T366 (incremental, campaign C1, N=2, r=6)`

- order: `T365 → T366`

- lane_cap=1

- overlay: `coder+.NET` + `nextorm-pdca`

- paths: `.pdca/status/collection-365-366-r2.md`, `.pdca/status/T365-rc5-3.md`, `.pdca/status/T366-rc5-1.md`; evidence `.pdca/365-366-r2/items/T365/rv1/` and `.pdca/365-366-r2/items/T366/step<k>/rv1/`

- Notice: the entry plan text was not transcribed verbatim into this record; the operative item contracts are verbatim at `.pdca/365-366-r2/items/T365/rv1/plan.md` (T365) and in the T366 charter once prepared. Criteria are the issue bodies above, verbatim.



### Phase decisions



- T365 PLAN persisted (`plan_state=ready`)

- T366 charter prepared (`preparation_state=ready`)

- ALL-barrier: passed (T366 charter prepared, `preparation_state=ready`; both items prepared); collection completeness not reached — T365 incomplete, T366 whole-goal open (previously: not yet passed — T366 charter not prepared; superseded)

- Schedule: T365 → T366, single lane, current worktree/branch, no worktree, no merge; revision vector T365@r1, T366@r6

- T366 S1 boundary: accepted on its own scope; baseline `(239, 2/6, 0)`; step2 opened with durable brief `.pdca/365-366-r2/items/T366/step2/brief.md`; floor calibrated; process defect `S1-EVIDENCE-HISTORY` deferred to `S2-AC0`.



### Groups



| group | items | worktree | branch | ref | status | note |

|---|---|---|---|---|---|---|

| g1 | T365 | `/home/alex/sources/nextorm-365-366` | `collection/365-366` | `83c545767cdd46cb58d054445c3e` | in-progress | T365 terminal execution-incomplete; T366 S1/S2 accepted on charter floor, next S3; group paused (previously: T365 terminal execution-incomplete; T366 S2 next by explicit user instruction; default group-stop superseded by that explicit instruction; superseded) |



### Items



| item | group | branch | status | plan_state | preparation_state | preparation_ref | form | contract | overlay | cycle_id | status path | plan_revision | attempt_n | disposition | evidence root | campaign_status | campaign_ref |

|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|

| T365 | g1 | collection/365-366 | incomplete | ready | — | — | cycle | pdca | coder+.NET + nextorm-pdca | T365-rc5-4 (N=4) | .pdca/status/T365-rc5-4.md | r1 | 1/3 | finalized; terminal=execution (AC6 unmet; user-declared no further AC6 attempts) | .pdca/365-366-r2/items/T365/rv3/ | — | — |

| T366 | g1 | `collection/365-366` | in-progress | pending | ready | `.pdca/365-366-r2/items/T366/preparation.md@r1` | campaign | — | — | T366-rc5-1 (N=2; S1/S2/S3/S4/S6/S8 accepted; S5 blocked; S7 blocked; next S9) | `.pdca/status/T366-rc5-1.md` | r2 | 1/3 | running (S8 accepted; next S9) | `.pdca/365-366-r2/items/T366/step<k>/rv1/` | running | `.pdca/status/T366-rc5-1.md` |



### General verification and recovery



- state: paused

- last common CHECK: T365 terminal not accepted (AC6 unmet; AC1–AC5/AC7 unverified; AC8 met; expert found no criteria defect); T366 S1 accepted on its own scope as current-state baseline `(239, 2/6, 0)`; T366 S2 CHECK r6 accepted on charter floor `(238, 6/6, 0)` per `USER-ACCEPT-S2-FLOOR`; T366 S3 accepted (post-merge baseline `(239 unique / 245 raw, 6/6, 0)` on `66c8f112`) — step scope only, whole-goal not reached; T366 S4 accepted step-local (`(237 unique / 243 raw, 6/6, 0)`, Δ −2 vs S3; cluster B reverted) — step scope only, whole-goal not reached; T366 S5 blocked (cluster needs runtime-generic-construction step; S4 baseline stands); T366 S6 accepted step-local (`(234 unique / 240 raw, 6/6, 0)`, Δ −3 vs S4) — step scope only, whole-goal not reached; T366 S7 blocked (runtime factory load-bearing for typed execution; 233 unique / 239 raw, IL3050 125→124, 1 vanished, 0 new; nextorm.core.tests 3915/245 failed/rc=2; no src change) — step scope only, whole-goal not reached; T366 whole-goal not reached

- blocking reason: none

- defect history: `AC6-six-warm-All-rows` (open); `WC-1-concurrent-writer` (closed by construction); `T365-N3-D0-fabricated-constraints` (fixed in both records: false constraint line replaced with the verbatim user line and its three constraints); `S1-WARN-POLICY` (fixed); `S1-REGISTRY-BINDING` (fixed); `S1-FAILURE-ORIGIN` (fixed); `S1-CONTOUR-PROOF` (fixed); `S1-VM3-EXTENSIONS` (fixed); `S1-EVIDENCE-HISTORY` (deferred → `S2-AC0`); S2-SMOKE-CRITERION (open; spec docs/specs/design/D366-pdca-process-defects.md); S2-CHECK-EVIDENCE (open); S2-EVIDENCE-CONSISTENCY (open)

- next allowed step: T366 S9 — re-apply the `QueryCommand<TResult>` → `QueryPlanner` → `RowMapperFactory` → `RowMaterializerBuilder` DAM chain closing the 9 `IL2070` in `RowMaterializerBuilder.cs` (slice d), on the S8 accepted base `(233 unique / 239 raw, 6/6, 0)`; S8 accepted; campaign running; issue #366 OPEN



### Completeness



- 5/6 accepted for T366 steps (S1, S2, S3, S4, S6); S5 blocked; S7 blocked; T365 incomplete

- T366: running; accepted S1 `(239, 2/6, 0)`, S2 `(238, 6/6, 0)` on charter floor, S3 `(239 unique / 245 raw, 6/6, 0)` on `66c8f112`, S4 `(237 unique / 243 raw, 6/6, 0)` step-local, S6 `(234 unique / 240 raw, 6/6, 0)` step-local; S5 blocked; S7 blocked (runtime factory load-bearing for typed execution); next S8

- integrated-tree verdict: none

- issues #365 CLOSED (limited: AC6 unmet), #366 OPEN

- commits made: `7abd2e1b`, `0320ecc3`, `091fddbd`, `80d900a0` (T365 bookkeeping + cycle work); `7b75ff581ed0ba9330be062044e3ddc206191f50` (T366 S1 accepted baseline); `53e8c5a4` (finalize #365/#366: statuses) and the reconciliation follow-up commits `71564ef2` and this final pass commit (see `git log -3 --oneline`)



## Progress log



2026-11-04T17:50:50Z | PLAN | T365@r1 | T365 PLAN persisted (plan_state=ready); collection prepared | `.pdca/status/T365-rc5-3.md`

2026-11-04T18:22:22Z | T365 paused at clean boundary in DO (n=0/3); T366 not started

2026-11-04T18:24:20Z | state correction D0 applied: fabricated constraints removed from collection Input and T365 Original task | `.pdca/status/T365-rc5-3.md`; `.pdca/status/collection-365-366-r2.md`



2026-11-04T18:40:19Z | T365 CHECK not accepted (n=1/3); next allowed step DO under r2 | `.pdca/status/T365-rc5-3.md`

2026-11-04T18:51:47Z | T365 r2 evidence batch collected (AC8 diff, build, live suite, provenance, harness copy); AC6 shape work and final table still open

2026-11-04T20:37:39Z | T365 incomplete; plan-terminal: acceptance criteria cannot be determined (AC7 65/74 vs 65/75); typed terminal=criteria-undetermined; group g1 remains in-progress, T366 pending | `.pdca/status/T365-rc5-3.md`; `.pdca/365-366-r2/items/T365/rv2/plan-terminal.md`

2026-11-04T20:50:28Z | DO | T366@r1 | T366 charter prepared (`preparation_state=ready`); T366 remains pending; group g1 remains in-progress | `.pdca/365-366-r2/items/T366/preparation.md@r1`



2026-11-04T20:51:10Z | ACT | T365 finalized incomplete (plan-terminal criteria-undetermined); T366 preparation ready; next: T366 step1 baseline

2026-11-04T23:08:20Z | ACT | T366 S1 accepted; baseline (239, 2/6, 0); step2 opened with durable brief; evidence `.pdca/365-366-r2/items/T366/step1/rv1/`

ACT bookkeeping commit 6bead8227f695de02c53ebafef1ad9c90219dc74 (status record only); tip = 6bead8227f695de02c53ebafef1ad9c90219dc74; no push, no merge

2026-11-05T17:59:32Z | T365 re-entry by explicit user input «п1»: AC7 = 85/75 и 65/75 unchanged; prior terminal=criteria-undetermined superseded; new cycle N=4 (r1, n=1/3), status T365-rc5-4.md | .pdca/status/T365-rc5-4.md



2026-11-06T00:00:00Z | T365 ACT closed incomplete/STOP (user authority boundary; AC6 unmet; AC1–AC5/AC7 unverified; AC8 met); T366 S2 next per explicit user instruction; issue #365 OPEN



2026-11-06T06:44:59Z | ACT | Collection paused: T365 issue #365 CLOSED limited (AC6 unmet); #366 OPEN. T366 accepted increments S1 (239, 2/6, 0), S2 (238, 6/6, 0); residual 238 unique diagnostics (244 raw) + unmeasured provider/EF/JSON-CSV matrix; next S3. | `.pdca/status/T366-rc5-1.md`



2026-11-06T06:51:35Z | finalize third pass: historical CHECK r4/r5 markers; container items/ALL-barrier/schedule/group note reconciled; finalize commit recorded in commits-made above | `.pdca/status/T366-rc5-1.md`; `.pdca/status/collection-365-366-r2.md`

## Git state



- `.pdca/status/collection-365-366-r2.md` — modified in this cycle; tracked; status only.

- `.pdca/status/T366-rc5-1.md` — modified in this cycle; tracked; status only.


2026-11-06T12:00:00Z | ACT | T366 S3 accepted post-merge baseline (239 unique / 245 raw, 6/6, 0) on 66c8f112; next S4 | `.pdca/status/T366-rc5-1.md`
2026-11-06T11:23:07Z | ACT | T366 S4 | accepted −2 (237 unique / 243 raw, 6/6, 0); next S5 | `.pdca/status/T366-rc5-1.md`
2026-11-06T16:27:05Z | ACT | T366 S5 | blocked (cluster needs runtime-generic-construction step); baseline S4 stands; next S6 | `.pdca/status/T366-rc5-1.md`
2026-11-06T18:41:25Z | ACT | T366 S6 | accepted (234/240, 6/6, 0); committed; next S7 | `.pdca/status/T366-rc5-1.md`
2026-11-06T19:24:44Z | ACT | T366 S7 | blocked: runtime factory load-bearing for typed execution; S8 must add untyped materializer | `.pdca/status/T366-rc5-1.md`
2026-11-06T21:20:15Z | CHECK | T366 S8 | accepted step-local: 233 unique / 239 raw, 6/6 SQLite native, JIT 0; −1/−1 vs S6; 0 new tuples | `.pdca/365-366-r2/items/T366/step8/rv1/`
2026-11-06T21:20:15Z | ACT | T366 S8 | accepted (233/239, 6/6, 0); committed; next S9 | `.pdca/status/T366-rc5-1.md`
