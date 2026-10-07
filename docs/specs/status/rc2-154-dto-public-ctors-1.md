# D154 — Public eager constructors for extreme-row DTOs

- task_id: D154
- issue: #154
- selected_variant: pdca-dotnet
- cycle_id: 1
- plan_revision: r=1
- evidence_contract_revision: rv=1
- plan_state: ready
- collection: 1.0.9-rc2 (COLLECTION TASK PLAN, phase P; stopped at PLAN→DO boundary)
- status-file: docs/specs/status/rc2-154-dto-public-ctors-1.md

## Plan (r=1) — heading

### Goal and boundaries
Enable a .NET assembly **without `InternalsVisibleTo`** to construct `ExtremeRowRenderColumn`, `ExtremeRowDescription`, `ExtremeRowRenderRequest` through public eager constructors and use them with its own `IExtremeRowRenderer`.

In scope: public accessibility of the three existing eager constructors; immediate structural validation of public inputs; borrowed collection identity preserved (no copy); internal lazy payload/core-preparation preserved (deferred payload evaluation); a constructor-only trusted internal path where needed so core preparation avoids public collection scans; external-consumer tests; lazy/value-type regressions; renderer-generation regressions; constructor XML docs; EN/RU docs; API register update.

Out of scope: public factories, config switches, `Func`/`Lazy` overloads, setters, extra public APIs; SQL algorithm changes; provider capability changes; SQL-syntax/security/provider-type validation; defensive copies/immutability; cross-list cardinality/disjointness rules; equality/hashing changes; #155 work; commits/merges/pushes.

### Minimal solution
1. Make the existing eager constructor signatures public.
2. Validate public inputs immediately; do not read `ExtremeRowDescription.Payload` to validate its eager argument.
3. Keep the existing internal factory/lazy description constructor unchanged.
4. Keep column checks constant-time; for the request/description public collection scans retain a trusted internal construction path (an internal overload distinguished by an internal construction marker, not a public switch/factory) and route core construction through it.
5. Leave assignments and collection references unchanged.

### Acceptance criteria
- AC-01 Public/external construction: a non-friend test assembly compiles and calls all three eager constructors + a custom renderer; reflection verifies ctor accessibility and absence of new public lazy signatures.
- AC-02 Required references: each required reference independently null throws immediate `ArgumentNullException` with correct `ParamName`.
- AC-03 Collection contents: null element in each applicable collection throws immediate `ArgumentException` identifying parameter and index (at construction, not render/`Payload`).
- AC-04 SQL/aliases/enum: null `SourceSql` → `ArgumentNullException`; empty/whitespace `SourceSql`, empty alias, undeclared enum value → immediate `ArgumentException`; whitespace-only alias is ACCEPTED (rule is non-empty, not non-whitespace).
- AC-05 Shapes without invented rules: global / grouped / composite-overlap / empty shapes construct; both `IsMax` values; no minimum counts, no cross-list cardinality, no alias disjointness, no SQL parse, no provider-type restriction.
- AC-06 Borrowing: `ReferenceEquals` observes the original lists through properties, including eager description payload; no copy/wrapper.
- AC-07 Lazy preservation: internal construction/preparation does not invoke the payload factory; first `Payload` access forces once, repeated access reuses result. (`Payload => _payload!.Value` at `DialectCapabilities.cs:287` is intentionally forcing when accessed; non-forcing applies to construction/validation/preparation only.)
- AC-08 Capability independence: a valid hand-constructed DTO may have `CanRender == false`; constructor validation never calls `CanRender`/`Render`.
- AC-09 Core regressions: existing renderer-generation, dialect guard, in-memory, payload-laziness, cache-stability, and relevant provider integration tests pass; whole-entity value-type and reference-type paths exercised.
- AC-10 Docs/build: warning-clean .NET 10 build; ctor XML docs and EN/RU contract agree; API register records #154; no public docs link `docs/specs/**`.

