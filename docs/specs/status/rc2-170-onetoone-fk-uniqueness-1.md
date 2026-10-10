## Collection rc2-final — own cycle (current tree HEAD a956598a; plan base 9a2a2871)

- task: T170 / #170; selected_variant: `pdca-dotnet`; cycle_id: N=1; plan_revision: r=1; attempt: n=2/3
- baseline: current tree HEAD `a956598a`; plan base `9a2a2871`; evidence contract: rv1 **pinned** at `artifacts/pdca/D170/rv1/manifest.json` (9/9 required rows `met`)
- plan_state: **done / verified**; CHECK r=1 verdict **PASS — rv1 9/9 rows met** (r=1)
- issue: #170 → **closed** (verified URL https://github.com/AlexeyShirshov/nextorm/issues/170)
- next step: none
- new P: `P170-oracle-first`

---
# T170 — OneToOne FK uniqueness: documented trust and verifiable runtime boundary
Collection `rc2-final`; issue #170; `pdca-dotnet`; N=1; r=1; n=2/3; evidence rv1 pinned.
Status `docs/specs/status/rc2-170-onetoone-fk-uniqueness-1.md`. DO executed stages 1–4b; rv1 pinned at `artifacts/pdca/D170/rv1/manifest.json` (9/9 rows `met`); the records inconsistency (`REQ170-RECORDS`) was reconciled (n=2/3) and re-CHECK passed at r=1 (rv1 9/9 rows met).
Historical r3 STOP remains terminal; its patch is reference material, not accepted evidence or an implementation baseline. Everything from `# D170 — Navigation: validate FK uniqueness for OneToOne` below is **historical** (prior terminal r3 STOP record), retained verbatim — not current state.
**New P: `P170-oracle-first`** — replace aggregate "coverage exists" claims with independently constructed shared-FK fixtures and an exhaustive, case-mapped runtime oracle.

## Goal and acceptance criteria
Deliver option **(b)**: explicitly settle declaration-time trust, document its limits, and prove the existing runtime backstop. **Do not claim metadata-build-time or database-schema uniqueness validation.**
- **AC1 / R170-01 — Trust:** metadata construction accepts a valid OneToOne declaration with either unique or non-unique FK data; it neither inspects schema/data nor introduces uniqueness constraints. Negative: non-unique FK must not cause a new metadata uniqueness exception.
- **AC2 / R170-02 — Independence:** separately declared navigations sharing an FK remain separate declarations; OneToOne does not globally make that FK or another relationship unique. Negative: an explicitly non-OneToOne relationship sharing that FK must still expose its permitted multiplicity.
- **AC3 / R170-03 — Runtime:** within one parent, two distinct non-null child identities cause `InvalidOperationException` with the existing "more than one distinct" diagnostic, on both sync and async consumption. Negative: silently selecting one distinct child, throwing before loading, or checking identities globally across parents fails.
- **AC3 positive boundary:** one child, no child, and repeated rows of the same child identity load correctly; keyless/null-identity cases carry no uniqueness guarantee. Negative: valid loads throwing, missing expected children, or claiming keyless duplicate detection fails.
- **AC4 / R170-04 — Providers:** SQLite, PostgreSQL, SQL Server, MySQL and ClickHouse each execute the required sync/async positive and negative cases. Negative: skipped providers, SQLite-only runs or inherited-suite assumptions for ClickHouse fail.
- **AC5 / R170-05 — Records:** EN/RU docs, roadmap and issue/status disposition consistently distinguish trust from runtime checking. Negative: leaving the open item unexplained, asserting schema validation, or citing historical STOP evidence as current completion fails.

## Minimal solution and alternatives
Purpose = make the uniqueness boundary unambiguous; constraints = no new metadata API, schema I/O or hot-path behavior; optimum = documentation plus failure-first tests of existing behavior.
| Approach | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Metadata uniqueness guard | New build-time rejection | No agreed uniqueness source/AC; requires new policy/API or schema access | Not selected |
| Documented trust + closed runtime oracle | Supported by current implementation; bounded, testable | Must distinguish known identities from actual FK/schema uniqueness | **Selected** |
| Optional configured uniqueness certificate | Potential future opt-in guard | New public contract; a declaration is not proof of database uniqueness | Deferred: explicit opt-in feature request and agreed certificate semantics |

Not a replay: independent shared-FK fixtures, multiplicity controls, provider×mode×outcome mapping and prerequisite oracle review become mandatory dependencies before documentation closure.

## What the request did not specify
- No metadata-build validation AC exists: define AC1–AC5 above from supplied production behavior; option (b) is explicitly authorized by this task.
- Historical status said §10 resolved, but current roadmap remains open: **current tree is authoritative**. Rewrite the item's disposition explicitly.
- Exact supported independent-declaration configuration and actual test symbols: targeted scout prerequisite.
- Global evidence-contract text, integration recovery instructions, CI coverage commands and verified issue URL: scout obtains them before execution.
- If the independent shared-FK fixture cannot be expressed through supported APIs, return to PLAN with observations; do not weaken AC2.

