# E176.09 / C07 + C08 — performance (refreshed r=2 / n=2 final tree)

## C07 — acceptance (7 cases), post-change vs D176.1 baseline

Command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
(`perf/acceptance-postchange.txt`). **exit 0, 7 executed, 0 failures, external wall 61 s** (BDN
`Global total time` 46.97 s), under the ≤4 min budget. Baseline: `../r1-n1/baseline-acceptance.txt`
(7 cases, wall 74 s).

| Case | Baseline Mean | Post Mean | Allocated (post) | Δ Mean |
|---|---|---|---|---|
| `InMemoryBenchmarkAggregates.Nextorm_Count` | 4.404 ms | 2.721 ms | 375 KB | −38.2 % (high-variance row, not an assertion) |
| `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` | 107.3 ms | 64.38 ms | 50.1 MB | −40.0 % (high-variance) |
| `SqliteBenchmarkAny.Nextorm_Cached` | 2.968 ms | 2.058 ms | 610.21 KB | −30.7 % |
| `SqliteBenchmarkCachedPlan.Prepared_ToList` | 1,252.2 µs | 973.3 µs | 76.14 KB | −22.3 % |
| `SqliteBenchmarkCachedPlan.Cached_ToList` | 2,747.6 µs | 1,988.0 µs | 583.98 KB | −27.6 % |
| `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param` | 869.4 µs | 618.8 µs | 507.83 KB | −28.8 % |
| `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` | 2.901 ms | 2.210 ms | 619.41 KB | −23.8 % |

**Cached/prepared ratio** (`Cached_ToList / Prepared_ToList`, computed within the run):
post **2.04** vs baseline **2.20** → **−7.3 %**. The shift is a **ratio decrease** (the prepared
per-call side improved more than the cached side), i.e. the favorable direction — not a
deterioration, and the acceptance rule (`docs/specs/performance/acceptance-benchmarks.md`) triggers
investigation only on a comparable ratio **deterioration** of >20 %. Every case measured at or below
baseline; **no case regressed**. Allocated ratio 7.67 vs baseline 7.66 (unchanged).

Reproducibility probe (since the ratio moved >5 % in the favorable direction): a second Release run
of just `*SqliteBenchmarkCachedPlan*` (`perf/ratio-repro.txt`, exit 0) gives
`Prepared_ToList` 914.6 µs, `Cached_ToList` 1,840.4 µs → ratio **2.01**, confirming a stable
~2.0–2.04 rather than a one-off. The whole-run downward shift is host noise (the run was quieter
than the baseline session), consistent with the D176.6 observation; it is not a code-driven
improvement claim.

## C08 — focused category `json-stream-phase2`

Command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=json-stream-phase2`
(`perf/c08-json-stream-phase2.txt`). Benchmark source unchanged by the loop-back, but the underlying
writer path changed materially (`JsonRowWriterFactory.ReadValue` now prefers a typed
`GetFieldValue<byte[]>`, `DBNull.Value` array elements map to JSON `null`, `MaxShapeDepth` guard), so
the category was **re-executed** on the final tree rather than carried over.
**exit 0, 24 executed, 0 failures, wall 187 s.** Workloads: flat Phase 1, nested object,
conditional null-arm/all-null object, joined slots (`Projection<T1,T2>`), `byte[]` Base64 (SQLite
end-to-end), plus a writer-level native `int[]`/jagged-array path over a fake `IDataRecord`.

Each end-to-end workload is contrasted with the materialize+STJ oracle on the same data
(`ToList()` then `JsonSerializer.SerializeToUtf8Bytes`). Row counts 1 000 / 10 000 expose
preparation (flat, per call) vs row-loop (linear) cost.

| Workload @10 000 | Stream Mean | Stream Alloc | Oracle Mean | Oracle Alloc |
|---|---|---|---|---|
| Flat | 7,278.3 µs | 240.1 KB | 4,840.3 µs | 858.15 KB |
| Nested object | 10,168.7 µs | 272.55 KB | 7,108.9 µs | 1,864.69 KB |
| Conditional / all-null | 7,898.1 µs | **50.77 KB** | 4,949.1 µs | 1,386.17 KB |
| Joined slots | 13,908.6 µs | 577.86 KB | 8,511.6 µs | 1,466.17 KB |
| `byte[]` Base64 | 4,725.3 µs | 874.52 KB | 4,371.9 µs | 1,831.45 KB |

**No whole-result buffering.** Streaming writes each row straight to the caller-owned sink: the
conditional/all-null path allocates **50.68 KB at 1 000 rows and 50.77 KB at 10 000 rows**
(constant, not linear in the result) and every streaming allocation is materially below the
materialize+STJ oracle, which retains the full list. The time gap on the SQLite end-to-end workloads
is the documented per-call preparation of `WriteJson` (`DataContext.PrepareJsonStream` clones and
prepares with `storeInCache:false`) versus the plan-cached `ToList` oracle — a phase-1 property,
unchanged by #176; C07 shows no regression on the cached path. Throughput is the inverse of Mean
(e.g. flat ≈1.37 M rows/s at 10 000 rows). Numbers are consistent with the D176.6 run within
`ShortRun` noise.

Writer-level native arrays (256-element `int[]`, 16-row jagged `int[][]`): `NativeArrayInt_Stream`
6.680 µs / 11,208 B vs oracle 2.059 µs / 1,360 B; `NativeArrayJagged_Stream` 1.575 µs / 1,552 B vs
oracle 1.407 µs / 560 B. Recorded finding (not a blocker, not introduced as a regression): the array
writer reads elements through `Array.GetValue(int)`, which boxes each value-type element; tracked here
as a potential future optimisation in the array path.

## Artifacts

`perf/acceptance-postchange.txt`, `perf/ratio-repro.txt`, `perf/c08-json-stream-phase2.txt`; BDN
originals under `BenchmarkDotNet.Artifacts/**`.
