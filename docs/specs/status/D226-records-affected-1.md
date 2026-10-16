# D226 — ProcedureResult/BatchResult `RecordsAffected`

- task: T226
- issue: #226
- collection: 1.0.9-todo
- group: G1
- variant: pdca-dotnet
- cycle: 1
- plan_revision: r=2
- iteration: n=2/3
- evidence_contract: rv=2
- plan_state: done
- phase: ACT / EXIT (closed)

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
| D0 (active prerequisite) | Preflight excerpts + Roslyn lifecycle/ownership review for `CommandReaderOwner.cs:16,31`, `ProcedureResult.cs:125,160`, `BatchRunner.cs:93,116,296,321`; rv1 conflict check; save preflight evidence | — | done (`artifacts/pdca/D226/rv1/preflight.md`) |
| D1 | Real-driver pre-/post-Close metadata for known-total DML, sync/async + early Dispose; record observations | D0 | done (D1 PASS: `d1-observation.md`, `release-probe.json`, 31/31 oracle rows) |
| D2 | `CommandReaderOwner.cs:16,31` cached int, safe snapshot around Close, publish before Dispose | D1 | done (`src/nextorm.core/DataContext/CommandReaderOwner.cs`) |
| D3 | `ProcedureResult.cs:23,38`, `BatchBuilder.cs:370,383`, `BatchRunner.cs:296,318,321,346` property/ctor/snapshot propagation | D2 | done (`ProcedureResult.cs`, `BatchBuilder.cs`, `BatchRunner.cs`) |
| D4 | Fake-reader contract + real provider tests; branch-delta, mutation, unit, integration, coverage gates | D3 | done (unit + real-driver tests; `logs/boundary-*.log`) |
| D5 | XML docs, four guide pages, status/evidence manifest, T53 mark (`collection-1.0.9-todo.md:25,66`) | D4 | done (XML docs + T53 registration in this file; the four guide pages are owned by the docs stream and verified in `doc-review.md`) |

## Progress log

