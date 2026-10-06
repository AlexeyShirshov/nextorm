# Status — corrective cycle `rc1-g01-c-evidence-1`

- Task id: `rc1-g01-c-evidence-1` (collection-level C evidence completion for group G01, collection `1.0.9-rc1`)
- Cycle: `N = 1`
- Plan revision: `r = 1`
- Attempt: `n = 3/3`
- Evidence contract revision: `rv = CEvidence.ContractA.1`
- Status: **done** — ACT complete; CHECK PASS (r=1, n=3/3); corrective documentation scope only
- Branch: `1.0.9-rc1`; HEAD: `36e540e2`
- Mode: autonomous; auto-commit **authorized** by the collection (ACT commits later; this DO task performs **no commit/push**)
- Hard constraint: **docs-only** — touch ONLY `docs/specs/status/**`. If any step appears to require a `src/`/`tests/`/config change, STOP and report it as a defect.
- Allowed commit paths: `docs/specs/status/**` only.
- Excluded from this cycle: `docs/specs/status/rc1-131-pg-json-column-1.md` (must not be modified).
- Notice: host has no `todowrite` tool; this status file carries the progress log instead.

## Goal

Close the collection gate `C-E03` (verified completion) and `C-E04` (safe integration) evidence gaps for group
G01 without changing any product/test/config file. Produce a **fresh collection-level evidence JSON** that
honestly records what actually ran (an aggregate sweep at HEAD `36e540e2` plus the historical targeted
PostgreSQL/MariaDB/core runs), run the inner-loop validator and record its real exits/violations, correct the
stale `R141-VERIFY` ACT claim, bind the D127/D126 acceptance tables to real test/log evidence, and record the
collection C-E03/C-E04 disposition (including a named waiver if the validator cannot represent the
collection/historical scope).

## Acceptance criteria

| ID | Criterion |
|---|---|
| **CE-R01** | A fresh `docs/specs/status/rc1-g01-c-evidence-1.json` exists and honestly describes the aggregate HEAD `36e540e2` executions (Debug/Release 0/0; unit 8438 total / 0 failed / 2523 skipped; container integration 3191 / 0 failed / 193 skipped; coverage 87.1% line / 78.8% branch; docfx 0 errors / 2 warnings; perf 7/7) and the targeted PG/MariaDB/core runs, each with a real command array, revision, integer exit code and `selected_count >= 1`. `scope.rebuild = none`; no command invented, no applicable execution omitted, no retrospective filter added, no broad command moved for exit 0. |
| **CE-R02** | A provenance manifest `docs/specs/status/rc1-g01-c-evidence-1/provenance.md` maps every execution → command, revision, log/artifact path and aggregate counts; the targeted runs are labeled "targeted" in prose only (no invented schema `phase`); the historical revision `815e0127+d141-do` is recorded truthfully and distinguished from HEAD `36e540e2`. |
| **CE-R03** | `validate_inner_loop.py brief` and `report` are run on the fresh JSON, stdout/stderr + exit codes saved to `docs/specs/status/rc1-g01-c-evidence-1/validator-brief.txt` and `validator-report.txt` (each ending `EXIT=<code>`); the `report` violations, if any, are quoted verbatim; if `brief` rejects on `executions`, a scope-only companion is created and reported. |
| **CE-R04** | `docs/specs/status/rc1-141-version-gates-1.md` gets a `## Validator re-run disposition (r2)` section (new JSON path, actual brief/report exits, verbatim violations, statement the original `/tmp/.../E141-evidence.json` is unchanged); ACT at `:302` no longer claims `R141-VERIFY` is met (historical report exited 2), points to the fresh exit-0 result or the named waiver; supersession note for the historical integration observations 3136/3137 → aggregate HEAD `36e540e2` 3191/0/193. |
| **CE-R05** | In `docs/specs/status/rc1-127-mssql-output-into-1.md` the ten-row table `D127-E01..E10` is inserted bound to real test/log evidence, D127-E08/E09 remain open gaps, run evidence (`test-sqlserver.log:4-8`, `sqlserver-class.log:22-23`, `integration.log:437-439`) is bound, and a correction note states the earlier `done` claim is not verified against the full frozen criteria; D127 is not marked superseded. In `docs/specs/status/rc1-126-tuple-ctor-1.md:183-187` the stale `(pending)/planned` rows are replaced by individual `E01–E13` final results using only values already in the file. |
| **CE-R06** | `docs/specs/status/collection-1.0.9-rc1.md` gets a `C-E03 evidence completion` section (all 10 stable IDs with frozen rv, CHECK PASS pointer, ACT done, artifact pointer, status; D141's validator disposition; D127's two gaps; the explicit statement that C-E03 needs an independent `Task→check` run), a `C-E04 safe integration` section (group ref not valid; single-group route ⇒ no branch/merge; `N/A`), the named waiver `W-C-D141-REPORT-CAP-1` if `report != 0` (or the exit-0 fact), and updated durable fields `:57-62`; the historical aggregate PASS at `:90` is preserved as an aggregate result only. Only `docs/specs/status/**` is touched; CRLF preserved; no commit. |

