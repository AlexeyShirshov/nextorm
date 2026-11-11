# JSON streaming ratio oracle v2 (T311, #311)

Internal specification. Not linked from public docs (`docs/**`, `docs/ru/**`, `readme.md`).
Supersedes the *measurement protocol* of `json-ratio-oracle-1.md` for T311 N=2 while leaving
`json-ratio-oracle-1.md` and all T247 frozen thresholds/tolerances/denominators/historical GAP
untouched (audit-only reference).

## 1. Scope and freeze

- Applies to `WriteJson` / `WriteJsonAsync` JSON streaming benchmarks at 1k / 10k / 100k rows.
- Public arms: `WriteJson_Array_Dto` (NextORM) and `Linq2Db_ToList_Json` (linq2db).
- Authoritative thresholds (unchanged from v1): T311 limits **1k ≤ 1.20; 10k ≤ 1.00; 100k ≤ 0.95**
  (time ratio vs linq2db). Allocation limit at 10k: **≤ 446,095 B/op** (exact bytes).
- Frozen before first live measurement; immutable thereafter. Rule changes require PLAN + rv supersession.

## 2. Measurement structure (capture-order fix)

- **Baseline-first ordering (the v1 capture-order flaw fix):** The pre-change baseline (round-1 and
  round-2) MUST be captured on the retained tree (`2ea3ef45`) BEFORE any product edit or candidate
  commit. Candidate rounds are captured only after. A run whose baseline was captured after a product
  change is invalid and yields 0 valid pairs. Source/DB identity and HEAD fingerprint are recorded
  per round and must not move within a round.
- Two paired rounds per session (`round-1`, `round-2`), same pinned database
  (`NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`), one benchmark process at a time, no concurrent
  container or build load.
- Paired ABBA: within each round, baseline and candidate legs run back-to-back in the same session;
  implementation order alternates by round seed. At least **2 paired pairs per size** are required
  (N=2 minimum); N=9 is no longer mandated because it was infeasible and produced 0 valid pairs.
- Retain raw BDN observations, means, allocations, process results, and order.

## 3. Decision statistic (paired log-difference)

For each size/round, with pairs indexed i = 1..n (n >= 2):

- `d_i = ln(nextorm_mean_ns_i / linq2db_mean_ns_i)`
- report `exp(mean(d))` and, when n >= 3, the 99% interval `exp(mean(d) ± t * sd(d) / sqrt(n))`.
- With n = 2, report both per-round ratios and their direction; a claim requires both rounds to
  agree in direction. Never average discordant rounds.

PASS: every round's ratio <= limit.
FAIL: both rounds' ratios > limit.
Otherwise INCONCLUSIVE.

## 4. Noise bound and stability

- Freeze noise bound `s = 0.30` (operational ceiling, unchanged from v1).
- Unstable if any paired ratio differs from its median by a multiplicative factor > 1.30, or
  first-half/last-half geometric means differ by > 1.30 -> INCONCLUSIVE.
- Diagnostic minimum ratio `min(nextorm)/min(linq2db)`; cannot promote FAIL/INCONCLUSIVE.

## 5. Baseline->final improvement (1k/10k)

- Differences of normalized log ratios indexed by round/sample.
- Improvement requires both rounds to show the same direction; report temporal-drift limitations.

## 6. Acceptance (7/7 controls)

Controls (final-round mean <= 1.20x corresponding baseline-round mean; all within 240 s per
acceptance round):

1. `InMemoryBenchmarkAggregates.Nextorm_Count`
2. `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount`
3. `SqliteBenchmarkAny.Nextorm_Cached`
4. `SqliteBenchmarkCachedPlan.Prepared_ToList`
5. `SqliteBenchmarkCachedPlan.Cached_ToList`
6. `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param`
7. `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync`

Collect all within 240 seconds wall time per acceptance round. Timeout fails.

## 7. Rejection rules

Reject missing/zero/duplicate samples, non-finite values, mismatched parameters/jobs, incorrect
source/DB identity, fewer than 2 pairs, or baseline captured after a product change.

## 8. Exit protocol

- `0` = valid success / PASS
- `1` = FAIL
- `2` = INCONCLUSIVE / invalid
- Structured reasons distinguish invalid input.

## 9. Audit

Preserves T247 outcomes separately; an old GAP does not become a new PASS. T247 frozen
thresholds/tolerances/denominators/historical GAP untouched.

## 10. cases.json schema (frozen)

Top level: `schema_version, task, contract_rv, spec_sha256, collector_sha256, snapshot,
source_identity, session_id, baseline_generation, round, generated_utc, db_sha256, environment,
cases[]`.

Case: `case_id, kind, implementation, parameters, pair_index, order, job_fingerprint, mean_ns,
allocated_b, raw_artifacts[], command, exit_code, elapsed_seconds`.

Controls use `implementation=nextorm`; JSON uses nextorm+linq2db. Do not invent linq2db versions of
nextorm-only controls.

## 11. Allocation judgment

Exact byte counts (0 B tolerance). Any decrease < 0 B is a reduction; any increase > 0 B is an increase.

## 12. Local acceptance (task closure)

If global target not reached: accept if (a) Δ_alloc < 0 B and Δ_time ≤ +ε_time, OR (b) Δ_time <
-ε_time and Δ_alloc ≤ 0 B, with all 7/7 acceptance cases within band. Record residual gap to global
target.

## 13. Capture-order rule (new in v2)

- Baseline artifacts (`baseline/round-{1,2}/acceptance/cases.json`,
  `baseline/round-{1,2}/public-json/cases.json`) must be written before any candidate/product change.
- Candidate artifacts are written after. The baseline generation id and HEAD fingerprint are recorded
  in each cases.json; a candidate round whose baseline_generation differs from the captured baseline
  is invalid.
