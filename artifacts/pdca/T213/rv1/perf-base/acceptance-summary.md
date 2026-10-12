# T213 D01 — acceptance perf baseline (unmodified build, r=1/rv=1)

command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
source revision: `fc149bff2bfed855ccad81c158e871103ccbd7ae`
raw log: `acceptance.log` (stdout) — exit 0
wall time: 0m52.3s (Bootstrap+run); BenchmarkDotNet global total 44.61s, executed benchmarks: **7/7**

7/7 cases (Mean / Allocated):
1. `Nextorm_Count` — 2.160 ms / 380.47 KB (baseline 1.00)
2. `Nextorm_GroupByCount` — 57.87 ms / 50.1 MB (baseline 1.00)
3. `Nextorm_Cached` — 2.053 ms / 615.7 KB
4. `Cached_PlanOnly_Param` — 551.2 us / 513.3 KB (ratio 0.62)
5. `Prepared_ToList` — 890.6 us / 76.14 KB (baseline 1.00)
6. `Cached_ToList` — 1,899.2 us / 589.44 KB (ratio 2.13)
7. `Nextorm_Cached_ToListAsync` — 1.992 ms / 613.94 KB

cached-vs-prepared ratio (SqliteBenchmarkCachedPlan): `Cached_ToList / Prepared_ToList = 2.13x`
(the cached path allocates 7.74x `Prepared_ToList`; `Cached_PlanOnly_Param` is 0.62x).

No `>= 20%` regression claim is made at this stage (baseline only; no fix applied).

## New in-memory JoinInto harness base reading

command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*JoinIntoBenchmarks*"`
raw log: `joininto-bench.log` — exit 0, executed benchmarks: 2

| Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---:|---:|---:|---:|
| JoinInto_OneToMany_ToList | 7.289 ms | 1.00 | 3.94 MB | 1.00 |
| JoinInto_ManyToMany_ToList | 9.793 ms | 1.35 | 4.27 MB | 1.08 |

Deliberately not tagged `acceptance`, so it cannot perturb the 7-case gate.