## Task list

| ID | Step | Scope | Status |
|---|---|---|---|
| **D:CE-01** | STEP 0 | Write this plan (header, criteria, tasks, contract, D127 decomposition, matrices, budget, log) | done |
| **D:CE-02** | STEP 1 | Fresh `rc1-g01-c-evidence-1.json` + provenance + validator `brief`/`report` runs | done |
| **D:CE-03** | STEP 1.5 | `rc1-141-version-gates-1.md`: validator disposition, ACT correction, supersession note | done |
| **D:CE-04** | STEP 2 | `rc1-127-mssql-output-into-1.md`: ten-row evidence table + run bindings + correction | done |
| **D:CE-05** | STEP 3 | `rc1-126-tuple-ctor-1.md`: individual E01–E13 final results | done |
| **D:CE-06** | STEP 4 | `collection-1.0.9-rc1.md`: C-E03/C-E04 + waiver + durable fields | done |
| **D:CE-07** | STEP 5 | CHECK r=1,n=1/3 loop-back document-defect fixes (five): C-E03 pointers, concrete D181/D168/D133 artifacts, JSON selector reconciliation, collection disposition restructure | done |

## Evidence contract

| ID | Invocation / action | Required result | Bound artifact |
|---|---|---|---|
| **CE-E01** | Aggregate executions at HEAD `36e540e2` (Debug/Release build, unit, integration, coverage, docfx, perf) | real command arrays; exit codes (0/0/0/0/0/0/0); counts unit 8438/0/2523, integration 3191/0/193, coverage 87.1/78.8, docfx 0 err, perf 7/7 | `/tmp/nextorm-rc1-coll-c/0{3..9}*.txt`, `10-docfx.txt`, `11-perf-acceptance.txt`, `tests/coverage/report/Summary.txt` |
| **CE-E02** | Targeted D141 full-project sweeps at `815e0127+d141-do` | PG 756/756, MariaDB 166/166, core 1524/1524; all exit 0 | `/tmp/nextorm-D141-ContractA-strong/r2/{pg-full,mdb-full,core-full}.log`, `E141-evidence.json` |
| **CE-E03** | Fresh collection-level evidence JSON | valid JSON, schema fields present, honest content | `docs/specs/status/rc1-g01-c-evidence-1.json` |
| **CE-E04** | `validate_inner_loop.py brief <json>` | exit recorded; brief scope valid | `docs/specs/status/rc1-g01-c-evidence-1/validator-brief.txt` |
| **CE-E05** | `validate_inner_loop.py report <json>` | exit recorded; verbatim violations if any | `docs/specs/status/rc1-g01-c-evidence-1/validator-report.txt` |
| **CE-E06** | rc1-141 correction | disposition + ACT fix + supersession note; original `/tmp` JSON unchanged | `docs/specs/status/rc1-141-version-gates-1.md:231+`, `:302`, `:180`, `:297` |
| **CE-E07** | D127 + D126 evidence tables | D127-E01..E10 (2 open), run bindings, correction; D126 E01–E13 final | `docs/specs/status/rc1-127-mssql-output-into-1.md:57`, `docs/specs/status/rc1-126-tuple-ctor-1.md:183-187` |
| **CE-E08** | Collection C-E03/C-E04 + waiver + durable fields | 10 stable IDs enumerated; C-E04 `N/A`; waiver or exit-0; fields updated; docs-only verified | `docs/specs/status/collection-1.0.9-rc1.md` |