### Variant matrix (execution path)
External column → description → `CanRender`; external request → `Render`; core preparation separately builds columns, a lazy description, a request. T=test, G=guard. No required branch deferred.
- Column in global/grouped/composite/empty fixtures — T.
- Column reference-type vs value-type `ClrType` — T.
- Column 3 boolean props, all 8 combos × 2 CLR categories — T.
- Description global/groups/composite/all-empty eager lists, both `IsMax` — T.
- Request global/groups/composite-overlap/all-empty, both `IsMax`, every declared `KeywordCase` — T.
- Public eager description payload validation at ctor return — T+G.
- Internal factory/lazy description ctor: no factory call at construction — T.
- Non-forcing capability/preparation (factory counter + throwing factory) — T.
- Actual `Payload` access forces once, reuses — T.
- Each borrowed collection property identity (array + mutable list where signature permits) — T.
- Null required refs, one at a time — T+G.
- Null collection element, each collection, first/later positions — T+G.
- `SourceSql` null/empty/whitespace/ordinary — T+G.
- Aliases null element/empty/whitespace-only/ordinary — T+G.
- Valid overlap across lists — T.
- Empty/independently sized lists — T.
- `KeywordCase` every declared value/undefined numeric — T+G.
- Valid DTO rejected by custom `CanRender`; supported DTO passed to custom `Render` — T.
- Whole-entity reference-type vs value-type core path (both min/max directions, provider payload prep) — T.
- PostgreSQL/ClickHouse rendering + plan-cache paths — T (existing suites + integration).
- Later/concurrent mutation of borrowed lists — G (caller contract, documented prohibited; construction validation does not prevent it).

