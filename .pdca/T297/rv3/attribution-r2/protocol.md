# E22 attribution protocol — T297 r=4 (P02)

Generated 2026-10-19T11:24:44Z by `.pdca/T297/rv3/tools/run_r4.py attribute`.

## Lock and duplicate guard

- Canonical host lock: `/tmp/opencode/nextorm-bdn-host.lock`, exclusive `flock` held for the whole block (timeout 600 s).
- Duplicate guard `/tmp/opencode/t247-measure.guard`: present (not acquired/nested by this runner).
- Competing `nextorm.benchmark`/`probe-` processes at start: none.

## Quiet window

- Idle observation before round 1: 60 s (start 2026-10-19T11:21:49Z, end 2026-10-19T11:22:49Z).
- `/proc/loadavg` start: `1.26 2.46 1.92 2/1016 3108944`; end: `0.67 2.07 1.82 1/1008 3109567`.
- Rounds are never excluded for slow time; the max 1-min load per block is recorded below.

## Order and schedule

- `--rounds 6 --order ABBA`: 6 paired rounds = 3 ABBA groups = 12 blocks (`A,B,B,A` × 3).
- One probe process per block: `taskset -c 1 dotnet <probe.dll> --arm all --warmup 5 --iters 30 --overhead-n 100000`.
- Env per block: `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, `DOTNET_gcServer=0`, `NEXTORM_BENCH_FULL` unset.

## Provenance

| side | commit | probe dll | probe sha256 | nextorm.core.dll sha256 |
|---|---|---|---|---|
| A | `0d4d5df2` | `/tmp/opencode/t297-p02/probe-a/bin/Release/net10.0/t297-probe-a.dll` | `47f4fb0ea7b2b71a8c14b0313ce45a7cd1a4a43fedea7890f95712b420fdc25b` | `b6d9d96a20957c9a2dc4d9aa1656297e3fbaaa73f900b5d32efc58d3963c329e` |
| B | `9d633521` | `/tmp/opencode/t297-p02/probe-b/bin/Release/net10.0/t297-probe-b.dll` | `6433b108c84830be11af93a76ad011353a399b7b9c24bdd5de83e13bed0c3714` | `9e1d3faf0a78a5ee10b57ad2759d812c59132f7239e941cd1722e920883d07c4` |

- `dotnet --version`: `10.0.401`; repo HEAD: `e6a67e3e98b201eb069cc5edd6380fd9521177f4`.
- Benchmark DB: `/tmp/nextorm-bench/test.db`, sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` (checked before and after every block; expected).
- Env snapshot: `meta.json` -> `env` (DOTNET_*/NEXTORM_*/PATH, plus key-list hash).
- Wall time total: 69.1 s.

## Blocks

| block | side | start | wall s | exit | loadavg 1-min before/after | db sha before/after | records |
|---|---|---|---|---|---|---|---|
| 01 | A | 2026-10-19T11:22:49Z | 0.7 | 0 | 0.67 / 0.67 | 32bf10a08291 / 32bf10a08291 | 5 |
| 02 | B | 2026-10-19T11:22:50Z | 0.7 | 0 | 0.67 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 03 | B | 2026-10-19T11:22:51Z | 0.8 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 04 | A | 2026-10-19T11:22:52Z | 0.7 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 05 | A | 2026-10-19T11:22:52Z | 0.7 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 06 | B | 2026-10-19T11:22:53Z | 0.8 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 07 | B | 2026-10-19T11:22:54Z | 0.8 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 08 | A | 2026-10-19T11:22:55Z | 0.7 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 09 | A | 2026-10-19T11:22:55Z | 0.7 | 0 | 0.70 / 0.80 | 32bf10a08291 / 32bf10a08291 | 5 |
| 10 | B | 2026-10-19T11:22:56Z | 0.8 | 0 | 0.80 / 0.80 | 32bf10a08291 / 32bf10a08291 | 5 |
| 11 | B | 2026-10-19T11:22:57Z | 0.8 | 0 | 0.80 / 0.80 | 32bf10a08291 / 32bf10a08291 | 5 |
| 12 | A | 2026-10-19T11:22:58Z | 0.7 | 0 | 0.80 / 0.80 | 32bf10a08291 / 32bf10a08291 | 5 |

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

