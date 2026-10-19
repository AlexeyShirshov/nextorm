# E22 attribution protocol — T297 r=4 (P02)

Generated 2026-10-19T11:05:08Z by `.pdca/T297/rv3/tools/run_r4.py attribute`.

## Lock and duplicate guard

- Canonical host lock: `/tmp/opencode/nextorm-bdn-host.lock`, exclusive `flock` held for the whole block (timeout 600 s).
- Duplicate guard `/tmp/opencode/t247-measure.guard`: present (not acquired/nested by this runner).
- Competing `nextorm.benchmark`/`probe-` processes at start: none.

## Quiet window

- Idle observation before round 1: 60 s (start 2026-10-19T11:01:42Z, end 2026-10-19T11:02:42Z).
- `/proc/loadavg` start: `1.09 1.35 1.41 3/1017 3084743`; end: `0.53 1.14 1.33 2/990 3085366`.
- Rounds are never excluded for slow time; the max 1-min load per block is recorded below.

## Order and schedule

- `--rounds 6 --order ABBA`: 6 paired rounds = 3 ABBA groups = 12 blocks (`A,B,B,A` × 3).
- One probe process per block: `taskset -c 1 dotnet <probe.dll> --arm all --warmup 5 --iters 30 --overhead-n 100000`.
- Env per block: `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, `DOTNET_gcServer=0`, `NEXTORM_BENCH_FULL` unset.

## Provenance

| side | commit | probe dll | probe sha256 | nextorm.core.dll sha256 |
|---|---|---|---|---|
| A | `0d4d5df2` | `/tmp/opencode/t297-p02/probe-a/bin/Release/net10.0/t297-probe-a.dll` | `836fde89d1eec1aff0e924835ddd93ecc41fb0aab85b803ce0fe57043b8e699b` | `7a28592b775fc74356d687fe98447638d2eb0a29ecddf14904dbb92775315146` |
| B | `9d633521` | `/tmp/opencode/t297-p02/probe-b/bin/Release/net10.0/t297-probe-b.dll` | `eb5444e36a60124b8b9ba0f2a90d19bf9936cf407822743c86b8b20376409b42` | `46af318938b67c06a6ab39ba5574993a2ec418aa9c89fd87f9da3a23d68e84ff` |

- `dotnet --version`: `10.0.401`; repo HEAD: `f805978d9560e6a52ed86321d5ab93fba410bacc`.
- Benchmark DB: `/tmp/nextorm-bench/test.db`, sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` (checked before and after every block; expected).
- Env snapshot: `meta.json` -> `env` (DOTNET_*/NEXTORM_*/PATH, plus key-list hash).
- Wall time total: 69.0 s.

## Blocks

| block | side | start | wall s | exit | loadavg 1-min before/after | db sha before/after | records |
|---|---|---|---|---|---|---|---|
| 01 | A | 2026-10-19T11:02:42Z | 0.8 | 0 | 0.53 / 0.53 | 32bf10a08291 / 32bf10a08291 | 5 |
| 02 | B | 2026-10-19T11:02:42Z | 0.8 | 0 | 0.53 / 0.53 | 32bf10a08291 / 32bf10a08291 | 5 |
| 03 | B | 2026-10-19T11:02:43Z | 0.8 | 0 | 0.53 / 0.53 | 32bf10a08291 / 32bf10a08291 | 5 |
| 04 | A | 2026-10-19T11:02:44Z | 0.7 | 0 | 0.53 / 0.53 | 32bf10a08291 / 32bf10a08291 | 5 |
| 05 | A | 2026-10-19T11:02:45Z | 0.7 | 0 | 0.53 / 0.65 | 32bf10a08291 / 32bf10a08291 | 5 |
| 06 | B | 2026-10-19T11:02:45Z | 0.8 | 0 | 0.65 / 0.65 | 32bf10a08291 / 32bf10a08291 | 5 |
| 07 | B | 2026-10-19T11:02:46Z | 0.7 | 0 | 0.65 / 0.65 | 32bf10a08291 / 32bf10a08291 | 5 |
| 08 | A | 2026-10-19T11:02:47Z | 0.7 | 0 | 0.65 / 0.65 | 32bf10a08291 / 32bf10a08291 | 5 |
| 09 | A | 2026-10-19T11:02:48Z | 0.7 | 0 | 0.65 / 0.65 | 32bf10a08291 / 32bf10a08291 | 5 |
| 10 | B | 2026-10-19T11:02:48Z | 0.7 | 0 | 0.65 / 0.65 | 32bf10a08291 / 32bf10a08291 | 5 |
| 11 | B | 2026-10-19T11:02:49Z | 0.7 | 0 | 0.65 / 0.68 | 32bf10a08291 / 32bf10a08291 | 5 |
| 12 | A | 2026-10-19T11:02:50Z | 0.7 | 0 | 0.68 / 0.68 | 32bf10a08291 / 32bf10a08291 | 5 |

Analysis: paired B/A ratio per round; median of per-round ratios; percentile bootstrap 95 % CI
(10000 resamples, fixed seed 297); calls/op and allocation delta are side totals; the overhead estimate
is `pair cost × instrumented boundary calls per query`, reported separately from the phase table.

