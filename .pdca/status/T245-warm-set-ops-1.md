# T245 — Warm EXCEPT and INTERSECT allocation parity — live status

task: T245
issue: 245
issue_title: Warm EXCEPT and INTERSECT allocation parity
milestone: 1.0.9-rc3
cycle: 1
plan_revision: 2
evidence_contract_rv: 1
attempt: 1/3
max_attempts: 3
mode: autonomous
plan_file: docs/specs/status/D245-warm-set-ops-1.md
phase: DO
status: do_started
branch: 1.0.9-rc3
entry_head: 8c94207f
evidence_dir: .pdca/T245/rv1/
contract_input: .pdca/T245/rv1/contract.json
frozen_manifest: .pdca/T245/rv1/manifest.json
check_regather_budget: 2
check_regather_owner: check
created: 2026-10-18

## Units

| Unit | State | Notes |
|---|---|---|
| D245-P1 | done | Prerequisite (r=2): leak-free benchmark-host execution + entry-r1 provenance — orphaned lock cleared, canonical wrapper recorded and exercised, Release build 0 Warning(s)/0 Error(s), `bdn/entry-r1/admission.json` admitted |
| D245-1 | active | Was blocked on D245-P1; unblocked when D245-P1 closed — entry evidence + attribution decision |
| D245-2 | pending | Narrow evidence admission and native-gate coverage |
| D245-3 | blocked on D245-1 lever decision | Measured narrow optimization and semantic guards |
| D245-4 | pending | Final measurement, documentation and evidence freeze |
| Deferred | deferred in 1.0.9-rc3 | Broad metadata/CTE redesign; requires a revised PLAN |

## Measurements

| # | Block | Artifact directory | State |
|---|---|---|---|
| 1 | entry-r1 | `.pdca/T245/rv1/bdn/entry-r1` | valid (admitted r=2) |
| 2 | entry-r2 | `.pdca/T245/rv1/bdn/entry-r2` | ABORTED before measurement — orphaned host flock (inherited fd held by MSBuild `nodeReuse` servers), lock not acquired in 900 s; no artifacts/numbers, no validity verdict; see `bdn/entry-r2/abort.json` |
| 3 | final-r1 | `.pdca/T245/rv1/bdn/final-r1` | not started |
| 4 | final-r2 | `.pdca/T245/rv1/bdn/final-r2` | not started |
| 5 | controls-entry | `.pdca/T245/rv1/bdn/controls-entry` | not started |
| 6 | controls-final | `.pdca/T245/rv1/bdn/controls-final` | not started |
| 7 | decompose-entry | `.pdca/T245/rv1/bdn/decompose-entry` | not started |
| 8 | decompose-final | `.pdca/T245/rv1/bdn/decompose-final` | not started |
| 9 | native-final | `.pdca/T245/rv1/bdn/native-final` | not started |

## Gate rows

| Row | State |
|---|---|
| R245-01 | open |
| R245-02 | open |
| R245-03 | open |
| R245-04 | open |
| R245-05 | open |
| R245-06 | open |
| R245-07 | open |
| R245-08 | open |
| R245-09 | open |
| R245-10 | open |
| R245-11 | open |
| R245-12 | open |
| R245-13 | open |
| R245-14 | open |
| R245-15 | open |
| R245-16 | open |

## Evidence contract rv=1

Contract: `.pdca/T245/rv1/contract.json` (`contract_rv: 1`, `base_ref: 01815f3a`, `entry_head: 8c94207f`, `evidence_root: .pdca/T245/rv1`).

| Row | State |
|---|---|
| E245-01 | open |
| E245-02 | open |
| E245-03 | open |
| E245-04 | open |
| E245-05 | open |
| E245-06 | open |
| E245-07 | open |
| E245-08 | open |
| E245-09 | open |
| E245-10 | open |
| E245-11 | open |
| E245-12 | open |
| E245-13 | open |
| E245-14 | open |
| E245-15 | open |
| E245-16 | open |

## Progress log

