# D226 — ProcedureResult/BatchResult `RecordsAffected`

- task: T226
- issue: #226
- collection: 1.0.9-todo
- group: G1
- variant: pdca-dotnet
- cycle: 1
- plan_revision: r=1
- iteration: n=1
- evidence_contract: rv=1
- plan_state: ready
- phase: PLAN (complete) — DO not started

## Goal

Prove the hypothesis that the final `DbDataReader.RecordsAffected` can be captured at reader
release and exposed through both result types without depending on a live reader, changing
queries/ownership or provider behavior.

## Acceptance criteria (all P1; negative case each)

- **C1 API/value.** Exactly `public int RecordsAffected { get; }` on both types; returns the
  driver's DML total including `0`, or `-1` when inapplicable. Negative: no setter, no counting
  of read rows, no manual summing of statements.
- **C2 availability.** `ProcedureResult` returns `-1` before the reader is released; returns the
  captured total after a read that actually completed the reader, or after Dispose. Negative: a
  partial read is not declared completion. `BatchResult` is returned already materialized.
- **C3 lifetime.** sync/async release, early Dispose and Dispose-after-read all preserve the
  value; repeated Dispose/getter access are idempotent; the getter does not throw after Dispose.
  Negative: getter never touches a disposed reader; existing Close/Dispose errors are not masked.
- **C4 batch.** single/multi-statement batch exposes one driver total through native `DbBatch` and
  the command fallback. Negative: result-set/element count does not substitute for affected count;
  no per-statement API.
- **C5 providers/boundaries.** SQL Server/PostgreSQL/MySQL/SQLite confirm known DML totals;
  ClickHouse returns the spec `-1`/`0`. Negative: unsupported operations and in-memory keep the
  prior `NotSupported`, no fictitious implementation.

## Minimal solution

Expose the total; keep existing lifecycle/provider semantics and the scope "two properties +
capture"; optimum is two cached-int getters plus one shared safe metadata access inside the
existing ownership area, no new type/interface.

## Alternatives considered

- Getter directly on reader — rejected (violates lifetime/non-throwing).
- Snapshot only before Close — rejected (some drivers not finalized).
- Snapshot around Close, publish once before Dispose — **CHOSEN** (2× O(1) metadata reads;
  drivers to be verified).

## Gaps closed by D0/D1

What the statement did not say → gaps closed by D0/D1 (see D-tasks): exact Close/Dispose order,
native/fallback ownership and metadata finalization; global evidence-contract text and
invariants 1–12; CI coverage steps. Passing them is not assumed; a contradiction returns to PLAN.

## D-tasks (fix now)

- **D0 (active prerequisite).** Before production edits, obtain the listed excerpts and a Roslyn
  semantic lifecycle/ownership review for `CommandReaderOwner.cs:16,31`,
  `ProcedureResult.cs:125,160`, `BatchRunner.cs:93,116,296,321`; confirm no conflict with rv1
  obligations; save preflight evidence.
- **D1.** Confirm on real drivers the pre-/post-Close metadata for DML with a known total,
  sync/async and early Dispose; record observations. Pass = the chosen algorithm reproduces the
  total without provider-specific workaround; otherwise stop dependent edits and return facts to
  PLAN.
- **D2.** `CommandReaderOwner.cs:16,31` — cached int, initial `-1`; on first release do a safe
  pre-Close snapshot, Close/CloseAsync, prefer the available post-Close value else pre-Close else
  `-1`; publish before Dispose and never overwrite. Handle unsupported/unavailable metadata
  separately from cleanup errors.
- **D3.** `ProcedureResult.cs:23,38` — add the exact property reading only the owner cache;
  `BatchBuilder.cs:370,383` — same cached property and add `int recordsAffected` to the internal
  ctor. `BatchRunner.cs:296,318,321,346` passes the snapshot at terminal release without changing
  resource ownership; the shared safe-read helper lives in the existing `CommandReaderOwner`,
  consumers = owner + BatchRunner.
- **D4.** Add fake-reader lifecycle/metadata contract tests and real provider cases in the files
  below; run branch-delta, mutation, unit, container integration and coverage gates. New test
  symbols are named by coder; none are declared existing yet.
- **D5.** XML property docs, four guide pages, status and evidence manifest; mark new public
  members for held T53 (`collection-1.0.9-todo.md:25,66`). The API-freeze itself is deferred,
  trigger: T53 starts in the current milestone; new terminals/flags only by separate agreed
  requirement.

## Execution mode

One sequential stream in the current worktree/branch `1.0.9`, G1; dependencies D0→D1→D2→D3→D4→D5
and shared lifecycle contract exclude parallel edits. No branches/worktrees/merge; no unauthorized
commits.

## Test strategy

