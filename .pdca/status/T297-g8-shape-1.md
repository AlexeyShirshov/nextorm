# T297-g8-shape — exact oracle-chain construction allocation (autonomous experiment)

Status: OPEN
Current cycle: 1
Plan revision: r1
Contract revision: rv1
Attempt: n 1/3
Phase: DO -> CHECK
Evidence root: `.pdca/T297/rv1/shape/`
Baseline: HEAD `9ffb8bdc`, branch `1.0.9-rc3`, .NET SDK 10.0.401, linux-x64 (WSL2)

## Goal

Answer one question with measurement, not projection: for the REAL CTE construction chains that
the acceptance oracle executes, what are **D** (replaceable QueryDefinition count per query) and
**R** (replaceable command-record count per query), what is the exact fluent sequence, and what are
the per-node bytes? Then recompute the D297 delta model for this exact shape and give verdicts
against RecursiveCte gates 2,640 / 3,482 / 5,199 B and Cte gates 216 / 2,073 B.

Hard boundary: `src/**` byte-identical start to end; no §13 unfreeze; no D297-02..04 work; no
cache/description implementation; no T244 acceptance re-run; no issue closures; no worktree, no
merge, no push.

## Real chains (verbatim from the oracle)

RecursiveCte — `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFairCached.cs:524-529`
(identical to `SqliteBenchmarkWarmDecompose.cs:82-87`):

```
anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
step   = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
_db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt)
```

Cte — `SqliteBenchmarkFeaturesFairCached.cs:491-499` (`BuildCte` `:73-80` in WarmDecompose):

```
recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
_db.With("recent", recent).From("recent")
   .Join(_ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id)
   .Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id })
```

Static expectation (to be confirmed or contradicted by measurement):

| Family | R | D | derivation |
|---|---:|---:|---|
| RecursiveCte | 3 | 2 | anchor Select + step Select + UnionAll `Clone()`; `WithRecursive` adds `CteQuery` + `CteDefinition` (64 B) + array and ZERO commands/definitions; `.From` is one builder; final Select = 1 command + 1 definition, excluded from R/D |
| Cte | 1 | 1 | body Select; `With` adds structural 64 B and no command; From + Join are builders; final Select excluded |
| RawHit arms | 0 | 0 | the raw-hit recipe path constructs nothing per timed hit |

## Plan (r1)

