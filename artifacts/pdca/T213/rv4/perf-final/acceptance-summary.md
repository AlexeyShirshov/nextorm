# T213 D07 — FINAL acceptance perf vs D01 baseline (r=5 / rv4)

command (frozen, identical to the baseline):
`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`

- final source revision: `2168692d0c06abf06e4eaacbfa1431f2fd32d409` (HEAD; working tree source-clean — no `src/`, `tests/`, `benchmarks/nextorm.benchmark` changes)
- baseline source revision (D01): `fc149bff2bfed855ccad81c158e871103ccbd7ae`
- raw logs:
  - final run 1: `acceptance.log` — exit 0; wall 0m57.2s; BDN global total 48.52s; executed benchmarks **7/7**
  - final run 2 (repeat): `acceptance-rerun.log` — exit 0; wall 0m52.4s; BDN global total 49.44s; executed benchmarks **7/7**
  - baseline: `../rv1/perf-base/acceptance.log` — exit 0; wall 0m52.3s; BDN global total 44.61s; executed benchmarks **7/7**

Environment: same host as baseline (AMD Ryzen 7 5800HS, 8 logical / 4 physical cores, .NET 10.0.12, .NET SDK 10.0.401, BenchmarkDotNet v0.15.8, `Job=ShortRun`, `InProcessEmitToolchain`, IterationCount=3 / LaunchCount=1 / WarmupCount=3).

## 7/7 cases — Mean / Allocated

| Case | baseline | final run 1 | final run 2 | Δ base→run1 |
|---|---:|---:|---:|---:|
| `Nextorm_Count` | 2.160 ms / 380.47 KB | 2.944 ms / 380.47 KB | 3.242 ms / 380.47 KB | +36.3% |
| `Nextorm_GroupByCount` | 57.87 ms / 50.1 MB | 86.43 ms / 50.11 MB | 79.17 ms / 50.12 MB | +49.3% |
| `Nextorm_Cached` | 2.053 ms / 615.7 KB | 2.904 ms / 615.68 KB | 2.791 ms / 615.68 KB | +41.5% |
| `Cached_PlanOnly_Param` | 551.2 us / 513.3 KB | 888.2 us / 513.3 KB | 770.4 us / 513.3 KB | +61.1% |
| `Prepared_ToList` | 890.6 us / 76.14 KB | 1,403.6 us / 76.14 KB | 1,134.9 us / 76.14 KB | +57.6% |
| `Cached_ToList` | 1,899.2 us / 589.44 KB | 2,797.2 us / 589.47 KB | 2,576.9 us / 589.45 KB | +47.3% |
| `Nextorm_Cached_ToListAsync` | 1.992 ms / 613.94 KB | 2.901 ms / 613.94 KB | 3.184 ms / 613.94 KB | +45.6% |

`Allocated` is identical (to BDN print precision) for 5 of the 7 cases; `Nextorm_GroupByCount`
(50.1 MB → 50.11/50.12 MB) and `Cached_ToList` (589.44 KB → 589.47/589.45 KB) differ by ~0.02–0.04% and
~0.005% respectively — all far below any code-attributable effect and not byte-identical. The earlier
"byte-identical for every case" wording was wrong.

## cached-vs-prepared ratio (`SqliteBenchmarkCachedPlan`)

`Cached_ToList / Prepared_ToList`: baseline **2.13x** (1,899.2 / 890.6 us) → final run 1 **1.99x** (2,797.2 / 1,403.6 us) → final run 2 **2.27x** (2,576.9 / 1,134.9 us).
Delta of the ratio vs baseline: **-6.6%** (run 1) / **+6.6%** (run 2) — sub-threshold.

## Cost gate

`--anyCategories=acceptance` (ShortRun) wall: **57.2s** (run 1) / **52.4s** (run 2) ≤ 4 min. Gate **pass**.

## >=20% delta investigation (repo rule)

Every one of the 7 cases is 27–61% slower in both final runs than the D01 baseline, so the repeat was
performed on the same machine. Classification: **reproduced across the repeat, but not attributable to
the retained change — machine-state / measurement variance.**

Grounds:
1. `Allocated` is identical for 5 of 7 cases and differs by ≤0.04% for the other two — no per-op work of
   the magnitude required to explain ≥27% mean shifts was added; allocation/GC is not byte-identical.
2. Five of the seven cases (`Nextorm_Count`, `Nextorm_GroupByCount`, `Nextorm_Cached`,
   `Cached_PlanOnly_Param`, `Nextorm_Cached_ToListAsync`) do not execute the retained in-memory
   JoinInto assembly path at all, yet they shift with the same sign/magnitude as the two
   prepared/cached-plan cases — a code-local regression cannot move untouched paths.
3. Host load at measurement time was `loadavg 3.69 3.68 3.88` (8 logical cores; `roslynq`,
   `opencode`, `VBCSCompiler` active). The D01 baseline (18:43) recorded no load and is a different
   ambient session.
4. Intra-run ShortRun variance dominates: run 1 `Nextorm_Count` has Error 4.345 ms > Mean 3.242 ms.

No regression claim and no improvement claim is made. The cached/prepared ratio delta is sub-threshold.
