# Cycle 3 — #183 cached-path iteration-15 (remaining: B1/B2/C)

- Task: #183; collection `1.0.9-b-5`; group-1; branch `collection/1.0.9-b-5/group-1`; initial tip `752943d7a8114c611832b210484f1d910026a863`.
- Plan revision `r=2`, iteration `n=1`. Evidence contract `rv=3`.
- Stage A: DONE @752943d (rv=2). B1: incomplete (r=1,n=1) — patch preserved. B2/C pending.
- Mode: autonomous + auto-commit authorized; no go-ahead pauses. Escalate is running on the #183 final-status rule; it does not block U1/U2.

## Goal
Resolve B1 honestly by re-measuring with the approved methodology; implement and validate B2 independently; complete C's decision/handoff without implementing another optimization.

## Acceptance criteria
- AC1 (B1): obtain ≥3 valid same-host paired/interleaved rounds; EITHER prove reproducibly lower B/op on all 4 target arms AND attributable target-stage speedup with no statistically reliable end-to-end regression; OR preserve the verified patch and record fallback (c). Negative: noisy/inconclusive timings never constitute acceptance.
- AC2 (B2): immutable, current-capture refresh preserves existing parameter/cache semantics and passes correctness + performance gates. Negative: retained first closure, double evaluation, incompatible recipe use, sticky `QueryCommand.Cache` mutation, or reliable regression rejects B2.
- AC3 (C): record remaining non-DB cost decomposition and a justified next decision. Negative: an unsupported "largest cost" assertion, or any implementation of M12 #3, fails C.
- AC4 (delivery): isolated branch, complete evidence, authorized commits only, no push/merge. Negative: skipped providers, missing evidence, or work outside group-1.

## Units (sequential U1 → U2 → U3, one tree = this worktree)
- **U1 (D1) B1 re-measurement** — fix now. Footprint: preserved patch `benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/b1-incomplete.patch` (verify sha256 `0be74f92aa4250c78a566849dc1574d97c1c74aa154e18f56650501e34a7dc50` before apply) touching `src/nextorm.core/DataContext/Cache/QueryPlan.cs`, `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs`, and 3 core tests. Methodology: same host/job/config, matched warmup/build settings, ≥3 interleaved pairs A1→B1→A2→B2→A3→B3, alternating only baseline↔patched candidate; export raw measurements, per-arm B/op, median, dispersion, paired time deltas, 99.9% CI, and actual 1-min loadavg per run. Decision: accept+commit ONLY per AC1; otherwise fallback (c): revert, preserve verified patch, mark B1 incomplete/unaccepted/uncommitted, continue.
- **U2 (D2) B2 guarded immutable refresh recipe** — fix now. Footprint: `src/nextorm.core/DataContext/QueryPlanner.cs` (58–63, 506–532, 644–658, 685, 696–739), immutable recipe metadata in the appropriate existing plan/cache footprint, plus characterization/core/integration tests (`tests/nextorm.sqlite.tests/CachedPathCharacterizationTests.cs` primary; `PlanCacheTests.cs`, `InListCacheTests.cs`, `PlanKeyUniquenessTests.cs`; `tests/nextorm.core.tests/{PlanKeyStructureTests,QueryCacheControlsTests,BaseCommandParameterCreationTests,DbPreparedQueryCommandFactoryTests}.cs`; integration `tests/nextorm.integration.tests/CommonTestSuite.Cache.cs` + per-provider). Implement: build recipe on miss binding CURRENT captures; validate structural/name/order/count compatibility BEFORE evaluating values; preserve dedup, conversion, null/default, stable/runtime semantics; unsupported converter/rawSQL/nested/IN forms use original `ExtractParams` BEFORE evaluation (no double evaluation); refresh IN/lookup/filter/prewhere/nested shapes before plan selection; runtime mismatch-safe fallback replaces `Debug.Assert`-only protection; no fresh-closure capture, no mutable `DbCommand`/enumerator retention; "recipe does not use the first closure" isolation is mandatory. Independent baseline, gates, rollback patch, commit; must not depend on B1's outcome.
- **U3 (D3) C decision/handoff** — fix now. Deliverable: `docs/specs/performance/iteration-15-cached-path-results.md` recording accepted/fallback stages, decomposition, uncertainty, limitations, #183 disposition. If `PrepareCommand` is the largest remaining non-DB cost distinguishable above measurement uncertainty → produce separate written `docs/specs/performance/m12-3-prepare-command-design.md` and hand off for review+PLAN; otherwise document the measured reason not to proceed. NO M12 implementation.

