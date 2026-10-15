# M12 #3 — verified preparation reuse on the cached path: DO results and evidence (T201 / #201, D201-5)

**Status: provisional DO outcome only — no final PASS.** The five Category B parity rows are a
**measured miss** (allocations FAIL) and the sixteen-row DO→CHECK manifest is therefore a truthful
**diagnostic** manifest with five `open` rows. CHECK owns the verdict.

## 1. Tested tree and environment

| Item | Value |
|---|---|
| Branch / HEAD | `1.0.9-rc3` @ `a21e5d89e6c22bcdb7bea7dae11458301ae729b3` (`head_tree 3356ffb9…`) |
| Candidate summary tree | 69 porcelain entries, `status_sha256 090f6a273541876339b2237d95f8784d89da6fec0e177e84469c64e3634aa07f` (`artifacts/pdca/T201/rv1/candidate/summary.json`) |
| Manifest `tree` field | `1.0.9-rc3@a21e5d89… status:090f6a273541` (same digest; recorded at the DO→CHECK boundary) |
| Baseline tree (D201-2) | `1.0.9-rc3` @ `bff40da2dec692b28a9788de0e22f1b4ce8386a9` |
| SDK / runtime | .NET SDK `10.0.401`, `net10.0` |
| Host | Linux 6.18.33.2-microsoft-standard-WSL2 x86_64, 8 vCPU, AMD Ryzen 7 5800HS |
| Competitor | LINQ to DB `6.5.0` (`Directory.Packages.props:13-14`), unchanged |
| Benchmarks | `SqliteBenchmarkFeaturesFairCached.B_*` (55 arms, exact ten-arm map), seven `acceptance` cases, unchanged bodies |
| Native gate | `eng/perf/iteration14_gate.py` + `eng/perf/iteration14-budgets.json`, unchanged (28 rows, `logical_ops_per_invocation = 100`, `bdn_operations_per_invoke = 1`) |

Note on the digest: `tree_identity()` hashes the `git status --porcelain` text, not file contents.
The unit's two tracked writes after this boundary (`docs/specs/performance/m12-3-prepare-command-results.md`,
`docs/specs/status/D201-cached-parity-1.md`) add porcelain lines and therefore change that digest, but they
touch no measured input. The measurement itself ran on `a21e5d89` plus the wrapper edits below.

Unit-local tooling writes (no product `src/**` change in this unit):

- `eng/perf/t201_evidence.py` — (a) the API `capture` block is now phase-aware
  (`pre_production = label == "baseline"`; candidate `source_revision = T201-D201-5-r2rv1`), fixing the
  D201-4 observation that `--phase candidate` hard-coded `pre_production: true`; (b) `stage_manifest` now
  derives a row's status from its own measured decision — EC201-02..06 from `reports/parity.json`,
  EC201-07 from both phases' fresh gate evidence + reconciliation exits, EC201-08 from the seven active
  cases and any unresolved >20 % control trip — so an `open` row can never be silently reported `met`
  because a report file merely exists. Raw BDN reports are attached for EC201-07/08 instead of the
  inventory-only paths.
- No `src/**`, benchmark body, prepared status, data/job/runtime, gate budget, gate code, tolerance or
  oracle was changed.

## 2. Exact commands, exits and wall times

Stage runner: `python3 eng/perf/t201_evidence.py --root artifacts/pdca/T201/rv1` (see `runs.json` for the
argument arrays, env set/unset, exits, wall times and log digests).

| Stage | Command (argv) | Exit | Wall |
|---|---|---:|---:|
| W-S | `eng/perf/t201_evidence.py --stage self-test --root artifacts/pdca/T201/rv1` | 0 | 0.3 s (46/46 cases) |
| W-C | `NEXTORM_T201_QUARANTINE=1 eng/perf/t201_evidence.py --stage candidate --root artifacts/pdca/T201/rv1` | 0 | 1744 s |
| W-P | `eng/perf/t201_evidence.py --stage parity --root artifacts/pdca/T201/rv1` | **1** | <1 s |
| W-W | `eng/perf/t201_evidence.py --stage warm-gate --root artifacts/pdca/T201/rv1` | 0 | 0.1 s |
| W-A | `eng/perf/t201_evidence.py --stage acceptance --root artifacts/pdca/T201/rv1` | 0 | <1 s |
| W-API | `eng/perf/t201_evidence.py --stage api --root artifacts/pdca/T201/rv1` | 0 | 22 s |
| W-Q | `eng/perf/t201_evidence.py --stage coverage --root artifacts/pdca/T201/rv1` | 0 | 43 s |
| W-M | `eng/perf/t201_evidence.py --stage manifest --root artifacts/pdca/T201/rv1` | **1** (5 open rows) | <1 s |
| V | `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T201/rv1/manifest.json` | 0 (schema only) | — |

