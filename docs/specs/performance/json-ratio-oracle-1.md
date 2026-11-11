# JSON streaming ratio oracle v1 (T311, #311)

Internal specification. Not linked from public docs (`docs/**`, `docs/ru/**`, `readme.md`).

## 1. Scope and freeze

- Applies to `WriteJson` / `WriteJsonAsync` JSON streaming benchmarks at 1k / 10k / 100k rows.
- Public arms: `WriteJson_Array_Dto` (NextORM) and `Linq2Db_ToList_Json` (linq2db).
- Authoritative thresholds: T311 v1 limits **1k ≤ 1.20; 10k ≤ 1.00; 100k ≤ 0.95** (time ratio vs linq2db).
- Allocation limit at 10k: **≤ 446,095 B/op** (exact bytes, no KiB rounding).
- Frozen before first live measurement; immutable thereafter. Rule changes require PLAN + rv supersession.

## 2. Measurement structure

- Two rounds per session (`round-1`, `round-2`), same pinned database (`NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`).
- **N = 9 implementation pairs per size / snapshot / round**. No optional stopping or extra confirmation runs.
- Each pair uses identical BDN jobs/configuration; alternate implementation order using a seed derived from the frozen spec digest (ABBA).
- Retain raw BDN observations, means, allocations, process results, and order.
- One benchmark process at a time; no concurrent container or build load.

## 3. Decision statistic (paired log-difference)

For each size/round, with pairs indexed i = 1..9:

- `d_i = ln(nextorm_mean_ns_i / linq2db_mean_ns_i)`
- report `exp(mean(d))` and 99% interval `exp(mean(d) ± 3.355 × sample_sd(d) / sqrt(9))`, per round separately.

PASS: every round's upper bound ≤ limit.
FAIL: both rounds' lower bounds > limit.
Otherwise INCONCLUSIVE. Never average discordant rounds.

## 4. Noise bound and stability

- Freeze noise bound `s = max(0.19, 0.20, 0.30) = 0.30` (operational ceiling from documented ~19%, ~20%, ±30% variability).
- Unstable if any paired ratio differs from its median by a multiplicative factor > 1.30, or first-three/last-three geometric means differ by > 1.30 → INCONCLUSIVE.
- Diagnostic minimum ratio `min(nextorm)/min(linq2db)`; cannot promote FAIL/INCONCLUSIVE.

## 5. Baseline→final improvement (10k)

- Differences of normalized log ratios indexed by round/sample.
- 99% interval upper bound must be < 0 in both rounds.
- Report temporal-drift limitations.

## 6. Acceptance (7/7 controls)

Controls (final-round mean ≤ 1.20× corresponding baseline-round mean; all within 240 s per acceptance round):

1. `InMemoryBenchmarkAggregates.Nextorm_Count`
2. `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount`
3. `SqliteBenchmarkAny.Nextorm_Cached`
4. `SqliteBenchmarkCachedPlan.Prepared_ToList`
5. `SqliteBenchmarkCachedPlan.Cached_ToList`
6. `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param`
7. `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync`

Collect all within 240 seconds wall time per acceptance round. Timeout fails.

## 7. Rejection rules

Reject missing/zero/duplicate samples, non-finite values, mismatched parameters/jobs, incorrect source/DB identity, fewer than nine pairs.

## 8. Exit protocol

- `0` = valid success / PASS
- `1` = FAIL
- `2` = INCONCLUSIVE / invalid
- Structured reasons distinguish invalid input.

## 9. Audit

Preserves T247 outcomes separately; an old GAP does not become a new PASS. T247 frozen thresholds/tolerances/denominators/historical GAP untouched.

## 10. cases.json schema (frozen)

Top level: `schema_version, task, contract_rv, spec_sha256, collector_sha256, snapshot, source_identity, session_id, baseline_generation, round, generated_utc, db_sha256, environment, cases[]`.

Case: `case_id, kind, implementation, parameters, pair_index, order, job_fingerprint, mean_ns, allocated_b, raw_artifacts[], command, exit_code, elapsed_seconds`.

Controls use `implementation=nextorm`; JSON uses nextorm+linq2db. Do not invent linq2db versions of nextorm-only controls.

## 11. Allocation judgment

Exact byte counts (0 B tolerance). Any decrease < 0 B is a reduction; any increase > 0 B is an increase.

## 12. Local acceptance (task closure)

If global target not reached: accept if (a) Δ_alloc < 0 B and Δ_time ≤ +ε_time, OR (b) Δ_time < -ε_time and Δ_alloc ≤ 0 B, with all 7/7 acceptance cases within band. Record residual gap to global target.