### Footprint (files/globs; uncertainty flagged)
- `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs` — ctors `:210`, `:254`, `:325`; keep internal lazy ctor `:263`; add guards + XML docs. [certain]
- `src/nextorm.core/SqlBuilder.cs:771` — route `ExtremeRowRenderRequest` construction through the trusted internal ctor; keep description factory `:867/871` and payload prep `:897` unchanged; column sites `:887/914` unchanged except constant-time validation. [certain under selected approach]
- `tests/nextorm.mariadb.tests/ExtremeRowPublicConstructionTests.cs` — NEW external (no-IVT) contract suite. [certain path; confirm actual assembly name/runner before writing]
- `tests/nextorm.core.tests/ExtremeRowDescriptionLazinessTests.cs` — extend factory-counter/throwing-factory; add whole-entity value-type boundary coverage. [certain file; exact new test names uncertain]
- `tests/nextorm.postgres.tests/**/ExtremeRowPayloadLazinessTests.cs`, `tests/nextorm.clickhouse.tests/**/ExtremeRowPayloadLazinessTests.cs` — extend lazy/value-type coverage. [glob; exact dir resolution uncertain]
- Docs: `docs/advanced/select-where-extrema-native.md:106–114`, `docs/ru/advanced/select-where-extrema-native.md:110–112`, `docs/advanced/api-reference.md:109–110`, `docs/ru/advanced/api-reference.md` (mirror). [certain]
- `docs/specs/design/API-NAMING-REVIEW.md:5567–5576` — record #154. [certain]
- `docs/specs/design/code-smells-review.md` — NOT changed (no #154 entry). [certain]
- `tests/nextorm.mariadb.tests/*.csproj` — conditional write ONLY if a reference is insufficient; no package/IVT addition authorized. [uncertain]
- Solution/CPM/build settings — not changed. PublicAPI files/ApiCompat/PublicApiAnalyzers — none exist; not introduced.

### Predecessor-result requirements
- Base commit `18659e41` is an ancestor of working HEAD; record HEAD + clean/dirty state; do not reset/overwrite.
- #144 native extreme-row merged (construction sites `SqlBuilder.cs:867/771/887/914` present). Provenance `docs/specs/status/native-extreme-row-144-1.md:298`.
- #155 must not have changed DTO laziness; if it did, that is a prerequisite needing PLAN review, not a revert.
- No dependency on any other D### task; single unit.

### Assumptions / prerequisites
- Approved design is authoritative (not a provisional assumption).
- External host = `nextorm.mariadb.tests` (references core without IVT); confirm assembly name/runner.
- No PublicAPI/ApiCompat artifacts to update; only the existing register.
- CS1591 enforced (GenerateDocumentationFile + TreatWarningsAsErrors; not in NoWarn) ⇒ public ctor needs XML docs.
- D154.0 prerequisite: bounded Roslyn lookup of exact parameter names, enum members, host metadata, value-type fixture pattern; and attach the loaded conventions below.
- Borrowed semantics accepted.

### Loaded conventions attached at PLAN (orchestrator)
- Contract: `pdca-dotnet` PLAN/DO/CHECK/ACT gates + "Versioned evidence contract" / "CHECK completeness gate — mandatory evidence contract" (this file uses rv=1).
- nextorm overlay invariants (`nextorm-pdca` §PLAN, 1–10): (1) abstraction only on a 2nd consumer/observed seam; (2) KISS = number of concepts; (3) measure, never assert (<~20% = noise); (4) don't reopen settled findings; (5) public API rename/remove touches `docs/**` and `docs/ru/**`; (6) build discipline (TreatWarningsAsErrors, nullable, CRLF, CPM); (7) known architecture facts; (8) nothing leaves the current milestone; (9) PLAN owns plan quality (criteria + negative case + variant matrix); (10) search ignore-aware, C# symbols via `roslyn`.
- Class-priority: this change is closest to "public API surface / DTO" — P1 rows = external accessibility, validation, borrowed identity, lazy preservation.

### Test strategy
Commands (run from `/home/alex/sources/nextorm`; logs/exit codes under `/tmp/nextorm-d154-r1/`):
- P0: `git rev-parse HEAD`; `git status --short`; `git merge-base --is-ancestor 18659e41 HEAD`.
- B (build): `dotnet build -c Debug`.
- E (external suite): `dotnet test tests/nextorm.mariadb.tests -c Debug`. Pre-change: `dotnet build tests/nextorm.mariadb.tests -c Debug` must exit nonzero with ctor accessibility diagnostics (externality proof).
- C (core): `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~ExtremeRowDescriptionLazinessTests|FullyQualifiedName~ExtremeRowDialectGuardTests|FullyQualifiedName~InMemoryExtremeRowTests"`.
- P (postgres): `dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~ExtremeRowNativeSqlGenerationTests|FullyQualifiedName~ExtremeRowPayloadLazinessTests|FullyQualifiedName~ExtremeRowNativeCacheStabilityTests"`.
- H (clickhouse): same filter on `tests/nextorm.clickhouse.tests`.
- I (integration): `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~PostgresExtremeRowNativeSpecificTests|FullyQualifiedName~ClickHouseExtremeRowNativeSpecificTests"`.
- A (boundary sweep + coverage): `DOCKER_HOST=... dotnet-coverage collect "dotnet test -c Debug" -s coverage.settings.xml -f xml -o /tmp/nextorm-d154-r1/coverage.xml`; then `reportgenerator "-reports:/tmp/nextorm-d154-r1/coverage.xml" "-targetdir:/tmp/nextorm-d154-r1/coverage-report" "-reporttypes:TextSummary;Cobertura"`.
- X (docs): `dotnet docfx docs/docfx.json`.

Unit vs integration: validation/ownership/externality/`CanRender` independence are unit/compile-contract; SQL-generation+lazy+cache are DB-free provider tests; container-backed PostgreSQL+ClickHouse integration IS required (core request construction touched) and providers must not be skipped (load `.opencode/skills/running-integration-tests/SKILL.md`; a skipped-provider run is not green). Coverage: line ≥85 / branch ≥75 (hard on main; record actual numbers + warning policy elsewhere). Stryker: applicable to null/empty/enum guards but no installed setup supplied ⇒ not a new prerequisite; the negative matrix must fail if a guard is removed/weakened (verified in boundary review). Boundary sweep point = step A before DO→CHECK. Inner loop = filtered affected tests only; full-project/solution in the inner loop is rejected.

### Docs plan
Update EN+RU native-extrema pages + API-reference pages (listed in footprint), add XML docs for all three public eager ctors, update `API-NAMING-REVIEW.md:5567–5576`. Document: public eager construction; borrowed lists (no copy/no immutability/no mutation-while-used incl. concurrent); validation only at construction; exact validation scope and no SQL/provider/security validation; empty shapes and overlapping aliases allowed; `CanRender` is a separate decision; internal lazy prep is not new public API. Do not edit generated site files; no public→specs links; preserve CRLF.

### Performance-measurement decision: NOT required
Public scans are construction-time, not per-row rendering; core keeps factory/lazy at `SqlBuilder.cs:867/871`; core request construction `SqlBuilder.cs:771` uses the trusted internal path; column construction `SqlBuilder.cs:887/914` gains only constant-time reference checks. No BDN baseline/throughput claim justified; tests prove non-forcing/constructibility only. If the trusted path cannot be retained without materially different hot-path work → return to PLAN.

### Reconnaissance decision: no prototype
API/ownership design is settled; a bounded Roslyn prerequisite lookup (exact parameter names, enum members, host metadata, value-type fixture) is a D154.0 fact-gathering step, not a design prototype.

### Unit execution mode
One unit, sequentially in the CURRENT tree (no worktree/branch). Production ctors + core call site + contract tests + docs share one contract.

### Design checklist
SOLID/responsibility: DTOs enforce structural boundaries only; renderer owns capability/render. DRY: share validation only where it reduces duplication without forcing payload/public helpers. Type design: sealed records, getter-only props preserved. Ownership: borrowed references retained, lifetime documented. Laziness/performance: internal factory preserved, no payload read in guards. Cache invariant: no shared `QueryCommand` mutation, no `Cache=false`; cache regressions run. Provider/type rules: none added. Toolchain/API: .NET 10, warnings-as-errors, XML docs, no new public lazy surface. Repo mechanics: Roslyn-only symbols, CRLF, CPM unchanged, no commit/push/merge. Scope/docs: EN+RU synced, no specs links, #155 preserved.

### Priority matrix
P1 by requirement: external accessibility, ref/element validation, source/alias/enum rules, borrowed identity, lazy preservation, capability independence, no extra public API. P1 by execution path: core trusted construction, whole-entity value-type branch, provider payload prep, SQL-generation/cache regressions. P1 by invariant: warning-clean XML-documented public API; preserve #155-related work. P2 supporting: docs presentation + register classification (register's RG-1 P2 BC is register metadata only; does not lower acceptance).

### Deferred findings
- #155 laziness redesign — deferred to #155 (trigger: its separately approved implementation).
- SQL/provider/security validation — out of scope (not an incomplete #154 fix).
- Defensive copying/immutable ownership — out of scope (new ownership change only).
- Broad mutation infrastructure — deferred (trigger: repo mutation tooling becomes available or a follow-up requires it).

## Evidence contract (rv=1)
All rows are planned evidence until produced. Each row requires: command exit code + full log; actual executed tests bound to scenarios; actual source/line refs post-implementation; result counts/assertions; artifact paths + tested HEAD/worktree identity. Zero selected tests, missing scenarios, skipped required providers, or missing logs do NOT satisfy a row. Command aliases = §Test strategy.

- EV-154-01 / AC-01: external direct construction of all 3 DTOs — pre-change `dotnet build tests/nextorm.mariadb.tests -c Debug` nonzero with ctor accessibility failures; final E exit 0 with executed external scenarios. Owner: external-test stream; CHECK verifies.
- EV-154-02 / AC-01: public eager signatures only, no public factory/lazy overload — E, B exit 0; 3 expected public ctors present; surface assertion passes.
- EV-154-03 / AC-02: every required reference param — E exit 0; each null case immediate exact `ArgumentNullException` with correct ParamName/message.
- EV-154-04 / AC-03: every collection param with reference elements — E exit 0; null element first/later immediate exact `ArgumentException` with param/index.
- EV-154-05 / AC-04: SourceSql + all alias lists + enum boundary — E exit 0; null/empty/whitespace/source and alias distinctions; all declared enum values accepted; undeclared rejected.
- EV-154-06 / AC-05: four shapes through each DTO execution role — E exit 0; all 12 DTO/shape cells; description/request both IsMax; request covers declared enum values.
- EV-154-07 / AC-06: every borrowed collection incl. eager description payload — E exit 0; identity assertions true; no mutation-during-use test.
- EV-154-08 / AC-07: internal lazy construction/preparation — C, P, H exit 0; factory count 0 before access, 1 after first/repeated; throwing factory not reached early.
- EV-154-09 / AC-08: valid DTO with false CanRender + direct supported render — E exit 0; rejection independent of construction; supported renderer output asserted; no renderer calls in ctor.
- EV-154-10 / AC-09: whole-entity ref/value types + min/max directions — C, P, H exit 0; each required branch executed, not merely compiled.
- EV-154-11 / AC-09: existing generation/guards/cache/in-memory paths — C, P, H exit 0; every selected class executes tests; no new unexpected skips.
- EV-154-12 / AC-09: real PostgreSQL/ClickHouse (core construction changed) — I exit 0; both provider-specific classes execute; neither required provider skipped.
- EV-154-13 / AC-10: build + docs — B, X exit 0; no build warnings; no broken doc links; ctor docs present; EN/RU/XML/register diff.
- EV-154-14 / AC-10: full boundary sweep + coverage — A exit 0; record line/branch %; enforce 85/75 on main, record warning policy elsewhere; required providers not skipped.
- EV-154-15 / predecessor + invariants: P0 ancestor exit 0; no unrelated overwrite; conventions attached; core route remains non-forcing/trusted; reviewed diff.

CHECK re-gather budget: owner `check` → read-only `scout`; ≤2 targeted requests per CHECK attempt, each ≤3 existing logs/artifacts or one narrowly scoped fact. Missing reports consume budget; do not alone justify a contract revision or DO iteration. Persistently low confidence → `escalate` trigger 5. A justified revision supersedes rv=1 explicitly, retains these row IDs, adds new ones.

## Gate / confidence / risks
Gate 1: plan + evidence contract recorded (this file); D154.0 supplies prerequisite facts. plan_state=ready; stopped at PLAN→DO (collection P phase). Confidence high in bounded implementation + test host; exact signature spelling, enum values, host runner details, value-type fixture mechanics need D154.0 evidence.

Principal risks: validating eager description through its forcing getter; stronger alias/list restrictions than approved; losing list identity; routing core request construction through public scans; proving accessibility in a friend assembly; treating zero-test/skipped-provider runs as passing; overwriting #155/pre-existing changes.

## Progress log
- PLAN written (r=1, rv=1). No code, no worktree/branch (collection phase P). plan_state=ready.
- 2026-10-07T14:59:29Z | DO | revision r=1 | iteration n=1/3 | DO started: 3 eager ctors public + construction-time validation + internal ExtremeRowTrustedConstruction; external non-friend suite; core/PG/CH lazy + value-type tests; EN/RU docs + API register | src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs; src/nextorm.core/DataContext/SqlBuilder.cs; tests/nextorm.mariadb.tests/ExtremeRowPublicConstructionTests.cs
- 2026-10-07T14:59:29Z | DO | revision r=1 | iteration n=1/3 | pre-change externality red proof: mariadb tests build exit 1, 13x CS1729 ctor-accessibility | TestResults/rc2-154-1/prechange-mariadb-build.log
- 2026-10-07T14:59:29Z | DO | revision r=1 | iteration n=1/3 | green evidence: solution build exit 0 (0 warn/0 err); external mariadb 218/0 skipped; core 7+2+30=39; postgres 30+4+3=37; clickhouse 52+4+3=59; PG integration 16 skipped 0; CH integration 35 skipped 0 | TestResults/rc2-154-1/{build,external-tests,core-*,postgres-*,clickhouse-*,integration-*}.log
- 2026-10-07T14:59:29Z | DO | revision r=1 | iteration n=1/3 | coverage nextorm.core line 65.2% / branch 59.4% (the -f xml format carries no branch data; recollected with -f cobertura matching CI); thresholds 85/75 hard on main only, warn on 1.0.9-rc2 | TestResults/rc2-154-1/coverage-report/Summary.txt; coverage-*.log
- 2026-10-07T14:59:29Z | DO | revision r=1 | iteration n=1/3 | gates: inner-loop brief exit 0 / report exit 0; git diff --check exit 0; link guard exit 1 = no docs/specs links in the four public pages; docfx 0 errors / 2 pre-existing warnings | TestResults/rc2-154-1/validator-report-exit.txt; diff-check.log; link-guard.log; docfx.log
- 2026-10-07T14:59:29Z | DO | revision r=1 | iteration n=1/3 | DO complete; ready for CHECK | TestResults/rc2-154-1/ (evidence.json, scope.json, task-diff.patch, coverage-report/)
- 2026-10-07T15:02:48Z | DO | revision r=1 | iteration n=1/3 | stale-remarks fix: type-level remarks of ExtremeRowRenderColumn/ExtremeRowDescription/ExtremeRowRenderRequest updated to agree with the public eager ctors (external construction without InternalsVisibleTo; construction-time validation; no SQL/provider/security/cardinality validation; Payload never read; CanRender/Render not called; internal lazy factory remains non-public) | src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs; TestResults/rc2-154-1/{build,external-tests}.log; TestResults/rc2-154-1/task-diff.patch
- 2026-10-07T20:11Z | CHECK | revision r=1 | iteration n=1/3 | CHECK r=1 n=1 PASS (AC-01..AC-10 met; EV-154-01..15 closed) | TestResults/rc2-154-1/evidence.json
- 2026-10-07T20:11Z | ACT | revision r=1 | iteration n=1/3 | ACT D154: CHECK passed, committing the D154 footprint to 1.0.9-rc2 (autocommit lane; no push) | git
