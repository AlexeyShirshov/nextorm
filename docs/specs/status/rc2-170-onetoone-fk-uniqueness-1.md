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