## Risks
- Recipe compatibility only `Debug.Assert` at `QueryPlanner.cs:736` → needs runtime mismatch-safe fallback.
- Dedup key pins first closure (`Parameter.cs:36`, `BaseExpressionVisitor.cs:245-249`); conversion happens during eval (`BaseExpressionVisitor.cs:115-138`); `NeedsParamRefresh`/`NoParams` frozen (`QueryPlanner.cs:644-658,685`); reference-keyed IN/lookup (`InValuesTranslator.cs:68-73`, `DictionaryLookupTranslator.cs:70-76`); `RawSqlOverride` (`:717-720`); `[ThreadStatic] QueryPlanStore` (`QueryPlanStore.cs:31-35`); mutable `DbPreparedQueryCommand` (`DbPreparedQueryCommand.cs:22-34,75`).
- Noise may prevent B1/U2 time attribution; thread-static store and shared cached commands must not leak into recipes.

## Test strategy
- Unit (DB-free) characterization first: `dotnet test tests/nextorm.core.tests -c Debug` and `dotnet test tests/nextorm.sqlite.tests -c Debug` with focused selectors. Full solution suites next. Explicit container integration last.
- Variant matrix — **test**: same/different fresh reference closures; value captures; null/default; parameter-free/nonempty transitions; stable/runtime split; duplicate captures; pN order/count; `PreparedCommandOptions` combinations; repeated/context-isolated hits (assert values AND evaluation counts).
- Variant matrix — **guard+test**: incompatible name/order/count/shape; converters/provider-bound types; raw SQL; nested queries; IN empty/nonempty/changed; lookup changes; filters/prewhere changes — all verify original-path fallback BEFORE evaluation and no double evaluation.
- Variant matrix — **providers**: DB-free full solution covers available dialects; SQLite + PostgreSQL/SQL Server/MySQL/ClickHouse integration mandatory. MariaDB real-server execution deferred until a dedicated server; never claim it ran.
- Branch delta: inventory hit-refresh/fallback/options branches vs `QueryPlanner.cs:644–658,696–699,717–739`; report before/after branch hits; coverage line ≥85 / branch ≥75.
- Mutation: Stryker 5.0.0 absent / MTP-incompatible → manual-justification route; seed reversible faults in compatibility, first-capture binding, dedup/order, fallback-before-evaluation; targeted tests kill each; restore and rerun gates; justify any unseeded invariant.
- Priority (P1 by construction, CHECK must not downgrade): acceptance invariants; current-capture isolation; runtime mismatch guard; fallback/evaluation ordering; shape-before-selection; sticky-cache prohibition; provider conversion; performance regression gates.