## Tasks, footprint and classification
All tasks **active/planned, fix now**.
- **D170-0 — Prerequisite scout:** verify independent declarations, identity boundaries and both execution paths; inspect global `pdca-dotnet` contract, integration skill, CI workflow and issue URL. Sources `EntityMetadataBuilder.cs:637-656,971-983`; `JoinIntoSpec.cs:518-545,652`; supplied test locations.
- **D170-1 — Oracle fixtures:** add explicit shared-FK configurations and controlled data; each negative fixture independently proves the offending rows exist before exercising OneToOne. `RelationshipMetadataTests.cs:253,555-581`; `JoinIntoOneToOneTests.cs:11`.
- **D170-2 — Core matrix:** separately identifiable sync/async outcomes, identity variants and positive controls; bind real test IDs to case IDs. `JoinIntoOneToOneTests.cs:11`; `JoinIntoRejectionTests.cs:176`.
- **D170-3 — Provider matrix:** extend `CommonTestSuite.JoinInto.cs:25,229,247,261,341` for its four providers; create ClickHouse-owned cases in planned `ClickHouseJoinIntoOneToOneTests.cs`. Do not count inheritance as ClickHouse coverage.
- **D170-4 — Contract documentation:** clarify XML docs only at `RelationshipKind.cs:25-29`, `EntityMetadataBuilder.cs:625-627`, relevant `EntityBuilder.cs:978,996` and `JoinIntoSpec.cs:518-545`; update public EN/RU navigation guides and `implicit-navigation-queries.md` mirrors.
- **D170-5 — Records and verification:** reconcile roadmap `todo_navigation_properties.md:56,259-283,324` and §10; execute commands, populate evidence, update issue/status with verified links.
Dependencies D0→D1→D2→D3→D4→D5. Docs cannot claim closure before oracle results.
Deferred: schema/index/DDL inspection and opt-in metadata rejection; trigger = separately approved feature.
Footprint: core tests, shared integration tests, one planned ClickHouse test file, XML comments, EN/RU guides, roadmap/status and artifacts; **no functional production edits anticipated**.
Classification: actionable scope decision + in-cycle evidence prerequisites.

## Test strategy and CLOSED variant matrix
Unit tests for metadata/identity semantics; integration tests for executed loads. Case IDs are stable scenario IDs, not invented test symbols; the manifest binds them to actual test IDs before CHECK.
| Case ID / variant | Disposition and explicit oracle |
|---|---|
| `UQ` — OneToOne on genuinely unique FK | test: metadata succeeds; controlled unique FK values; exact graph loads. Enforceable UNIQUE fixture on SQLite/PG/SQLServer/MySQL; ClickHouse uses independently verified unique data. |
| `NQ` — OneToOne on non-unique FK | test: unconstrained fixture with two distinct child identities for one FK; metadata succeeds, runtime rejects. Runtime negative, not metadata rejection. |
| `SH1` — independent OneToOne + non-OneToOne, same mapped FK | test: distinct explicit declarations, no inferred inverse; metadata preserves both; non-OneToOne control returns repeated-FK multiplicity. |
| `SH2` — two independently declared OneToOne navigations, same FK | test: both declarations survive separately without a schema constraint; each violation checked per parent/navigation. |
| `DS`/`DA` — distinct child identities, sync/async | test: fully consume; exact runtime exception and diagnostic; two-parent control rules out global identity comparison; same outcome both modes. |
| `RS`/`RA` — repeated same identity | test: sync/async do not throw; one reference with expected identity. |
| `KS`/`KA` — keyless/null identity | test core both modes: no promised rejection; null plus one known identity not misreported. |
| `ZS`/`ZA` — null/default | test core both modes: empty child input leaves navigation null; null not a distinct identity; zero-valued non-null key remains real. guard: no schema-null uniqueness promise. |
| `PS`/`PA` — positive load | test: one-child, no-child, two-parent fixtures; assert parent/child identities and payload, not only row counts. |
| `IV`/`IR` — value/reference identity | test core both modes: integer identities incl. zero, non-null string identities. |
| `PV` — provider/execution cross-product | test: `{SQLite,PG,SQLServer,MySQL,ClickHouse} × {sync,async} × {distinct rejection,positive load,repeated identity}` = **30 individually mapped cases**, incl. ClickHouse-owned tests. |