Shared metadata/lifetime contract on a fake `DbDataReader` in new **planned**
`tests/nextorm.core.tests/RecordsAffectedTests.cs`; existing `RawCommandInMemoryTests.cs:11`,
`BatchTests.cs:11`, SQLite `RawCommandTests.cs:21`, `RawResultSetTraversalTests.cs:13`,
`BatchMultipleResultTests.cs:26`; integration cases — `CommonTestSuite.Raw.cs:14`,
`CommonTestSuite.BatchMultipleResults.cs:6`, `CommonTestSuite.StoredProcedures.cs:11`,
provider-specific files as needed. Commands use full projects, no invented method filters.

## Variant matrix

Axes: P=ProcedureResult raw/procedure, B=BatchResult, S=parameterless/single vs
parameterized/multi-statement, L=sync/async, full/partial read, Dispose before/after read,
repeated Dispose/getter.

- **SQL Server** — test: known DML `N` and `0`; fixture controls NOCOUNT; no production SQL
  change; SELECT/result traversal not used as affected-count oracle.
- **PostgreSQL** — test: same scenarios; DML oracle checks the driver's own total, not a sum of
  result rows.
- **MySQL** — test: same; fixture avoids changed/matched-update ambiguity using insert/delete with
  a known total.
- **SQLite** — test: same via supported entry points; no stored procedure → guard prior
  unsupported semantics.
- **ClickHouse** — test: supported raw/batch scenarios return `-1`/`0`; genuinely unsupported
  entry points → guard the confirmed prior error, not a silent skip.
- **Unavailable states** — guard + test: a live `BatchResult` is not published; native/fallback
  verified by fake dispatch and available provider paths; in-memory raw/procedure/batch keep
  throwing `NotSupported`.
- **Additional variants** — test: metadata `-1/0/N`, getter unavailable, differing pre/post-Close
  total, value/reference projections and `null/default` payload do not affect the count. No new
  flags/providers; future variants deferred, trigger: release or provider capability change.

## Coverage / branch-delta

≥85% line and ≥75% branch for the `coverage.settings.xml` set, regardless of the warn-only branch
mode; keep the current-tree baseline before edits and an after report. Every new/changed
release/capture branch gets a positive/negative test; do not lower the original criteria for a
global percentage.

## Mutation

Five targeted mutants — remove capture; replace the total with row-count; replace the sentinel with
`0`; reset the cache on repeated release; read a disposed reader from the getter. Each must be
killed by a relevant assertion; infrastructure failure does not count. Patches applied
sequentially backward; before/after hashes and kill report required.

## Docs

`docs/guide/12-raw-sql.md:310,332,579` + RU counterpart `:318+`;
`docs/guide/23-sql-batch.md:215,222,242` + RU counterpart `:215+`: value/sentinel, availability
moment, early Dispose, single batch count, ClickHouse limits; XML docs on both properties. No other
guides/readme; no public links to specs.

## Perf measurement

Not needed — `CommandReaderOwner.cs:16,31`, `BatchRunner.cs:296,321` are O(1) capture at release,
not per-row and not the cached query path; the getter reads an int. Guard review forbids new
per-row allocations/enumerations.

## Reconnaissance

Only bounded D0/D1, no architectural prototype: prove ownership and when the driver metadata is
finalized; observable criterion = a known DML total survives release under sync/async and early
Dispose.

## Design checklist (six lenses)

SOLID — ownership stays; DRY — one shared safe-read with concrete consumers; type design — readonly
int, no new public type; perf — O(1), no per-row work; priorities — P1 rows on the
execution/release path; invariants — 2/8/11 addressed, the other nine are verified by D0 and are
not declared passed yet.

## Priorities

C1–C5, API/capture/release/batch rows and mandatory build/docs/invariants/tests/coverage — P1 by
construction, CHECK does not lower; no P0; separate T53 implementation is P2/deferred, but its
registration note here is P1.

## Risks

Main — post-Close metadata differs across drivers → D1; fallback snapshot may be preliminary →
early-Dispose check; batch ownership unknown → D0, do not assume a second Close; SQL Server NOCOUNT
/ MySQL semantics → deterministic fixtures; containers absent → mandatory environment recovery, not
a green skip.

## Evidence contract — rv=1 FROZEN

Artifact root `artifacts/pdca/D226/rv1/`; `plan_state=ready`. Common required slots each row:
stable row ID, `rv=1`, P1, scenario, owner=`coder/G1`, verifier=`check`, applicability=`T226
executes` with no missing-report exclusion; sources/artifacts are PLANNED.
`R=artifacts/pdca/D226/rv1`; manifest carries command, exit, test counts, source/tree fingerprint
and artifact links. All commands run from repo root; stdout/stderr saved; successful test runs
require a nonzero selected-test count and no unexpected skips.

- **B:** `dotnet build nextorm.slnx -c Debug` → exit 0, 0 warnings/errors; `$R/build.log`.
- **U:** `dotnet test tests/nextorm.core.tests -c Debug` && `dotnet test tests/nextorm.sqlite.tests
  -c Debug` && `dotnet test tests/nextorm.clickhouse.tests -c Debug` → each exit 0; `$R/unit.log`,
  runner results, `$R/test-map.json` with actual symbols/scenarios.