## D127 ten-row decomposition (target content)

| Id | Criterion | Evidence | Status |
|---|---|---|---|
| D127-E01 | SQL-gen INSERT table-variable | `tests/nextorm.sqlserver.tests/InsertSqlGenerationTests.cs:434,447` | closed |
| D127-E02 | SQL-gen UPDATE/DELETE table-variable | `UpdateSqlGenerationTests.cs:135`, `DeleteSqlGenerationTests.cs:93` | closed |
| D127-E03 | SQL-gen guards/rejections | sqlserver `InsertSqlGenerationTests.cs:421,465,478,491`; postgres `:672`, mysql `:349`, sqlite `:604` | closed |
| D127-E04 | real container INSERT table-variable identity read-back + nullable reference NULL | `tests/nextorm.integration.tests/SqlServerSpecificTests.cs:1920`; `artifacts/d127/sqlserver-class.xml` | closed |
| D127-E05 | real container UPDATE/DELETE table-variable populated AND empty set | `SqlServerSpecificTests.cs:1958,2002` | closed |
| D127-E06 | sync + async execution all DML | `SqlServerSpecificTests.cs:2041,2068,2100` | closed |
| D127-E07 | nullable/value/reference source values | value `id`/`age`; nullable reference `name` NULL round-trip `SqlServerSpecificTests.cs:1937-1954` | closed |
| D127-E08 | INSERT empty-result set | **gap: no test exists** | open (not closed) |
| D127-E09 | computed source value through OutputInto table-variable | **gap: no `OutputInto*` test references a computed column** | open (not closed) |
| D127-E10 | EN+RU docs (no "phase", comparison table, no public→specs links) | `docs/guide/15-insert-statement.md:531-572,576-582`, `docs/ru/guide/15-insert-statement.md:534-577,579-585`; link audit | closed |

Run bindings: `artifacts/d127/test-sqlserver.log:4-8` (697/697/0/0), `artifacts/d127/sqlserver-class.log:22-23` (87/0/0, EXIT=0), `artifacts/d127/integration.log:437-439` (3191/0/193, EXIT=0).

## Variant / priority matrix

- **V1 chosen** — fresh JSON keeps the three targeted D141 full sweeps as `boundary` executions at their true
  historical revision; **V1' rejected** — dropping them would omit applicable evidence (forbidden).
- **V2 chosen** — report the validator exit truthfully; if the helper's single-cycle caps (1 boundary sweep,
  1 boundary solution build) reject the collection/historical scope, record `W-C-D141-REPORT-CAP-1`; not
  restructure/reclassify evidence to obtain exit 0.
- **V3 chosen** — `R141-VERIFY` points to the fresh exit-0 result only if the report exits 0, otherwise to the
  named waiver.
- Priority: **P1** evidence honesty and no omission > **P2** docs-only constraint (stop on any src/tests need)
  > **P3** minimal focused edits.

## CHECK budget

At most **2** targeted evidence requests at the collection CHECK invocation; the CHECK must perform an
independent `Task → check` run (a run, not merely a document) — this corrective task only supplies the
evidence pointers for that invocation.

## Progress log

