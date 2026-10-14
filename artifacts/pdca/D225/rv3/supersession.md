# T225 (#225) — evidence contract rv=3 supersedes rv=2 (evidence-integrity handoff)

- Task: T225 / issue [#225](https://github.com/AlexeyShirshov/nextorm/issues/225); collection `1.0.9-todo` group G1; branch `1.0.9-rc3`.
- Cycle **N=1**; plan revision **r=4**; attempt **n=1/3**; contract **rv=3** (frozen by PLAN r=4, supersedes **rv=2** frozen by PLAN r=3).
- Baseline `$B = 0ed8e01dae44122276056f9f89676d913891ff09` (never floated to HEAD).
- Evaluated evidence source tree `T = 30bd9c5f34bae15c5c7e2651ccc1f52405b0d02a` (HEAD at r=4 start; the commit after `e9bea5f7` that recorded the r=3 DO→CHECK handoff).
- `$A2 = artifacts/pdca/D225/rv2` (immutable), `$A3 = artifacts/pdca/D225/rv3` (this revision), `$S` = the T225 status file.

## 1. Why a new contract revision

CHECK returned **FAIL** with a CHECK→PLAN loop-back on the frozen rv2 **E02** row alone. Criteria C1–C8 and rows
E01, E03–E07 were all `met`; CHECK explicitly recorded **no product or DO defect**.

Cause: rv2's pinned E02 scenario was

```
git diff --check "$B" -- "${F[@]}" "$S" "$A"
```

which lexically includes the raw evidence directory `$A2`. That directory holds generated stdout captures
(`logs/coverage.log`, `docs.patch`, `logs/docfx.log`, `logs/discovery.log`) whose bytes carry trailing whitespace and
blank-line-at-EOF, so the pinned argv exits **2** with 3060 offenders — none of them authored:

| raw artifact | offenders |
|---|---|
| `artifacts/pdca/D225/rv2/logs/coverage.log` | 3056 |
| `artifacts/pdca/D225/rv2/docs.patch` | 2 |
| `artifacts/pdca/D225/rv2/logs/docfx.log` | 1 |
| `artifacts/pdca/D225/rv2/logs/discovery.log` | 1 |
| **authored footprint / docs / status** | **0** |

Raw evidence formatting is outside authored-page whitespace validation. rv3 therefore corrects an over-broad
**verification scenario**, not delivered content: no product, test, configuration or authored documentation byte changes.

## 2. Supersession scope

- **Retained unchanged:** row IDs, requirements, scenarios and P1 priority for **E01, E03, E04, E05, E06, E07**; every
  C5 predicate of E02 (authored CRLF, EN/RU equivalence, links/numbering/TOCs, no public→`docs/specs/**` links,
  authored whitespace clean); the criteria C1–C8 and their meaning; the baseline `$B`.
- **Superseded:** only E02's artifact-inclusive whitespace scenario `… "$S" "$A"`. Its replacement is the
  authored-path command `git diff --check "$B" -- "${F[@]}" "$S"` → **exit 0, empty output** (`logs/diff-check.log`,
  re-run after the status freeze in `logs/diff-check-final.log`).
- **rv2 artifacts are immutable and re-referenced, never rerun or rewritten.** Each `E0x-retained.json` records the
  frozen rv2 row verbatim, the original invocation array with its exit code, selected count and value, the source
  revision, and sha256 integrity hashes of both the retained artifacts and the underlying source/test/doc files at `T`.
  The manifest points at those original sources, not merely at an index.

## 3. No repeat regression / container sweep (boundary argument)

The single execution sweep already occurred once, at the r=3 boundary. rv3 does not repeat it because:

1. The task's authored delta (`$F`, 16 files) and the status file are the only T225-authored content, and the status
   file's rv=3 addition is log/plan text.
2. The evaluated evidence domain — `src/**`, `tests/**`, the 16 footprint docs, `docs/specs/comparison`,
   `docs/specs/roadmap`, `nextorm.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`,
   `coverage.settings.xml` — is **byte-identical to `T`** (probes 1 and 2 of `logs/drift-check.log`: 0 changed names,
   0 porcelain entries including untracked).
3. Therefore the rv2 receipts (build 0/0, 159/159 core oracle, 4/4 + 17/17 composite oracles, coverage 87.30% line /
   79.90% branch, container integration with all five providers executed, portability and DocFX) still describe the
   current content. Re-running them would add cost and risk without adding acceptance evidence.

## 4. Drift statement (recorded for CHECK)

`git status --porcelain` shows 37 working-tree entries against `T`. One is `$S` (this cycle's status file); the
remaining 36 are **outside** the T225 footprint and outside the evaluated domain: `.opencode/**`, `AGENTS.md`,
`examples/**`, `docs/assets/**`, `docs/ru/assets/**`, other tasks' status files, `artifacts/pdca/D218-*/**`, untracked
BenchmarkDotNet output and `docs/specs/status/evidence/**`.

- None of them was written by this DO (DO wrote only `$S` and `$A3/**`, which `artifacts/` being gitignored hides from
  `git status`).
- Every one of them predates the r=3 boundary sweep: the newest such mtime is `2026-10-14 20:51:35 +0500`
  (`2026-10-14 15:51:35 UTC`), earlier than the sweep (`2026-10-14 23:45 +0500` / `18:45 UTC`). The retained receipts
  therefore already evaluated that same content, including during `dotnet build nextorm.slnx -c Debug`.
- Consequence: **no drift inside the evaluated evidence domain ⇒ evidence reuse is valid**. Because the churn is
  out-of-domain and pre-existing, it is recorded as a Notice rather than treated as disqualifying drift; CHECK owns the
  final judgement. Raw probe output: `logs/drift-check.log`.

## 5. Manifest, budget, immutability

- Manifest: `artifacts/pdca/D225/rv3/manifest.json`, frozen at DO→CHECK, bound to `tree = T` and `contract_rv = 3`,
  append-only during CHECK. Validated with
  `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D225/rv3/manifest.json` → exit 0.
- CHECK budget: original total 2, consumed 1, **remaining 1**; owner CHECK. Neither r=4 nor rv=3 resets it.
- No PASS may be recorded while any required row is `open`; in rv3 no required row is open.
- No push, branch or merge; commits stay confined to `$S` and `$A3/**` with the `#225 ` prefix.