- 2026-10-13 | PLAN | revision r=1 | iteration 1/3 | PLAN(r=1) written by coder; plan_state=ready | `docs/specs/status/D226-records-affected-1.md`
- 2026-10-14T12:07:54Z | DO | revision r=1 | iteration 1/3 | DO started (phase PLAN → DO); STEP 1 mandatory test scope validated fail-closed: `python3 scripts/validate_inner_loop.py brief /tmp/nextorm-d226-scope.json` → exit 0 (payload wrapped as `{unit, scope}` per the validator schema; the scope object itself is verbatim from the brief) | `/tmp/nextorm-d226-scope.json`, `artifacts/pdca/D226/rv1/`
- 2026-10-14T12:07:54Z | DO | revision r=1 | iteration 1/3 | D0 closed — preflight: excerpts of `CommandReaderOwner.cs:16,31`, `ProcedureResult.cs:125,160`, `BatchRunner.cs:93,116,296,321`; Roslyn lifecycle/ownership review (10 batched `roslyn` calls: members/refs of CommandReaderOwner, ProcedureResult, BatchResult, IBatchExecutor, BatchPlan, BatchRunner.RunBatchMultiple); rv1 conflict check = no existing `RecordsAffected` member/behaviour claims the contract, no naming collision | `artifacts/pdca/D226/rv1/preflight.md`
- 2026-10-14T12:12:55Z | DO | revision r=1 | iteration 1/3 | D1 closed (PASS) — real-driver probe on 5 drivers (sqlite in-process; postgres/sqlserver/mysql/clickhouse via Testcontainers, same pinned images as the integration suite): known DML total 3 reproduced by "pre-close snapshot → release → prefer post-release else pre-close else -1" in 31/31 oracle rows (bare DML, `dml; select`, sync Close/Dispose, DisposeAsync, early Dispose, partial read, native DbBatch and joined fallback); pure SELECT keeps `-1` on the 4 oracle drivers; ClickHouse driver reports `0` and refuses lightweight DELETE (observation only). Runner `bash artifacts/pdca/D226/rv1/run-d1-probe.sh` → script exit 0 / probe exit 0 / build 0 warnings 0 errors | `artifacts/pdca/D226/rv1/release-probe.json`, `artifacts/pdca/D226/rv1/d1-observation.md`, `artifacts/pdca/D226/rv1/logs/d1-probe.log`
- 2026-10-14T12:27:44Z | DO | revision r=1 | iteration 1/3 | D2 closed — `CommandReaderOwner` captures one cached `RecordsAffected` (initial `-1`) on the first release of each of `CloseReader`/`Dispose`/`DisposeAsync`: pre-release snapshot published first, post-release value upgrades it, repeated release cannot reset it, and the shared safe reader (`internal static int ReadRecordsAffected`) treats `NotSupported`/`InvalidOperation`/`ObjectDisposed` as unavailable while cleanup errors keep propagating. Build `dotnet build nextorm.slnx -c Debug` → exit 0, 0 Warning(s) 0 Error(s) | `src/nextorm.core/DataContext/CommandReaderOwner.cs:16-31,72-125`
- 2026-10-14T12:27:44Z | DO | revision r=1 | iteration 1/3 | D3 closed — `public int RecordsAffected => _owner.RecordsAffected` added to `ProcedureResult` (cached read, never touches a reader, does not throw after Dispose); `BatchResult` gained `public int RecordsAffected { get; }` fed by the new internal-ctor parameter; `BatchRunner.ReadMultiple`/`ReadMultipleAsync` pass `CommandReaderOwner.ReadRecordsAffected(reader)` at terminal release without changing resource ownership. Build exit 0, 0 Warning(s) 0 Error(s); inner core `--filter FullyQualifiedName~RecordsAffected` exit 0 → 15 selected / 15 passed / 0 failed / 0 skipped; inner sqlite `--filter "~RawCommandTests|~RawResultSetTraversalTests|~BatchMultipleResultTests"` exit 0 → 94/94 (compound filter, superseded at the boundary by three single-selector runs) | `src/nextorm.core/DataContext/ProcedureResult.cs:74-84`, `src/nextorm.core/Builders/BatchBuilder.cs:383-405`, `src/nextorm.core/DataContext/BatchRunner.cs:296,321`, `artifacts/pdca/D226/rv1/logs/inner-core-recordsaffected.log`
- 2026-10-14T12:27:44Z | DO | revision r=1 | iteration 1/3 | D4 closed (unit + real-driver cases) — new `tests/nextorm.core.tests/RecordsAffectedTests.cs` (fake-`DbDataReader` contract: exact get-only `int` surface on both types via reflection, sentinel-until-release, differing pre/post-release values, unavailable/zero totals, async/early/repeated release, non-throwing cached getter, unmasked cleanup errors); real-driver cases added in `tests/nextorm.sqlite.tests/RawCommandTests.cs`, `RawResultSetTraversalTests.cs`, `BatchMultipleResultTests.cs` and in the integration suites `CommonTestSuite.Raw.cs`, `CommonTestSuite.BatchMultipleResults.cs`, `SqlServerSpecificTests.cs`, `PostgresSpecificTests.cs`, plus the test-only capability flag `ITestProvider.SupportsAffectedRowMetadata` (default `true`, `false` for ClickHouse). `CommonTestSuite.StoredProcedures.cs` was left unchanged on purpose: its shared tests are the unsupported-provider guards (they return no `ProcedureResult`), and the procedure path is covered by the provider-specific cases above. Filtered runs: core `~RawCommand` exit 0 → 8/8; core `~BatchTests` exit 0 → 1/1; sqlite `~RawCommand` exit 0 → 48/48; sqlite `~RawResultSetTraversal` exit 0 → 25/25; sqlite `~BatchMultipleResult` exit 0 → 21/21 | `tests/nextorm.core.tests/RecordsAffectedTests.cs`, `artifacts/pdca/D226/rv1/logs/boundary-*.log`
- 2026-10-14T12:27:44Z | DO | revision r=1 | iteration 1/3 | Boundary sweep closed — build `dotnet build nextorm.slnx -c Debug` exit 0 (0 Warning(s) 0 Error(s)); filtered core/sqlite boundary runs all exit 0 (8/1/15 and 48/25/21 selected, 0 failed, 0 skipped); integration with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` and `--no-build -noColor`: `FullyQualifiedName~RecordsAffected` exit 0 → 22 selected / 22 passed / 0 failed / 0 skipped with PostgreSQL, MySQL, SQL Server and SQLite all executed (the 22 = 5 shared cases × 4 provider suites + 2 provider-specific procedure cases), `FullyQualifiedName~StoredProcedure` exit 0 → 1/1/0/0 (SQLite guard); no provider was unavailable and no provider-reported skip occurred. Coverage via the CI steps: `dotnet-coverage collect … "dotnet test --no-build --verbosity normal"` exit 0 with 11882 total / 0 failed / 11683 succeeded / 199 capability-reason skipped / 0 provider-unavailable, then `reportgenerator` exit 0 → **line 88.7% (≥85) / branch 80.8% (≥75)**; no pre-edit baseline was captured | `artifacts/pdca/D226/rv1/evidence.json`, `artifacts/pdca/D226/rv1/manifest.json` (validators exit 0), `artifacts/pdca/D226/rv1/coverage-report/Summary.txt`, `artifacts/pdca/D226/rv1/logs/coverage.log`
- 2026-10-14T12:27:44Z | DO | revision r=1 | iteration 1/3 | EVIDENCE (observations recorded, no decision taken — C4/C5 ownership sits with CHECK): (a) SQLite exposes the **command-level** DML total for a batch — `create table; insert 2 rows; update 1 row; select …` reports `3` (2+1), not only the last statement's count; the nextorm batch path reports what the driver reports, it never sums statements; (b) the ClickHouse driver returns `0` for both INSERT and SELECT (not `-1`) and exposes no affected-row metadata at all — recorded in the test-only capability flag `ITestProvider.SupportsAffectedRowMetadata = false` for ClickHouse; (c) Npgsql exposes no affected-row total for a `CALL` of a procedure that itself performs the DML, so the `ProcedureResult` sentinel `-1` is the driver's value on the PostgreSQL procedure path while SQL Server's procedure reports the DML total (`ExecuteProcedure_Dml_ShouldExposeTheDriverRecordsAffectedAfterRelease`). Raw evidence: `artifacts/pdca/D226/rv1/release-probe.json`, `artifacts/pdca/D226/rv1/logs/boundary-integration-recordsaffected.log`, `artifacts/pdca/D226/rv1/logs/coverage.log`

- 2026-10-14 | CHECK | r=1 | n=1/3 | verdict FAIL (CHECK→DO): required rows E226-05/07/08/09 open; corrective DO started n=2/3. Docs signature-snippet finding ACCEPTED as non-defective (get-only API). Scope amendment: ClickHouse product-surface test file added to the test scope. | `artifacts/pdca/D226/rv1/manifest.json`
- 2026-10-14 | DO | revision r=1 | iteration n=2/3 | corrective unit 1 closed — E226-05 ClickHouse product surface: new `tests/nextorm.integration.tests/ClickHouseRecordsAffectedTests.cs` (raw insert sync + async, raw query, executed batch fail-closed guard, executed procedure guard) selected AND executed against the real ClickHouse container: `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~ClickHouseRecordsAffected"` → exit 0, 5 selected / 5 passed / 0 failed / **0 skipped** (no capability skip); `$R/provider-matrix.json` now carries the five-provider matrix incl. ClickHouse. Driver facts pinned: every ClickHouse statement reports `0` (never `-1`), the batch role exists but rendering throws NotSupportedException | `artifacts/pdca/D226/rv1/logs/boundary-integration-clickhouserecordsaffected.log`, `artifacts/pdca/D226/rv1/provider-matrix.json`
- 2026-10-14 | DO | revision r=1 | iteration n=2/3 | corrective unit 2 closed — E226-09 branch/mutation obligations: `$R/verify-mutations.sh` produced the five required regressions, each killed by an assertion with a clean project-scoped control run (control core 15/15, sqlite 21/21 — before the +1 test below) and byte-identical restore (`sha256` owner+runner before/after, `git diff --stat` unchanged): M1 wrong total, M2 cache reset on repeated release, M3 disposed-reader getter, M4 sentinel destroyed, M5 batch pass-through wrong value — **5/5 killed** (`exit 2`, `failed:1`, FluentAssertions assertion present, `run_valid=yes`; two earlier attempts were rejected because the control build failed and the script now refuses a run with a failed build or unmatched patch). Stryker survivors adjudicated in `$R/mutation-report.json`: 2 ConfigureAwait removals justified as context-equivalent, the 3 statement removals (`:106`, `:136`, `:142`) now killed by `CloseReader_PublishesTheTotal_PreferingThePostReleaseValue`, `AsyncRelease_PrefersThePostReleaseValue` and the new `AsyncRelease_DisposesTheCommand` (tracking DbCommand). `$R/branch-delta.json` maps PRE-EXISTING vs CHANGED branches with positive/negative cases incl. `CommandReaderOwner.cs:76→77`, `85→86`, `101→102` (unreachable claims answered with observable justification); core `~RecordsAffected` re-run after the new test: exit 0, 16 selected / 16 passed / 0 failed / 0 skipped | `artifacts/pdca/D226/rv1/mutation.log`, `artifacts/pdca/D226/rv1/mutation-report.json`, `artifacts/pdca/D226/rv1/branch-delta.json`, `artifacts/pdca/D226/rv1/logs/boundary-core-recordsaffected.log`