```
2026-10-06T02:48:21Z | DO | r1 | 1/3 | DO started | docs/specs/status/rc1-g01-c-evidence-1.md
2026-10-06T02:49:04Z | DO | r1 | 1/3 | D:CE-02 fresh collection evidence JSON + provenance created; validator brief EXIT 0, report EXIT 2 (verbatim: "more than one comprehensive boundary test sweep: 4"; "multiple boundary solution builds: 3") | docs/specs/status/rc1-g01-c-evidence-1.json; docs/specs/status/rc1-g01-c-evidence-1/{provenance.md,validator-brief.txt,validator-report.txt}
2026-10-06T02:49:30Z | DO | r1 | 1/3 | D:CE-03 rc1-141 ACT corrected (R141-VERIFY not met on helper gate) + Validator re-run disposition (r2) + 3136/3137 supersession note; original /tmp E141-evidence.json unchanged | docs/specs/status/rc1-141-version-gates-1.md
2026-10-06T02:49:50Z | DO | r1 | 1/3 | D:CE-04 D127 ten-row evidence table E01-E10 inserted (E08/E09 open gaps), run bindings + correction note | docs/specs/status/rc1-127-mssql-output-into-1.md
2026-10-06T02:50:00Z | DO | r1 | 1/3 | D:CE-05 D126 individual E01-E13 final results bound to existing file values | docs/specs/status/rc1-126-tuple-ctor-1.md
2026-10-06T02:50:20Z | DO | r1 | 1/3 | D:CE-06 collection C-E03 (10 stable IDs) + C-E04 (N/A, no group branch/merge) + waiver W-C-D141-REPORT-CAP-1 + durable fields updated; historical aggregate PASS :90 preserved | docs/specs/status/collection-1.0.9-rc1.md
2026-10-06T02:50:30Z | DO | r1 | 1/3 | docs-only constraint verified: only docs/specs/status/** touched; CRLF lone-LF=0 on all edited files; no commit | git status
2026-10-06T02:56Z | CHECK | r1 | 1/3 | CHECK (r=1, n=1/3): FAIL — P1 document defects | docs/specs/status/rc1-g01-c-evidence-1.md
2026-10-06T02:56Z | DO | r1 | 2/3 | DO started (r=1, n=2/3): five document-defect fixes | docs/specs/status/rc1-g01-c-evidence-1.md
2026-10-06T02:57Z | DO | r1 | 2/3 | Fix1 C-E03 pointers corrected: D126 -> rc1-126-...md:349-351, D127 -> rc1-127-...md:163; the other 8 IDs verified against their final CHECK/ACT lines | docs/specs/status/collection-1.0.9-rc1.md
2026-10-06T02:57Z | DO | r1 | 2/3 | Fix2 concrete artifacts bound: D181 artifacts/d181/c2-integration.log; D168 artifacts/d168/bdn-gate-summary.md; D133 /tmp/nextorm-D133/lob-capability-probe-r2.log; D182 kept as-is | docs/specs/status/collection-1.0.9-rc1.md
2026-10-06T02:57Z | DO | r1 | 2/3 | Fix3 JSON scope.selectors reconciled to unfiltered aggregate/targeted executions; rationale + provenance state explicitly no --filter applied; counts/revisions/executions unchanged; brief EXIT 0, report EXIT 2 (same 4-sweep/3-build caps) | docs/specs/status/rc1-g01-c-evidence-1.json; docs/specs/status/rc1-g01-c-evidence-1/{provenance.md,validator-brief.txt,validator-report.txt}
2026-10-06T02:57Z | DO | r1 | 2/3 | Fix3 (cont.) D141 Validator re-run disposition (r2) refreshed with latest exits + verbatim violations + selector reconciliation; waiver consistent with still-nonzero report exit | docs/specs/status/rc1-141-version-gates-1.md
2026-10-06T02:57Z | DO | r1 | 2/3 | Fix4 collection durable fields restructured: documentation outcome separated from Collection gate C BLOCKED; Blocking reason names D127-E08/E09 + parent gate-C Task->check; historical aggregate PASS preserved | docs/specs/status/collection-1.0.9-rc1.md
2026-10-06T02:57Z | DO | r1 | 2/3 | Fix5 D126 stale lead-in replaced: table holds final E01-E13 results | docs/specs/status/rc1-126-tuple-ctor-1.md
2026-10-06T02:58Z | DO | r1 | 2/3 | docs-only constraint verified: only docs/specs/status/** touched; CRLF-normalized; no commit; durable state unchanged (cycle N=1, r=1, n=2/3) | git status
2026-10-06T02:59Z | CHECK | r1 | 2/3 | CHECK (r=1, n=2/3): FAIL — CE-R05 artifact bindings | docs/specs/status/rc1-g01-c-evidence-1.md
2026-10-06T03:00Z | DO | r1 | 3/3 | DO started (r=1, n=3/3): bound concrete execution files for D126/D131/D128/D163 | docs/specs/status/collection-1.0.9-rc1.md
2026-10-06T08:04Z | CHECK | r1 | 3/3 | CHECK (r=1, n=3/3) PASS for the corrective documentation scope; CE-R01..CE-R06 met; CE-R02 via waiver W-C-D141-REPORT-CAP-1 (report EXIT 2, two verbatim FAILs); does not establish D127 product-test completion or collection readiness | docs/specs/status/rc1-g01-c-evidence-1/check-v3-evidence.txt
2026-10-06T08:05Z | ACT | r1 | 3/3 | ACT done: documentation scope finalized; D141 waiver is the authorized exception; D127-E08/E09 remain open product-test gaps (outside docs-only scope); collection gate C BLOCKED; C-E04 N/A (no merge); next plan empty | docs/specs/status/rc1-g01-c-evidence-1.md
```