- PLAN closed; DO started (2026-10-18). Re-anchor note appended; evidence root moved to .pdca/T245/rv1/.
- Bootstrap: scope validated; status file created; contract.json written.
- 2026-10-18 | DO | revision r | iteration 1/3 | scope-bootstrap validated (validate_inner_loop.py brief rc=0) | .pdca/T245/rv1/scope-bootstrap.json
- 2026-10-18 | DO | revision r | iteration 1/3 | contract.json written, 16 rows E245-01..E245-16, JSON parse rc=0 | .pdca/T245/rv1/contract.json
- 2026-10-18T16:08:26Z | DO | revision r=1 | iteration 1/3 | D245-1 preflight complete: env captured, DB pin match=2/2, C-BUILD 0 warnings/0 errors rc=0, C-CORE 211 passed/0 failed/0 skipped rc=0, C-SQL sqlite 740 / postgres 831 / sqlserver 607 / mariadb 263 passed, 0 failed, 0 skipped, rc=0 each; C-PY 108 passed/0 failed rc=0; quiet window median 19.73% p95 24.41% (not quiet, no foreign benchmark/testhost/coverage process); API entry 14 calls verbatim, six set-op methods resolved 1042/1058/1074/1090/1106/1122; scope rc=0 with rebuild deviation | .pdca/T245/rv1/measurement-environment.json
- Deviation D245-1/a: brief scope carried rebuild=affected with files=[], which `validate_inner_loop.py brief` rejects (rc=2, fictitious rebuild); recorded verbatim at .pdca/T245/rv1/logs/scope-brief-verbatim.json and corrected to rebuild=none (no compiled input changed in this step; compile coverage is the full Debug build gate), rc=0 | .pdca/T245/rv1/scope-d245-1-preflight.json
- Deviation D245-1/b: `python3 -m pytest` unavailable (`No module named pytest`); both perf test modules are stdlib unittest and document unittest as the primary runner, so they were executed as `python3 -m unittest test_t240_evidence` (12) and `test_t244_evidence` (96) from .pdca/scripts/perf/tests, rc=0 each; no install performed | .pdca/T245/rv1/tests/perf-tools.log
- Deviation D245-1/c: raw tool logs were captured as emitted and then normalized to CRLF per repo line-ending policy; no content changes. HEAD verified f82edf36 (branch 1.0.9-rc3), base 01815f3a exists.
- 2026-10-18 | DO | revision r=1 | iteration 1/3 | D245-1 instrumentation added: 10 arms in `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWarmDecompose.cs` — Except_{Construct,Prepare_NoHash,Prepare_Hash,Warm_PlanOnly,Warm_Reused} and Intersect_{...} (category Warm_Except / Warm_Intersect); 48 pre-existing arms verified byte-identical (block hash + LOC delta 0 each); build gate `dotnet build nextorm.slnx -c Debug` 0 Warning(s) 0 Error(s) rc=0; C-CORE `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~T240CommandConstructionTests` 11 passed / 0 failed / 0 skipped rc=0 | .pdca/T245/rv1/logs/decompose-arm-inventory.txt
- Deviation D245-1/d: the file carried no `BenchmarkCategory` on any pre-existing arm, so the 10 new arms introduce operation-scoped categories `Warm_Except` / `Warm_Intersect` (additive; existing arms/categories untouched). Harness discovery of the new arms is deferred to the entry round: no BenchmarkDotNet command was run in this step per the step instruction (compile-only), and the Debug build proves the methods compile into nextorm.benchmark.dll.
- 2026-10-18T16:16:27Z | DO | revision r=1 | iteration 1/3 | quiet-window admission: attempt 1 (16:16:27Z-16:16:56Z) median 19.95 % / p95 28.29 % REJECTED; 90 s sleep; attempt 2 (16:18:23Z-16:18:52Z) median 4.37 % / p95 6.68 % ADMITTED (2 of 4 attempts) | .pdca/T245/rv1/logs/entry-r1-quiet-window.txt
- 2026-10-18T16:18:52Z | DO | revision r=1 | iteration 1/3 | block entry-r1 (C-FAIR): `env -u NEXTORM_BENCH_FULL NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkFeaturesFairCached.B_* --exporters json --artifacts .pdca/T245/rv1/bdn/entry-r1`; lock `flock -x /tmp/opencode/nextorm-bdn-host.lock` held for the whole block (single lock, no inherited fd); rc=0, wall 416 s, 55 benchmarks, BDN 0.15.8, job ShortRun + InProcessEmitToolchain (NEXTORM_BENCH_FULL unset → no Default job); DB pin 32bf10a0 match 2/2 | .pdca/T245/rv1/logs/entry-r1-run.json
- 2026-10-18T16:25:48Z | DO | revision r=1 | iteration 1/3 | entry-r1 arms (ShortRun/InProcessEmitToolchain, raw B/op): EXCEPT nextorm 65281 B (63.75 KB displayed → 6.38 KB/query, 210.2 µs) vs linq2db 51762 B (50.55 KB → 5.06 KB/query, 567.8 µs), ratio 1.2612; INTERSECT nextorm 65121 B (63.59 KB → 6.36 KB/query, 248.5 µs) vs linq2db 51285 B (50.08 KB → 5.01 KB/query, 568.5 µs), ratio 1.2698; all 4 required arms present per category B_Except/B_Intersect, no missing pair; BDN emitted no validation warnings | .pdca/T245/rv1/bdn/entry-r1/summary.json
- 2026-10-18T16:25:54Z | DO | revision r=1 | iteration 1/3 | entry-r1 INVALID (reason codes HEAD_CHANGED_DURING_INTERVAL, FOREIGN_LOCK_WAITER_PROCESS): HEAD e7813beb → 08004366 (foreign commit "#297 plan: T297-cow rv2 contract, owner unfreeze override, COW acceptance" at 16:23:32Z, touching only .pdca/status/T297-cow-1.md and docs/specs/status/D297-structural-cte-graph-1.md — 0 files under src/ tests/ benchmarks/); scoped (src tests benchmarks .pdca) and full porcelain fingerprints identical before/after; foreign pid 2001250 polled `flock -n` every 15 s (lock-contention probe, not a build/test/coverage/BDN process); numbers recorded but NOT admissible as entry baseline | .pdca/T245/rv1/logs/entry-r1-foreign-monitor.txt
- 2026-10-18T16:44:20Z | DO | revision r=1 | iteration 1/3 | entry-r2 (C-FAIR) ABORTED BEFORE MEASUREMENT: the single `flock -x -w 900 /tmp/opencode/nextorm-bdn-host.lock` returned rc=1 after 877 s (16:29:43Z..16:44:20Z, 15-min acquisition budget exhausted) and the block script never executed (no `.pdca/T245/rv1/logs/entry-r2-block.txt`); no retry, no parallel run queued | .pdca/T245/rv1/logs/entry-r2-lock-wait.txt, /tmp/opencode/t245-entry-r2-outer-result.txt
- 2026-10-18T16:45:31Z | DO | revision r=1 | iteration 1/3 | entry-r2 lock root cause: the exclusive FLOCK ADVISORY WRITE on inode 187859 is orphaned — `/proc/2002215/fdinfo/3` and `/proc/2002312/fdinfo/3` read `lock: 1: FLOCK  ADVISORY  WRITE 0 08:30:187859 0 EOF` (owner pid 0), i.e. the lock lives on an inherited open file description inside two idle `MSBuild.dll /nodemode:1 /nodeReuse:true` build servers (pids 2002215/2002312, started 16:25:38Z/16:25:40Z); `/proc/locks` has no entry for 08:30:187859, `flock -n` on the path = BUSY, a fresh lock file in the same directory locks at once; no benchmark/build/testhost/coverage process executed in the window; concurrent lane T297 (pid 2006493, blocked since 16:28:05Z) has no `lock:` line in fdinfo → waiter, OBSERVATION_ONLY | .pdca/T245/rv1/bdn/entry-r2/abort.json
- 2026-10-18T16:45:31Z | DO | revision r=1 | iteration 1/3 | entry-r2 state: HEAD eceefd94695ffbec0a91fbc9cf8bf46a28da8040 unchanged, DB pin 32bf10a0…72295 verified 2/2 before launch, tracked `benchmarks/BenchmarkDotNet.Artifacts` clean; no quiet window taken and no BDN run, so the round has no alloc/mean numbers and no valid/invalid verdict (NOT measured — not "invalid") | .pdca/T245/rv1/bdn/entry-r2/abort.json
- 2026-10-18T16:45:31Z | DO | revision r=1 | iteration 1/3 | entry-r2 remedy for the re-run: acquire with `flock -x --close <lock> …` (closes the lock fd before exec so MSBuild node-reuse servers cannot inherit it) and/or `MSBUILDDISABLENODEREUSE=1` / `-nodeReuse:false`; the current stuck lock clears only when pids 2002215/2002312 exit — entry-r2 needs a new attempt after that, or an orchestrator decision on the idle nodes | .pdca/T245/rv1/logs/entry-r2-lock-wait.txt
- Correction (r=2, 2026-10-18): the entry-r1 verdict above is retained **verbatim** and is not rewritten — the round stays INVALID on its own validity flags and on its own numbers. It is now **ADMITTED on provenance** via `.pdca/T245/rv1/bdn/entry-r1/admission.json` (`admission: "admitted"`, `basis: "docs/status-only commit + lock waiter, zero measured-path diff, no concurrent execution"`): the mid-interval commit `08004366` touched only `.pdca/status/T297-cow-1.md` and `docs/specs/status/D297-structural-cte-graph-1.md` (`measured_paths_changed: []` for `src/ tests/ benchmarks/ Directory.Build.props Directory.Packages.props global.json`, working-tree fingerprints byte-identical before/after), and pid 2001250 was a `flock -n` poller that never held the lock or executed a build/test/BDN process. Measurement-table row 1 is therefore `valid (admitted r=2)`. No number, ratio, threshold, denominator or row ID is revised by this correction.
- 2026-10-18T16:49:17Z | DO | revision r=2 | iteration 1/3 | D245-P1 Step A preconditions PASS: pids 2002215/2002312 are idle `MSBuild.dll /nodemode:1 /nodeReuse:true` servers (ppid 669, no children, 1.4 % avg / 20 s CPU time), `/proc/<pid>/fdinfo/3` = `lock: 1: FLOCK  ADVISORY  WRITE 0 08:30:187859 0 EOF` (owner pid 0 = orphaned), no `dotnet build|test|run`, testhost, BenchmarkDotNet or coverage process, no active client; foreign T297 waiter 2006493 left untouched (fd 3 -> ino 187859, NO `lock:` line); `/proc/locks` has no entry for 187859; `flock -n` rc=1 BUSY; inode 187859 | .pdca/T245/rv1/logs/lock-recovery-proof.txt
- 2026-10-18T16:49:31Z | DO | revision r=2 | iteration 1/3 | D245-P1 Step B: `kill -TERM -- 2002215 2002312` rc=0; 5 s later `ps -p 2002215,2002312` rc=1 with no rows (node exit proof: both gone); `flock -n` probe rc=1 BUSY because the **foreign T297 lane acquired the released lock legitimately at that moment** (fdinfo `lock: … WRITE 2006493`, child `dotnet run … --filter *SqliteBenchmarkFeaturesFairCached.B_*`) — no SIGKILL, no unlink, no pkill, no signal to the foreign lane, the executor waited for its window; lock re-probed rc=0 at 17:27:13Z | .pdca/T245/rv1/logs/lock-recovery-proof.txt
- 2026-10-18T16:50:48Z | DO | revision r=2 | iteration 1/3 | D245-P1 Step C decision: `flock --version` util-linux 2.37.2; `-o/--close` is documented as "close the file descriptor on which the lock is held before executing command … useful if command spawns a child process which should not be holding the lock" and `--no-fork` is documented incompatible with `--close`, so the canonical wrapper uses `flock -x --close` (never `--no-fork`) with `MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` and `--disable-build-servers -nodeReuse:false` | .pdca/T245/rv1/logs/measurement-wrapper.txt, .pdca/T245/rv1/logs/lock-recovery-proof.txt
- 2026-10-18T17:26:52Z | DO | revision r=2 | iteration 1/3 | D245-P1 Step C Release build under the recovered host lock: attempt 1 waiter superseded after 1500 s behind the foreign T297 round (rc 143 = waiter SIGTERM, command never started); attempt 2 acquired 17:26:47Z and ran `dotnet build benchmarks/nextorm.benchmark -c Release --disable-build-servers -nodeReuse:false` → **rc=0, Build succeeded, 0 Warning(s), 0 Error(s)**, elapsed 4.97 s | .pdca/T245/rv1/logs/build-release-benchmark.log
- 2026-10-18T17:27:13Z | DO | revision r=2 | iteration 1/3 | D245-P1 post-build leak check: `flock -n <lock> true` **rc=0**, `lsof` on the lock empty, no `nodemode` server holds the lock, benchmark assembly sha256 unchanged `5d499f38…3f4379` (deterministic/incremental) — the hardened wrapper leaves no orphaned lock | .pdca/T245/rv1/logs/build-release-benchmark.log, .pdca/T245/rv1/logs/lock-recovery-proof.txt
- 2026-10-18T17:27:23Z | DO | revision r=2 | iteration 1/3 | D245-P1 Step D: entry-r1 admission bundle assembled from files — interval 16:18:52Z..16:25:48Z, argv verbatim, BDN 0.15.8, ShortRun + InProcessEmitToolchain, 55 benchmarks, rc=0, wall 416 s; `head_before e7813beb` → `head_after 08004366` with the full diff list (2 docs/status files, `measured_paths_changed: []`); full+scoped fingerprints byte-identical (sha256 f4e972af…, 8f3b4337…); quiet-window samples + median 4.37 / p95 6.68; foreign 2001250 waiter records; four-arm inventory per category; `admission: "admitted"` | .pdca/T245/rv1/bdn/entry-r1/admission.json
- 2026-10-18T17:27:40Z | DO | revision r=2 | iteration 1/3 | reserve ledger after D245-P1: **entry rounds — 1 admitted (entry-r1), 1 remaining valid (entry-r2 still owed); final rounds — 2 required (final-r1, final-r2)**; D245-1 unblocked and active, D245-2/D245-3/D245-4 and the deferred unit unchanged, rv=1 and N=1 unchanged | .pdca/T245/rv1/bdn/entry-r1/admission.json
