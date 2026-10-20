# E22 attribution protocol — T297 r=4 (P02)

Generated 2026-10-19T12:07:26Z by `.pdca/T297/rv3/tools/run_r4.py attribute`.

## Lock and duplicate guard

- Canonical host lock: `/tmp/opencode/nextorm-bdn-host.lock`, exclusive `flock` held for the whole block (timeout 600 s).
- Duplicate guard `/tmp/opencode/t247-measure.guard`: present (not acquired/nested by this runner).
- Competing `nextorm.benchmark`/`probe-` processes at start: none.

## Quiet window

- Idle observation before round 1: 60 s (start 2026-10-19T12:06:14Z, end 2026-10-19T12:07:14Z).
- `/proc/loadavg` start: `2.85 4.61 3.67 2/1015 3167389`; end: `1.33 3.84 3.46 1/1012 3168004`.
- Rounds are never excluded for slow time; the max 1-min load per block is recorded below.

## Order and schedule

- `--rounds 6 --order ABBA`: 6 paired rounds = 3 ABBA groups = 12 blocks (`A,B,B,A` × 3).
- One probe process per block: `taskset -c 1 dotnet <probe.dll> --arm all --warmup 5 --iters 30 --overhead-n 100000`.
- Env per block: `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, `DOTNET_gcServer=0`, `NEXTORM_BENCH_FULL` unset.

## Provenance

| side | commit | probe dll | probe sha256 | nextorm.core.dll sha256 |
|---|---|---|---|---|
| A | `0d4d5df2` | `/tmp/opencode/t297-p02/probe-a/bin/Release/net10.0/t297-probe-a.dll` | `47f4fb0ea7b2b71a8c14b0313ce45a7cd1a4a43fedea7890f95712b420fdc25b` | `b6d9d96a20957c9a2dc4d9aa1656297e3fbaaa73f900b5d32efc58d3963c329e` |
| B | `c4b34178` | `/tmp/opencode/t297-p02/probe-c/bin/Release/net10.0/t297-probe-c.dll` | `cc30bb48397303a169a107c6a9a620283e3c42dcf29d61b7a0829b733581644c` | `293602d71b9df6d43e038c56e13602fe984ef3023fb6dde0e3d4c6ee5ef44ecf` |

- `dotnet --version`: `10.0.401`; repo HEAD: `c4b3417883a14921175d47e7638ad39685d9dc17`.
- Benchmark DB: `/tmp/nextorm-bench/test.db`, sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` (checked before and after every block; expected).
- Env snapshot: `meta.json` -> `env` (DOTNET_*/NEXTORM_*/PATH, plus key-list hash).
- Wall time total: 69.5 s.

## Blocks

| block | side | start | wall s | exit | loadavg 1-min before/after | db sha before/after | records |
|---|---|---|---|---|---|---|---|
| 01 | A | 2026-10-19T12:07:14Z | 0.8 | 0 | 1.33 / 1.33 | 32bf10a08291 / 32bf10a08291 | 5 |
| 02 | B | 2026-10-19T12:07:15Z | 0.8 | 0 | 1.33 / 1.33 | 32bf10a08291 / 32bf10a08291 | 5 |
| 03 | B | 2026-10-19T12:07:16Z | 0.8 | 0 | 1.33 / 1.33 | 32bf10a08291 / 32bf10a08291 | 5 |
| 04 | A | 2026-10-19T12:07:17Z | 0.7 | 0 | 1.33 / 1.33 | 32bf10a08291 / 32bf10a08291 | 5 |
| 05 | A | 2026-10-19T12:07:17Z | 0.9 | 0 | 1.33 / 1.39 | 32bf10a08291 / 32bf10a08291 | 5 |
| 06 | B | 2026-10-19T12:07:18Z | 0.8 | 0 | 1.39 / 1.39 | 32bf10a08291 / 32bf10a08291 | 5 |
| 07 | B | 2026-10-19T12:07:19Z | 0.8 | 0 | 1.39 / 1.39 | 32bf10a08291 / 32bf10a08291 | 5 |
| 08 | A | 2026-10-19T12:07:20Z | 0.7 | 0 | 1.39 / 1.39 | 32bf10a08291 / 32bf10a08291 | 5 |
| 09 | A | 2026-10-19T12:07:21Z | 0.7 | 0 | 1.39 / 1.39 | 32bf10a08291 / 32bf10a08291 | 5 |
| 10 | B | 2026-10-19T12:07:21Z | 0.8 | 0 | 1.39 / 1.39 | 32bf10a08291 / 32bf10a08291 | 5 |
| 11 | B | 2026-10-19T12:07:22Z | 0.8 | 0 | 1.39 / 1.39 | 32bf10a08291 / 32bf10a08291 | 5 |
| 12 | A | 2026-10-19T12:07:23Z | 0.8 | 0 | 1.36 / 1.36 | 32bf10a08291 / 32bf10a08291 | 5 |

## Decomposition (P02b refinement)

- Same protocol as the P02 run: order, rounds, warmup, iterations, quiet window, lock, db, probes.
- Only the instrumentation and the derivation changed; the P02 evidence dir is untouched.
- New phases inside the `prepare` span: `planner_gate`, `planner_recipe`, `prepare_pipeline`; inside
  `prepare_pipeline`: `prep_preamble`, `nav_expand`, `get_from`, `prepare_from`, `prepare_join`,
  `prepare_columns`, `prepare_where`, `prepare_prewhere`, `prepare_arrayjoin`, `prepare_grouping`,
  `prepare_sorting`, `prepare_union`, `prepare_ctes`, `prepare_hints`, `unkeyed_scans`, `from_hash`.
- `prepare_exclusive` is re-derived by subtracting the whole pipeline and the planner preamble; the P02
  derivation is reported as `legacy_prepare_exclusive`.


Analysis: paired B/A ratio per round; median of per-round ratios; percentile bootstrap 95 % CI
(10000 resamples, fixed seed 297); calls/op and allocation delta are side totals; the overhead estimate
is `pair cost × instrumented boundary calls per query`, reported separately from the phase table.

