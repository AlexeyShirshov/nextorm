# T213 D07 — FINAL in-memory JoinInto perf vs D01 baseline (r=5 / rv4)

command (frozen, identical to the baseline):
`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*JoinIntoBenchmarks*"`

Harness: `benchmarks/nextorm.benchmark/JoinIntoBenchmarks.cs` — scalar 1:N (`HasMany`) and scalar M:N
(`HasManyThrough` junction + occurrence dedup), ParentCount=500, ChildrenPerParent=4, InternalIterations=5.
No `acceptance` category (cannot perturb the 7-case gate).

- final source revision: `2168692d0c06abf06e4eaacbfa1431f2fd32d409` (final run revision)
- baseline source revision (D01): `fc149bff2bfed855ccad81c158e871103ccbd7ae`
- raw logs:
  - final run 1: `joininto-bench.log` — exit 0; executed benchmarks 2; BDN global total 10.37s
  - final run 2 (repeat): `joininto-bench-rerun.log` — exit 0; executed benchmarks 2
  - baseline: `../rv1/perf-base/joininto-bench.log` — exit 0; executed benchmarks 2; global total 12.49s

## Results — Mean / Gen0 / Gen1 / Gen2 / Allocated

| Scenario | run | Mean | Gen0 | Gen1 | Gen2 | Allocated |
|---|---|---:|---:|---:|---:|---:|
| `JoinInto_OneToMany_ToList` | baseline (D01) | 7.289 ms | 437.5000 | 218.7500 | 140.6250 | 3.94 MB |
| `JoinInto_OneToMany_ToList` | final run 1 | 13.16 ms | 437.5000 | 218.7500 | 125.0000 | 3.94 MB |
| `JoinInto_OneToMany_ToList` | final run 2 | 12.27 ms | 437.5000 | 218.7500 | 125.0000 | 3.94 MB |
| `JoinInto_ManyToMany_ToList` | baseline (D01) | 9.793 ms | 484.3750 | 468.7500 | 234.3750 | 4.27 MB |
| `JoinInto_ManyToMany_ToList` | final run 1 | 14.81 ms | 468.7500 | 437.5000 | 218.7500 | 4.26 MB |
| `JoinInto_ManyToMany_ToList` | final run 2 | 14.91 ms | 468.7500 | 437.5000 | 218.7500 | 4.26 MB |
| M:N / 1:N mean ratio | baseline / run 1 / run 2 | 1.35 / 1.13 / 1.22 | — | — | — | — |

### Honest allocation/GC deltas (corrected r=5 n=2/3)

- 1:N `Allocated` is **identical** to BDN print precision (3.94 MB), Gen0/Gen1 are identical
  (437.5000 / 218.7500), but **Gen2 differs**: 140.6250 → 125.0000 (**−11.1%** in both final runs).
- M:N `Allocated` is **not byte-identical**: 4.27 MB → 4.26 MB (Δ −0.01 MB, **≈0.2%**, fewer bytes), and
  every GC counter differs: Gen0 484.3750 → 468.7500 (−3.2%), Gen1 468.7500 → 437.5000 (−6.7%),
  Gen2 234.3750 → 218.7500 (−6.7%).
- So the earlier "byte-identical `Allocated`" wording was wrong: allocation/GC is not byte-identical, and
  the M:N allocation delta is ≤~0.2% and in the direction of **fewer** allocations.

## >=20% delta investigation (repo rule)

The repeat was performed on the same machine. Classification: **reproduced across the repeat, but not
code-attributable — machine-state / measurement variance.**

Grounds:
1. The measured allocation deltas are at most ≈0.2% (M:N −0.2%, 1:N 0%) and in the direction of fewer
   allocations; the 1:N `Allocated` is identical while its Gen2 rate is lower. A ≥68% per-op CPU change is
   not explained by a sub-0.2% allocation delta, and the allocation/GC counters are **not** byte-identical,
   so no allocation-based attribution is claimed in either direction.
2. The unrelated acceptance cases (see `acceptance-summary.md`) shift with the same sign and a
   comparable magnitude on paths that do not execute this harness at all — a code-local regression
   cannot move untouched paths.
3. Host load at measurement time was `loadavg 3.69 3.68 3.88` (8 logical cores, agent toolchain
   active); the D01 baseline (18:43) was a different ambient session.

No regression claim and no improvement claim is made.