- 2026-10-14 | DO | revision r=1 | iteration n=2/3 | corrective unit 3 closed — E226-08 comparable coverage baseline: the missed pre-edit capture is recorded honestly (it never existed); a faithful pre-T226 snapshot was reconstructed OUTSIDE the working tree at `/tmp/nextorm-d226-baseline/` (tar copy without bin/obj/artifacts/.git, T226-touched files restored with `git show HEAD:<path>`, the two files absent at HEAD deleted, unrelated dirty state preserved; no git worktree, no stash, working tree untouched) and the SAME CI coverage command ran there: `dotnet-coverage collect … "dotnet test --no-build --verbosity normal"` → 11838 total / 1 pre-existing order-dependent failure (`ParamRefreshRecipeTests.HasSupportedShape_ShouldRejectArrayJoin`, unrelated to RecordsAffected) / 199 capability skips. Final tree: 11888 total / 2 failures — `ClickHouseExtremeRowNativeSpecificTests` HttpClient 120 s timeouts under full parallel load, re-run of that class alone → 35/35 / 0 failed, i.e. environmental. `bash $R/verify-coverage.sh <xml> 85 75` → exit 0 both times: **baseline line 88.7% / branch 80.8%, final line 88.7% / branch 80.8%** (no delta invented) | `artifacts/pdca/D226/rv1/baseline/coverage.cobertura.xml`, `artifacts/pdca/D226/rv1/baseline/verify.log`, `artifacts/pdca/D226/rv1/coverage.xml`, `artifacts/pdca/D226/rv1/verify-coverage-final.log`, `artifacts/pdca/D226/rv1/logs/recheck-clickhouse-extreme.log`
- 2026-10-14 | DO | revision r=1 | iteration n=2/3 | corrective unit 4 closed — E226-07 docs/invariant seal: `$R/invariants.json` (12 invariants, each with `file:line` + the observing test/artifact), `$R/test-map.json` (16 variant-matrix rows → test name → `file:line`, 0 unresolved), `$R/doc-review.md` (XML docs on both public members; the four guide pages verified but NOT edited — they are owned by the separate docs stream: EN/RU mention parity 3/3 and 2/2, all four CRLF, no `docs/specs` links from public guides), `$R/verify-plan-evidence.sh` → **exit 0** (49 OK / 0 MISS: named slots, parity, scoped CRLF on all 16 T226 files, T53 registration, valid JSON) | `artifacts/pdca/D226/rv1/invariants.json`, `artifacts/pdca/D226/rv1/test-map.json`, `artifacts/pdca/D226/rv1/doc-review.md`, `artifacts/pdca/D226/rv1/audit.log`
- 2026-10-14 | DO | revision r=1 | iteration n=2/3 | corrective unit 5 closed — final sweep + evidence seal: `$R/build.log` `dotnet build nextorm.slnx -c Debug` exit 0 / **0 Warning(s) 0 Error(s)**; `$R/unit.log` full project runs → core 2529/2529, sqlite 1747 (1 opt-in skip), clickhouse 733/733, all exit 0; `$R/integration.log` full container suite (`dotnet run … --no-build -- -noColor`, DOCKER_HOST) → Total 3904 / Errors 0 / Failed 0 / Skipped 198 / Not Run 0, exit 0, all five providers executed; boundary filtered rows: core 8/1/16, sqlite 48/25/21, integration `~RecordsAffected` 22/22, `~StoredProcedure` 1/1, `~ClickHouseRecordsAffected` 5/5, all exit 0 / 0 failed / 0 skipped; `$R/evidence.json` (12 boundary executions + inner-loop rows + coverage baseline block) and `$R/manifest.json` (all nine rows E226-01…09 **met** with real evidence paths); validators `python3 scripts/validate_inner_loop.py report $R/evidence.json` → **exit 0** and `… manifest $R/manifest.json` → **exit 0** after recording the CHECK scope amendment (`FullyQualifiedName~ClickHouseRecordsAffected`, `validated_before_run: true`) | `artifacts/pdca/D226/rv1/build.log`, `artifacts/pdca/D226/rv1/unit.log`, `artifacts/pdca/D226/rv1/integration.log`, `artifacts/pdca/D226/rv1/evidence.json`, `artifacts/pdca/D226/rv1/manifest.json`
- 2026-10-14 | ESCALATE | r=1 | n=2/3 | decision: corrective DO under r=1 as the final attempt n=3/3 (route coder, no r=2). Frozen mutants executed literally; verifier made fail-closed with negative controls; survivor verdicts re-derived from a post-fix Stryker run; traceability obligation→mutant→patch→killing test. r=2 only if a frozen mutant is proven unexpressible/equivalent → STOP with proof, never a silent substitution. | `docs/specs/status/D226-records-affected-1.md:132-135`, `artifacts/pdca/D226/rv1/mutation-report.json`

