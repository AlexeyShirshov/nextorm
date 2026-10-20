# E22 attribution protocol — T297 r=4 (P02)

Generated 2026-10-19T12:23:57Z by `.pdca/T297/rv3/tools/run_r4.py attribute`.

## Lock and duplicate guard

- Canonical host lock: `/tmp/opencode/nextorm-bdn-host.lock`, exclusive `flock` held for the whole block (timeout 600 s).
- Duplicate guard `/tmp/opencode/t247-measure.guard`: present (not acquired/nested by this runner).
- Competing `nextorm.benchmark`/`probe-` processes at start: none.

## Quiet window

- Idle observation before round 1: 60 s (start 2026-10-19T12:20:02Z, end 2026-10-19T12:21:02Z).
- `/proc/loadavg` start: `0.71 1.56 2.21 1/1003 3189417`; end: `0.59 1.37 2.10 1/1004 3190059`.
- Rounds are never excluded for slow time; the max 1-min load per block is recorded below.

## Order and schedule

- `--rounds 6 --order ABBA`: 6 paired rounds = 3 ABBA groups = 12 blocks (`A,B,B,A` × 3).
- One probe process per block: `taskset -c 1 dotnet <probe.dll> --arm all --warmup 5 --iters 30 --overhead-n 100000`.
- Env per block: `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, `DOTNET_gcServer=0`, `NEXTORM_BENCH_FULL` unset.

## Provenance

| side | commit | probe dll | probe sha256 | nextorm.core.dll sha256 |
|---|---|---|---|---|
| A | `0d4d5df2` | `/tmp/opencode/t297-p02/probe-a/bin/Release/net10.0/t297-probe-a.dll` | `cd92b6185eb1b6e7bf511c5db38c0a8fe235993b4ba8f0c9fc06173efb46658c` | `f996ecd001085805de32a3829d31616025b208abc0a78ba01cd4e35819e0bc4f` |
| B | `777e9233` | `/tmp/opencode/t297-p02/probe-c/bin/Release/net10.0/t297-probe-c.dll` | `4992f06e5fc8731ad5817f42521625513f0de7543de9806420e9cabd87d72d97` | `3a0391c490d73fecaf8365d9ec759e17856310110227c8ed9f640b8f2ac8d675` |

- `dotnet --version`: `10.0.401`; repo HEAD: `39fef763821c43a39fd9053760608b25905298bc`.
- Benchmark DB: `/tmp/nextorm-bench/test.db`, sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` (checked before and after every block; expected).
- Env snapshot: `meta.json` -> `env` (DOTNET_*/NEXTORM_*/PATH, plus key-list hash).
- Wall time total: 70.1 s.

## Blocks

| block | side | start | wall s | exit | loadavg 1-min before/after | db sha before/after | records |
|---|---|---|---|---|---|---|---|
| 01 | A | 2026-10-19T12:21:02Z | 0.8 | 0 | 0.59 / 0.59 | 32bf10a08291 / 32bf10a08291 | 5 |
| 02 | B | 2026-10-19T12:21:03Z | 0.8 | 0 | 0.59 / 0.59 | 32bf10a08291 / 32bf10a08291 | 5 |
| 03 | B | 2026-10-19T12:21:04Z | 0.8 | 0 | 0.59 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 04 | A | 2026-10-19T12:21:04Z | 0.8 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 05 | A | 2026-10-19T12:21:05Z | 0.8 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 06 | B | 2026-10-19T12:21:06Z | 0.8 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 07 | B | 2026-10-19T12:21:07Z | 0.8 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 08 | A | 2026-10-19T12:21:08Z | 0.8 | 0 | 0.70 / 0.70 | 32bf10a08291 / 32bf10a08291 | 5 |
| 09 | A | 2026-10-19T12:21:08Z | 0.9 | 0 | 0.70 / 0.81 | 32bf10a08291 / 32bf10a08291 | 5 |
| 10 | B | 2026-10-19T12:21:09Z | 0.8 | 0 | 0.81 / 0.81 | 32bf10a08291 / 32bf10a08291 | 5 |
| 11 | B | 2026-10-19T12:21:10Z | 0.8 | 0 | 0.81 / 0.81 | 32bf10a08291 / 32bf10a08291 | 5 |
| 12 | A | 2026-10-19T12:21:11Z | 0.8 | 0 | 0.81 / 0.81 | 32bf10a08291 / 32bf10a08291 | 5 |

## Decomposition (P02b refinement)

- Same protocol as the P02 run: order, rounds, warmup, iterations, quiet window, lock, db, probes.
- Only the instrumentation and the derivation changed; the P02 evidence dir is untouched.
- New phases inside the `prepare` span: `planner_gate`, `planner_recipe`, `prepare_pipeline`; inside
  `prepare_pipeline`: `prep_preamble`, `nav_expand`, `get_from`, `prepare_from`, `prepare_join`,
  `prepare_columns`, `prepare_where`, `prepare_prewhere`, `prepare_arrayjoin`, `prepare_grouping`,
  `prepare_sorting`, `prepare_union`, `prepare_ctes`, `prepare_hints`, `unkeyed_scans`, `from_hash`.
- `prepare_exclusive` is re-derived by subtracting the whole pipeline and the planner preamble; the P02
  derivation is reported as `legacy_prepare_exclusive`.

- P04 (E38): 15 new `rcp_*` phases decompose the `planner_recipe` span; exclusive terms are derived by
  subtraction exactly once (definitions in `recipe-decomposition.md`). The new phases are reported
  beside the E22 reconciliation; no protocol, threshold or verdict-rule change.


Analysis: paired B/A ratio per round; median of per-round ratios; percentile bootstrap 95 % CI
(10000 resamples, fixed seed 297); calls/op and allocation delta are side totals; the overhead estimate
is `pair cost × instrumented boundary calls per query`, reported separately from the phase table.