Inner commands invoked by **W-C** (all with real exit codes; logs under `artifacts/pdca/T201/rv1/candidate/`):

| Inner | Command | Exit | Wall |
|---|---|---:|---:|
| `candidate/build-release` | `dotnet build -c Release` | 0 | 49.5 s |
| `candidate/category-b` | `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkFeaturesFairCached.B_* --exporters json --artifacts artifacts/pdca/T201/rv1/candidate/category-b` | 0 | 808.0 s |
| `candidate/acceptance` | `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --exporters json --artifacts artifacts/pdca/T201/rv1/candidate/acceptance` | 0 | 119.2 s |
| `candidate/gate-live` | `env -u NEXTORM_BENCH_FULL python3 eng/perf/iteration14_gate.py --manifest eng/perf/iteration14-budgets.json --reports-dir benchmarks/BenchmarkDotNet.Artifacts/results` | 0 | 766.6 s |
| `candidate/gate-report` | same `+ --no-run` | 0 | 0.05 s |

Baseline order of magnitude is unchanged (D201-2: build 2.7 s, B-BASE 779.9 s, A-BASE 130.3 s,
G-LIVE 780.0 s, G-REPORT 0.05 s; the candidate build is longer only because D201-3 changed `src/**`).
55/55 Category B arms and 7/7 acceptance cases were selected in both phases
(`oracle/reconciliation.json`).

Benchmark-project guard (`instrumentation/benchmark-project-quarantine-candidate.json`): three foreign
stale `nextorm.benchmark.csproj` copies under `artifacts/pdca/D208/**` and `artifacts/pdca/D236-*/**`
were quarantined by byte-preserving rename (fail-closed opt-in `NEXTORM_T201_QUARANTINE=1`) and
**restored with zero problems**. `benchmarks/BenchmarkDotNet.Artifacts` was byte-identical before/after
the whole unit: 302 files, 0 differing, `git status --porcelain` for it identical at 18 entries
(`reports/unit-boundaries/D201-5-bdn-files.{start,end}.json`,
`D201-5-git-status-bdn.{start,end}.txt`).

## 3. Category B — the five time/allocation decisions (frozen rule)

Rule (design §"Executable ratio and noise decisions", unchanged): unrounded machine-readable values from
matching jobs; `point = nx.mean/linq.mean`, `ratio_low = nx.ci_low/linq.ci_high`,
`ratio_high = nx.ci_high/linq.ci_low`; time **MET** iff `point ≤ 1.05` **and** `ratio_high ≤ 1.05`;
**FAIL** iff `point > 1.05` **and** `ratio_low > 1.05`; else **OPEN**. Allocations on raw bytes/op:
`linq == 0 → MET iff nx == 0 else FAIL`, else `MET iff nx ≤ linq`. Both arms exported the same
confidence-level identifier (12) and the interval is arithmetically at least a two-sided 99 % t-interval
(`Margin/StandardError` 4.07–4.44 vs `t_crit_99` 2.95–3.11 for df 11–15). No division by the gate's
100-operation normalization.

Two valid rounds (the required baseline and candidate full jobs), neither invalidated — no operational
reason existed and unfavourable ratios are not a reason.

| Scenario | Round | time point | ratio_low | ratio_high | time | NextORM B/op | LINQ B/op | allocations |
|---|---|---:|---:|---:|---|---:|---:|---|
| Cte | baseline | 0.7183 | 0.7078 | 0.7289 | MET | 145 132 | 92 802 | FAIL |
| Cte | candidate | 0.5286 | 0.5141 | 0.5438 | MET | 115 130 | 92 802 | FAIL |
| RecursiveCte | baseline | 0.6715 | 0.6673 | 0.6758 | MET | 167 684 | 85 922 | FAIL |
| RecursiveCte | candidate | 0.5780 | 0.5709 | 0.5852 | MET | 144 243 | 87 522 | FAIL |
| Except | baseline | 0.5386 | 0.5354 | 0.5418 | MET | 84 564 | 52 960 | FAIL |
| Except | candidate | 0.5006 | 0.4981 | 0.5032 | MET | 71 523 | 51 760 | FAIL |
| Intersect | baseline | 0.5454 | 0.5380 | 0.5530 | MET | 84 404 | 52 800 | FAIL |
| Intersect | candidate | 0.4795 | 0.4695 | 0.4897 | MET | 71 363 | 51 280 | FAIL |
| Distinct | baseline | 0.6001 | 0.5884 | 0.6120 | MET | 50 720 | 40 080 | FAIL |
| Distinct | candidate | 0.5230 | 0.5155 | 0.5306 | MET | 43 440 | 40 080 | FAIL |