## Exact execution selectors and evidence requirements
- **B:** `dotnet build -c Debug`
- **U:** `dotnet test tests/nextorm.core.tests -c Debug`
- **Q:** `dotnet test tests/nextorm.sqlite.tests -c Debug && dotnet test tests/nextorm.postgres.tests -c Debug`
- **I:** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`
- **C:** `DOCKER_HOST=... dotnet-coverage collect "dotnet test -c Debug" --settings coverage.settings.xml -f cobertura -o artifacts/pdca/D170/rv1/coverage.cobertura.xml`
- **CR:** `reportgenerator -reports:artifacts/pdca/D170/rv1/coverage.cobertura.xml -targetdir:artifacts/pdca/D170/rv1/coverage-report -reporttypes:TextSummary`
- **DOC:** `dotnet docfx docs/docfx.json`; **DIFF:** `git diff --check`
Each requires exit 0 and retained stdout/stderr; test commands require executed, passing mapped tests, not empty selections. Load the integration skill first; missing socket requires start/wait/recheck recovery, not skipping containers. Record all five providers. Coverage thresholds line **85%**, branch **75%**, main-hard-fail/other-branch-warning.

## Versioned evidence contract — rv1
Pin **before DO** at `artifacts/pdca/D170/rv1/manifest.json`; status links it. All sources below are **planned**. Common predicate: "T170 execution on the recorded base plus recorded working-tree diff"; all rows unconditional within the selected scope. Common artifacts `artifacts/pdca/D170/rv1/{row-id}/` with logs, actual test-ID→case-ID map, result/oracle references.
Rows: `REQ170-TRUST`/R170-01 (`UQ,NQ`); `REQ170-INDEPENDENCE`/R170-02 (`SH1,SH2`); `REQ170-RUNTIME`/R170-03 aggregate (every required runtime case mapped, no aggregate-only substitute); `REQ170-RUNTIME`/R170-03-S (sync `DS,RS,KS,ZS,PS` + `IV/IR` + provider sync cells); `REQ170-RUNTIME`/R170-03-A (async `DA,RA,KA,ZA,PA` + `IV/IR` + provider async cells); `REQ170-PROVIDERS`/R170-04 (all 30 `PV` cells; five providers execute; ClickHouse-owned); `REQ170-RECORDS`/R170-05 (AC5; roadmap contradiction resolved; verified issue URL; EN/RU parity); `REQ170-REGRESSION`/R170-06 (build, core and SQL-gen regressions); `REQ170-COVERAGE`/R170-07 (line/branch + policy). All P1 by acceptance invariants. CHECK owns re-gather: at most two targeted evidence retrievals and one rerun of an already required command total. rv1 belongs to this new cycle; historical STOP/contracts remain archived.

## Execution, design, docs, performance and risks
**Mode:** sequential units in the current worktree; shared core/integration fixtures and docs overlap. No worktree, commits, merges or pushes.
**Design checklist:** declaration ≠ uniqueness proof; per-parent identity scope; sync/async parity; independent relationship registration; keyless limitation; no new public API, cache mutation, provider defaults or runtime algorithm; CRLF; warnings-as-errors.
**Docs/registers:** EN/RU articles and XML comments touched; generated API/site not hand-edited. Roadmap records "resolved as documented trust with runtime boundary", schema validation explicitly outside this deliverable; no public links to internal specs.
**Performance:** no benchmark required: only tests/docs/XML comments change. Metadata construction is one-time (`EntityMetadataBuilder.cs:637-656`); per-load code (`JoinIntoSpec.cs:518-545`) functionally unchanged. Any functional hot-path edit returns to PLAN.
**Reconnaissance:** required, bounded to D0. Observable success = supported explicit shared-FK fixtures, identified sync/async APIs/test IDs, confirmed null/keyless boundary and loaded execution/contract instructions.
**Priority:** all contract rows P1. CHECK cannot downgrade.
**Risks:** repeating r1–r3 via indirect tests/unbound positive cases; accidental inferred-inverse substitution; ClickHouse fixture collapsing duplicates; false uniqueness claims; missing provider logs.
**Confidence:** high in the trust/runtime boundary; unverified in exact fixture/API expressibility, actual symbols and environment readiness. D0 resolves these.
Predecessors: restored branch and preserved STOP patch; no dependency on accepting that patch. Three failed CHECKs in one revision require escalation, not a fourth attempt.
Refs: #170 (verified URL to be gathered); `todo_navigation_properties.md:56,259-283,324`; supplied production/test locations; `docs/specs/status/rc2-170-evidence/D170-STOP-incomplete.patch`.
**Handoff:** coder persists status and rv1 manifest with planned sources now; retain HOLD. Later DO starts with D0, then oracle-first fixtures; closure requires every P1 row and a CHECK verdict.
---

> **HISTORICAL — prior terminal r3 STOP record (retained verbatim; not current state, not accepted evidence).**
> Everything in this D170 record down to and including the `2026-10-08T16:25:37Z | STOP | r3` journal line is historical: its plan, its `r=3` durable state and its r1–r3 journal. The current-cycle `r=1` (rv1) DO journal lines appended later in its Progress log (from `2026-10-09T20:20:21Z`) belong to the current cycle.

# D170 — Navigation: validate FK uniqueness for OneToOne

- task: D170 (GitHub issue #170)
- collection: 1.0.9-rc2
- selected_variant: pdca-dotnet
- status: **incomplete (terminal STOP)** — CHECK r=3 fail, same defect family as r=1/r=2; no 4th corrective slot
- status file: docs/specs/status/rc2-170-onetoone-fk-uniqueness-1.md
- phase: terminal STOP — incomplete (CHECK r=3 fail)
- current cycle N: 1
- plan revision r: 3
- attempt n: 1 (of 3)
- plan_state: **terminal** (r=3 sealed; same-family CHECK fail ⇒ STOP; no further revision)
- evidence contract revision rv: 3
- source brief: /tmp/opencode/rc2/briefs.md §#170

## Current state

r=1 and r=2 both FAILED CHECK on the **same defect family**: (a) the ClickHouse row was grounded on
the wrong capability flag (`ClickHouseDialect.cs:476` / `SupportsReferenceToCollectionNavigation`),
(b) residual overbroad `JoinInto` wording remained in docs/XML, and (c) some closed-matrix variants
were left unmapped. The r=2 corrective changes did not close that family, so escalation produced a
**genuine r=3** plan (revision increment, iteration reset): it closes the runtime-evidence and
qualification defects by adding real ClickHouse container-runtime testing plus the residual cases.
r=3 is the **last corrective slot**; one CHECK follows; a same-family CHECK failure at r=3 ⇒ **STOP**,
no 4th attempt. Minimal solution is now fixed: reuse existing `eager_parent/eager_child` + a test-local
one-to-one mapping. **Runtime product behavior is unchanged.**

## Goal

Finish GitHub issue #170 **without production-runtime changes**. Close the runtime-evidence and
qualification defects: every applicable required check must actually execute; skipped tests,
SQL-generation-only evidence and missing reports cannot establish acceptance.

## Scope

- In: qualification of all duplicate-rejection wording; metadata/DDL assertions for independently
  declared shared-FK relationships; sync/async identity rejection and positive loads; ClickHouse
  container-runtime negative; residual case coverage; affected XML/prose + both language mirrors;
  registry/roadmap records; evidence contract rv=3.
- Out: production runtime/API/cache/provider-capability changes; schema/unique-index validation;
  identity algorithm; new exception/abstraction types; new key forms; new tables; capability-based
  exclusion; unrelated seed/table changes; deferred milestone rows.

## Acceptance criteria (each with a negative case)

- **R170-01** — qualify **all** duplicate-rejection wording: two distinct **non-null child
  identities** throw; keyless/null-identity repeats are **tolerated, first child wins**. Negative:
  reject every remaining unconditional claim.
- **R170-02** — independently declared relationships sharing an FK do **not** force uniqueness in
  metadata/DDL; replace the tautological inverse-null assertion. Negative: a forced-uniqueness
  flag/constraint fails the assertion.
- **R170-03** — preserve sync/async duplicate-identity rejection; demonstrate positive single-child
  Guid/string loads sync/async, tolerated repeats, absent-child/null-FK behavior. Negatives: no
  suppressed/false exception; no wrong null/populated navigation.
- **R170-04** — require CH container-runtime `.JoinInto(..., p => p.Primary)` with a **non-unique FK**
  and **two distinct children** → `InvalidOperationException` sync/async. **Only observed fallback:**
  if real execution rejects the shape **before materialization**, pin `NotSupportedException` + text.
  Never ground applicability on `ClickHouseDialect.cs:476` / `SupportsReferenceToCollectionNavigation`.

## Variant matrix (every row closed)

| axis / variant | closure |
|---|---|
| int / Guid / string key | tests: closed matrix `int/Guid/string × single/distinct/repeated child × sync/async` |
| single / distinct / repeated child | tests: single and distinct identity rejection; repeated (Cartesian) same-identity tolerated |
| sync / async | both selected across core tests |
| keyless / null identity repeats | tests: first child wins, no throw |
| null / unmatched default FK / no child | tests: null / no throw |
| equal-value identities | existing equality tests |
| key types lacking value equality | existing guard `JoinIntoSpec.cs:584-609` |
| independently declared shared-FK relationships | metadata/DDL tests: no forced uniqueness |
| provider | retain every provider obligation (items below); CH = new container-runtime negative or observed pinned rejection, never SQL-only/skipped |

## Provider / flag matrix

Retain every provider obligation. ClickHouse must be a **new container-runtime negative** or an
**observed pinned rejection** — never SQL-generation-only and never skipped. No unsupported-provider
exemption; no deferred milestone row.

## Minimal solution (fixed)

Reuse the existing `eager_parent/eager_child` fixtures + a **test-local one-to-one mapping** (chosen).
Minimal extra eager rows are a conditional prerequisite. A new table or capability-based exclusion is
**rejected**.

## Gaps to resolve this revision

Seed cardinality, remaining wording copies, and the original evidence row IDs/selectors are resolved
by focused scout/contract retrieval — **not assumptions**. Do not invent row IDs.

## DO units / streams

- **D170.3a** (additive prerequisite) — one focused scout: `ClickHouseTestProvider.cs:454-461`
  cardinality/mapping capability + sweep `src/**`, `docs/**`, `docs/ru/**` for equivalent overbroad
  wording. Roslyn first for symbols; ignore-aware text search only for prose.
- **D170.3b** — add CH runtime tests beside `ClickHouseImplicitNavigationTests.cs:14`, deriving
  `ProviderTestSuite`; reuse eager fixtures, seed minimally only if needed, **never add `eager_note`**;
  sync + async negative; add the fallback test only after an observed pre-materialization rejection.
- **D170.3c** — qualify `EntityBuilder.cs:684-686` + every scout-confirmed copy; repair shared-FK
  assertions; add Guid/string positives beside `JoinIntoOneToOneTests.cs:160-188` and an
  absent-child/null-FK case.
- **D170.3d** — update issue/registry/roadmap records and the contract; boundary sweep confirms no
  runtime/API/cache/provider-capability change, no unrelated seed/table changes, CRLF preserved, and
  no public links to `docs/specs/**`.

## Unit execution mode

Sequential, one tree; no worktree/commit/merge. The original D170 is **temporarily blocked** on the
additive prerequisite D170.3a (remains active, not superseded). CRLF preserved and
`TreatWarningsAsErrors` respected; commits happen only in ACT after CHECK.

## Test commands

- `B` = `dotnet build -c Debug`
- `U` = `dotnet test tests/nextorm.core.tests -c Debug`
- `F` = `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~JoinIntoOneToOneTests"`
- `C` (CH) = `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~ClickHouse"`; require discovery/execution of the new sync/async cases.
- `I` (integration) = `DOCKER_HOST=... dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`; load `.opencode/skills/running-integration-tests/SKILL.md`, start Podman if needed; skipped required providers are insufficient.
- `V` (coverage) = reproduce the CI workflow `dotnet-coverage collect`→`reportgenerator` from
  `.github/workflows/dotnet.yml`; scope `coverage.settings.xml`; targets **85% line / 75% branch**.
  Missing workflow details are a **gate dependency**, not invented commands.

## Priority matrix

P1: R170-01/02 qualifications, **every** R170-03 positive/negative, R170-04 runtime closure including
fallback correctness, and the absence of forbidden public links. Registry/prose clarifications are P2.
CHECK may add rows but **must not downgrade P1**.

## Docs plan

Update affected XML/prose + both language mirrors wherever the scout sweep finds copies; registry and
roadmap must be accurate. No renaming, no generated docs, and no public-to-internal-spec links
(`docs/**`, root `readme.md` must not link to `docs/specs/**`).

## Perf measurement decision

N/A — production `JoinIntoSpec.cs:518-545` is unchanged; scope is docs/tests plus an optional one-time
seed. No timing assertions.

## Reconnaissance / classification

The focused scout is an **additive prerequisite**; CH behavior is settled by **real execution**, not
by a capability flag. This is bounded retrieval, not DO repetition.

## Versioned evidence contract (rv3)

rv=3 **supersedes rv=2**; it retains every inherited stable row ID and obligation, amends the
ClickHouse predicate (item R170-04), and adds **E170-CH-RT**. Sources/artifacts are planned until
observed.

- **R170-01 rows**: owner scout→coder, verifier check; invocation `git diff -- src docs docs/ru` +
  scout sweep; exit 0; all claims qualified / no forbidden links.
- **R170-02 rows**: owner coder, verifier check; command `U` + selectors; exit 0; assertions prove
  independent relationships, no forced uniqueness in metadata/DDL.
- **R170-03 rows**: owner coder, verifier check; commands `F`+`U`; exit 0; all required
  positives/negatives discovered and passed.
- **R170-04 rows**: owner coder, verifier check; commands `I`+`C`; exit 0; required providers/cases
  executed, no skip-based PASS.
- **E170-CH-RT / R170-04**: owner coder, verifier check; applicability = "runtime negative executed on
  container" **observed from logs**; command `C`; exit 0 + sync/async distinct-child exception
  assertions pass; fallback requires a **logged pre-materialization rejection** + pinned
  `NotSupportedException`/text.
- **Cross-cutting**: retain inherited build/coverage/boundary rows; commands `B`, `V`,
  `git diff --check`; CHECK re-gather budget = **2 targeted scout requests**, owner check; exhaustion
  **cannot** imply PASS.

## CHECK re-gather budget

Max 2 targeted scout calls on r=3, owner `check`. A missing report is re-gathered, not a DO iteration
and not a product FAIL. Persistent low confidence after budget → orchestrator `escalate`. No PASS
while an applicable required row is open.

## Assumptions / prerequisites / risks

- Assumption: public API and runtime backstop are preserved; a genuine backstop defect would be a new
  PLAN task, not silent scope expansion (the original unit stays active).
- Prerequisite: before container runs load `.opencode/skills/running-integration-tests/SKILL.md`;
  start Podman if the socket is missing; unknown tools/fixtures are classified in-cycle first, not
  PASS/STOP.
- Risk: process-wide metadata cache isolation in tests; keyless metadata registration order;
  CRLF/CPM/`TreatWarningsAsErrors` must hold.
- Confidence: high on the semantic defect and the minimal remedy; seed contents, contract details and
  CH execution remain **unverified until gathered**. No acceptance weakening; CHECK enforces the r=3
  STOP rule.

## Deferred + trigger (stays in milestone 1.0.9-rc2)

Schema/unique-index validation and composite-key support expansion remain OUT of scope with trigger: a
separate agreed requirement with an available schema/key contract. The ClickHouse runtime evidence is
now **in-scope for r=3** (no longer deferred); no remainder moves to a later milestone.

## Durable state

- Current cycle N: 1
- Plan revision r: 3
- Attempt n: 1 (of 3)
- Defect history:
  - `DEF-170-01` docs/XML overbroad keyless guarantee — observed r1/1 (CHECK fail); fixes applied: 1 (r2);
    observed again r2 (CHECK fail, same family: residual overbroad `JoinInto` wording); fixes applied: 2 (r3);
    evidence `artifacts/pdca/rc2-170/r1/evidence.json`, `artifacts/pdca/rc2-170/r2/evidence.json`; fix applied r3 (D170.3c); CHECK r=3 fail — same family; terminal (incomplete).
  - `DEF-170-02` ClickHouse evidence row grounded on the wrong capability flag
    (`ClickHouseDialect.cs:476` / `SupportsReferenceToCollectionNavigation`), no runtime test —
    observed r1/1 and r2 (CHECK fail, same family); fixes applied: 2 (r3 replaces with CH
    container-runtime testing + **E170-CH-RT**); evidence `artifacts/pdca/rc2-170/r2/evidence.json`;
    fix applied r3 (D170.3b); CHECK r=3 fail — same family; terminal (incomplete).
  - `DEF-170-03` closed matrix variants without a mapped case — observed r1/1; fix applied: 1 (r2);
    observed again r2 (CHECK fail, same family: variants still unmapped); fixes applied: 2 (r3 adds
    residual cases); evidence `artifacts/pdca/rc2-170/r1/scope.json`; fix applied r3 (D170.3c); CHECK r=3
    fail — same family; terminal (incomplete).
  - r=3 STOP rule: a same-family CHECK failure at r=3 ⇒ STOP; no 4th attempt — **triggered at r=3**.
- Verification state: **terminal STOP / incomplete** — CHECK r=1, r=2 and r=3 all failed on the same
  defect family (unmet R170-02/R170-03 variant/oracle closure; CH runtime row satisfied but matrix not
  closed); D170.3a additive prerequisite closed (original D170 resumed, not superseded); r=3, n=1/3,
  plan_state=terminal; no ACT mark. Branch restored to green tip `5b7fb9e7`; patch preserved; next
  allowed step = none for D170.

## Progress log

2026-10-07T00:00:00Z | PLAN | r1 | 1/3 | PLAN ready — plan_state=ready (collection P; DO not started) | this file
2026-10-08T15:37:00Z | DO | r1 | 1/3 | D170.1–D170.6: XML-doc trust boundary (`HasOneToOne`); core async guard + declared-trust/shared-FK metadata tests; integration non-unique-FK async negative; EN+RU relationships/limitations docs; roadmap §10 + design register. Builds Debug 0/0, Release 0/0. Inner `JoinIntoOneToOne` 6/6, `RelationshipMetadata` 27/27; boundary core full 1759/1759, integration `JoinInto_OneToOne` 16/16 (0 skipped, DOCKER_HOST). Scope amendment: `FullyQualifiedName~CommonTestSuite.JoinInto` selects 0 (xunit v3 concrete-class FQN) → `FullyQualifiedName~JoinInto_OneToOne`, validated before run. Verified issue #170 URL: https://github.com/AlexeyShirshov/nextorm/issues/170. Design register: docs/specs/design/implicit-navigation-queries.md. Evidence: artifacts/pdca/rc2-170/r1/ | artifacts/pdca/rc2-170/r1/
2026-10-08T10:46:00Z | PLAN | r2 | 1/3 | Replanned: r=1 CHECK fail (docs overbroad keyless guarantee + ClickHouse evidence plan + unmapped variants) | this file
2026-10-08T10:57:00Z | DO | r2 | 1/3 | D170.2-DOC done — qualified `HasOneToOne` XML-doc with trusted-not-validated uniqueness + non-null-identity backstop + keyless collapse (src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:622-634); EN/RU `docs/advanced/relationships.md` and `docs/advanced/limitations.md` (RU mirrors) qualified; no unconditional backstop claim remains | artifacts/pdca/rc2-170/r2/build-core-debug.log
2026-10-08T10:57:30Z | DO | r2 | 1/3 | D170.2-TEST done — `JoinIntoOneToOneTests.cs` keyless sync/async collapse pins + Guid/string-key identity fixtures (10/10); `RelationshipMetadataTests.cs` `SharedFkParent` OneToMany+OneToOne and two-`HasOneToOne` shared-FK acceptance (28/28); async integration negative added in `CommonTestSuite.JoinInto.cs` | artifacts/pdca/rc2-170/r2/inner-core-joinintoonetoone.log, artifacts/pdca/rc2-170/r2/inner-core-relationshipmetadata.log
2026-10-08T10:58:00Z | DO | r2 | 1/3 | D170.2-APPLICABILITY done — ClickHouse applicability predicate (`ClickHouseDialect.cs:476`, `SupportsReferenceToCollectionNavigation => false`) + in-milestone 1.0.9-rc2 reopening trigger recorded in `docs/specs/design/implicit-navigation-queries.md` §2 (SQL-gen is not runtime-backstop evidence) | docs/specs/design/implicit-navigation-queries.md
2026-10-08T10:58:30Z | DO | r2 | 1/3 | D170.2-REGISTRY/BOUNDARY done — roadmap `docs/specs/roadmap/todo_navigation_properties.md` §10 marks #170 resolved; builds Debug/Release exit 0 (0W/0E); core full 1764/1764; integration `JoinInto_OneToOne` 16/16 (SQLite+PostgreSQL+SQL Server+MySQL ran, 0 skipped; DOCKER_HOST); CH SQL-gen applicability log retained; docfx exit 0 (0 errors, 2 pre-existing duplicate-source warnings); inner-loop gate brief/report exit 0 | artifacts/pdca/rc2-170/r2/evidence.json, artifacts/pdca/rc2-170/r2/scope.json
2026-10-08T10:58:45Z | DO | r2 | 1/3 | DO r2 complete — pending CHECK; r=2, n=1/3, plan_state=ready; no CHECK/ACT mark; the stale `boundary-sqlserver-joinintoonetoone.log` (wrong selector, Zero tests exit 8) is superseded by the single whole-integration filtered run | docs/specs/status/rc2-170-onetoone-fk-uniqueness-1.md
2026-10-08T16:05:00Z | PLAN | r3 | 1/3 | Replanned: r=2 CHECK fail (same defect family — CH row grounded on wrong capability flag + residual overbroad JoinInto wording + unmapped variants); escalate → genuine r=3; last corrective slot; CHECK r=3 same-family fail ⇒ STOP | this file
2026-10-08T16:07:00Z | DO | r3 | 1/3 | D170.3a additive prerequisite closed — focused scout: ClickHouseTestProvider.cs:454-461 cardinality/mapping capability + overbroad-wording sweep (src/docs/docs-ru, Roslyn first); original D170 resumed (not superseded) | artifacts/pdca/rc2-170/r3/scope.json
2026-10-08T16:10:00Z | DO | r3 | 1/3 | D170.3b CH runtime test added — ClickHouseJoinIntoOneToOneTests.cs (2/2 passed, 0 skipped, DOCKER_HOST container): primary path InvalidOperationException sync+async for non-unique FK with two distinct child identities at materialisation | artifacts/pdca/rc2-170/r3/ch-runtime-joinintoonetoone.log
2026-10-08T16:11:00Z | DO | r3 | 1/3 | D170.3c residual qualification done — qualified remaining overbroad JoinInto wording (EntityBuilder.cs:684-686 + scout copies); Guid/string-key positive single-child loads sync/async + absent-child/null-FK case; inner core affected selectors 44/44 (16 JoinIntoOneToOne + 28 RelationshipMetadata) | artifacts/pdca/rc2-170/r3/inner-core-joinintoonetoone-relationshipmetadata.log
2026-10-08T16:13:00Z | DO | r3 | 1/3 | D170.3d registry/roadmap updated — issue/registry/roadmap records + contract rv=3; no public link to docs/specs/** (scan: none); git diff --check exit 0 | artifacts/pdca/rc2-170/r3/boundary-git-diff-check.log
2026-10-08T16:17:00Z | DO | r3 | 1/3 | DO r3 complete — pending CHECK; r=3, n=1/3, plan_state=ready; builds Debug/Release 0 warnings/0 errors; inner core 44 (16+28); boundary core full 1770/1770; integration all-providers exit 0 Total 3343/Failed 0/Skipped 197 (origins MySQL 80, SQL Server 48, SQLite 40, PostgreSQL 26, LobCapabilityProbeTests 2, LobPerfHarness 1 — all capability-based); per-provider executed/skipped SQLite 588/40, PostgreSQL 602/26, SQL Server 580/48, MySQL 548/80, ClickHouse 117/0 — all five required providers RAN; docfx exit 0 (0 errors, 2 pre-existing warnings); inner-loop validator brief exit 0 / report exit 0; no commit | artifacts/pdca/rc2-170/r3/evidence.json
2026-10-08T16:25:37Z | STOP | r3 | 1/3 | terminal STOP bookkeeping — CHECK r=3 fail, same defect family as r=1/r=2 (unmet R170-02/R170-03 variant/oracle closure); no 4th corrective slot; task → incomplete; patch docs/specs/status/rc2-170-evidence/D170-STOP-incomplete.patch; branch restored to green tip 5b7fb9e7 | docs/specs/status/rc2-170-evidence/README.md
2026-10-09T20:20:21Z | DO | r1 | 1/3 | stage1: R170-02 metadata-independence oracles (`SharedFkParent` OneToMany+OneToOne sharing one mapped FK preserved separately + negative no-forced-uniqueness `ForeignKey[0].IsKey == false`; `DualRef` two HasOneToOne) and R170-03 runtime oracle (distinct identities sync+async → InvalidOperationException `*more than one distinct*`, same-identity async parity, keyless sync+async tolerated/no throw, two-parent per-parent control, payload-asserting one-child/no-child/inner positives); discriminating red↔green: backstop throw suppressed → inner 2 failed exit 2, restored exactly (sha256 30b4a16b… matches before) → 33/33 exit 0; boundary Debug sln build 0W/0E, Release sln build 0W/0E, full core 1981 passed/0 failed/0 skipped; inner-loop validator brief+report exit 0; no production change, no commit | artifacts/pdca/D170/rv1/
2026-10-09T20:29:30Z | DO | r1 | 1/3 | stage2: R170-04 provider matrix — shared `CommonTestSuite.JoinInto.cs` async duplicate-identity rejection (`*more than one distinct*`) and positive two-parent/one-child-per-parent control (sync+async, payload-asserting); ClickHouse-owned `ClickHouseJoinIntoOneToOneTests.cs` 6 cases (non-unique FK two distinct identities sync+async → InvalidOperationException; positive single-child sync+async; repeated same-identity tolerated sync+async); inner integration `FullyQualifiedName~JoinInto_OneToOne` 30 passed/0 failed/0 skipped, CH class 6 passed/0 skipped; boundary full sweep exit 0 Total 3583/Failed 0/Skipped 197; per-provider executed/skipped SQLite 625/40, PostgreSQL 639/26, SQL Server 617/48, MySQL 585/80, ClickHouse 148/0 (CH one-to-one 6/0); sln Debug+Release 0W/0E; validator brief+report exit 0; CRLF; no production change, no commit | artifacts/pdca/D170/rv1/
2026-10-09T20:34:51Z | DO | r1 | 1/3 | stage3 (final DO): R170-05 records/sweep + R170-07 coverage + frozen rv1 manifest — swept `src`/`docs`/`docs/ru` (git grep/rg, roslyn) and qualified every remaining overbroad one-to-one claim: XML docs `EntityMetadataBuilder.HasOneToOne`, `RelationshipKind.OneToOne`, `JoinOptions.OneToOne`, `EntityBuilder` reference `JoinInto` (declaration-time trust vs per-parent duplicate-identity backstop; repeated/keyless/null-identity tolerated, first child wins); EN/RU `advanced/relationships.md` + `advanced/limitations.md` (new declaration-time-trust limitation row); roadmap §10 #170 rewritten as resolved with schema/unique-index validation explicitly out of scope; public docs link to no `docs/specs/**` (NONE). Evidence: `scope-docs.json` brief exit 0; inner build 0/0 + `JoinIntoOneToOneTests` 11/11; sln Debug 0W/0E, Release 0W/0E; full core 1981 passed/0 failed/0 skipped (one known-flaky `QueryCacheControlsTests.SlidingExpiration` timing test first run, re-run green); docfx exit 0 (0 errors, 2 pre-existing duplicate-source warnings); `git diff --check` exit 0; CI coverage reproduced exit 0 — line 86.8% / branch 79.4% (both above 85/75); `manifest.json` validate exit 0 (9/9 rows met) | artifacts/pdca/D170/rv1/manifest.json
2026-10-10T01:42:00Z | DO | r1 | 2/3 | stage4a matrix closure (core, re-attempt after CHECK-gather) — closed the flagged rows: IV/IR string+Guid+zero-valued identity (single/distinct/repeat, sync+async), ZS/ZA empty-child null navigation, UQ/NQ metadata declaration acceptance (unique + non-unique FK, no metadata rejection), SH1 runtime OneToMany+OneToOne shared-FK multiplicity preserved, SH2 two independent OneToOne navigations each per-parent reject, async two-parent payload parity, KS/KA set-membership instead of join order; targeted condition inversion `!Equals`→`Equals` red↔green (`control-mutation.patch`, red exit 2 14 failed/40 passed, green 54/54 exit 0, sha256 30b4a16b before==after, restored); case-map binds every case id (file:line); manifest validator exit 0; scope-4a brief exit 0; inner build 0W/0E + inner 54/54 exit 0; no product change (JoinIntoSpec restored), no commit | artifacts/pdca/D170/rv1/do-stage4a-inner.log
2026-10-10T01:50:25Z | DO | r1 | 1/3 | correction: the 2026-10-10T01:42:00Z stage4a line mislabelled the attempt counter as 2/3; durable state is r=1, n=1/3 (attempt counter not advanced) | this file
2026-10-10T01:50:25Z | DO | r1 | 1/3 | stage4b provider-matrix closure — shared `CommonTestSuite.JoinInto.cs` repeated same-identity one-to-one case sync+async (`// case PV(repeated)`, cartesian repeat of one physical child row via the notes collection; shared tables carry a primary key) closing `{SQLite,PostgreSQL,SqlServer,MySQL}×{sync,async}×repeated`; async two-parent control now asserts the same `ParentId`/`Name` payload as the sync twin; ClickHouse `ClickHouseJoinIntoOneToOneTests.cs` repeated sync+async now assert a raw child `Count == 2` before the collapse (proves two physical same-identity rows reach the oracle); PV closed 30/30 in case-map.md. Inner build 0W/0E + `FullyQualifiedName~JoinInto_OneToOne` 38 passed/0 failed/0 skipped, CH class 6/6; boundary full sweep exit 0 Total 3591/Failed 0/Skipped 197; per-provider total/skipped SQLite 667/40, PostgreSQL 667/26, SqlServer 667/48, MySQL 667/80, ClickHouse 148/0; sln Debug+Release 0W/0E; `git diff --check` exit 0; manifest validator exit 0; report validator exit 0; no product change, no commit | artifacts/pdca/D170/rv1/
2026-10-10T01:58:00Z | DO | r1 | 2/3 | records reconciliation (reason = CHECK r=1 records fail; open row REQ170-RECORDS only, no product defect): reconciled top current-cycle block to executed DO stages 1–4b + pinned rv1 manifest (9/9 rows met); baseline current tree HEAD a956598a / plan base 9a2a2871; labelled prior r3 STOP record historical; issue/roadmap disposition unchanged (documented declaration-time trust + closed runtime boundary; schema validation explicitly out of scope); no product/test change | this file
2026-10-09T20:59:37Z | ACT | r1 | 2/3 | CHECK r=1 PASS (rv1 9/9 rows met); ACT finalize — outcome CHECK PASS (r=1, n=2/3, rv1 9/9 met), plan_state done/verified, issue #170 closed (verified URL https://github.com/AlexeyShirshov/nextorm/issues/170), next step none; no production defect | artifacts/pdca/D170/rv1/manifest.json

## Terminal STOP (incomplete)

- Outcome: **incomplete (terminal STOP)** — issue #170 stays **OPEN**; next allowed step = **none for D170**.
- STOP reason: CHECK failed at r=1, r=2 and r=3 on the **same defect family** — unmet R170-02 /
  R170-03 acceptance-test and variant/oracle closure. The ClickHouse runtime row was satisfied, but
  the required variant matrix was not closed. A same-family CHECK failure at r=3 is the sealed
  terminal revision; escalation mandates STOP and there is no 4th corrective slot.
- Preserved evidence (complete D170 change — tracked modifications + the new untracked test):
  `docs/specs/status/rc2-170-evidence/D170-STOP-incomplete.patch`; per-revision logs retained at
  `artifacts/pdca/rc2-170/r1/`, `artifacts/pdca/rc2-170/r2/`, `artifacts/pdca/rc2-170/r3/`.
- Restore: the task's 12 tracked product/test/doc paths were reset to the last verified-green tip
  `5b7fb9e7`, and the new untracked test
  `tests/nextorm.integration.tests/ClickHouseJoinIntoOneToOneTests.cs` was deleted (kept only in the
  patch); `git diff --stat` is empty and no product/test/doc path is modified.
- The lane continues with the next pending task per the user-authorized continuation policy.