- **I:** after loading `running-integration-tests` and preflight,
  `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run
  --project tests/nextorm.integration.tests -c Debug -- -noColor` → exit 0; `$R/integration.log`,
  `$R/provider-matrix.json`; every mandatory provider actually executed, capability guards
  separately proven.
- **C:** `DOCKER_HOST=... dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" --settings
  coverage.settings.xml --output-format cobertura --output "$R/coverage.xml" && reportgenerator ...
  && bash "$R/verify-coverage.sh" "$R/coverage.xml" 85 75` → exit 0, both thresholds; planned
  verifier reconciled with CI in D0, baseline by the same call before edits.
- **M:** `bash "$R/verify-mutations.sh"` → exit 0 only with five assertion-killed mutants and
  restored hashes; `$R/mutation.log`, `$R/mutations/`, `$R/mutation-report.json`.
- **A:** `bash "$R/verify-plan-evidence.sh"` → exit 0 only with D0/D1 evidence, full invariant
  checklist, branch/test mapping, EN/RU docs, CRLF and the T53 note; `$R/audit.log`,
  `$R/preflight.md`, `$R/release-probe.json`, `$R/branch-delta.json`, `$R/doc-review.md`,
  `$R/invariants.json`.

### Rows

- **E226-01 / R226-API:** C1 exact signatures/getter-only on both types + build gate; B+U;
  artifacts build/unit logs, test-map.
- **E226-02 / R226-CAPTURE:** C1/C2 sentinel, zero/count, pre/post-Close selection, unavailable
  metadata, snapshot-before-disposal; U+A; artifacts unit log, test-map, release-probe.
- **E226-03 / R226-LIFETIME:** C2/C3 sync/async, partial/full read, early/late/repeated Dispose,
  non-throwing cached getter, cleanup errors preserved; U+I; artifacts unit/integration logs,
  test-map.
- **E226-04 / R226-BATCH:** C4 ctor propagation, single/multiple statements, native/fallback, no
  row-count/per-statement substitution; U+I; artifacts test-map, provider-matrix, runner logs.
- **E226-05 / R226-PROVIDERS:** five-provider matrix for both results + known DML oracle; I+A;
  artifacts integration log, provider-matrix, release-probe.
- **E226-06 / R226-UNSUPPORTED:** C5 unchanged in-memory/unsupported guards; U+I; artifacts
  test-map, provider-matrix, runner logs; a missing container does not make the row inapplicable.
- **E226-07 / R226-DOC-INVARIANTS:** four docs pages/XML, CRLF, T53 registration, scope, all 12
  invariants; A+B; artifacts doc-review, invariants, audit/build logs.
- **E226-08 / R226-COVERAGE:** ≥85/≥75, comparable baseline, a test per changed branch; C+A;
  artifacts baseline/after coverage, coverage-report, branch-delta, verifier log.
- **E226-09 / R226-MUTATION:** sensitivity to five regressions + clean restore; M; artifacts
  mutation-report/log, patches, before/after hashes.

## CHECK re-gather

owner=`check`, max 2 targeted scout queries, one extra reproducing run per query; then missing
decisive evidence = GAP, not PASS. A missing report alone does not change rv/r or start DO; no rv1
supersession.

## Gate

Status file must contain the full plan/contract before any `go`; the implementation brief does not
authorize DO. After go, D0/D1 are mandatory before production edits; frozen rv1 must not be
silently weakened.

## D-task summary

| D-task | Scope | Depends on | State |
| --- | --- | --- | --- |
| D0 (active prerequisite) | Preflight excerpts + Roslyn lifecycle/ownership review for `CommandReaderOwner.cs:16,31`, `ProcedureResult.cs:125,160`, `BatchRunner.cs:93,116,296,321`; rv1 conflict check; save preflight evidence | — | pending (active) |
| D1 | Real-driver pre-/post-Close metadata for known-total DML, sync/async + early Dispose; record observations | D0 | pending |
| D2 | `CommandReaderOwner.cs:16,31` cached int, safe snapshot around Close, publish before Dispose | D1 | pending |
| D3 | `ProcedureResult.cs:23,38`, `BatchBuilder.cs:370,383`, `BatchRunner.cs:296,318,321,346` property/ctor/snapshot propagation | D2 | pending |
| D4 | Fake-reader contract + real provider tests; branch-delta, mutation, unit, integration, coverage gates | D3 | pending |
| D5 | XML docs, four guide pages, status/evidence manifest, T53 mark (`collection-1.0.9-todo.md:25,66`) | D4 | pending |

## Progress log

- 2026-10-13 | PLAN | revision r=1 | iteration 1/3 | PLAN(r=1) written by coder; plan_state=ready | `docs/specs/status/D226-records-affected-1.md`