Across rounds: `time_decisions = [MET, MET]` and `allocation_decisions = [FAIL, FAIL]` for **all five**
scenarios. Aggregation: *MET only if every round is MET for time and allocations; FAIL if every time
round is decisively FAIL or any round fails allocations.* Time is not decisively FAIL in any round, but
allocations fail in **every** round, therefore

**Decision for EC201-02/R201-B-CTE, EC201-03/R201-B-RECURSIVE, EC201-04/R201-B-EXCEPT,
EC201-05/R201-B-INTERSECT, EC201-06/R201-B-DISTINCT: FAIL (measured miss).**

The candidate does reduce allocations versus the baseline, but not to the competitor's level:

| Scenario | baseline B/op | candidate B/op | Δ vs baseline | candidate over LINQ |
|---|---:|---:|---:|---:|
| Cte | 145 132 | 115 130 | −20.7 % | +24.1 % |
| RecursiveCte | 167 684 | 144 243 | −14.0 % | +64.8 % |
| Except | 84 564 | 71 523 | −15.4 % | +38.2 % |
| Intersect | 84 404 | 71 363 | −15.5 % | +39.2 % |
| Distinct | 50 720 | 43 440 | −14.4 % | +8.4 % |

Two further targeted full-job rounds (the remaining DO measurement budget) were **not** run: the rule
already fixes the across-round outcome (a round can never un-FAIL; `MET` needs every round `MET`, `FAIL`
needs only one allocation failure), so extra rounds could not change the decision, and re-running only
the NextORM arm is forbidden. Every raw report and both valid rounds are preserved under
`candidate/category-b/results/**` and `baseline/category-b/results/**`; decisions in
`reports/parity.json` / `reports/parity.md`.

## 4. Native iteration14 gate (unchanged 28-row × 2-toolchain oracle)

Run against the candidate inside W-C as contracted (`NEXTORM_BENCH_FULL` unset so the `Default` and
`InProcessEmitToolchain` jobs both exist), then reconciled report-only in W-W.

- `G-LIVE` exit **0** (766.6 s), `G-REPORT --no-run` exit **0** (0.05 s);
  W-W report-only reconciliation against each phase's fresh reports exit **0** (baseline 0.05 s / candidate 0.05 s).
- Candidate: **56/56** row/jobs covered — 28 rows × {`Default`, `InProcessEmitToolchain`}; all rows `ok`;
  `row_count == expected_row_count == 56`.
- The three zero-kind rows (`Cte_Warm_Reused`, `RecursiveCte_Warm_Reused`, `Join4_Warm_Reused`) are
  exactly **0 B** on both toolchains.
- `InAtIn_Inline_Warm_Reused` is the **840 B/op budget** row (not a fourth zero row): measured
  744.00 / 744.01 B/op (baseline identical), within budget.
- Normalization unchanged: divisor 100, `bdn_operations_per_invoke = 1`.
- Provenance: only the three pinned classes were archived (18 files per phase), fresh reports copied to
  `candidate/iteration14/fresh/` (18 files, hashes in `fresh-pinned-reports.json`), and the historical
  tracked reports restored byte-identically — `restore-verification.json` and the 302-file/0-differing
  snapshot above; `git status` unchanged at 18 entries.

Evidence: `candidate/iteration14/{gate-live.log,gate-report.log,gate-evidence.json,fresh/**,archive/**,reconciliation/**}`,
`reports/iteration14.json`.

## 5. Seven-case acceptance (W-A)

Active cases: exactly 7, none substituted, the build-excluded eighth case
(`SqlServerBufferedNumericAcceptanceBenchmark`) not run. `error < mean` and `ci_low > 0` for all cases →
**no case is noisy**.