## CHECK (r=1, n=3/3) — PASS

- **Verdict: PASS** for the corrective documentation scope (frozen at `N = 1`, `r = 1`, `n = 3/3`,
  `rv = CEvidence.ContractA.1`).
- **CE-R01..CE-R06 all met:** fresh evidence JSON + provenance manifest; validator `brief`/`report` recorded
  with real exits; `rc1-141` disposition + ACT correction + supersession note; D127 ten-row evidence table
  with run bindings and D126 E01–E13 final results; collection C-E03/C-E04 + waiver + durable fields.
- **CE-R02** is satisfied via the authorized waiver `W-C-D141-REPORT-CAP-1`: `validate_inner_loop.py report`
  exits **2**, with the two verbatim FAILs `FAIL: more than one comprehensive boundary test sweep: 4` and
  `FAIL: multiple boundary solution builds: 3` (the helper's single-cycle caps vs the collection-level
  aggregate plus the historical targeted D141 sweeps). No execution was rewritten, omitted or reclassified,
  and no retrospective `--filter` was added.
- **CE-R05 met:** the D127 ten rows are bound to real test/log evidence with **D127-E08/E09 open** (D127 is
  not marked superseded) and the D126 `E01–E13` individual final results are recorded.
- Deterministic capture: `docs/specs/status/rc1-g01-c-evidence-1/check-v3-evidence.txt` (CRLF lone-LF = 0 on
  every touched file, C-E04 ref predicate exit 128, D127 rows confirmed, pinned `N`/`r`/`n`/`rv`, docs-only
  scope check).
- **This CHECK does NOT establish** D127 product-test completion (E08/E09 remain open gaps) or collection
  readiness (the parent gate-C evidence audit/re-check is still outstanding).

## ACT

- **Status: `done`.** The corrective documentation for collection gate C-E03/C-E04 is complete and frozen
  (CHECK PASS, `r = 1`, `n = 3/3`, `rv = CEvidence.ContractA.1`).
- The D141 `report` gate exception is the authorized exception, covered by the named waiver
  `W-C-D141-REPORT-CAP-1`; it waives nothing else.
- **D127-E08** (INSERT empty-result set) and **D127-E09** (computed source value through an `OutputInto`
  table variable) remain **open product-test gaps**, outside this docs-only cycle's scope.
- **Collection gate C remains BLOCKED** on those two product-test gaps plus the parent gate-C independent
  `Task → check` re-run over the supplied C-E03 pointers (see
  `docs/specs/status/collection-1.0.9-rc1.md`).
- **No merge performed** — this is a single-group collection, so no group branch exists and C-E04 is `N/A`
  (ref predicate exit 128).
- **Next plan is empty** for this corrective task; no further corrective DO/PLAN is authorized.
- ACT stages only the explicit `docs/specs/status/**` paths (auto-commit authorized by the collection;
  **no push, no merge**).
