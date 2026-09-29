# Experiment #112 Execution Plan (CSV streaming — Sol-vs-DS on decisions)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Adaptation note:** this plan orchestrates an *experiment*, not product code. "Tests" are
> checkable command outputs (hashes, greps, rc, non-empty diff), not unit tests. The arms
> write the product code; no task here writes feature code.

**Goal:** Measure whether `a3` (PDCA, `plan`/`check` on DeepSeek) vs `upstream` (PDCA, `plan`/`check`
on Sol) differ on a feature that exists in neither base — issue #112 (streaming CSV to `Stream`).

**Architecture:** A sanitized single-commit clean-room is the frozen base. Three arms (`a1` bare, `a2`
skill, `a3` PDCA-on-DS) run there from the issue text alone; a fourth worktree in the real repo runs
the user's standard PDCA with Sol on decisions. A single-DS judge grades L2 against a pre-registered
rubric; L3 runs two streams (generic single-DS + read-only `nextorm-code-auditor`). `upstream` is
graded by the same judges so all totals share one scale.

**Tech Stack:** git worktrees, opencode CLI (`--pure --format json`), DeepSeek Flash, exp94 harness
(copied as exp112), Python 3 (report.py/compare.py/dbmetrics.py), .NET 10 (`dotnet test`).

**Spec:** `docs/specs/experiments/112-csv-stream/00-design.md`

## Global Constraints