| Case | phase | Mean (ns) | Error | Allocated (B/op) | CI |
|---|---|---:|---:|---:|---|
| `InMemoryBenchmarkAggregates.Nextorm_Count` | baseline / candidate | 2 260 026.9 / 2 152 750.2 | 8 109 / 1 141 | 392 000 / 392 000 | >0 |
| `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` | baseline / candidate | 55 794 048.6 / 53 720 806.5 | 136 168 / 91 367 | 52 537 756 / 52 545 722 | >0 |
| `SqliteBenchmarkAny.Nextorm_Cached` | baseline / candidate | 1 695 894.0 / 1 688 931.0 | 1 770 / 1 491 | 632 837 / 632 837 | >0 |
| `SqliteBenchmarkCachedPlan.Prepared_ToList` | baseline / candidate | 880 930.1 / 883 952.2 | 869 / 775 | 77 960 / 77 960 | >0 |
| `SqliteBenchmarkCachedPlan.Cached_ToList` | baseline / candidate | 1 624 918.0 / 1 392 151.9 | 2 843 / 3 018 | 605 973 / 465 970 | >0 |
| `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param` | baseline / candidate | 459 780.3 / 333 908.9 | 1 278 / 581 | 528 011 / 388 008 | >0 |
| `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` | baseline / candidate | 1 622 667.1 / 1 668 085.5 | 2 642 / 5 552 | 631 053 / 682 254 | >0 |

Cached/prepared ratios (`Cached_ToList / Prepared_ToList`), with the recorded historical same-host
ratios 1.87 (time) and 7.42 (allocations):

| Ratio | baseline | candidate | candidate vs historical | candidate vs baseline |
|---|---:|---:|---:|---:|
| time | 1.8445, conservative CI [1.8237, 1.8655] | 1.5749, CI [1.5549, 1.5951] | −15.8 % | −14.6 %, CI [−16.7 %, −12.5 %] |
| allocations | 7.7729 (exact ints) | 5.9770 (exact ints) | −19.4 % | −23.1 % |

**Control/noise investigation resolution.** The design's trigger is: investigate if the point growth
exceeds 20 % **or** the uncertainty does not exclude growth above 20 %. The conservative growth interval
for the time ratio is entirely below 20 % (upper bound −12.5 %), and the allocation ratio's point growth
is −23.1 % (allocation counts are exact integers with no exported interval; the observed run-to-run
competitor drift is ≤3.1 %). Both therefore **exclude** growth above 20 % → the trigger does not fire and
**no unresolved control trip remains**; the wrapper records zero `investigate: true` trips
(`reports/acceptance.json`). `Prepared_ToList` is a **no-regression control only**: its time is +0.34 %
(883 952 vs 880 930 ns, far inside the exported CI overlap) and its allocation is **exactly unchanged**
(77 960 B/op), so the control holds; it is never evidence of Category B parity.

Honest observation for CHECK (not a design-mandated decision): `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync`
allocations moved **+8.11 %** (631 053 → 682 254 B/op) and time **+2.80 %** (beyond the exported error)
between the two phases, while its repeat-run competitor drift is smaller. The design defines no
investigation budget for a single acceptance case outside the cached/prepared control, so it is reported
as an observation and left for CHECK rather than resolved by a further ad-hoc round.

Manifest-driven decision for EC201-08: seven active cases in both phases, no unresolved trip → **MET**.

## 6. Public API (W-API)

- `dotnet build tests/nextorm.core.tests -c Debug` exit 0 (19.7 s); export test exit 0.
- Candidate inventory: **298 public types** across 4 assemblies (`nextorm.core` 285, `nextorm.postgres` 4,
  `nextorm.sqlserver` 4, `nextorm.clickhouse` 5), all `1.0.9.0`; captured with
  `pre_production: false`, `source_revision: T201-D201-5-r2rv1`.
- Baseline export kept as captured pre-production (`pre_production: true`, head `da9b8100`,
  on-disk sha256 `a9f60e83d7505e3c9503928385867db3c434669e5905d1e87ad56492283c46c5`).
- `api/diff.json`: **`equal = true`** — added 0, removed 0, changed 0 → identical public surface across
  assemblies/types/members; `Prepare()` unchanged, no new required public preparation.

The inventory `sha256` of the candidate export (`c1613bba…`) differs from the digest D201-4 recorded
(`9d2e3e35…`) although the public surface is identical; the digest covers the canonical inventory
including the export `label`, and the surface comparison (`api_diff`) is label-independent, so the
required result (equal public surface) is unaffected.

## 7. Coverage (W-Q)

Re-collected once (the tree is not byte-identical, even though no `.cs` input changed):
`dotnet tool restore` exit 0 → `dotnet-coverage collect -s coverage.settings.xml … "dotnet test --no-build --verbosity normal"`
exit 0 (38.0 s, total 12 080 / failed 0 / skipped 3 171) → `reportgenerator` exit 0 (4.7 s).

