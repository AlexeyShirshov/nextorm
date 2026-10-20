# T233 (#233) — D233-window-over-overload, cycle N=1

## Identity and state

- task_id: `T233`; issue: `#233`
- title: «Window functions: перегрузка `Over` с одним `partitionBy` и одним `WindowOrder` (без массивов)»
- selected_variant: pdca-dotnet
- cycle: `N=1`
- plan_revision: `r=3` (**current**; corrective re-open of T233/#233 after the terminal `incomplete` — supersedes plan `r=2`, **not** D233 or its acceptance criteria; D233 reopens active. r=2 and r=1 text retained below as history)
- iteration: `n=1/3` (r=3 attempt 1 — corrective re-open; the r=1 `n=1/3` probe FAIL, the r=2 `n=1/3` DO→CHECK boundary, the r=2 `n=2/3` corrective and the r=2 `n=3/3` terminal-ACT `incomplete` are recorded history; real replan → plan revision `r=2`→`r=3`, iteration reset to `n=1/3`)
- plan_state: `done`
- evidence_contract: `rv=1` (frozen, **unchanged** — r=3 does not revise the evidence contract; retains all 17 `EC233-*` IDs and `R233-1..5` with unchanged stable IDs and obligations)
- milestone: `1.0.9-rc3`; collection run: `1.0.9-todo`; group: `G1` (single group — no worktree, no branches, no merge, current worktree/branch)
- defect: key `EC233-COMPAT-overload-ambiguity` (observed in r=1; fixes applied after the r=1 FAIL: 0 → r=2 applied fix #1, the priority attribute; r=2 probe **PASS**)
- phase: `done / closed` — **ACT complete (r=3, n=1/3, rv=1): CHECK verdict PASS — all 22 evidence rows `met`; registry/manifest repair closed; cycle N=1 closed; group `G1` fail-stop lifted by the collection-owner-authorized corrective ACT**. prior: `CHECK` — **DO complete (r=3, n=1/3): all D units closed; evidence green; manifest 22/22 met**; prior: `PLAN` — **reopened for DO (corrective re-open; PLAN r=3 ready, `n=1/3`)**; prior phase: `ACT` — **terminal `incomplete (product green; evidence residual: EC233-REGISTRY, R233-5, EC233-MANIFEST)`** (verifier `check`, medium tier, after the `escalate`-authorized bounded CHECK n=3 re-gather). At that time product/build/docs rows were certified `met`, the registry rows (`EC233-REGISTRY`, `R233-5`) stayed open and `EC233-MANIFEST` was recorded open/unmet, because the touched registry entries still carried stale `file:line` references and registry repair was forbidden in the re-gather pass. Definitive `incomplete` → collection group G1 **fail-stop** (T201 never executed). See the ACT section below. **[Superseded by the corrective re-open authorized by the collection owner to close EC233-REGISTRY/EC233-MANIFEST/R233-5 without product change.]**
- act_state: `done` — **ACT closed (r=3, `n=1/3`, `rv=1`): CHECK verdict PASS; ACT actions and final evidence pointers recorded in the `## ACT — done` section below; `#239 resolved-by-repair`; `T233` → `done`, G1 fail-stop lifted, T201 restored to `pending`; this ACT commit carries this status file and `collection-1.0.9-todo.md`** — prior: `superseded by the corrective re-open (PLAN r=3, `n=1/3`)` — the terminal `incomplete` ACT below is retained as history (at that time: no replan, plan stayed `r=2`, `rv=1`, `n=3/3`); retained commits `1429fd21`, `d5823ffb`, `e65b601e`; new public `Over` overload registered as a **T53 freeze candidate** (freeze **HELD**); follow-up issue **#239** for the stale registry references (its resolution requires separate authorization).
- mode: autonomous collection with authorized auto-commit (explicit paths, `#233` prefix, never `git add -A`, never push)
- status file: `docs/specs/status/D233-window-over-overload-1.md`

## Progress log

2026-10-14T17:41:57Z | PLAN | r1 | n1/3 | PLAN r=1 persisted by coder (PLAN-only step); evidence contract rv=1 frozen; DO not started (PLAN→DO boundary). | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T09:22:47Z | DO | r1 | n1/3 | EC233-GATE: implementation approval recorded — collection owner authorized continuing the lane (directive 2026-10-15) | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T09:22:47Z | DO | r1 | n1/3 | DO started (unit D:233.1 gate + blocking compiler compatibility probe, then D:233.2 API addition); test scope validated: `python3 scripts/validate_inner_loop.py brief /tmp/nextorm-D233-compat/scope-code.json` exit 0 | /tmp/nextorm-D233-compat/scope-code.json

2026-10-15T09:27:02Z | DO | r1 | n1/3 | EC233-COMPAT probe FAIL (blocking). Disposable baseline/candidate trees `/tmp/nextorm-D233-compat/tree-{baseline,candidate}` (only `src/nextorm.core/Query/WindowFunctions.cs` differs); argv+exit: baseline probe-existing `dotnet build tree-baseline/probe-existing/probe-existing.csproj -c Debug -v:m` exit 1 (2 warnings / 6 errors, all pre-existing), candidate probe-existing exit 1 (2 warnings / 12 errors), baseline probe-new (new call form) exit 1 (4 errors CS1503x3+CS0123), candidate probe-new exit 0 (0/0) | /tmp/nextorm-D233-compat/logs/{baseline-existing,candidate-existing,baseline-new,candidate-new}.log

2026-10-15T09:27:02Z | DO | r1 | n1/3 | EC233-COMPAT diagnostic diff candidate−baseline = 6 NEW CS0121 (baseline-success → candidate-ambiguous, all ambiguity between the old `Over(Expression?,Expression?,WindowFrame?)` and the planned `Over(Expression?,WindowOrder,WindowFrame?)`): `Over(partitionBy: e, orderBy: default)` (mandated corpus shape), `Over(partitionBy: en, orderBy: null)`, `Over(partitionBy: () => 1, orderBy: default)`, `Over(partitionBy: () => 1, orderBy: null)`, `Over(() => 1, orderBy: default)`, `Over(() => 1, orderBy: null)`. No other new/removed diagnostics, no changed resolution: all 5 existing signatures still bind (method-group pins) and the new 3-arg form binds to the new overload. defect key: `EC233-COMPAT-overload-ambiguity`; fixes applied: 0 | /tmp/nextorm-D233-compat/PROBE-SUMMARY.md

2026-10-15T09:27:02Z | DO | r1 | n1/3 | Real-call-site check: roslyn refs on `NextORM.Core.WindowFunction<T>.Over` = 50 references; every real call site uses typed lambdas / `WindowOrder` / `asc|desc` / arrays / `"w"` — no real call site passes an untyped `null`/`default`. The 6 new CS0121 are confined to the synthetic corner shapes (R233-2 upheld, not weakened). | /tmp/nextorm-D233-compat/PROBE-SUMMARY.md

2026-10-15T09:27:02Z | DO | r1 | n1/3 | STOP per plan r=1 §4/§12 ("любая новая ошибка старого успешного вызова блокирует выбранный дизайн") and the DO brief ("if any new diagnostic appears ⇒ STOP, report as probe FAIL, do not edit the repo"): **no product edit** (D:233.2 not applied), no registry change (D:233.7), no `dotnet build nextorm.slnx`, no inner-loop test run, no commit. DO→PLAN loop-back: candidate design invalidated; needs a new `P:` from the planner (real replan → r+1, n resets to 1). | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T09:31:36Z | PLAN | r2 | n1/3 | Replanned: r=1 probe FAIL (6 new CS0121 on untyped default/null) -> r=2 adds [OverloadResolutionPriority(-1)] to the new overload. Real replan: plan revision `r=1`→`r=2`, iteration reset to `n=1/3`; `rv=1` unchanged; R233-1..5 unchanged and **no criterion weakened, no ambiguity accepted**; escalation rule: if any supported-consumer case is still ambiguous after the attribute ⇒ STOP, return to PLAN, do not relax R233-2. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T09:31:36Z | PLAN | r2 | n1/3 | P0 prerequisite (compiler/language floor for the priority attribute) resolved and satisfiable: `Directory.Build.props:29` `<TargetFramework>net10.0</TargetFramework>`, `:31` `<LangVersion>latest</LangVersion>`, `:40` `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`; `global.json` pins no SDK; public docs state only "A project targeting `net10.0`" (`docs/getting-started/01-installation.md:5`); the attribute is already used 6× in `src/nextorm.core` (e.g. `src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:794` `[OverloadResolutionPriority(-1)]`, `src/nextorm.core/Builders/MergeBuilder.cs:201` `[OverloadResolutionPriority(1)]`) ⇒ already resolvable under `net10.0`/TreatWarningsAsErrors, **no silent new minimum**. | src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:794, src/nextorm.core/Builders/MergeBuilder.cs:201, Directory.Build.props:29

2026-10-15T09:31:36Z | DO | r2 | n1/3 | EC233-GATE: implementation approval recorded — collection owner authorized continuing the lane (directive 2026-10-15) | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T09:31:36Z | DO | r2 | n1/3 | DO r=2 started (D:233.1 extended differential probe with the attribute before any product edit; then D:233.2 API addition, D:233.7 registry). Test scope re-validated before any edit: `python3 scripts/validate_inner_loop.py brief /tmp/nextorm-D233-compat/scope-code.json` exit 0 | /tmp/nextorm-D233-compat/scope-code.json

2026-10-15T09:36:14Z | DO | r2 | n1/3 | EC233-COMPAT r=2 **PASS**. Disposable trees keep r=1 artifacts: `tree-baseline` (unmodified), `tree-candidate` (r=1 candidate, kept), new `tree-attr` = tree-candidate + `using System.Runtime.CompilerServices;` + `[OverloadResolutionPriority(-1)]` on the new overload only. argv+exit: `dotnet build tree-baseline/probe-existing/probe-existing.csproj -c Debug -v:m` exit 1 (8 unique diagnostics, all pre-existing) · `dotnet build tree-attr/probe-existing/probe-existing.csproj -c Debug -v:m` exit 1 (same 8 unique) · `dotnet build tree-{baseline,attr}/probe-bind/probe-bind.csproj` exit 0 both · `dotnet build tree-baseline/probe-bind-new/...` exit 1 (7 unique: CS1503x5+CS0123) vs `tree-attr` exit 0 · `dotnet build tree-baseline/probe-new/...` exit 1 (4 unique) vs `tree-attr` exit 0. Zero new diagnostics: `logs-r2/tree-baseline-probe-existing.diags` == `logs-r2/tree-attr-probe-existing.diags` == r=1 `logs/baseline-existing.diags`; `comm -23 logs/candidate-existing.diags logs-r2/tree-attr-probe-existing.diags` = exactly the six r=1 CS0121 (Corpus.cs :57,67,70,71,72,73) — removed. | /tmp/nextorm-D233-compat/PROBE-SUMMARY-r2.md

2026-10-15T09:36:14Z | DO | r2 | n1/3 | EC233-COMPAT r=2 per-expression **binding** proof (not exit status): `dotnet run --project tree-{baseline,attr}/probe-bind/probe-bind.csproj -c Debug` exit 0 both, output byte-identical on all 15 probes → the six formerly-ambiguous corners (`Over(partitionBy: e, orderBy: default[, frame: null])`, `…en, null…`, `…() => 1, default…`, `…() => 1, null…`, `…() => 1, orderBy: default…`, `…, null…`) resolve to `WindowFunction<T>.Over(Expression<Func<Object>>, Expression<Func<Object>>, WindowFrame)` (prio none) in BOTH trees; all five existing signatures resolve to themselves (sig2 `Over(WindowOrder, WindowFrame)`, sig3 `Over(String)`, sig4 `Over(WindowOrder[], WindowFrame)`, sig5 `Over(Expression<Func<Object>>[], WindowOrder[], WindowFrame)`); typed defaults + typed-null also on the original. `dotnet run --project tree-attr/probe-bind-new/probe-bind-new.csproj -c Debug` exit 0 → all three new forms resolve to `Over(Expression<Func<Object>>, WindowOrder, WindowFrame)` **prio=-1** (the new overload); intended 2-arg forms + new method-group pin compile on tree-attr and fail on tree-baseline. Method: the call is compiled inside an expression tree and the recorded `MethodInfo` is read at run time. | /tmp/nextorm-D233-compat/logs-r2/*.run.log

2026-10-15T09:36:14Z | DO | r2 | n1/3 | D:233.2 **applied** (product edit, r=2). `src/nextorm.core/Query/WindowFunctions.cs`: +`using System.Runtime.CompilerServices;` and the new overload `[OverloadResolutionPriority(-1)] public T Over(Expression<Func<object?>>? partitionBy, WindowOrder orderBy, WindowFrame? frame = null) => default!;` placed after the existing expression overload, with `<summary>`/3x`<param>` XML docs stating the scalar shape and the source-compat rationale. Five existing overloads untouched: `git diff --stat` = 23 insertions, 0 deletions; CRLF preserved (`file` → CRLF). | src/nextorm.core/Query/WindowFunctions.cs

2026-10-15T09:36:14Z | DO | r2 | n1/3 | D:233.7 **applied** (registry). `docs/specs/design/API-NAMING-REVIEW.md`: new per-audit section `## Аудит 15.10.2026 — issue #233 …` (`**Область.**` + bullets with `src/...:line`, additive/no-renames/existing-overloads-untouched, the `[OverloadResolutionPriority(-1)]` source-compat rationale with pointers to the 6 existing usages + both probe summaries, `N233-1` P2/Шаг 5 candidate row, ℹ️ `N233-2` compiler floor C# 13+/net10.0); stale `10-window-functions.md` → `11-window-functions.md` fixed only at the 7 planned lines (12 occurrences: :204-207,1078,1134,2242); CRLF preserved. | docs/specs/design/API-NAMING-REVIEW.md

2026-10-15T09:36:14Z | DO | r2 | n1/3 | Build gate PASS: `dotnet build nextorm.slnx -c Debug` exit 0, `0 Warning(s) 0 Error(s)` — this also re-compiles all 39 real `Over` call sites unchanged (no CS0121). Inner loop: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~WindowFunctionMarkerTests"` (phase inner, filtered) exit 0, total 12 / succeeded 12 / failed 0 / skipped 0. No commit in this task; tree left dirty for the single explicit-path autocommit at the DO→CHECK boundary. | /tmp/nextorm-D233-compat/logs-r2/{solution-build,inner-loop-marker}.log

2026-10-15T09:36:14Z | DO | r2 | n1/3 | Defect history: `EC233-COMPAT-overload-ambiguity` — observed r=1 n1/3 (6 new CS0121, evidence `/tmp/nextorm-D233-compat/PROBE-SUMMARY.md`), fixes applied: 0 → r=2 applies fix #1 (`[OverloadResolutionPriority(-1)]`); re-observed r=2 n1/3: **no** (zero new diagnostics, all six corners on their original overloads, evidence `/tmp/nextorm-D233-compat/PROBE-SUMMARY-r2.md`); escalation not triggered. Re-injected-change triggers per r=2 escalation rule: any further change to the overload set or to consumer compilation ⇒ re-run EC233-COMPAT, and if a supported-consumer case is still ambiguous ⇒ STOP → PLAN. | /tmp/nextorm-D233-compat/PROBE-SUMMARY-r2.md

2026-10-15T09:48:54Z | DO | r2 | n1/3 | DO→CHECK boundary build: `["dotnet","build","nextorm.slnx","-c","Debug"]` exit 0, **0 Warning(s) / 0 Error(s)** (all 39 real `Over` call sites re-compiled, no CS0121) | /tmp/opencode/t233-boundary/logs/build.log

2026-10-15T09:48:54Z | DO | r2 | n1/3 | Boundary filtered project suites (serial, `--no-build` after the single build): `~WindowFunctionMarkerTests` 16/16 pass (core.tests); dialect `~Window` — sqlite 12, postgres 27, sqlserver 12, mysql 7, mariadb 5, clickhouse 27 (all exit 0, 0 failed, 0 skipped); exact new test `~WindowScalarOverload` **1 selected / 1 passed** on each of the six dialect projects | /tmp/opencode/t233-boundary/logs/{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}{,.scalar}.log

2026-10-15T09:48:54Z | DO | r2 | n1/3 | Container-backed integration (mandatory; `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, single OR filter over the 9 window method names, `--no-build`, one run) exit 0: total 30 / succeeded 30 / failed 0 / **skipped 0**. Per-provider window tests (xunit `-list` discovery): sqlite 7, postgres 7, sqlserver 7, mysql 7, clickhouse 2 = 30 → PostgreSQL/SQL Server/MySQL/ClickHouse **ran, not skipped** (incl. `ScalarOverload_ShouldMatchTheArrayOverloadResult` on SQLite/PostgreSQL/SQL Server/MySQL). MariaDB has no CommonTestSuite-derived integration class (dialect-only per the variant matrix) | /tmp/opencode/t233-boundary/logs/integration-window.log, /tmp/opencode/t233-boundary/logs/discover-*.log

2026-10-15T09:48:54Z | DO | r2 | n1/3 | Coverage — CI path of `.github/workflows/dotnet.yml` reproduced (no `DOCKER_HOST`, as in CI): `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o <out> "dotnet test --no-build --verbosity normal"` exit 0 (total 12007 / succeeded 8875 / failed 0 / skipped 3132) then `dotnet tool run reportgenerator -reporttypes:'JsonSummary;TextSummary;Cobertura'` exit 0 → **line 87.4 % ≥ 85, branch 80 % ≥ 75** (4 assemblies per coverage.settings.xml) | /tmp/opencode/t233-test/coverage/report/Summary.txt, /tmp/opencode/t233-test/coverage/coverage.cobertura.xml

2026-10-15T09:48:54Z | DO | r2 | n1/3 | Docs reconciliation (item 4): the new samples in `docs/guide/11-window-functions.md` (EN) and `docs/ru/guide/11-window-functions.md` (RU) match the final signature `Over(partitionBy, orderBy: <WindowOrder>, frame:)` (2-/3-arg, positional + `frame:`), contain **no attribute**, the generated SQL is stated identical to the one-element array form, EN/RU are structurally in sync and both files are CRLF (`file` → CRLF). No genuine mismatch found → no edit. Manifest path is gitignored (`git check-ignore` → `.gitignore:7 artifacts/`), so it is excluded from the commit | docs/guide/11-window-functions.md, docs/ru/guide/11-window-functions.md

2026-10-15T09:48:54Z | DO | r2 | n1/3 | Frozen evidence manifest `rv=1` written at `artifacts/pdca/D233/rv1/manifest.json` (contract carried verbatim from r=2; 22 required rows = 17 `EC233-*` + `R233-1..5`; supplied r=2 probe/test-stream observations distinguished from newly verified boundary results). `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json` exit 0; `... report /tmp/opencode/t233-test/evidence.json` exit 0 and `... report /tmp/opencode/t233-boundary/evidence.json` exit 0. `EC233-REGISTRY`, `EC233-CAPABILITY`, `R233-5` recorded **`open`** — their obligations require independent CHECK certification (`certifications/EC233-{REGISTRY,CAPABILITY}.json`), so DO cannot self-certify them | artifacts/pdca/D233/rv1/manifest.json

2026-10-15T09:48:54Z | DO | r2 | n1/3 | Defect history at the boundary: `EC233-COMPAT-overload-ambiguity` — re-observed r=2 n1/3 = **no** (zero new diagnostics; six untyped `default`/`null` corners on their original overloads); fixes applied: 1 (priority attribute); escalation not triggered. No new defect keys observed in the boundary sweep (build, 13 filtered project runs, integration, coverage all exit 0) | /tmp/nextorm-D233-compat/PROBE-SUMMARY-r2.md

2026-10-15T09:48:54Z | DO | r2 | n1/3 | Single autocommit of task paths (explicit paths only, no `git add -A`, no push): `1429fd21f2201ed350c362387421a7dd4e3a5519` — 12 files / +597 −7 (`WindowFunctions.cs`, 6× dialect `SqlGenerationTests.cs`, `WindowFunctionMarkerTests.cs`, `CommonTestSuite.Window.cs`, 2 guide pages, `API-NAMING-REVIEW.md`). Manifest path skipped from the commit (gitignored). Cycle status file and other tasks' status/docs not committed | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:02:28Z | DO | r2 | n2/3 | CHECK attempt 1 (r=2 n=1/3) **FAILED** (verifier `check`, medium tier) → corrective DO `n=2/3`. Failures: Major/P1 `D:233.5` (ClickHouse integration case not executed); missing CHECK-side evidence (DocFX never run; coverage run omitted `DOCKER_HOST`); `EC233-GATE` evidence package incomplete; minor registry defect (`Directory.Build.props` LangVersion line). No criterion was changed and no action-set change → no real replan: plan revision stays `r=2`, iteration advances to `n=2/3`. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:02:28Z | DO | r2 | n2/3 | `D:233.5` corrected — ClickHouse gap cause established from the tree: `CommonTestSuite` derives `ProviderTestSuite` (`tests/nextorm.integration.tests/CommonTestSuite.cs:7`), but `ClickHouseIntegrationTests` derives `ProviderTestSuite` **directly** and not `CommonTestSuite` (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:18`); `SqliteIntegrationTests.cs:5`, `PostgresIntegrationTests.cs:8`, `SqlServerIntegrationTests.cs:8`, `MySqlIntegrationTests.cs:8` all derive `CommonTestSuite`. So the shared `Window` suite is **not inherited** on ClickHouse — a **wiring omission in the T233 change, not a capability guard** (the ClickHouse dialect already pins the identical SQL shape: `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:720-762`). Corrective edit (minimal, same class/style): two mirrored facts added to `ClickHouseIntegrationTests.cs` — `WindowScalarOverload_ShouldMatchTheArrayOverloadResult` (scalar-vs-array parity incl. the NULL partition of `complex_entity.nullableint`) and `WindowScalarOverload_WithRowsFrame_ShouldMatchTheArrayOverloadResult` (`WindowFrame.Rows(1,1)` parity). No non-Integration-Tests project touched. Route-to-PLAN: none. | tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs

2026-10-15T10:02:28Z | DO | r2 | n2/3 | Corrective build gate: `["dotnet","build","nextorm.slnx","-c","Debug"]` exit 0, **0 Warning(s) / 0 Error(s)**. ClickHouse integration (ONE `DOCKER_HOST` run, `--no-build`): `["dotnet","test","tests/nextorm.integration.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~ClickHouseIntegrationTests.WindowScalarOverload|FullyQualifiedName~ClickHouseIntegrationTests.InFrameWindowFunctions|FullyQualifiedName~ClickHouseIntegrationTests.WindowFunnel"]` exit 0 → total 4 / succeeded 4 / failed 0 / **skipped 0**. Discovery confirms exactly the 4 CH window tests: `WindowFunnel_ShouldCountConsecutiveConditions`, `InFrameWindowFunctions_ShouldRespectFrame`, `WindowScalarOverload_ShouldMatchTheArrayOverloadResult`, `WindowScalarOverload_WithRowsFrame_ShouldMatchTheArrayOverloadResult`. | artifacts/pdca/D233/rv1/logs/{build,clickhouse-window,clickhouse-window.discovery}.log

2026-10-15T10:02:28Z | DO | r2 | n2/3 | `EC233-DOCS` corrective: `["dotnet","docfx","docs/docfx.json"]` exit 0 — **2 warning(s) / 0 error(s)**; both warnings are pre-existing `Duplicate source file .../src/nextorm.core.sourcegenerator/AnalyzerReleases.{Shipped,Unshipped}.md` and are unrelated to the window change (no new broken references). | artifacts/pdca/D233/rv1/logs/docfx.log

2026-10-15T10:02:28Z | DO | r2 | n2/3 | `EC233-COVERAGE` corrective — the plan's frozen invocation re-run **with** `DOCKER_HOST` (frozen C-COVERAGE/C-REPORT; local tools via `dotnet tool run`): `dotnet-coverage collect --settings coverage.settings.xml --output-format cobertura --output artifacts/pdca/D233/rv1/coverage/coverage.cobertura.xml "dotnet test nextorm.slnx -c Debug"` exit 0 (total 12009 / succeeded 11802 / failed 0 / skipped 207 — container providers actually executed) then `reportgenerator -reports:… -targetdir:… -reporttypes:JsonSummary;TextSummary` exit 0 → **line 88.9 % (53515/60194) ≥ 85, branch 80.9 % (28995/35805) ≥ 75** (4 assemblies per `coverage.settings.xml`). | artifacts/pdca/D233/rv1/coverage/report/Summary.txt, artifacts/pdca/D233/rv1/logs/{coverage-collect-docker,coverage-report-docker}.log

2026-10-15T10:02:28Z | DO | r2 | n2/3 | `EC233-GATE` evidence package assembled. (a) Authorization lines (recorded approval, in this file): line 23 `2026-10-15T09:22:47Z | DO | r1 | n1/3 | EC233-GATE: implementation approval recorded — collection owner authorized continuing the lane (directive 2026-10-15)` and line 39 `2026-10-15T09:31:36Z | DO | r2 | n1/3 | EC233-GATE: implementation approval recorded …`. (b) T232 handoff: collection status `docs/specs/status/collection-1.0.9-todo.md:26` row `| T232 | #232 | … done-with-caveat (r=5, rv=5; product green; residual evidence-provenance → #238; #237 accepted risk) |` and `:34` `… T232 (done-with-caveat) → T233 …`; handoff/group-stop-lift commit `7bdea6567828073d1ae56efb31ebed686e54430a` (`%cI = 2026-10-15T14:15:01+05:00`, subject `collection 1.0.9-todo: T232 accepted done-with-caveat …; group-stop lifted; next T233`). (c) Edit chronology: `git merge-base --is-ancestor 7bdea656 1429fd21` → **exit 0** (handoff is an ancestor of the code commit; `git log --oneline 7bdea656..1429fd21` = only `1429fd21`); DO code commit `1429fd21f2201ed350c362387421a7dd4e3a5519` `%cI = 2026-10-15T14:48:51+05:00` (= `09:48:51Z`) is **after** both recorded authorization lines (`09:22:47Z` / `09:31:36Z`) ⇒ the recorded approval precedes the product edit. | docs/specs/status/collection-1.0.9-todo.md:26,34; git 7bdea656, 1429fd21

2026-10-15T10:02:28Z | DO | r2 | n2/3 | `D:233.7` corrective (registry): `docs/specs/design/API-NAMING-REVIEW.md:5799` — `Directory.Build.props:29,31` → **`:29,32`** (verified `Directory.Build.props:32` is `<LangVersion>latest</LangVersion>`; `:31` is `<Nullable>enable</Nullable>`). `N233-2` rewritten as an **accepted documented limitation with a trigger**: `[OverloadResolutionPriority]` is honoured by the compiler that builds the consumer; a consumer compiled with a compiler predating the attribute sees the pre-existing untyped-`default`/`null` ambiguity (CS0121); **trigger** = re-probe on a compiler-policy change or an `Over` overload-set change; supported consumer floor recorded (`net10.0` + `LangVersion latest`, public docs `docs/getting-started/01-installation.md:5`). **No** older-consumer-compatibility claim. CRLF preserved. | docs/specs/design/API-NAMING-REVIEW.md

2026-10-15T10:02:28Z | DO | r2 | n2/3 | Certification records written **open / not certified** (no positive certificate): `artifacts/pdca/D233/rv1/certifications/EC233-REGISTRY.json` and `.../EC233-CAPABILITY.json` — verifier `check` (medium tier), `rv=1`, concrete blockers listed, `certified:false`, `is_a_certificate:false`. Manifest rows `EC233-REGISTRY`/`EC233-CAPABILITY`/`R233-5` left **`open`** (not flipped by DO). Manifest rows `EC233-INTEGRATION` (CH evidence), `EC233-COVERAGE` (DOCKER_HOST evidence) and `EC233-DOCS` (DocFX evidence) augmented **additively**; `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json` exit 0. | artifacts/pdca/D233/rv1/manifest.json, artifacts/pdca/D233/rv1/certifications/EC233-{REGISTRY,CAPABILITY}.json

2026-10-15T10:02:28Z | DO | r2 | n2/3 | Defect history: `EC233-COMPAT-overload-ambiguity` (r=1 → r=2, fix #1 `[OverloadResolutionPriority(-1)]`; re-observed r=2 = no). New CHECK-attempt-1 defect keys and outcome: `EC233-CH-CAPABILITY-GAP` (ClickHouse integration case not executed → **closed**, fixes applied: 1, evidence `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs` + `clickhouse-window.log`); `EC233-EVIDENCE-COVERAGE-DOCKER` (coverage omitted `DOCKER_HOST` → **closed**, frozen invocation re-run with `DOCKER_HOST`); `EC233-EVIDENCE-DOCFX` (DocFX never run → **closed**, exit 0 / 2W / 0E); `EC233-GATE-PACKAGE` (incomplete → **closed** in this file); `EC233-REGISTRY-LANGVERSION-LINE` (minor registry ref → **fixed**). No criterion weakened; no escalation triggered. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:05:00Z | DO | r2 | n2/3 | Corrective autocommit (explicit paths only, no `git add -A`, no push): `d5823ffbcade2765210f04041e233fa7f6db4a47` — 2 files / +73 −5 (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs`, `docs/specs/design/API-NAMING-REVIEW.md`). `artifacts/**` skipped (gitignored, `.gitignore:7`, verified with `git check-ignore -v`); this status file and other tasks' status/docs not committed; `examples/**`, `benchmarks/**`, diagrams not touched. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:15:45Z | DO | r2 | n3/3 | CHECK attempt 2 (r=2 n=2/3) **FAILED** (verifier `check`, medium tier) → corrective DO `n=3/3`. Root finding `EC233-NULL-PARTITION-VARIANT`: the frozen variant-matrix row "`partitionBy: null` × single order" had **no test location** (the plan requires the explicit scalar-vs-array SQL parity asserting the absence of `PARTITION BY` across the six dialects). Secondary open items: `EC233-REGISTRY` / `EC233-CAPABILITY` / `R233-5` (no audit artifacts), `EC233-DOCS` / `EC233-BOUNDARY` (unverified for lack of frozen audits), `EC233-MANIFEST` (frozen canonical manifest locator did not exist on disk). No criterion changed and no action-set change → **no real replan**: plan revision stays `r=2`, iteration advances to `n=3/3`. Already met and not re-litigated: `EC233-GATE`, `EC233-COMPAT`, `EC233-MARKER`, `EC233-SQL-*`, `EC233-INTEGRATION`, `EC233-BUILD`, `EC233-COVERAGE`, `R233-1..4` except the null-partition clause of R233-1. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:15:45Z | DO | r2 | n3/3 | **Null-partition variant closure.** Mapper decision (from code, not guessed): `WindowSql.SplitWindowArguments` (`src/nextorm.core/Visitors/WindowSql.cs:163-164`) forwards the `partitionBy` argument untouched, and `ParseWindowPartitions` (`src/nextorm.core/Visitors/WindowSql.cs:65-66`) returns an **empty** list for a `null`/`ConstantExpression{null}` expression and for an empty array (`:68-72`); a single null element inside the array is dropped by `AddWindowLambda` → `UnwrapWindowLambda` returns `null` (`:79-83,126-132`). The translator emits `partition by` **only** when the list is non-empty (`src/nextorm.core/Visitors/WindowFunctionTranslator.cs:152-154`). Therefore `Over(partitionBy: null, orderBy: o)` is byte-identical to the null array, the empty array (`new Expression<Func<object?>>[0]`) and the order-only overload. New tests added: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:2394`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:2629`, `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1783`, `tests/nextorm.mysql.tests/SqlGenerationTests.cs:515`, `tests/nextorm.mariadb.tests/SqlGenerationTests.cs:406`, `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:765`, `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs:118` — each asserts scalar-null == array-null == empty-array == order-only and `NotContainEquivalentOf("partition by")` (case-insensitive). No new integration case was added for this row (not required; the nullable-partition integration parity already exists at `tests/nextorm.integration.tests/CommonTestSuite.Window.cs:145`). No sub-case left uncovered. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:15:45Z | DO | r2 | n3/3 | **`EC233-MANIFEST` frozen-locator repair.** The frozen contract spelled the artifact locators as `artifacts/pdca/D233-window-over-overload/rv1/**` (original frozen path); the artifacts live under `artifacts/pdca/D233/rv1/**` (real path — overlay default slug directory = task slug `D233`). Locators only were repaired (11 occurrences: `:360,373,387-403,584`) with an explicit `Notice:` at `:361` recording the original path, the real path and the reason (plan-text typo, not a missing artifact); nothing was weakened. Disk check (`test -d`) confirms `artifacts/pdca/D233/rv1/` + `coverage/` + `certifications/` + `logs/` resolve. Two planned artifacts named in the frozen catalog were never materialised and are not obligations: the per-row package dir `artifacts/pdca/D233/rv1/rows/` (the manifest carries evidence inline in `rows[]`) and the disposable helpers `audit.py`/`compat_probe.py` (self-declared planned helpers). Path sweep over the manifest + frozen rows: 82 distinct `artifacts/…`/`tests/…`/`src/…`/`/tmp/…` paths — all resolve except the original-typo string, the two never-materialised helpers, the unused `rows/` dir and the brace-truncated false positive `AnalyzerReleases.{Shipped` (the real `.Shipped.md`/`.Unshipped.md` exist). | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:15:45Z | DO | r2 | n3/3 | **Data-only certification audits written** (author = DO, not the certifier; all `certified:false`, `rv=1`, `is_a_certificate:false`): `artifacts/pdca/D233/rv1/certifications/EC233-REGISTRY-audit.json` (new signature `src/nextorm.core/Query/WindowFunctions.cs:51-54` + attribute `:50`; the five unchanged overloads `:29,:57,:65,:72,:80`; EN guide `docs/guide/11-window-functions.md:125-148` / RU `docs/ru/guide/11-window-functions.md:126-149`; W1–W4/NW2 reconciliation vs code; link check 7+7 EN/RU relative links resolve, 0 public→spec), `…/EC233-DOCS-audit.json` (EN/RU parity, `frame` optionality, no stale `10-window-functions.md` in public docs, DocFX exit 0 / 2 pre-existing warnings proven unrelated, no public→`docs/specs/**` link), `…/EC233-BOUNDARY-audit.json` (changed-path scope + deliberately-untouched list + perf non-applicability argument), `…/EC233-CAPABILITY-audit.json` (capability flags + the provider×variant table). Manifest rows `EC233-REGISTRY`/`EC233-CAPABILITY` remain **`open`**; the audit paths and the n=3/3 logs were added to the manifest **additively**. `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json` → **exit 0**. | artifacts/pdca/D233/rv1/certifications/

2026-10-15T10:15:45Z | DO | r2 | n3/3 | **Verification (n=3/3).** `["dotnet","build","nextorm.slnx","-c","Debug"]` → exit 0, **0 Warning(s) / 0 Error(s)**. Filtered suites (each project once, `--no-build`): core `--filter "FullyQualifiedName~WindowFunctionMarkerTests"` exit 0 → 17/17 (was 16); sqlite `~Window` exit 0 → 13/13; postgres exit 0 → 28/28; sqlserver exit 0 → 13/13; mysql exit 0 → 8/8; mariadb exit 0 → 6/6; clickhouse exit 0 → 28/28 (each +1 = the new null-partition test; 0 failed, 0 skipped). `python3 scripts/validate_inner_loop.py report /tmp/opencode/t233-n3/evidence.json` → **exit 0**. Raw logs: `artifacts/pdca/D233/rv1/logs/{build,core-marker,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-n3.log`. | artifacts/pdca/D233/rv1/logs/

2026-10-15T10:15:45Z | DO | r2 | n3/3 | **Defect history.** `EC233-COMPAT-overload-ambiguity` — r=1 → r=2, fix #1 (`[OverloadResolutionPriority(-1)]`), re-observed r=2 = no (fixes applied: 1). `EC233-CH-CAPABILITY-GAP`, `EC233-EVIDENCE-COVERAGE-DOCKER`, `EC233-EVIDENCE-DOCFX`, `EC233-GATE-PACKAGE`, `EC233-REGISTRY-LANGVERSION-LINE` — closed at n=2/3. **New (CHECK attempt 2):** `EC233-NULL-PARTITION-VARIANT` (null-partition parity row had no location) → **closed**, fixes applied: 1, evidence the 7 new tests above (each executed, 1 selected per dialect, all green) and this file; `EC233-MANIFEST-LOCATOR` (frozen locator `artifacts/pdca/D233-window-over-overload/rv1/manifest.json` absent) → **closed**, fixes applied: 1, evidence the `Notice:` at `:361` + validator exit 0; `EC233-REGISTRY`/`EC233-CAPABILITY`/`R233-5` (no audit artifacts) → **audit data produced**, still **open** pending independent CHECK certification (author≠certifier). No criterion weakened; no escalation triggered. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:15:45Z | DO | r2 | n3/3 | Autocommit (explicit paths only, no `git add -A`, no push): `e65b601e0df26ddbf66ee643a2d38f93a6a26f35` — 7 files / +322 −0 (the 6 dialect `SqlGenerationTests.cs` + `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs`). No public-docs file was edited this attempt; the cycle status file and other tasks' status/docs are not committed (established convention for this cycle); `artifacts/**` skipped (gitignored, `.gitignore:7`); `examples/**`, `benchmarks/**`, diagrams not touched. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T10:45:00Z | CHECK | r2 | n3/3 | CHECK (independent certifier, medium tier) after the `escalate`-authorized bounded re-gather: certified `met` — `R233-1..4`, `EC233-GATE`, `EC233-COMPAT`, `EC233-MARKER`, `EC233-SQL-{SQLITE,POSTGRES,SQLSERVER,MYSQL,MARIADB,CLICKHOUSE}`, `EC233-INTEGRATION`, `EC233-BUILD`, `EC233-COVERAGE`, `EC233-DOCS`, `EC233-BOUNDARY`, `EC233-CAPABILITY`. Kept **open (blocker)**: `EC233-REGISTRY` and `R233-5` — the touched registry entries still carry stale references (W2 `:205,1138` → sample text instead of the footer; W4 `:207,1136` → Frames section and flag `:64` instead of `:274`; NW2 `:2242` → `WindowFunctions.cs:64` instead of `:87`); registry repair was forbidden in the authorized pass, so it cannot be fixed here. `EC233-MANIFEST` recorded open/unmet (three mandatory rows open; the r=2/n=3 validator log was 0 bytes). | artifacts/pdca/D233/rv1/certifications/, artifacts/pdca/D233/rv1/manifest.json

2026-10-15T10:45:00Z | CHECK | r2 | n3/3 | Bounded re-gather journal (`escalate`-authorized, NOT a DO iteration; request 1 of 2): `artifacts/pdca/D233/rv1/certifications/CHECK-n3-regather-request1.json` — raw extracts (scope diff, EN/RU docs sections, DocFX lines, capability refs, 39 citation existence checks, 7 guard exact-name runs, W4 correction, manifest row states); `previous_sha 7bdea656`, `head_sha e65b601e`. No `src`/`tests`/public-docs/registry edited; no big suites rerun; no certificates created by the re-gather. | artifacts/pdca/D233/rv1/certifications/CHECK-n3-regather-request1.json

2026-10-15T11:05:00Z | ACT | r2 | n3/3 | ACT outcome (single writer of this file): **terminal `incomplete (product green; evidence residual: EC233-REGISTRY, R233-5, EC233-MANIFEST)`**; no replan (plan stays `r=2`, `rv=1`, `n=3/3`). Positive certificates written: `EC233-CAPABILITY.json` (certified:true), `EC233-DOCS.json`, `EC233-BOUNDARY.json`; open/not-certified records retained: `EC233-REGISTRY.json`, new `EC233-R233-5.json`. Manifest reconciled **additively** (`EC233-CAPABILITY`→`met`; `EC233-DOCS`/`EC233-BOUNDARY` carry their certificate paths; `EC233-REGISTRY`/`R233-5` stay `open` naming the registry blocker; `EC233-MANIFEST`→`open`/unmet). Live manifest validation (request 2 of 2): `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json` → **exit 0**, log `artifacts/pdca/D233/rv1/logs/manifest-validate-n3.log` (non-empty, 16 bytes). Follow-up issue **#239** (https://github.com/AlexeyShirshov/nextorm/issues/239) tracks the four stale registry `file:line` pairs. Retained commits `1429fd21`, `d5823ffb`, `e65b601e` (never pushed). | artifacts/pdca/D233/rv1/manifest.json, artifacts/pdca/D233/rv1/logs/manifest-validate-n3.log

2026-10-15T11:05:00Z | ACT | r2 | n3/3 | Collection fail-stop executed: definitive task `incomplete` stops group G1 — the remaining pending task **T201 (#201)** is marked `incomplete` («группа остановлена» / group stopped) and **not executed**; new public `Over` overload registered as a T53 freeze candidate (freeze **HELD**). Recorded in `docs/specs/status/collection-1.0.9-todo.md`. | docs/specs/status/collection-1.0.9-todo.md

2026-10-15T10:53:46Z | PLAN | r3 | n1/3 | Reopened: corrective PLAN r=3 (registry/manifest repair only; no product change). | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T11:18:40Z | DO | r3 | n1/3 | **`D: REGISTRY` closed** (docs-only, stream A). Every stale `file:line` locator in the touched registry entries of `docs/specs/design/API-NAMING-REVIEW.md` repaired and verified in place: W2 `:205` — EN `docs/guide/11-window-functions.md:244-248`→`:352-356`, RU `docs/ru/guide/11-window-functions.md:247-251`→`:355-359` (current `Source:` footers), `SqlFunctions.cs:23`→`:26` (`Sql` property), `WindowFunctions.cs:16`→`:17` (`WindowFunction<T>`), `SqlFunctions.cs:221`→`:739` (`date_diff`), window methods `:314-372`→`:934-980`; `:1138` — `guide/10` `Source:` footer → EN `:352-356` (+RU `:355-359`); W4 `:207` — EN `:220-234`→`:308-312`, RU `:222-237`→`:310-314`; `:1136` — ClickHouse flag `:64`→`src/nextorm.clickhouse/ClickHouseDialect.cs:274` (`SupportsPercentRankCumeDist`); NW2 `:2242` — `Query/WindowFunctions.cs:64`→`:87` (`WindowOrder`), `Builders/EntityBuilder.cs:558,565`→`:2110,2117` (`Asc`/`Desc`); plus the RD2 batch-3 row `:2241` — `Query/WindowFunctions.cs` 42/80/95/172/181/184/191→65/103/120/205/214/217/224. Ledger: **21 entries = 19 repaired + 2 verified-unchanged**. No product source touched. | artifacts/pdca/D233/rv1/r3/registry-ledger.json, docs/specs/design/API-NAMING-REVIEW.md

2026-10-15T11:18:40Z | DO | r3 | n1/3 | **`D: AUDIT` closed** — `artifacts/pdca/D233/rv1/r3/registry-ledger.json` records, per entry, the old locator → the verified current locator, the verification source/command and the result. Every C# symbol was resolved with `roslyn` (`members`/`types`: `WindowFunction<T>`:17, `WindowOrder`:87, `WindowFrameType`:103, `WindowFrameExclusion`:120, `WindowFrame.Exclusion/Groups/WithExclusion`:205/214/217/224, `CommonFunctions.date_diff`:739, `CommonFunctions` window methods :934-980, `ClickHouseDialect.SupportsPercentRankCumeDist`:274, `EntityBuilder.Asc/Desc`:2110/2117), never by textual symbol search; documentary line ranges were checked with `nl -ba`. | artifacts/pdca/D233/rv1/r3/registry-ledger.json

2026-10-15T11:18:40Z | DO | r3 | n1/3 | **`D: EVIDENCE` closed** (all runs tree-bound to `/home/alex/sources/nextorm`; integration/coverage with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`): `["dotnet","build","nextorm.slnx","-c","Debug"]` exit 0 → **0 Warning(s) / 0 Error(s)**; `["dotnet","test","tests/nextorm.core.tests","-c","Debug"]` exit 0 → total 2546 / succeeded 2546 / failed 0 / skipped 0; six provider SQL-gen suites `["dotnet","test","tests/nextorm.<p>.tests","-c","Debug"]` all exit 0 with 0 failed — sqlite 1749 (1748 passed / 1 skip `SqliteRowIdLobProbeTests`, pre-existing and unrelated), sqlserver 970 (962 / 8 pre-existing skips `T232DerivedSourceReproTests`), postgres 978, mysql 456, mariadb 338, clickhouse 735; integration window sweep exit 0 → total **31 / succeeded 31 / failed 0 / skipped 0** (discovery: `Discovered 31 tests`), plus the literal `~Window` run 8/8 (0 skipped); `["dotnet","docfx","docs/docfx.json"]` exit 0 → 2 warning(s) / 0 error(s), both pre-existing `AnalyzerReleases.{Shipped,Unshipped}.md` duplicates unrelated to the window change; coverage (attempt 3) collect exit 0 → total 12016 / succeeded 11809 / failed 0 / skipped 207, report exit 0 → **line 88.9 % (53515/60194) ≥ 85, branch 80.9 % (28997/35805) ≥ 75**. | artifacts/pdca/D233/rv1/r3/{build,core-tests,sqlgen-sqlite,sqlgen-sqlserver,sqlgen-postgres,sqlgen-mysql,sqlgen-mariadb,sqlgen-clickhouse,integration-window-full,integration-window,docfx,coverage-collect-attempt3}.log, artifacts/pdca/D233/rv1/r3/coverage-report-attempt3/Summary.txt

2026-10-15T11:18:40Z | DO | r3 | n1/3 | **`D: MANIFEST` closed** — `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json` → **exit 0**. Independent inspection at the same boundary: 22 `required_rows` (17 `EC233-*` + `R233-1..5`), 22 unique row IDs covering them exactly, **22/22 `met`** (0 `open`, 0 `na`); the three rows open at ACT (`EC233-REGISTRY`, `EC233-MANIFEST`, `R233-5`) are now `met` with the r3 evidence; `contract_rv` stays 1 and every stable row ID/obligation is unchanged (rv=1 not revised); row evidence refreshed additively, earlier pointers retained. | artifacts/pdca/D233/rv1/r3/manifest-validate-r3.log, artifacts/pdca/D233/rv1/manifest.json

2026-10-15T11:18:40Z | DO | r3 | n1/3 | **DO complete (r=3, n=1/3): all D units closed; evidence green; manifest 22/22 met.** Phase → `CHECK`; plan_state stays `ready`, `r=3`, `n=1/3`, `rv=1`, group `G1`. This revision touched only the registry (stream A), the manifest and this status file — no product/guide edit, no commit, no push (G1 stays fail-stopped during DO). | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T11:18:40Z | DO | r3 | n1/3 | **Defect history.** `EC233-REGISTRY-STALE-LOCATORS` (observed r=2 n=3/3: W2 `:205,1138`, W4 `:207,1136`, NW2 `:2242`) → **closed**, fixes applied: 1 (registry locator repair), evidence `artifacts/pdca/D233/rv1/r3/registry-ledger.json`; `EC233-MANIFEST-OPEN-ROWS` (observed r=2 n=3/3: 3 open rows + 0-byte n3 validator log) → **closed**, fixes applied: 1 (manifest completion + validator exit 0), evidence `artifacts/pdca/D233/rv1/r3/manifest-validate-r3.log`; `EC233-COMPAT-overload-ambiguity` (r=1 → r=2 fix #1 `[OverloadResolutionPriority(-1)]`) re-observed r=3 = **no** (product byte-identical, zero new diagnostics; fixes applied to date: 1). No criterion weakened; CHECK cannot downgrade the P1 rows; no escalation triggered. | docs/specs/status/D233-window-over-overload-1.md

2026-10-15T11:26:15Z | CHECK | r3 | n1/3 | **CHECK evidence-completion (bounded pass, routed by the `check` verdict) — independent registry certification persisted; no DO iteration and no product/doc change.** The independent verification that the `check` stream had already performed and accepted was the only blocking gap (the persisted certification still read negative), and it is now PERSISTED, not re-invented. Issued positive certificates: `artifacts/pdca/D233/rv1/certifications/EC233-REGISTRY.json` → `{"row_id":"EC233-REGISTRY","verifier":"check","verifier_tier":"medium","rv":1,"certified":true,…}` (compact template shape, `subject` preserved: registry `docs/specs/design/API-NAMING-REVIEW.md` + signature `WindowFunction<T>.Over(Expression<Func<object?>>? partitionBy, WindowOrder orderBy, WindowFrame? frame = null)`) and `…/EC233-R233-5.json` → same shape, `certified:true`. The earlier open / `not_certified` records are **preserved verbatim as history** at `…/EC233-REGISTRY-not-certified-r2.json` and `…/EC233-R233-5-not-certified-r2.json` (superseded pointers added to the manifest notes; neither is read as a pass). `certifications/EC233-REGISTRY-audit.json` now records the ACTUAL r3 verification under `/r3_independent_verification`: the six repaired locator groups matched the committed tree — `:205` (guide EN 352-356 / RU 355-359; `Query/SqlFunctions.cs:26`=`Sql`; `Query/WindowFunctions.cs:17`=`WindowFunction<T>`; `SqlFunctions.cs:739`=`date_diff`; window block 934-980), `:207` (guide EN 308-312 / RU 310-314), `:1136` (`src/nextorm.clickhouse/ClickHouseDialect.cs:274`=`SupportsPercentRankCumeDist`; `CommonTestSuite.Window.cs:54` unchanged), `:1138` (guide 352-356), `:2241` (`WindowFunctions.cs` 65/103/120/205/214/217/224 = `Over(string)`/`WindowFrameType`/`WindowFrameExclusion`/`WindowFrame.Exclusion`/`Groups(WindowFrameBound,WindowFrameBound)`/`Groups(int,int)`/`WithExclusion`; `WindowDefinition.cs:9,29` unchanged), `:2242` (`WindowDefinition.cs:9` vs `WindowFunctions.cs:87`; `EntityBuilder.cs:2110,2117`); ledger `artifacts/pdca/D233/rv1/r3/registry-ledger.json` = 21 entries (19 repaired + 2 verified-unchanged); product identity `git diff --stat -- src tests` **empty**, `WindowFunctions.cs:50-54` = the new `[OverloadResolutionPriority(-1)]` overload and the five pre-existing overloads present; regression evidence build exit 0 (0W/0E), core 2546/0/0, six provider suites 0 failed, integration sweep 31/0/0 (0 skipped), DocFX exit 0, coverage attempt 3 exit 0 (line 88.9 % / branch 80.9 %). Prior audit findings are retained; the stale ones are marked under `/superseded_findings` (`/note`'s I-REGISTRY-not-performed implication, W2 `:205,1138` `not-checked`, the r2-only repair-scope statement) with `/not_superseded` listing what is carried unchanged. Manifest rows `EC233-REGISTRY` and `R233-5` relinked **additively** with a `kind:"certificate"` evidence entry, mirroring exactly how `EC233-BOUNDARY`/`EC233-CAPABILITY` attach theirs; `contract_rv` stays 1, every ID and obligation unchanged. Validator `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json` → **EXIT=0**, independently confirmed 22/22 `met` (22 required = 22 rows, 0 open, 0 na); log `artifacts/pdca/D233/rv1/r3/manifest-validate-r3-cert.log`. The independent CHECK certification of the registry is now **POSITIVE** — the only ACT blocker is closed. Phase stays `CHECK`, `r=3`, `n=1/3`, `rv=1`, group `G1`. | artifacts/pdca/D233/rv1/certifications/EC233-REGISTRY.json, artifacts/pdca/D233/rv1/certifications/EC233-R233-5.json, artifacts/pdca/D233/rv1/r3/manifest-validate-r3-cert.log

2026-10-15T11:32:06Z | ACT | r3 | n1/3 | **ACT done (corrective ACT, collection-owner-authorized; single writer of this file).** CHECK verdict **PASS** at `r=3`, `n=1/3`, `rv=1`: all **22/22 evidence rows `met`** (validator `EXIT=0`, `artifacts/pdca/D233/rv1/r3/manifest-validate-r3-cert.log`); `EC233-REGISTRY`/`EC233-MANIFEST`/`R233-5` closed; no replan, no counter reset (`r=3`, `n=1/3`, `rv=1`, N=1, `G1`). Actions: registry repair certified (13 stale locators repaired + 2 verified-unchanged; ledger 21 entries), positive CHECK certificates `certifications/EC233-REGISTRY.json` + `EC233-R233-5.json` (`certified:true`); no product change (`git diff --stat -- src tests` empty, `WindowFunctions.cs:50-54` byte-identical); collection reconciled — both T233 rows `done`, G1 fail-stop lifted, T201 restored to `pending`; **`#239 resolved-by-repair`** (the registry/manifest residual that #239 tracked is closed by this repair). `T233` → `done`; cycle N=1 closed; status file kept. | artifacts/pdca/D233/rv1/r3/manifest-validate-r3-cert.log, artifacts/pdca/D233/rv1/r3/registry-ledger.json, artifacts/pdca/D233/rv1/certifications/EC233-{REGISTRY,R233-5}.json, docs/specs/status/collection-1.0.9-todo.md

**Provider × variant → test/guard map (r=2 n=3/3; full data in `artifacts/pdca/D233/rv1/certifications/EC233-CAPABILITY-audit.json`).** CHECK attempt-2 row (7) = "`partitionBy: null` × single order" is now **placed** (`тест`):

| Variant | Closure | Test/guard locations |
|---|---|---|
| single partition × single `asc`/`desc` × no frame | test | sqlite:2333, postgres:2568, sqlserver:1722, mysql:470, mariadb:361, clickhouse:720, integration `CommonTestSuite.Window.cs:145` |
| **`partitionBy: null` × single order** (CHECK row 7) | **test** | sqlite:2394, postgres:2629, sqlserver:1783, mysql:515, mariadb:406, clickhouse:765, core `WindowFunctionMarkerTests.cs:118` |
| nullable value/reference partition | test | `CommonTestSuite.Window.cs:145` |
| `T`: value/nullable/reference | test | `WindowFunctionMarkerTests.cs:60` |
| `frame: WindowFrame.Rows(...)` | test | sqlite:2333, postgres:2568, clickhouse:720, `ClickHouseIntegrationTests.cs:782` |
| `Groups`/exclusion, capability=true | test | sqlite:2559, postgres:2776/2793, clickhouse:703, postgres:2841 |
| `Groups`/exclusion, capability=false | guard | sqlserver:263/280, mysql:578/595, mariadb:469/486, clickhouse:828 |
| old expression/array/order-only/optional forms | test | `WindowFunctionMarkerTests.cs:29,41,164,185` + full build |
| named-window | test+guard | core:52, sqlite:2576, mysql:561, mariadb:452, clickhouse:811, postgres:2822 |
| untyped `default`/`null`, typed defaults | test | `WindowFunctionMarkerTests.cs:164` + EC233-COMPAT probe |
| six dialects | test | each dialect project `~Window`, exit 0 |
| SQLite + 4 container providers | test | `CommonTestSuite.Window.cs:145`, `ClickHouseIntegrationTests.cs:750,782` |
| separate MariaDB container integration | deferred + trigger | trigger: a MariaDB integration fixture (T232) or a provider-specific change → replan in the same milestone |
| `NULLS FIRST/LAST`, new functions/flags | deferred | separate agreed task in the milestone |
| global API-analyzer config | deferred | separate PublicAPI-baseline task; manual registry + certification remain mandatory |

**Capability flags (unchanged by T233):** `SupportsWindowFrameGroups` — interface `ISqlDialect.cs:365`, base `SqlDialectBase.cs:118 (false)`, opt-in `SqliteDialect.cs:165` / `PostgresDialect.cs:372` / `ClickHouseDialect.cs:300`; `SupportsWindowFrameExclusion` — `ISqlDialect.cs:372`, base `SqlDialectBase.cs:120 (false)`, opt-in `SqliteDialect.cs:168` / `PostgresDialect.cs:375`; gate sites `WindowFunctionTranslator.cs:92,95`.

- `plan_state: ready`
- `plan_revision: r2` (r1 superseded by the DO→PLAN loop-back)
- `evidence_contract_revision: rv1` (unchanged)
- `planned_writes_public_api: src/nextorm.core/Query/WindowFunctions.cs (PUBLIC API addition — add one overload)`
- `planned_writes_other`: `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs`; `tests/nextorm.sqlite.tests/SqlGenerationTests.cs`; `tests/nextorm.postgres.tests/SqlGenerationTests.cs`; `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`; `tests/nextorm.mysql.tests/SqlGenerationTests.cs`; `tests/nextorm.mariadb.tests/SqlGenerationTests.cs`; `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`; `tests/nextorm.integration.tests/CommonTestSuite.Window.cs`; `docs/guide/11-window-functions.md`; `docs/ru/guide/11-window-functions.md`; `docs/specs/design/API-NAMING-REVIEW.md`; `docs/specs/status/D233-window-over-overload-1.md`
- `prerequisites`: (1) existing user approval covers **only the plan** — a separate recorded approval is required before any implementation edit; (2) T233 runs **after** T232 within G1 — requires the T232 handoff (final diff + public-API/worker/capability changes), no parallel edits to `Query/WindowFunctions.cs` / `Builders/**` / `Dialect/**`; (3) mandatory compiler compatibility probe (baseline→candidate) for untyped `default`/`null` at the old call sites must pass before the public API edit; (4) required containers/toolchain must be available (integration + coverage).
- `requirements_on_siblings`: T232 completes and is certified in G1 / `1.0.9-rc3`; hands off its final diff; stops touching the shared window builder / `Builders/**` / `Dialect/**`; if T232 already added an overlapping signature or changed the arg mapper/capability boundary, T233 returns to PLAN (no duplication).
- `all_barrier_contribution`: T233 PLAN complete = plan_state ready; the task's contribution to the collection ALL-barrier for the PLAN phase is satisfied (this run does not execute DO).
- `next_allowed_step`: `DO (in a later, separately authorized run) — unit D:233.1 (gate + compatibility probe); no implementation before recorded approval`

## ACT — done (r=3, n=1/3, rv=1; registry/manifest repair, CHECK PASS)

ACT outcome (single writer of this file; collection-owner-authorized corrective ACT). **CHECK verdict: PASS** at plan revision `r=3`, iteration `n=1/3`, evidence contract `rv=1` — all **22 evidence rows `met`** (validator **exit 0**, log `artifacts/pdca/D233/rv1/r3/manifest-validate-r3-cert.log`), zero `open`, zero `na`; `EC233-REGISTRY`, `EC233-MANIFEST` and `R233-5` are closed. **No replan, no counter reset** — `r=3`, `n=1/3`, `rv=1`, cycle N=1, group `G1`. Task **`T233` → `done`**; the cycle is closed and this status file is kept.

### §ACT actions (this corrective ACT)

- **Registry repair certified.** 13 stale locators repaired + 2 verified-unchanged in the touched registry entries of `docs/specs/design/API-NAMING-REVIEW.md` (W2 `:205`/`:1138`, W4 `:207`/`:1136`, NW2 `:2242`, RD2 batch-3 `:2241`); ledger `artifacts/pdca/D233/rv1/r3/registry-ledger.json` (**21 entries = 19 repaired + 2 verified-unchanged**). Every C# locator resolved with `roslyn` (`members`/`types`), documentary line ranges with `nl -ba` — never textual symbol search.
- **Positive CHECK certificates issued:** `artifacts/pdca/D233/rv1/certifications/EC233-REGISTRY.json` and `…/EC233-R233-5.json` (`certified:true`, `verifier: check`, medium tier, `rv=1`); the earlier open / `not_certified` records are preserved verbatim as history (`…-not-certified-r2.json`) and are not read as a pass.
- **No product change.** `git diff --stat -- src tests` is empty; the new overload `src/nextorm.core/Query/WindowFunctions.cs:50-54` is byte-identical.
- **Collection reconciliation** (`docs/specs/status/collection-1.0.9-todo.md`): both T233 rows → `done (r=3, rv=1, n=1/3)`; **G1 fail-stop lifted** (`in-progress`); **T201 (#201) restored to `pending`**; the `#239` note recorded.
- **Follow-up issue `#239` — resolved-by-repair:** the stale registry/manifest residual that #239 tracked is closed by this repair (registry locators verified in place, manifest 22/22 `met`, validator exit 0). No separate product fix is required.

### Final evidence pointers

- `artifacts/pdca/D233/rv1/r3/**` — `manifest-validate-r3-cert.log` (validator exit 0), `registry-ledger.json`, `build.log` (0W/0E), `core-tests.log`, `sqlgen-*.log`, `integration-window{,-full}.{log,*.discovery.log}`, `docfx.log` (exit 0; 2 pre-existing unrelated warnings), `coverage-collect-attempt3.log` + `coverage-report-attempt3/Summary.txt` (line 88.9 % / branch 80.9 %).
- `artifacts/pdca/D233/rv1/certifications/{EC233-REGISTRY.json,EC233-R233-5.json}` (`certified:true`), `EC233-REGISTRY-audit.json` (`/r3_independent_verification`), `EC233-REGISTRY-not-certified-r2.json` / `EC233-R233-5-not-certified-r2.json` (history).
- `artifacts/pdca/D233/rv1/manifest.json` — `rv=1`, **22/22 rows `met`**.
- Retained product commits `1429fd21`, `d5823ffb`, `e65b601e` (never pushed); this ACT commit carries the two status files. The status file is **kept** (not deleted).

## ACT — terminal `incomplete (product green; evidence residual: EC233-REGISTRY, R233-5, EC233-MANIFEST)` (superseded)

> **Superseded (2026-10-15T10:53:46Z) by the corrective re-open — PLAN r=3, `n=1/3`.** Reason: "corrective re-open authorized by collection owner to close EC233-REGISTRY/EC233-MANIFEST/R233-5 without product change". This terminal-ACT record is retained as history (nothing deleted); the active plan is `## PLAN r=3 (current)`.

ACT outcome (single writer of this file). **No replan** — `plan_revision` stays `r=2`, `evidence_contract` stays `rv=1`, `iteration` stays `n=3/3`. The cycle terminates **definitively `incomplete`** (route: the independent CHECK certification after the `escalate`-authorized bounded re-gather). The product/build/docs work is green; only registry-manifest residuals remain.

### Certification matrix (verifier `check`, medium tier, `rv=1`)

**Certified `met`** (positive certificates under `artifacts/pdca/D233/rv1/certifications/`):

- `R233-1`, `R233-2`, `R233-3`, `R233-4`
- `EC233-GATE`, `EC233-COMPAT`, `EC233-MARKER`
- `EC233-SQL-SQLITE`, `EC233-SQL-POSTGRES`, `EC233-SQL-SQLSERVER`, `EC233-SQL-MYSQL`, `EC233-SQL-MARIADB`, `EC233-SQL-CLICKHOUSE`
- `EC233-INTEGRATION`, `EC233-BUILD`, `EC233-COVERAGE`
- `EC233-DOCS` → `EC233-DOCS.json` (`certified:true`)
- `EC233-BOUNDARY` → `EC233-BOUNDARY.json` (`certified:true`)
- `EC233-CAPABILITY` → `EC233-CAPABILITY.json` (`certified:true`)

**Open / `not_certified`** (blocker; uncertifiable in-cycle):

- `EC233-REGISTRY` → `EC233-REGISTRY.json` (`open` / `not_certified`) — stale `file:line` references in the touched registry entries.
- `R233-5` → `EC233-R233-5.json` (`open` / `not_certified`) — depends on the registry certification.
- `EC233-MANIFEST` → recorded `open` / unmet (three mandatory rows open; the r=2/n=3 validator log was 0 bytes).

### Registry blocker (why the cycle cannot close green)

The touched registry entries in `docs/specs/design/API-NAMING-REVIEW.md` still carry stale references: **W2** `:205,1138` points to sample text instead of the `Source:` footer; **W4** `:207,1136` cites the Frames section rather than the intended section and the flag `:64` instead of `:274`; **NW2** `:2242` cites `WindowFunctions.cs:64` instead of `:87`. Registry repair was forbidden in the escalation-authorized pass, so the defect cannot be fixed in-cycle. Follow-up issue **#239** — https://github.com/AlexeyShirshov/nextorm/issues/239 — tracks the four stale pairs.

### Escalate decision + bounded re-gather journal

- `escalate` (strong tier) authorized a CHECK `n=3` **bounded re-gather** (journaled step, **NOT** a DO iteration; no fourth attempt, no `r=3`). Hard constraints respected: no `src`/`tests`/public-docs/registry edits; no big-suite reruns; no certificates created by the re-gather.
- **Request 1 of 2** — `artifacts/pdca/D233/rv1/certifications/CHECK-n3-regather-request1.json`: raw extracts (scope diff, EN/RU docs sections, DocFX lines, capability refs, 39 citation existence checks, 7 guard exact-name runs, W4 correction, manifest row states); `previous_sha 7bdea656`, `head_sha e65b601e`.
- **Request 2 of 2** — this ACT: live manifest validation `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json` → **exit 0**, log `artifacts/pdca/D233/rv1/logs/manifest-validate-n3.log` (non-empty, 16 bytes, `MANIFEST_EXIT=0`), after the additive manifest reconciliation.

### Retained commits (branch `1.0.9-rc3`, never pushed)

- `1429fd21` — 12 files (product overload + 6 dialect `SqlGenerationTests.cs` + core marker + integration `CommonTestSuite.Window.cs` + 2 guide pages + registry).
- `d5823ffb` — 2 files (ClickHouse integration mirror + registry N233-2 limitation / LangVersion line).
- `e65b601e` — 7 files (null-partition no-`PARTITION BY` parity: 6 dialects + core marker).

### Product/test/build evidence (final, retained)

- build `dotnet build nextorm.slnx -c Debug` → **0W / 0E**; six dialect window suites + core marker **17/17**; integration **30/30** + ClickHouse **4/4**, **0 skipped**; coverage line **88.9 %** / branch **80.9 %** (≥ 85 / 75); DocFX **exit 0** (2 pre-existing unrelated warnings).

### Deferred / trigger rows carried to live tracking (unchanged)

- separate MariaDB container integration — `deferred + trigger` (a MariaDB integration fixture appears as a result of T232, or a provider-specific change lands) — no separate task created.
- `NULLS FIRST/LAST`, new functions/flags — `deferred` (only a separately agreed milestone task introduces them).
- global API-analyzer configuration (PublicAPI baseline) — `deferred`; the manual registry + independent certification remain mandatory now.
- new public `Over` overload registered as a **T53 freeze candidate** — **freeze remains HELD** (not activated).

### Per-row evidence pointers

- `artifacts/pdca/D233/rv1/manifest.json` (rv=1 contract, 22 rows: 19 `met`, `EC233-REGISTRY` / `R233-5` / `EC233-MANIFEST` `open`).
- certificates `artifacts/pdca/D233/rv1/certifications/{EC233-CAPABILITY,EC233-DOCS,EC233-BOUNDARY,EC233-REGISTRY,EC233-R233-5}.json`; audits `…-audit.json`; re-gather `CHECK-n3-regather-request1.json`.
- logs `artifacts/pdca/D233/rv1/logs/{build,core-marker,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-n3.log`, `exact-nullpart-*-n3.log`, `guard-*-n3.log`, `integration-window.log`, `clickhouse-window.log`, `coverage-*`, `docfx.log`, `manifest-validate-n3.log`.

---

## PLAN r=3 (current)

**Inherited evidence contract — present, not duplicated.** The frozen 22-row evidence contract (`rv=1`) is already materialized in this status file from r=1/r=2: the 17 `EC233-*` required rows are enumerated in `## 7. Evidence contract — frozen rv=1` (`### Frozen required_rows` + the row table), and `R233-1..5` are in `## 2. Acceptance criteria`. r=3 records **only the delta below**; the 22 rows, their stable IDs and obligations are carried over unchanged (r=3 does not itself revise `rv`).

---

P: T233 / #233 / milestone 1.0.9-rc3 — corrective plan r=3, n=1/3, plan_state=ready; supersedes plan r=2, not D233 or its acceptance criteria. D233 reopens active.
- Goal: close EC233-REGISTRY, EC233-MANIFEST and R233-5 honestly, without changing the green product.
- Acceptance R233-1..4: retain their exact existing wording at :273-279 and all associated checks; new overload `WindowFunctions.cs:50-54` remains byte-identical and the five existing overloads unchanged. Negative: any product-source delta rejects closure.
- Acceptance registry: every stale reference in `docs/specs/design/API-NAMING-REVIEW.md` resolves to a currently verified `file:line`, including stale `Source:` footers. Negative: one unresolved/stale locator fails EC233-REGISTRY.
- Acceptance R233-5/manifest: all 22 required rows are `met`; the three named rows cannot be deferred or merely relabelled. Negative: missing artifacts, mismatched fingerprints or an open row fails closure.
- Acceptance regression evidence: build 0W/0E, marker/core tests, six SQL-generation suites, container-backed Window integration with zero skipped, DocFX and coverage pass. Negative: missing, skipped, stale or foreign-worktree evidence is not passing evidence.
- Constraints: no product edits. One documentation/evidence unit.
- D: REGISTRY — fix now: repair W2 :205/:1138, W4 :207/:1136, NW2 :2242 and every additional stale locator discovered in this same registry; inspect N233-1/N233-2 :5789-5800 for consistency.
- D: AUDIT — fix now: record old locator -> verified current locator, verification source and result in an evidence ledger; resolve C# symbols with Roslyn, never textual symbol searches; numbered-file inspection for documentary locations.
- D: EVIDENCE — fix now: run the commands below from `/home/alex/sources/nextorm`; retain complete logs and native test reports.
- Command setup: `set -euo pipefail; export DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; load the integration-running skill and perform its Podman recovery if necessary.
- Build: `dotnet build nextorm.slnx -c Debug` (must be 0 Warning(s) 0 Error(s)).
- Regression: `dotnet test tests/nextorm.core.tests -c Debug`.
- Six-provider SQL generation: `for p in sqlite sqlserver postgres mysql mariadb clickhouse; do dotnet test "tests/nextorm.$p.tests" -c Debug; done`.
- Integration: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~Window"`; require executed provider coverage and 0 skipped.
- DocFX: `dotnet docfx docs/docfx.json`.
- Coverage: `dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D233/rv1/r3/coverage.cobertura.xml`; `reportgenerator "-reports:artifacts/pdca/D233/rv1/r3/coverage.cobertura.xml" "-targetdir:artifacts/pdca/D233/rv1/r3/coverage-report" "-reporttypes:TextSummary"`; require line >=85%, branch >=75% for the configured scope.
- D: MANIFEST — fix now: refresh row evidence pointers/results and provenance, then run `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json`; require exit 0 plus independent inspection confirming 22/22 `met`.
- Test scope: write `artifacts/pdca/D233/rv1/r3/test-scope.json`: docs-only, `rebuild: none` for registry impact, product unchanged, no new tests; the mandated build/tests are renewed regression evidence.
- Variant/branch-delta matrix: product/API branch delta is zero; preserve the existing input/null/default/value/reference/frame/provider matrix as regression guards. Six SQL providers and inherited integration variants remain required; no new variant or flag introduced.
- Execution mode: sequential, single unit, current tree/branch `1.0.9-rc3`; no worktree, branch, merge, commit or push during DO. G1 stays fail-stopped during DO; the fail-stop lift and T201 restore-to-pending are ACT-phase actions under separate collection-owner authorization.
- Priority matrix: the three open rows, source-identity invariant, all original R233 rows and their prerequisite evidence are P1; CHECK cannot downgrade them.
- Docs footprint: only `docs/specs/design/API-NAMING-REVIEW.md`, D233 manifest/status and T233-only collection metadata; generated D233 evidence allowed. No public-doc edits, renames or generated DocFX files committed; preserve CRLF.
- Performance: no acceptance benchmark needed: registry edits at :205/:207/:1136/:1138/:2242 execute neither per query nor per row; `WindowFunctions.cs:50-54` unchanged.
- Reconnaissance: no solution-selection spike needed; the defect and repair are established. Bounded locator verification is required evidence gathering, not product investigation.
- Evidence contract: retain rv=1, all 17 existing EC233 IDs and R233-1..5, with unchanged stable IDs and obligations; r=3 does not itself revise rv.
- CHECK re-gather budget: CHECK owns at most two targeted evidence-only passes: one locator/provenance repair pass and one final validation pass; no product DO or counter reset because a report is absent.
- Risks/out-of-scope: unrelated SVGs, examples, agent/config files, AGENTS.md, D218/D232 artifacts and every other task's files are read-only. Preserve the #239 note; resolution of #239 requires separate authorization.
- Failure classification: missing evidence -> targeted scout within budget; genuine product regression or external blocker -> report and route to planner/escalate, never silently expand scope.
- Gate: coder records this complete r=3 plan and references the inherited 22-row contract before DO.
---

## PLAN r=2 (superseded by r=3 — retained as history) — revision delta over r=1

`planner` revised plan, issued after the r=1 probe FAIL (`EC233-COMPAT-overload-ambiguity`). Only the disambiguation mechanism changed; the goal, scope, non-goals, design signature, variant matrix, evidence contract and R233-1..5 are carried over from r=1 **verbatim**.

### Change (r=1 → r=2)

1. **Signature unchanged.** The planned overload stays exactly

   ```csharp
   [System.Runtime.CompilerServices.OverloadResolutionPriority(-1)]
   public T Over(
       Expression<Func<object?>>? partitionBy,
       WindowOrder orderBy,
       WindowFrame? frame = null) => default!;
   ```

   The only addition versus r=1 is the `[OverloadResolutionPriority(-1)]` attribute **on that new overload only**. The five existing overloads keep priority 0 and **must win** whenever they are applicable.

2. **Source-compatibility rule (r=1 §4/§12, tightened):** the six r=1 baseline-success → candidate-ambiguous call shapes (`Over(partitionBy: e, orderBy: default)`, `Over(partitionBy: en, orderBy: null)`, `Over(partitionBy: () => 1, orderBy: default)`, `Over(partitionBy: () => 1, orderBy: null)`, `Over(() => 1, orderBy: default)`, `Over(() => 1, orderBy: null)`) must **compile and select their original overload**. R233-2 is **not weakened**; **no ambiguity is accepted**; no existing call site may be "repaired".

3. **Escalation rule.** If any supported-consumer case is still ambiguous after the attribute ⇒ **STOP and report** (return to PLAN). Do not relax R233-2.

4. **Probe prerequisite (P0), satisfied** — see the progress-log entry `P0 prerequisite (compiler/language floor …)`: `net10.0` + `LangVersion latest` + `TreatWarningsAsErrors`, `global.json` pinning no SDK, and the attribute already used 6× in `src/nextorm.core` (`DataContext/Meta/EntityMetadataBuilder.cs:794`, `Builders/MergeBuilder.cs:201`) ⇒ no silent new minimum for consumers.

5. **Extended differential probe requirement.** Before any product edit, re-run the r=1 differential probe with the attribute and prove **binding** (not just exit status) per expression: which overload each of the six shapes selects, the complete r=1 variant matrix, typed defaults, the five existing signature pins, and the intended new 2-/3-argument forms. PASS = zero new diagnostics + six shapes on the original overloads + new forms on the new overload. Otherwise STOP (no repo edit).

### Carried over verbatim from r=1

Goal/scope/non-goals (§1), acceptance criteria **R233-1..5** (§2), chosen design signature and XML-doc obligation (§3), variant matrix (§4), test strategy and structured `test scope` (§5), documentation plan (§6), evidence contract **rv=1** (§7, unchanged), performance decision (§8), reconnaissance decision (§9), execution mode (§10), design checklist (§11), risks/prerequisites (§12), ordered task list (§13), write-footprint (§14), self-check (§15) — all as written in the r=1 block below, with the r=2 changes above layered on top.

---

## PLAN r=1 (superseded by r=2 — retained as history)

---
# T233 — PLAN r=1

**Краткая сводка**
- **Решение:** план r=1 подготовлен; evidence contract **rv=1 заморожен**.
- **DO запрещён:** текущий запуск — PLAN only; отдельно требуется утверждение этого плана.
- **Дизайн:** один публичный marker-overload, без изменения SQL renderer.
- **Зависимость:** T233 выполняется **после T232** в G1, в текущем дереве.
- **Основной риск:** совместимость вызовов с нетипизированным `default` должна быть доказана; отсутствие defaults у первых параметров само по себе этого не доказывает.

## 1. Goal; scope / non-goals

### Цель

Позволить выразить окно с **одним partition expression и одним `WindowOrder` без пользовательских массивов**, сохранив SQL, существующее поведение и ограничения провайдеров.

### В scope

- Один overload `WindowFunction<T>.Over`.
- Marker-, compiler-compatibility-, dialect- и integration-проверки.
- EN/RU руководства, реестр API.
- Доказательства, статус и независимая CHECK-сертификация.

### Не в scope

- Новые SQL-функции, направления сортировки, `NULLS FIRST/LAST`.
- Новые capability flags или расширение поддержки окон.
- Изменение существующих overloads, translator, planner, executor, cache.
- Массовая миграция существующих array-call sites.
- Новые branches/worktrees, merge или push.
- Настройка PublicApiAnalyzers/ApiCompat для всего проекта.

**Freeze/public API:** новая сигнатура — аддитивное изменение публичной поверхности, но не автоматически source-compatible изменение. До отдельного утверждения плана разрешена только его фиксация в статусе; правки реализации, тестов, публичных документов и реестра запрещены. Этот запуск заканчивается на PLAN→DO и **DO не начинает**.

## 2. Acceptance criteria

| ID | Наблюдаемый критерий | Негативный случай |
|---|---|---|
| **R233-1** | Для одинаковых partition/order/frame scalar- и array-формы дают **побайтно одинаковую SQL-строку** на всех шести dialect-проектах. Проверяются `asc`, `desc`, `partitionBy: null`, nullable partition. | Потеря partition/order/frame, смена направления или любое различие SQL — fail. |
| **R233-2** | Существующие сигнатуры неизменны; старые 39 call sites собираются без изменений. Типизированные expression/order/array/named/optional формы сохраняют binding и поведение. Differential compiler-probe сохраняет успешность ранее допустимых `null`/`default` вызовов. | Новый CS0121, смена выбранного overload или необходимость «починить» старый вызов — fail, возврат DO→PLAN. |
| **R233-3** | `frame:` поддерживается при позиционных и именованных первых аргументах; `WindowFrame.Rows` даёт ту же SQL и результат, что array-форма. `Groups`/exclusion не получают новых возможностей. | Frame игнорируется, неверно отображается или обходит существующее ограничение провайдера — fail. |
| **R233-4** | Build: exit 0, **0 warnings / 0 errors**. Обязательные тесты проходят; контейнерные провайдеры реально исполнены. Coverage по `coverage.settings.xml`: **line ≥85%, branch ≥75%**, независимо от warning-only режима RC-ветки. | Недобор любого порога, skipped обязательного провайдера, неполный отчёт или warning — не pass. |
| **R233-5** | EN и RU показывают scalar-форму, array-эквивалент и `frame:`. Реестр фиксирует точную публичную сигнатуру и ограничения совместимости; отдельный CHECK-поток сертифицирует запись. | Только один язык, неверная optionality, устаревшая ссылка в затронутой записи или самосертификация автором — fail. |

**Доказуемость:** перечисленные критерии проверяемы. Для R233-2 есть **открытый compatibility gap**, а не доказательство совместимости: нетипизированный `default` может сделать новый overload конкурентом старому expression-overload. Этот риск **не разрешает ослабить R233-2**.

## 3. Chosen design — r=1

Добавить в `src/nextorm.core/Query/WindowFunctions.cs:16–57`:

```csharp
public T Over(
    Expression<Func<object?>>? partitionBy,
    WindowOrder orderBy,
    WindowFrame? frame = null)
```

Первые два параметра **обязательны**. Имена `partitionBy`, `orderBy`, `frame` являются частью контракта с существующим argument mapper.

Реализация — marker, возвращающий `default` так же, как соседние overloads. Не вызывать array-overload, не создавать массивы, не исполнять expressions.

### Обоснование binding

- `:28`: типизированный `WindowOrder` не является `Expression<Func<object?>>`; `Over(expr, expr)` остаётся старой формой.
- `:34`: форма order-only не конкурирует с новым обязательным partition.
- `:42`: named-window форма не меняется.
- `:49`: требуется массив order.
- `:57`: требуются массивы; типизированные array-вызовы не конкурируют.
- `Over()`, `Over(frame: ...)`, partition-only вызовы не получают нового applicable candidate.
- **Нетипизированные `default` и, в зависимости от типа `WindowOrder`, `null` требуют отдельного compiler-probe.** Разведочное заключение об отсутствии ambiguity для типизированных аргументов нельзя расширять на эти литералы.

### Translator

Изменения **не нужны**:

- `Visitors/WindowSql.cs:139–171` распределяет аргументы по именам.
- `WindowSql.cs:63–77,102–120` уже обрабатывает single expressions/order.
- `Visitors/WindowFunctionTranslator.cs:29–33` распознаёт методы, объявленные на `WindowFunction<T>`.
- Общий rendering остаётся `WindowFunctionTranslator.cs:107–179`.

Если тест обнаружит необходимость правки worker, **не расширять scope самостоятельно**: новая `P:`-задача, фактический replan с сохранением критериев.

### XML documentation

**Обязательна:** summary, все три `<param>`, marker/default-return semantics и назначение scalar-формы. Не обещать исполнение метода в памяти или новую поддержку frames.

### Альтернативы

| Подход | Плюсы | Минусы / цена / риск | Решение |
|---|---|---|---|
| Прямой marker-overload | Минимальный API; использует существующий mapper | Требует compiler-compatibility доказательства | **Выбран, с обязательным probe до API-правки** |
| Новый overload плюс специальная ветка renderer | Явный отдельный путь | Дублирование; лишняя query-path поверхность без необходимости | Отклонён |
| Иное имя/extension helper | Может избежать overload competition | Не выполняет требование `WindowFunction<T>.Over`; translator ограничен declaring type | Отклонён |

## 4. Variant matrix

| Вариант | Закрытие |
|---|---|
| Single partition × single `asc`/`desc` × frame omitted/null | **Тест:** точное SQL parity на шести dialect-проектах; integration parity. |
| `partitionBy: null` × single order | **Тест:** отсутствие `PARTITION BY`; parity с array-null/order-only формой. **Закрыто (corrective r=2 n=3/3):** `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:2394`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:2629`, `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1783`, `tests/nextorm.mysql.tests/SqlGenerationTests.cs:515`, `tests/nextorm.mariadb.tests/SqlGenerationTests.cs:406`, `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:765`, `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs:118`. |
| Nullable value/reference partition expression | **Тест:** SQL parity и NULL-partition semantics в общей integration-проверке. |
| `T`: value, nullable value, reference | **Тест:** marker возвращает соответствующий `default`, expressions не исполняются. |
| `frame: WindowFrame.Rows(...)` | **Тест:** positional/named binding, SQL parity и исполняемый результат. |
| `Groups` / exclusion, capability=true | **Тест:** scalar/array SQL parity; существующая поддержка без изменений. |
| `Groups` / exclusion, capability=false | **Guard:** нет новой поддержки. Если array-форма отвергается — scalar даёт тот же отказ; если renderer допускает SQL — проверяется SQL parity, но unsupported frame не исполняется как positive DB case. |
| Старые expression-, array-, order-only и optional формы | **Тест:** compiler corpus, существующие marker/dialect suites и полный build. |
| Named-window | **Тест:** старый binding и существующие suites; **guard:** новый overload не является named-window API. |
| Нетипизированный `default`; `null`; typed defaults | **Тест:** baseline→candidate differential compilation. Любая новая ошибка старого успешного вызова блокирует выбранный дизайн. |
| SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse | **Тест:** каждый dialect-проект, без исключений. |
| SQLite + четыре существующих container-backed providers | **Тест:** общая integration suite; обязательные window-cases не skipped. |
| Отдельная MariaDB container integration | **Guard:** не добавляется новый fixture/provider launcher; MariaDB обязательно покрывается dialect-тестами. Триггер дополнительной DB-проверки — наличие MariaDB integration fixture в результате T232 или появление provider-specific изменения. Тогда prerequisite/replan в том же milestone. |
| `NULLS FIRST/LAST`, новые функции/flags | **Deferred:** только отдельная явно согласованная задача в текущем milestone; T233 их не вводит. |
| Глобальная настройка API analyzers | **Deferred:** отдельная задача public-API baseline в том же milestone; ручной реестр и независимая сертификация обязательны сейчас. |

## 5. Test strategy

### Unit / compiler

1. `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs:28–51`:
   - новая scalar-форма;
   - value/nullable/reference `T`;
   - null partition и frame;
   - expressions не исполняются;
   - существующие marker-формы остаются.

2. Disposable compiler corpus:
   - baseline API **после T232, до API-правки T233**;
   - candidate API;
   - старые сигнатуры, named/positional/optional/array формы;
   - отдельно `Over(partitionExpression, default)` и null/typed-default варианты.
   - baseline-success → candidate-failure запрещён.
   - временные проекты только в `/tmp/nextorm-D233-compat/`; будущие test symbols не фиксируются заранее.

3. Все шесть dialect SQL-generation suites:
   - paired scalar/array assertions;
   - exact SQL equality;
   - asc/desc, nullable/null partition, omitted/null/Rows frame;
   - supported Groups/exclusion и сохранение existing unsupported behavior.

### Integration

Добавить parity-сценарий в `tests/nextorm.integration.tests/CommonTestSuite.Window.cs:14–131`, используя существующие window fixtures:

- scalar и array дают одинаковые упорядоченные результаты;
- уникальный order key исключает случайный порядок ties;
- NULL partition;
- `Rows` frame;
- SQLite, PostgreSQL, SQL Server, MySQL, ClickHouse реально исполняются.

Перед запуском загрузить `running-integration-tests`. Если socket отсутствует — выполнить документированный startup/retry; отсутствие контейнеров не превращать в pass.

### Coverage

Полный предусмотренный сбор по `coverage.settings.xml`; оба порога обязательны. Отдельный changed-lines coverage threshold не вводится.

### Structured `test scope`

```json
{
  "projects": [
    "tests/nextorm.core.tests",
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.postgres.tests",
    "tests/nextorm.sqlserver.tests",
    "tests/nextorm.mysql.tests",
    "tests/nextorm.mariadb.tests",
    "tests/nextorm.clickhouse.tests",
    "tests/nextorm.integration.tests"
  ],
  "selectors": {
    "marker_fast": "FullyQualifiedName~WindowFunctionMarkerTests",
    "dialect_final": "all tests in each listed dialect project",
    "integration_final": "all tests in nextorm.integration.tests; required window cases must execute on SQLite, PostgreSQL, SQL Server, MySQL and ClickHouse",
    "coverage": "solution test run under coverage.settings.xml",
    "compatibility": "baseline/candidate compiler corpus; no invented future test names"
  },
  "files": [
    "tests/nextorm.core.tests/WindowFunctionMarkerTests.cs",
    "tests/nextorm.sqlite.tests/SqlGenerationTests.cs",
    "tests/nextorm.postgres.tests/SqlGenerationTests.cs",
    "tests/nextorm.sqlserver.tests/SqlGenerationTests.cs",
    "tests/nextorm.mysql.tests/SqlGenerationTests.cs",
    "tests/nextorm.mariadb.tests/SqlGenerationTests.cs",
    "tests/nextorm.clickhouse.tests/SqlGenerationTests.cs",
    "tests/nextorm.integration.tests/CommonTestSuite.Window.cs"
  ],
  "rebuild": {
    "required": true,
    "configuration": "Debug",
    "solution": "nextorm.slnx",
    "no_build_for_first_run": false,
    "baseline": "capture API after T232 and before T233 API edit",
    "final": "rebuild and rerun against the final unchanged diff"
  },
  "boundary": "New WindowFunction<T>.Over signature and shared argument mapping/rendering; existing overloads, six dialects and existing real-database window paths are regression boundaries.",
  "rationale": "A public overload can change compilation even when generated SQL is unchanged. Shared rendering requires provider parity and mandatory database evidence."
}
```

## 6. Documentation plan

**Fix now после approval:**

- `docs/guide/11-window-functions.md:105–116`;
- `docs/ru/guide/11-window-functions.md:106–116`.

В обеих страницах: scalar пример, эквивалентная array-форма, `frame: Rows`, обязательность первых двух аргументов. Compatibility guidance должна соответствовать доказанному compiler behavior, а не маскировать регрессию.

- `docs/specs/design/API-NAMING-REVIEW.md:2055,2076–2081` и соответствующие W1–W4/NW2:
  - точная новая сигнатура;
  - additive public-API отметка;
  - существующие overloads сохранены;
  - compiler evidence;
  - автор записи и отдельный CHECK-certifier.

**Stale `10-window-functions.md` references:** **fix now**, только существующие ссылки на window-guide в затронутых W1–W4/NW2, заменить на `09`. Не делать общий cleanup реестра.

**Не трогаем:** guide numbering, TOCs, indexes, advanced API overview, examples, capability roadmap/matrix. Нет переименования страницы или существующего symbol. Public docs не получают ссылок на `docs/specs/**`.

## 7. Evidence contract — frozen rv=1

### Идентичность и хранение

- Task slug: `D233-window-over-overload`.
- Status: `docs/specs/status/D233-window-over-overload-1.md`.
- Frozen manifest: `artifacts/pdca/D233/rv1/manifest.json`.
- **Notice — locator repair (corrective DO r=2 n=3/3, 2026-10-15).** The r=1/r=2 frozen contract text spelled every artifact locator as `artifacts/pdca/D233-window-over-overload/rv1/**` (the **original frozen path**; e.g. the canonical manifest locator was `artifacts/pdca/D233-window-over-overload/rv1/manifest.json`). The artifacts were actually written under the **real path** `artifacts/pdca/D233/rv1/**` — the overlay's default slug directory is the task slug `D233`, not `D233-window-over-overload` — so the frozen string was a plan-text typo, not a missing artifact. **Only the locators were repaired**; no obligation, required-row, exit requirement or acceptance criterion was weakened, dropped or renamed. The directories named now all resolve on disk (`test -d`): `artifacts/pdca/D233/rv1/` (manifest.json, boundary-evidence.json, logs/, coverage/, certifications/, probe/, test-stream/), `artifacts/pdca/D233/rv1/coverage/`, `artifacts/pdca/D233/rv1/certifications/`, `artifacts/pdca/D233/rv1/logs/`. Two planned artifacts named in the frozen catalog were never materialised and are **not** obligations: the per-row package directory `artifacts/pdca/D233/rv1/rows/` (the manifest carries each row's evidence by reference inline in `rows[]`, with raw logs under `artifacts/pdca/D233/rv1/logs/`) and the disposable helpers `audit.py`/`compat_probe.py` (self-declared as "планируемые evidence helpers, не production tooling"); their *checks* were performed by the documented commands and manual inspection recorded in the rows.
- Все перечисленные ниже источники и артефакты **планируемые**, не существующие доказательства.
- `r=1`, `rv=1`; supersession отсутствует.
- Любая обоснованная ревизия должна явно supersede rv=1, сохранить ID и обязательства строк, добавить новые variant IDs. Отсутствующий отчёт не основание для ревизии.

### Общие обязательные слоты каждой строки

Для каждой строки таблицы:

- `row_id`, стабильные `requirement_ids`, `rv=1`;
- сценарий, ожидаемые evidence kinds/sources;
- точная команда/вызов через каталог ниже;
- требуемые exit/result/log;
- `path = artifacts/pdca/D233/rv1/rows/<row_id>.json`;
- ожидаемые raw logs/artifacts в соседнем каталоге `<row_id>/`;
- поток-владелец, applicability predicate, приоритет.

`path` — **входной evidence-пакет**, не фиктивный положительный verdict. Владелец сохраняет команду, timestamp, revision/diff identity, exit code, результаты и ссылки на raw evidence. DO формирует manifest на DO→CHECK boundary. CHECK **сначала открывает manifest**, затем читает `path` каждой строки.

Независимые CHECK-certificates создаются при CHECK отдельно; до этого они отмечены pending, а не pass.

### Exact command / invocation catalog

Все shell-команды выполняются из repo root. Capture wrapper использует `pipefail`, сохраняет combined stdout/stderr и настоящий exit code; ненулевой exit не маскируется `tee`.

| Cmd | Точная команда/вызов |
|---|---|
| **C-GATE** | `python3 artifacts/pdca/D233/rv1/audit.py gate` |
| **C-COMPAT** | `python3 artifacts/pdca/D233/rv1/compat_probe.py` |
| **C-MARKER** | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~WindowFunctionMarkerTests"` |
| **C-SQLITE** | `dotnet test tests/nextorm.sqlite.tests -c Debug` |
| **C-POSTGRES** | `dotnet test tests/nextorm.postgres.tests -c Debug` |
| **C-SQLSERVER** | `dotnet test tests/nextorm.sqlserver.tests -c Debug` |
| **C-MYSQL** | `dotnet test tests/nextorm.mysql.tests -c Debug` |
| **C-MARIADB** | `dotnet test tests/nextorm.mariadb.tests -c Debug` |
| **C-CLICKHOUSE** | `dotnet test tests/nextorm.clickhouse.tests -c Debug` |
| **C-INTEGRATION** | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` |
| **C-BUILD** | `dotnet build nextorm.slnx -c Debug` |
| **C-COVERAGE** | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect --settings coverage.settings.xml --output-format cobertura --output artifacts/pdca/D233/rv1/coverage/coverage.cobertura.xml "dotnet test nextorm.slnx -c Debug"` |
| **C-REPORT** | `reportgenerator "-reports:artifacts/pdca/D233/rv1/coverage/coverage.cobertura.xml" "-targetdir:artifacts/pdca/D233/rv1/coverage/report" "-reporttypes:JsonSummary;TextSummary"` |
| **C-COVERAGE-GATE** | `python3 artifacts/pdca/D233/rv1/audit.py coverage` |
| **C-DOCS** | `python3 artifacts/pdca/D233/rv1/audit.py docs` затем `dotnet docfx docs/docfx.json` |
| **C-BOUNDARY** | `python3 artifacts/pdca/D233/rv1/audit.py boundary` |
| **C-MANIFEST** | `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233/rv1/manifest.json` |
| **I-REGISTRY** | `Task(check): "Открой manifest rv=1 первым. Независимо сертифицируй EC233-REGISTRY: точную Over-сигнатуру, EN/RU ссылки, W1–W4/NW2, compatibility evidence и author≠certifier. Сохрани certificate по контракту; отсутствие доказательства не pass."` |
| **I-CAPABILITY** | `Task(check): "Открой manifest rv=1 первым. Проверь EC233-CAPABILITY: неизменность capability flags и worker, матрицу шести dialects, nullable/frame guards. Используй Roslyn для C# symbols. Сохрани independent certificate; неподтверждённый guard не pass."` |

`audit.py` и `compat_probe.py` — планируемые evidence helpers, не production tooling:

- `gate`: проверяет сохранённое отдельное approval и handoff T232, порядок относительно первого implementation edit.
- `compat_probe`: строит disposable baseline/candidate corpus; сохраняет diagnostics и binding outcomes; exit 0 только без baseline-success→candidate-failure и без смены старого binding.
- `coverage`: читает реальные summary/XML; exit 0 только при обоих порогах.
- `docs`: проверяет обе страницы, точную optionality, ссылки и отсутствие public→spec links.
- `boundary`: проверяет explicit footprint, CRLF, неизменность старых API/worker/capability surface; семантические выводы подтверждаются Roslyn/CHECK.

Если установленный toolchain не поддерживает указанную coverage-команду, это **предусловие**, а не разрешение убрать coverage: вернуть точный diagnostics в PLAN для корректного equivalent invocation по CI workflow.

### Frozen `required_rows`

```text
EC233-GATE
EC233-COMPAT
EC233-MARKER
EC233-SQL-SQLITE
EC233-SQL-POSTGRES
EC233-SQL-SQLSERVER
EC233-SQL-MYSQL
EC233-SQL-MARIADB
EC233-SQL-CLICKHOUSE
EC233-INTEGRATION
EC233-BUILD
EC233-COVERAGE
EC233-DOCS
EC233-REGISTRY
EC233-CAPABILITY
EC233-BOUNDARY
EC233-MANIFEST
```

| Row ID → requirements | Сценарий; evidence kinds / planned sources | Cmd; обязательный результат | Артефакты; владелец; applicability; priority |
|---|---|---|---|
| **EC233-GATE → R233-GATE** | Approval, T232 handoff, edit chronology; authorization/status records | C-GATE; exit 0, оба разрешения подтверждены | Gate report, approval/handoff extracts; orchestrator + evidence coder; **всегда до implementation**; P1 |
| **EC233-COMPAT → R233-2** | Все старые формы, null/default; compiler diagnostics и semantic binding | C-COMPAT; exit 0, ноль regressions | Corpus sources, baseline/candidate logs, binding comparison; compiler-test stream; **всегда**; P1 |
| **EC233-MARKER → R233-2,3** | Scalar marker, value/reference/nullable/default, неисполнение expressions | C-MARKER; exit 0, новые сценарии исполнены | Test log + scenario→actual-test mapping; core-test stream; **всегда**; P1 |
| **EC233-SQL-SQLITE → R233-1,2,3** | Exact parity, nullable, asc/desc, frames; SQLite suite | C-SQLITE; exit 0, mandatory cases passed | Log, actual test map, paired SQL evidence; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-POSTGRES → R233-1,2,3** | То же, PostgreSQL | C-POSTGRES; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-SQLSERVER → R233-1,2,3** | То же, SQL Server | C-SQLSERVER; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-MYSQL → R233-1,2,3** | То же, MySQL | C-MYSQL; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-MARIADB → R233-1,2,3** | То же, MariaDB | C-MARIADB; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-CLICKHOUSE → R233-1,2,3** | То же, ClickHouse, сохранение in-frame boundary | C-CLICKHOUSE; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-INTEGRATION → R233-1,3,4** | Result/NULL/Rows parity; пять существующих DB providers | C-INTEGRATION; exit 0, обязательные window cases реально executed, не skipped | Log, provider×scenario execution table, results; integration stream; **всегда**; P1 |
| **EC233-BUILD → R233-2,4** | Solution и старые call sites | C-BUILD; exit 0, 0 warnings/errors | Полный build log; build stream; **всегда**; P1 |
| **EC233-COVERAGE → R233-4** | Project-configured line и branch coverage | C-COVERAGE, C-REPORT, C-COVERAGE-GATE; все exit 0; ≥85/≥75 | Cobertura XML, Json/Text summaries, threshold report; coverage stream; **всегда**; P1 |
| **EC233-DOCS → R233-5** | EN/RU examples, frame, links, DocFX | C-DOCS; оба exit 0, нет новых broken references | Audit/DocFX logs, docs diff; docs-test stream; **всегда**; P1 |
| **EC233-REGISTRY → R233-5** | API entry, stale references, независимость | I-REGISTRY; explicit certified result | DO input diff/report; `certifications/EC233-REGISTRY.json`; **CHECK, не автор записи**; **всегда**; P1 |
| **EC233-CAPABILITY → R233-1,2,3** | Flag/worker invariants, nullable и unsupported-frame guards | I-CAPABILITY; explicit verified result по всей матрице | Capability table с actual evidence locations; `certifications/EC233-CAPABILITY.json`; CHECK semantic-review stream; **всегда**; P1 |
| **EC233-BOUNDARY → R233-2,5,R233-SCOPE** | API-only scope, CRLF, no worker/cache changes; perf applicability | C-BOUNDARY; exit 0; CHECK подтверждает semantic review | Boundary/diff report, perf decision; boundary stream + CHECK; **всегда**; P1 |
| **EC233-MANIFEST → R233-EVIDENCE** | Все required rows/paths, rv, артефакты и источники | C-MANIFEST; exit 0; затем CHECK читает каждый path | Manifest validation log и completeness review; evidence coder + CHECK; **всегда**; P1 |

**Приоритеты по построению:** все строки выше P1: исходные инварианты, public-API compatibility либо проектная таблица класса «новая SQL-функция/оператор». Последняя применяется консервативно: parity, capability boundary, каждый provider, nullable semantics обязательны. CHECK не понижает эти строки.

### CHECK re-gather budget

- Владелец: **check**.
- Бюджет: **2 адресных re-gather запроса за CHECK**, каждый для одной семьи evidence rows.
- Сначала читать существующие `path` и raw artifacts; затем запросить отсутствующее доказательство у соответствующего evidence owner.
- Повторный сбор без source edits не является новой DO-итерацией.
- Исчерпанный бюджет → явно unresolved row. При стойко низкой уверенности — рекомендация orchestrator вызвать `escalate`, **триггер 5**.
- Отсутствие отчёта само по себе не оправдывает rv/r revision или исправительную DO.

## 8. Performance-measurement decision

**Не нужен benchmark в выбранном scope.**

Основание: marker API находится в `Query/WindowFunctions.cs:16,28–57`; новый метод только представляет expression-call. Mapping/rendering остаются `WindowSql.cs:139–171` и `WindowFunctionTranslator.cs:107–179`. Не добавляется per-row работа, allocation, planner/cache/executor/materializer путь; SQL одинаков.

Проверка применимости обязательна в EC233-BOUNDARY. Если фактическое решение потребует изменения query-path/plan-cache/executor/materialization — DO→PLAN; тогда обязательны baseline и проектные **ровно 7 acceptance benchmark cases**, без молчаливого исключения.

## 9. Reconnaissance decision

**Нужен ограниченный compiler experiment**, но не prototype SQL renderer.

Что доказывает: сохранение overload resolution для старых `null`/`default` и optional форм.

Наблюдаемый критерий: каждый baseline-success пример остаётся candidate-success с прежним binding; нет нового CS0121.

Выполняется только в последующем разрешённом запуске, **перед публичной API-правкой**. Провал инвалидирует выбранный дизайн и требует нового `P:`, а не изменения старых call sites или criteria.

Отдельный renderer spike не нужен: имеющийся mapping по именам и существующие scalar-order paths дают достаточную основу для выбранного минимального решения.

## 10. Unit execution mode

**Последовательно, один writer, одно дерево.**

Причины: G1 принудительно одна lane; общий публичный файл и registry, плюс зависимость от T232. Не создавать worktrees/branches.

Write-footprint: публичный API, core marker tests, шесть dialect SQL-generation files, общий integration window file, два guide files, registry, status; generated evidence subtree отдельно.

## 11. Design checklist

- **SOLID:** marker остаётся marker; rendering/validation не переносится в API.
- **DRY:** используются существующие mapping и frame renderer.
- **Type design:** существующие `Expression`, `WindowOrder`, `WindowFrame`; новые types/constraints отсутствуют.
- **API:** первые параметры не optional; старые сигнатуры не редактируются; compiler compatibility доказана отдельно.
- **Perf:** нет массивов/closures/compile/Invoke в новом marker body.
- **Cache:** никакой mutation `QueryCommand.Cache` или shared-command state.
- **Toolchain:** Roslyn-first для symbols/binding; CPM без новых packages; CRLF; warnings-as-errors.
- **Review:** docs/registry author не сертифицирует собственную запись.

## 12. Risks, assumptions, prerequisites and sibling results

### Чего не сказала постановка

| Пробел | Закрытие |
|---|---|
| Compatibility для untyped `default` | **Не допущение:** обязательный compiler probe; failure блокирует выбранный дизайн. |
| Семантика default/null `WindowOrder` | Сравнить с существующим order-only поведением; новую семантику не изобретать. |
| Конкретные nullable fixtures | Использовать существующие window fixtures; required data setup ограничить window-test files. Если нужны shared fixture edits — additive prerequisite/replan, без потери исходного D. |
| MariaDB DB fixture | Не предполагается существующим; обязательный dialect test остаётся. При наличии fixture после T232 — включить DB evidence. |
| Состояние T232 | Требуется подтверждённый handoff; до него T233 blocked. |
| Новое implementation approval | Текущий issue comment его не даёт; требуется отдельная сохранённая авторизация. |
| Полная tool availability для coverage | Проверяется prerequisite; tool failure не отменяет thresholds. |

### Требования к T232

T233 выполняется **после T232**. Ожидается:

1. T232 завершён и сертифицирован в G1 / `1.0.9-rc3`.
2. Передан окончательный diff и описание public-API/worker/capability изменений.
3. `Query/WindowFunctions.cs`, `Builders/**`, `Dialect/**` больше не изменяются параллельно.
4. Сохранён baseline после T232 для compatibility и build.
5. Если T232 уже добавил пересекающуюся сигнатуру или изменил mapper/capability boundary — T233 возвращается в PLAN; дублирование запрещено.

### Блокеры и классификация

- Approval и T232 — **предусловия**, не завершение D и не STOP от DO.
- Недостающий report — **недостаток доказательств**, сначала bounded targeted gathering.
- Container/toolchain recovery — in-cycle prerequisite по проектным инструкциям.
- Доказанная несовместимость выбранной сигнатуры — реальное основание для replan.
- Истинное препятствие вне разрешений/ресурсов → рекомендация orchestrator вызвать `escalate`.
- Никакая обнаруженная incompatibility не принимается как «допустимый риск» ценой ослабления R233-2.

## 13. Ordered task list

### Сейчас — только PLAN

**P:233.1 — Persist and freeze.**  
`coder` записывает этот план в `docs/specs/status/D233-window-over-overload-1.md`, фиксирует `r=1`, `rv=1`, N=1, pending approval/T232 и frozen contract. **Затем остановка.** Ни implementation edits, ни тестовые запуски.

### После отдельного разрешения и нового запуска DO

1. **D:233.1 — Gate и compatibility prerequisite.** Touch: status, generated evidence subtree, disposable `/tmp/nextorm-D233-compat/`. **Fix now:** approval/T232 evidence, baseline build, differential probe. При несовместимости — DO→PLAN до API-правки.
2. **D:233.2 — PUBLIC API ADDITION, approval-required.** Touch: `src/nextorm.core/Query/WindowFunctions.cs:16–57`. **Fix now:** один выбранный overload и XML docs. Старые overloads неизменны; worker untouched.
3. **D:233.3 — Marker tests.** Touch: `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs:28–51`. **Fix now:** scalar/default/nullability/nonexecution cases.
4. **D:233.4 — Six-dialect parity.** Touch: шесть `SqlGenerationTests.cs` из footprint. Anchors: PostgreSQL `2551,2658`; SQLite `2316,2349,2365,2402,2441,2469`; SQL Server `1705,1738,1754,1812,2361`; MySQL `271,469,486,503`; MariaDB `248,360,377,394`; ClickHouse `676,703,720,737,1108`. **Fix now:** матрица parity/frame/nullable и capability boundary.
5. **D:233.5 — Integration parity.** Touch: `tests/nextorm.integration.tests/CommonTestSuite.Window.cs:14–131`. **Fix now:** deterministic scalar/array results, NULL partition, Rows.
6. **D:233.6 — EN/RU documentation.** Touch: два `11-window-functions.md`. **Fix now:** paired examples, frame, корректная optionality.
7. **D:233.7 — Registry authoring.** Touch: `docs/specs/design/API-NAMING-REVIEW.md:2055,2076–2081`, W1–W4/NW2. **Fix now:** API entry и stale window-guide references. Не сертифицировать собственную правку.
8. **D:233.8 — Final evidence boundary.** Touch: status и generated evidence subtree. **Fix now:** CRLF, final build/tests/coverage/docs, все row packages и manifest; вызвать CHECK для независимой сертификации. Последний source edit требует релевантного повторного evidence.

**Deferred:** только явно перечисленные non-goals из matrix. Ни один обязательный критерий T233 не deferred.

При additive prerequisite исходный D сохраняется **active/blocked** с неизменными criteria и остатком. Только фактическая замена scope допускает `superseded→replacement`. Любой исправительный возврат получает новую `P:`; `r` увеличивается лишь при реальной смене действий/зависимостей, не при переформулировке.

## 14. Planned write-footprint

**13 tracked paths:**

1. **PUBLIC API:** `src/nextorm.core/Query/WindowFunctions.cs`
2. `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs`
3. `tests/nextorm.sqlite.tests/SqlGenerationTests.cs`
4. `tests/nextorm.postgres.tests/SqlGenerationTests.cs`
5. `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`
6. `tests/nextorm.mysql.tests/SqlGenerationTests.cs`
7. `tests/nextorm.mariadb.tests/SqlGenerationTests.cs`
8. `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`
9. `tests/nextorm.integration.tests/CommonTestSuite.Window.cs`
10. `docs/guide/11-window-functions.md`
11. `docs/ru/guide/11-window-functions.md`
12. `docs/specs/design/API-NAMING-REVIEW.md`
13. `docs/specs/status/D233-window-over-overload-1.md`

Generated/uncommitted evidence:

- `artifacts/pdca/D233/rv1/**`
- `/tmp/nextorm-D233-compat/**`
- test-runner outputs under approved `TestResults/**`.

`Builders/**`, `Dialect/**`, translator, examples, workflow/config files **не входят** в разрешённый footprint T233.

В последующем разрешённом collection execution auto-commit допустим только после CHECK, с **явно перечисленными paths**, префиксом `#233`. Никогда `git add -A`, push, branch или merge.

## 15. Evidence-contract & gate self-check

- **Goal/scope:** определены сущность, ограничения и non-goals.
- **Acceptance:** R233-1…5 имеют наблюдаемые проверки и негативные случаи.
- **Minimal design:** точная сигнатура, optionality, XML docs и alternatives зафиксированы.
- **Public-API approval:** жёсткий запрет DO до отдельного approval сохранён.
- **Compatibility:** default/null gap не скрыт; обязательный blocking probe предусмотрен.
- **Decomposition:** ordered D, dependencies, completion и replan rules определены.
- **Variants:** input/null/default/value/reference/provider/frame/flags закрыты test/guard/deferred.
- **Test scope:** обязательный structured JSON присутствует.
- **Priorities:** обязательные строки P1; CHECK не понижает.
- **Docs:** EN/RU и registry обязательны; stale references fix now.
- **Perf:** неприменимость аргументирована `file:line`; scope-change trigger указан.
- **Recon:** compiler experiment имеет наблюдаемый критерий.
- **Execution:** single writer/tree, T232→T233, без worktree/branches.
- **Evidence:** rv=1, stable requirement/row IDs, commands, results, artifacts, owners и predicates закреплены.
- **Manifest:** frozen path, required_rows, DO→CHECK production и manifest-first CHECK определены.
- **Re-gather:** конечный бюджет 2, владелец CHECK; missing evidence не создаёт фиктивный replan.
- **Certification:** registry author≠certifier.
- **Project rules:** CRLF, Roslyn-first, thresholds и реальные контейнеры сохранены.
- **Current boundary:** записать план и остановиться; DO сейчас запрещён.

Verdict: **r=1 ready; DO blocked**, evidence contract rv=1 frozen.
Chosen signature: `Over(Expression<Func<object?>>? partitionBy, WindowOrder orderBy, WindowFrame? frame = null)`.
Footprint size: **13 tracked paths**, один public-API file; generated evidence отдельно.
Open assumptions: T232 handoff, новое approval, compiler compatibility null/default и доступность существующих fixtures.
Next allowed step: **coder сохраняет этот план в status file и останавливается; DO не запускается**.
---
