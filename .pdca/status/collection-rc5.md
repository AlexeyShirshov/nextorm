# Collection rc5 — milestone 1.0.9-rc5 (#23), all open tasks

- collection-id: `rc5`
- entry: fresh entry (autonomous, authorized auto-commit)
- repo/branch: `AlexeyShirshov/nextorm` / `1.0.9-rc5`
- base: `1e31331d`
- status path: `.pdca/status/collection-rc5.md`
- entry-plan ref: `.pdca/rc5/entry-plan.md` (verbatim copy of the entry planner's returned report, sha256 `ffaecc0b745db18d6af833398ea1b9a7f038a31ad36e57838a9ebc353f3f3f0f`)
- container revision: r = 2 (entry scope 22) · entry-plan ref r=2: .pdca/rc5/entry-plan-r2.md (verbatim planner report, sha256 0283edbe19341e172e61660c3a041eb7e52075a0d17175f6d1835b534574ace4, 30744 bytes, 146 lines)
- created (UTC): 2026-10-30T05:51:46Z
- form: `collection`
- overlay: `coder+.NET`
- plan_cap: 3
- lane_cap: 1
- cap_reason: `plan_cap = 3` is the `pdca-collection` §P step 2 parallel-preparation ceiling actually used (started at 4; lowered to 3 after one planner call failed in transport, `T246`, and was retried on the same PLAN); `lane_cap = 1` is the post-barrier clustering decision (`G1`, one group) — 16 of the 18 executable items form one transitive write-surface component and all container/benchmark runs are globally exclusive, so a second lane could verify nothing.

## Original task

«pdca по открытым задачам из майлстона rc5 автономно с автокомитом»

User constraints (three, quoted verbatim from the task line above):

- `по открытым задачам из майлстона rc5`
- `автономно`
- `с автокомитом`

Auto-commit policy in force for this collection (verbatim, repo `AGENTS.md` §Git): "a `pdca-collection` cycle started with explicit auto-commit permission may (a) commit on `collection/**` branches inside the cycle worktree and (b) `git merge --no-ff` a group branch into the current branch." Push stays with the user: no `git push`, no merge into `main`, no tag or release commit.

## Task queue (source of truth)

| id | issue | execution_kind | group | selected_variant | plan_state | preparation_state | preparation_ref | status | disposition | branch | cycle_id | status_path | plan_revision | baseline | notes | terminal | attempts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| T353 | #353 | cycle | G1 | coder+.NET | ready | — | — | done | finalized | 1.0.9-rc5 | N=1 | .pdca/status/T353-rc5-1.md | r=1 | 1e31331d | wave 1 correctness; plan ready: cycle + coder+.NET, AC-T353-01..03, plan_ref .pdca/status/T353-rc5-1.md §PLAN record, evidence_root .pdca/T353/rv1/; evidence_root superseded -> .pdca/rc5/items/T353/rv1/ (id collision with rc4 evidence); done: AC-T353-01..03 met (CHECK accepted), commit 5d108505, evidence .pdca/rc5/items/T353/rv1/ | execution | 0 |
| T352 | #352 | cycle | G1 | coder+.NET | ready | — | — | done | finalized | 1.0.9-rc5 | N=1 | .pdca/status/T352-rc5-1.md | r=1 | 1e31331d | wave 1 correctness; plan ready: cycle + coder+.NET, AC-T352-01..03, plan_ref .pdca/status/T352-rc5-1.md §PLAN record, evidence_root .pdca/T352/rv1/; evidence_root superseded -> .pdca/rc5/items/T352/rv1/ (id collision with rc4 evidence); done: AC-T352-01..03 met (CHECK accepted), commit 808dd494, evidence .pdca/rc5/items/T352/rv1/ | execution | 0 |
| T318 | #318 | cycle | G1 | coder+.NET | — | — | — | incomplete | active | 1.0.9-rc5 | N=1 | .pdca/status/T318-rc5-1.md | r=1 | 1e31331d | wave 2 core perf; plan ready: cycle + coder+.NET, AC-T318-01..03, plan_ref .pdca/status/T318-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T318/rv1/; terminal: infeasible — empty-collection copying already absent (CloneForCache Count>0, null arrays, 872 B = bare command shell); AC-T318-01 unattainable in scope; residual ~69 KB raw-recipe/plan work owned by T317/T245; issue #318 left OPEN (no whole-goal acceptance, no CHECK); evidence .pdca/rc5/items/T318/rv1/; column value per lint schema; actual DO attempts = 1 (see item record) | infeasible | 0 |
| T317 | #317 | cycle | G1 | coder+.NET | — | — | — | incomplete | active | 1.0.9-rc5 | N=1 | .pdca/status/T317-rc5-1.md | r=1 | 1e31331d | wave 2 core perf; plan ready: cycle + coder+.NET, AC-317-01..04, plan_ref .pdca/status/T317-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T317/rv1/; DO attempt 1 blocked then reverted — integration RED under the candidate (16 failed, exit 2) vs 0 failed at the same base, allocation regression on 4 `In*` arms (defects T317-D2 hypothesis / T317-D3); tree = base, base green after revert (build 0W/0E, integration 7933/7732/0/201); patch kept at .pdca/rc5/items/T317/rv1/attempts/attempt1-full.diff for attempt 2; attempt 2 fully gated (build 0W/0E, core 3892/3892, dialects 7/7, integration 7933/7732/0/201, acceptance 7/7 rc 0) but violated AC-317-02 on 15 of 17 nextorm arms (+160…+1,761 B/op, 11 deterministic) and showed no AC-317-01 reduction above the host noise floor, so the pre-registered rule rejected and reverted it; terminal: infeasible (expert-reviewed) — per-call QueryCommand makes a per-instance plan-key/shape memo unusable (write-before-read); AC-317-02 unattainable as scoped; D2 fixed, D3 unresolved; residual → T319/T245; issue #317 left OPEN; evidence .pdca/rc5/items/T317/rv1/; no acceptance, no CHECK; column value per lint schema; actual DO attempts = 2 (see item record) | infeasible | 0 |
| T245 | #245 | campaign | G1 | coder+.NET (step-level) | — | ready | .pdca/rc5/items/T245/preparation.md@r1 | done | active | 1.0.9-rc5 | N=3 | .pdca/status/T245-warm-set-ops-3.md | r=3 | 1e31331d | re-opened rc5: new revision r+1 in the existing rc4 record (AC-COL-04); prepared: campaign (incremental), AC-245-01..07, preparation_ref .pdca/rc5/items/T245/preparation.md@r1, evidence_root .pdca/T245/rv3/; identity per item PLAN (N=3 r=1) supersedes the queue's N=1 r=2 because -1.md and -2.md are closed terminal records; step 1 baseline measured (CHECK not accepted -> DO evidence completion, no attempt consumed); evidence .pdca/rc5/items/T245/rv3/baseline/; step-1 evidence completion returned (provenance/reconciliation/exact extraction) — re-CHECK pending; step-1 re-CHECK not accepted -> DO evidence completion (5 items); escalation in flight (same-defect recurrence + PLAN re-anchor); PLAN r=2 (X1 re-anchor = cb2246e9; preparation-r2.md@r2; JIT-regime comparability condition); bounded step-1 completion authorized; step-1 bounded round 2 returned (both-operation attribution, Roslyn observations, errata, final identity) — re-CHECK pending; step 1 accepted under PLAN r=2 (X1 = cb2246e9): per-operation baseline 7 112.1/7 096.1 vs linq2db 5 176.2/5 128.2, attribution construction 4 104 / preparation 2 248 / execution 760-744, residual −0.02; campaign open, Step 2 at the planner boundary; **step 2 (n=1/3) DO — no candidate**: the contract's object-breakdown probe measured the two candidate buckets in one process (exit 0, pinned DB unchanged) and closed them exactly — construction `4 584` = caller operands `3 712` (out-of-surface) + one `#240`-mandatory live clone `872`, with `ResetPreparation`/`SetOperation` at `0 B`; the prepare rung `2 248` is the D201-3 raw-reuse path (`reuses=1`) = `RawShape` `1 872` + `RawGraph` `48` (out-of-surface general prepare machinery; the ordinary pipeline it skips costs `2 424`) + command-bound plan key `328–408` (`T317`-protected), set-op-specific hash/apply/lookup `0 B` ⇒ **0 B removable inside §4**, pre-registered rule clause 1 unsatisfiable (max delta `0.00 B/query`) and clause 10 triggered ⇒ closure path **C, terminal branch** for PLAN/ACT; zero product change (`src`/`tests`/`examples` byte-identical to `X1 cb2246e9`), build 0W/0E, no CHECK run, campaign not accepted, issue #245 OPEN; evidence `.pdca/rc5/items/T245/rv3/step2/`; step 2 (r=2,n=1) rejected at CHECK -> PLAN (contract drift S2-F1..F4, negative-branch unproven S2-F2/F5); r=3 issued, n=1/3, no product change; step 2 (r=3, n=1/3) DO completed in the **same** attempt the controller cut off (controller loss consumed nothing): retained **M1** (immutable descriptor reuse on the set-op preparation path, `QueryCommand.QueryPreparer.cs` +11/−8) measured **ΔB 464–544 B/query** on both `Except` and `Intersect` under a held-JIT-regime ABBA pair (`7 592.3/7 576.3` → `7 048.3/7 032.3`), mean time inside the ±20 % band ⇒ **Tier B** local win on `R1*`, Tier A `1.3616×/1.3712×` with residual `836.7/878.0 B/query` to the `1.20×` bound; `Warm_Reused 0.00 B`, `Prepare()` controls `760.00/744.10` unregressed; `M2` measured `ΔB = 0`, not credited, no code left; new native gate `SqliteWarmSetOperationAllocationTests` with ceilings locked `7 530`/`1 090`/`0` (supersede of the `9000`/`1400` placeholders); gates: build 0W/0E, `T240` 11/11, cache-key 28/28 + 13/13, core 3 886, dialects all green, fail-closed integration 7 934/7 733/0/201; Lane G1 two-tier amendment recorded at `r=3` (no `r=4`, criteria unchanged); **no CHECK, no verdict, no campaign acceptance**, issue #245 OPEN; evidence `.pdca/rc5/items/T245/rv3/step2-r3/`; step-2 (r=3) DO committed as `bc641498` (single commit after `e1404aca`, subject `#245 T245 step-2 (r3): retained M1 -464..-544 B/query, Tier B; gates + locked allocation ceilings; evidence .pdca/rc5/items/T245/rv3/`), exactly four paths by explicit `git add` — `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `tests/nextorm.integration.tests/Sqlite/SqliteWarmSetOperationAllocationTests.cs`, `.pdca/status/T245-warm-set-ops-3.md`, `.pdca/status/collection-rc5.md`; `.pdca/rc5/items/T245/rv3/**` evidence local and gitignored, not a CI input; step-2 (r=3, n=1) CHECK returned **not accepted → DO** (CHECK consumed no attempt; `N=3 r=3` stand): retain `R1*`/`M1` and its locked allocation gates, never revert the measured win, complete the missing evidence without consuming an attempt, then continue the ladder at **`n=2/3`, same `r=3`**; findings `T245-S2-F6` (evidence linkage / committed gate snapshot / readable `Prepare()`+`Warm_Reused` extraction / `r2-held-C1` identification) and `T245-S2-F7` (arithmetic+units); evidence completion + append-only errata written at the same `n=1/3` — `commit-linkage.log` binds the measured source to `bc641498` (CRLF-attributed sha256 + clean product tree), committed gate-test snapshot sha256 `b33f7eb3…`, `bdn-controls-and-reused.csv` `760.00/744.10 B/query` + `Warm_Reused 0.00` every run, extractor fixed to per-query (`bdn-extract.v1.py:66–70` → `bdn-extract.py:121–127`), corrected `R0-A` ratios `1.46666×/1.47726×`, C1 INTERSECT time `1.0243×`, residuals `836.68/877.98 B/query`; **zero product change**, no ladder exhaustion, campaign open, issue #245 OPEN; evidence `.pdca/rc5/items/T245/rv3/step2-r3/` (index `evidence-completion/INDEX.md`); **step-2 (r=3, n=2/3) lever rung `R2` retained** (async-wrapper removal on `DataContext.ToListAsync`, `src/nextorm.core/DataContext/DataContext.cs:1882`, `+4/−2`): measured decomposition of the execution bucket first (`760/744 B/query` = reader-open `384` + provider async-reader delta `144` + result list `88/72` + executor async terminal `72` + `DataContext` wrapper `72`, closure `0.00`), then paired ABBA `R1*-A → R2-B1 → R2-B2 → R1*-A2` with **`ΔB = −72.00 B/query` in all four pairings on both operators** (cached `7 048.6/7 032.3 → 6 976.6/6 960.3`), `Prepare()` controls `760.00/744.10 → 688.00/672.10` (not regressed), `Warm_Reused` exact `0 B`, time ratios `0.986–1.046` inside the `±20 %` band; Tier A still unmet at `1.3477×/1.3572×` with residual `764.68/805.98 B/query`; gates: build 0W/0E, `T240` 11/11, cache-key 19/19 + 9/9 + 13/13, core 3 886, sqlite/postgres/sqlserver/mysql/mariadb/clickhouse all green, native allocation gate `7 530/1 090/0` green inside both full-suite runs; **fail-closed integration suite NOT green** — two runs `rc 2`, `7 934/7 732/1/201` each, one **ClickHouse `HttpClient.Timeout` (120.0 s)** per run on a **different** test (`T286bF18DictionaryTests` fixture setup, then `ClickHouseScalarFunctionsTests`), both **green in isolation** on the same tree (`16/16` and `1/1`, rc 0) ⇒ classified **environment/infrastructure**, defect key `T245-S2-G1`, no product defect, no revert (`gates2/classification.md`); **`n=2/3` spent**, `n=3/3` open, `r=3` unchanged, **no `r=4`**, no verdict/no `PASS`/no campaign acceptance, issue #245 OPEN; evidence `.pdca/rc5/items/T245/rv3/step2-r3/{exec-decomposition/,gates2/}`; **step-2 (r=3) CHECK → DO block A executed at the same `n=2/3`** (no attempt consumed, zero product change): (1) current-candidate linkage — the `n=2/3` measured/tested candidate is bound to commit **`c3294de9`** by raw `git show`/full diffs, equal blob ids and CRLF-restored sha256 for `DataContext.cs`/`Program.cs`/`T245ExecDecompositionProbe.cs`, empty `git status`/`git diff` over `src tests examples benchmarks`, per-run core DLL sha256 (`R1*` `9e3c373e…`, `R2` `78aabc76…`) and the current Release artefacts (`core 78aabc76…` 16:04:28Z, benchmark dll `a41a13a0…`, apphost `093a5710…` 16:04:30Z), with the run-time benchmark-executable hash stated as not captured; (2) append-only errata — `errata-step2-r3.md` §E9 withdraws "fully attributed": the held `R1` preparation rung `1 704 B/query` closes `1 328 + 48 + 80 = 1 456` and leaves **≈296 B/query unknown** (not exhaustion); §E10 keeps the executor remainder `E10 − E11 = 72 B/query` **open** (distinct from the removed `DataContext` wrapper bucket); (3) launch/regime provenance — invocation records on disk incl. the complete `R1-C1` held command, probe run headers `TieredCompilation="0"`/`LaunchCommand`, BDN `HostEnvironmentInfo` of all four ABBA runs; **no** BDN artefact records the tiering variable (0 hits) and the launch line is not in `~/.bash_history`, so the regime is proven by invocation text + `R1-C1` level agreement and that limitation is recorded, not waived; `R1*`/`R2` and the ceilings `7 530 / 1 090 / 0` untouched; block B (one canonical green full container-backed integration run) is next, `n=3/3` open, no `r=4`, no verdict/no `PASS`, issue #245 OPEN; evidence `.pdca/rc5/items/T245/rv3/step2-r3/evidence-completion/{candidate-linkage-c3294de9.*,regime-provenance.*}`; **block B executed — the canonical full container-backed integration run is GREEN** on the frozen candidate: socket present, machine running, no stale ClickHouse container/volume (nothing deleted), no provider opt-out, `rc 0`, **7 934 total / 7 733 succeeded / 0 failed / 201 skipped**, 10 m 58.649 s, no ClickHouse `HttpClient.Timeout` (both previously-failing tests inside the run); native gate green in-run and in a targeted report re-run (`7416/7417.3`, `1088`, `0` under `7530/1090/0`); `T245-S2-G1` **closed green** (earlier two stalls kept as environment/infrastructure history, superseded as gate evidence, not waived); `n=3/3` unspent and open, `r=3`, no `r=4`, no verdict/no `PASS`, issue #245 OPEN; evidence `.pdca/rc5/items/T245/rv3/step2-r3/gates3/`; **block C (2026-10-30T17:30Z)**: `n=3/3` investigation probe `+25` lines (`E12`/`E13` controls) measured `E10=688/672`, `E11=616/600`, `E12=72`, `E13=72` B/query — remainder is irreducible `Task<List<TResult>>` cost; no in-scope lever; `n=3/3` unspent; gate green (block B) holds; resume point step-2 boundary `DO → CHECK`; evidence `.pdca/rc5/items/T245/rv3/step2-r3/executor-remainder/` ; ACT finalized (2026-10-30): step 2 not accepted — campaign stopped (N=3 r=3 n=3/3), M1 (bc641498) + R2 (c3294de9) retained and uncredited, residual 764.68/805.98 B/query to 1.20×, issue #245 stays OPEN; user decision «п1» — limited (Tier B accepted with residual 764.68/805.98 B/query; terminal cell forced to execution because lint TERMINAL enum has no limited value, limited kept here in notes); accepted scope: M1 (bc641498) + R2 (c3294de9), locked gate 7 530/1 090/0, integration gate 7 934/7 733/0/201; #319 owns the operand-construction remainder (~1 936/1 968 B/query) | execution | 0 |
| T246 | #246 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=2 | .pdca/status/T246-raw-datareader-2.md | r=1 | 1e31331d | re-opened rc5: new revision r+1 in the existing rc4 record (AC-COL-04); plan ready: cycle + coder+.NET, C1..C4, plan_ref .pdca/status/T246-raw-datareader-2.md §PLAN record, evidence_root .pdca/T246/rv6/; identity per item PLAN (N=2 r=1) supersedes the queue's N=1 r=2 (the -1.md record is closed at r=5) | execution | 0 |
| T247 | #247 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T247-json-streaming-1.md | r=2 | 1e31331d | re-opened rc5: new revision r+1 in the existing rc4 record (AC-COL-04); plan ready (r=2): cycle + coder+.NET, AC-247-01..06, plan_ref .pdca/status/T247-json-streaming-1.md §PLAN record r2, evidence_root .pdca/T247/rv3/ | execution | 0 |
| T250 | #250 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T250-csv-terminal-plan-cache-1.md | r=1 | 1e31331d | re-opened rc5: plan ready (resumption, r=1); DO attempt 1 candidate delivered — TerminalPlanCache + CsvPlanCacheKey + adapter + ResolveCsvPlan; build 0W/0E; core/sqlite CSV green; roadmap corrected; evidence .pdca/T250/rv1/; candidate implemented: bounded per-context CSV terminal plan cache keyed on CSV options/storage types/sequential/projection, generation invalidation, ValueTransform bypass; 8 new correctness tests 8/8; build 0W/0E; measurement verdict as measured — candidate allocates ~13.4 KB less per 3-column export, faster on all arms, but the 10 k speedup 9 597/9 812 µs vs 10 628/10 347 µs falls below the frozen lower bound 10 573 µs i.e. a measured miss of the pre-registered threshold, and the per-column reduction ~4.5 KB is below the ≥8 KB/column claim ; plan_revision r=1 → r=2 (rc5-r2): magnitude of AC-250-2 superseded — ≥8 KB/col contradicted by measured 15 000 B/3-col compile object (≈4.9–5.0 KB/col); residual list: (i) AC-250-2 magnitude −4.4…−4.5 KB/col vs literal ≥8 KB/col; (ii) AC-250-1 10 000-row ±3% miss upward 9 596.78/9 812.37 µs vs [10 573,11 227] µs; (iii) no same-round linq2db arm; (iv) container suite not run; (v) forward note for #247 derive bar from cold-vs-warm decomposition | execution | 0 |
| T311 | #311 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1, r=2 | .pdca/status/T311-json-streaming-1.md | r=2 | 1e31331d | re-opened rc5: new revision r+1 in the existing rc4 record (AC-COL-04); plan ready (r=2): cycle + coder+.NET, C311-01..05, plan_ref .pdca/status/T311-json-streaming-1.md §PLAN record r2, evidence_root .pdca/T311/rv2/ | execution | 0 |
| T267 | #267 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T267-rc5-1.md | r=1 | 1e31331d | wave 4 examples PG; plan ready: cycle + coder+.NET, AC1..AC3, plan_ref .pdca/status/T267-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T267/rv1/ | execution | 0 |
| T268 | #268 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T268-rc5-1.md | r=1 | 1e31331d | wave 4 examples PG; plan ready: cycle + coder+.NET, AC1..AC3, plan_ref .pdca/status/T268-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T268/rv1/; write surface overlaps T267/T269 per plan §Estimated Write Surface | execution | 0 |
| T269 | #269 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T269-rc5-1.md | r=1 | 1e31331d | wave 4 examples PG; plan ready: cycle + coder+.NET, AC1..AC3, plan_ref .pdca/status/T269-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T269/rv1/ | execution | 0 |
| T270 | #270 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T270-rc5-1.md | r=1 | 1e31331d | wave 4 examples CH; plan ready: cycle + coder+.NET, AC1..AC3, plan_ref .pdca/status/T270-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T270/rv1/ | execution | 0 |
| T271 | #271 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T271-rc5-1.md | r=1 | 1e31331d | wave 4 examples CH; plan ready: cycle + coder+.NET, AC1..AC3, plan_ref .pdca/status/T271-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T271/rv1/ | execution | 0 |
| T272 | #272 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T272-rc5-1.md | r=1 | 1e31331d | wave 4 examples CH; plan ready: cycle + coder+.NET, AC1..AC3, plan_ref .pdca/status/T272-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T272/rv1/ | execution | 0 |
| T273 | #273 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T273-rc5-1.md | r=1 | 1e31331d | wave 4 examples CH; plan ready: cycle + coder+.NET, AC1..AC3, plan_ref .pdca/status/T273-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T273/rv1/ | execution | 0 |
| T274 | #274 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T274-rc5-1.md | r=1 | 1e31331d | wave 4 examples CH; plan ready: cycle + coder+.NET, AC1..AC3, plan_ref .pdca/status/T274-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T274/rv1/ | execution | 0 |
| T302 | #302 | — | — | — | — | — | — | incomplete | active | — | N=1 | .pdca/status/T302-rc5-1.md | r=1 | 1e31331d | PLAN-terminal (r1 draft recorded, plan_state never ready): original task infeasible under the stated constraints — the issue body gates the product work behind two explicit USER acts (implementation-plan approval + separate implementation authorization) that autonomous mode cannot obtain; no DO, no CHECK, attempts=0, issue #302 stays OPEN in 1.0.9-rc5; next authorized action = normal-mode approval of the recorded plan draft | infeasible | 0 |
| T319 | #319 | campaign | G1 | coder+.NET (step-level) | — | ready | .pdca/rc5/items/T319/preparation.md@r1 | in-progress | paused | 1.0.9-rc5 | N=1 | .pdca/status/T319-rc5-1.md | r=1 | 1e31331d | epic, candidate not scheduled; blocked by #317+#318; prepared: campaign (incremental), AC1..AC4, preparation_ref .pdca/rc5/items/T319/preparation.md@r1, depends_on T317+T318 (hard execution prerequisite), first step = post-prerequisite baseline measurement; Step 1 opened (planner: prerequisites satisfied as sequencing gate; both terminal infra → fallback clause activated); zero product edits; baseline pending; paused for the lane: Step 1 evidence complete, resume point = Step 1 boundary CHECK, counters unchanged, no attempt granted; re-anchoring of the baseline only if the boundary opens a change-producing Step 2 after other items touch the shared query path | execution | 0 |
| T359 | #359 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T359-rc5-1.md | r=1 | 8ab09df4 | scope revision r=2 (user added #359 to milestone); plan ready: cycle + coder+.NET, AC-T359-01..02, plan_ref .pdca/status/T359-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T359/rv1/; blocks T271 (ClickHouse examples Retention, Array(Tuple(...))) | execution | 0 |
| T360 | #360 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T360-rc5-1.md | r=1 | 8ab09df4 | scope revision r=2 (user added #360); plan ready: cycle + coder+.NET, AC-T360-01..02, plan_ref .pdca/status/T360-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T360/rv1/; opt-in, does not block T268, examples/** untouched | execution | 0 |
| T361 | #361 | cycle | G1 | coder+.NET | ready | — | — | pending | active | 1.0.9-rc5 | N=1 | .pdca/status/T361-rc5-1.md | r=1 | 8ab09df4 | scope revision r=2 (user added #361); plan ready: cycle + coder+.NET, AC-T361-01..02, plan_ref .pdca/status/T361-rc5-1.md §PLAN record, evidence_root .pdca/rc5/items/T361/rv1/; product fix already landed in the user's commit c8eed046 — scope is the missing regression tests only, c8eed046 never re-attributed/reverted | execution | 0 |


## Input (verbatim from /tmp/nextorm-rc5-issues.md)

Source: `/tmp/nextorm-rc5-issues.md` — 95 310 bytes, 19 sections (`#353` … `#245`, in the file's own order). Every section below is the complete verbatim body of that file, unabridged, in its original language and formatting (code blocks and benchmark tables kept); no paraphrase, translation, summary or reordering. This section is the authoritative input for every queue item. Issue bodies keep their own `## …` headings verbatim: those are content of this section, not outer container sections. Under container revision `r = 2` the three scope-extension briefs `#359`, `#360`, `#361` were appended verbatim from `.pdca/rc5/input-scope-extension/issue-{359,360,361}.md` (22 sections in total; each of the three carries its `title:`/`state:`/`labels:`/`milestone:`/`number:` header fields in that file's own order).

### #353 — Core: рукописные ManyToOne-метаданные с FK.Count > PrincipalKey.Count дают IndexOutOfRangeException вместо именной диагностики
Найдено release-gate аудитом 1.0.9-rc4 (`docs/specs/design/code-smells-review.md` §«Предрелизный аудит v1.0.9-rc4», находка A). Классификация — **P2**: путь не публичный по умолчанию, но достижим из публичного API через рукописные метаданные.

## Где

`src/nextorm.core/Builders/MetadataEagerLoadBinding.cs:581-590` (`MapComponents`).

```csharp
var parent = new PropertyInfo[principal.Count];
var child  = new PropertyInfo[foreign.Count];

if (relationship.Kind == RelationshipKind.ManyToOne)
{
    for (var i = 0; i < foreign.Count; i++)
        parent[i] = foreign[i].PropertyInfo;   // выход за границы, если foreign.Count > principal.Count
```

## Режим отказа

Для **рукописной** реализации `IRelationshipMetadata` с `Kind = ManyToOne` и `Foreign.Count > PrincipalKey.Count` цикл пишет в `parent`, выделенный по `principal.Count`, и падает с сырым `IndexOutOfRangeException` — **до** проверки арности ниже по стеку (`MetadataEagerLoadBinding.cs:359`), которая для несогласованной пары ключей обещает именованный `NotSupportedException` с описанием связи.

То есть обещание «несогласованная арность → дизайнерский отказ» не выполняется ровно на этом срезе: пользователь получает исключение без контекста (какая связь, какие ключи, какую форму чинить).

## Ожидаемое

Именованный отказ с текстом связи и обеими арностями — тот же, что уже отдаёт проверка арности. Варианты фикса (выбрать в PLAN):

1. выделять `parent`/`child` по фактическим длинам ветви (`ManyToOne`: `parent = new PropertyInfo[foreign.Count]`), чтобы до проверки арности доходило всегда; либо
2. поднять проверку арности выше `MapComponents` (до выделения массивов) и не полагаться на порядок вызовов.

## Репро

Единичный тест в `tests/nextorm.core.tests`: метаданные `ManyToOne` с `PrincipalKey = [Id]`, `ForeignKey = [AId, BId]`, вызов eager-load `LoadWith` по этой связи → сейчас `IndexOutOfRangeException`, ожидается `NotSupportedException` с описанием связи.

### #352 — MariaDB: xxh3 / xxh32 are advertised but absent on 11.4 (error 1305)
## What

`NextORM.MariaDb.MariaDbNativeFunctions` advertises `xxh3` and `xxh32` on the pinned MariaDB **11.4** baseline, but the server has neither function: the emitted SQL fails at execution time with **error 1305**.

Same defect class as #280 (`months_between` / `to_date` / `to_number` advertised but absent on 11.4), which was fixed by a per-name version gate; `xxh3`/`xxh32` were left ungated.

- **Provider:** MariaDB (`nextorm.mariadb`, `src/nextorm.mariadb/MariaDbDialect.cs`, `MariaDbNativeFunctions`)
- **Server:** `mariadb:11.4` (image pinned by the integration suite; `select version()` starts with `11.4`)
- **Capability:** `SqlFunctions.MySql.xxh3(string)` / `SqlFunctions.MySql.xxh32(string)`

## Expected

`MariaDbNativeFunctions.Supports("xxh3")` / `Supports("xxh32")` must be `false` for a server that does not implement them, exactly as the same renderer already excludes `uuid_to_bin`, `json_arrow`, `is_uuid`, `grouping` and the eight names the 11.4 baseline reports as absent (error 1305). A gated name is then refused with `NotSupportedException` before any SQL is emitted.

## Actual

`Supports` returns `true`, the translator emits `xxh3(...)` / `xxh32(...)`, and the server rejects the statement:

```
MySqlConnector.MySqlException: FUNCTION nextorm.xxh3 does not exist   (Number = 1305)
MySqlConnector.MySqlException: FUNCTION nextorm.xxh32 does not exist  (Number = 1305)
```

## Reproduce

```sql
select xxh3('abc');    -- ERROR 1305 (42000): FUNCTION nextorm.xxh3 does not exist
select xxh32('abc');   -- ERROR 1305 (42000): FUNCTION nextorm.xxh32 does not exist
```

Through the ORM (production path):

```csharp
using var ctx = new MariaDbDataContext("<mariadb:11.4 connection string>", new DataContextBuilder());
ctx.From<Probe>().Where(x => x.Id == 1)
   .Select(x => SqlFunctions.MySql.xxh3("abc"))
   .ToList();   // MySqlException 1305
```

## Observed by

`tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs`,
`AdvertisedXxhNames_ShouldBeRefusedByThePinnedServer` (executed against the live 11.4 container; it asserts the 1305 refusal and pins `select version()`).

Found while raising branch coverage of the MariaDB/MySQL native-function surface (cycle `integration-branch-coverage-11`, step 1).

### #319 — Epic: build-once query construction (lazy descriptor chain) — candidate, not scheduled
## Why

COW (T297) was rejected because it reduced allocations by 1–12% but slowed every cached arm by 5–15%. The attribution shows the cost is spread over raw-recipe planning (`CaptureRawShape`/`Fingerprint`/`Matches`/`CopyTo`, 52–71%), capture (26–59%) and plan hashing (up to 77% on one arm) — no single dominant cause.

The structural alternative is not to compress a heavy command per node, but to **stop building one per node**.

## Idea

- Fluent operators produce a compact, immutable descriptor chain instead of a full `QueryCommand` each.
- The single `QueryCommand` is materialized once, at the end of the chain.
- Evaluate a struct/SOA description (one array, no per-node heap object) as the implementation, for locality and allocation reasons.
- Keep public fluent semantics and the ownership/clone contract.

## Hard bars (unchanged from the #297 decision)

- Time **≤1.00×** the same-round baseline on every cached arm (including a non-CTE sentinel), judged with the measurement error.
- Allocations strictly better than baseline where the change applies; zero regression elsewhere.
- Public API unchanged; protected `QueryCommand` surface unchanged (E19 gate).
- Full benchmark matrix + all test suites per cycle.

## Status

**Candidate, not scheduled.** Do not start before the plan-hash memoization and empty-collection fast-path issues land — they may close the remaining gap without a structural refactor. This epic is the fallback, not the default.

## Related

- #297 — COW, closed not viable.
- #244 — warm CTE allocation residual (accepted).
- #245 — set-op allocation parity.

### #318 — Perf: avoid copying empty collections in QueryCommand.Clone()
## Context

`QueryCommand.Clone()` is semantically mandatory (G4: `NestedSetOperations_KeepOperandsUnchanged` proves operands must not be mutated), but the clone shell (**816 B**, T245 evidence) copies joins / sorting / array-join / window / CTE metadata even when those collections are null or empty.

T245 established the clone shell itself is required; it did **not** establish that copying empty collections is required.

## Proposal

- Fast paths for null/empty collections in `Clone()` / `CopyTo`: return the shared immutable empty instance instead of allocating and copying.
- Keep the existing tail-clone fix for chained set operations (`a.Union(b).Except(c)`).
- Scope: general construction path; no benchmark-specific conditions.

## Acceptance

- Allocation reduction on clone-heavy arms (set operations, CTE) with **no time regression** (interleaved ABBA, all cached arms).
- Ownership/aliasing tests: mutating a clone never affects the source; chained set-op semantics unchanged; operand reuse unchanged.
- Full test suites, build 0W/0E; public API unchanged.

## Related

- #240 — construction allocation follow-up (closed with accepted residual).
- #245 — set-op allocation parity (same command path).

### #317 — Perf: memoize plan hash/fingerprint per description version (warm path)
## Context

T297 (COW) was closed as not viable on time, but its attribution (E38/P04) produced a lever that applies to the **baseline** tree:

- `B_Nextorm_Cached_In_ListContains_Captured`: `qplan_hash` was **76.9%** of the measured gap;
- Cte / RecursiveCte / LeftJoin: raw-recipe planning in `src/nextorm.core/DataContext/QueryPlanner.cs:614-662` (`CaptureRawShape`, `Fingerprint`, `Matches`, `ApplyPreparationFrom` → `CopyTo`, staged `QueryPlan`) was **52–71%**.

The warm cached path still constructs and hashes the query shape on every fresh-fluent call even when the description has not changed.

## Proposal

Memoize the shape fingerprint/hash per description version; reuse the staged `QueryPlan` when the shape matches. Invalidate on mutation via an explicit version counter (the write barrier already exists for `SharedRelations`). No public API change; plan-cache key semantics unchanged.

## Acceptance

- Paired (interleaved ABBA) measurement on `SqliteBenchmarkFeaturesFairCached`, **all families**: measurable time reduction; no statistically significant regression on any arm.
- Allocations non-increasing on every arm.
- Plan-key equivalence tests: identical shape → identical key; any mutation → new key; cached and non-cached paths agree.
- Full test suites (core, dialects, fail-closed integration), build 0W/0E.

## Constraints

- No benchmark-specific shortcuts, no threshold changes, no `Cache = false` mutation of shared commands.
- Evidence: two valid same-round rounds plus raw BDN JSON; thresholds stay as recorded in the issue.

## Related

- #297 — closed not viable; this lever is independent of COW.
- #240 / #201 — warm-path construction allocation scope.

### #311 — Perf: JSON streaming — 10k per-row throughput lever, same-session acceptance pairing, noise-robust ratio oracle (#247 follow-up)
Follow-up to #247 (T247 ended `incomplete` / terminal measured GAP — see #247 for the verdict). Three concrete items, none of them a re-litigation of T247's thresholds:

### 1. Per-row throughput lever for 10k rows
The change removed the per-call (compile/intercept) cost — 100-row ratio 0.0837/0.0887, intercept `a` 2,197,187 → 48,892 ns and 1,851,476 → 78,669 ns — but the 10k comparison against linq2db is still discordant: **0.99323** (pass) / **1.04546** (fail) and 100k paired **0.91917 > 0.81079** / **0.96335 ≤ 1.04409**. ~486–777 µs per call of un-attributed work remains. The open question is the per-row write path (row-writer delegate, boxed reads, `Utf8JsonWriter` usage, buffer flushing), not the compile path.

### 2. Same-session paired acceptance measurement
R247-09 measured **0 of 7** acceptance cases because no paired acceptance baseline exists. The gate needs `<baseline>/round-{1,2}/acceptance/cases.json` captured in the *same* session/pinned tree as the final run; the pre-change tree no longer exists, and pre-committed BDN artifacts are not a valid paired baseline. Define and record the acceptance arm up front, in the same fingerprint, before any product change.

### 3. Noise-robust ratio criterion, via a new spec
The per-round ratio oracle produced discordant rounds on identical code (10k and 100k), so a single-round threshold of 1.0 is not noise-robust at this effect size. This must be resolved by **a new specification** (e.g. min-of-N or paired-difference with a pre-registered noise band, decided before measurement) — explicitly **not** by editing T247's thresholds, tolerances or denominators, which stay frozen for audit.

## Acceptance criteria (2026-10-20 two-tier policy)

**Global target (milestone-level):** JSON streaming at 10,000 rows — time ≤ **1.00×** linq2db and allocations ≤ **1.20×** linq2db, same-session paired measurement.

**Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
- allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
- time improves beyond the pre-registered noise band and allocations do not increase.

A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.

**Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.

### Task-specific deliverables
- A per-row throughput lever for 10k is attributed, implemented and measured; operation-count change is shown.
- A same-session paired acceptance baseline (`<baseline>/round-{1,2}/acceptance/cases.json`, 7/7 cases) is captured before the product change.
- A new noise-robust ratio oracle is pre-registered before its first live measurement; T247’s frozen thresholds, tolerances, denominators and historical GAP are **not** edited.
- Output bytes, streaming/error/cancellation contracts and provider behavior are preserved; cache bounds/isolation remain.


Cross-references: #247 (terminal GAP record), #250 (CSV terminal plan cache, same benchmark family), #297 (CTE structural allocation follow-up, same measurement approach).

### #302 — SelectWhereMax/Min: reconsider the portable window lowering — GROUP BY / join-back strategy (perf)
## Статус

- **Стадия:** **письменная спецификация утверждена USER 2026-10-18** (явный approval: «апрув»). Утверждена revised **SQL-only** редакция: in-memory вне области; native-пути PostgreSQL/ClickHouse сохраняются. Design scope согласован; **свежий независимый builder-check этой редакции выполнен** (одно уже решённое NULL-уточнение внесено без нового user question). Предыдущий builder-check относился к предыдущей редакции и не заменяет fresh check.
- **Разграничение гейтов:** design approval ≠ approval плана ≠ разрешение на исполнение. **План реализации не создан и не утверждён** (следующий допустимый артефакт; требует отдельного USER approval). **Продуктовая реализация не авторизована** — нужно отдельное явное разрешение после approval плана. Автоматического approval нет.
- **Issue:** эта задача, #302, **OPEN**. Title не меняется. Milestone — **`1.0.9-rc5`** (проверен в GitHub; сохраняется, новый не создаётся). Новый issue не создаётся: это та же согласованная задача, расширенная до SQL-only архитектуры.
- **Спецификация:** `docs/superpowers/specs/2026-10-18-issue-302-all-aggregate-lowering-design.md`.
- **Связанные:** #144 — native `One` для PostgreSQL/ClickHouse (closed); его требование window-portability для `All` заменяется этой задачей, исторические артефакты #144 не переписываются; #115 — исходный API (closed).
- Git: commit/push/merge не авторизованы. Issue не закрывается; approval спеки не означает approval плана или approval реализации.

## Согласованные решения

Прежнее обязательное правило `All` (не меняется):

> «в режиме All нужно использовать группировку вместо оконок там, где нет нативного пути».

Это **безусловная стратегия** для поддерживаемых форм `ExtremeRowTies.All`. Она не зависит от индексов, оценок оптимизатора, универсального порога скорости или отдельного performance-approval. Предложение оставить grouped `All` на оконном понижении из-за неблагоприятных индексных планов пользователем **отклонено**; к этому вопросу не возвращаемся.

Согласованное расширение архитектурного scope:

- `SelectWhereMax` / `SelectWhereMin` — **SQL-side синтаксический сахар**: пользовательская форма задаёт логическую семантику, SQL-стратегия строится внутри обычного query-конвейера.
- `ExtremeRow` остаётся **логическим описанием** до выбора SQL-стратегии; отдельного исполняемого SQL-конвейера вокруг него быть не должно.
- **In-memory явно вне области** этой задачи.
- **Native-пути PostgreSQL/ClickHouse должны быть сохранены** (пользователь подтвердил).

## Центральное требование: SQL-side sugar и общий query-tree

`SelectWhereMax/Min` понижается в обычный query-tree/derived source, а SQL собирается **общей** инфраструктурой движка:

- portable `All` → агрегат + join-back/predicate;
- portable `One` → существующая оконная стратегия (без изменений);
- native `One` PostgreSQL/ClickHouse и внешний `IExtremeRowRenderer` — сохраняются.

Результат обязан использовать общие примитивы для source/`FROM`, projection, filter, alias, `DISTINCT`, sorting и parameter rendering. Отдельный полный `MakeExtremeRowOuter`-рендерер, дублирующий эти примитивы, не допускается. Недостаточно перенести существующую SQL-строковую сборку в новый `ExtremeRow`-builder-класс: дублирование должно быть **устранено**, а не переименовано. Специализированный semantic-to-query lowering разрешён и необходим; небольшой адаптер для строкового native-renderer'а допустим, публичный контракт не меняется. Native-адаптер может передать opaque source result в общий outer projection, если нужен общий внутренний механизм; точное представление выбирает план, готовый адаптер не считается проверенным.

**Фаза понижения (architect scope/safety choice, не цитата пользователя):** сохраняются logical command/cache identity и общая clause/preparation/equality/clone bookkeeping, если изменение не строго необходимо для SQL lowering; shared state, нужное неизменному in-memory, не удаляется. Допустимо понижать detached SQL tree **после** формирования логического ключа при условиях: исходная keyed-команда не мутируется, для SQL- и parameter-traversal'ов и cache-hit'ов используется идентичная детерминированная lowered-структура. Before-key normalizer **не обязателен**; если выбран — обязан обработать raw-shape preparation recipes, cache identity, clone state и hash invalidation до ключа. План выбирает и документирует точную фазу и владельца.

## In-memory — явно вне области

- Не удалять и не переписывать `InMemoryExtremeRow`.
- Не добавлять in-memory поддержку subquery join, window, group-row selection.
- Не маршрутизировать in-memory через новый SQL query-tree.
- Не менять source-order winner, null-семантику, guards, async-ограничения или сложность исполнения.
- Не пытаться исправить обнаруженное поведение in-memory с right-side derived join в рамках #302.
- Общие logical objects обязаны оставаться совместимыми с in-memory.
- Существующий in-memory unit suite — **только regression check** из-за общего кода, не implementation deliverable и не расширение scope.

## Область

**В области:**

- все SQL-провайдеры: SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, ClickHouse;
- `All`, grouped и global, `Max` и `Min`;
- скалярный и составной value key;
- агрегатное понижение: grouped — `GROUP BY` + join-back; global — скалярный агрегат + join-back/predicate;
- необходимая общая поддержка построения query-tree: hidden aliases, pass-through columns, derived source;
- сохранение семантики, eligibility, guards, projection/mapping, outer `DISTINCT`, output ordering;
- SQL-generation тесты по всем шести семействам диалектов и live integration в доступном harness;
- публичная документация EN+RU: `All` — агрегатное понижение через общий lowering, native-hook сохранён, `One` без изменения семантики; без ссылок на внутренние спецификации;
- performance evidence как supporting-материал, не acceptance gate.

**Вне области:** in-memory; изменения `One` (кроме внутренних рефакторингов с byte-identical SQL); новые native `All` renderer'ы; API tie-break; новый публичный API; расширение query shapes; новые ограничения runtime-типов/селекторов; стратегия, зависящая от метаданных/индексов/плана; создание схемы/индексов; blanket performance guarantee; переписывание общего normalization/optimizer (LINQ roadmap); рефакторинг других синтаксических фич (`PerParentPaging` — optional, не требуется).

Не является целью удалить каждое упоминание `ExtremeRow`, удалить clause из публичного builder'а/логического `QueryCommand` или переписать plan-cache model. Цель — устранить избыточный исполняемый SQL-конвейер и использовать общие примитивы движка. Логическое состояние, необходимое для unchanged in-memory, может оставаться в общих preparation/cloning/cache структурах.

## Семантические инварианты

Наследуются из `docs/specs/design/issue-144-native-extreme-row.md:22-30`:

- `All` возвращает **все** tied winner-строки; каждая строка — **полная исходная строка**, payload не собирается из произвольных агрегатов.
- Ties — **только** по полному селектору. Пользовательский `OrderBy` влияет только на выдачу после выбора победителей. API tie-break нет.
- Дубликаты исходных строк сохраняются в рамках существующей семантики внешнего `DISTINCT`.
- Поддерживаются `Max` и `Min`.
- Строки, где **любой** компонент value key `NULL`, исключаются **до** агрегата и **до** join-back.
- Nullable-ключи групп поддерживаются; равенство групп при join-back — null-safe **по каждому компоненту** group key, выразимое обычным OR: `(a = b OR (a IS NULL AND b IS NULL))`. Провайдерно-специфичный синтаксис — необязательная оптимизация; отсутствие native null-safe оператора не блокер.
- Группа опускается **только** если после исключения строк с `NULL` хотя бы в одном компоненте value-selector в ней не осталось допустимых строк. `NULL` в group-selector не исключает ни строку, ни группу; в том числе полностью `NULL` составной ключ группы образует допустимую группу. Пустой вход опускается; global на пустом входе возвращает ноль строк.
- Пользовательские фильтры применяются к **одной и той же** eligible-популяции и для экстремума, и для payload (одинаково на обеих сторонах). Winner не может быть отфильтрован только с одной стороны.
- Составной value key сравнивается **лексикографически** (поэтапные агрегаты), никогда не независимыми `MAX(a)`, `MAX(b)`.
- Сохраняются projection, mapping, outer `DISTINCT`, output ordering и текущий набор guards.
- Query shapes не расширяются; ранее принятые selector-типы/формы не начинают отвергаться; новые ограничения runtime-типов/селекторов не вводятся.
- Source mapping и converter-семантика MySQL/MariaDB и остальных провайдеров не меняются.
- Window-lowering **не** используется как fallback для `All`.
- Валидация пользовательского запроса выполняется **до** lowering и остаётся отдельной; внутренние join/grouping/window-узлы не легализуют запрещённые пользовательские формы и не должны вызывать не относящиеся к ним `ExtremeRow` guards. Набор guard-отказов пользовательских запросов не меняется.

## Native-hook

Публичный `IExtremeRowRenderer`, DTO-конструкторы и их ленивость сохраняются. `CanRender` — side-effect-free eligibility **до** любых SQL/alias/parameter-мутаций; отрицательный ответ даёт fallback на build-strategy time, исключение пробрасывается без retry/late fallback. Renderer возвращает строку — адаптер не устраняется «AST-only» подходом. Native `One` PostgreSQL/ClickHouse сохраняется, требование byte-identical SQL для `One` остаётся. Существующие fragment-тесты `One` недостаточны как исторический golden: нужен baseline полного SQL до/после.

## Концептуальная SQL-конструкция

> Architect guidance, не выбранный пользователем синтаксис. Builder может выбрать эквивалентную provider-специфичную SQL без ranking window и оптимизировать scalar/composite раздельно.

1. **Eligible source `S`** — исходный `FROM` + пользовательский фильтр + `IS NOT NULL` по каждому компоненту value key. Одна и та же популяция используется для агрегата и join-back.
2. **Global scalar** — `E = MAX(value)` / `MIN(value)` по `S`; join-back `S` к `E` по равенству value. На пустом `S` агрегат `NULL` → winners нет. Допустима эквивалентная форма `value = (SELECT MAX(value) ...)`.
3. **Grouped scalar** — `E = group key + MAX/MIN(value) GROUP BY group key`; join-back по null-safe равенству group key (по каждому компоненту) и value-сравнению, согласованному с ranking/ties провайдера.
4. **Составной value key** — поэтапное агрегатное ограничение: агрегировать первый компонент в исходных группах (глобально, если group key нет), оставить tied по лучшему префиксу, агрегировать следующий компонент внутри выжившей популяции той же группы, повторять до конца; вернуть только финальные winner-строки. Это даёт `(9,2)` > `(8,100)` и не создаёт несуществующую `(9,100)`. Чистый `NOT EXISTS` не заменяет требуемое агрегатное понижение; допустима другая семантически эквивалентная агрегатная конструкция.

Внутренние derived tables/CTE допустимы в пределах возможностей провайдера; `MATERIALIZED` CTE не предписывается, поддержка CTE не становится требованием к публичной форме. Никакого создания схемы/индексов, навязывания имён индексов, runtime-инспекции схемы или переключения стратегии по плану. Оконное понижение **не** используется как fallback для `All`; окно остаётся стратегией `One` там, где нет native-рендера. SQL-синтаксис/диалектные адаптации — зона builder/plan; общая core-семантика не дублируется по имени провайдера.

## Типовой паритет

- Все текущие допустимые key/group типы, converted keys, nullable keys и arities составных ключей остаются допустимыми.
- Сохраняются bound SQL type semantics, collation equality и поведение floating-point `NaN`/non-finite там, где оно сейчас поддержано.
- Несовместимости вроде `MIN`/`MAX` над SQL Server `bit`/`bool` решаются provider-appropriate агрегатными/order-preserving представлениями, а не сменой контракта.
- Никакого нового публичного API, window-fallback или молчаливого ограничения типов/селекторов как workaround.
- Точные адаптеры и SQL рассматриваются в плане; parity-тесты обязательны. Невозможность паритета — **planning blocker**, а не разрешение изменить требование.

## Кэш и детерминизм

Наследуется из #144: стратегия детерминирована формой запроса и провайдером; нет mutable per-context переключателей/per-instance toggles; отсутствие capability ≠ неподдержанная фича; guards выполняются до диспатча; запрещено мутировать sticky `QueryCommand.Cache`; после decline renderer'а cache остаётся включённым; число запросов, наблюдаемое вызывающей стороной, не меняется; нет client-side materialization; выполняется один DB statement.

Дополнительно: SQL- и parameter-traversal используют одну и ту же детерминированную lowered-структуру; cache-hit её не пересобирает иначе. Одинаковая логическая форма с разными bound values переиспользует план с текущими значениями; другая форма `Where` не даёт ошибочного sharing; raw-shape cold/hit и повторные prepared/fresh запросы сохраняют поведение. Поведение внешнего renderer'а на cache path пинуется тестами как существующее intended behavior; не предполагается, что callbacks никогда не повторяются во время parameter extraction.

## Grounded discovery

Проверенные точки кода (ингредиенты, не доказательство готового сквозного прототипа):

- `src/nextorm.core/DataContext/SqlBuilder.cs:46-49` — ранний специальный диспатч команды с `ExtremeRow`.
- `SqlBuilder.cs:1267-1407` — `MakeExtremeRowOuter` + `AppendOrderBy`: дублирующие projection/`DISTINCT`/ordering/parameter traversal.
- `src/nextorm.core/Expressions/FromExpression.cs:48/106` — derived-table source.
- `src/nextorm.core/DataContext/SqlSourceRenderer.cs:559-593` — общий рендеринг derived source.
- `src/nextorm.core/Expressions/SelectExpression.cs:24,26,109,117,126,133,140` — projection identity и hidden/pass-through metadata.
- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:1515-1544,1579-1598` — pass-through/derived-output columns.
- `src/nextorm.core/Query/SqlFunctions.cs:101` + `src/nextorm.core/Visitors/NormSqlTranslator.cs:186-199` — column-by-name и ссылки на output alias.
- `src/nextorm.core/DataContext/QueryPlanner.cs:609-656` — raw recipe до preparation; `:692-700` — логический ключ до SQL; `:743-745` — сбор SQL/параметров; `:887-909` — параметры на cache-hit.
- `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:478/487` — публичные `CanRender`/`Render`.

Ограничения evidence: это наличие ингредиентов и точек интеграции, **не** успешный end-to-end lowered-прототип. Построение всего SQL AST не объявляется проверенным/протестированным. Общая null-safe OR-форма уже поддержана; отсутствие отдельного native null-safe оператора не является блокером.

## Performance evidence (supporting, не acceptance gate)

Временные артефакты, **не** durable tracked документация:

- `/tmp/opencode/issue-302-all-spike/report.md` — SQLite `3.37.2`, raw SQL, 200k строк.
- `/tmp/opencode/issue-302-all-spike-modern/report.md` — SQLite `3.53.4`, .NET 10, `Microsoft.Data.Sqlite`, raw SQL, 200k строк.

Наблюдения:

- Scalar grouped без индекса: агрегатная форма примерно **1.6–2x** быстрее rank (в отчёте `1.57–2.02`).
- Grouped с подходящим составным индексом: кратный выигрыш (в старом отчёте `7–29x`).
- На modern SQLite при индексе только по value (`ix(v)`) исходный join-back уходит за **20 s** guard (abort), rank — **0.64 s**.
- `MATERIALIZED`-кандидат убирает abort, но в 30 состояниях медленнее rank на **12–81%** (ratios `1.12–1.81`). Это одна исследованная SQL-форма, не доказательство, что агрегатное понижение неверно.
- Composite performance **не измерялся**. Composite `NOT EXISTS` fixture проверял корректность semijoin, а не реализацию агрегатного понижения.
- Не утверждается, что все провайдеры измерены.

Пользователь осведомлён и всё равно требует агрегатный `All`. Универсальное ускорение не гарантируется; порога, при котором нужно оставить окно, нет. Регрессии нельзя маскировать — они должны быть явно показаны. Выбор агрегатной стратегии не зависит от процентов ускорения, индексов или оценок оптимизатора.

## Приёмка

**Архитектура:** существующий общий render используется, параллельного outer-SELECT конвейера нет; дублирование не «переехало» в новый `ExtremeRow`-builder.

**SQL-generation и normalized tree:** шесть семейств диалектов; `Max`/`Min`; grouped/global; скалярный/составной key; контракты `NULL`, group keys, фильтров (одинаково на обеих сторонах), projection, ordering, dedup; для `All` assert'ится агрегатное понижение и отсутствие `rank()`/`row_number()` lowering; hidden alias collision, mapped physical columns, DTO/anonymous/record projection, nullable payload, output sorting, `DISTINCT`; guards исходного запроса — до внутреннего lowering, внутренние узлы не вызывают чужие guards.

**`One`/native:** baseline полного SQL до/после (fragment-тесты недостаточны как golden); native `One` PG/CH и forced-portable дают эквивалентный результат; внешний renderer accept/decline/throw; decline — fallback на build-strategy time, throw — без retry; ленивость payload/DTO не нарушена.

**Кэш/параметры:** одинаковая логическая форма с разными bound values переиспользует план с текущими значениями; другая форма `Where` не даёт ошибочного sharing; raw-shape cold/hit; повторные prepared/fresh запросы; после decline cache остаётся включённым; sticky `QueryCommand.Cache` не мутируется; параметры детерминированы в SQL и extraction mode; custom renderer на cache path ведёт себя согласно пинуемому существующему intended behavior.

**Live integration:** реальный SQLite; container-backed PostgreSQL, SQL Server, MySQL, ClickHouse; MariaDB — SQL-generation плюс live в рамках доступного harness; запуск по `.opencode/skills/running-integration-tests`; unit/SQL-generation тесты не заменяют live; SQL-generation обязателен по всем шести семействам.

**Correctness matrix:** несколько ties; целостность payload; сохранение дубликатов; `NULL` в любом компоненте value key; nullable группы (сохраняются; `NULL` в group-selector, включая полностью `NULL` составной ключ, группу не исключает), группы без допустимых value-строк после исключения `NULL` хотя бы в одном компоненте value-selector (опускаются), пустой вход; фильтры, удаляющие winners, — одна популяция с обеих сторон; `(9,2)` vs `(8,100)`; converted keys, включая SQL Server `bool`; floating-point/collation в пределах поддержки; ordering, projection, distinct. Previous window lowering — только test-only baseline; production window-fallback запрещён. Существующие guard-отказы — без изменений.

**In-memory:** только regression check существующего unit suite; новых тестов/capability нет; поведение не меняется; не implementation deliverable.

**Performance:** воспроизводимый raw/ORM-замер; минимум без индекса, с подходящим индексом и только по value; показываются и улучшения, и регрессии; стратегия не гейтится процентами; никаких schema changes; composite-замеры не выдаются за выполненные, если их не было.

**Документационная ревизия:** эта ревизия — documentation-only; build/tests для неё не запускались, и это ожидаемо. Acceptance gates относятся к будущей реализации.

## Публичная документация (обязательный deliverable)

Обновляются обе локали в одном изменении: `docs/guide/09-distinct.md`, `docs/advanced/select-where-extrema-native.md`, `docs/advanced/api-reference.md` и соответствующие RU-страницы.

- `SelectWhereMax/Min` — SQL-side syntactic sugar; логическая форма понижается внутри общего query-конвейера.
- `All` описывается как агрегатное понижение (grouped `GROUP BY` + join-back; global скалярный агрегат + join-back/predicate; composite лексикографически), ranking window для `All` не используется.
- Native-hook описывается точно: native `One` PG/CH и внешний `IExtremeRowRenderer` сохранены; native `All` renderer'а нет, поэтому `All` идёт агрегатным понижением.
- `One` — без изменения семантики: native PG/CH, portable window в остальных случаях.
- Никаких ссылок на `docs/specs/**` или `docs/superpowers/**` из публичных страниц/readme; обоснование inline.
- EN и RU зеркальны; новых публичных API нет.

## Non-goals

In-memory; переписывание общего normalization/optimizer (LINQ roadmap); редизайн публичного renderer-контракта; удаление clause state как самоцель; рефакторинг других синтаксических фич (`PerParentPaging` — optional); изменения `One` кроме внутренних рефакторингов; новые native `All` renderer'ы; API tie-break; новый публичный API; расширение query shapes или новые ограничения типов/селекторов; стратегия по метаданным/индексам/плану; schema/индексы; blanket performance guarantee. Общие переиспользуемые куски могут пригодиться будущим фичам, но #302 на них не расширяется.

## Риски и блокеры

- Composite-агрегат не измерялся; корректность доказывается тестами, а не выводится из scalar-случая.
- Провайдерные различия null-safe equality, collation, `NaN`/non-finite и `bit`/`bool` могут вскрыть type blockers — planning blocker, не повод менять требование. Отсутствие native null-safe оператора не блокер.
- Агрегатная форма может регрессировать на части планов; это показывается честно, без маскировки и без возврата к окну.
- MariaDB live-доступность зависит от harness; SQL-generation покрытие обязательно в любом случае.
- Необходимость публичного изменения `IExtremeRowRenderer` — остановка и отдельный вопрос на approval.
- Точное внутреннее представление «opaque native source → общая projection» не проверено; это planning detail.
- Обнаруженное in-memory поведение right-side derived join **не** исправляется в #302; фиксируется отдельно как вне-scope находка.

## Следующий гейт

1. ~~Свежий независимый builder-check **этой** редакции~~ — **выполнен**: одно уточнение про `NULL` уже решено унаследованной семантикой (внесено в спецификацию, §4/§5.5.3/§11.6), нового user question не потребовалось. Предыдущий review относился к предыдущей редакции.
2. ~~USER review и approval письменной спецификации~~ — **выполнено 2026-10-18**: USER дал явный approval («апрув») revised SQL-only редакции; in-memory вне области. Автоматического approval нет и не подразумевается.
3. **Текущий гейт:** план реализации. План **ещё не создан и не утверждён**; он требует отдельного USER approval. Реализация (код/тесты/доки продукта) **не авторизована** — нужно отдельное явное разрешение после approval плана.
4. Commit/push/merge не авторизованы.

Approval спецификации не означает approval плана и не означает approval реализации; issue остаётся **OPEN** до завершения будущей реализации.

### #274 — Perf: examples/CH RollingActivity allocates 1.22× linq2db
## Context

The 2026-10-17 examples comparison benchmark (Release, `ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`, warm ClickHouse volume; AMD Ryzen 7 5800HS, .NET 10.0.12; working tree at `c9b12d12`) shows the nextorm arm allocating **1.22×** what the linq2db arm allocates, while time is better:

| Arm | Mean | Allocated |
|---|---:|---:|
| `Nextorm_RollingActivity` | **58.88 ms** | 58.12 KB |
| `Linq2Db_RollingActivity` | 66.74 ms | **47.48 KB** |
| Ratio | 0.88× | **1.22×** |

Benchmark: `examples/nextorm.examples.clickhouse.analytics/Benchmarks.cs`, course query 11 (7-day rolling + cumulative windows) over `datasets.hits_v1`; both arms return 7 rows.

## Why it matters

Time is already better than linq2db; the remaining regression is +10.6 KB of fixed per-call allocation for a 7-row result. Fixing it removes the last allocation regression in this example and keeps the time win.

## Preliminary attribution

Both arms aggregate daily `hits`/`uniqExact` and apply the same two framed windows; nextorm emits the windows with `SqlFunctions.Sql.sum_over(...)` and casts to `long`. Suspects: window descriptor construction (two windows over the same aggregate) and command/parameter allocation.

## Proposed work

1. Profile `Nextorm_RollingActivity` and compare generated SQL with the linq2db arm (`--log-sql`).
2. Remove avoidable per-call allocations in the two-window shape (share the aggregate/frame descriptors, reuse the plan for the repeated shape) while keeping the current time win.
3. Post before/after numbers here and update `examples/VERIFICATION.md`.

## Acceptance criteria

- `Nextorm_RollingActivity` allocated ≤ **1.2×** `Linq2Db_RollingActivity` (stretch ≤1.0×), time remains better than the current 66.7 ms linq2db value, same 7 rows.
- `dotnet build examples/nextorm.examples.clickhouse.analytics -c Release` stays 0W/0E.
- Before/after values (at least two runs each) posted here.

## Reproduction

```bash
cd /home/alex/sources/nextorm
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Release -- \
  --benchmark --filter '*RollingActivity*'
```

## Constraints

- Do not change the modelled SQL semantics or the entity mappings to game allocation.
- Do not remove the linq2db arm.
- `ShortRun` is noisy: validate with at least two runs before/after.

## Related

- Comparison conversion and full benchmark record: `examples/VERIFICATION.md` (2026-10-17).
- Sibling allocation regressions: #244, #245, #246, #247, #250.

### #273 — Perf: examples/CH SessionDepth allocates 1.62× linq2db
## Context

The 2026-10-17 examples comparison benchmark (Release, `ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`, warm ClickHouse volume; AMD Ryzen 7 5800HS, .NET 10.0.12; working tree at `c9b12d12`) shows the nextorm arm allocating **1.62×** what the linq2db arm allocates:

| Arm | Mean | Allocated |
|---|---:|---:|
| `Nextorm_SessionDepth` | 166.90 ms | **88.20 KB** |
| `Linq2Db_SessionDepth` | 115.63 ms | **54.52 KB** |
| Ratio | 1.44× | **1.62×** |

Benchmark: `examples/nextorm.examples.clickhouse.analytics/Benchmarks.cs`, course query 10 (sessionization + `multiIf` depth buckets) over `datasets.hits_v1`; both arms return 5 rows.

## Why it matters

Only 5 rows are returned, so the 33.7 KB delta is fixed per-call cost. Time also regresses 1.44×, so the two are likely connected (extra descriptor/plan work).

## Preliminary attribution

Both arms use `lagInFrame`/cumulative-sum windows, a grouped session table and `multiIf` buckets. nextorm expresses the bucket in `GroupBy`/`OrderBy`/`Select` via `SqlFunctions.ClickHouse.multi_if(when(...), otherwise(...))`; suspects are the three descriptor instances of the same bucket expression and per-call plan construction. Confirm by profiling and SQL diff.

## Proposed work

1. Profile `Nextorm_SessionDepth` and compare generated SQL with the linq2db arm (`--log-sql`).
2. Avoid rebuilding the `multiIf` bucket expression three times per call (shared descriptor / cached plan) and reduce per-call command/parameter allocation.
3. Post before/after numbers here and update `examples/VERIFICATION.md`.

## Acceptance criteria

- `Nextorm_SessionDepth` allocated ≤ **1.2×** `Linq2Db_SessionDepth` (stretch ≤1.0×), time not worse than the current 166.9 ms, same 5 rows.
- `dotnet build examples/nextorm.examples.clickhouse.analytics -c Release` stays 0W/0E.
- Before/after values (at least two runs each) posted here.

## Reproduction

```bash
cd /home/alex/sources/nextorm
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Release -- \
  --benchmark --filter '*SessionDepth*'
```

## Constraints

- Do not change the modelled SQL semantics or the entity mappings to game allocation.
- Do not remove the linq2db arm.
- `ShortRun` is noisy: validate with at least two runs before/after.

## Related

- Comparison conversion and full benchmark record: `examples/VERIFICATION.md` (2026-10-17).
- Sibling allocation regressions: #244, #245, #246, #247, #250.

### #272 — Perf: examples/CH Sessions allocates 2.82× linq2db
## Context

The 2026-10-17 examples comparison benchmark (Release, `ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`, warm ClickHouse volume; AMD Ryzen 7 5800HS, .NET 10.0.12; working tree at `c9b12d12`) shows the nextorm arm allocating **2.82×** what the linq2db arm allocates:

| Arm | Mean | Median | Allocated |
|---|---:|---:|---:|
| `Nextorm_Sessions` | 8,023.28 ms* | 1,270.66 ms | **488.07 KB** |
| `Linq2Db_Sessions` | 1,033.79 ms | 1,045.71 ms | **173.20 KB** |
| Ratio | — | 1.22× | **2.82×** |

\* the mean is skewed by a heavy tail (StdDev 11.8 s); use the median for time, allocation is stable. Benchmark: `examples/nextorm.examples.clickhouse.analytics/Benchmarks.cs`, query `Sql/clickhouse_sessions.sql` over `datasets.hits_v1`; both arms return 100 rows.

## Why it matters

This is the only case where the gap is plausibly per-row (+3.15 KB per returned row over 100 rows) as well as fixed: session windows, the quantile threshold and the 100-row projection. It is the largest absolute allocation regression in ClickHouse (315 KB per call).

## Preliminary attribution

Both arms use three chained CTEs, `lagInFrame`/cumulative-sum windows and a `quantile(0.99)` threshold; nextorm additionally casts `UserID`/`session_id` to `long` and materialises `session_id` via `Sql.Expr`. Suspects: per-row anonymous-type materialisation, window descriptor construction, and the threshold query/plan. Confirm with allocation tracing, not by inspection alone.

## Proposed work

1. Profile `Nextorm_Sessions` (BDN `MemoryDiagnoser`, allocation tracing) and compare generated SQL with the linq2db arm (`--log-sql`).
2. Separate fixed (plan/command/descriptor) from per-row (materialiser) allocation; reduce the dominant one. For per-row, check the row materialiser/anonymous projection; for fixed, reuse the plan for a repeated shape.
3. Post before/after numbers here and update `examples/VERIFICATION.md`.

## Acceptance criteria

- `Nextorm_Sessions` allocated ≤ **1.2×** `Linq2Db_Sessions` (stretch ≤1.0×), median time not worse than the current 1.27 s, same 100 rows.
- `dotnet build examples/nextorm.examples.clickhouse.analytics -c Release` stays 0W/0E.
- Before/after values (at least two runs each) posted here.

## Reproduction

```bash
cd /home/alex/sources/nextorm
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Release -- \
  --benchmark --filter '*Sessions*'
```

## Constraints

- Do not change the modelled SQL semantics or the entity mappings to game allocation.
- Do not remove the linq2db arm.
- `ShortRun` is noisy (this case has a heavy tail): validate with at least two runs and report the median.

## Related

- Comparison conversion and full benchmark record: `examples/VERIFICATION.md` (2026-10-17).
- Sibling allocation regressions: #244, #245, #246, #247, #250.

### #271 — Perf: examples/CH Retention allocates 3.02× linq2db
## Context

The 2026-10-17 examples comparison benchmark (Release, `ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`, warm ClickHouse volume; AMD Ryzen 7 5800HS, .NET 10.0.12; working tree at `c9b12d12`) shows the nextorm arm allocating **3.02×** what the linq2db arm allocates:

| Arm | Mean | Allocated |
|---|---:|---:|
| `Nextorm_Retention` | 283.38 ms | **98.80 KB** |
| `Linq2Db_Retention` | 279.03 ms | **32.75 KB** |
| Ratio | 1.02× | **3.02×** |

Benchmark: `examples/nextorm.examples.clickhouse.analytics/Benchmarks.cs`, query `Sql/clickhouse_retention.sql` over `datasets.hits_v1`; both arms return 1 row. Allocation is per single operation; server memory is not measured.

Caveat: the two arms intentionally materialise the matrix differently — nextorm returns `Tuple<byte,double>[]` and formats it in C#, while the linq2db arm uses `FromSql` with `toString(groupArray(...))`. The comparison is therefore not purely ORM-vs-ORM: part of the gap is representation plus the raw terminal. It is still the fixed per-call cost of the nextorm LINQ CTE chain (`first_visits`/`cohort_sizes`/`weekly` + `group_array`) that should be reduced.

## Why it matters

A single row is returned, so 98.8 KB is entirely fixed query-build/plan cost plus the small tuple array. This is a good probe for the cost of three chained typed CTEs and `group_array(Tuple.Create(...))`.

## Preliminary attribution

Suspects: three CTE descriptors, the `toMonday`/`date_diff`/`cast` expressions, `group_array(Tuple.Create(...))` argument construction, and command/parameter allocation. Confirm by tracing and by comparing the generated SQL with the `FromSql` SQL.

## Proposed work

1. Profile the arm and compare generated SQL with the linq2db arm (`--log-sql`).
2. Reduce per-call allocations in the CTE/`group_array` path (descriptor reuse/plan cache for the repeated shape, without mutating a shared `QueryCommand`).
3. Keep a raw-terminal and a LINQ-vs-LINQ control so the acceptance number is interpreted correctly; post before/after values here and in `examples/VERIFICATION.md`.

## Acceptance criteria

- `Nextorm_Retention` allocated ≤ **1.2×** `Linq2Db_Retention` (stretch ≤1.0×), time not worse than the current 283.4 ms, same 1 row and correct matrix.
- `dotnet build examples/nextorm.examples.clickhouse.analytics -c Release` stays 0W/0E.
- Before/after values (at least two runs each) posted here.

## Reproduction

```bash
cd /home/alex/sources/nextorm
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Release -- \
  --benchmark --filter '*Retention*'
```

## Constraints

- Do not change the modelled SQL semantics or the entity mappings to game allocation.
- Do not remove the linq2db arm or its `FromSql` fallback.
- `ShortRun` is noisy: validate with at least two runs before/after.

## Related

- Comparison conversion and full benchmark record: `examples/VERIFICATION.md` (2026-10-17).
- Sibling allocation regressions: #244, #245, #246, #247, #250.

### #270 — Perf: examples/CH Funnel allocates 3.04× linq2db
## Context

The 2026-10-17 examples comparison benchmark (Release, `ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`, warm ClickHouse volume; AMD Ryzen 7 5800HS, .NET 10.0.12; working tree at `c9b12d12`) shows the nextorm arm allocating **3.04×** what the linq2db arm allocates:

| Arm | Mean | Allocated |
|---|---:|---:|
| `Nextorm_Funnel` | 189.70 ms | **161.07 KB** |
| `Linq2Db_Funnel` | 193.58 ms | **52.93 KB** |
| Ratio | 0.98× | **3.04×** |

Benchmark: `examples/nextorm.examples.clickhouse.analytics/Benchmarks.cs`, query `Sql/clickhouse_funnel.sql` over `datasets.hits_v1`; both arms return 3 rows. Allocation is per single operation (build + execute + materialise); server memory is not measured.

Caveat: nextorm uses the LINQ `SqlFunctions.ClickHouse.window_funnel(...)` surface, while the linq2db arm issues the per-user `windowFunnel` part through `FromSql` and then groups with LINQ. Part of the gap is LINQ descriptor construction vs a raw terminal, but +108 KB per call for a 3-row result is fixed overhead.

## Why it matters

Three rows are returned, so this is a fixed per-call cost (window-function descriptor, derived-query/CTE plan, command/parameter setup), not materialisation. This is the largest ClickHouse allocation regression.

## Preliminary attribution

Suspects: construction of the `windowFunnel` descriptor and its three boolean arguments per call, derived-query/CTE plan building, and command/parameter allocation. Confirm with allocation tracing and by comparing the generated SQL (`--log-sql`) with the `FromSql` SQL.

## Proposed work

1. Profile `Nextorm_Funnel` (BDN `MemoryDiagnoser` + allocation tracing) and diff the generated SQL against the linq2db arm.
2. Reduce per-call descriptor/plan allocation for the window-funnel shape (cache the plan/descriptor for the repeated query without mutating a shared `QueryCommand`).
3. Add a LINQ-vs-LINQ control if the `FromSql` advantage remains, and record before/after numbers here and in `examples/VERIFICATION.md`.

## Acceptance criteria

- `Nextorm_Funnel` allocated ≤ **1.2×** `Linq2Db_Funnel` (stretch ≤1.0×), time not worse than the current 189.7 ms, same 3 rows.
- `dotnet build examples/nextorm.examples.clickhouse.analytics -c Release` stays 0W/0E.
- Before/after values (at least two runs each) posted here.

## Reproduction

```bash
cd /home/alex/sources/nextorm
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Release -- \
  --benchmark --filter '*Funnel*'
```

## Constraints

- Do not change the modelled SQL semantics or the entity mappings to game allocation.
- Do not remove the linq2db arm or its `FromSql` fallback.
- `ShortRun` is noisy: validate with at least two runs before/after.

## Related

- Comparison conversion and full benchmark record: `examples/VERIFICATION.md` (2026-10-17).
- Sibling allocation regressions: #244, #245, #246, #247, #250.

### #269 — Perf: examples/PG AirportOnTimePerformance allocates 1.23× linq2db
## Context

The 2026-10-17 examples comparison benchmark (Release, `ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`, warm Testcontainers volumes; AMD Ryzen 7 5800HS, .NET 10.0.12; working tree at `c9b12d12`) shows the nextorm arm allocating **1.23×** what the linq2db arm allocates:

| Arm | Mean | Allocated |
|---|---:|---:|
| `Nextorm_AirportOnTimePerformance` | 62.78 ms | **52.81 KB** |
| `Linq2Db_AirportOnTimePerformance` | 62.08 ms | **43.07 KB** |
| Ratio | 1.01× | **1.23×** |

Benchmark: `examples/nextorm.examples.postgres.aviasales/Benchmarks.cs`. Both arms model the airport on-time-performance query (63 rows) with conditional aggregation over `bookings.timetable`.

## Why it matters

Time is at parity; the regression is +9.7 KB of fixed per-call allocation for a 63-row result. It points at query-build/parameter overhead rather than materialisation.

## Preliminary attribution

The nextorm query chains `delay`/`isDelayed`/`isCancelled` projections and conditional `sum` aggregates. The `date_diff` expression is duplicated in `Delay` and `IsDelayed`; descriptor/parameter construction for the repeated expression is a plausible source. Confirm by profiling, not by inspection alone.

## Proposed work

1. Profile the arm with BDN `MemoryDiagnoser` and compare SQL with the linq2db arm (`--log-sql`).
2. Remove avoidable repeated expression/parameter construction (share the `date_diff` descriptor between `Delay` and `IsDelayed`, or cache the plan for the repeated shape) without changing the SQL result.
3. Post before/after numbers and update `examples/VERIFICATION.md`.

## Acceptance criteria

- `Nextorm_AirportOnTimePerformance` allocated ≤ **1.2×** `Linq2Db_AirportOnTimePerformance` (stretch ≤1.0×), time not worse than the current 62.8 ms, same 63 rows.
- `dotnet build examples/nextorm.examples.postgres.aviasales -c Release` stays 0W/0E.
- Before/after values (at least two runs each) posted here.

## Reproduction

```bash
cd /home/alex/sources/nextorm
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Release -- \
  --benchmark --filter '*AirportOnTimePerformance*'
```

## Constraints

- Do not change the modelled SQL semantics or the entity mappings to game allocation.
- Do not remove the linq2db arm.
- `ShortRun` is noisy: validate with at least two runs before/after.

## Related

- Comparison conversion and full benchmark record: `examples/VERIFICATION.md` (2026-10-17).
- Sibling allocation regressions: #244, #245, #246, #247, #250.

### #268 — Perf: examples/PG AircraftDelayChains allocates 1.42× linq2db
## Context

The 2026-10-17 examples comparison benchmark (Release, `ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`, warm Testcontainers volumes; AMD Ryzen 7 5800HS, .NET 10.0.12; working tree at `c9b12d12`) shows the nextorm arm allocating **1.42×** what the linq2db arm allocates:

| Arm | Mean | Allocated |
|---|---:|---:|
| `Nextorm_AircraftDelayChains` | 54.30 ms | **60.18 KB** |
| `Linq2Db_AircraftDelayChains` | 55.25 ms | **42.51 KB** |
| Ratio | 0.98× | **1.42×** |

Benchmark: `examples/nextorm.examples.postgres.aviasales/Benchmarks.cs`. Both arms model `Sql/aircraft_delay_chains.sql` (two CTEs + `LAG` windows + join) over the same dataset and return 30 rows. Time is at parity; only allocation regresses (+17.7 KB per call).

## Why it matters

This is the CTE/window-heavy reference query, and the extra allocation is fixed per call (30 rows, tiny result set). It is a useful proxy for the cost of typed CTE + window descriptors in nextorm versus linq2db's cached query plan.

## Preliminary attribution

Both arms are LINQ with `AsCte` + `Sql.Window`; the difference is likely in how nextorm builds the two CTE descriptors and the window functions per call (plan-key/command construction), not in materialisation. Confirm with allocation tracing before changing anything.

## Proposed work

1. Profile the nextorm arm and compare its generated SQL with the linq2db arm (`--log-sql`); identify whether the extra allocations sit in CTE descriptor construction, window expression building, or command/parameter setup.
2. Remove avoidable per-call allocations in that path (cache the plan/command shape for a repeated identical query without mutating a shared `QueryCommand`).
3. Record before/after numbers in this issue and `examples/VERIFICATION.md`.

## Acceptance criteria

- `Nextorm_AircraftDelayChains` allocated ≤ **1.2×** `Linq2Db_AircraftDelayChains` (stretch ≤1.0×), time not worse than the current 54.3 ms, same 30 rows.
- `dotnet build examples/nextorm.examples.postgres.aviasales -c Release` stays 0W/0E.
- Before/after values (at least two runs each) posted here.

## Reproduction

```bash
cd /home/alex/sources/nextorm
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Release -- \
  --benchmark --filter '*AircraftDelayChains*'
```

## Constraints

- Do not change the modelled SQL semantics or the entity mappings to game allocation.
- Do not remove the linq2db arm.
- `ShortRun` is noisy: validate with at least two runs before/after.

## Related

- Comparison conversion and full benchmark record: `examples/VERIFICATION.md` (2026-10-17).
- Sibling allocation regressions: #244, #245, #246, #247, #250.

### #267 — Perf: examples/PG DelayPercentilesByModel allocates 5.67× linq2db
## Context

The 2026-10-17 examples comparison benchmark (Release, `ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`, warm Testcontainers volumes; AMD Ryzen 7 5800HS, .NET 10.0.12; working tree at `c9b12d12`) shows the nextorm arm allocating **5.67×** what the linq2db arm allocates on the same query and dataset:

| Arm | Mean | Allocated |
|---|---:|---:|
| `Nextorm_DelayPercentilesByModel` | 45.05 ms | **31.30 KB** |
| `Linq2Db_DelayPercentilesByModel` | 45.04 ms | **5.52 KB** |
| Ratio | 1.00× | **5.67×** |

Benchmark: `examples/nextorm.examples.postgres.aviasales/Benchmarks.cs`. Both arms model `Sql/delay_percentiles_by_model.sql` over the same 3M-row `bookings` dataset and return 8 rows. Allocation is per single operation (build + execute + materialise); database server memory is not measured.

Caveat: the linq2db arm issues the ordered-set `percentile_disc(...) WITHIN GROUP (...)` through `FromSql` (linq2db has no LINQ surface for it), while nextorm uses `SqlFunctions.Postgres.percentile_disc(...)`. Part of the gap is therefore LINQ translation vs a raw terminal, but 25.8 KB per call is still worth removing.

## Why it matters

Only 8 rows are returned, so allocation is dominated by fixed per-call work (plan/command/parameter construction and the aggregate expression), not by row materialisation. This is the largest allocation regression in the PostgreSQL example.

## Preliminary attribution

The nextorm arm builds a `percentile_disc` aggregate from a lambda (`() => SqlFunctions.Sql.date_diff(...)`) inside a `GroupBy` projection. Suspects: per-call expression/parameter construction for the aggregate, the `date_diff` argument closure, and plan-key/command allocation. Needs a MemoryDiagnoser breakdown, not a guess.

## Proposed work

1. Profile `Nextorm_DelayPercentilesByModel` with `DOTNET_` allocation tracing / BDN `MemoryDiagnoser` and compare the generated SQL (`--log-sql`) with the linq2db `FromSql` SQL; confirm whether row materialisation or per-call build dominates.
2. Remove avoidable per-call expression/parameter allocation in the `percentile_disc` + `date_diff` path (or cache the aggregate descriptor/plan for the repeated shape without mutating a shared `QueryCommand`).
3. If the remaining gap is the raw-SQL advantage, document it explicitly and add a LINQ-vs-LINQ control row so the comparison stays honest.
4. Record the post-fix numbers in `examples/VERIFICATION.md` and in this issue.

## Acceptance criteria

- `Nextorm_DelayPercentilesByModel` allocated ≤ **1.2×** `Linq2Db_DelayPercentilesByModel` (stretch ≤1.0×), with no time regression and the same 8 rows in both arms.
- `dotnet build examples/nextorm.examples.postgres.aviasales -c Release` stays 0W/0E.
- Before/after values (at least two runs each) posted here.

## Reproduction

```bash
cd /home/alex/sources/nextorm
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Release -- \
  --benchmark --filter '*DelayPercentilesByModel*'
```

## Constraints

- Do not change the modelled SQL semantics or the entity mappings to game allocation.
- Do not remove the linq2db arm or the `FromSql` fallback.
- `ShortRun` is noisy: validate with at least two runs before/after.

## Related

- Comparison conversion and full benchmark record: `examples/VERIFICATION.md` (2026-10-17).
- Sibling allocation regressions: #244, #245, #246, #247, #250.

### #250 — Perf: CSV terminal recompiles its column plan on every export (terminal plan cache, CSV slice)
## Context

`todo_csv_streaming.md` §2(a) is still open: the CSV terminal compiles its column plan on every export. Issue #112 (the feature) is closed, but that slice was left in the plan without a tracking issue, so this ticket tracks it. Same fixed-cost pattern as the JSON terminal in #247, measured on the same machine/day.

## Evidence (measured 2026-10-17)

Harness: new benchmark class `benchmarks/nextorm.benchmark/SqliteBenchmarkCsvFixedCost.cs` (currently untracked in the working tree), `ShortRun` + in-process toolchain, SQLite `large_table` (10 000 rows, `long`/`string`/`DateTime`), projections 1/3/8 columns, `Limit(1..10000)`. Report: `benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsvFixedCost-report-github.md`.

| Rows | ToList 3-col (warm plan) | WriteCsv 1 col | WriteCsv 3 col | WriteCsv 8 col |
|---|---|---|---|---|
| 1 | 17.0 µs | 198.0 µs | 571.1 µs | 1 621.0 µs |
| 10 | 24.1 µs | 204.2 µs | 588.0 µs | 1 618.2 µs |
| 100 | 97.5 µs | 235.4 µs | 697.9 µs | 1 924.3 µs |
| 1 000 | 829.4 µs | 544.8 µs | 1 637.1 µs | 4 815.6 µs |
| 10 000 | 8 526 µs | 3 678 µs | 10 907 µs | 31 963 µs |

Fit of `T(N) = fixed + N × perRow`:

| Arm | fixed | perRow |
|---|---|---|
| ToList 3-col | ~12–17 µs | 0.86 µs |
| WriteCsv 1 col | ~198 µs | 0.35 µs |
| WriteCsv 3 col | ~590 µs | 1.03 µs |
| WriteCsv 8 col | ~1 650 µs | 3.02 µs |

- **~200–207 µs of fixed cost per column**, plus ~8–9 KB of plan garbage per column per call (WriteCsv 3-col at 1 row: 28.1 KB vs 4.5 KB for `ToList`).
- The whole delta against the warmed `ToList` arm is accounted for by 3 × ~200 µs, i.e. **the per-column compile dominates; SQL re-planning on SQLite is small** (< ~50 µs). Caching only the column plan (not the SQL plan) is therefore the right first target.
- Cost share for a 3-column export: 100 rows — 590/698 µs (**85 %**); 1 000 rows — 36 %; 10 000 rows — 5.4 %.

## Root cause

- `CsvStreamWriter.Build` compiles one delegate per column: `Expression.Lambda<Action<IDataRecord, CsvRowBuffer>>(...).Compile()` — `src/nextorm.core/Query/Csv/CsvStreamWriter.cs:312-313`, invoked from `QueryCommandExtensions.cs:431` and `:475` on every call.
- CSV preparation deliberately skips the plan cache: `DataContext.PrepareResultCommand` → `storeInCache: false` (`src/nextorm.core/DataContext/DataContext.cs:573-582`), so the SQL plan is rebuilt too (small on SQLite, may matter more on PostgreSQL/SQL Server — not measured yet).

## Proposed work

1. One **bounded per-context** cache of compiled terminal plans, shared in design with the JSON shape/writer cache proposed in #247 (one mechanism, not two; J3 from #180: bounded, `MapperCache`-style, no unbounded static dictionary).
2. CSV key: concrete context type (`MapTypedColumnExpression` is `protected virtual`, `DataContext.cs:747`), projection signature, resolved reader storage types, applied `SequentialAccess` mode, CSV options (delimiter, null marker, Excel mode, header). **`ValueTransform` must bypass the cache** — it is a per-call delegate that may capture state.
3. Cache is per-context, cleared by `DataContextCache.Clear()`; never mutate the shared `QueryCommand`, never flip `QueryCommand.Cache` (sticky-state hazard).
4. Preserve every fail-closed behaviour: unsupported projection rejected before the header, `AsTempTable` guard, in-memory `NotSupportedException`.
5. Commit the harness and add a small-export case to the perf gate.

## Acceptance criteria (2026-10-20 two-tier policy)

**Global target (milestone-level):** 3-column export — 100 rows ≤ **230 µs**, 1,000 rows ≤ **1.1 ms**, 10,000 rows within ±3 % of **10.9 ms**; allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db on the same-round report.

**Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
- allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
- time improves beyond the pre-registered noise band and allocations do not increase.

A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.

**Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.

The cache-hit allocation win (≥8 KB/column per call) is a valid local acceptance even if the absolute time targets are missed, provided time does not worsen beyond the noise band.

### Mandatory correctness and evidence
- Remove the per-column plan garbage on cache hits (≥8 KB/column per call).
- Correctness tests: a cache hit is byte-identical to a cold run; different options/provider/storage types/sequential mode never alias; `ValueTransform` always bypasses; `DataContextCache.Clear()` drops the entries; size stays within the bound.
- No public API change; JSON and CSV do not regress each other.

## Constraints

- No output change: CSV dialect contract (RFC 4180, CRLF, null marker, Excel guard) stays as shipped.
- Do not promote a CSV plan into the normal query plan cache without an owner decision; CSV keeps its separate preparation shape.

## Related

- #112 — CSV streaming feature (closed; this slice remains).
- #167, #168 — chunked `byte[]` read and SQL Server typed numeric mapping.
- #247 — JSON streaming fixed cost (same root cause; measured fixed cost is ~2.9× larger there).
- Specs: `docs/specs/roadmap/todo_csv_streaming.md` §2(a), `docs/advanced/limitations.md`.

### #247 — Perf: JSON streaming is slower than linq2db at 10k rows
## Context

The 2026-10-17 warm-path feature report (release 1.0.9-rc2, branch `1.0.9-rc3`, HEAD `881bf497`) shows JSON streaming slower than linq2db at the 10,000-row size, despite lower allocations:

| JSON array, DTO payload | Nextorm `WriteJson_Array_Dto` | linq2db materialize + `JsonSerializer` | Nextorm / linq2db |
|---|---:|---:|---:|
| 1,000 rows | 2.132 ms / 84.01 KB | 0.562 ms / 90.39 KB | **3.79×** time |
| 10,000 rows | 10.352 ms / 435.64 KB | **9.150 ms / 963.56 KB** | **1.13×** time (0.45× allocation) |
| 100,000 rows | **82.891 ms** / 3,951.92 KB | 86.797 ms / 9,087.21 KB | 0.95× time |

Source: `benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report.csv` (rows filtered by `RowCount`; 10,000 is the headline size); new report: `docs/comparisons/feature-benchmarks-2026-10-17.md`.

Important nuance: `WriteJson` does not materialize `TResult` (the 435.64 KB is not a DTO list). It is ~44 B/row, consistent with per-row `GetString` for the `name` column plus writer/plan state: the key-only `WriteJson_Array_Scalar` is 38.04 KB at 10,000 rows. “Zero materialization” therefore does not mean “zero allocation”. The 1k/100k points show a large **fixed per-call cost**: Nextorm loses badly at 1k rows and is roughly level at 100k. The direct writer also spends 4.18 ms more than materializing the same DTOs (`ToList_Dto`, 6.170 ms / 785.21 KB at 10k, no serialization); that is a profiling signal, not a like-for-like serialization comparison — the control arm in the proposal makes it fair.

## Root-cause analysis

`WriteJson` rebuilds the streaming machinery on every call:

- `src/nextorm.core/DataContext/DataContext.cs:353-420` (`PrepareJsonStream`) clones the command, resets preparation, prepares a document-mode command with `storeInCache: false`, builds `JsonShapePlan`, and compiles a new `JsonRowWriter` via `JsonRowWriterFactory.Build` on **every call**; there is no shape/writer cache.
- `src/nextorm.core/Query/Json/JsonStreamWriter.cs:47-65` — `WriteRow` calls `Utf8JsonWriter.Flush()` and `_sink.Flush()` after **every row**; NDJSON needs per-record boundaries, but array mode can batch flushes.
- The per-row path reads strings with `IDataRecord.GetString` (one string allocation per row); this is the main remaining allocation, not DTO materialization.

The fixed-cost pattern (3.79× at 1k, 1.13× at 10k, 0.95× at 100k) is exactly what per-call clone/prepare/plan/compile plus per-row flush would produce.

## Proposed work

1. **Attribute first.** Add a JSON phase benchmark (1k/10k/100k) that separates command clone/prepare, shape-plan build, row-writer compile, per-row `WriteRow`, and flush. Use the 1k row case to expose fixed costs; do not optimize only the 10k number.
2. **Cache the immutable shape/writer.** Cache `JsonShapePlan` and the compiled `JsonRowWriter` keyed by projection shape + options + naming policy; make it bounded/thread-safe and invalidate on metadata/options changes. Keep the command/parameter path separate.
3. **Batch array-mode flushes.** In `JsonStreamWriter.WriteRow`, avoid flushing `Utf8JsonWriter` and the pooled sink after every row for array mode; flush when the pooled buffer is full or at `Complete`, while preserving bounded memory and NDJSON’s per-record behavior. Add a buffer-size threshold test.
4. **Reuse a document-mode prepared descriptor.** Evaluate caching the no-mapper document preparation (or its immutable recipe) keyed by shape, instead of clone + `storeInCache: false` per call. Preserve the rule that the caller’s command is never mutated and no plan is promoted into the normal cache without an owner decision.
5. **Reduce fixed setup.** Pool/reuse `JsonStreamWriter`/`Utf8JsonWriter` where ownership allows, or at least avoid rebuilding `JsonShapePlan`/expression trees; measure before deciding whether the per-row string extraction needs a `GetChars`/pooled-char path.
6. **Add a control arm.** Benchmark Nextorm `ToList_Dto + JsonSerializer.Serialize` with the same payload and sink; this distinguishes “direct writer is slow” from “serialization itself is slow” and prevents an unfair comparison.

## Acceptance criteria (2026-10-20 two-tier policy)

**Global target (milestone-level):** at 10,000 rows, `WriteJson_Array_Dto` ≤ **1.00×** linq2db time and ≤ **1.20×** linq2db allocations; at 1,000 rows, the fixed-cost gap is materially reduced; 100k rows do not regress. The original **9.150 ms / 435.64 KB** and ≤1.2× 1k targets remain the global/stretch reference.

**Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
- allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
- time improves beyond the pre-registered noise band and allocations do not increase.

A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.

**Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.

Note: 10k allocations are already below linq2db, so a **time-only win with allocations not increasing** is sufficient for local acceptance.

### Mandatory correctness and evidence
- No public API/output change: valid JSON, same DTO payload, array/NDJSON/root/IgnoreNull semantics, cancellation and partial-output/error policy preserved.
- Add JSON 1k/10k/100k rows to the performance gate; include the `ToList_Dto + JsonSerializer` control in the report.
- Explain the remaining per-row string allocation explicitly; “zero materialization” is not claimed as zero allocation.

## Constraints

- Do not switch the public `WriteJson` contract to materialize DTOs; direct streaming must remain the default.
- Do not weaken JSON validation, cancellation, or error policy for speed.
- No benchmark-specific payload checks; cache keys must be general and bounded.

## Related

- #39 — `WriteJson`/`WriteJsonAsync` streaming feature.
- #176-#180 — JSON streaming phases/validation.
- #188 — Tier-1 cross-library JSON/CSV benchmark.
- Report: `docs/comparisons/feature-benchmarks-2026-10-17.md`.

### #246 — Perf: ToDataReader raw reader is slower than linq2db
## Context

The 2026-10-17 warm-path feature report (release 1.0.9-rc2, branch `1.0.9-rc3`, HEAD `881bf497`) shows the zero-materialization raw reader slower than linq2db, while the report’s other streaming rows are fine:

| Arm | Nextorm | linq2db | Dapper | Nextorm / linq2db |
|---|---:|---:|---:|---:|
| Raw reader, 10,000 rows | 4.677 ms / 947.98 KB | **3.942 ms / 943.00 KB** | 3.996 ms / 942.77 KB | **1.19×** time, **1.005×** allocation |
| Raw reader, empty result | 237.7 µs / 12.74 KB | **109.2 µs / 5.52 KB** | 126.7 µs / 5.29 KB | **2.18×** time, **2.31×** allocation |
| Prepared control, 10,000 rows | 4.754 ms / 944.8 KB | — | — | 1.21× time, 1.00× allocation |

Source: `benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report.csv`; new report: `docs/comparisons/feature-benchmarks-2026-10-17.md`.

The benchmark is `B_Nextorm_ToDataReader`: a fresh `_ctx.LargeEntity.Select(...).ToDataReader()` against linq2db `ExecuteReader` and Dapper `ExecuteReader`, all scanning the same `id, someString` rows through one non-retaining sink. The prepared Nextorm control is slower too, so this is **not** primarily command construction.

## Root-cause analysis

`ToDataReader` on SQLite currently takes the LOB reader seam and wraps the provider reader:

- `src/nextorm.core/Query/QueryCommandExtensions.cs:326-355` — `ToDataReader` calls `OpenLobReader` when `Dialect.LobLocatorColumn is null` (SQLite), otherwise `OpenResultReader`.
- `src/nextorm.core/DataContext/DataContext.cs:469-505` — `OpenLobReader` uses `PrepareLobCommand`, which prepares with `storeInCache: false` and goes through the LOB path; `OpenResultReader` is the plain multi-column seam.
- `src/nextorm.core/DataContext/LobDataReader.cs:20-90` — every `Read`, `GetInt64`/`GetString` (and other accessors) is forwarded through an extra `DbDataReader` wrapper with `ThrowIfDisposed` and a cancellation check; on SQLite there is no LOB locator to manage for this projection.

The empty-result arm is decisive: with no rows to scan, Nextorm still needs 2.18× linq2db’s time and 2.31× its allocation. That points to fixed per-call reader/LOB setup and wrapper overhead, not row scanning or `GetString`.

## Proposed work

1. **Route `ToDataReader` through the plain result-reader seam.** For a multi-column raw reader that does not need a LOB locator, use `OpenResultReader`/its executor path instead of `OpenLobReader` + `LobDataReader`; reserve the LOB seam for projections that actually require locator/LOB semantics (and for `ToStream`/`ToTextReader`). Verify SQL, LOB behavior and ownership/dispose behavior on SQLite and the sequential-access providers.
2. **Remove the per-access wrapper cost.** If an owner-aware wrapper is still required, make it a lean pass-through: `[MethodImpl(AggressiveInlining)]` on `Read`/typed getters, no per-access cancellation check when the token cannot be canceled, and cached field count/ordinal metadata. Consider returning the provider reader through an owner object rather than intercepting every accessor.
3. **Avoid per-call preparation duplication.** `OpenLobReader`/`OpenResultReader` use `storeInCache: false`; evaluate an internal cached reader-plan descriptor for repeated shapes without mutating a shared `QueryCommand` or exposing `Prepare()`. The prepared control shows ~3 KB of the gap, so do this after the wrapper fix.
4. **Add a direct-provider-reader control.** Benchmark Nextorm raw reader vs the same `DbDataReader` obtained directly from the provider; this separates executor/command overhead from wrapper/scanner overhead.
5. **Keep the empty arm in the gate.** It is the cheapest regression detector for fixed overhead.

## Acceptance criteria (2026-10-20 two-tier policy)

**Global target (milestone-level):** raw reader at 10,000 rows ≤ **1.00×** linq2db time and ≤ **1.20×** linq2db allocations; the empty-result arm likewise (or a documented, owner-approved tolerance). The original **3.942 ms / 943.00 KB** and ≤1.00× absolute/ratio numbers remain the global/stretch reference.

**Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
- allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
- time improves beyond the pre-registered noise band and allocations do not increase.

A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.

**Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.

### Mandatory correctness and evidence
- No materialization/result change; LOB terminals (`ToStream`/`ToTextReader`) retain their locator/sequential-access behavior and tests.
- `CommandReaderOwner` dispose semantics remain: disposing the reader releases the per-call command and provider reader; the context stays open.
- Add a benchmark/gate row for the raw reader; run the streaming report on all relevant providers if the seam changes cross-provider.

## Constraints

- Do not remove LOB support or change `ToStream`/`ToTextReader` semantics.
- Do not require callers to use `Prepare()`; the measured path remains fresh-fluent.
- No benchmark-specific SQL or SQLite-only production hardcode; the seam choice must be dialect-driven.

## Related

- #188 — Tier-1 comparison benchmarks (streaming/raw reader scenario).
- #189 / #134 — `ToDataReader` SQLite support.
- Report: `docs/comparisons/feature-benchmarks-2026-10-17.md`.

### #245 — Perf: warm EXCEPT/INTERSECT allocations exceed linq2db (#240 follow-up)
## Context

The 2026-10-17 warm-path feature report (release 1.0.9-rc2, branch `1.0.9-rc3`, HEAD `881bf497`) shows EXCEPT/INTERSECT still above linq2db on allocations; time is much better:

| Scenario | Nextorm cached | linq2db | Nextorm / linq2db | Nextorm `Prepare()` control (per query) |
|---|---:|---:|---:|---:|
| EXCEPT | 6.38 KB/query (17.15 µs) | 5.05 KB/query (38.10 µs) | **1.26×** allocation | ~742 B (11.07 µs) |
| INTERSECT | 6.36 KB/query (16.79 µs) | 5.01 KB/query (36.77 µs) | **1.27×** allocation | ~727 B (10.36 µs) |

Source: `benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report.csv`; new report: `docs/comparisons/feature-benchmarks-2026-10-17.md`.

## Relationship to #240

#240 closed the construction-allocation follow-up to #201 with a partial landing and an owner-accepted residual: Except 1.2426× and Intersect 1.2511× remained above the 1.20× target. The new report measures 1.26×/1.27×. This ticket is the owner-requested rc3 follow-up for **set operations only**; it does not reopen the frozen D201-3 recipe or change the accepted threshold. The prepared controls are diagnostic only — the target remains the fresh-fluent cached path.

## Root cause

`Except`/`Intersect` construct a new first-operand clone and attach the second operand, then prepare and hash the combined tree:

- `src/nextorm.core/Query/QueryCommand.TResult.cs:1106-1131` — `Except`/`Intersect` do `(QueryCommand<TResult>)Clone()`, preserve `_from`, `ResetPreparation()`, then `SetOperation(...)`.
- `src/nextorm.core/Query/QueryCommand.cs:1164-1190` — `SetOperation` stores the second operand by reference and clones the tail only when chaining; the first-operand clone and its arrays/lists dominate for the two-operand case.
- `Clone()` copies the command’s joins/sorting/array-join/window/CTE metadata even when those collections are empty, and the fresh command still pays the per-call plan-hash/prepare path.

#240 evidence: construction alone is larger than LINQ to DB’s entire query cost; the prepared controls above (~727–742 B/query) show that SQL execution and row reading are not the gap.

## Proposed work

1. **Attribute the set-op path.** Add EXCEPT/INTERSECT phases to the existing decomposition probe (construct first operand, attach second, prepare/hash, execute) and publish B/op. Confirm how much is `Clone()` vs hash vs metadata.
2. **Avoid full first-operand cloning.** Introduce a copy-on-write/set-op view or a dedicated set-operation command that references the immutable first operand instead of cloning every collection. Keep public fluent semantics and ownership (the existing tail-clone fix must remain).
3. **Use empty-collection fast paths.** `Clone()`/`CopyTo` should not allocate/copy arrays and lists that are null/empty; apply the same to `ResetPreparation` paths for set operations.
4. **Reuse the prepared set-op shape.** Cache the immutable set-operation preparation descriptor keyed by both operand shapes, so a repeated fresh EXCEPT/INTERSECT does not re-hash and re-copy both operands. Do not cache mutable commands; preserve parameter/result semantics.
5. **Keep the frozen recipe and plan-cache key semantics unchanged.** No threshold change; no benchmark-specific shortcuts.

## Acceptance criteria (2026-10-20 two-tier policy)

**Global target (milestone-level):** EXCEPT/INTERSECT allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db on the same-round report. The original ≤ **5.05 KB/query** (EXCEPT) / ≤ **5.01 KB/query** (INTERSECT) parity numbers remain the stretch reference, not a per-task gate.

**Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
- allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
- time improves beyond the pre-registered noise band and allocations do not increase.

A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.

**Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.

### Mandatory correctness and evidence
- Time does not regress beyond the ShortRun noise band; SQL, result order/duplicates and public API unchanged.
- Prepared controls and `Warm_Reused = 0 B` unchanged; add both rows to the native allocation gate.
- Evidence includes attribution and final per-case BDN numbers; no averaged set-operation number.

## Constraints

- Preserve the #240 fix that prevents mutating a shared set-op tail (`a.Union(b).Except(c)`).
- Do not change the cache-key semantics or the frozen D201-3 recipe.
- General path only; no benchmark-specific SQL/type checks.

## Related

- #240 — predecessor construction-allocation scope and accepted Except/Intersect residual.
- #201 — cached-path parity goal (Category-B scope).
- #200 — earlier set-operation allocation work.
- Report: `docs/comparisons/feature-benchmarks-2026-10-17.md`.

### #359 — ClickHouse: group_array over a Tuple element renders Array(Nullable(Tuple`2)) — invalid SQL

title:	ClickHouse: group_array over a Tuple element renders Array(Nullable(Tuple`2)) — invalid SQL
state:	OPEN
labels:	bug
milestone:	1.0.9-rc5
number:	359

## Summary

A ClickHouse projection of `SqlFunctions.ClickHouse.group_array(Tuple.Create(...))` renders a cast whose
element type is the CLR name of the tuple with its generic arity (`Tuple`2`), so the emitted SQL is
invalid:

```sql
cast(groupArray(tuple(t1.`WeekNumber`, round(...))) as Array(Nullable(Tuple`2))) as `RetentionMatrix`
```

ClickHouse parses the backtick as the start of a quoted identifier and rejects the statement:

```
Code: 62. DB::Exception: Back quoted string is not closed: Syntax error:
failed at position 1172 (` desc) ...
```

## Impact

`examples/nextorm.examples.clickhouse.analytics` query 4 (`Retention`) aborts on the `1.0.9-rc4`
package — the only query in that example that projects an array **of tuples**. Any user projecting
`group_array`/`group_uniq_array`/`array_map`/… over a tuple element hits the same invalid SQL.

Regression vs `1.0.9-rc3`: the array-result normalisation (`MakeArrayResult`), which wraps an
array-returning call in `cast(... as Array(...))`, did not exist in rc3 (it arrived with #276/#336), so
rc3 emitted the bare `groupArray(...)` and the server inferred the type.

## Root cause

`src/nextorm.clickhouse/ClickHouseDialect.cs:266` — `MakeArrayElementType`:

```csharp
private string MakeArrayElementType(Type type)
{
    if (Nullable.GetUnderlyingType(type) is { IsValueType: true } underlying)
        return $"Nullable({MakeTypeName(underlying)})";
    if (type.IsArray)
        return $"Array({MakeArrayElementType(type.GetElementType()!)})";
    return type.IsValueType ? MakeTypeName(type) : $"Nullable({MakeTypeName(type)})";
}
```

Two problems on the reference-element arm:

1. it goes through `MakeTypeName`, whose open fall-back is `type.Name` (`SqlDialectBase.cs:534-546`), and
   a generic CLR name carries its arity — ``Tuple`2``;
2. ClickHouse does not allow `Nullable(Tuple(...))` (nor `Nullable(Array(...))`), so the `Nullable(...)`
   wrapper is wrong for tuple elements in the first place.

The file already has a tuple-aware renderer — `TypeName(Type)` (`ClickHouseDialect.cs:1356`, emits
`Tuple(UInt8, Float64)` and is used by the aggregate-result path) — but `MakeArrayElementType` does not
use it.

## Expected

```
cast(groupArray(tuple(...)) as Array(Tuple(UInt8, Float64)))
```

## Reproduction

`examples/nextorm.examples.clickhouse.analytics` → `ClickHouseQueries.Retention` at
`examples/nextorm.examples.clickhouse.analytics/ClickHouseQueries.cs:131`:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Debug -- --only Retention
```

The same shape can be asserted without a database by preparing
`ctx.From<IHit>().GroupBy(...).Select(g => new { M = SqlFunctions.ClickHouse.group_array(Tuple.Create(...)) })`
and reading `DbPreparedQueryCommand<T>.DbCommand.CommandText`.

## Acceptance

- `group_array` (and the other `MakeArrayResult` members) over a `Tuple<...>` element emits
  `Array(Tuple(<element types>))`, with no `Nullable(...)` wrapper and no CLR arity in the name.
- SQL-generation test in `tests/nextorm.clickhouse.tests` pinning the text; existing non-tuple array
  expectations (`Array(Nullable(String))` etc.) stay byte-identical.
- `examples/nextorm.examples.clickhouse.analytics` query 4 `Retention` runs green on the next package
  (it is excluded from the rc4 run because of this defect).

### #360 — Core: composite ORDER BY key — OrderBy(p => new { … }) concatenates the columns into invalid SQL instead of expanding them

title:	Core: composite ORDER BY key — OrderBy(p => new { … }) concatenates the columns into invalid SQL instead of expanding them
state:	OPEN
labels:	enhancement
milestone:	1.0.9-rc5
number:	360

## Summary

`OrderBy`/`OrderByDescending` accept `Expression<Func<TEntity, object?>>`, so a **composite** key selector compiles:

```csharp
.OrderBy(p => new { p.DelayChain.AirplaneCode, p.DelayChain.ScheduledDeparture })
```

but the engine stores it as **one** `Sorting` entry and renders it as one expression. The selector body is a
`MemberInit`; the column visitor walks the bindings and concatenates their SQL fragments without a
separator, so the statement is invalid and the failure is silent at compile time:

```sql
order by t1.idt2.id      -- measured: From<ISimpleEntity>().Join<IComplexEntity>().OrderBy(p => new { A = p.Item1.Id, B = p.Item2.Id })
```

Expected: `order by t1.id, t2.id`.

This is a feature request to give composite keys a real surface (one call → several sort keys) and to make
the unsupported shapes fail closed with a named message instead of emitting broken SQL.

## Current behaviour

* `EntityBuilder<TEntity>.OrderBy(Expression<Func<TEntity, object?>>, OrderDirection)` appends exactly one
  `Sorting` (`src/nextorm.core/Builders/EntityBuilder.cs:5172-5180`; `Sorting` carries a single expression).
* Rendering iterates the `Sorting[]` and calls `SqlSourceRenderer.MakeSort(...)` once per entry
  (`src/nextorm.core/DataContext/SqlBuilder.cs:1680-1700`), which visits the expression with a column
  visitor (`src/nextorm.core/DataContext/SqlSourceRenderer.cs:1460-1470`).
* There is no expansion of `MemberInit` / anonymous types / `Tuple.Create` / `ValueTuple` into several keys,
  and no diagnostic: the concatenated fragments produce syntactically valid-looking but semantically wrong
  SQL (or a provider-side syntax error, depending on the fragments).
* Workaround (documented, not a substitute): repeated `OrderBy`/`ThenBy` calls, which append tie-break keys
  (`docs/guide/06-sorting-and-paging.md:24`). `OrderBy(a).OrderBy(b)` and `OrderBy(a).ThenBy(b)` are the
  same request.

## Desired surface

Two acceptable designs; pick one in PLAN:

1. **Expand a composite selector** — `OrderBy(p => new { p.A, p.B })`, `OrderBy(p => (p.A, p.B))` and
   `OrderBy(p => Tuple.Create(p.A, p.B))` become N keys in declaration order, with the call's `direction`
   and `NullsPosition` applied to every key.
2. **Explicit multi-key overloads** — `OrderBy(params Expression<Func<TEntity, object?>>[] keys)` plus
   `ThenBy`/`OrderByDescending` twins, leaving the single-key selector untouched.

Whichever is chosen, a composite-shaped selector that cannot be expanded into plain scalar keys (for
example a method call returning an object, or `new SomeClass { A = …, B = … }` with a mapping the engine
cannot resolve) must throw a `NotSupportedException` naming the composite ORDER BY rule, **before** SQL is
emitted — never concatenate bindings.

## Acceptance

- `OrderBy(p => new { p.A, p.B })` (or the chosen surface) emits `order by t1.a, t2.b` on every provider;
  SQL is byte-identical to `OrderBy(p => p.A).ThenBy(p => p.B)`.
- Direction and `NullsPosition` apply to each expanded key; the ordinal (`OrderBy(int)`) and single-key
  expression paths stay byte-identical to today.
- Composite shapes that cannot be expanded fail closed with a named `NotSupportedException` and emit no SQL.
- SQL-generation tests for at least PostgreSQL and SQL Server (joined source, an alias chain, a CTE source)
  plus a Composite-key row in `docs/guide/06-sorting-and-paging.md` (EN + RU).
- `examples/nextorm.examples.postgres.aviasales/AviasalesQueries.cs` `AircraftDelayChains` can keep its two
  `OrderBy` calls (they remain idiomatic) — the feature is opt-in and does not rewrite existing examples.

## Context

Found while reviewing the named-join example conversion (#231) — the two-key ordering there is written as
two `OrderBy` calls, and the natural single-call form silently produces invalid SQL.

### #361 — Join aliases: a joined CTE descriptor produced by an aliased chain silently drops Alias.X (CS0117)

title:	Join aliases: a joined CTE descriptor produced by an aliased chain silently drops Alias.X (CS0117)
state:	OPEN
labels:	bug
milestone:	1.0.9-rc5
number:	361

## Summary

The join-alias generator silently drops an alias when the **joined source is a CTE/derived descriptor
built from a chain that itself carries aliases**:

```csharp
var supplierDelays = ctx.From<IPurchaseOrderDetail>()
    .WithAlias(Alias.OrderDetail)
    .Join<IPurchaseOrderHeader>(..., Alias.PurchaseOrder)
    .Join<IVendor>(..., Alias.Vendor)
    .Select(p => new SupplierDelayRow { … })
    .AsCte("SupplierDelays");

var rows = await ctx.From(productionImpact).WithAlias(Alias.ProductionImpact)
    .Join(supplierDelays, (pi, sd) => pi.ProductionImpact.ProductID == sd.ProductID, Alias.Supplier)
    //                                                                              ^ CS0117: Alias.Supplier
    …
```

`Alias.Supplier` is never emitted — the generator treats the join step as unresolvable and produces no
member and no diagnostic, so the failure surfaces only as `CS0117` at the call site.

Found by the examples conversion (#231): `examples/nextorm.examples.mssql.adventureworks`
`AdventureWorksQueries.SupplyChain`.

## Root cause

`JoinAliasGenerator.TryGetJoinedType` (`src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:772`)
resolves the joined element type **only from the semantic model**:

```csharp
if (semanticModel.GetTypeInfo(arguments[0].Expression, ct).Type is INamedTypeSymbol source
    && source.TypeArguments.Length == 1
    && (IsCteSource(source, semanticModel) || source.Name == "EntityBuilder")) { … }
```

On the generator's own pass the generated alias extensions do not exist yet, so the producer chain's
type is an **error type** and the branch never matches. The *root* path already has the #230 syntax
recovery (`ResolveQueryElementType`) for exactly this situation; the joined path had none.

A second, related gap: `ResolveQueryElementType` recognised only `Select` as the operator that
establishes the projected type, so a `SelectGroup` producer could not be recovered even through the
syntax walk.

## Fix (in this branch)

1. When the joined source's semantic type is missing or an error type, recover the element type through
   the existing `ResolveQueryElementType` walk and detect the descriptor kind (`AsCte` /
   `AsRecursiveCte`) syntactically.
2. Treat `SelectGroup` like `Select` in `ResolveQueryElementType` — it establishes the projected type
   from the same lambda shape.

No new diagnostic, no change for positional or unaliased shapes.

## Acceptance

- `Join(cte, …, Alias.X)` emits `Alias.X` when `cte` was produced by an aliased chain; the generated SQL
  is identical to the positional form.
- A `SelectGroup` producer can be consumed by an aliased chain.
- `tests/nextorm.alias.tests` stays green (172/172 before the fix); a regression test covers both cases.
- Examples: aliasing every `AdventureWorksQueries` chain (including `SupplyChain.supplierDelays`) builds
  with 0 warnings / 0 errors and the live run keeps 11/11.

## Groups

Clustering decided by `pdca-planner` after the ALL-barrier and persisted here (see §Decisions log): one group, `cap = 1` lane; the order below is the execution order inside that single lane.

| group | ordered units | shared surface | why one group | worktree | branch | ref | status |
|---|---|---|---|---|---|---|---|
| G1 | T353 → T352 → T318 → T317 → T302 → T245 → T250 → T246 → T247 → T311 → T359 → T360 → T361 → T268 → T269 → T267 → T270 → T271 → T272 → T273 → T274 → T319 | src/nextorm.core/Query/QueryCommand*.cs; src/nextorm.core/Query/QueryCommandExtensions.cs; src/nextorm.core/DataContext/QueryPlanner.cs; src/nextorm.core/DataContext/DataContext.cs; src/nextorm.core/DataContext/SqlBuilder.cs; src/nextorm.core/Query/Json/; src/nextorm.core/Builders/ (CteHoister.cs, MetadataEagerLoadBinding.cs); examples/VERIFICATION.md; examples/nextorm.examples.postgres.aviasales/Benchmarks.cs; examples/nextorm.examples.clickhouse.analytics/Benchmarks.cs; src/nextorm.clickhouse/ClickHouseDialect.cs; tests/nextorm.clickhouse.tests/; tests/nextorm.alias.tests/; tests/nextorm.core.tests/ | 16 of the 18 executable units form one transitive write-surface component (src/nextorm.core core query, planner and builder files plus examples/VERIFICATION.md and the example Benchmarks.cs); T353 shares src/nextorm.core/Builders/ with T319; every container and benchmark run is globally exclusive, so a second lane could verify nothing in parallel | — | 1.0.9-rc5 | — | pending |

## Integration branch

One group `G1` ⇒ **no merge barrier and no merge**. Work happens in the **current worktree** on **`1.0.9-rc5`**, which now carries the lane's commits (fast-forwarded to `46cd9785`, the tip carrying the `T245` step-1 CHECK record; the collection base stays `1e31331d`). `collection/rc5/group-1` still points at `46cd9785` but is **retired** — a stale label, never checked out again, never recreated, not an integration target. `git merge --no-ff` is never used for this collection; `git push` never runs (the user pushes), no tag, no release commit, nothing merged into `main`.

## Decisions log

- 2026-10-30T05:51:46Z · **Entry PLAN (form decision).** The entry `pdca-planner` returned the form `collection` with overlay `coder+.NET` (`pdca-coder` + `pdca-dotnet`), 19-item entry scope, criteria `AC-COL-01` … `AC-COL-04` plus one `AC-<issue>` per item, container revision `r = 1` and item revisions `r = 1` (`r = 2` for the five re-opened rc4 items). The complete returned report is persisted verbatim at `.pdca/rc5/entry-plan.md` (sha256 `ffaecc0b745db18d6af833398ea1b9a7f038a31ad36e57838a9ebc353f3f3f0f`, 54 333 bytes, 256 lines); this log entry references it and does not retell it.
- 2026-10-30T05:51:46Z · **Tooling substitution.** `.pdca/scripts/` does not contain `validate_inner_loop.py` or `pdca_evidence.py`; both are optional in `pdca-dotnet`, so this run uses the tooling that exists — `status_lint.py` (staged byte-identically from `pdca-collection/scripts/`, sha256 `3c08f33d699afc18310c8c6de5fc51c310a66f2e6568c1ae1e6b50ce6a2d7c71`) for the queue, and direct artifact paths under `.pdca/<task>/rv<k>/` for evidence. No validator run is claimed for the two absent helpers.
### Correction (caller-verified, 2026-10-30T05:55:26Z)

The preceding tooling note rested on a wrong caller fact; it is superseded here, not deleted. `validate_inner_loop.py` and `pdca_evidence.py` live in the installed .NET overlay package `pdca-dotnet`, not in the base `pdca` package: `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py` (sha256 `59ed8d885f7a811d5dd1ebde8b377c58e731d932714cc7c3663b271b45735946`) and `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/pdca_evidence.py` (sha256 `d3bb29dc853ab3e6b5f4fc7a3d98c4f50f3ec5771555a850c621057629d4a488`), byte-identical to the source skill and to the `opencode-config` snapshot. The earlier check looked only at `~/.config/opencode/skills/pdca/scripts/` (the base package) and never at `~/.config/opencode/skills/pdca-dotnet/scripts/`.

`.pdca/scripts/` legitimately lacks them at rest: `.gitignore:31` ignores `/.pdca/scripts/`, and the install contract is that the executor stages byte-identical copies **before the first helper run** (precedent: unit T307 in this repo recorded the same two hashes).

Therefore: **no substitution, no waiver, no deviation, no `Notice:`** about missing helpers. An unconditional mandated check cannot be waived; a fabricated deviation for a non-existent deficiency is worse than the deficiency.

Both helpers are **mandatory** when a unit's .NET inner-loop or evidence gate is due: stage them byte-identically from `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/` into `<repo>/.pdca/scripts/`, then run them from the project root (`python3 .pdca/scripts/validate_inner_loop.py brief|report|project|manifest …`, `python3 .pdca/scripts/pdca_evidence.py generate|lint …`). A helper is never run from the installed skill directory.

**Staged this dispatch (2026-10-30T05:55:26Z):** byte-identical copies into `/home/alex/sources/nextorm/.pdca/scripts/`, verified with `sha256sum` and `cmp` on source and copy — `validate_inner_loop.py`: source `59ed8d885f7a811d5dd1ebde8b377c58e731d932714cc7c3663b271b45735946` = copy `59ed8d885f7a811d5dd1ebde8b377c58e731d932714cc7c3663b271b45735946`; `pdca_evidence.py`: source `d3bb29dc853ab3e6b5f4fc7a3d98c4f50f3ec5771555a850c621057629d4a488` = copy `d3bb29dc853ab3e6b5f4fc7a3d98c4f50f3ec5771555a850c621057629d4a488`.

no plan revision bump, no new attempt, no defect entry — plan unchanged.

- 2026-10-30T05:51:46Z · **Containers available.** Containers are available with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` (Podman Engine 5.8.6, verified by the caller); the integration suite is fail-closed and `NEXTORM_SKIP_PROVIDERS` is never used as evidence of a passing provider run.
- 2026-10-30T06:38:24Z · **Parallel preparation ceiling.** `plan_cap = 4` for the initial per-item PLAN/preparation dispatches, concurrent up to `min(#items without a ready plan, plan_cap)` and bounded by observed host capacity; reason: `pdca-collection` §P step 2 «Parallel preparation» scheduling choice (the host exposes no such key), 19 independent items, planner calls are read-only and independent — planning is the only parallel phase. All writes to this collection status and to every item status stay serialized through `pdca-executor`; the ALL-barrier still gates lane allocation and every DO; all timing/benchmark/container-backed integration execution stays strictly one process at a time. `plan_cap` is lowered if a provider rate-limit or a failed call is observed, and the new value + reason are recorded the same way.

- 2026-10-30T07:38:03Z · **Post-barrier schedule and lane allocation (`pdca-planner`).** Exactly one group `G1`, execution order `T353 → T352 → T318 → T317 → T319 → T245 → T250 → T246 → T247 → T311 → T268 → T269 → T267 → T270 → T271 → T272 → T273 → T274` (§Groups). Clustering reason: 16 of the 18 executable units form one transitive write-surface component (`src/nextorm.core/**` — `QueryCommand*.cs`, `QueryPlanner.cs`, `DataContext.cs`, `QueryCommandExtensions.cs`, `Query/Json/**`, `CteHoister.cs`, `SqlBuilder.cs` — plus `examples/VERIFICATION.md` and the example `Benchmarks.cs`), `T353` shares `src/nextorm.core/Builders/**` with `T319`, and every container/benchmark run is globally exclusive, so a second lane could verify nothing in parallel. Lane count `cap = 1` for exactly that reason (serialized write surface plus global exclusivity of container and benchmark execution). DAG (hard ordering): `T318 → T319`, `T317 → T319`, `T318 → T245`, `T245 → T250`, `T250 → T246`, `T246 → T247`, `T247 → T311`, `T319 → T268`, `T268 → T269 → T267`, `T267 → T270 → T271 → T272 → T273 → T274`. Lane allocated: `collection/rc5/group-1` in the **current worktree** (one group ⇒ no separate worktree), created from `1.0.9-rc5` at `1e31331d`; nothing merged into `1.0.9-rc5` or `main`, no push. `T302` excluded as terminal: `infeasible` at `r=1`, `attempts=0` — no lane, no group, no execution. `T319` is gated on `T317` and `T318` both `done`. PLAN revision vector unchanged — this decision introduces no item revision (each item keeps the revision already persisted: `r=1`, and `r=2` for `T247`/`T311`); `T302` terminal at `r=1`, `attempts=0`; no counter, attempt or defect entry changed.

- 2026-10-30T11:49:36Z · **Canonical `status_lint` v3 staged (bookkeeping, unchanged record).** `.pdca/scripts/status_lint.py` was the v2 copy (sha256 `3c08f33d699afc18310c8c6de5fc51c310a66f2e6568c1ae1e6b50ce6a2d7c71`, `VERSION = 2`); the canonical source `pdca-collection/scripts/status_lint.py` (sha256 `fa999808475ca9699d2682161c2c7136a3692cb99f02b0eaf3774a1f6d2b350f`, `VERSION = 3`, `status-lint 3`) was staged over it byte-identically: source = copy = `fa999808475ca9699d2682161c2c7136a3692cb99f02b0eaf3774a1f6d2b350f`, `cmp -s` rc 0. v3 additionally requires `plan_cap`/`lane_cap` in the header for `form: collection` plus a `cap_reason` for any value below the contract default 4, so the header now records `plan_cap = 3` (the §P step 2 parallel-preparation ceiling actually used: started at 4, lowered to 3 after one planner call failed in transport, `T246`, retried on the same PLAN) and `lane_cap = 1` (post-barrier clustering: `G1`, one group — 16 of 18 executable items are one transitive write-surface component and every container/benchmark run is globally exclusive, so a second lane could verify nothing). No queue row, criterion, plan revision, attempt, counter or defect entry changed.

- 2026-10-30T12:53:00Z · **`T245` Step 1 baseline recorded (measurement only, no product change).** On the frozen identity `HEAD cb2246e9` (`collection/rc5/group-1`, `src`/`tests` clean) the dispatch's three BDN runs plus the existing `--t245-attribution` probe ran one process at a time (all `rc=0`): EXCEPT `B_Nextorm_Cached_Except` `7 112.1 B/query` (71 121 B/op ÷ 10) vs `B_Linq2Db_Except` `5 176.2` ⇒ `1.374×` allocated, time `0.475×`; INTERSECT `B_Nextorm_Cached_Intersect` `7 096.1` vs `5 128.2` ⇒ `1.384×`, time `0.477×`; `Prepare()` controls `760.0`/`744.0 B/query`; ladder `Construct 4 184.18 → Prepare_NoHash 5 816.26 → Prepare_Hash 6 608.31 → Warm_PlanOnly 6 432.31/6 432.28 → Warm_Reused 0.00` B/query; probe composition per query EXCEPT/INTERSECT = operands `3 712`, clone `872`, prepare/hash `2 248`, execute `808`/`792`, closure residual `0`. Both same-round ratios sit above the `1.20×` target of `AC-245-01` — recorded, not adjudicated (verdict belongs to the Step 1 CHECK). Run A's filter did **not** yield the `Prepare()` controls or `*_Warm_Reused` the dispatch expected (they live in `SqliteBenchmarkFeaturesFair` / `SqliteBenchmarkWarmDecompose`); a 2-arm control run was added and nothing else. No attempt consumed, no criterion changed, no plan revision, no defect entry, zero product edits; item record `.pdca/status/T245-warm-set-ops-3.md` created with §Baseline record. Committed per this dispatch: the item record and this container status only (the container status is otherwise not committed by the campaign's policy). | `.pdca/status/T245-warm-set-ops-3.md` §Baseline record; `.pdca/rc5/items/T245/rv3/baseline/summary.md`; queue row `T245` unchanged

- 2026-10-30T13:05:34Z · **Branch change directed by the user; the lane moved to `1.0.9-rc5` (ff-only).** The user directed that the lane's commits move from `collection/rc5/group-1` to `1.0.9-rc5`; this dispatch fast-forwarded the release branch to the lane tip and switched the worktree onto it (`git branch -f 1.0.9-rc5 46cd9785`, then `git switch 1.0.9-rc5`) under an ff-only precondition check — `git merge-base --is-ancestor 1.0.9-rc5 46cd9785` rc 0, `git diff --stat 1.0.9-rc5..46cd9785` exactly the two status files, nothing staged — after the caller's premise was **refuted** on disk (the caller reported the lane already fast-forwarded to `c7f4442d` and `1.0.9-rc5` checked out; the actual lane tip was `46cd9785`, one commit ahead of `c7f4442d`). Nothing was rewritten, reset or lost: HEAD `46cd9785` before and after, both status-file sha256 values unchanged, the 14-path `.pdca/status/*` dirty set unchanged and still uncommitted. `collection/rc5/group-1` is retired but kept as a label at `46cd9785` (never checked out again, never recreated, not an integration target). No merge barrier (one group `G1`), no `git merge --no-ff`, no push, no tag. `plan_cap = 3`, `lane_cap = 1`, the lane order, all counters, the evidence roots, the paused `T319` and the pending `#359`/`#360`/`#361` scope extension are unchanged. This changes no criterion, no plan revision, no attempt, no defect entry and no product file.
- 2026-10-30T13:13:20Z · **Container revision `r = 1 → r = 2` (entry scope 19 → 22).** The user added `#359`, `#360`, `#361` to milestone `1.0.9-rc5` (#23); the single configured `pdca-planner` decided the revision — form `collection` and overlay `coder+.NET` unchanged, the form rationale carried forward from `r = 1` — with entry scope **22 tasks** (`#361`, `#360`, `#359`, `#353`, `#352`, `#319`, `#318`, `#317`, `#311`, `#302`, `#274`–`#267`, `#250`, `#247`, `#246`, `#245`: 20 open at revision time plus the 2 already closed by this collection) and the requested = delivered result unchanged in kind (verified execution and accounting of every milestone task on `1.0.9-rc5` under the collection contract).
  - Criteria, verbatim from `.pdca/rc5/entry-plan-r2.md` §1 (`AC-COL-01` **re-issued** by the planner; `AC-COL-02`/`AC-COL-03`/`AC-COL-04` **unchanged** with the planner's stated reasons):
    ```text
    - **`AC-COL-01` (Полнота учёта очереди и дословная фиксация входов)**: *Принимаемый исход*: Все 22 задачи майлстона `1.0.9-rc5` (`#361`, `#360`, `#359`, `#353`, `#352`, `#319`, `#318`, `#317`, `#311`, `#302`, `#274`–`#267`, `#250`, `#247`, `#246`, `#245`, включая 2 уже закрытые этой коллекцией `#353`, `#352` и 20 открытых) зарегистрированы в `.pdca/status/collection-rc5.md` со стандартной 15-колоночной таблицей очереди и обязательными секциями контейнера, а полный исходный текст каждой из 22 задач (из `/tmp/nextorm-rc5-issues.md` для исходных 19 задач и дословных карточек GitHub Issues `#359`, `#360`, `#361` в снимке входов коллекции / записях элементов) сохранён без сокращений. *Негативное условие*: хотя бы одна из 22 задач отсутствует в очереди, её исходный текст усечён или переписан, либо задача перенесена в другой майлстон / создан дубликат в будущем майлстоне.
    - **`AC-COL-02`**: **unchanged** — общерепозиторные гейты сборки (`0W/0E`), юнит-/диалектных/конвенционных/контейнерных интеграционных тестов (без `NEXTORM_SKIP_PROVIDERS`), приёмочного набора (7 кейсов ≤ 4 мин, 0 падений) и порогов покрытия (`85/75` и `65/74`) действуют без изменений для расширенного до 22 задач набора.
    - **`AC-COL-03`**: **unchanged по составу обязательств автокоммита и гигиены Git/GitHub, с фиксацией входного ограничения пользователя по ветке** — коммиты линии `G1` выполняются напрямую в ветку `1.0.9-rc5` (метка `collection/rc5/group-1` выведена из обращения на `46cd9785`, барьер `git merge --no-ff` снят решением пользователя и не выполняется); все остальные требования `AC-COL-03` (автокоммит только затронутых файлов с `#N` и указателем на доказательства, закрытие выполненных задач с итоговым комментарием, сохранение незавершённых задач в `OPEN` в `1.0.9-rc5`, запрет `push`/`force-push`/слияния в `main`/тегов/захвата чужих грязных файлов) остаются в силе без изменений.
    - **`AC-COL-04`**: **unchanged** — правило открытия пяти переформулированных из `rc4` задач (`#245`, `#246`, `#247`, `#250`, `#311`) на ревизии `r+1` с оценкой строго по тексту `rc5` не затрагивается добавлением `#359`, `#360`, `#361`.
    ```
  - Three new item plans, each `form: cycle` with `skill: pdca-dotnet` (`coder+.NET`), identity `N=1`, `r=1`, `n=1/3`, `plan_state=ready`, no DO started, no attempt consumed:
    - `T359` — issue `#359` (ClickHouse `Array(Tuple(...))` in `MakeArrayElementType`), criteria `AC-T359-01`..`02`, evidence root `.pdca/rc5/items/T359/rv1/`, status `.pdca/status/T359-rc5-1.md`.
    - `T360` — issue `#360` (core composite `OrderBy` selector), criteria `AC-T360-01`..`02`, evidence root `.pdca/rc5/items/T360/rv1/`, status `.pdca/status/T360-rc5-1.md`.
    - `T361` — issue `#361` (regression tests for the `JoinAliasGenerator` CTE-descriptor / `SelectGroup` cases on top of the user's commit `c8eed046`), criteria `AC-T361-01`..`02`, evidence root `.pdca/rc5/items/T361/rv1/`, status `.pdca/status/T361-rc5-1.md`.
  - New `G1` execution order (§Groups): `T353 → T352 → T318 → T317 → T302 → T245 → T250 → T246 → T247 → T311 → T359 → T360 → T361 → T268 → T269 → T267 → T270 → T271 → T272 → T273 → T274 → T319` — the three new units inserted as one block after `T311` and before the examples block `T268`…`T274`, the relative order of all 19 existing units unchanged.
  - New dependency edge `T359 → T271` (the ClickHouse `Retention` example cannot run green until `Array(Tuple(...))` renders; `T359` therefore runs before the whole examples block). `T360` is opt-in and blocks nothing; `T361` is test-only.
  - Schedule-revision classification: **compatible local** (persisted revision binding) — no stop and no re-approval; grounds: the scope extension was made by the user inside autonomous mode, no accepted/terminal/in-flight unit is cancelled, restarted or moved, and every pending unit keeps its relative order while the new edge runs forward along `G1`.
  - Revision-binding line, verbatim:
    ```text
    schedule_revision: r=2 (compatible-local; base_revision=r1; active_item=T245@N3.r1.n1 untouched; scope=19->22 [+T359,+T360,+T361]; edges=[T359->T271]; order=T353->T352->T318->T317->T302->T245->T250->T246->T247->T311->T359->T360->T361->T268->T269->T267->T270->T271->T272->T273->T274->T319; branch=1.0.9-rc5 direct, group-branch merge superseded)
    ```
  - Complete returned report: `.pdca/rc5/entry-plan-r2.md` (verbatim planner report, sha256 `0283edbe19341e172e61660c3a041eb7e52075a0d17175f6d1835b534574ace4`, 30 744 bytes, 146 lines) — this entry references it and does not retell it.
  - This changes no existing item's status, counters, plan revision or attempt (`T245` stays `N=3 r=1 n=1/3` mid-item, the three plan-terminal resolutions and the paused `T319` are untouched) and no product file.

- 2026-10-30T15:24:24Z · **Lane `G1` two-tier performance acceptance & retention rule recorded (user instruction verbatim); `pdca-incremental` loaded; no item revision changes.** Recorded by `pdca-executor` on the `pdca-planner` decision for item `T245` (lane `G1`, campaign, step 2, `N=3 r=3 n=1/3`). Nothing in the queue, in any counter (`N`/`r`/`n`), in any attempt or in any criterion text changed: this entry records an **operational clarification** of the already-supplied two-tier rule plus the contract load, and it is the source of the per-item recording rule stated below.
  - **Contract load.** `pdca-incremental` is loaded for this lane at this turn, after the gap in `pdca-collection` §P line 22 (that section never names the campaign container of an item that is itself an incremental campaign). It now governs `T245` and, later, `T319`; base `pdca` still governs the shared rules (counters, status skeleton, phase contracts, evidence discipline) and `pdca-collection` still governs this container. `T245`'s record keeps the base skeleton it has carried since `r=1`; the incremental container sections are mapped **additively** in `.pdca/status/T245-warm-set-ops-3.md` §`Campaign state (pdca-incremental bridge)`, with the shape divergence recorded there as a fidelity limitation and no history rewritten.
  - **Scope.** All `G1` performance items: `T245`, `T246`, `T247`, `T250`, `T311`, `T267`–`T274`. The non-performance items `T359`, `T360`, `T361` are **unaffected** (no performance predicate, no retention rule).
  - **User instruction (verbatim):** `«чек не должен отклонять полностью задачу если есть хоть какое-то улучшение с точки зрения производительности. т.е. если мы не досигли целевых 1.2, но улучшили аллокации и при этом не потеряли в скорости - это принимается (после 3-х попыток). Т.е. мы не теряем результаты тюнинга перформанса»`
  - **Amendment block (verbatim, as `pdca-planner` returned it; persisted at `.pdca/rc5/items/T245/amendment-r3.md`, sha256 `ca2a7e0f4f6b11bb93dd99520e2ad73c7119788d92c0ca4c6ff9ac506b05d349`, 7282 bytes, attaching to plan revision `r=3` — there is no `r=4` and the criteria text is unchanged):**

````text
### Amendment — Lane G1 Two-Tier Performance Acceptance & Retention Rule (user instruction, 2026-10-30)

**User instruction (verbatim):**
> «чек не должен отклонять полностью задачу если есть хоть какое-то улучшение с точки зрения производительности. т.е. если мы не досигли целевых 1.2, но улучшили аллокации и при этом не потеряли в скорости - это принимается (после 3-х попыток). Т.е. мы не теряем результаты тюнинга перформанса»

**Normative rules for `pdca-planner`, `pdca-executor` (DO), and `pdca-check` (CHECK) across all `G1` performance items:**

1. **Mandatory correctness and invariant gates (hard, non-waivable):**
   - Build passes with `0` warnings and `0` errors (`TreatWarningsAsErrors=true`).
   - All required unit, dialect, convention, and container-backed integration test suites pass.
   - Generated SQL, result set/ordering/duplicates, public API, cache-key semantics, and task-specific invariants (for `T245`: the `#240` shared set-op tail immutability fix, frozen `D201-3` recipe, `Warm_Reused == 0 B`, and non-regressing `Prepare()` controls) remain intact.
   - Any candidate that violates any correctness, API, SQL, or invariant gate is **rejected and reverted**.

2. **Two-tier performance predicate (target vs. floor):**
   - **Tier A — Global target (`AC-*-01`, stretch target, not an acceptance veto):** reaching the milestone ratio target (for `T245`: `Nextorm_Allocated_B / linq2db_Allocated_B ≤ 1.20` and `Nextorm_Mean_us / linq2db_Mean_us ≤ 1.00` on the same-round paired report) satisfies the performance predicate immediately on any attempt `n ∈ {1, 2, 3}` when all mandatory gates in (1) pass.
   - **Tier B — Local acceptance (`AC-*-02`, credit floor, task closure):** a candidate that does not reach Tier A satisfies the performance predicate if and only if, vs. the same-round paired baseline under the fixed measurement method:
     - **(i) Allocation-axis win:** allocations strictly decrease by at least the 1-byte floor (`ΔAllocated_B ≤ −1 B/query`, judged by exact byte counts on the warm cached path) **and** mean execution time does not worsen beyond the pre-registered noise band (`±20 %` ShortRun on this WSL/Linux host with paired ABBA replicate-interval overlap); **or**
     - **(ii) Time-axis win:** mean execution time improves beyond the pre-registered noise band **and** allocations do not increase (`ΔAllocated_B ≤ 0 B/query`, exact bytes).

3. **Attempt budget (`n/3`) and monotonic retention:**
   - Achieving a Tier B local win on attempt `n < 3` **retains the winning rung as the monotonic baseline (`R*`)** and **does not stop attempts `n+1..3`** while plausible in-scope levers remain.
   - A Tier B win is **accepted for task/campaign closure (`met` under `AC-*-02`)** when either:
     - the attempt budget `n = 3/3` is exhausted (or all planned in-scope mechanisms in the ladder have been measured and non-improving mechanisms reverted back to the best retained rung `R*`), **or**
     - no further in-scope mechanism exists without violating task constraints.
   - A candidate mechanism that improves neither axis (`ΔAllocated_B == 0 B` and time within noise), or improves one axis while regressing the other beyond its band/floor, is **not credited** and is **reverted back to the prior retained rung `R*`**, never reverting an earlier retained win.

4. **Disposition on acceptance (keep, commit, and close with residual gap):**
   - When a candidate is accepted under Tier A or Tier B, the winning code changes and locked native allocation gate tests **must be kept and committed** (under the collection's auto-commit authorization) — **never reverted to leave a clean tree**.
   - The status record and `gh issue close` comment must record:
     1. Exact same-round paired per-case BDN and native counter numbers (baseline vs. retained candidate, `ΔAllocated_B` in exact bytes, `ΔMean_us` and `%`, ABBA noise verdict, `Warm_Reused = 0 B`, and `Prepare()` controls).
     2. Attribution of the saved bytes/time and why discarded mechanisms yielded `0 B` or were infeasible under constraints.
     3. The exact **residual gap to the Tier A global target** (`1.20×` / `1.00×`).
````

  - **Per-item recording rule.** When an item's frozen plan already carries `AC-*-02` verbatim, the amendment is recorded **inline** as a `### Amendment (2026-10-30)` block at that item's **current revision** — no revision bump, no attempt consumed, no criterion text touched. A genuine `r+1` at that item's turn is allowed **only** if that item's frozen plan omitted or contradicted `AC-*-02` (the `T245-S2-F1` failure mode); otherwise a revision is forbidden. `T245` is the first item: its amendment is recorded inline at `r=3`, and the `T245` status record points at it from §PLAN record `### r3`.
  - **Threshold provenance and the one executed supersede** are recorded in `.pdca/rc5/items/T245/amendment-r3.md` §Threshold provenance and §Supersede record (`1.20×`/`1.00×` and the `−1 B/query` floor stand as user-supplied; the `8 B/query` floor and the `1.00×` allocation rewrite of `r=2` are void permanently; the `±20 %` band stands and is now baseline-backed; the native gate ceilings move from the placeholders `9000`/`1400` to the measured-backed `7530`/`1090`/`0`).

- 2026-10-30T15:28:53Z · **Amendment scoping clarification (user, verbatim).** The user supplied a scoping clarification of the 2026-10-30 Lane `G1` two-tier acceptance & retention amendment recorded above (`.pdca/rc5/items/T245/amendment-r3.md` §`Amendment — Lane G1 Two-Tier Performance Acceptance & Retention Rule`). It is recorded here **verbatim and unaltered** — the driver issues no wording of its own:

````text
Clarification of the amendment I just sent — scoping only, no new work.

The 3-attempt budget is **per step and per contract revision**, not per campaign and not per item:
- `n/3` counts DO attempts on the current `r` of the current step (base §State machine and counters: `PLAN(r) → DO` starts at 1, `CHECK → DO` increments, a new revision resets `n` to 1 and never erases the defect history).
- The campaign's **number of steps is not capped**: each accepted step advances the basis and the planner's step-boundary decision opens the next step or a STOP. The campaign ends only at a whole-goal `met` or an allowed stop verdict (`limited`/`unreachable`/`blocked`/`stopped`) — never because a step counter ran out.
- Therefore the partial-win acceptance applies **at step level**: a step whose ratio target is unreached but which yields a real one-axis win with no regression is credited and committed when that step's attempt budget is spent — it does not end the campaign, and it does not prevent the next step from attacking the residual.

Practical note for the current record: `T245` stands at `r=3 n=1/3`, and its `r` grew through `CHECK → PLAN` revisions rather than through DO attempts — so its attempt budget is not spent; the partial-win verdict applies only when the DO attempts on the accepted contract are exhausted. Do not treat `r` growth as "attempts used", and do not let a revision restart act as a way to avoid the verdict: if the contract is being revised only to avoid accepting a measured win, that is a defect for CHECK.
````

this is the user's own scoping of the 2026-10-30 Lane G1 two-tier amendment; the driver issues no wording of its own and changes no threshold; amendment rule 3 is read subject to this scoping; no plan revision, no attempt, no counter, no defect entry changes.

- 2026-10-30T (ACT finalization, T245) · **User-directed continuation of lane G1 past the incomplete T245 (verbatim instruction, recorded as the user's decision, not a contract default).** The user directed: «Then continue the lane: `T250 → T246 → T247 → T311 → T359 → T360 → T361 → T268 → T269 → T267 → T270 → T271 → T272 → T273 → T274`». This continues the `G1` lane past an `incomplete` item (`T245`, stop verdict `stopped`, not a product failure — the tree is regression-free with all gates green), and is an explicit user schedule decision superseding the collection's group-stop default for this item; the remaining items keep their own statuses; no item is marked `incomplete` by the group-stop rule. The DAG edge `T245 → T250` is therefore crossed without an accepted `T245` basis, and `T250`'s own stale-plan applicability check applies at its DO. The planner's boundary decision is preserved verbatim in the item record (`.pdca/status/T245-warm-set-ops-3.md`). No counter, plan revision, attempt or criterion changed by this entry. | `.pdca/status/T245-warm-set-ops-3.md`; queue row `T245`

- 2026-10-30T19:00:00Z · **Process amendment — Evidence-round cap (user, lane G1, effective immediately).** User process amendment (lane-wide, effective immediately); no plan revision, no counter, no attempt, no defect entry, no criterion and no product file changes.

- 2026-10-30T (ACT, T245) · **User-accepted `limited` outcome («п1»).** The user chose option 1 (accept Tier B with residual, no further evidence round) over a targeted re-run or leaving it stopped. Recorded verbatim as `«п1» = accept Tier B locally with the recorded residual, no additional evidence round`. No criterion was rewritten: `AC-245-01` stays `unmet` (1.347719×/1.357154×), `AC-245-03` stays `unverified`; `AC-245-02` accepted under Tier B; `AC-245-05/06/07` met. Accepted scope: mechanisms M1 (bc641498, −464…−544 B/query) and R2 (c3294de9, −72.00 B/query on both operators, all four pairings); locked native gate 7 530/1 090/0; fail-closed integration gate green (7 934/7 733/0/201); residual to the 1.20× target 764.68/805.98 B/query; parity remainder (≈1 936/1 968 B/query) owned by #319 (prepared, paused). Both candidates stay committed; nothing reverted; no further evidence round; n=3/3 remains spent, no attempt added. Queue row T245: status incomplete → done (accepted within the recorded limited scope only); terminal cell set to execution because status_lint TERMINAL enum has no limited value (limited kept in notes and in the item record). Issue #245 closed as limited. | .pdca/status/T245-warm-set-ops-3.md; .pdca/status/collection-rc5.md row T245

  Verbatim rule (user authorization «п1 - ок, делай»):
  ````markdown
  ### Process amendment — Evidence-round cap (user, lane G1, effective immediately)

  **User authorization (verbatim):** «п1 - ок, делай»

  **Rule — one evidence round per criterion per revision.**

  1. A `CHECK → DO` return whose **sole** ground is missing evidence/verification for a criterion grants that criterion exactly **one** additional DO round, recorded as `evidence_round=1` in the item record. Per the base contract that round is not a defect and **does not consume an attempt** (`n/3` unchanged) — that part stays exactly as it is.
  2. If the criterion is still not verifiable from the artefacts on disk after that round, CHECK must return a verdict on what exists — `unverified` where evidence is missing, never silently `met` — and the driver routes it: a product defect → a counted DO attempt; a wrong/unachievable contract → `CHECK → PLAN`; otherwise → `pdca-escalate` or the planner's step-boundary decision (STOP / next step).
  3. **No second evidence round on the same criterion within the same revision.** A genuinely new criterion, or a new artefact requirement created by an accepted revision, opens its own single round.
  4. The rule targets the observed failure mode: `unverified` is not an attempt, so an unbounded "gather more evidence" loop can run without ever charging `n/3` — `T245` spent roughly an hour in exactly that loop after `n=3/3` was already exhausted.

  **Scope:** every item of lane `G1` (`T250`, `T246`, `T247`, `T311`, `T359`, `T360`, `T361`, `T268`, `T269`, `T267`, `T270`–`T274`, and any later item). It is a process constraint on phase routing, **not** a criteria change: no threshold, tolerance, denominator or measurement method moves, and it neither widens nor narrows any user-supplied criterion. Every subsequent `pdca-check` dispatch carries this rule in its inline contract.
  ````

## General verification and recovery

- common verification state: `unverified` — no integration CHECK has run yet; there is no `C` result and no evidence pointer.
- blocking reason: none recorded (the state is `unverified`, not `blocked`).
- defect id: none — no defect history yet.
- linked corrective cycle: none.
- next allowed step: per-item PLANs → ALL-barrier.
- evidence-round cap (user, lane G1, effective immediately): one evidence round per criterion per revision, recorded as evidence_round=1, not a defect and not an attempt; a second evidence round on the same criterion within the same revision is forbidden — CHECK then verdicts on the artefacts on disk (unverified where missing, never silently met) and the driver routes to a counted DO attempt / CHECK → PLAN / pdca-escalate / the planner's step-boundary decision. Full text: §Decisions log entry of the same date.

## Done / Verified / Incomplete

- **Accepted goals: 1 of 22, as a `limited` acceptance (not `met`)** — `T245` (#245) accepted under Tier B with residual: mechanisms M1 (bc641498) + R2 (c3294de9) retained and committed, locked native gate 7 530/1 090/0, fail-closed integration gate 7 934/7 733/0/201, residual to the 1.20× target 764.68/805.98 B/query; caveat — `AC-245-01` remains `unmet` (1.347719×/1.357154×) and `AC-245-03` remains `unverified`; issue #245 closed as `limited`, not `met`. The other 21 items are unchanged.
- **Plan-terminal resolutions: 3** of 22 — `T302` = `infeasible` (PLAN-terminal, `attempts=0`, plan_state never `ready`, no DO, no CHECK; issue `#302` stays OPEN); `T318` = `infeasible` (PLAN-terminal after DO attempt 1: the scoped empty-collection copying is already absent, `0 B` measured delta on the clone-heavy arms, candidate reverted, no CHECK, no whole-goal acceptance; the queue's `attempts` cell reads `0` per the lint schema and the item record carries the real count of 1; issue `#318` stays OPEN); and `T317` = `infeasible` (DO → ACT with an expert feasibility decision after DO attempts 1 and 2: attempt 2 passed every gate — build 0W/0E, core 3892/3892, dialects 7/7, integration 7933/7732/0/201, acceptance 7/7 — but violated `AC-317-02` on 15 of 17 nextorm arms and showed no `AC-317-01` reduction above the host noise floor, and the routed strong-tier `pdca-escalate` decision established that a per-`QueryCommand` memo is written and never read on fresh-fluent calls, so attempt `3/3` was not run and no further attempt is consumed; the queue's `attempts` cell reads `0` per the lint schema and the item record carries the real count of 2; residual `qplan_hash` / `CaptureRawShape`·`Fingerprint` transferred to `T319` and `T245`; issue `#317` stays OPEN).
- **Execution failures: 0** — `T245` is no longer an execution failure: it was reclassified by the user's explicit decision («п1») as a `limited` acceptance (queue row `T245` = `done`, terminal cell `execution` per the lint enum, `limited` kept in notes and the item record) with the residual 764.68/805.98 B/query; the earlier `stopped`/budget-exhausted record is preserved verbatim in `.pdca/status/T245-warm-set-ops-3.md` (§ACT record) and was not rewritten. The three earlier `incomplete` rows (`T302`, `T318`, `T317`) remain PLAN-/DO-terminal feasibility resolutions, not execution failures.
- **Collection completeness: none yet** — no original item goal is accepted.
- **Integrated-tree verdict: none yet** — no independent integration CHECK has run.

## Progress log

append-only, one row per event, never edited retroactively:

2026-10-30T06:38:14Z | P | revision r=1 | attempt n=0 | Recovered after interruption: a host restart killed the previous driver session `ses_e74a9b803ffebtRyA9WyHbkdmI`; the new driver continues the SAME collection `rc5` (no new collection id, no second status file, no second entry PLAN). State as read from this file at recovery: container revision `r = 1`; 19 queue items (`T353`,`T352`,`T318`,`T317`,`T245`,`T246`,`T247`,`T250`,`T311`,`T267`…`T274`,`T302`,`T319`) all `plan_state = pending`, `status = pending`; 0 of 19 prepared; `## Groups` and `## Integration branch` still placeholders; ALL-barrier not passed; no item DO has started; no `collection/rc5/*` branch and no rc5 worktree. No counter change (`N` and `r` unchanged), no plan revision bump, no attempt consumed, no defect entry. Containers available (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, Podman 5.8.6). Next allowed step: per-item PLANs → ALL-barrier. Resuming `pdca-collection` §P step 2. | `.pdca/status/collection-rc5.md` (state as read); entry plan `.pdca/rc5/entry-plan.md`
2026-10-30T06:45:06Z | P | revision r=1 | attempt n=0 | Evidence root superseded for every item of this collection: `.pdca/<item-id>/rv<k>/` → `.pdca/rc5/items/<item-id>/rv<k>/` (collection-scoped, `attempts/` beneath it; `T353` and `T352` updated now). Reason: item ids are reused from collection `rc4`, so the default per-item root is not isolated — `.pdca/T352/rv1/` holds foreign issue-`#296` artifacts dated 2026-10-28 (`checkpoint.md`, `evidence.json`, `manifest.json`, `integration.log.plain`, `plan-lint.json`, `inputs/`, `review/`) and `.pdca/T353/rv1/` likewise holds 2026-10-28 foreign artifacts, so a scoped CHECK read of the old root would read foreign evidence; the collection id is unique, the item id is not. Previous values stay recorded in the two item records as references only; `.pdca/T352/**` and `.pdca/T353/**` are untouched. No criterion, plan revision, attempt, counter or defect entry changed. Notice (autonomous) for the final report. | `.pdca/rc5/items/T353/rv1/attempts/`, `.pdca/rc5/items/T352/rv1/attempts/` (directories only); item records `.pdca/status/T353-rc5-1.md`, `.pdca/status/T352-rc5-1.md` (§Current state + §Progress log); queue rows `T353`/`T352` of this file
2026-10-30T07:00:53Z | P | revision r=1 | attempt n=0 | T245 campaign preparation persisted (form=incremental, preparation_state=ready, preparation_ref .pdca/rc5/items/T245/preparation.md@r1, N=3 r=1 n=1/3, status_path .pdca/status/T245-warm-set-ops-3.md) — barrier-ready, no runtime status, no baseline, no DO | `.pdca/rc5/items/T245/preparation.md` (immutable charter r1); queue row `T245` of this file; `.pdca/T245/rv3/` (directory only)
2026-10-30T07:00:53Z | P | revision r=1 | attempt n=0 | Notice (autonomous) for the final report: item `T245` PLAN set `N=3`/`r=1` and a new status path `.pdca/status/T245-warm-set-ops-3.md` instead of the queue's `N=1`/`r=2`, because both existing `T245` records are closed terminal cycles (`.pdca/status/T245-warm-set-ops-1.md` at `N=1 r=2 n=1/3`, `.pdca/status/T245-warm-set-ops-2.md` at `N=2 r=1 n=1/3`) and reusing or overwriting either would erase terminal history; no counter or plan revision of the collection itself changed, no attempt consumed, no defect entry | `.pdca/rc5/items/T245/preparation.md` §0; `.pdca/status/T245-warm-set-ops-1.md`, `.pdca/status/T245-warm-set-ops-2.md` (records read as identity evidence, untouched)
2026-10-30T07:03:32Z | P | revision r=1 | attempt n=0 | T246 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=2 r=1 n=1/3, status_path .pdca/status/T246-raw-datareader-2.md, criteria C1..C4, evidence_root .pdca/T246/rv6/ (directories only); queue row T246 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, cycle_id=N=2, status_path, plan_revision=r=1); no criterion, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | .pdca/status/T246-raw-datareader-2.md (§Current state, §PLAN record r1); queue row T246 of this file; .pdca/T246/rv6/attempts/
2026-10-30T07:03:32Z | P | revision r=1 | attempt n=0 | Notice (autonomous) for the final report: the queue's T246 identity N=1, r=2 was superseded by the item PLAN's N=2, r=1 — .pdca/status/T246-raw-datareader-1.md is a closed record at N=1 r=5 n=1/3 (terminal incomplete), so r=2 would collide with existing revision history and a changed measurement/acceptance basis opens a new cycle; the N=1 r=6 in-place alternative was not used; plan_cap lowered from 4 to 3 after one planner call failed in transport (timeout, no state written) and was retried on the same PLAN — no counter, attempt or defect entry of the collection changed | .pdca/status/T246-raw-datareader-2.md §1 (identity), §Revision history; .pdca/status/T246-raw-datareader-1.md (read-only)
2026-10-30T07:05:59Z | P | revision r=1 | attempt n=0 | T247 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=2 n=1/3, status_path .pdca/status/T247-json-streaming-1.md, criteria AC-247-01..06, evidence_root .pdca/T247/rv3/ (directories only); queue row T247 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=2); r=1 superseded by a genuine contract revision (two-tier acceptance + explicit scope boundary against T311/T250), DO never ran so n stays 1/3; no collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | .pdca/status/T247-json-streaming-1.md (§Current state, §PLAN record r2, §Revision history); queue row T247 of this file; .pdca/T247/rv3/attempts/
2026-10-30T07:08:29Z | P | revision r=1 | attempt n=0 | T250 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1/3 (resumption of the saved r=1 plan at the PLAN→DO boundary, not a revision), status_path .pdca/status/T250-csv-terminal-plan-cache-1.md, criteria AC-250-1..4, evidence_root .pdca/T250/rv1/ (directories only); queue row T250 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1, cycle_id cell normalized from 'N=1, r=2' to 'N=1'); no collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | .pdca/status/T250-csv-terminal-plan-cache-1.md (§Current state, §PLAN record rc5-r1, §Revision history); queue row T250 of this file; .pdca/T250/rv1/attempts/
2026-10-30T07:08:29Z | P | revision r=1 | attempt n=0 | Notice (autonomous) for the final report: the collection queue's plan revision for item T250 was corrected r=2 → r=1 — the item PLAN re-entered T250 as a resumption of its saved r=1 plan at the PLAN→DO boundary (DO never ran, defect history empty, brief and criteria unchanged, the stop at T246 was external; re-anchoring rc3 0d4b0cf6 → rc5 1e31331d is a Notice with no semantic drift), so a revision bump was forbidden and the re-open rule's r+1 does not apply to an unexecuted plan; the cycle_id cell dropped its stale ', r=2' suffix to match the identity; no criterion, attempt, collection counter or defect entry changed | .pdca/status/T250-csv-terminal-plan-cache-1.md §PLAN record rc5-r1, §Revision history
2026-10-30T07:15:16Z | P | revision r=1 | attempt n=0 | T311 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=2 n=1, status_path .pdca/status/T311-json-streaming-1.md, criteria C311-01..05, evidence_root .pdca/T311/rv2/ (directories only); queue row T311 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=2); r=1 superseded by a genuine contract revision (2026-10-20 two-tier acceptance policy adopted verbatim + explicit sibling boundaries T247/T250 + retired E311/R311 scaffolding), DO never ran so n stays 1; no collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T311-json-streaming-1.md` (§Current state, §PLAN record r2, §Revision history); queue row `T311` of this file; `.pdca/T311/rv2/attempts/`
2026-10-30T07:15:16Z | P | revision r=1 | attempt n=0 | Notice (autonomous) for the final report: the collection-wide evidence-root supersession of 2026-10-30T06:45:06Z was motivated by the item-id collision with foreign rc4 artifacts under `.pdca/T352/rv1/` and `.pdca/T353/rv1/`; for `T311` no such collision exists (`.pdca/T311` did not exist before this write), so the item PLAN's contract evidence root `.pdca/T311/rv2/` is used as-is and no foreign artifact can be read through it; this follows the same root convention already recorded for T246 (`rv6`), T247 (`rv3`) and T250 (`rv1`); no criterion, plan revision, attempt, collection counter or defect entry changed | `.pdca/status/T311-json-streaming-1.md` §Current state (evidence root); this file's progress-log row of 2026-10-30T06:45:06Z (read-only)
2026-10-30T07:18:02Z | P | revision r=1 | attempt n=0 | T267 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1, status_path .pdca/status/T267-rc5-1.md, criteria AC1..AC3, evidence_root .pdca/rc5/items/T267/rv1/ (attempts/ directory only, collection-scoped, no .pdca/T267 root); queue row T267 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1); no criterion, collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T267-rc5-1.md` (§Current state, §PLAN record r1, §Revision history); queue row `T267` of this file; `.pdca/rc5/items/T267/rv1/attempts/`
2026-10-30T07:18:02Z | P | revision r=1 | attempt n=0 | Notice (autonomous) for the final report: the T267 planner's verbatim quote of the issue benchmark table inside its returned plan was delivered with a mangled third row (`| Linq2Db_DelayPercentilesByModel | 45.04 ms | **5.52 KB` -> **5.67×** |`); it is recorded exactly as returned and not repaired, the authoritative brief stays this file's §Input → `### #267`, and the fidelity note lives in the item record's §Current state + §Progress log, not inside the verbatim block; no criterion, plan revision, collection counter, attempt or defect entry changed | `.pdca/status/T267-rc5-1.md` §Current state (fidelity note), §Progress log; this file's progress-log row of 2026-10-30T07:18:02Z
2026-10-30T07:19:17Z | P | revision r=1 | attempt n=0 | T268 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1, status_path .pdca/status/T268-rc5-1.md, criteria AC1..AC3, evidence_root .pdca/rc5/items/T268/rv1/ (attempts/ directory only, collection-scoped, no .pdca/T268 root); queue row T268 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1); no criterion, collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T268-rc5-1.md` (§Current state, §PLAN record r1, §Revision history); queue row `T268` of this file; `.pdca/rc5/items/T268/rv1/attempts/`
2026-10-30T07:23:23Z | P | revision r=1 | attempt n=0 | T269 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1, status_path .pdca/status/T269-rc5-1.md, criteria AC1..AC3, evidence_root .pdca/rc5/items/T269/rv1/ (attempts/ directory only, collection-scoped, superseding the plan text's .pdca/T269/rv1/ binding root - the item id is reused from an earlier collection); queue row T269 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1); no criterion, collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T269-rc5-1.md` (§Current state, §PLAN record r1, §Revision history); queue row `T269` of this file; `.pdca/rc5/items/T269/rv1/attempts/`
2026-10-30T07:24:34Z | P | revision r=1 | attempt n=0 | T270 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1, status_path .pdca/status/T270-rc5-1.md, criteria AC1..AC3, evidence_root .pdca/rc5/items/T270/rv1/ (attempts/ directory only, collection-scoped); queue row T270 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1); no criterion, collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T270-rc5-1.md` (§Current state, §PLAN record r1, §Revision history); queue row `T270` of this file; `.pdca/rc5/items/T270/rv1/attempts/`
2026-10-30T07:25:02Z | P | revision r=1 | attempt n=0 | T271 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1, status_path .pdca/status/T271-rc5-1.md, criteria AC1..AC3, evidence_root .pdca/rc5/items/T271/rv1/ (attempts/ directory only, collection-scoped); queue row T271 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1); no criterion, collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T271-rc5-1.md` (§Current state, §PLAN record r1, §Revision history); queue row `T271` of this file; `.pdca/rc5/items/T271/rv1/attempts/`
2026-10-30T07:28:02Z | P | revision r=1 | attempt n=0 | T272 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1, status_path .pdca/status/T272-rc5-1.md, criteria AC1..AC3, evidence_root .pdca/rc5/items/T272/rv1/ (attempts/ directory only, collection-scoped); queue row T272 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1); no criterion, collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T272-rc5-1.md` (§Current state, §PLAN record r1, §Revision history); queue row `T272` of this file; `.pdca/rc5/items/T272/rv1/attempts/`
2026-10-30T07:28:43Z | P | revision r=1 | attempt n=0 | T273 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1, status_path .pdca/status/T273-rc5-1.md, criteria AC1..AC3, evidence_root .pdca/rc5/items/T273/rv1/ (attempts/ directory only, collection-scoped, superseding the plan text's `.pdca/T273/rv1/` binding root - the item id is reused from an earlier collection); queue row T273 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1); no criterion, collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T273-rc5-1.md` (§Current state, §PLAN record r1, §Revision history); queue row `T273` of this file; `.pdca/rc5/items/T273/rv1/attempts/`
2026-10-30T07:29:14Z | P | revision r=1 | attempt n=0 | T274 PLAN persisted: form=cycle, overlay=coder+.NET, plan_state=ready, status=pending, identity N=1 r=1 n=1, status_path .pdca/status/T274-rc5-1.md, criteria AC1..AC3, evidence_root .pdca/rc5/items/T274/rv1/ (attempts/ directory only, collection-scoped, superseding the plan text's `.pdca/T274/rv1/` binding root - the item id is reused from an earlier collection); queue row T274 updated (execution_kind=cycle, selected_variant=coder+.NET, plan_state=ready, plan_revision=r=1); no criterion, collection counter, attempt or defect entry changed; PLAN→DO boundary reached, no DO started | `.pdca/status/T274-rc5-1.md` (§Current state, §PLAN record r1, §Revision history); queue row `T274` of this file; `.pdca/rc5/items/T274/rv1/attempts/`
2026-10-30T07:35:22Z | P | revision r=1 | attempt n=0 | T302 PLAN-terminal resolution recorded: terminal=infeasible, attempts=0, plan_state=— (never ready), no DO, no CHECK — cause verbatim-short: original task is infeasible under the stated constraints, because the item's own body gates the product work behind two explicit USER acts (approval of an implementation plan that does not exist yet, then a separate authorization of the implementation) and autonomous mode can obtain neither, nor narrow the requested result to the plan alone; cycle N=1 r=1 n=0 unspent; the r1 plan draft is kept in the record for the next authorized run; issue #302 stays OPEN in milestone 1.0.9-rc5; queue row T302 = status=incomplete, cycle_id=N=1, plan_revision=r=1 | `.pdca/status/T302-rc5-1.md` (§Current state, §PLAN record r1, §ACT record); queue row `T302` of this file
2026-10-30T07:35:22Z | P | revision r=1 | attempt n=0 | Notice (autonomous) for the final report: item `T302` closed at the PLAN→ACT boundary as `infeasible` with `attempts=0` — no user approval of the implementation plan and no separate user authorization of the product implementation are obtainable in autonomous mode, so no DO was entered and no independent CHECK ran (recorded as absent, never as a pass); the item is `incomplete`, not `done`, and nothing in this resolution is credit toward the collection goal; a normal-mode, user-present session must approve the recorded plan draft before a new cycle N=2 may open | `.pdca/status/T302-rc5-1.md` §ACT record, §CHECK record; this file §Done / Verified / Incomplete
2026-10-30T07:35:22Z | P | revision r=1 | attempt n=0 | T319 campaign preparation persisted: form=incremental (campaign), preparation_state=ready, preparation_ref .pdca/rc5/items/T319/preparation.md@r1, identity N=1 r=1 n=0/3, status_path .pdca/status/T319-rc5-1.md, criteria AC1..AC4 verbatim, depends_on T317+T318 (hard execution prerequisite), first step = post-prerequisite baseline measurement with a pre-set STOP at the Step 1 boundary — barrier-ready but prerequisite-blocked: no campaign runtime status created, no baseline measured, no DO, no product change | `.pdca/rc5/items/T319/preparation.md` (immutable charter r1, sha256 `fe74e5b39674acb3ac8ca0aad176c7b5868164d8727aa858a4d5c64023ab5260`); queue row `T319` of this file
2026-10-30T07:35:22Z | P | revision r=1 | attempt n=0 | Notice (autonomous) for the final report: item `T319` is prepared as a `campaign` although the issue says «Candidate, not scheduled. Do not start before» `#317`/`#318` land — the bar is on execution, not on preparing, so it is represented as a hard `depends_on: T317, T318` plus a Step 1 that measures the post-prerequisite gap first and stops the campaign there if the gap is already closed; the campaign must not start until both prerequisites are accepted or explicitly resolved, and the queue row stays `plan_state=—`, `status=pending` | `.pdca/rc5/items/T319/preparation.md` §0, §7, §12; queue row `T319` of this file
2026-10-30T07:39:11Z | P | revision r=1 | attempt n=0 | ALL-barrier passed; schedule persisted (G1, cap=1, order T353 → T352 → T318 → T317 → T319 → T245 → T250 → T246 → T247 → T311 → T268 → T269 → T267 → T270 → T271 → T272 → T273 → T274); lane allocated: collection/rc5/group-1 in the current worktree; T302 terminal (no lane); next: T353 DO | §Groups, §Integration branch, §Decisions log of this file; `git rev-parse HEAD` 1e31331d (branch collection/rc5/group-1)
2026-10-30T08:38:05Z | A | revision r=1 | attempt n=1 | T318 closed at the PLAN→ACT boundary as terminal `infeasible`: the scoped mechanism (avoid null/empty-collection copying in `Clone()`/`CopyTo`/`CloneForCache`) cannot produce AC-T318-01's mandatory reduction because that copying is already absent — `CloneForCache` guards lists with `Count > 0`, join/array-join arrays are `null`, sorting/window/CTE are reference-copied, and the whole measured clone allocation 872 B equals the bare `QueryCommand` shell (`V6−V5`, T245); DO attempt 1 measured a 0 B delta on both clone-heavy arms and was reverted, tree = measured baseline 3e42047b. No CHECK ran, no PASS, no `done`, no substitute deliverable; AC-T318-01 unmet/unattainable in scope, AC-T318-02 not delivered, AC-T318-03 build leg only; residual ~69 KB raw-recipe/plan owned by T317/T245. Queue row T318 = status `incomplete`, terminal `infeasible`, `plan_state` released from `ready` to `—`, `attempts` cell `0` per the lint schema (real DO attempts = 1, recorded in the item record); execution failures unchanged; group continues with T317. Issue #318 stays OPEN in 1.0.9-rc5 — comment posted, not closed | `.pdca/status/T318-rc5-1.md` §ACT record; queue row `T318` of this file; `.pdca/rc5/items/T318/rv1/`; `gh issue comment 318` → https://github.com/AlexeyShirshov/nextorm/issues/318#issuecomment-6518662205
2026-10-30T10:22:01Z | D | revision r=1 | attempt n=1 | T317 DO attempt 1 **rejected and reverted**: the candidate was RED on the fail-closed integration suite (16 failed, exit 2) against 0 failed on the same base in a detached worktree, and regressed allocation on the four `In*` arms; recovery-first, the full patch was preserved (`attempts/attempt1-full.diff`, `attempts/attempt1-PlanKeyVersionEquivalenceTests.cs.txt`, plus `diff.patch`), then `git checkout --` on the three product files and `rm` of the untracked test file — tree back to the base, no `.pdca/**` or `examples/**` path touched. Reverted tree: build exit `0` (`0 Warning(s) 0 Error(s)`), integration GREEN exit `0` (total 7933 / succeeded 7732 / failed 0 / skipped 201), no `NEXTORM_SKIP_PROVIDERS`. Counters unchanged (`n=1` consumed, next `2/3`, `r=1`); defects `T317-D2` (hypothesis, unconfirmed) and `T317-D3` retained; no acceptance claim, no CHECK, no commit of product code, no push; group continues with T317 attempt 2 | `.pdca/status/T317-rc5-1.md` §DO result continuation; queue row `T317` of this file; `.pdca/rc5/items/T317/rv1/build-reverted.log`, `integration-reverted.log`, `attempts/`
2026-10-30T11:44:14Z | A | revision r=1 | attempt n=2 | T317 closed at the DO→ACT boundary as terminal `infeasible` (expert-reviewed): attempt 2 passed every gate (build 0W/0E, core 3892/3892, dialects 7/7, fail-closed integration 7933/7732/0/201, acceptance 7/7 rc 0) but violated `AC-317-02` on 15 of 17 nextorm arms (`+160 … +1,761 B/op`, 11 deterministic) and showed no `AC-317-01` reduction above the host noise floor; the routed strong-tier `pdca-escalate` decision proved a per-`QueryCommand` memo is written and never read on fresh-fluent calls (`EntityBuilder.Select` → `new QueryCommand<TResult>(...)` per call, `EntityBuilder.cs:5481`), so attempt `3/3` was not run and no further attempt is consumed (real attempts = 2). No CHECK ran on any candidate, no PASS, no `done`, no substitute deliverable; the tree is at `9af1e0f1` with no product delta. Queue row T317 = status `incomplete`, terminal `infeasible`, `plan_state` released `ready`→`—`, `attempts` cell `0` per the lint schema (real DO attempts = 2, recorded in the item record); plan-terminal resolutions now 3 (`T302`, `T318`, `T317`), execution failures unchanged; group continues. Issue #317 stays OPEN in 1.0.9-rc5 — comment posted, not closed | `.pdca/status/T317-rc5-1.md` §ACT record; queue row `T317` of this file; `.pdca/rc5/items/T317/rv1/`; `gh issue comment 317` → https://github.com/AlexeyShirshov/nextorm/issues/317#issuecomment-6521830486
2026-10-30T11:48:09Z | P | revision r=1 | attempt n=1 | T319 Step 1 opened: the prerequisites `T317`/`T318` are satisfied **as a sequencing gate** — both closed terminal `infeasible` with zero product edits, so no prerequisite work is pending and there is no tree drift on `collection/rc5/group-1`; the issue's "fallback, not the default" clause is activated, not refuted (`T317`'s expert review proved reuse must live in builder-side or shared state — this campaign's seam — and `T318` found empty-collection copying already absent, so the cheap fixes handed their residual to `T319`). The campaign runtime record `.pdca/status/T319-rc5-1.md` was created as a container (`Status: running`, `Form: incremental`, `Overlay: pdca-dotnet`, `Preparation ref: .pdca/rc5/items/T319/preparation.md@r1`, `Commit policy: auto`), counters `N=1 / r=1 / n=1` (Step 1 DO enters as attempt `1/3`), charter unchanged (no `r+1`), `## Baseline record` = `not captured: Step 1 not run`; queue row `T319` status `pending` → `in-progress`, `preparation_state` stays `ready`. Zero product edits, nothing committed, no push; resume point = Step 1 DO (same-round BDN baseline into `.pdca/rc5/items/T319/rv1/`) | `.pdca/status/T319-rc5-1.md` §Campaign state, §Checkpoint, §Step records; queue row `T319` of this file; `.pdca/rc5/items/T319/preparation.md@r1` §7
2026-10-30T12:34:32Z | P | revision r=1 | attempt n=1 | schedule revision (`T319` pause): queue row `T319` disposition `active` → `paused` with status unchanged at `in-progress`, checkpoint recorded in the item record — Step 1 evidence complete: baseline, attribution and net-addressability `+1,120 / +360 / −160 B/query` (best / likely / worst), resume point = Step 1 boundary CHECK; the lane resumes at `T245` (the first non-terminal unit) and the `G1` order carries no dependency on `T319` — `T319` moved to immediately after `T274` so no item waits on it, and the terminal items (`T353`, `T352` done; `T302`, `T318`, `T317` plan-terminal) keep their place in the order for history; counters `N=1 / r=1 / n=1`, `preparation_state=ready`, `preparation_ref`, `cycle_id`, `status_path` and `plan_revision` unchanged, no attempt granted or consumed, no defect entry, no criterion changed, no product edit, nothing committed for an item | `.pdca/status/collection-rc5.md` queue row `T319`, §Groups row `G1`, §Progress log; `.pdca/status/T319-rc5-1.md` §Campaign state, §Checkpoint; `.pdca/rc5/items/T319/rv1/` (Step 1 evidence)
2026-10-30T12:53:00Z | D | revision r=1 | attempt n=1 | T245 Step 1 baseline (measurement only, no product change, no attempt consumed) on the frozen identity HEAD cb2246e9: EXCEPT 7 112.1 B/query vs linq2db 5 176.2 (1.374× alloc, 0.475× time, +1 935.9 B), INTERSECT 7 096.1 vs 5 128.2 (1.384×, 0.477×, +1 967.9 B), Prepare() controls 760.0/744.0, ladder Construct 4 184.18 → Warm_PlanOnly 6 432.31/6 432.28 → Warm_Reused 0.00 B/query, probe phases operands 3 712 / clone 872 / prepare 2 248 / execute 808-792 with closure residual 0; three BDN runs + the existing attribution probe all exit 0; CHECK pending, no verdict | `.pdca/rc5/items/T245/rv3/baseline/summary.md`, `.pdca/status/T245-warm-set-ops-3.md` §Baseline record
2026-10-30T13:02:40Z | C | revision r=1 | attempt n=1 | T245 step-1 CHECK: not accepted -> DO evidence completion (no attempt consumed, counters unchanged); findings T245-S1-F1..F3 | `.pdca/rc5/items/T245/rv3/baseline/`
2026-10-30T13:05:34Z | P | revision r=1 | attempt n=1 | Branch change directed by the user (bookkeeping only, no product change): the lane's commits now land on `1.0.9-rc5` — before `HEAD 46cd9785` on `collection/rc5/group-1`, after `HEAD 46cd9785` on `1.0.9-rc5`; `git branch -f 1.0.9-rc5 46cd9785` was legal only under the proven fast-forward (`git merge-base --is-ancestor 1.0.9-rc5 46cd9785` rc 0; `git diff --stat 1.0.9-rc5..46cd9785` = `.pdca/status/T245-warm-set-ops-3.md` and `.pdca/status/collection-rc5.md` only; `git diff --cached --stat` empty), then `git switch 1.0.9-rc5`. The 14-path dirty set (3 modified `T247`/`T250`/`T311`, 11 untracked `T246`/`T267`/`T268`/`T269`/`T270`/`T271`/`T272`/`T273`/`T274`/`T302`/`T319`) is unchanged and still uncommitted; sha256 `.pdca/status/collection-rc5.md` `3abd0701d5e8931123bb72ce2e05fdaa3f73eab94cb4fa0ab6e92854d69c6e25` before → same after, `.pdca/status/T245-warm-set-ops-3.md` `75b1834b88c6db7a06d3b1d6a06ec092dd0e942519e2b48c586dbf1ef99ffb59` before → same after; `collection/rc5/group-1` kept at `46cd9785` as a retired label (never checked out again, never recreated); no merge barrier, no `git merge --no-ff`, no push, no tag; no criterion, plan revision, attempt, counter, defect entry or product file changed. Notice (autonomous) for the final report: the caller's premise was refuted on disk — it reported the lane already fast-forwarded to `c7f4442d` with `1.0.9-rc5` checked out, while the actual state was `HEAD 46cd9785` on `collection/rc5/group-1` and `1.0.9-rc5` at `c7f4442d`, one commit behind. | queue `branch` cells, §Groups `G1`, §Integration branch, §Decisions log, this row; `git rev-parse HEAD` 46cd9785 on both sides; `git status --porcelain` 14 paths
2026-10-30T13:13:51Z | P | revision r=2 | attempt n=1 | Container revision r=1 → r=2 recorded (entry scope 19 → 22): three new queue rows `T359`/`T360`/`T361` (`plan_state=ready`, `cycle` + `coder+.NET`, `N=1` `r=1` `n=1/3`, status paths `.pdca/status/T{359,360,361}-rc5-1.md`, evidence roots `.pdca/rc5/items/T{359,360,361}/rv1/`), `## Input` extended with the verbatim `#359`/`#360`/`#361` briefs (bodies byte-identical to `.pdca/rc5/input-scope-extension/issue-{359,360,361}.md`, header fields carried in file order), `G1` order now `T353 → T352 → T318 → T317 → T302 → T245 → T250 → T246 → T247 → T311 → T359 → T360 → T361 → T268 → T269 → T267 → T270 → T271 → T272 → T273 → T274 → T319`, edge `T359 → T271`, schedule revision classified **compatible local** (no stop/re-approve); `AC-COL-01` re-issued by `pdca-planner`, `AC-COL-02..04` unchanged; `T245` stays mid-item (`N=3 r=1 n=1/3`), no existing item's status, counter, plan revision or attempt changed, no product file touched; three item records created at the PLAN→DO boundary (no DO started, no attempt consumed) | `.pdca/rc5/entry-plan-r2.md`; queue rows `T359`/`T360`/`T361`; §Groups `G1`; §Decisions log; `.pdca/status/T359-rc5-1.md`, `.pdca/status/T360-rc5-1.md`, `.pdca/status/T361-rc5-1.md`
2026-10-30T13:35:00Z | D | revision r=1 | attempt n=1 | T245 step-1 evidence completion returned for re-CHECK (no attempt consumed, no product change, counters N=3 r=1 n=1/3 unchanged): the three artifacts CHECK requested — provenance observation (tree identity: cb2246e9..HEAD is .pdca/status-only, src/tests/benchmarks/examples diff empty and worktree clean, so the measured tree equals today tip 0d3bf558; charter 1e31331d is an ancestor but differs in 8 product/test files, equivalence NOT established; missing item PLAN recorded as a fidelity gap, not reconstructed; benchmark-source sha256, executed-binary identity, DB pin, const loop divisors 10/100), acceptance-workload reconciliation (all four gaps closed on one named owner: +399.82 B construction and +48.00 B execution are the JIT tiered-compilation regime — BDN with DOTNET_TieredCompilation=0 measures 4 584.19/4 584.20 and 760.00/744.00 against the probe 4 584 and 760/744; +80.21 is unequal JIT state between the three baseline BDN runs, single-process closure −0.12/+0.19/0.00; prepare phase 2 248.13 vs 2 248.12 vs 2 248 matched and now measured independently of the algebraic subtraction) and exact extraction (exact-values.json/csv, 125 rows: integer BytesAllocatedPerOperation, divisor file:line, B/query division, Mean/StdErr ns, run identity + display-rounding table); unverified and named: object-level mechanism of the tiered-JIT reduction, cross-run spread of the tiered level, ~2× time of the tiering-off runs; measurement-only harness added under benchmarks/nextorm.benchmark/Attribution/ (2 new files) + Program.cs:38-43, no acceptance-recipe/threshold/denominator change | `.pdca/rc5/items/T245/rv3/provenance/provenance.md`, `.pdca/rc5/items/T245/rv3/reconciliation/{reconciliation.md,exact-values.json,exact-values.csv,extraction-notes.md}`; queue row T245
2026-10-30T13:37:35Z | C | revision r=1 | attempt n=1 | T245 step-1 **re-CHECK**: **not accepted** (Step 1 only) -> `DO` evidence completion with 5 requested items; **no attempt consumed**, counters `N=3 r=1 n=1/3` unchanged; criteria `AC-245-01` unmet (`1.374×`/`1.384×` allocation, `0.475×`/`0.477×` time), `AC-245-02..07` unverified; defects `T245-S1-F1` (blocking, partially resolved, not closed — preparation now independently measured at ≈`2,248.12 B/query`, `760/744` controls reproduced with tiering off; the `80.21/80.18 B` cross-run discrepancy is associated with the JIT regime, not attributed), `T245-S1-F2` (blocking, mismatch confirmed / authorization unverified / not closed — `1e31331d` vs `cb2246e9` = 8 product/test files `253+/44−`; no superseding authorization; delta ends before `aed135d1`; `rg` instead of Roslyn), `T245-S1-F3` (informational, closed as a provenance limitation), `T245-S1-F4` (informational, confirmed — inconsistent evidence labels); requested: (1) exact existing PLAN/re-anchor authorization, (2) final identity/delta through `aed135d1` incl. tracked+untracked scope and run/binary linkage, (3) Roslyn-grounded workload/operation/denominator observations, (4) matched baseline-only phase evidence for both operations under the acceptance regime, (5) append-only label correction; no candidate ABBA run, native gate or whole-suite run requested; same-defect recurrence on F1/F2 -> `pdca-escalate` in flight (also covering the PLAN-owned re-anchor question); zero product fixes, no product change | `.pdca/status/T245-warm-set-ops-3.md` §CHECK record `### Step 1 re-CHECK (after evidence completion)`; `.pdca/rc5/items/T245/rv3/{provenance,reconciliation}/`; queue row `T245` notes
2026-10-30T13:44:16Z | P | revision r=2 | attempt n=0 | T245 PLAN revision r=2 recorded (binding X1 = cb2246e9; charter `.pdca/rc5/items/T245/preparation-r2.md@r2` supersedes `preparation.md@r1`; JIT-regime comparability condition added to the measurement method) and one bounded Step 1 DO evidence-completion round (items 2–5) authorized; plan report persisted verbatim at `.pdca/rc5/items/T245/plan-r2.md` (sha256 `108017a5…beef`); queue row `T245` `plan_revision` r=1 → r=2; `AC-245-01..07`, thresholds, tolerances and denominators unchanged; no attempt consumed, no product change | `.pdca/rc5/items/T245/plan-r2.md`, `.pdca/rc5/items/T245/preparation-r2.md`, `.pdca/status/T245-warm-set-ops-3.md` §PLAN record `### r2`
2026-10-30T14:03Z | D | revision r=2 | attempt n=1 | T245 step-1 bounded round 2 returned for re-CHECK (no run and no measurement in the finalizing dispatch; no attempt consumed, counters N=3 r=2 n=1/3 unchanged, no criterion/threshold/denominator/tolerance changed, zero product change): both-operation attribution with five new `_Intersect` arms — construction 4 104.16/4 104.18, prepare+lookup 2 248.12/2 248.10, execution 760.10/744.10, full 7 112.40/7 096.40 B/query, closure −0.02/−0.02 — with the residual bounded at 160.01 B cross-process (112.01 construction + 48.00 execution; 528.03 tiering-off) and explicitly not closed by direct observation; Roslyn observations of harness/acceptance-arm equivalence, preparation entry points and denominators 10/100; append-only errata (residual sign, `queries_per_window`, changed-files contradiction) + one pointer line in `reconciliation.md`; final identity through `5fa1849c` with the 8-file `1e31331d..cb2246e9` inventory proven non-touching via `roslyn refs` and the core-DLL baseline-hash limitation stated; item record finalized (DO-result heading order fixed, changed files / git state refreshed, defect history + progress log appended); commit subject `#245 T245 step-1 bounded completion: both-operation attribution, Roslyn observations, errata, final identity; evidence .pdca/rc5/items/T245/rv3/` | `.pdca/status/T245-warm-set-ops-3.md`; `.pdca/rc5/items/T245/rv3/reconciliation/`, `.pdca/rc5/items/T245/rv3/provenance/`
2026-10-30T14:09Z | A | revision r=2 | attempt n=1 | T245 **step 1 accepted** under PLAN `r=2` (independent `pdca-check`: `accepted — Step 1 under r=2 only`) — ACT closed for step 1 only, **campaign open**, no campaign PASS, no `done`, **no attempt consumed** (`N=3 r=2 n=1/3`), **zero product change**, issue `#245` stays OPEN; accepted basis = `X1` binding `cb2246e9` with run L per-operation attribution (construction `4 104.16/4 104.18`, preparation `2 248.12/2 248.10`, execution `760.10/744.10`, full `7 112.40/7 096.40`, residual `−0.02` B/query) and the baseline BDN arms `7 112.1/7 096.1` vs linq2db `5 176.2/5 128.2` (`1.374×`/`1.384×`, time ≈`0.475×`/`0.477×`, controls `760.0/744.0`, `Warm_Reused` `0.00`); carry-forward limits as CHECK stated (`160.01`/`528.03` diagnostic observed ranges, not bounds; ≈`2 248` invariance observed not proven; `872 B` clone not a proven removable pool; no drift allowance; Step 2 needs its own same-round paired comparison under the held-JIT-regime condition); findings `T245-S1-F1` closed for the `r=2` Step 1 scope, `F2` closed on the authorized source-tree basis, `F3` remains a provenance limitation, `F4` closed by append-only clarification; queue row `T245` notes appended; next: the campaign step boundary — `pdca-planner` decides Step 2's scope/contract, no Step 2 work before that decision | `.pdca/status/T245-warm-set-ops-3.md` §CHECK record `### Step 1 CHECK under PLAN r=2 — accepted`, §ACT record, §Baseline record `### Accepted basis (r=2, after Step 1 CHECK)`; `.pdca/rc5/items/T245/rv3/reconciliation/{both-operations.md,bdn-both-operations.log,exact-values.csv}`
2026-10-30T14:22Z | D | revision r=2 | attempt n=1 | T245 **step 2 (n=1/3) — no candidate, zero product change**: object-level breakdown of the two candidate buckets (new measurement-only probe `--t245-step2-breakdown`, exit **0**, pinned DB `32bf10a0…72295` unchanged) closes them to the byte — construction `4 584.00` = operands `3 712.00` (out-of-surface) + `#240`-mandatory live clone `872.00` (residual `0.00`), `ResetPreparation`/`SetOperation` `0 B`; prepare/hash/lookup `2 248.00` (= accepted `2 248.12/2 248.10`) is the D201-3 raw-reuse path (`reuses=1`, ordinary `PrepareCommand` = `2 424` and is skipped) = `RawShape ×2` `1 872.00` + `RawGraphSnapshot` `48.00` (out-of-surface general prepare machinery) + command-bound plan key `328–408` (`T317`-protected), set-op-specific hash/apply/lookup `0 B`; `≤ 80 B` rung/parts overlap recorded as an attribution residual. Verdict `0 B removable-without-breaking-invariant inside §4` ⇒ pre-registered rule clause 1 unsatisfiable (max §4 delta `0.00 B/query`, floor `−8.00 B/query`) and clause 10 triggered ⇒ closure path **C (terminal branch)** returned to PLAN/ACT; `dotnet build nextorm.slnx -c Debug` exit **0** (`14:20:23Z→14:22:20Z`, 1 m 56.49 s, 0W/0E); no product edit, nothing reverted, `src`/`tests`/`examples` byte-identical to `X1 = cb2246e9`; residual `AC-245-01` `1.374×`/`1.384×` (`+1 935.9`/`+1 967.9 B/query`), `AC-245-02` `ΔB = 0.00`; **`n=1/3` spent**, no CHECK run, no PASS, no `done`, campaign not accepted, issue #245 stays OPEN | `.pdca/rc5/items/T245/rv3/step2/{object-breakdown.md,no-candidate.md,probe-step2-breakdown.log,build-debug-solution.log}`; `.pdca/status/T245-warm-set-ops-3.md` §DO result `### Step 2 (n=1/3)`
2026-10-30T14:33:53Z | P | revision r=3 | attempt n=1 | T245 step 2 CHECK -> PLAN: contract revision r=3 — the Step 2 CHECK returned **not accepted** (supplied criteria rewritten under the original IDs, the zero-removable-byte conclusion exceeding its evidence, attribution incomplete, invalid rejection/termination reasoning, unverified final provenance: `T245-S2-F1..F5`, no attempt consumed by the CHECK); `r=3` preserves `AC-245-01..07` verbatim by exact reference (`preparation-r2.md@r2` §6), authorizes the mechanism set `M1`/`M2` on `Except`/`Intersect`, the monotonic-rung attribution ladder `R0 -> R1 -> R1* -> R2` with the `≥ 1 B/query` retention floor, the two-tier resolution (Tier A `≤ 1.20×` per subject operator; Tier B charter structural floor + exact residual breakdown) and the restated rejection rule; counters `N=3 r=3 n=1/3` — the outgoing `r=2 n=1` attempt stays recorded as unaccepted with zero product change and consumes no attempt of the new revision; no product change, no attempt consumed, no plan-terminal blocker, campaign open, issue `#245` stays OPEN; plan text `.pdca/rc5/items/T245/plan-r3.md` (sha256 `8f98155e…00c34`, 8 093 B, 39 lines), CHECK report `.pdca/rc5/items/T245/check-step2.md` (sha256 `ea7c2a80…46a80`, 8 668 B, 64 lines) | queue row `T245` (`plan_revision` `r=2` → `r=3`, notes append); `.pdca/status/T245-warm-set-ops-3.md` §PLAN record `### r3`, §CHECK record `### Step 2 CHECK (r=2, n=1)`, §Revision history, §Defect history, §Progress log
2026-10-30T15:26Z | D | revision r=3 | attempt n=1 | T245 **step 2 (r=3, n=1/3) DO completed** in the same attempt a controller cap cut off (nothing consumed, no product fix counted twice): retained **M1** = one capture of the set-operation operand's `RawShape` on the preparation path (`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` +11/−8, CRLF; probe identity `rawshape 1 872 → 1 328`, `plan-only 2 248 → 1 704` = the eliminated duplicate descriptor), measured under `DOTNET_TieredCompilation=0` ABBA `R0(A) → R1(B1) → R1(B2) → R0(A2)` plus an independent `R1-C1`: `Except 7 592.3/7 576.3 → 7 128.3…7 048.6/7 032.3 B/query` ⇒ **ΔB 464–544 B/query**, `ΔMean_us` inside the ±20 % band (`≤1.00×` baseline), ratios `1.3757×→1.3561…1.3616×` / `1.3855×→1.3655…1.3712×` ⇒ **Tier B** under `AC-245-02` with Tier A residual **836.7/878.0 B/query** to the `1.20×` bound; `Warm_Reused 0.00 B/query`, `Prepare()` `760.00/744.10` vs accepted `760.0/744.0`; `M2` `ΔB = 0` non-credited with no code remaining; native gate ceilings locked `7 530`/`1 090`/`0` (supersede of `9000`/`1400`, `+2 B/query` margin justified by the observed 21-run spread and the 112 B/query tiering states); gates all `rc 0`: build 0W/0E, `T240CommandConstructionTests` 11/11, cache-key 28/28 + 13/13, core 3 886/3 886, sqlite 2 252/2 253, postgres 1 727, sqlserver 1 841/1 849, mysql 1 171, mariadb 1 014, clickhouse 1 446, fail-closed integration 7 934/7 733/0/201; Lane G1 amendment recorded at `r=3` (no `r=4`, criteria verbatim); counters `N=3 r=3 n=1/3` (attempt spent by this DO), campaign open, **no CHECK/verdict/PASS**, issue #245 OPEN | `.pdca/rc5/items/T245/rv3/step2-r3/{attribution.md,gates.md,provenance/}`, `.pdca/rc5/items/T245/amendment-r3.md`; `.pdca/status/T245-warm-set-ops-3.md` §DO result `### Step 2 (r=3, n=1/3)`
2026-10-30T15:29Z | D | revision r=3 | attempt n=1 | T245 **step-2 (r=3) DO commit recorded**: `bc641498e0091d37023f9f041cc9ab9be08b1147` (`bc641498`), the single commit after `e1404aca`, `4 files changed, 402 insertions(+), 13 deletions(-)`, subject `#245 T245 step-2 (r3): retained M1 -464..-544 B/query, Tier B; gates + locked allocation ceilings; evidence .pdca/rc5/items/T245/rv3/`; exactly four item-owned paths committed by one explicit-path `git add` — `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `tests/nextorm.integration.tests/Sqlite/SqliteWarmSetOperationAllocationTests.cs`, `.pdca/status/T245-warm-set-ops-3.md`, `.pdca/status/collection-rc5.md` — no `-A`, no push, no merge, no tag, no new branch; `.pdca/rc5/items/T245/rv3/**` is **local evidence, gitignored** (`.gitignore` `/.pdca/*`), never a CI input; the foreign `.pdca/status/` dirty/untracked set (`T247`, `T250`, `T311`, `T246`, `T267`…`T274`, `T302`, `T319`) untouched and unclaimed; no counter, criterion, plan revision or attempt changed | queue row `T245`; `.pdca/status/T245-warm-set-ops-3.md` §Git state, §Progress log; `git log -1 --format='%H %s'` = `bc641498`
2026-10-30T15:28:53Z | P | revision r=3 | attempt n=1 | T245 **amendment scoping clarification recorded (user, verbatim, no driver wording)**: the `3`-attempt budget is **per step and per contract revision** (not per campaign, not per item) — `n/3` counts DO attempts on the current `r` of the current step, a revision resets `n` to 1 without erasing the defect history; the campaign's **number of steps is not capped**, it ends only at a whole-goal `met` or an allowed stop verdict (`limited`/`unreachable`/`blocked`/`stopped`), never because a step counter ran out; partial-win acceptance applies **at step level** (credited and committed when that step's attempt budget is spent, without ending the campaign and without blocking the residual from the next step); practical note — `T245` `r=3` grew through `CHECK → PLAN` revisions, so `n=1/3` is **not spent** and treating `r` growth as used attempts, or revising only to avoid a measured win, is a CHECK defect; amendment rule 3 is read subject to this scoping; no plan revision, no attempt, no counter, no criterion, no threshold, no defect entry changed, zero product change | `.pdca/status/collection-rc5.md` §Decisions log entry of 2026-10-30T15:28:53Z; `.pdca/rc5/items/T245/amendment-r3.md` §`Scoping clarification (user, verbatim, 2026-10-30)`; `.pdca/status/T245-warm-set-ops-3.md` §PLAN record `### r3`
2026-10-30T15:33Z | C | revision r=3 | attempt n=1 | T245 **step-2 CHECK (r=3, n=1) returned `not accepted → DO`** (CHECK consumed no attempt; `N=3 r=3 n=1/3` stand): retain `R1*` (`M1`) and its locked allocation gates, never revert the measured win, complete the missing evidence without consuming an attempt, then continue the authorized ladder at `n=2/3` (same `r=3`) while a plausible in-scope lever remains; findings `T245-S2-F6` (evidence linkage / committed gate snapshot / controls extraction / `r2-held-C1` identity) and `T245-S2-F7` (arithmetic and units a–d); **no ladder exhaustion, no campaign closure**. The complete CHECK report text was **not supplied to the executor and is not persisted under `.pdca/`** — recorded as a source limitation and requested, never reconstructed | `.pdca/status/T245-warm-set-ops-3.md` §CHECK record `### Step 2 CHECK (r=3, n=1)`; dispatch
2026-10-30T15:40Z | D | revision r=3 | attempt n=1 | T245 **evidence completion + reporting errata (S2-F6/S2-F7) written at the same `n=1/3`, zero product change**: (1) `commit-linkage.log` (146 lines, rc 0) binds the measured source to `bc641498` — raw `git show --stat`, the +11/−8 `M1` hunk, `git show` vs working-tree sha256 with the CRLF attribution (`git hash-object` identical `3a48b6c5`, `git diff --ignore-cr-at-eol` empty), clean `git status --porcelain -- src tests examples benchmarks`; (2) committed gate-test snapshot `SqliteWarmSetOperationAllocationTests.bc641498.cs` (sha256 `b33f7eb3…32d7e`, ceilings `7 530/1 090/0` visible); (3) `bdn-controls-and-reused.csv` + `bdn-held-regime-arms-perquery.{csv,txt}` (106 rows) with `Prepare()` `760.00/744.10 B/query`, `Warm_Reused 0.00` every run; extractor fixed per query (`bdn-extract.v1.py:66–70` → `bdn-extract.py:121–127`); (4) append-only errata: `R0-A` ratios **1.46666×/1.47726×**, time per query (`R0-A` 30.3556/29.0209 … `R1-C1` 30.3542/29.7272 µs/q), C1 INTERSECT time **1.0243×**, residuals **836.68/877.98 B/query**, held `R0` `7 592.3/7 576.3` vs accepted default basis `7 112.1/7 096.1` labelled and never mixed; (5) `r2-held-C1*` = second held-regime process of the **`R1` level** (`ΔB = 0.00` vs `R1-B2`), not rung `R2`; counters unchanged (`n=1/3`, no attempt consumed), no verdict, campaign open | `.pdca/rc5/items/T245/rv3/step2-r3/evidence-completion/`, `errata-step2-r3.md`, `attribution.md` §7, `bdn-extract.py`; `.pdca/status/T245-warm-set-ops-3.md` §DO result `### Step 2 (r=3) — evidence completion and reporting errata`
2026-10-30T15:52Z | DO | revision r=3 | attempt n=1 | T245 **Block 2 (`n=2/3`) not attempted — clean stop at the block boundary**: `n=2/3` stays **unspent and open** (`N=3 r=3`, `n=1/3` spent), **no ladder exhaustion is claimed** and the campaign stays open; the remaining lever needs a measured decomposition of the **execution** bucket (`~760–808 B/query`, the only `M2` sub-stage never decomposed), because the preparation bucket is now fully attributed (descriptor `1 328` frozen D201-3/key semantics, plan key `≈80` on the reuse path `T317`) and the old `48 B` "rawgraph" rung is **probe-side boxing** (`GC.KeepAlive` on a 32 B `record struct`), not a product allocation; construction = `3 712` out-of-surface + `872` `#240`-mandatory clone; `R1*` and all locked ceilings retained | `.pdca/rc5/items/T245/rv3/step2-r3/errata-step2-r3.md` §E8; `.pdca/status/T245-warm-set-ops-3.md` §Current state, §Progress log
2026-10-30T15:48:58Z | DO | revision r=3 | attempt n=1 | T245 **step-2 `r=3` CHECK report persisted verbatim (evidence only, no attempt consumed)**: the complete independent `pdca-check` report of `r=3 n=1/3` is now on disk at `.pdca/rc5/items/T245/rv3/step2-r3/check-step2-r3.md` — sha256 `ac676f1f6a713a6a3ac7fa157af9edf56a21074d85e17d850f0ac035d3d643c3`, **8 366 bytes**, 74 content lines (75 with the final newline), CRLF, UTF-8, byte-identical to the report returned to the driver (no rewrite, summary or omission); it is recorded **by exact reference** in `.pdca/status/T245-warm-set-ops-3.md` §CHECK record `### Step 2 CHECK (r=3, n=1/3)` (verdict `not accepted — Step 2 only` → `→ DO`, retain `R1*` and the locked gates), curing the earlier "report not supplied" source limitation append-only; `N=3 r=3 n=1/3` stand, campaign open | `.pdca/rc5/items/T245/rv3/step2-r3/check-step2-r3.md`; `.pdca/status/T245-warm-set-ops-3.md` §CHECK record
2026-10-30T16:36Z | DO | revision r=3 | attempt n=2 | T245 **step-2 `n=2/3` lever retained — rung `R2`, `ΔB = −72.00 B/query` on both `Except` and `Intersect`**, with the execution bucket decomposed first (`760/744` = reader-open `384` + provider async-reader delta `144` + result list `88/72` + executor async terminal `72` + `DataContext` async wrapper `72`, closure `0.00`) and a paired ABBA `R1*-A → R2-B1 → R2-B2 → R1*-A2` (pinned DB, per-rung core sha256): `−72.00 B/query` in all four pairings on both operators, `Prepare()` controls `760.00/744.10 → 688.00/672.10` (not regressed), `Warm_Reused` exact `0 B`, time ratios `0.986–1.046` inside the pre-registered `±20 %` band; Tier A unmet at `1.3477×/1.3572×` (residual `764.68/805.98 B/query` to `1.20×`); gates build 0W/0E + `T240` 11/11 + cache-key 19/19/9/9/13/13 + core 3 886 + six dialect projects, all rc 0, native allocation gate `7 530/1 090/0` green inside both full-suite runs; **fail-closed integration suite did not return green**: two full runs `rc 2` with `7 934/7 732/1/201` each and exactly one ClickHouse `HttpClient.Timeout` (120.0 s) per run on a **different** test (`T286bF18DictionaryTests` setup, then `ClickHouseScalarFunctionsTests` terminal), both **green in isolation** on the same tree (`16/16`, `1/1`, rc 0) ⇒ classified **environment/infrastructure** (defect key `T245-S2-G1`), no product defect and nothing reverted; `n=2/3` spent, `n=3/3` open, `r=3` unchanged (no `r=4`), no verdict/no `PASS`, campaign open, issue #245 OPEN | `.pdca/rc5/items/T245/rv3/step2-r3/exec-decomposition/README.md`, `…/gates2/classification.md`; `.pdca/status/T245-warm-set-ops-3.md` §DO result `### Step 2 (r=3, n=2/3)`, §Defect history `T245-S2-G1`
2026-10-30T16:55Z | D | revision r=3 | attempt n=2 | T245 step-2 (r=3) CHECK -> DO **block A** (no attempt consumed, zero product change): candidate linkage binds the `n=2/3` measured/tested `R2` candidate to **`c3294de9`** (full diffs, equal blob ids + CRLF-restored sha256, empty `src tests examples benchmarks` status/diff, per-run core DLL sha256 `9e3c373e…`/`78aabc76…`, current Release artefacts `78aabc76…` @16:04:28Z, `a41a13a0…`/`093a5710…` @16:04:30Z); append-only errata `E9` (preparation **not** fully attributed: `1 704 B/query` closes `1 456`, **≈296 B/query unknown**) and `E10` (executor `E10 − E11 = 72 B/query` stays open); regime provenance recorded (invocations, probe `TieredCompilation="0"` headers, BDN host headers; **no** BDN artefact records the tiering flag ⇒ limitation stated, recovery step named but not executed); `R1*`/`R2` and ceilings untouched; next: block B full integration run, `n=3/3` open, issue #245 OPEN | `.pdca/rc5/items/T245/rv3/step2-r3/evidence-completion/{candidate-linkage-c3294de9.*,regime-provenance.*}`, `errata-step2-r3.md` §E9/§E10
2026-10-30T17:02Z | D | revision r=3 | attempt n=2 | T245 step-2 (r=3) CHECK -> DO **block B**: the canonical fail-closed integration suite returned **green** on the frozen candidate — `DOCKER_HOST` set, no `NEXTORM_SKIP_PROVIDERS`, `rc 0`, **7 934 / 7 733 / 0 failed / 201 skipped**, 10 m 58.649 s (`16:48:15Z→17:00:58Z`), no ClickHouse `HttpClient.Timeout`; pre-flight found no running container and no stale ClickHouse container/volume, so nothing was deleted or recreated and the product DB was untouched; native gate green in-run and in a targeted report re-run (`7 416`, `7 417.3`, `1 088`, `0` under `7 530 / 1 090 / 0`); `T245-S2-G1` closed **green**, the earlier two 120 s ClickHouse stalls kept as environment/infrastructure history and superseded as gate evidence (not waived); block A already committed; `n=3/3` open, `r=3`, no `r=4`, no product change, no verdict/no `PASS` | `.pdca/rc5/items/T245/rv3/step2-r3/gates3/{README.md,integration-full-blockB.log}`

2026-10-30T17:30Z | DO | revision r=3 | attempt n=2 | T245 step-2 (r=3) CHECK -> DO **block C** (no attempt consumed, zero product change): gate already green (block B, `T245-S2-G1` resolved); `n=3/3` investigation probe extension (`benchmarks/…/T245ExecDecompositionProbe.cs` `+25` lines, `E12`/`E13` controls + `IdAsync`) run `rc 0` under `DOTNET_TieredCompilation=0`, pinned DB `32bf10a0…72295`; result `E10=688/672`, `E11=616/600`, `E12=72`, `E13=72` B/query — the `72 B/query` remainder is the irreducible cost of returning a `Task<List<TResult>>`; **no in-scope lever**; `n=3/3` left **unspent**; evidence `.pdca/rc5/items/T245/rv3/step2-r3/executor-remainder/README.md` | `.pdca/rc5/items/T245/rv3/step2-r3/executor-remainder/`
2026-10-30T17:32Z | D | revision r=3 | attempt n=2 | T245 step-2 (r=3) block C commit on `1.0.9-rc5`: subject `#245 T245 step-2 (r3): integration gate green 7934/7733/0/201; n=3/3 unspent — 72 B/query Task-result remainder is irreducible; evidence .pdca/rc5/items/T245/rv3/` — exactly three paths staged by explicit `git add` (`benchmarks/nextorm.benchmark/Attribution/T245ExecDecompositionProbe.cs`, `.pdca/status/T245-warm-set-ops-3.md`, `.pdca/status/collection-rc5.md`); no `-A`, no push, no merge, no tag, no branch switch; `.pdca/rc5/**` evidence stays local/gitignored; `n=2/3` spent, `n=3/3` open/unspent, `r=3` unchanged, no `r=4`, no product change; **resume point: step-2 boundary `DO → CHECK`** | queue row `T245` notes append; `.pdca/status/T245-warm-set-ops-3.md` §Git state, §Progress log
2026-10-30T18:35Z | DO | revision r=3 | attempt n=3 | T245 step-2 (r=3) **third CHECK -> DO three blockers worked**: Block 1 accounting correction (errata §E11: product-attributed `1 408`, product-unknown `≈296`, `48` probe-only) commit `172bb703`; Block 2 process-level JIT-regime header (Program.cs) + captured held-regime headers commit `3b2d03f4`; Block 3 preparation-remainder decomposition — `≈296` = TryGetRaw comparers `216` + staged-key validation `80`, general machinery protected by `AC-245-07`, **no in-scope lever**, revert nothing, **`n=3/3` spent**, attempt budget exhausted, commit `b568b9bd`; build 0W/0E throughout; no product change; `R2` retained; ceilings `7 530 / 1 090 / 0` intact; resume point step-2 boundary `DO → CHECK` | `.pdca/status/T245-warm-set-ops-3.md`; `.pdca/rc5/items/T245/rv3/step2-r3/`
2026-10-30T (ACT) | A | revision r=3 | attempt n=3/3 | T245 ACT finalization: step 2 not accepted — campaign stopped (N=3 r=3 n=3/3); M1 (bc641498) + R2 (c3294de9) retained and uncredited; residual 764.68/805.98 B/query to 1.20×; issue #245 stays OPEN; user-directed continuation of lane G1 recorded in Decisions log | .pdca/status/T245-warm-set-ops-3.md; .pdca/status/collection-rc5.md
2026-10-30T19:00:00Z | P | revision r=3 | attempt n=1 | Process amendment recorded: evidence-round cap (user, lane G1, effective immediately) — one evidence round per criterion per revision (evidence_round=1, not a defect, not an attempt); no second evidence round on the same criterion within the same revision; CHECK then verdicts on artefacts on disk (unverified where missing, never silently met) and the driver routes. Lane-wide, all G1 items (T250, T246, T247, T311, T359, T360, T361, T268, T269, T267, T270–T274 and any later item). No plan revision, no counter, no attempt, no defect entry, no criterion, no product file. Full text: §Decisions log entry of the same date. | .pdca/status/collection-rc5.md §Decisions log; .pdca/status/T245-warm-set-ops-3.md
2026-10-30T (ACT) | A | revision r=3 | attempt n=3/3 | T245 user-accepted `limited` outcome («п1»): queue row T245 incomplete → done, terminal cell execution (lint has no limited value; limited kept in notes); AC-245-01 unmet (1.347719×/1.357154×), AC-245-03 unverified, AC-245-02 accepted under Tier B; scope M1 (bc641498) + R2 (c3294de9), gate 7 530/1 090/0, integration gate 7 934/7 733/0/201, residual 764.68/805.98 B/query; issue #245 closed as limited | .pdca/status/T245-warm-set-ops-3.md; .pdca/status/collection-rc5.md row T245
2026-10-30T19:15:00Z | D | revision r=1 | attempt n=1 | T250 DO candidate delivered — TerminalPlanCache (shared engine) + CsvPlanCacheKey + JsonRowWriterCache adapter + ResolveCsvPlan cache reuse; build 0W/0E; core CsvStreamWriterTests+JsonRowWriterCacheTests 123/123, sqlite CsvStreamTests 30/30; roadmap §2(a) corrected; evidence .pdca/T250/rv1/ | .pdca/status/T250-csv-terminal-plan-cache-1.md §DO result; src/nextorm.core/Query/TerminalPlanCache.cs, Csv/CsvPlanCacheKey.cs
2026-10-30T23:45:00Z | D | revision r=1 | attempt n=1 | T250 DO candidate implemented + measurement recorded — bounded per-context CSV terminal plan cache keyed on CSV options/storage types/sequential/projection, generation invalidation, ValueTransform bypass; 8/8 correctness tests; build 0W/0E; measured ~13.4 KB less per 3-column export, faster on all arms; 10k speedup 9 597/9 812 µs vs 10 628/10 347 µs below frozen lower bound 10 573 µs (measured miss); per-column reduction ~4.5 KB below the ≥8 KB/column claim; evidence .pdca/T250/rv1/attempts/attempt1/ | .pdca/status/T250-csv-terminal-plan-cache-1.md §DO result; .pdca/status/collection-rc5.md queue row T250
2026-10-30T23:58:00Z | D | revision r=1 | attempt n=2 | T250 DO attempt 2 — correctness gaps closed: +6 tests (14/14 green: cross-context isolation, bound <=64, ValueTransform bypass+counter, options/Clear fresh plan+content, storage-type end-to-end, byte-identical+CRLF); no product defect found; decomposition recorded (cold compile ~5 KB/col; warm WriteCsv 3-col 18.4 KB); ABBA recorded (100 rows 149.24 us/35.52 KB; 1000 rows 1036.76 us/190.20 KB; 10000 rows 9908.04 us/1736.78 KB); no product change; evidence .pdca/T250/rv1/attempts/attempt2/ | .pdca/status/T250-csv-terminal-plan-cache-1.md §DO result; tests/nextorm.core.tests/Csv/CsvTerminalPlanCacheTests.cs
2026-10-31T00:05:00Z | P | revision r=2 | attempt n=1 | T250 rc5-r2 booked: plan_revision r=1 → r=2 (CHECK → PLAN on T250-C2-01..04; n=2/3 incomplete/unaccepted); criteria AC-250-1..4 reproduced verbatim; AC-250-2 magnitude superseded (≥8 KB/col contradicted by measured 15 000 B/3-col compile object); residual list (i)–(v) recorded; r=2 n=1/3; no product change; evidence .pdca/T250/rv1/attempts/attempt2/ (14/14 tests green, rc 0) | .pdca/status/T250-csv-terminal-plan-cache-1.md; .pdca/status/collection-rc5.md