## Docs plan
- Internal results/handoff + status only (`-results.md`, C's `m12-3-*` if justified). Do NOT reopen the approved design review. No public API change planned → public EN/RU untouched; any proposed public API change returns to PLAN with an EN/RU obligation.

## Perf-measurement decision
- Required: cached-path work is per-query (`QueryPlanner.cs:727–739`), not one-time. B2 baseline is accepted B1, else Stage A. Measure allocation and attributable refresh time independently. Acceptance = exactly 7 cases, each complete invocation ≤240 s.

## Reconnaissance decision
- Narrow scout prerequisite: confirm global/overlay/integration-skill rules, tool commands, benchmark attribution and gate artifacts; Roslyn-only for symbols. Add measurement-only attribution instrumentation if existing exports do not separate target cost. No design reopen.

## Unit execution mode
- Sequential U1→U2→U3 in this existing worktree/branch; no additional worktrees.

## Evidence contract rv=3 (supersedes rv=2 prospectively; Stage-A obligations/evidence retained unchanged)
- Common slots per row: requirement/row ID; scenario; command; result; artifacts; owner; applicability. `E=benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining`; all commands in the worktree with captured stdout/stderr and exit codes.
- Planned gate script: `bash "$E/gates.sh" Ux` → `dotnet build nextorm.slnx -c Debug` (0/0), both unit selectors, full solution tests, `timeout 240s dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` (exactly 7 passes), coverage collect/report (commands copied from `.github/workflows/dotnet.yml`), integration below; every component exit 0.
- Integration: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`; documented Podman recovery if needed; four DBs executed, zero failures, skips≠pass.
- Artifacts: `$E/Ux/{build,unit,full-tests,acceptance,integration,coverage,structural-audit}` logs/reports, threshold summary, seven-case/duration manifest; structural audit proves isolation, CRLF, CPM, no public API change, no sticky `QueryCommand.Cache` mutation.
- Rows:
  - `G1-PERF/U1` — B1 attribution: `bash "$E/measure.sh" U1`; ≥3 valid interleaved pairs, statistics + load; accepted predicate above OR explicit fallback (c). Artifacts `$E/U1/perf/`. Owner U1.
  - `G1-CACHE/U1` — `bash "$E/gates.sh" U1-candidate`; and `U1-delivered` if reverted; all G. Artifacts per-gate dirs. Owner U1.
  - `G1-PERF/U2` — `bash "$E/measure.sh" U2`; same ≥3-pair methodology; reproducibly lower B/op + attributable refresh speedup, no reliable regression. Artifacts `$E/U2/perf/`. Owner U2.
  - `G1-CACHE/U2` — `bash "$E/gates.sh" U2`; G passes, changed branches covered; `bash "$E/mutations.sh" U2` exits 0 only with killed-fault/restoration report. Artifacts `$E/U2/`. Owner U2.
  - `G1-PERF/U3` — `bash "$E/decompose.sh" U3`; evidence-backed largest-cost predicate; `bash "$E/gates.sh" U3` passes. Owner U3.
  - `G1-CACHE/U3` — `bash "$E/gates.sh" U3`; unchanged accepted behavior + all G. Owner U3.
  - `G1-CTE/U1-U3` — `bash "$E/scope-check.sh" U1 U2 U3`; exit 0 only when the guard is untouched, else stop/re-scope; never N/A without proof. Owner U1–U3.
- CHECK budget: ≤2 targeted re-gather requests per CHECK invocation; unresolved missing evidence cannot pass and does not itself justify DO/revision. Revision discipline: any later row obligation/applicability change goes through CHECK→PLAN with explicit supersession, retained IDs, new IDs.
- Deferred: #166 CTE work — trigger = any touch of `CteHoister.Hoist` / nested read-CTE `PrepareCtes`; then stop that scope, re-scope, run #166 first. Broader refresh support stays guarded fallback.

## Progress log
- PLAN complete: plan r=2, evidence contract rv=3; awaiting escalate decision on #183 final-status rule (non-blocking for U1/U2).
- Escalate decision (a) routed: #183 may be DONE-with-limitation after B2/C; B1 stays incomplete/unaccepted/uncommitted; U2 independent of B1; proceed U1 measurement now.
- U1 done: 3 interleaved rounds; alloc −43,202 B/op on 4 cached-hit arms (reproducible); time not attributable (within CI); no reliable regression → fallback (c) applied; B1 incomplete/unaccepted/uncommitted; tree at base 752943d. Proceeding to U2.
- U2 warrant adjudicated (a) proceed under guardrail; U2 DO started.
- U2 reverted per CHECK FAIL; patch+evidence preserved; delivered tree = base 752943d; proceeding to U3 (C).
- r=3/rv=4 reconciliation written: AC2 superseded as rejected-with-limitation (AC2-REJECT-01); D1–D3 evidence manifests attached; awaiting CHECK re-gather.

## Escalation decision (routed) — #183 final-status rule
- Decision: (a) #183 may close as DONE (with limitation) after B2/C; B1 stays explicitly incomplete / unaccepted / uncommitted (attributable target-stage time speedup unmet). Group-1 is NOT stopped by B1; other tasks are not marked incomplete on this account.
- Rationale: design `:201-203` is conjunctive so B1 is not accepted; but the `loadavg<1.0` precondition and fallback (c) are NOT in the approved design (`design` has no such text) and therefore are not a gate. "Unattributable time" is an acceptable negative experimental outcome, not a task failure. Owner explicitly authorized not stopping the group on the timing metric (owner-scope decision; does not waive security/gates/no-4th).
- Conditions imposed: (1) quote the owner directive in issue #183 and in this status; (2) record confirmed facts (B/op −432 on 4/4 arms ×2 rounds; no statistically reliable time regression) and unconfirmed facts (attributable target-stage speedup, `...-b1.md:384-402`); (3) do NOT merge/commit B1; preserve `b1-incomplete.patch` (sha256 `0be74f92…`) as artifact; (4) B2 built from base WITHOUT B1, its own gates mandatory; (5) C records B1 as negative/inconclusive + patch path + open requirement to redo on a quiet host / with process control — never "B1 speeds up"; (6) if B2/C itself fails, ordinary no-4th/group-stop rules apply.
- Owner directive quoted: "autonomy + auto-commit authorized; ... if the quiet-host precondition stays unmeetable and the design permits, apply the documented fallback and continue with B2/C rather than re-blocking — record the limitation; do not stop the group merely on the timing metric."
- Accepted final statuses: #183 = DONE (with limitation); B1 value NOT counted; no "accepted"/"speedup proven" wording.

## U1 outcome — B1 re-measurement (3 interleaved rounds) → fallback (c)
- Method: 3 interleaved rounds A1→B1→A2→B2→A3→B3, same host/job/config (Ryzen 7 5800HS 4C/8T, ShortRun/InProcessEmit, BDN 0.15.8). Patch sha256 `0be74f92…` verified; `git apply --check` exit 0. All 6 runs exit 0. 1-min loadavg per run: A1 6.54, B1 11.33, A2 6.52, B2 6.05, A3 6.25, B3 5.60 (start 5.80).
- Allocations (fact): candidate allocates ~43,202 B/op (~43.2 KB) LESS deterministically in all 3 rounds on exactly the 4 cached-hit arms: Where_CachedHit_PlanOnly, Where_CachedHit_ToList, Join_CachedHit_PlanOnly, Join_CachedHit_ToList. All other arms Δalloc = 0. Unchanged from B1's original result.
- Time (fact): NOT attributable. Only Where_CachedHit_PlanOnly (med −94.0 µs, range 19.7) and Where_CachedHit_ToList (med −98.0 µs, range 342) keep a consistent negative sign, but both deltas sit far inside the combined 99.9% CI (2021.5 µs and ~5349 µs respectively). All other arms flip sign. No statistically reliable end-to-end regression >20% (largest 3-round mean +12.9%, not CI-separated). Interpreter: `dotnet-performance-analyst` (read-only).
- Decision per pinned AC1: speedup condition unmet → **fallback (c)**: B1 stays incomplete / unaccepted / uncommitted; tree returned to base `752943d`; patch preserved at `benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/b1-incomplete.patch` (sha256 `0be74f92…`). Owner directive + escalate decision (a) honored: group continues.
- Evidence: `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U1/perf/` (run-A*/run-B*.log, summary.json).

## U2 warrant (DO→PLAN, routed)
- Planner decision (a): proceed with U2 as a guarded experiment. The repeatable Stage-A diagnostic 1343.916 B/op warrants evaluating the recipe; it does NOT prove adoption. Keep/commit U2 only if its own exact-isolation contribution is confirmed and all gates pass; otherwise guard/fallback or revert, preserve patch/evidence.
- No plan revision: r=2, rv=3, attempt counters unchanged; U2 acceptance/variant matrix/test scope binding.

## U2 outcome — CHECK FAIL → reverted (guardrail)
- CHECK verdict FAIL (medium): AC1 met (B1 fallback correctly recorded); AC2 unmet — allocation win confirmed (−112,802 B/op deterministic on the two Where cached-hit arms) but the design's conjunctive gate also requires an attributable target-stage speedup, which is not attributable on this host; test-adequacy gap (isolation test does not prove the fast path ran); AC3 unmet (C not started); AC4 unverified.
- Ruling: revert U2, preserve patch + evidence, do not commit as accepted; no acceptance relaxation, no PLAN revision.
- U2 patch preserved: `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U2/u2-incomplete.patch` (sha256 reported in log). U2 evidence: `.../U2/perf/`, coverage, integration logs.
- Delivered tree = base 752943d (Stage A); fresh evidence recorded.
- U3 (C) DO started: results/handoff deliverable.

- U3 correction: integration failure attribution aligned to artifact evidence.

---
## Plan revision r=3 / evidence contract rv=4 — closeout reconciliation
- Revision `r=3`, `n=1`, contract `rv=4`; autonomous. `rv=4` supersedes `rv=3` **U2 acceptance disposition only** (AC2). Every other stable requirement/row ID, obligation, command, predicate, artifact requirement, owner and budget is preserved. All unconditional requirements (security, correctness gates, no-fourth-attempt, scope, sticky-cache ban) remain in force; **no waiver**.
- **Supersession:** AC2's identity and original conjunctive gate are unchanged; outcome variant **AC2-REJECT-01** = B2 was evaluated, failed the conjunctive performance gate on attributable speedup, and was rejected/reverted under `design:141`; this is **not** an unmet product defect and does **not** retroactively pass the gate.
- **Ledger:** U1/B1 = incomplete, unaccepted, uncommitted, authorized fallback (c), patch preserved. U2/B2 = incomplete, unaccepted, rejected/reverted, patch+evidence preserved. U3/C = delivered + verified. Neither B1 nor B2 is counted as delivered value.
- **#183 disposition:** `DONE (with limitation)`, authorized by escalate decision (a) + owner directive, subject to a passing CHECK.

### rv=4 row dispositions (binding = the original pinned predicate; planned helper scripts `measure.sh`/`gates.sh`/`mutations.sh`/`decompose.sh`/`scope-check.sh` were never created — the equivalent commands were run directly; exact invocations/logs are in the D2 manifests below)
- **G1-PERF/U1 — satisfied via authorized fallback (c).** 3 interleaved rounds (A1→B1→A2→B2→A3→B3), all 6 runs exit 0; deterministic −43,202 B/op on the 4 cached-hit arms; attributable target-stage speedup unproven; no statistically reliable regression. Artifacts `$E/U1/perf/`.
- **G1-CACHE/U1 — unverified/pending.** The rv=3 candidate/delivered gate was not separately executed for U1 because U1 was never committed (patch on disk only; tree at base); the delivered base tree's gates are covered by G1-CACHE/U3. Not relabelled satisfied; no DO iteration charged for a missing report.
- **G1-PERF/U2 — failed + superseded (AC2-REJECT-01).** Allocations −112,802 B/op deterministic (3 rounds) on `Where_CachedHit_PlanOnly`/`Where_CachedHit_ToList`, 0 on the Join arms; time not attributable; acceptance 7/7 exit 0 in 50 s. Artifacts `$E/U2/perf/`.
- **G1-CACHE/U2 — moot for retained implementation by verified revert**, with candidate correctness evidence preserved (build 0/0; core 1482/0; sqlite 993 pass/0 fail/1 env skip; coverage 87.1/78.7; 4 providers isolated 0-fail). Not erased, not relabelled N/A.
- **G1-PERF/U3 — N/A** (docs-only unit; scoped diff has no code). Decomposition decision at `docs/specs/performance/iteration-15-cached-path-results.md:41`.
- **G1-CACHE/U3 — N/A** (docs-only unit). Delivered-tree gates (base) green: build 0/0; core 1482/0; sqlite 993 pass/0 fail/1 env skip.
- **G1-CTE/U1-U3 — N/A**: binding = CTE-touch predicate; `CteHoister`/`Hoist`/`PrepareCtes` untouched (`git diff` empty).

### D1 — pinned rv=3 contract/ledger
- The rv=3 row block above in this file is retained verbatim as superseded history (not overwritten); rv=4 dispositions bind to those same row IDs.

### D2 — execution manifests (exact invocations, exit codes, artifact paths)
- build: `dotnet build nextorm.slnx -c Debug` → exit 0; `Build succeeded. 0 Warning(s) 0 Error(s)`; `$E/U2-delivered/build.log:28-32`.
- core: `dotnet test tests/nextorm.core.tests -c Debug` → exit 0; total 1482 / succeeded 1482 / failed 0 / skipped 0; `$E/U2-delivered/core.log:4-8`.
- sqlite: `dotnet test tests/nextorm.sqlite.tests -c Debug` → exit 0; total 994 / succeeded 993 / failed 0 / skipped 1 (`SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`, env-gated by `NEXTORM_LOB_SQLITE_PROBE=1`, not a failure); `$E/U2-delivered/sqlite.log:2-11`.
- U2 candidate inner/boundary/revert logs: `$E/U2/{build.log,inner-core-selectors.log,inner-sqlite-selectors.log,boundary-core-full.log,boundary-sqlite-full.log,dbg.log}`; patch `$E/U2/u2-incomplete.patch` sha256 `dd876ae9ed5b68a4fe3804478194db64741fb39bde3bebae24edb21899d18b7f`, recorded `$E/U2/u2-incomplete.sha256`.
- coverage: workflow-reproduced `dotnet-coverage collect -s coverage.settings.xml -f cobertura` → `reportgenerator` → Line 87.1% (44396/50968), Branch 78.7% (22968/29164); `$E/U2/coverage/report/Summary.txt:7,12`; logs `$E/U2/coverage/{collect.log,reportgen.log}`; assemblies nextorm.core 87.3 / postgres 79.2 / sqlite 89.3 / sqlserver 78.2.
- integration isolated (same U2 binaries): PostgreSQL 776 total/0 failed/25 skipped `$E/U2/integration-postgres.log:79-83`; SQL Server 698/0/43 `integration-sqlserver.log:133-137`; MySQL 656/0/80 `integration-mysql.log:244-248`; ClickHouse 173/0/0 `integration-clickhouse.log:4-8`. Combined run `integration.log:15068` = Total 3136, Errors 0, Failed 663, Skipped 191, 633.8 s; failure breakdown 653 SQL Server pre-login handshake (`SqlException`, `Win32Exception: Unknown error 258`) + 1 MySQL + 9 ClickHouse; OOM not confirmable and not claimed; documented as environmental, not a U2 regression (isolated runs on the same binaries are green).
- U2 perf: `$E/U2/perf/run-{A1,A2,A3,B1,B2,B3}.log`, `run-*-full.json`, `summary.json` (alloc medians `:332,:349` = −112,802 B; `:366,:383` = 0; time medians within CI), `acceptance.log` (7/7 exit 0, 50 s).
- U1 perf: `$E/U1/perf/run-{A1..B3}.log`, `summary.json`; 6 runs exit 0; alloc median −43,202 B/op on 4 cached-hit arms; patch `benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/b1-incomplete.patch` sha256 `0be74f92aa4250c78a566849dc1574d97c1c74aa154e18f56650501e34a7dc50`.
- U3: `docs/specs/performance/iteration-15-cached-path-results.md` (independent scout verification confirmed the numeric claims; the combined-integration attribution was corrected to evidence-only; no `m12-3-prepare-command-design.md` created).

### D3 — branch/base/scope/authorization
- Branch `collection/1.0.9-b-5/group-1`; base/HEAD `752943d7a8114c611832b210484f1d910026a863`.
- `git diff --stat -- src tests` = empty (delivered tree has no code change); `git status --short` = untracked docs/status/artifacts only.
- U1 working-tree status: the B1 patch was never left applied — it was applied and reverted within the measurement rounds; the tree is at base.
- Authorization: owner granted autonomous + auto-commit for collection `1.0.9-b-5` (recorded in the shared `docs/specs/status/collection-1.0.9-b-5.md`, group-1 row).

### r=3 units
- D1 (fix now) pinned rv=3 contract/ledger — done above. D2 (fix now) execution manifests — done above. D3 (fix now) branch/base/scope/authorization — done above.
- CHECK re-gather: one targeted batch owned by `check`. ACT allowlist: `iteration-15-cached-path-results.md` + this status file only; B1/B2 patches remain uncommitted. Issue action: comment/close #183 as DONE (with limitation) after a passing CHECK.
---
- ACT: CHECK PASS (r=3/rv=4 reconciled) → #183 DONE (with limitation); committing results.md + this status file.

## Contract revision rv=5 (r+1, collection C closeout)
P: Close the U1 applicability gap without weakening delivered-tree acceptance; plan r+1, evidence contract rv=5.
Supersession: rv=5 supersedes rv=4 ONLY for the G1-CACHE/U1 applicability predicate. Every other stable requirement/row ID, obligation, verification scenario, evidence kind/source, exact command, exit/result/log requirement, artifact requirement, owner, predicate and CHECK re-gather budget is preserved verbatim.
- G1-CACHE/U1 — N/A; predicate: "unit not in the delivered tree; B1 patch sha256 0be74f92… preserved on disk, not delivered"; owner CHECK; rv=5.
- G1-PERF/U1 — satisfied via authorized fallback (c); unchanged.
- G1-PERF/U2 — superseded (AC2-REJECT-01); unchanged.
- G1-CACHE/U2 — moot (verified revert); unchanged.
- G1-PERF/U3 — N/A; G1-CACHE/U3 — N/A; unchanged.
- G1-CTE/U1–U3 — N/A, CTE-touch predicate false; unchanged.
Delivered-tree gates re-run fresh on `13d94a0` (code-identical to `52c4b3a`) → `/tmp/opencode/coll-c/rv5/`:
- build `dotnet build nextorm.slnx -c Debug` → exit 0; `Build succeeded. 0 Warning(s) 0 Error(s)`; log `/tmp/opencode/coll-c/rv5/build.log`.
- core `dotnet test tests/nextorm.core.tests -c Debug` → exit 0; Total 1482 / Passed 1482 / Failed 0 / Skipped 0; log `/tmp/opencode/coll-c/rv5/test-core.log`.
- sqlite `dotnet test tests/nextorm.sqlite.tests -c Debug` → exit 0; Total 1003 / Passed 1002 / Failed 0 / Skipped 1 (env-gated `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`, `NEXTORM_LOB_SQLITE_PROBE=1`; skip is not a pass); log `/tmp/opencode/coll-c/rv5/test-sqlite.log`.
- docs lens retained; no `src/**` change. NO performance improvement is claimed. Disposition remains DONE (with limitation).

---
### rv=5 evidence appendices
Consolidated per-row evidence for the rv=5 closeout (append-only; the rv=5 dispositions at lines 141-154 are not revised). Paths: `$E=benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining`; collection worktree `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-1`.

#### Variant matrix + priority floor — `iteration-15-cached-path-183-3.md:29-34` (verbatim)
- [29] Variant matrix — **test**: same/different fresh reference closures; value captures; null/default; parameter-free/nonempty transitions; stable/runtime split; duplicate captures; pN order/count; `PreparedCommandOptions` combinations; repeated/context-isolated hits (assert values AND evaluation counts).
- [30] Variant matrix — **guard+test**: incompatible name/order/count/shape; converters/provider-bound types; raw SQL; nested queries; IN empty/nonempty/changed; lookup changes; filters/prewhere changes — all verify original-path fallback BEFORE evaluation and no double evaluation.
- [31] Variant matrix — **providers**: DB-free full solution covers available dialects; SQLite + PostgreSQL/SQL Server/MySQL/ClickHouse integration mandatory. MariaDB real-server execution deferred until a dedicated server; never claim it ran.
- [32] Branch delta: inventory hit-refresh/fallback/options branches vs `QueryPlanner.cs:644–658,696–699,717–739`; report before/after branch hits; coverage line ≥85 / branch ≥75.
- [33] Mutation: Stryker 5.0.0 absent / MTP-incompatible → manual-justification route; seed reversible faults in compatibility, first-capture binding, dedup/order, fallback-before-evaluation; targeted tests kill each; restore and rerun gates; justify any unseeded invariant.
- [34] Priority (P1 by construction, CHECK must not downgrade): acceptance invariants; current-capture isolation; runtime mismatch guard; fallback/evaluation ordering; shape-before-selection; sticky-cache prohibition; provider conversion; performance regression gates.
- [88] No plan revision: r=2, rv=3, attempt counters unchanged; U2 acceptance/variant matrix/test scope binding.

#### G1-PERF/U1 — fallback-(c) support
`183-3.md:107` (verbatim):
- **G1-PERF/U1 — satisfied via authorized fallback (c).** 3 interleaved rounds (A1→B1→A2→B2→A3→B3), all 6 runs exit 0; deterministic −43,202 B/op on the 4 cached-hit arms; attributable target-stage speedup unproven; no statistically reliable regression. Artifacts `$E/U1/perf/`.
Allocation (verbatim, `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U1/perf/summary.json`):
- `:271` `"alloc_delta_median_bytes": -43202.6` (`Where_CachedHit_PlanOnly`)
- `:577` `"alloc_delta_median_bytes": -43202.6` (`Join_CachedHit_PlanOnly`)
- `:628` `"alloc_delta_median_bytes": -43202.6` (`Join_CachedHit_ToList`)
- `:322` `"alloc_delta_median_bytes": -43192.3` (`Where_CachedHit_ToList`)
File path: `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U1/perf/summary.json`.

#### G1-CACHE/U2 — revert trace
`183-3.md:110` (verbatim):
- **G1-CACHE/U2 — moot for retained implementation by verified revert**, with candidate correctness evidence preserved (build 0/0; core 1482/0; sqlite 993 pass/0 fail/1 env skip; coverage 87.1/78.7; 4 providers isolated 0-fail). Not erased, not relabelled N/A.
Delivered tree == base; NO `src/**` change in the #183 collection/closeout range:
- `git diff --stat 5d9b8d8..HEAD -- src/ 2>/dev/null | tail -1` → `206 files changed, 33691 insertions(+), 1361 deletions(-)` — `5d9b8d8` is the unrelated PR #91 merge, NOT the collection base; not applicable.
- `git diff --stat 82e344f..HEAD -- src/` → `5 files changed, 243 insertions(+), 40 deletions(-)` — attributable solely to pre-existing `158233f #186 Unify result-set traversal` (not #183/#164); not a collection change.
- Collection-base evidence: `git diff --stat 752943d..3b1fcaf -- src/` = empty (group-1 delivered tip `3b1fcaf` == base `752943d`); closeout `git diff --stat 52c4b3a..HEAD -- src/` = empty and `git diff --stat 13d94a0..HEAD -- src/` = empty. → NO `src/**` change in the #183 collection/closeout range.
- `git status --short --branch` → `## 1.0.9-b...origin/1.0.9-b [ahead 14]`; only pre-existing untracked/modified docs, no staged code.

#### G2-LIVE / G2-LIVE-B — applicability to this snapshot
`pdca-evidence-164-1.md:260-269` (verbatim):
- [260] `## CHECK verdict`
- [262] `PASS (rv=1): (a) met — loaded-skill spill tool_10746e894001aaPVuuBcdcnZt4:884/1083 in post-restart session ses_efa0237e9ffe6w6pOI50N73e5H; (b) met — real planner run ses_ef81912e9ffez8Z0xiwWjVCaU6 recorded rv=1 (G2-LIVE/G2-LIVE-B) with trace artifact sha256 eb6525b462dd07305f9cc0366c02a728b5c1d045a5764ee63bc41218a48215e2.`
- [264] `## ACT`
- [266] `## Done / Verified / Incomplete`
- [268] `Done: #164 G2-LIVE + G2-LIVE-B`
- [269] `Verified: CHECK PASS rv=1`
The files referenced by that verdict are outside the repo and unchanged by collection `1.0.9-b-5`: `/home/alex/.config/opencode/skills/pdca-dotnet/SKILL.md`, `/home/alex/.local/share/opencode/tool-output/tool_10746e894001aaPVuuBcdcnZt4`, `/home/alex/.config/opencode/agents/pdca-check.md`. No repo diff touches them; G2-LIVE/G2-LIVE-B is agent-runtime applicability, not SQL-provider applicability.

#### Coverage totals
Reused; no `src/**` change since recorded:
- `183-3.md:123`: Line 87.1% (44396/50968), Branch 78.7% (22968/29164); source `$E/U2/coverage/report/Summary.txt:7,12` (group-1 worktree), logs `$E/U2/coverage/{collect.log,reportgen.log}`; per-assembly line core 87.3 / postgres 79.2 / sqlite 89.3 / sqlserver 78.2.
- rv=5 fresh delivered-tree gates (`/tmp/opencode/coll-c/rv5/`) did not collect coverage; the absolute gates remain met by the reused numbers because `src/**` is unchanged since that run. Record: `coverage: reused from iteration-15-cached-path-183-3.md:123, no src change since`.

#### Container-provider integration applicability
Recorded isolated per-provider result (`183-3.md:124`, same U2 binaries): PostgreSQL total 776 / failed 0 / skipped 25; SQL Server 698/0/43; MySQL 656/0/80; ClickHouse 173/0/0. Applicability predicate: the #183 collection diff has NO `src/**` change, so provider SQL generation is byte-identical and the recorded provider results remain valid for the delivered tree. A provider-skipped run is NOT claimed as pass — the combined run (`integration.log:15068`) reported Total 3136, Errors 0, Failed 663, Skipped 191, 633.8 s, with all 663 failures environmental (653 SQL Server pre-login handshake `SqlException`/`Win32Exception: Unknown error 258` + 1 MySQL + 9 ClickHouse); the isolated 4-provider 0-fail runs are the applicable evidence.

#### G1-CACHE/U2 provider-evidence trace (rv=5 appendices)
Closes the rv=5 gap in the preserved evidence: exact per-provider command, exit code and log path were not recorded at rv=4. Isolation mechanism reconstructed and verified against the preserved logs — `--filter "FullyQualifiedName~<Provider>"` discovers exactly the recorded class sets (Postgres 776, SqlServer 698, MySql 656, ClickHouse 173 via `dotnet test tests/nextorm.integration.tests -c Debug --no-build --list-tests --filter ...`). All reruns ran from the group-1 worktree on the delivered/base binaries (no `src/**` change), `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`. Host `loadavg(1m)` during the reruns was 32–118 (shared host, concurrent sessions); only PostgreSQL stayed green, the three container-backed providers could not execute and are recorded as SKIPPED — **not** as passes. Their totals are reused from the preserved rv=4 logs (valid because `src/**` is unchanged).

- PostgreSQL — `source: rerun`.
  - `command:` `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~Postgres"`
  - `exit:` `0`
  - `totals:` total 776 / failed 0 / succeeded 751 / skipped 25 — `$E/U2/integration-postgres-rv5.log:79-85`
  - `log:` `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U2/integration-postgres-rv5.log` (group-1 worktree). Preserved `$E/U2/integration-postgres.log:79-84` matches 776/0/25.
- SQL Server — `source: reused preserved totals; rerun could not execute (SKIPPED)`.
  - `command:` `timeout 360 dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~SqlServer"` (plus a first unbounded attempt)
  - `exit:` `SKIPPED(environmental: host loadavg 32–118; execution timeout, 360 s bound exceeded)` — bound run `$E/U2/integration-sqlserver-rv5.log:2` `exit=124`; first attempt `$E/U2/integration-sqlserver-rv5-attempt1-timeout.log:7` `SqlException: Execution Timeout Expired` (`CommonTestSuite.RawSourceBinding_BoundCompatible_ShouldReturnFilteredRows`), `:33-34` `failed with 1 error(s) (19m 53s)` / `Exit code: 143`; the failure is environmental, not a U2 regression.
  - `totals:` reused — total 698 / failed 0 / succeeded 655 / skipped 43 — `$E/U2/integration-sqlserver.log:133-138`
  - `log:` preserved `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U2/integration-sqlserver.log`; rerun attempts `.../U2/integration-sqlserver-rv5.log`, `.../U2/integration-sqlserver-rv5-attempt1-timeout.log`.
- MySQL — `source: reused preserved totals; rerun could not execute (SKIPPED)`.
  - `command:` `timeout 300 dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~MySql"`
  - `exit:` `SKIPPED(environmental: host loadavg 32–118; container start/connection canceled, 300 s bound exceeded)` — `$E/U2/integration-mysql-rv5.log:30` `exit=124` (log shows `MySqlConnector ... TryResetConnectionAsync` connect failure).
  - `totals:` reused — total 656 / failed 0 / succeeded 576 / skipped 80 — `$E/U2/integration-mysql.log:244-249`
  - `log:` preserved `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U2/integration-mysql.log`; rerun `.../U2/integration-mysql-rv5.log`.
- ClickHouse — `source: reused preserved totals; rerun could not execute (SKIPPED)`.
  - `command:` `timeout 300 dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~ClickHouse"`
  - `exit:` `SKIPPED(environmental: host loadavg 32–118; container never became ready, zero tests ran, 300 s bound exceeded)` — `$E/U2/integration-clickhouse-rv5.log:2-4` `Zero tests ran (4m 52s 636ms)` / `Exit code: 143` / `exit=124`.
  - `totals:` reused — total 173 / failed 0 / succeeded 173 / skipped 0 — `$E/U2/integration-clickhouse.log:4-9`
  - `log:` preserved `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U2/integration-clickhouse.log`; rerun `.../U2/integration-clickhouse-rv5.log`.