- 2026-10-14 | DO | revision r=1 | iteration n=3/3 | corrective unit 1 (final attempt) closed — `$R/verify-mutations.sh` rewritten fail-closed (`set -Eeuo pipefail`, no `| tee`, `trap EXIT` folding the restore status, `$R/mutation.exit`, per-mutant patch hash + before/after sha256 + killing test + assertion message) and the five FROZEN obligations executed literally: **5/5 killed by assertions** (remove-capture, total-to-row-count, sentinel-to-0, reset-on-repeated-release, disposed-reader-from-getter) plus five extra mutants (publish-after-release ×2, command-disposal, wrong-total, publish-pre-release) all killed; controls core 16/16 and sqlite 21/21 at exit 0; restore byte-identical | exit `verify-mutations.sh` = 0, `$R/mutation.exit` = 0, `$R/mutation.log`, `$R/mutation-report.json`, `$R/mutations/*.patch`
- 2026-10-14 | DO | revision r=1 | iteration n=3/3 | corrective unit 2 (negative controls) closed — five fail-closed controls, each a temporary perturbed copy with its artifact root redirected to a temp dir, **all nonzero**: `PATCH_NO_MATCH` (no-match needle), `SURVIVED` (comment-only no-op mutant), `GUARD-FAIL restore mismatch` (perturbed expected hash), `GUARD-FAIL core control is not 99/99` (failing control), missing log directory (invalid artifact path) | `$R/mutation-negative-controls.json`, `all_controls_nonzero=true`
- 2026-10-14 | DO | revision r=1 | iteration n=3/3 | corrective unit 3 (survivor verdicts) closed — no verdict rests on an unsubstantiated claim: the three former `killed` labels are now backed by hand-mutant patches + killing tests from this run (X1 `remove-publish-after-release-closereader` ↔ `CloseReader_PublishesTheTotal_PreferingThePostReleaseValue`, X2 `…-disposeasync` ↔ `AsyncRelease_PrefersThePostReleaseValue`, X3 `remove-command-disposeasync` ↔ `AsyncRelease_DisposesTheCommand`); the two `ConfigureAwait` verdicts stay `justified`, explicitly labelled reasoning (structural equivalence), never a kill. `stryker_re_run` records why a Stryker re-run was not reproducible: the in-tree `stryker-config.json` scopes Stryker to `nextorm.postgres`/`PostgresExtremeRowRenderer.cs`, not the T226 files, and the CHECK re-run's overrides are not recorded in the tree | `$R/mutation-report.json` (`stryker_re_run`)
- 2026-10-14 | DO | revision r=1 | iteration n=3/3 | corrective unit 4 (traceability) closed — frozen obligation (`docs/specs/status/D226-records-affected-1.md:132-135`) → mutant → patch → killing test, row by row: `remove capture` → `remove-capture` → `mutations/remove-capture.patch` → `RecordsAffectedTests.BeforeRelease_ReturnsSentinel_AfterDispose_ReturnsCapturedTotal`; `replace the total with row-count` → `total-to-row-count` → `mutations/total-to-row-count.patch` → `UnavailableMetadata_IsTheSentinel_AndNeverThrows` (owner) + `BatchMultipleResultTests.Execute_SideEffectBatch_…NotTheResultSetCount` (batch); `replace the sentinel with 0` → `sentinel-to-0` → `mutations/sentinel-to-0.patch` → `UnavailableMetadata_IsTheSentinel_AndNeverThrows`; `reset the cache on repeated release` → `reset-on-repeated-release` → `mutations/reset-on-repeated-release.patch` → `RepeatedReleaseAndGetter_AreIdempotent_AndDoNotThrow`; `read a disposed reader from the getter` → `disposed-reader-from-getter` → `mutations/disposed-reader-from-getter.patch` → `UnavailableMetadata_IsTheSentinel_AndNeverThrows` | `$R/traceability.json`
- 2026-10-14 | DO | revision r=1 | iteration n=3/3 | corrective unit 5 (final counters + seal) closed — core `~RecordsAffected` exit 0 16/16/0; sqlite compound `~RawCommand|~RawResultSetTraversal|~BatchMultipleResult` exit 0 94/94/0; integration `~RecordsAffected` (DOCKER_HOST) exit 0 27 selected / 27 passed / 0 failed / 0 skipped — 27 = 5 shared cases × 4 provider suites + 2 provider-specific procedure cases + the 5 `ClickHouseRecordsAffectedTests` cases, and the filter value is unchanged so the recorded row now carries the real count; integration `~ClickHouseRecordsAffected` exit 0 5/5/0; `verify-mutations.sh` exit 0 (`mutation.exit` 0); `verify-plan-evidence.sh` exit 0; `verify-coverage.sh $R/coverage.xml 85 75` exit 0 (line 88.7 % / branch 80.8 %); `validate_inner_loop.py report` exit 0 and `manifest` exit 0 (E226-09 now `met` from the new artifacts; the other eight rows unchanged and still `met`) | `$R/evidence.json`, `$R/manifest.json`, `$R/audit.log`, `$R/coverage-report/Summary.txt`

### r=2 DO progress (appended; existing lines untouched)

- 2026-10-14 | DO | r=2 | n=1/3 | P226-R2-01 done — rv2 staged: `r1-status.md` (pre-append snapshot), `inherited-contract.json` (all 9 inherited rows + 2 new variant rows, verbatim), `accepted-evidence.json`, `rv1-frozen.sha256` (48 rv1 artifacts), rv1 verifier machinery copied to `rv2/rv1-machinery/`; `verify-accepted-evidence.py --all` → exit 0 (E226-01..08 accepted with rv1 evidence paths re-checked; **E226-09 recorded NOT accepted** — the rv1 row-count patch counted columns/result sets, no upgrade); `--row E226-03` → exit 0 | `artifacts/pdca/D226/rv2/{inherited-contract.json,accepted-evidence.json,r1-status.md,accepted/*}`
- 2026-10-14 | DO | r=2 | n=1/3 | P226-R2-02 done — the frozen row-count obligation executed literally: `mutations/total-to-row-count.patch` generated from CURRENT bytes at `BatchRunner.cs:320,:349` (2 sites, 2 hunks, `git apply --check` exit 0), total replaced by `Enumerable.Sum(sets, set => set.Count)`; strengthened oracle (same method name) retains both lists once, asserts combined returned rows 3, then `RecordsAffected == 1`; clean control exit 0 (1/1) vs mutated exit 2 with **“Expected result.RecordsAffected to be 1, but found 3”** (expected 1 / actual 3) | `rv2/row-count-scope.md`, `rv2/mutation-traceability.json`, `rv2/mutations/total-to-row-count.patch`
- 2026-10-14 | DO | r=2 | n=1/3 | P226-R2-03 done — fail-closed verifier: base build gate, controls core 16/16 + sqlite row-count 1/1, per-restore ACTUAL byte comparisons persisted per mutant and in the final cleanup, report written after the final restoration, verified kill classifier with infrastructure rejection (verbatim rv1 tokens rejected); result `mutation.exit`=0, 8/8 mutants killed (5 frozen + X1–X3), restoration compared byte-identically, `guards=[]`; six negative controls all exit 1 for the intended reason incl. **N6 `infrastructure diagnostic present`**, aggregate exit 0 | `rv2/{verify-mutations.sh,mutation-runner.py,mutation.log,mutation-report.json,mutation.exit,verify-mutations-negative.sh,mutation-negative-controls.json,negative-controls/N6-*}`
- 2026-10-14 | DO | r=2 | n=1/3 | P226-R2-04 done — additive provenance erratum: X1–X3 hand-mutants substantiate the three former survivors; the scoped PRE-FIX Stryker output (`rv1/check/**`, present as history) is NOT a substantiated post-fix rerun (final-tree identity unverified); the two ConfigureAwait entries are justified reasoning, not kills; no `stryker_re_run` claim in rv2 records; rv1's invalid E226-09 not reused | `rv2/provenance.md`, `rv2/verify-provenance.py` → exit 0
- 2026-10-14 | DO | r=2 | n=1/3 | P226-R2-05 done — evidence slots with real child exits: build 0 (0 warnings/0 errors); core 16/16/0/0; sqlite `~RecordsAffected` 6/6/0/0; row-count control 1/1/0/0; integration `~RecordsAffected` 27/27/0/0; `~ClickHouseRecordsAffected` 5/5/0/0; `~Batch_SideEffect_…NotTheResultSetCount` 4/4/0/0 (DOCKER_HOST, serialised, no `-- -noColor` tail); E226-06 delta vs the r2 starting tree: production unchanged (4 files), branch-delta empty, nothing outside snapshot baseline + allowlist — `verify-r2-delta.py check` exit 0 | `rv2/*.log|.exit`, `rv2/{branch-*.patch,branch-delta.json,production-*-sha256}`
- 2026-10-14 | DO | r=2 | n=1/3 | P226-R2-06 done — seal candidate for CHECK (no PASS declared): `manifest.json` with **11/11 rows `met`** (E226-01…09 inherited + **E226-09-BATCH-ROWS** and **E226-09-N6**), `contract_rv: 2`, `required_rows` listing all 11, no open/na, each row carrying actual-artifact evidence; `validate-manifest.sh` (frozen argv in `validator-command.json`, argv+result in `validation.log`) → **exit 0**; `verify-plan-evidence.sh` → **exit 0, audit_miss=0**; `validate_inner_loop.py report evidence.json` → **exit 0**; production and rv1 bytes unchanged | `rv2/{manifest.json,evidence.json,audit.log,validation.log,validator-command.json,r2-summary.md}`

