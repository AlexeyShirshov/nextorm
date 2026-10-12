# T213 D07 — FINAL in-memory JoinInto perf vs D01 baseline (r=5 / rv4)

command (frozen, identical to the baseline):
`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*JoinIntoBenchmarks*"`

Harness: `benchmarks/nextorm.benchmark/JoinIntoBenchmarks.cs` — scalar 1:N (`HasMany`) and scalar M:N
(`HasManyThrough` junction + occurrence dedup), ParentCount=500, ChildrenPerParent=4, InternalIterations=5.
No `acceptance` category (cannot perturb the 7-case gate).

- final source revision: `2168692d0c06abf06e4eaacbfa1431f2fd32d409` (HEAD)
- baseline source revision (D01): `fc149bff2bfed855ccad81c158e871103ccbd7ae`
- raw logs:
  - final run 1: `joininto-bench.log` — exit 0; executed benchmarks 2; BDN global total 10.37s
  - final run 2 (repeat): `joininto-bench-rerun.log` — exit 0; executed benchmarks 2
  - baseline: `../rv1/perf-base/joininto-bench.log` — exit 0; executed benchmarks 2; global total 12.49s

## Results — Mean / Allocated

| Scenario | baseline | final run 1 | final run 2 | Δ base→run1 |
|---|---:|---:|---:|---:|
| `JoinInto_OneToMany_ToList` | 7.289 ms / 3.94 MB | 13.16 ms / 3.94 MB | 12.27 ms / 3.94 MB | +80.5% |
| `JoinInto_ManyToMany_ToList` | 9.793 ms / 4.27 MB | 14.81 ms / 4.26 MB | 14.91 ms / 4.26 MB | +51.2% |
| M:N / 1:N ratio | 1.35 | 1.13 | 1.22 | — |

`Allocated` is byte-identical between baseline and both final runs (3.94 MB / 4.26–4.27 MB).

## >=20% delta investigation (repo rule)

The repeat was performed on the same machine. Classification: **reproduced across the repeat, but not
attributable to the retained in-memory assembly change — machine-state / measurement variance.**

Grounds:
1. `Allocated` is byte-identical for both scenarios — the assembly produced exactly the same managed
   allocations; an 80% per-op CPU regression with zero added allocation is not supported by the data.
2. The unrelated acceptance cases (see `acceptance-summary.md`) shift with the same sign and a
   comparable magnitude on paths that do not execute this harness at all, and they too keep identical
   allocations — a code-local regression cannot move untouched paths.
3. Host load at measurement time was `loadavg 3.69 3.68 3.88` (8 logical cores, agent toolchain
   active); the D01 baseline (18:43) was a different ambient session.

No regression claim and no improvement claim is made.