- **Line coverage 87.5 %** (threshold 85), **Branch coverage 79.2 % (29 785 / 37 571)** (threshold 75),
  Method 78.7 %. Warning-only on `1.0.9-rc3` — identical to the D201-4 collect.
- The six touched-branch families without a directly targeted test are enumerated in `reports/quality.md`.

## 8. Sixteen-row DO→CHECK manifest

`artifacts/pdca/T201/rv1/manifest.json` enumerates all sixteen `required_rows` exactly once; `contract_rv`
integer 1; no `na`; every `met` row has existing, non-empty evidence paths (typed evidence re-validated
by the wrapper), and the five parity rows are `open` with their measured reason.

| Row | Status | Evidence / reason |
|---|---|---|
| EC201-01 | met | `reviews/design.md`, `reviews/plan.md` |
| EC201-02 | **open** | Cte: frozen rule decides FAIL over 2 valid rounds — time [MET, MET], allocations [FAIL, FAIL] |
| EC201-03 | **open** | RecursiveCte: same |
| EC201-04 | **open** | Except: same |
| EC201-05 | **open** | Intersect: same |
| EC201-06 | **open** | Distinct: same |
| EC201-07 | met | fresh pinned reports from both phases, `gate-evidence.json` ×2, `iteration14-inventory.json`, `reports/iteration14.json` |
| EC201-08 | met | `oracle/acceptance.json` ×2 phases, 10 raw acceptance reports, `reports/acceptance.json` |
| EC201-09 | met | `reports/correctness.md` (D201-4) |
| EC201-10 | met | `reports/shared-command.md` |
| EC201-11 | met | `reports/temp-tvp.md` |
| EC201-12 | met | `api/baseline.json`, `api/diff.json` |
| EC201-13 | met | `reports/cache-contract.md` |
| EC201-14 | met | `reports/recipe-consumers.md` |
| EC201-15 | met | `reports/quality.md`, `baseline/build.log`, `instrumentation/self-test.json` |
| EC201-16 | met | `discovery/{ownership-state,shared-route,m9-inventory,siblings}.md`, `reviews/collection-reconciliation.md`, `reports/manifest-validation.md` |

- `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T201/rv1/manifest.json` → **exit 0**
  (schema valid only; open rows are schema-valid input).
- The wrapper's stricter local semantic validation (`reports/manifest-validation.json`) → `pass = false`
  with exactly the five intended problems (`EC201-02..06: status 'open' forbids PASS`); every `met` row
  and every typed evidence item validated with **0** additional problems. W-M therefore exits **1**.
- `artifacts/pdca/T201/rv1/manifest.boundary.json` is the frozen DO→CHECK snapshot (schema exit 0).

Boundary observations carried to CHECK:

1. EC201-09/10/11/13/14 evidence is D201-4's boundary test execution (core 2562/2562, container
   integration 3978 total / 0 failed / 0 provider skips with all 48 `CachedPreparation` cases executed,
   `reports/correctness.md` + `tests/unit-boundaries/D201-4/boundary-*.log`). The wrapper's own
   `--stage tests` (W-T) was **not** invoked in this unit (it is outside the D201-5 stage set) and its
   `tests/results.json` slot is therefore absent.
2. EC201-07's gate evidence is measured in the candidate's own fresh run; the historical tracked reports
   were only restored, never used as post-change evidence.
3. The `api/candidate.json` inventory digest differs from D201-4's record (label-covered digest; surface
   identical) — see §6.

## 9. Outcome

- **Met:** the unchanged native gate (EC201-07), the seven-case acceptance with the cached/prepared
  control (EC201-08), the public-surface equality (EC201-12), coverage thresholds (EC201-15 portion) and
  the completeness/order rows.
- **Measured FAIL / open:** EC201-02..06 — time is MET on both rounds for all five scenarios, but raw-byte
  allocations FAIL on both rounds for all five (NextORM 43 440–144 243 B/op versus LINQ to DB
  40 080–92 802 B/op). Under the frozen rule this is a decisive overall FAIL, not an OPEN.
- No tolerance, benchmark body, prepared-status semantic, gate budget, or oracle was changed to reach this
  result; no round was discarded. This is a real performance finding and requires a real replan
  (DO→PLAN), not a revision-name reset. Performance parity is **not** claimed and no final PASS is issued.