### r=2 DO corrective progress (attempt n=2/3; appended, existing lines untouched)

- 2026-10-14 | DO | r=2 | n=2/3 | report-regeneration blocker fixed (P1) — `mutation-runner.py` now emits the truthful `provenance` block **from the generator itself** (`build_provenance(records)`), so regenerating the report can no longer discard it: (i) X1–X3 as the evidence for the three former survivors (killing test + assertion + patch + kill_status read from that run's records), (ii) `stryker_post_fix_rerun: "not-substantiated"` with the reason (the scoped Stryker output under `rv1/check/**` is pre-fix and its final-tree identity is unverified), (iii) the two `ConfigureAwait` entries as `justified`/`basis: reasoning`, never kills, (iv) **no `stryker_re_run` key**. Determinism proven by two consecutive generator runs: provenance block sha256 `9ef7a2061d023309ae73c67fed707d5f634f4277f341dca7dab9dab02baba822` and whole-report sha256 `bc0d0a38103a2f51ad99c6a99da0546f8ec87415a0818c8956c5099d77b720b5` — **identical both times** | `rv2/provenance-determinism.json`, `rv2/mutation-report.json`
- 2026-10-14 | DO | r=2 | n=2/3 | contradictory application note corrected in the generator: `git apply` is a **CRLF no-op** in this worktree (`core.autocrlf=true`), so every mutant is applied through its parsed hunks while the patch artifact and the `git apply --check` exit-0 proof are retained, and the verifier's no-op guard reports the situation. The rv1 patches are copied byte-identically and applied through the same hunk mechanism.
- 2026-10-14 | DO | r=2 | n=2/3 | bookkeeping repairs — (G1) `evidence.json`: the six execution entries that pointed at the malformed empty-slot path `artifacts/pdca/D226/rv2/.log` now point at their real logs (`core-tests.log`, `sqlite-tests.log`, `row-count-control.log`, `integration-tests.log`, `clickhouse-tests.log`, `batch-integration-control.log`) with matching `.exit` files, and the build entry's `selected_count` is explicitly labelled as a contract-required integer, **not** a test-count claim. (G2) `negative-controls/N5-invalid-artifact-path.evidence.json`: `artifact_log` is now `null` with `artifact_log_status` stating that the log is **not produced by design** (the control deliberately points the artifact path at a non-existent directory) and linking the `.exit` evidence — no log file was fabricated.
- 2026-10-14 | DO | r=2 | n=2/3 | ordered re-seal after the LAST report regeneration, every step with its real exit code: `bash rv2/verify-mutations.sh` → **0** (`mutation.exit` 0); `bash rv2/verify-mutations-negative.sh` → **0** (six controls, each exit 1 for the intended reason); `python3 rv2/verify-provenance.py` → **0**; `bash rv2/run-evidence.sh audit -- bash rv2/verify-plan-evidence.sh` → **0** (`audit_miss=0`); `bash rv2/validate-manifest.sh` → **0**; `python3 scripts/validate_inner_loop.py manifest rv2/manifest.json` → **0**; `python3 scripts/validate_inner_loop.py report rv2/evidence.json` → **0**. Final report instance bound: `rv2/final-report.sha256` = `bc0d0a38103a2f51ad99c6a99da0546f8ec87415a0818c8956c5099d77b720b5`, and `rv2/validation.log` carries the ordered command list with argv, exit codes, timestamps and that bound hash, so the validation claims provably refer to THIS report instance. Manifest remains **11/11 rows `met`** (no row's evidence path changed except the refreshed `mutation-report.json`); production bytes unchanged (`CommandReaderOwner cf030526…`, `BatchRunner 9bf20094…`); rv1 byte-identical (0/48 changed). No PASS declared — CHECK owns the verdict.

- 2026-10-14 | CHECK-evidence reconciliation | r=2 | n=2/3 (attempt counter unchanged — no new DO revision) | **authoritative seal ledger now lives at `artifacts/pdca/D226/rv2/validation-ledger.{log,json}`**, a path no adapter owns. Root cause of the lost record: `rv2/validate-manifest.sh` is the **sole writer** of `rv2/validation.log` and line 20 (`} > "$R/validation.log" 2>&1`) **truncates** it, rewriting only its own five lines (argv, cwd, validator output, `result_exit_code`, `validated_at_utc`); the targeted CHECK re-run of that adapter therefore destroyed the previously appended seven-step ledger and its `bound_report`/`bound_report_sha256` lines. **Recovery: the original ledger was NOT recoverable** — a recursive content search for the lost markers (`bound_report_sha256`, `ordered re-seal`) across `artifacts/pdca/D226`, including the CHECK directories `rv2/check/**` and `rv2/check2/**`, found no file containing them; the seven-step record was therefore **freshly re-produced** with real exit codes and timestamps. Re-produced ledger (argv | exit | start→finish UTC, all **0**): `bash rv2/verify-mutations.sh` | 0 | 2026-10-14T15:0x | → `bash rv2/verify-mutations-negative.sh` | 0 | → `python3 rv2/verify-provenance.py` | 0 | → `bash rv2/run-evidence.sh audit -- bash rv2/verify-plan-evidence.sh` | 0 (audit_miss=0) | → `bash rv2/validate-manifest.sh` | 0 | → `python3 scripts/validate_inner_loop.py manifest rv2/manifest.json` | 0 | → `python3 scripts/validate_inner_loop.py report rv2/evidence.json` | 0; the adapter's own entry is quoted inside the ledger. **Bound report**: `artifacts/pdca/D226/rv2/mutation-report.json` sha256 `bc0d0a38103a2f51ad99c6a99da0546f8ec87415a0818c8956c5099d77b720b5`, re-written into `rv2/final-report.sha256` after the last regeneration and **equal to the recomputed hash** of the current report (`match=yes`). `rv2/validation.log` intentionally carries only the adapter's own record (its truncating behaviour); this line supersedes the earlier claim that the ordered ledger lived there — `S:291`'s claim is corrected here, not rewritten. Scope: no product/test/script edit; `sha256sum -c rv2/rv1-frozen.sha256` → **exit 0 (48/48 OK)**; the four product files unchanged (`CommandReaderOwner cf030526…`, `ProcedureResult a166586f…`, `BatchBuilder 9e5a15b1…`, `BatchRunner 9bf20094…`) with the accepted r=1/r=2 diff stat (4 files, 100 insertions, 5 deletions).
## CHECK PASS (r=2, rv=2, n=2/3) and ACT / EXIT

- **CHECK r=2 → PASS.** All 11 required rows `met` (E226-01…09 inherited + **E226-09-BATCH-ROWS** + **E226-09-N6**); C1–C5 met; no blockers; attempt counter `n=2/3` stands (no further attempt, no new revision). Accepted findings: the literal row-count obligation is satisfied by the two-hunk `Σ set.Count` substitution at `BatchRunner.cs:320/349` (killed with expected 1 / actual 3); the fail-closed verifier and N1–N6 are correct; production and rv1 bytes are byte-identical.
- **ACT / EXIT.** Status finalised; two commits prepared with explicit paths (`#226 expose captured RecordsAffected on result types`; `#226 preserve CHECK evidence and close T226`). Issue **#226 left OPEN** — closure is not authorised by the collection lane brief; issue disposition belongs to the collection owner. T53 freeze stays HELD.
- **Seven-step seal ledger** (authoritative: `artifacts/pdca/D226/rv2/validation-ledger.{log,json}` — `validation.log` holds only the adapter's own record because that adapter truncates its own file):
  1. `bash artifacts/pdca/D226/rv2/verify-mutations.sh` — exit 0 — 2026-10-14T15:07:21Z → 15:08:33Z
  2. `bash artifacts/pdca/D226/rv2/verify-mutations-negative.sh` — exit 0 — 15:08:33Z → 15:10:46Z
  3. `python3 artifacts/pdca/D226/rv2/verify-provenance.py` — exit 0 — 15:10:46Z → 15:10:46Z
  4. `bash artifacts/pdca/D226/rv2/run-evidence.sh audit -- bash artifacts/pdca/D226/rv2/verify-plan-evidence.sh` — exit 0 (`audit_miss=0`) — 15:10:46Z → 15:10:47Z
  5. `bash artifacts/pdca/D226/rv2/validate-manifest.sh` — exit 0
  6. `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D226/rv2/manifest.json` — exit 0
  7. `python3 scripts/validate_inner_loop.py report artifacts/pdca/D226/rv2/evidence.json` — exit 0
- **Bound report**: `artifacts/pdca/D226/rv2/mutation-report.json` sha256 `bc0d0a38103a2f51ad99c6a99da0546f8ec87415a0818c8956c5099d77b720b5` (rewritten into `final-report.sha256` after the last regeneration; ledger value == recomputed hash).
- **Byte-identity**: production `CommandReaderOwner cf030526…`, `ProcedureResult a166586f…`, `BatchBuilder 9e5a15b1…`, `BatchRunner 9bf20094…` (unchanged throughout r=2); rv1 `sha256sum -c rv2/rv1-frozen.sha256` → exit 0 (48/48).

### Done (T226, r=2)

- **C1 API/value** — exactly `public int RecordsAffected { get; }` on `ProcedureResult` and `BatchResult`; get-only (no setter), no row counting, no statement summing.
- **C2 availability** — sentinel `-1` while the reader is live; the captured total after release (incl. the `CloseReader`/output-parameters path).
- **C3 lifetime** — sync/async/early/repeated release, non-throwing cached getter, cleanup errors unmasked.
- **C4 batch** — one driver total through native `DbBatch` and the joined fallback; never the result-set or returned-row count.
- **C5 providers/boundaries** — five-provider matrix incl. the ClickHouse product surface (5/5 executed, 0 skipped); in-memory/unsupported guards unchanged.
- All 11 rows: **E226-01** API/get-only + build gate; **E226-02** capture; **E226-03** lifetime; **E226-04** batch; **E226-05** providers incl. ClickHouse; **E226-06** unsupported/delta (production unchanged); **E226-07** docs/invariants; **E226-08** coverage (line 88.7 % / branch 80.8 %); **E226-09** mutation (5 frozen mutants assertion-killed, every restore byte-compared); **E226-09-BATCH-ROWS** literal returned-row substitution (expected 1 / actual 3); **E226-09-N6** infrastructure rejection.
- D0–D5 **done** (table above); r=2 tasks **P226-R2-01…06 done**; corrective units n=2/3 done.

### Verified (evidence pointers)

- `artifacts/pdca/D226/rv2/manifest.json` (11/11 `met`, `contract_rv: 2`) — manifest validator exit 0 (`rv2/validation.log`, `rv2/validation-ledger.json`); `rv2/evidence.json` — report validator exit 0.
- `rv2/mutation-report.json` + `mutation.log` + `mutation.exit` (0) + `rv2/mutations/*.patch`; `rv2/mutation-negative-controls.json` (N1–N6 each exit 1 intended, aggregate 0); `rv2/traceability.json`, `rv2/mutation-traceability.json`, `rv2/row-count-scope.md`.
- `rv2/verify-mutations.sh`, `rv2/mutation-runner.py`, `rv2/verify-*.py`, `rv2/validate-manifest.sh`, `rv2/verify-plan-evidence.sh`, `rv2/audit.log` (`audit_miss=0`), `rv2/validation-ledger.{log,json}`, `rv2/final-report.sha256`.
- `rv2/{build,core-tests,sqlite-tests,row-count-control,integration-tests,clickhouse-tests,batch-integration-control}.{log,exit}`; `rv2/branch-delta.json`, `production-before/after.sha256`, `rv2-start`/`r2-start.json`.
- rv1 (immutable history): `rv1/check*/**`, `rv1/baseline/**`, `rv1/coverage.xml`, `rv1/mutation*`, `rv1/preflight.md`, `rv1/d1-observation.md`, `rv1/release-probe.json`, `rv1/provider-matrix.json`, `rv1/invariants.json`, `rv1/test-map.json`, `rv1/doc-review.md`.

### P2 erratum — provenance sub-hash withdrawn

The provenance **sub-hash `9ef7a206…`** recorded in `rv2/provenance-determinism.json` was produced with an **undocumented serialization method** (`json.dumps(..., sort_keys=True, separators=(',',':'))` over the sub-object) and is **not independently reproduced** by any other verifier, so it is **withdrawn as verification evidence**. The independent runs established report **byte reproducibility** with SHA-256 **`bc0d0a38103a2f51ad99c6a99da0546f8ec87415a0818c8956c5099d77b720b5`** (the whole `mutation-report.json`, identical across two consecutive generator runs and equal to the bound/validated instance). No method is invented and the unrelated `7a493880…` sub-hash is not substituted for it. The same erratum is appended to `artifacts/pdca/D226/rv2/provenance.md`.

## T53 freeze candidates — D226 (#226) registered (freeze stays HELD, not activated)

- `ProcedureResult.RecordsAffected` — `public int RecordsAffected { get; }` (`src/nextorm.core/DataContext/ProcedureResult.cs`), get-only, reads only the owner cache; no setter, no row counting.
- `BatchResult.RecordsAffected` — `public int RecordsAffected { get; }` (`src/nextorm.core/Builders/BatchBuilder.cs`), get-only, fed by the internal batch-runner snapshot.
- **2 members**, tag `#226`. Recorded here only (T53's own status file is deliberately not edited); the freeze itself is deferred, trigger: T53 starts in the current milestone. Internal additions that are explicitly **not** freeze candidates: `CommandReaderOwner.RecordsAffected`, `CommandReaderOwner.ReadRecordsAffected`, the `BatchResult` internal-ctor parameter.

- 2026-10-14 | DO | r=1 | docs stream scope amendment: documentation-only (no compiled input) → no test scope; validate_inner_loop brief is structurally unsatisfiable for docs-only (scripts/validate_inner_loop.py:111-116,235-249), gate unchanged for compiled-input streams.

## Notices

- Notice: host exposes no todowrite tool; the sidebar todo mirror is unavailable for this cycle.

- current_state: **closed** — CHECK PASS at r=2 (rv=2, n=2/3, attempt counter unchanged); 11/11 required rows met (E226-01…09 + E226-09-BATCH-ROWS + E226-09-N6); C1–C5 met; no blockers; production and rv1 bytes byte-identical; issue #226 left OPEN (closure not authorised by the collection lane brief).

## r=2 plan (sealed)

- artifact root R = `artifacts/pdca/D226/rv2/`; R1 = `artifacts/pdca/D226/rv1/` stays byte-identical forever.
- planner-sealed revision: r=2, cycle N=1, iteration n=1/3, evidence contract rv=2, plan_state ready.

**Goal.** Fulfill the literal frozen row-count obligation and make mutation verification fail closed — without reopening accepted product/provider/docs/coverage evidence. Unchanged acceptance criteria C1–C5 (`S:21-37`); rows E226-01…E226-08 remain accepted; production bytes must end unchanged.

**Minimal solution — the literal row-count mutant** (`$R/mutations/total-to-row-count.patch`), two substitutions only, at `src/nextorm.core/DataContext/BatchRunner.cs:320` and `:349`:
`- return new BatchResult(sets, resultTypes, columnNames, CommandReaderOwner.ReadRecordsAffected(reader));`
`+ return new BatchResult(sets, resultTypes, columnNames, System.Linq.Enumerable.Sum(sets, static set => set.Count));`
Observable substituted quantity = the sum of materialized `IList.Count` (a genuine returned-row count), not columns and not result sets. No `CommandReaderOwner` substitution.

**Primary killing oracle:** `tests/nextorm.sqlite.tests/BatchMultipleResultTests.cs:518 Execute_SideEffectBatch_ExposesOneDriverRecordsAffected_NotTheResultSetCount` — strengthen WITHOUT renaming: keep the two-result-set assertion, retain each returned list once and assert their combined count is **3**, assert `RecordsAffected == 1` independently of those rows. Clean control passes; the installed mutant fails the affected-total assertion with expected **1**, actual **3**. Additional oracle: `CommonTestSuite.BatchMultipleResults.cs:83-104` (2 rows vs total 1).

**Explicit limitation (`$R/row-count-scope.md`):** `CommandReaderOwner.cs:74-91` has no returned/read-row counter; `FieldCount` is a column count; the procedure read lists at `ProcedureResult.cs:149,375` are not retained; pure DML has no result set and `Read<T>` throws at `:226-227`; the batch lists at `BatchRunner.cs:311,338` provide genuine row counts. This does not make the obligation unexpressible — the batch mutation fulfils it literally; do not relabel a column-count substitution or fabricate a procedure counter.

**Tasks.** P226-R2-01 freeze/stage rv2 (`r1-status.md`, `inherited-contract.json`, `accepted-evidence.json`, verifier machinery copies + preserved patches, production/tree snapshot; proof `verify-accepted-evidence.py --all` + `verify-r2-delta.py snapshot`). P226-R2-02 literal row-count mutant/oracle (strengthen the SQLite oracle; `make-row-count-mutant.py`, `mutations/total-to-row-count.patch`, `row-count-scope.md`, `mutation-traceability.json`; patch generated against CURRENT bytes; `git apply --check` 0; clean/mutated runs). P226-R2-03 fail-closed verifier (restoration derivation with per-restore actual comparisons and report generated after final restoration; kill classifier requiring successful build + matching clean control + nonzero mutated exit with executed failed tests + the designated killing test bound to its failure record + expected/actual 1/3 (or 1/2) + no infrastructure diagnostic; infrastructure rejection takes precedence over apparent assertion evidence). P226-R2-04 provenance erratum (additive; correct the X1–X3 / Stryker claims in NEW records only; the scoped CHECK Stryker output exists at `R1/check/stryker-output/reports/mutation-report.json` but its identity against the final accepted tree is unverified, so it is NOT a substantiated post-fix rerun; the two `ConfigureAwait` entries stay justified reasoning; remove unsupported `stryker_re_run` references from newly generated records; never edit rv1). P226-R2-05 execute evidence (clean controls, frozen mutants sequentially with reversal before the next, negative controls, branch-delta checks; temporary patches only, byte-identical restore). P226-R2-06 seal candidate for CHECK (populate `$R/manifest.json`, `audit.log`, `validation.log`, `r2-summary.md`; reconcile all mandatory rows; append outcome without deleting r1 history; submit to CHECK — do NOT declare PASS).

**N6 negative control:** add `N6-infrastructure-failure` in the copied harness between the N5 block and the aggregate: retain a genuine assertion-failure record from a frozen mutant but inject `GUARD-FAIL: base build failed` into that invocation's evidence while keeping a nonzero exit and >=1 failed test → the verifier must exit 1 and classify infrastructure failure (not killed), produce no passing aggregate, and still restore the source. Preserve N1–N5; the aggregate harness exits 0 only when all six controls exit 1 for the intended reasons. Isolated copies under `/tmp/opencode/`; no worktree.

**Preserved mutants/provenance.** Copy the four remaining frozen patches byte-identically from rv1 (`cmp` 0 + matching SHA-256) with their killing tests: remove capture → `BeforeRelease_ReturnsSentinel_AfterDispose_ReturnsCapturedTotal` (3 vs -1); sentinel→0 → `UnavailableMetadata_IsTheSentinel_AndNeverThrows` (-1 vs 0); reset on repeated release → `RepeatedReleaseAndGetter_AreIdempotent_AndDoNotThrow` (8 vs -1); disposed reader from getter → `UnavailableMetadata_IsTheSentinel_AndNeverThrows` (unexpected `System.NullReferenceException`). Preserve X1–X3 (`remove-publish-after-release-closereader.patch` → `CloseReader_PublishesTheTotal_PreferringThePostReleaseValue`; `…disposeasync.patch` → `AsyncRelease_PrefersThePostReleaseValue`; `…remove-command-disposeasync.patch` → `AsyncRelease_DisposesTheCommand`).

**Variant closure.** procedure lifecycle / sync+async / sentinel+0+positive / direct-ctor 3-rows-2-sets-total-5 (not a runner-mutant kill) / executed batch 3 rows vs total 1 (strengthened SQLite oracle + literal runner mutant) / integration batch 2 rows vs total 1 / native+fallback sync+async (both runner sites patched; no claim that each site is independently killed) / five providers (accepted evidence + filtered clean controls) / ClickHouse (accepted 5/5 + preserved guards) / unsupported+in-memory (prior `NotSupported`) / procedure-path row-count mutation (documented unavailable quantity, proof above). No variant newly deferred; skipped providers never satisfy provider evidence.

**Verification invocations** (`run-evidence.sh SLOT -- CMD…` captures `$R/SLOT.log`, the real child exit in `$R/SLOT.exit`, propagates without pipeline masking): `verify-accepted-evidence.py --all`; build; core `~RecordsAffected`; sqlite `~RecordsAffected`; row-count control `~Execute_SideEffectBatch_ExposesOneDriverRecordsAffected_NotTheResultSetCount`; integration with DOCKER_HOST for `~RecordsAffected`, `~ClickHouseRecordsAffected`, `~Batch_SideEffect_ShouldExposeOneDriverRecordsAffected_NotTheResultSetCount`; `verify-mutations.sh`; `verify-mutations-negative.sh`; `verify-provenance.py`; `verify-r2-delta.py check`; `run-evidence.sh audit -- verify-plan-evidence.sh`; `validate-manifest.sh`. Expected: all top-level commands exit 0; build 0/0; clean tests nonzero selected / zero failed / no required-provider skip (core 16/16, integration 27/27, ClickHouse 5/5); mutated invocations nonzero WITH the designated assertion evidence; five targeted mutants assertion-killed with every restore succeeding; N1–N6 each exit 1 and the aggregate exits 0; audit zero MISS; no open required row. `validate-manifest.sh` is a strict adapter to `scripts/validate_inner_loop.py manifest` with frozen argv in `$R/validator-command.json` and argv+result in `$R/validation.log`. Do NOT append `-- -noColor` to `dotnet test --filter`; read the integration skill and recover the Podman socket if needed; skipped provider runs are not green.

**rv2 evidence contract.** rv2 supersedes rv1 prospectively, preserving ALL original requirement/row IDs (E226-01…09), priorities (P1), scenarios, negative cases and obligations; it does not rewrite rv1 verdicts. `$R/inherited-contract.json` copies the complete original row definitions. Frozen manifest `$R/manifest.json`, owner `DO-G1`, independent CHECK owns the verdict; every row applicable, no N/A; row invocations use `verify-accepted-evidence.py --row ID`. Two new variant rows: **E226-09-BATCH-ROWS / V226-BATCH-ROWS** (two-site returned-row substitution; rows 3 vs total 1, distinct from 2 sets; artifacts `mutations/total-to-row-count.patch`, `row-count-scope.md`, `mutation-traceability.json`, named mutant assertion/build/test/exit records) and **E226-09-N6 / V226-INFRA-REJECTION** (an infrastructure diagnostic cannot become a kill despite apparent assertion evidence; artifacts `mutation-negative-controls.json`, `negative-controls/N6-infrastructure-failure/`). `required_rows` = the original nine IDs plus the two variant IDs; no PASS with an open row; no blanket reference to rv1's invalid E226-09 as rv2 proof. E226-06 delta artifacts: `branch-before.patch`, `branch-after.patch`, `branch-delta.patch`, `branch-delta.json`, `production-before.sha256`, `production-after.sha256` against the r2 starting tree. CHECK re-gather budget: at most two targeted read-only batches, owner CHECK, routed by the orchestrator; no new mutation application or source edits during re-gather.

**Docs/perf/recon.** The four EN/RU guide pages and XML docs remain accepted and unchanged (only the additive status revision, provenance erratum, mutation scope and traceability are new); no new perf measurement (re-affirm `CommandReaderOwner.cs:16,31`, `BatchRunner.cs:296,321` with anchors `rv1/invariants.json:10`, `rv1/preflight.md:103`; the temporary row-summing mutant is not shipped); no further recon spike.

**History preserved (CHECK #1 → #2 → #3).** CHECK #1: E226-05/07/08/09 open. CHECK #2: E226-09 — the executed mutants differed from the frozen five. CHECK #3, r1 n=3/3: the "row-count" patch counted columns/result sets; literal row-count evidence absent; restoration reporting, classifier and provenance corrections also required. Never run a fourth CHECK under r1. Allowed permanent changes for r2: the existing SQLite test, additive `S`, and rv2 artifacts.

**Definition of done for r2.** All inherited obligations retained; five literal mutants assertion-killed; the true row-count oracle evidenced; actual restoration comparisons succeed; all six negative controls behave correctly; provenance truthful; production and rv1 bytes unchanged; audit and the installed manifest validator pass; all required rows met; **independent CHECK returns PASS**. Until then the original D stays active.

## Owner decision (2026-10-14)

- **Issue closed by the owner (done).** #226 CLOSED; task T226 r=2/rv=2, CHECK PASS, 11/11 rows met; `RecordsAffected` public on both result types (product commit `7cacee88`), T53 candidates registered (tag `#226`). Record: https://github.com/AlexeyShirshov/nextorm/issues/226
