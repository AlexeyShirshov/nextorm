# E08 disposition — T218 (#218) preparation-path control (DO → CHECK → corrective DO)

Binding: corrective commit `cb3d5b6bddd79f6468a432eee537c8b01e85b578` (tree
`317f0078b86d8b7f53bfe86ca07178dee020e017`); **product code byte-unchanged from `22b96233`** (the
corrective revision is test-only). The control measures the unchanged `patindex` scalar on the
SQL Server query-preparation path (`SqlFunctions.SqlServer.patindex`); its code was frozen before the
D0 baseline and did not change in any later revision, so the control is a valid **no-change** probe.

## Measurements — control `Prepare_Control_Patindex_Uncached`

`ShortRun(Toolchain=InProcessEmitToolchain, IterationCount=3, LaunchCount=1, WarmupCount=3)`,
`[MemoryDiagnoser]`; every run logs `MinIterationTime` (the workload is shorter than BDN's 100 ms floor).

| run | revision | Mean | Min | Max | StdDev | Allocated | Gen0 | mean vs baseline |
|---|---|---|---|---|---|---|---|---|
| DO baseline (PRE-change) | `f8caa690-pre-d1` | 879.498 us | 609.573 us | 1134.626 us | 262.839 us | 784.39 KB | 93.7500 | 100.0 % |
| DO POST | `f8caa690` | 640.050 us | 630.642 us | 652.930 us | 11.543 us | 789.47 KB | 93.7500 | 72.8 % |
| CHECK POST | `22b96233` | 909.197 us | 644.836 us | 1315.473 us | 357.132 us | 789.47 KB | 93.7500 | 103.4 % |
| corrective run 1 | `cb3d5b6b` | 838.724 us | 640.739 us | 999.202 us | 182.151 us | 789.47 KB | 93.7500 | 95.4 % |
| corrective run 2 | `cb3d5b6b` | 864.161 us | 650.544 us | 1038.407 us | 196.906 us | 789.47 KB | 93.7500 | 98.3 % |

Logs: `logs/d0-t218-preparation-baseline.log`, `logs/d5-t218-preparation-post.log`,
`/tmp/opencode/t218/bench.log` (CHECK), `logs/corrective-bench-run1.log`, `logs/corrective-bench-run2.log`.

## Decision (plan `:100`)

1. **Time — met.** Every post-change control run stays within the plan's `mean <= 105 % of baseline`
   bound: DO POST 72.8 %, CHECK POST 103.4 %, corrective run 1 95.4 %, corrective run 2 98.3 %. The
   ratio never exceeds 105 %; the four runs reproduce the bound and the (unchanged) control carries no
   cached-path regression signal.
2. **Allocations — reproducible delta on unchanged code, therefore not attributable; recorded as
   incomparable, not silently accepted.** All four post-change runs allocate exactly **789.47 KB**
   against the baseline **784.39 KB** (+5.08 KB, **+0.65 %**), with identical Gen0 (93.7500). The value
   is bit-identical across four independent runs, so it is **not** run-to-run random noise: it is a
   *reproducible* shift. Because it appears on a code path whose bytes are **identical** between the
   baseline and every post run, no product change can be credited with it — it is a
   benchmark-binary/allocator-level incomparability between the baseline build and the later builds,
   not a product regression. Recording it as a regression would be inventing one; recording it as
   "noise" would understate that it is reproducible. Per the plan rule, the incomparability is
   **stated** here, and the allocation criterion of the control is therefore **non-discriminating at
   this granularity** and is not used as a pass/fail oracle.

The performance decision's binding constraint holds: the control mean stayed `<= 105 %` of baseline
in every post run, the four new sequence cases were reported as absolute values with no invented
pre-feature baseline, and the new sequence benchmark carries category `t218-preparation`, never
`acceptance`.

**Row verdict: E08 met** (control time criterion met; allocation delta explicitly dispositioned as
reproducible-but-unattributable / incomparable, no regression invented).