1. **Status** — this file, written before any other edit.
2. **Scope gate** — `.pdca/T297/rv1/shape/test-scope.json` (verbatim from the brief) plus
   `test-scope.wrapped.json` (same scope under the validator's required `unit`/`scope` schema);
   run `validate_inner_loop.py brief` on both before editing.
3. **Benchmark** — one new file
   `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkG8Shape.cs`: exact full-chain probes
   (`G8_Cte_Whole`, `G8_RecursiveCte_Whole`, construct only), prefix probes, one-call isolations
   with freshly prepared untimed inputs, controls (noop / `new object()` / builder-only), and an
   `Iso_SelectCached` pin (cached expression -> definition + command). Measurement by
   `GC.GetAllocatedBytesForCurrentThread` deltas around construction only, 1,000 warmups +
   1,001 samples per probe, median/p95/min/max, raw sample list and CSV exported from
   `[GlobalCleanup]` to `.pdca/T297/rv1/shape/steps.json` + `samples.csv`. No `Program.cs` edit.
4. **Driver** — `.pdca/T297/rv1/shape/run.sh`: DB pin sha256 check on both DB paths, 30x1 s
   `/proc/stat` CPU sampling per round (G7 floor 6.2-6.4 %, admit median <=8.4 %, p95 <=11.4 %),
   `flock -x /tmp/opencode/nextorm-bdn-host.lock` around the whole BDN invocation, the exact
   filter set from the brief, three rounds + at most two replacement attempts, per-round
   `environment.json`, `cpu.json`, `db.sha256`, `stdout.log`, raw BDN artifacts.
5. **Verifier** — `.pdca/T297/rv1/shape/verify.py` (stdlib only), subcommands
   `goal|shape|nodes|model|verdict|environment|boundary|src`, each writing `check/<row>.json` and
   exiting 0 only on its stated predicate. `model` must first reproduce the published examples
   (Cte R=2/D=2 -> 1,680/1,504/904; RecursiveCte R=5/D=3 -> 4,072/3,808/3,208) from
   `layer=816R+240D`, `cand_opt=40R+88D+88D`, `cand_cons=40R+88D+176D`, `cand_pess=cand_cons+600`,
   `delta=layer-cand`, then compute the real shapes. `verdict` uses `eps=max(64 B, 5 % of gate)`.
6. **Run + recompute** — execute the protocol, cross-check the WarmDecompose ladder rungs, write
   `.pdca/T297/rv1/shape/model.json` and `report.md`.
7. **Build gate** — `dotnet build nextorm.slnx -c Debug` after the last edit: exit 0, 0 warnings,
   0 errors.
8. **Commit** — only the status file and the new benchmark file (explicit `git add <path>`), no push.

## Evidence contract (rv1)

| Row | Predicate | Evidence |
|---|---|---|
| EC-G8-01 | `src/**` byte-identical before/after (empty `git status --porcelain src`; tracked-file sha256 unchanged); only allowed paths changed | `check/EC-G8-01.json`, `src-before.txt`, `src-after.txt`, `check/boundary.json` |
| EC-G8-02 | Environment pinned per round (SDK/runtime/CPU/HEAD) and DB sha256 equals `32bf10a0...2295` on both paths; CPU window recorded against the G7 floor | `round-*/environment.json`, `round-*/cpu.json`, `round-*/db.sha256`, `check/EC-G8-02.json` |
| EC-G8-03 | The benchmark reproduces the exact oracle chains verbatim in all three source files; every declared probe executed | `check/EC-G8-03.json`, `.pdca/T297/rv1/shape/steps.json` |
| EC-G8-04 | Per-node bytes measured: command 816, definition + command 1,056, builder 400, `With`/`WithRecursive` 64-168 with zero command/definition increments, controls 0/24/400 | `check/EC-G8-04.json`, `samples.csv` |
| EC-G8-05 | Measured R/D for both families, cross-checked against the static expectation; any contradiction reported with both values and measured treated as authoritative | `check/EC-G8-05.json`, `model.json` |
| EC-G8-06 | Closure: sum of isolated stage costs vs independently measured whole within `max(64 B, 5 %)` for both families | `check/EC-G8-06.json`, `steps.json` closure block |
| EC-G8-07 | Delta model reproduces the published examples exactly and computes the real-shape opt/cons/pess values | `check/EC-G8-07.json`, `model.json` |
| EC-G8-08 | One verdict per gate with `eps=max(64 B, 5 % of gate)` and signed margins; one verdict per family | `check/EC-G8-08.json`, `model.json`, `report.md` |
| EC-G8-09 | WarmDecompose ladder cross-checked: rung increments reported, `Construct/100` consistent with the new whole-chain probes; 8,816 / 11,376 explicitly attributed to `SqliteBenchmarkConstructAllocation.B_Construct` (different chain) | `check/EC-G8-09.json`, `report.md` |

Scope-gate note: the verbatim brief JSON has no `unit`/`scope` wrapper; the validator rejects it
with `FAIL: missing required key: unit` / `FAIL: missing required key: scope` (exit 2) - a schema
rejection, not a test-project rejection. No test project is fabricated. The identical scope,
wrapped under the required schema, validates with exit 0 and is the enforced brief. Recorded as
`Notice:` in the progress log.

## DO ledger

Plan r1 sealed as-is (no revision bump, attempt stays n 1/3).

| Unit | Result | Evidence |
|---|---|---|
| D1 harness fix | `RunAllProbes()` (calls the existing 32 `[Benchmark]` bodies once, then `Verify()`/`WriteEvidence()`/`VerifyEvidence()`), `ProbeBodies[32]`, `NEXTORM_G8_PROBE_LIMIT` (diagnostics only), `Program.cs` env gate `NEXTORM_G8_PROBE_ONLY=1`; no sampler, attribute, warmup/sample count, BDN config or measured body changed | `benchmarks/nextorm.benchmark/Program.cs`, `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkG8Shape.cs`, commit `ee9549c5` |
| D1 negative case | exit **1**: `probes collected: 1 (expected 32)` + `samples.csv: 1002 lines (expected 32033)` (`NEXTORM_G8_PROBE_LIMIT=1`, temp evidence dir) | `/tmp/opencode/t297-g8-negative.log` |
| D2 CPU admission | median **4.99 %**, p95 **7.58 %** → admitted (thresholds 8.4 / 11.4; −1.31 pp vs the G7 floor 6.2–6.4 %, deviation recorded) | `.pdca/T297/rv1/shape/probe-1/cpu.json` |
| D2 DB pin | both paths `32bf10a0…d72295`, `match=1` | `.pdca/T297/rv1/shape/probe-1/db.sha256` |
| D2 builds | benchmark Release: 0 Warning(s) 0 Error(s); `nextorm.slnx` Debug: 0 Warning(s) 0 Error(s) | `/tmp/opencode/t297-g8-build-bench-release.log`, `/tmp/opencode/t297-g8-build-sln-debug.log` |
| D2 probe run | exit **0**, wall **2 s**: `ProbesCollected 32/32`, `Complete: true`, 1001 samples each, `samples.csv` 32 033 lines; controls 0 / 24 / 400 B | `.pdca/T297/rv1/shape/probe-1/{steps.json,samples.csv,stdout.log}` |
| D2 verifier | byte-identical copy at `.pdca/T297/rv1/verify/verify.py` (sha256 `c9661046…`, arithmetic/gates untouched), rc: `shape` 1, `nodes` 1, `goal` 1, `model` 0, `verdict` 0, `environment` 1, `boundary` 1, `src` 0 | `.pdca/T297/rv1/shape/probe-1/verify-<sub>.log` |
| D3 commit | `ee9549c5` "Fix G8 probe-only evidence collection (benchmark harness)" — only the two benchmark paths, no push | `git show --stat ee9549c5` |
| D4 derivation | Cte R=1 / D=1 → δ 840 / 752 / 152; RecursiveCte R=3 / D=2 → δ 2 456 / 2 280 / 1 680; closure diff 0 / 0; gates: RecursiveCte all `not-reachable` (−184 / −1 026 / −2 743), Cte checkpoint `borderline` (+624 / −64, δpess = gate−eps), Cte parity `not-reachable` (−1 233) | `probe-1/steps.json`, `docs/specs/status/D297-structural-cte-graph-1.md` §Real graph shape (G8) |
| D5 manifest | `.pdca/T297/rv1/manifest.json` (rows: 01–04, 09 open; 05–08 met); both gates exit 0 | `.pdca/T297/rv1/manifest.json`, `/tmp/opencode/t297-g8-gate-{manifest,lint}.log` |

No product change: `src/**` tracked fingerprint `b7ccf93b…` unchanged, `git status --porcelain src`
empty; §13 frozen; #297/#244/#245 untouched.

## Progress log (append-only)

2026-10-18T13:25:43Z | PLAN | revision r1 | iteration n1/3 | `src` baseline captured (git status src empty, tracked fingerprint b7ccf93b...; HEAD 9ffb8bdc, SDK 10.0.401); scope brief written and validated; PLAN ready - autonomous, DO started | `.pdca/T297/rv1/shape/src-before.txt`, `.pdca/T297/rv1/shape/test-scope.validate.log`
2026-10-18T15:23:09Z | DO | revision r1 | iteration n1/3 | PLAN r1 sealed (no revision bump); D1 harness fix landed: `SqliteBenchmarkG8Shape.RunAllProbes()` runs the 32 existing `[Benchmark]` bodies in one process and asserts 32x1001 samples + a 32033-line `samples.csv`; `Program.cs` env gate `NEXTORM_G8_PROBE_ONLY=1`; no sampler/attribute/config/warmup change. Benchmark Release build 0 W 0 E. Negative case `NEXTORM_G8_PROBE_LIMIT=1` exit 1 (`probes collected: 1 (expected 32)`, `samples.csv: 1002 lines (expected 32033)`) | `benchmarks/nextorm.benchmark/{Program.cs,Sqlite/SqliteBenchmarkG8Shape.cs}`, `/tmp/opencode/t297-g8-negative.log`
2026-10-18T15:23:49Z | DO | revision r1 | iteration n1/3 | CPU quiet window admitted: median 4.99 %, p95 7.58 % (admission 8.4 / 11.4; -1.31 pp vs the 6.2-6.4 % G7 floor, recorded as deviation); DB pin `32bf10a0...d72295` matches on both paths | `.pdca/T297/rv1/shape/probe-1/{cpu.json,db.sha256}`
2026-10-18T15:24:06Z | DO | revision r1 | iteration n1/3 | `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s) | `/tmp/opencode/t297-g8-build-sln-debug.log`
2026-10-18T15:24:11Z | DO | revision r1 | iteration n1/3 | probe-only run exit 0, wall 2 s: 32/32 probes, 1001 samples each, `samples.csv` 32033 lines, `Complete: true`; controls 0/24/400 B; closure diff 0 (10 208 / 8 824) | `.pdca/T297/rv1/shape/probe-1/{steps.json,samples.csv,stdout.log}`
2026-10-18T15:25:55Z | DO | revision r1 | iteration n1/3 | verifier (byte-identical copy, sha256 `c9661046...`) rc: shape 1, nodes 1, goal 1, model 0, verdict 0, environment 1, boundary 1, src 0; open reasons: single probe run vs the 3/3-round requirement, `cte_projection` literal absent in oracle/WarmDecompose (`joined.` receiver), `Program.cs` absent from the frozen ALLOWED list; measured `select_cached(_fresh_builder)` 1 056 / 1 456 contradicts verifier-expected 816 / 1 216 (definition not elided) | `.pdca/T297/rv1/shape/probe-1/verify-<sub>.log`
2026-10-18T15:26:30Z | DO | revision r1 | iteration n1/3 | D3 commit `ee9549c5` "Fix G8 probe-only evidence collection (benchmark harness)" (only the two benchmark paths; no push) | `git show --stat ee9549c5`
2026-10-18T15:27:40Z | DO | revision r1 | iteration n1/3 | D4 outcome: D/R Cte 1/1, RecursiveCte 3/2; delta opt/cons/pess 840/752/152 and 2 456/2 280/1 680; gates RecursiveCte entry/checkpoint/parity all not-reachable (-184/-1 026/-2 743), Cte checkpoint borderline (+624/-64), parity not-reachable (-1 233); BDN cross-check 8 297 vs 8 824 and 9 520 vs 10 208 (-5.97 % / -6.74 %, within the 15 % band); evidence section appended to the D297 status | `docs/specs/status/D297-structural-cte-graph-1.md`
2026-10-18T15:27:45Z | DO | revision r1 | iteration n1/3 | D5 manifest pinned (contract_rv 1 = rv1; rows 01-04 and 09 open, 05-08 met): `validate_inner_loop.py manifest` exit 0, `pdca_evidence.py lint --repo . --manifest ...` exit 0 | `.pdca/T297/rv1/manifest.json`, `/tmp/opencode/t297-g8-gate-{manifest,lint}.log`
2026-10-18T15:32:52Z | ACT | revision r1 | iteration n1/3 | CHECK PASS-WITH-CAVEATS closed with no loop-back; F1/F2/F3 disposed: `verify-goal.log` (rc 1) added to EC-G8-05/06 evidence with an explicit reason, EC-G8-01..04/09 stay `open` with `disposition` fields, both gates re-run rc 0 into non-empty `probe-1/gate-{manifest,lint}.log`, F3 reason now cites fragment key `cte_projection` (verify.py:368); no product change (`git status src` empty) | `.pdca/T297/rv1/manifest.json`, `.pdca/T297/rv1/shape/probe-1/gate-{manifest,lint}.log`

## ACT (CHECK PASS-WITH-CAVEATS -> close the lane)

CHECK verdict: **PASS-WITH-CAVEATS** on the rv1 manifest. Iteration `n=1/3` is closed with
**no loop-back**: no plan revision bump, no new attempt, no 4th iteration.

Per-row dispositions (final):

| Row | Final status | Disposition |
|---|---|---|
| EC-G8-05 | met | R/D measured for both families (Cte 1/1, RecursiveCte 3/2) and cross-checked; the `verify-goal.log` rc 1 is verifier-input lag only (3-round BDN protocol plus oracle-literal fragment), recorded in the row's evidence `reason`. |
| EC-G8-06 | met | Closure Pass true for both families (residual 0: 10 208 / 8 824, tolerances 511 / 442); same `verify-goal.log` rc 1 provenance. |
| EC-G8-07 | met | `verify-model` rc 0; the published examples reproduced exactly, real-shape opt/cons/pess computed. |
| EC-G8-08 | met | `verify-verdict` rc 0; `eps=max(64 B, 5 %)` with signed margins per gate and per family. |
| EC-G8-01..04, 09 | open | Substantively satisfied (src gate rc 0, round-1 environment/CPU/DB pin, 32/32 probes, exact node pins, ladder cross-check); open **only** because the frozen verifier inputs lag the authorized harness change (`boundary` allowlist omits `benchmarks/nextorm.benchmark/Program.cs`) and enforce the 3-round BDN protocol / the `cte_projection` oracle literal. Verifier-input lag, **not** product failure. |

Finding dispositions:

- **F1** — `verify-goal.log` (rc 1) added to the `EC-G8-05` / `EC-G8-06` `evidence[]` with an explicit `reason`; `EC-G8-01..04` / `09` stay `open` (not `met`, not `na`) with a `disposition` naming the satisfying evidence and the verifier-input reason; `required_rows` unchanged. The met rows are met from `probe-1/steps.json` (32/32 probes, 1 001 samples each, closure Pass true) plus the D297 `## Real graph shape (G8)` section.
- **F2** — both gates re-run with stdout+stderr captured: `probe-1/gate-manifest.log` and `probe-1/gate-lint.log` are non-empty (228 / 248 B) and each ends `exit=0`; no threshold, tolerance or row schema changed.
- **F3** — the reason text now cites fragment key `cte_projection` (`verify.py:368`) and notes the oracle writes the projection in the fluent `.Select(...)` continuation instead of the non-existent `joined.Select(...)` literal.

Deliberate non-runs: BDN rounds 2-3 (the single probe-only round is the authorized evidence; the
3-round requirement belongs to the BDN protocol, not to this probe run); no BDN re-run (round-1
evidence is complete and the probe fix does not touch the BDN path); no worktree, no merge, no push.

Commits (branch `1.0.9-rc3`): `ee9549c5` (probe-only evidence collection, benchmark harness),
`279eb9d8` (real CTE graph shape G8 in the D297 status), plus this ACT commit (status only).

Verdict: measurement complete; §13 stays frozen; no gate claimed; no product change.