- **Input = only the issue text** (#112). Rubric and any repo design docs are NOT given to arms or to
  `upstream`. Arm prompts ban network access and following issue links (#39 is a near-duplicate).
- **Freeze before implementing:** base commit hash and rubric hash are recorded before any arm starts.
- **Sanitization parity:** `todo_json_streaming.md` (§4 of spec) and its 3 pointers are removed from
  both the clean-room base and the `upstream` worktree.
- **Judges:** L2 = one `deepseek/deepseek-flash` primary; L3 = generic single-DS **and**
  `nextorm-code-auditor` (subagent, read-only, pinned `deepseek/deepseek-flash`, on a copy).
- **No reference L1 port** (`NO_PORT=1`); L1 = each arm's own tests.
- **CRLF everywhere**; never commit/push main; experiment artifacts stay uncommitted unless asked.
- **Costs** from `dbmetrics.py` (parent + subagents), not the parent-only `summary.tsv` column.

## Review Focus

Failure modes the spec implies but no arm is told, most likely first — each gets a test in the
owning task's steps:

1. **CSV quoting** of values containing the delimiter, `"`, `\r`/`\n` (must quote and double the quote).
2. **`NULL` vs empty string** rendered distinctly and consistently with the CSV convention chosen.
3. **Empty result set** — header only, or nothing, but defined and non-crashing.
4. **Stream ownership** — caller's `Stream` is not closed/`Dispose`d; only `Flush`ed; reader/command
   released.
5. **No per-row `TResult` and no boxing** on the hot path (typed getters, not `object`).
6. **sync + async parity**, `CancellationToken`, and a defined in-memory behavior
   (`NotSupportedException` or documented fallback).

---

### Task 1: Freeze the sanitized clean-room base

**Files:**
- Create: `/home/alex/sources/nextorm-cleanroom-112` (git repo, single commit)
- Modify: `exp112/env.cleanroom.sh` (write `BASE=<hash>`)

**Interfaces:**
- Produces: `BASE` commit hash (single-commit repo, no history/branches) + `REPO` path for Task 3.

- [ ] **Step 1: Create a single-commit clone at `0f1f222`**

```bash
SRC=/home/alex/sources/nextorm; DST=/home/alex/sources/nextorm-cleanroom-112
rm -rf "$DST"; git clone --no-hardlinks "$SRC" "$DST"
git -C "$DST" checkout --detach 0f1f222
git -C "$DST" checkout --orphan base112
git -C "$DST" add -A && git -C "$DST" commit -q -m "base 0f1f222 (#112, unsanitized)"
git -C "$DST" branch -D main 2>/dev/null || true
git -C "$DST" for-each-ref --format='%(refname:short)' refs/heads | grep -v '^base112$' | xargs -r -n1 git -C "$DST" branch -D
```
Expected: `git -C $DST log --oneline` shows exactly one commit.

- [ ] **Step 2: Sanitize the #39 JSON-streaming spoiler (spec §4)**

```bash
git -C "$DST" rm docs/specs/roadmap/todo_json_streaming.md
# scrub the 3 pointers (linq2db-backlog-gap-analysis.md, design-review-todos-2026-09-24.md,
# todo_interface_poco.md) — remove only the clauses naming todo_json_streaming.md
```
Expected: the 3 files no longer mention `todo_json_streaming`.

- [ ] **Step 3: Verify no feature leak remains**

```bash
git -C "$DST" grep -in "json_streaming\|JsonRowWriter\|IJsonStreamWriter\|WriteJsonAsync\|NdJson" -- 'docs/specs/**' || echo "CLEAN"
git -C "$DST" commit -q -am "sanitize #112 (remove #39 json-streaming recipe)"
```
Expected: `CLEAN`.

- [ ] **Step 4: Record the base hash**

```bash
git -C "$DST" rev-parse HEAD   # -> BASE
```
Write `export BASE=<hash>` into `exp112/env.cleanroom.sh`. Expected: 40-hex hash.

---

### Task 2: Freeze the rubric and judge/audit prompts

**Files:**
- Create: `exp112/ref/issue112.md`, `exp112/eval/rubric-112.md`, `exp112/eval/prompts/judge.md`,
  `exp112/eval/prompts/audit.md`, `exp112/eval/profile.jsonc`, `exp112/ref/frozen.sha256`
- Reference: `docs/specs/experiments/112-csv-stream/00-design.md` §5, §6

**Interfaces:**
- Produces: rubric file + its sha256 (frozen); judge/auditor prompts emitting the L2/L3 JSON schemas
  (same shape as exp94 so `report.py` works unchanged).

- [ ] **Step 1: Freeze the issue text and rubric**

Copy the exact #112 body into `exp112/ref/issue112.md`. Write `exp112/eval/rubric-112.md` with R1–R6
and the L3 scope verbatim from spec §5. Expected: file present, no `TBD`.

- [ ] **Step 2: Record hashes BEFORE any implementation**

```bash
cd /home/alex/sources/nextorm-experiments/exp112
sha256sum eval/rubric-112.md ref/issue112.md > ref/frozen.sha256 && cat ref/frozen.sha256
```
Expected: two hashes printed; store for the report.

- [ ] **Step 3: Write judge/audit prompts**

`judge.md`: read TREE + BASE + `ref/issue112.md` + `eval/rubric-112.md`; apply R1–R6; output ONLY the
rubric JSON. `audit.md`: read the diff `BASE..TREE`; apply the L3 scope; output ONLY audit JSON. Both
ban editing, subagents, and network. (Adapt exp94 prompts, swapping the reference from
`upstream-94-*.patch` to `rubric-112.md`.)

- [ ] **Step 4: Write the eval profile (two L3 streams)**

Copy `exp94/eval/profile.jsonc`; repoint every `external_directory` allow-path from `exp94` to
`exp112`; keep `judge`/`auditor` as single-DS primaries. Add the specialist as a **subagent** entry
and allow `task` on the generic auditor:

```jsonc
"nextorm-code-auditor": {
  "mode": "subagent", "model": "deepseek/deepseek-flash", "temperature": 0.1,
  "tools": { "read": true, "grep": true, "glob": true, "list": true, "bash": true,
             "edit": false, "write": false, "patch": false, "task": false },
  "prompt": "<verbatim role/purpose from /home/alex/sources/nextorm/.opencode/agents/nextorm-code-auditor.md, read-only>"
}
```
Expected: profile parses; `judge`/`auditor` remain primary.

---

### Task 3: exp112 harness, env, and arm worktrees

**Files:**
- Create: `exp112/{env.cleanroom.sh,compare.py,dbmetrics.py,integrity.py,reset-arm.sh,run-arm.sh,clean-run-all.sh,fix-models.sh,setup.sh}`
- Create: `exp112/eval/{prepare-scratch.sh,run-eval.sh,report.py,run-eval-all.sh,run-eval-specialist.sh}`
- Create: `exp112/prompts/{a1-bare.md,a2-skill.md,a3-pdca.md}`

**Interfaces:**
- Consumes: `BASE` and `REPO` from Task 1.
- Produces: worktrees `$WTBASE/exp-{a1-bare,a2-skill,a3-pdca}` at `BASE`; arm prompts; eval scripts.

- [ ] **Step 1: Copy and repoint harness scripts**

Copy the listed files from `exp94`. Repoint `EXP`, `REPO`, `WTBASE`, `UPSTREAM_REPO`, `BASE`, session
titles (`exp112/a1` …), and `dbmetrics.ARMS`. Expected: `grep -rn "exp94" exp112` returns only
comments/intentional references.

- [ ] **Step 2: Write arm prompts (issue-only, no network)**

Each prompt = exact `ref/issue112.md` + the standard target-dir preamble + a hard ban:
"Не обращайся к сети, не открывай ссылки issue (в т.ч. #39), не читай каталоги вне рабочего дерева."
Expected: three prompt files, identical task text, different framing per arm.

- [ ] **Step 3: Create the worktrees**

```bash
cd /home/alex/sources/nextorm-experiments/exp112
set -a; . env.cleanroom.sh; set +a
mkdir -p "$WTBASE"; bash setup.sh
for w in exp-a1-bare exp-a2-skill exp-a3-pdca; do echo "$w $(git -C "$WTBASE/$w" rev-parse HEAD)"; done
```
Expected: each HEAD == `BASE`.

---

### Task 4: Create the sanitized `upstream` worktree (user's PDCA)

**Files:**
- Create: worktree `/home/alex/sources/nextorm-worktrees-112/upstream` in the real repo
- Modify: same §4 sanitization there

**Interfaces:**
- Produces: a real-repo worktree at the sanitized base where the user runs PDCA (`plan`/`check` = Sol,
  `coder` = DS) and commits the #112 implementation.

- [ ] **Step 1: Create a sanitized branch/worktree in the real repo**

```bash
R=/home/alex/sources/nextorm; U=/home/alex/sources/nextorm-worktrees-112
mkdir -p "$U"; git -C "$R" worktree add -b exp112/upstream "$U/upstream" 0f1f222
git -C "$U/upstream" rm docs/specs/roadmap/todo_json_streaming.md
# scrub the same 3 pointers as Task 1 Step 2
git -C "$U/upstream" commit -q -am "sanitize #112 (json-streaming recipe removed)"
```
Expected: `git -C "$U/upstream" grep -in json_streaming -- docs/specs` → empty.

- [ ] **Step 2: User runs the standard PDCA on #112 here**

Hand off to the user: run the normal project process inside `$U/upstream`, `plan`/`check` on
`opencode/gpt-6-sol`, `coder` on DeepSeek; commit the result. Do not merge into `main`.
Expected: one or more commits implementing #112 in that worktree.

---

### Task 5: Run the three arms

**Files:**
- Create: `exp112/logs/{a1,a2,a3}.{log,jsonl,rc}`

**Interfaces:**
- Consumes: worktrees from Task 3.

- [ ] **Step 1: Run (background, sequential, with retry)**

```bash
cd /home/alex/sources/nextorm-experiments/exp112
nohup bash clean-run-all.sh 5400 2 a1 a2 a3 > logs/run-all.log 2>&1 &
```
Expected per arm: `rc=0` AND a non-empty diff (`git diff --shortstat $BASE`). A failing arm retries.

- [ ] **Step 2: Confirm arm outputs**

```bash
for a in a1 a2 a3; do echo "== $a: $(git -C "$WTBASE/exp-$a"* rev-parse --short HEAD)"; done
```
Expected: unlike `BAS`; `logs/*.rc` == 0.

---

### Task 6: Generate patches and diffstat (all four targets)

**Files:**
- Create: `exp112/report/{a1,a2,a3,upstream}.{patch,diffstat.txt}`, `exp112/report/summary.tsv`

**Interfaces:**
- Consumes: arm worktrees (Task 3/5) + upstream worktree (Task 4).
- Produces: `report/<target>.patch` consumed by `eval/prepare-scratch.sh`.

- [ ] **Step 1: Adapt `compare.py` for a 4th target**

Add `upstream` to `ARMS` with `wt=$U/upstream`; replace `upstream_files()` (no pre-existing reference)
with a post-hoc overlap computed across all four patches after they exist; keep `I`/`PATCH_EXCLUDE`.

- [ ] **Step 2: Run it**

```bash
cd /home/alex/sources/nextorm-experiments/exp112 && VERIFY=1 python3 compare.py
ls -l report/
```
Expected: four `*.patch` non-empty; `summary.tsv` has 4 rows; `VERIFY` build rc=0 and own core/sqlite
tests green for each target.

---

### Task 7: Evaluate all four targets (L1 + L2 + L3×2)

**Files:**
- Create: `exp112/eval/out/<target>.{l2.jsonl,l3.jsonl,l3-specialist.jsonl,l1.log}`

**Interfaces:**
- Consumes: patches (Task 6); profile/rubric (Task 2).
- Produces: judge/audit JSONL consumed by `eval/report.py`.

- [ ] **Step 1: L2 + L3-generic + L1 (own tests only)**

```bash
cd /home/alex/sources/nextorm-experiments/exp112
NO_PORT=1 bash eval/run-eval-all.sh
```
Expected: `L2_RC=0`, `L3_RC=0` per target; `*.l1.log` shows `0 failed` for core and sqlite.

- [ ] **Step 2: L3-specialist (`nextorm-code-auditor`, read-only, on a copy)**

Write `eval/run-eval-specialist.sh`: for each target, prepare a scratch tree, then run a primary that
dispatches the `nextorm-code-auditor` subagent on a **copy** of that tree (deny edit), output to
`out/<target>.l3-specialist.jsonl`. Run it.

```bash
bash eval/run-eval-specialist.sh
```
Expected: 4 specialist JSONL files, each with P0/P1/P2 findings; no writes into `eval-wt/*`.

---

### Task 8: Costs and the report docset

**Files:**
- Modify: `docs/specs/experiments/112-csv-stream/{README,01-scenario,02-methodology,03-evaluation,04-costs,05-stages,06-caveats}.md`
- Create: `exp112/eval/report.md`, `exp112/collect-reports.sh`

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Costs (parent + subagents)**

```bash
cd /home/alex/sources/nextorm-experiments/exp112 && python3 dbmetrics.py
```
Expected: per-target parent + children cost; note that `a3`/`upstream` subagent totals dominate.

- [ ] **Step 2: Build the aggregate report**

```bash
python3 eval/report.py    # writes eval/report.md from the *.jsonl/*.l1.log
```
Expected: L1/L2/L3(+specialist) tables for 4 targets.

- [ ] **Step 3: Write the docset**

7 markdown files in `docs/specs/experiments/112-csv-stream/` (spec §9): main conclusion `a3` ↔
`upstream`, `a1`/`a2` as context, both L3 streams with agreement, costs, limits. Copy artifacts via
`collect-reports.sh`. Expected: docset complete.

- [ ] **Step 4: Normalize CRLF**

```bash
perl -pi -e 's/\r?\n/\r\n/g' /home/alex/sources/nextorm/docs/specs/experiments/112-csv-stream/*.md
```
Expected: every file CRLF; no LF-only.

## Self-Review

- **Spec coverage:** §3→T3/T4, §4→T1/T4, §5→T2, §6→T2/T7, §7→T6/T7, §8→T1–T8, §9→T8, §11→T8 docset.
- **Step scan:** each step gives one command + expected result; no "handle edge cases".
- **Type consistency:** `BASE`, `EXP`, `WTBASE`, `REPO`, `target ∈ {a1,a2,a3,upstream}`, and the JSONL
  names are used identically across Tasks 1/3/6/7/8.
- **Review Focus:** the six failure modes above are graded by rubric R4 (1,2), R1/R4 (3), R5 (4),
  R2/R3 (5), R6 (6); expert-review step T7 grades cached-path/branch behavior.
- **Proportion:** plan ≈ spec size; no function bodies.

## Execution Handoff

Two execution methods: **subagent-driven** (fresh subagent per task + reviewer) or **native** (I run
tasks here). Recommended: **native** — tasks are sequential, share `BASE`/harness state, and a wrong
step (e.g. a bad base hash) would poison every later task.
