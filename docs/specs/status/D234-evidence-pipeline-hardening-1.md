# D234 — T234 evidence pipeline hardening — cycle status

task: T234
issue: https://github.com/AlexeyShirshov/nextorm/issues/234
issue_title: Evidence pipeline hardening: atomic evidence generator + stale-hash lint (from T217 r=5 STOP)
milestone: 1.0.9-rc3   (gh reports milestone title `1.0.9`; collection 1.0.9-rc3-open — record both)
collection: 1.0.9-rc3-open
collection_status_path: docs/specs/status/collection-1.0.9-rc3-open.md
selected_variant: pdca-coder
cycle_id: N=1
attempt: n=1
plan_revision: r=2
plan_state: ready
evidence_contract_rv: 3   (rv2 = in-repo slice; rv3 = follow-up #242 closure, supersedes rv2/kept)
supersedes: rv1 (plan-level only; no rv1 artifacts generated)
spec_version: T234-plan-v1
baseline_ref: c7ff89ca8ae339c920cc6cb0c6b6ef4558aa6481  (short c7ff89ca; collection baseline)
base_ref: 4557bd14eee739310ede3c4998a37b7e5b1003c2  (short 4557bd14; r=2 frozen-tree base)
branch: 1.0.9-rc3
worktree: /home/alex/sources/nextorm
execution_mode: single-group (current worktree; no worktree/branch/merge; no commit/push)

## PLAN (r=1)

r=1 replaced: frozen-tree BASE re-pinned after T207 (ec42c960) and T237 (11268ba7, 4557bd14) merged; HEAD moved c7ff89ca → 4557bd14

### Goal (essence, not issue wording)

Prevent evidence from being accepted after its inputs change, without letting evidence generation invalidate its own provenance (the T217 `D217-EVIDENCE-BINDING` terminal failure). Deliverable: an in-repo tracked Python CLI with `generate` + `lint`, atomic publication of receipts bound to a frozen tree, its tests/negatives, and repo-side wiring. **No product change.**

### Scope

In-repo tooling, tests, CI step, nextorm overlay instruction, T234 status/evidence.

### Non-scope / out-of-workspace gap (state explicitly)

The canonical owner of the pdca evidence contract is the GLOBAL skill dir `~/.config/opencode/skills/pdca-dotnet/**` (SKILL.md `:919` "Versioned evidence contract", `:1147` "CHECK completeness gate — mandatory evidence contract", path convention `:944-949`, frozen-manifest fields `:956-973`, rv supersession `:975-979`). That dir is **outside the repo and NOT authorized for writes in this run**. Therefore acceptance item 3 splits: **in-repo wiring is implemented** (tracked tooling + `.github/workflows/dotnet.yml` step + `.opencode/skills/nextorm-pdca/SKILL.md` instruction); **canonical global admission is a documented gap** (row R234-03G) and remains open until an authorized external change is made. Local success must not be reported as closing #234's item 3.

### Chosen approach

New tracked stdlib-Python CLI (`generate` + `lint`) instead of promoting the gitignored D217 shell scripts (which have hardcoded paths/omissions); existing `scripts/validate_inner_loop.py` is left **unchanged** (`:495-561` validated, no binding today). Freeze **source inputs**, not HEAD identity; publish through one atomic manifest replacement.

### Frozen tree (T217 root cause eliminated by design)

Resolve `c7ff89ca^{tree}`; `git read-tree` that tree into an isolated temporary index; stage a frozen, sorted, explicitly declared footprint with `git add -A -- <paths>`; `git write-tree` → the frozen tree. The footprint covers the declared tooling/config/docs paths; **`artifacts/**` and `docs/specs/status/**` are excluded** (`.gitignore:7` ignores `artifacts/`) — so generated receipts and status edits can never move the tree (this is what made D217 self-referential: chain `236737…`→`4c3ac382…`→`03c284d6…`→`fdfe8ac6…`). Manifest records the exact path list, exclusions, base object, and method.

### Legacy policy (decided)

Lint scope is one explicitly selected `rv` directory (default: the task's own), never all of `artifacts/pdca/**`; inside the selected target non-40-hex/placeholder trees **fail with no grandfathering**; unrelated historical revisions are untouched (evidence: ~123 of 167 top-level `tree` values under `artifacts/pdca/**` are not 40-hex, e.g. `artifacts/pdca/D223/rv1/manifest.json:3`, `artifacts/pdca/T213/rv5/manifest.json:3`).

### Publication/receipt semantics

Generation does not invent executions nor turn `open` rows into `met`; rehash source evidence; write deterministic immutable `generations/<content-digest>/results/<row-id>.json`; validate the complete candidate; atomically replace root `manifest.json` last; concurrent writers refused via a lock; preflight lint of existing evidence **before** regeneration (stale/missing → fail, never silent repair). Receipts repeat row metadata + both binding fields `tree` and `tree_binding`; manifest carries `result` references, evidence digests, footprint, base, `tree_method`, `contract_rv`. Exit codes: **2** = validation violation, **1** = invocation/operational error, **0** = success.

### Acceptance criteria (observable + negative)

| ID | Observable | Negative |
|---|---|---|
| R234-01 | one `generate` invocation produces every required row receipt; exit 0; independent `lint` exit 0 | existing stale receipt → exit **2** + binding diagnostic, no published replacement |
| R234-02 | manifest and every receipt satisfy `tree == tree_binding == recomputed frozen tree` | stale manifest/receipt/`tree_binding`, absent receipt, absent source evidence, missing/invalid tree → **2**, identifying path/field |
| R234-03L | tracked overlay + CI invoke the local provenance gate | any selected malformed legacy manifest rejected; historical targets not silently scanned |
| R234-03G | canonical global CHECK contract requires this mechanical gate | **open/blocked** without authorized global integration; local success cannot substitute |
| R234-04 | only declared tooling/config/internal-doc changes | task-attributable product changes or writes outside scope fail review |
| R234-05 | repeated generation byte-identical; interrupted generation leaves prior publication valid | injected pre-publication failure never exposes a partial manifest/receipt set |

Anchors: validator lacks binding (`scripts/validate_inner_loop.py:66-68,495-561`); canonical ownership (global SKILL.md `:919,944-979,1147`); prior terminal defect (`docs/specs/status/D217-datetimeoffset-1.md:820,838`).

### What the statement did not say (each closed)

- (a) atomic = coherent publication, not fsync durability — explicit assumption;
- (b) global canonical adoption semantics unverified → blocked, not assumed (R234-03G);
- (c) ~123 historical invalid hashes → task/rv scoping (above);
- (d) new python tests would miss CI (`.github/workflows/dotnet.yml:51-52` runs only `test_iteration14_gate.py`) → add a dedicated unittest step;
- (e) pre-existing untracked collection files exist → record initial diff, only task-attributable changes are reviewed, never revert others;
- (f) receipt-path escape, symlinks outside allowed roots, duplicate IDs, ambiguous layouts → guarded, not accepted as legacy.

### Variant matrix (execution path; every row closed)

Clean generation; second identical generation (idempotence) — test; stale manifest tree — test (rc2 + `manifest.tree` diagnostic); stale receipt `tree`/`tree_binding` — tests; missing receipt / missing manifest-row source file — separate tests; zero hash / `"1.0.9"` / empty / missing `tree` — parameterized tests (malformed vs degenerate vs absent distinguished); legacy non-40-hex inside scope vs unrelated historical revision — tests; footprint file changed after freeze — test (recompute mismatch); rv2 while rv1 exists — test (rv1 intact, supersession recorded); crash before publication / lock contention / path escape / duplicate IDs — tests + guards.

### Test strategy

Stdlib `unittest` (no pytest/pyproject in repo); tracked fixture **recipes** + disposable Git trees/receipts built at runtime (no external DB, no py deps). Harness records each child command, actual rc, diagnostic, and before/after publication digests; expected-negative child rc **2**, harness rc **0** only when all assertions hold. Red↔green: run stale fixtures, then kill deliberate mutants disabling the equality/recomputation/receipt-enumeration/missing-file/publication guards. Coverage: ≥90% executable lines in the new CLI; every changed decision outcome mapped to a test or explicit guard; all non-equivalent targeted mutants killed (.NET 85/75 thresholds are not a substitute). Negative-fixture design reuses the proven mutation classes from the D217 fixtures `artifacts/pca…` — correct path: `artifacts/pdca/D217-datetimeoffset/rv2/r4/negative/` (`stale-manifest-tree`, `stale-result`, `stale-tree-binding` with `tree_binding=0*40`, `missing-result`) and the harness pattern `negative/harness.sh:77-105` (rc==1 + diagnostic per case) — **design reuse only, never run from the ignored D217 tree**.

### DO units (D:)

- D234-01 (fix now) establish disposable-Git fixtures, independent oracle, red tests, recorded baseline — no git commits even for fixture setup;
- D234-02 (fix now) implement CLI, binding, strict scoped lint, deterministic publication, locking, failure preservation;
- D234-03 (fix now) negatives, coverage, mutation checks, CI step, nextorm overlay instructions;
- D234-04 (fix now) record contract, observed evidence, review findings, explicit global blocker — no claim that #234 is complete;
- **D234-05 (blocked active dependency)** authorized canonical wiring — preserved unchanged, the orchestrator routes it to `escalate`, the global dir is never written; per nextorm invariant 8 the residual slice stays in milestone 1.0.9 (tracked issue/unit in the same milestone), never moved to a later milestone.

### Planned write footprint (exact)

`scripts/pdca_evidence.py`; `scripts/tests/test_pdca_evidence.py`; `scripts/tests/pdca_evidence_harness.py`; `scripts/tests/fixtures/pdca_evidence/cases.json`; `scripts/README.pdca-evidence.md`; `.github/workflows/dotnet.yml`; `.opencode/skills/nextorm-pdca/SKILL.md`; `docs/specs/status/D234-evidence-pipeline-hardening-1.md`; generated outputs only under `artifacts/pdca/T234/rv1/`. No edits to seed artifacts, no product code. Additional write paths require PLAN review; preserve CRLF.

### Risks

Publication bugs; path-enumeration omissions in the footprint; confusing scoped success with whole-issue completion; accidental writes to the global dir.

### Prerequisites/assumptions

Git present and `c7ff89ca^{tree}` resolvable; python3 stdlib only; local owner alex **writable** on the global pdca-dotnet dir but **not authorized** in this run (gap, needs an owner decision); `artifacts/` stays gitignored.

### Docs plan

`scripts/README.pdca-evidence.md` (tooling), this internal status doc, `.opencode/skills/nextorm-pdca/SKILL.md` (project wiring). No public docs (`docs/**`, `docs/ru/**`, root readme) touched and no public link to `docs/specs/**`.

### Perf-measurement decision

Not needed — one-time evidence tooling, not per-row/query-path execution (precedent: `scripts/validate_inner_loop.py:586`); record elapsed harness time only, no benchmark.

### Reconnaissance decision

Separate spike not needed — disposable Git fixtures are the decisive experiment; D234-01 must demonstrate the Git-tree and stale-fixture oracles before implementation is frozen.

### Unit execution mode

Sequential, current tree (generator/tests/workflow/contract share interfaces) — no worktrees, no parallel edits.

### Toolchain + review-lens selection

Toolchain = `python3 -m unittest`, `python3 scripts/pdca_evidence.py`, git plumbing (`rev-parse`/`read-tree`/`add`/`write-tree`/`cat-file`), CI `.github/workflows/dotnet.yml`; lenses = provenance/security (traversal, hash/tree trust), atomicity (failure/interleaving evidence), test adequacy (branch/mutant matrix), scope (initial/final diff) — each must return command + exit code + numbers or `file:line` + rule.

### Obtainability (nextorm invariants 11–12)

The decisive probe is obtainable from committed/declarable sources only — committed validator + CI, declarable fixture recipes, `git rev-parse c7ff89ca^{tree}`, `git cat-file -t`, independent SHA-256 comparisons; **no dependency on the gitignored D217 scripts**. This eliminates D217's self-reference by design and does **not** reopen terminal `D217-EVIDENCE-BINDING` or authorize a fourth attempt. Previous terminal cause is a mandatory PLAN input and is recorded above.

### Versioned evidence contract — rv=1 (planned, not observed; stable row IDs; owner coder, verifier CHECK; commands run at repo root; `$A=artifacts/pdca/T234/rv1`)

| Row → req | Invocation | Expected |
|---|---|---|
| E234-01 → R234-01,02 | `python3 -m unittest discover -s scripts/tests -p 'test_pdca_evidence.py'` | 0, no skips → `$A/logs/unit.log` |
| E234-02 → R234-01,02,05 | `python3 scripts/tests/pdca_evidence_harness.py negative --output "$A/negative"` | 0; child negatives 2 with diagnostics → `$A/negative/summary.json` |
| E234-03 → R234-05 | `python3 scripts/tests/pdca_evidence_harness.py coverage --output "$A/coverage"` | 0, ≥90% lines, decision mapping complete → `$A/coverage/summary.json` |
| E234-04 → R234-02,05 | `python3 scripts/tests/pdca_evidence_harness.py mutate --output "$A/mutation"` | 0, all specified non-equivalent mutants killed → `$A/mutation/summary.json` |
| E234-05 → R234-01,02 | `python3 scripts/pdca_evidence.py generate --repo . --contract "$A/inputs/contract.json" --output "$A"` | 0 twice, identical publication → `$A/manifest.json`, `$A/generations/*/results/*.json`, `$A/logs/generate.log` |
| E234-06 → R234-02 | `python3 scripts/pdca_evidence.py lint --repo . --manifest "$A/manifest.json"` | 0, exhaustive selected-root verification → `$A/logs/lint.log` |
| E234-07 → R234-03L,04 | `git diff c7ff89ca -- .github/workflows/dotnet.yml .opencode/skills/nextorm-pdca/SKILL.md scripts docs/specs/status` | inspection available (not acceptance) → `$A/logs/wiring-diff.log`, `$A/review.json` |
| E234-08 → R234-03G | `escalate` on the canonical-wiring question | explicit blocked/authorization decision → `$A/global-wiring-decision.md`; **open until independently verified integration** |

CHECK re-gather budget: maximum two targeted requests, owned by CHECK; missing reports alone never justify DO or an rv revision. All rows are **P1**; CHECK cannot downgrade them; a substantive revision increments rv and explicitly supersedes rv1 while preserving IDs/obligations.

## PLAN (r=2)

r=1 → r=2 replan (plan-level only; no rv1 artifacts were ever generated — see `supersedes: rv1`).

### New P (r=2)

Re-pin the frozen-tree base to the merged tip; separate collection provenance; revise
reconciliation evidence accordingly.

### Changes (confined to)

- **BASE assignment** — the r=2 frozen-tree base ref is the merged tip
  `4557bd14eee739310ede3c4998a37b7e5b1003c2` (after T207 `ec42c960` and T237 `11268ba7` merged);
  the r=1 frozen-tree base `c7ff89ca8ae339c920cc6cb0c6b6ef4558aa6481` is retained only as the
  **collection baseline**, not as the freeze base.
- **Manifest baseline-field semantics** — `base_ref` = r=2 frozen-tree base (full 40-hex);
  `collection_baseline_ref` = full 40-hex `c7ff89ca…`; the two are separate fields and are not
  conflated.
- **Tree-binding / reconciliation contract wording** — the `tree_method` string is written verbatim
  into the manifest; reconciliation repeats the recipe with committed footprint contents after ACT;
  the resulting tree is not asserted to equal `HEAD^{tree}`.
- **Base-dependent commands** — E234-07 diff base becomes `4557bd14` (was `c7ff89ca`); the frozen
  evidence output moves to `artifacts/pdca/T234/rv2/` (rv1 was never produced).

### Unchanged

Requirement IDs and row IDs are unchanged (E234-01..08, R234-01..05, R234-03L, R234-03G); no new
rows; all obligations preserved. E234-08 / R234-03G stays `open` (out-of-workspace).

### Progress log

2026-10-16 | PLAN | revision r=1 | iteration 1/3 | PLAN r=1 written; plan_state=ready; stopped at PLAN→DO boundary (collection P.2 own-PLAN phase; no DO started, no commit). | docs/specs/status/D234-evidence-pipeline-hardening-1.md
2026-10-16 | PLAN | revision r=2 | iteration 1/3 | Replanned: base re-pinned to merged tip 4557bd14 (r=1 → r=2, n=1) | docs/specs/status/D234-evidence-pipeline-hardening-1.md
2026-10-16 | DO | revision r=2 | iteration 1/3 | DO started | docs/specs/status/D234-evidence-pipeline-hardening-1.md
2026-10-16 | DO | revision r=2 | iteration 1/3 | D234-01 done: disposable-Git fixtures + independent oracle + red/green negative suite; negative 16/16 rc0, each child case rc2 with path+field diagnostic, lint never mutates publication | artifacts/pdca/T234/rv2/negative/summary.json
2026-10-16 | DO | revision r=2 | iteration 1/3 | D234-02 done: scripts/pdca_evidence.py (generate+lint, stdlib only) rc0; frozen tree ed6c72ab2696b1a97d4dcfe5da3cc75c910528c0; manifest.tree == recomputed == every receipt tree/tree_binding; atomic publish (os.replace last) + exclusive lock | artifacts/pdca/T234/rv2/logs/lint.log
2026-10-16 | DO | revision r=2 | iteration 1/3 | D234-03 done: coverage 95.06% (>=90, tests 56/56), mutants killed 5/5, CI step (.github/workflows/dotnet.yml) + nextorm overlay instruction added | artifacts/pdca/T234/rv2/coverage/summary.json; artifacts/pdca/T234/rv2/mutation/summary.json
2026-10-16 | DO | revision r=2 | iteration 1/3 | D234-04 done: manifest frozen and validate_inner_loop manifest rc0; second generation byte-identical (digest a8ba5847f9d4b18041d88a46bd2925aeb16e36905160c9e7377407fe6bc79080); E234-08/R234-03G stays open (out-of-workspace) | artifacts/pdca/T234/rv2/manifest.json
2026-10-16 | DO | revision r=2 | iteration 1/3 | DO complete; manifest frozen at artifacts/pdca/T234/rv2/manifest.json; stopped at DO→CHECK | artifacts/pdca/T234/rv2/manifest.json


## ACT

CHECK verdict: **PASS** (in-repo slice, r=2, rv=2). cycle_outcome: **done (in-repo slice)**.

Named residual (verbatim): **Global canonical evidence-gate admission + required_rows coverage fold-in.**

- Residual state: **open**. `R234-03G` / `E234-08` **open**; `D234-05` **blocked** (unauthorized out-of-workspace dependency).
- Owner: the global `pdca-dotnet` skill maintainer.
- Trigger: authorized global-admission work (the coverage fold-in accompanies the promotion).
- Interim guard: CHECK runs **both** `scripts/pdca_evidence.py lint` **and** `scripts/validate_inner_loop.py manifest` (the lint alone does not check `required_rows` ↔ `rows` two-way coverage).
- Follow-up issue: **#242** — https://github.com/AlexeyShirshov/nextorm/issues/242 (milestone `1.0.9-rc3`).
- **#234 remains open** until the residual lands.

Evidence:

- `artifacts/pdca/T234/rv2/manifest.json`
- `artifacts/pdca/T234/rv2/logs/*` (`unit.log`, `negative.log`, `coverage.log`, `mutation.log`, `generate.log`, `generate-idempotent.log`, `lint.log`, `wiring-diff.log`, `idempotence.json`)
- `artifacts/pdca/T234/rv2/negative/summary.json`
- `artifacts/pdca/T234/rv2/coverage/summary.json`
- `artifacts/pdca/T234/rv2/mutation/summary.json`
- `artifacts/pdca/T234/rv2/global-wiring-decision.md`

### Progress log (ACT)

2026-10-16T13:01:33Z | ACT | revision r=2 | iteration 1/3 | CHECK PASS (in-repo slice); cycle_outcome=done (in-repo slice); residual 'Global canonical evidence-gate admission + required_rows coverage fold-in' open (R234-03G/E234-08 open, D234-05 blocked); follow-up #242 (milestone 1.0.9-rc3); #234 remains open | docs/specs/status/D234-evidence-pipeline-hardening-1.md

ACT commit: `7acad56d292401a28ba0e2b8d0d1cfae779f5873` (short `7acad56d`) — `#234 T234: atomic evidence generator + stale-hash lint (in-repo slice; R234-03G residual open)`.
Citation correction: the earlier `3eee152e…` (pre-amend) reference was dangling after the amend; this correction is carried by the follow-up status-record commit, so no commit cites its own hash.

## Closure — canonical admission + coverage fold-in (follow-up #242, rv=3)

Authorized follow-up after ACT. The residual recorded above is **closed**; the r=2 / rv=2 block
stays as the historical record of the in-repo slice.

### Obligation A — canonical global admission (done)

`scripts/pdca_evidence.py` (`generate` + `lint`) and its harness/tests/fixtures are now part of the
canonical skill `~/.config/opencode/skills/pdca-dotnet/**` (source snapshot
`opencode-config/config/skills/pdca-dotnet/**`, commit `063dffa`); the deployed copy is
byte-identical (`diff -rq --exclude=__pycache__` rc 0). `SKILL.md` gained §Versioned evidence
contract → **Mechanical evidence pipeline** (generate at the DO→CHECK boundary, lint in CHECK,
contract fields, exit codes) and a mandatory mechanical gate in §CHECK completeness gate that
requires **both** `scripts/validate_inner_loop.py manifest` and `scripts/pdca_evidence.py lint`.

### Obligation B — `required_rows` ↔ `rows` coverage folded into lint (done)

`pdca_evidence.py lint` now fails (rc 2) when a required row has no manifest row **or** a manifest
row is not declared in `required_rows`. Covered by 7 new unit tests, 2 new negative fixtures and a
dedicated mutation (killed). Measured on the canonical skill: **63/63** unit, **18/18** negatives,
coverage **95.29 %**, **6/6** mutants killed.

### State

- `R234-03G` / `E234-08`: **met** in rv=3 (evidence:
  `artifacts/pdca/T234/rv3/logs/global-admission.log`).
- rv=2 artefacts are kept unchanged; rv=3 explicitly supersedes them
  (`supersedes: {rv: 2, artifacts: "kept"}`) and preserves every row ID. The rv=2 frozen tree no
  longer reconciles because this follow-up edited `.opencode/skills/nextorm-pdca/SKILL.md`, which is
  inside the rv2 footprint — normal drift, not a lost verdict: rv2 recorded the state it was frozen
  against.
- Overlay `.opencode/skills/nextorm-pdca/SKILL.md`: the "canonical acceptance pending (R234-03G)"
  note is replaced by the accepted-gate statement; both project gates stay mandatory.
- Issue #242 carries the closure comment; #234's request 3 (wire the gate into the pdca evidence
  contract so CHECK validates provenance mechanically) is satisfied by this admission.
- No product change; the nextorm in-repo copy of `scripts/pdca_evidence.py` is intentionally
  unchanged (it is bound by the rv2 frozen tree) — the canonical gate is the skill's script.

Evidence:

- `artifacts/pdca/T234/rv3/manifest.json` — contract_rv 3, frozen tree
  `a5bd02518d667639334dcffa335b81c93d83148d`, manifest sha256
  `e77750df2e8c1418822337092e31116979b3fca5e09abf727dff2e58b9f228b9` (second generate
  byte-identical; both project gates rc 0)
- `artifacts/pdca/T234/rv3/logs/global-admission.log`
- `artifacts/pdca/T234/rv3/logs/{unit,negative,coverage,mutation,generate,generate-idempotent,lint,validate-manifest}.log`
- `artifacts/pdca/T234/rv3/inputs/contract.json`
- `opencode-config` commit `063dffa` (pushed to `origin/main`)

### Progress log (closure)

2026-10-17 | ACT | revision r=2 | follow-up #242 | residual closed: canonical pdca-dotnet admission (Obligation A) + required_rows coverage folded into lint (Obligation B); rv=3 generated, both project gates rc 0, E234-08/R234-03G met; rv2 kept historical; #242 commented | artifacts/pdca/T234/rv3/manifest.json
