# PDCA evidence — #164 post-restart live validation of the #152 evidence contract

- task: #164 (follow-up trigger of #152)
- group: group-2, collection `1.0.9-b-5`
- branch: `collection/1.0.9-b-5/group-2`
- base HEAD: `82e344f`
- cycle N=1; plan revision r=1; iteration n=1
- mode: autonomous; auto-commit authorized (collection lane)
- deliverable: this file (`docs/specs/status/pdca-evidence-164-1.md`)

## PLAN (r=1) — traceable invocation

Planner role invocation: Task `ses_ef81912e9ffez8Z0xiwWjVCaU6`, dispatched by primary session `ses_efa0237e9ffe6w6pOI50N73e5H` on 2026-10-04; returned the plan and the authoritative contract below. This is the real planner run (prior aborted orchestrator attempts are superseded).

Goal / acceptance:
- (a) prove post-restart loading of both new global skill sections;
- (b) prove a real planner run records `rv=1` in #164's status file.

Deliverable = this file (`docs/specs/status/pdca-evidence-164-1.md`). Docs-only; perf N/A; reconnaissance N/A; single sequential unit.

Test strategy: evidence inspection only — correct heading lines + trace-linked `rv`; reject missing/wrong headings, absent/mismatched `rv`, or fabricated/untraceable invocation assertions.

## Versioned evidence contract rv=1 (authoritative)

Contract revision `rv=1`.

| Row ID | Req | Requirement | Evidence source | Exact command / call | Exit / result / log | Expected artifacts | Owner | Applicability | rv |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| G2-LIVE | R164-A | post-restart session loaded both new global skill sections | existing loaded-skill output + primary-session metadata | f=/home/alex/.local/share/opencode/tool-output/tool_10746e894001aaPVuuBcdcnZt4; test -s "$f" && sha256sum "$f" && sed -n '884p;1083p' "$f" | exit 0 + exact headings at 884/1083 | sha256 of spill + lines 884/1083 | CHECK | unconditional | rv=1 |
| G2-LIVE-B | R164-B | a real planner run produced rv=1 and it is recorded here | planner invocation trace (Task id + timestamp) + this record | rg -n 'rv=1|G2-LIVE|ses_ef81912e' docs/specs/status/pdca-evidence-164-1.md | this file with invocation trace | CHECK | unconditional | rv=1 |

## Progress log

- PLAN r=1 pinned contract rv=1 (row G2-LIVE); DO not started.

- DO started (coder) — collecting C1–C4 evidence.
- DO done — C1–C4 + backups + provenance captured; raw log
  `/tmp/opencode/1.0.9-b-5/164/evidence.log`.

## DO ledger

- Notice: `scripts/validate_inner_loop.py` absent; docs-only scope, no compiled
  inputs.
- D:164.1 PLAN + `rv=1` contract written before DO (this file; PLAN record).
- D:164.2 C1 `sha256sum` exit=0; C2 `stat -c '%n|%s|%y|%Y'` exit=0.
- D:164.3 C3 `nl -ba | sed -n` all exits=0; every required line present.
- D:164.3b C3b: no standalone `rv` token in `pdca-planner.md`/`pdca-check.md`
  (literal `grep -n 'rv'` matches only incidental substrings inside words).
- D:164.4 C4 `git show collection/1.0.9-b-5/group-1:…` exit=0; rv heading + rows
  found.
- D:164.5 backups listing exit=0.
- D:164.6 provenance: `opencode export` blocked on interactive picker (timeout);
  `opencode session list` exit=0 — fallback to runtime Task provenance.
- D:164.7 DO ledger + live-validation report written below.
- D:164.8 CRLF normalized; `git status --porcelain` shows only this new file; no
  commit (ACT later).

## Live-validation report

### Global file audit (C1/C2)

| Ref | Absolute path | sha256 | mtime | size (bytes) |
| --- | --- | --- | --- | --- |
| C1a | `/home/alex/.config/opencode/skills/pdca-dotnet/SKILL.md` | `53e270d31c77372e2e479e62f3f08657636696ad028457acf5c7a243802be0c7` | `2026-10-03 16:32:21.341186996 +0500` | 97097 |
| C1b | `/home/alex/.config/opencode/agents/planner.md` | `7b84cdf0380f73ed196694a7e571a6a6fdecafa83acaa8f07ec5e7381fc99522` | `2026-10-03 16:32:53.768375963 +0500` | 12425 |
| C1c | `/home/alex/.config/opencode/agents/check.md` | `d2fdbca2e18c6f6534f50b5f75ad028d0902e56603db9c2508b8a4f2889b7c32` | `2026-10-03 16:33:02.477273960 +0500` | 8644 |

Label: **source-of-truth file identity (not the loaded wrapper).** These values
identify the on-disk source files; the content actually returned by the `skill`
tool is the spilled wrapper cited in the provenance statement below.

These sha256 values are byte-identical to the #152 ACT record
(`docs/specs/status/pdca-evidence-152-1.md`, lines 38) — the live files were not
mutated after #152.

### Contract anchors (C3)

Exact section headings and `rv` lines, `nl -ba` exit=0 each:

- `SKILL.md:886` → `### Versioned evidence contract`
- `SKILL.md:903` → ``` - The contract revision `rv`. ```
- `SKILL.md:1085` → `### CHECK completeness gate — mandatory evidence contract`
- `SKILL.md:1089` → ``` current `rv`. Required artifacts must exist and support the reported result. N/A is valid only ```

Role lines (`nl -ba` exit=0 each):

- `planner.md:40` → `«Versioned evidence contract» глобального скилла `pdca-dotnet`. До DO закрепи контракт`
- `planner.md:44` → `наблюдаемый предикат применимости и ревизия `rv`. На выбор PLAN — только представление:`
- `planner.md:117` → `` `rv`, стабильные ID, предикаты применимости, бюджет/владелец CHECK re-gather и явный ``
- `check.md:38` → ``` - Закреплённый версионированный evidence contract с текущей `rv`, DO ledger с привязкой ```
- `check.md:67` → `8. **Обязательный гейт полноты evidence:** применяй раздел «CHECK completeness gate —`
- `check.md:69` → ``` обязательную строку с фактическим evidence для текущей `rv`; проверь артефакты ```

### Stale role files (C3b)

- Host/active roles are `agents/planner.md` and `agents/check.md` (both exist and
  carry the contract token).
- The domain-neutral `pdca` role files `agents/pdca-planner.md` /
  `agents/pdca-check.md` (frontmatter `name: pdca-planner` / `pdca-check`) carry
  **no standalone `rv` token**: `(^|[^A-Za-z])rv([^A-Za-z]|$)` matches 0 lines in
  each; no backticked `` `rv` `` (grep exit=1).
- Literal `grep -n 'rv'` does return lines, but only as incidental substrings
  inside ordinary words (`server`, `preserved`, `observed`, `revisions`) — not
  the contract token. No stale `rv` contract definition competes.

### Group-1 corroboration (C4)

`git show collection/1.0.9-b-5/group-1:docs/specs/status/iteration-15-cached-path-183-1.md` exit=0:

- line 64 → `## Versioned evidence contract rv=1`
- line 68 → `| G1-PERF | #183 | … not-applicable (not run) |`
- line 69 → `| G1-CACHE | #183 | … pass (no diff) |`
- line 70 → `| G1-CTE | #166 trigger | … pass (no diff) |`

Group-1 is **terminal incomplete** (`outcome: **incomplete** — awaiting written
spec review`; `Incomplete: #183 — awaiting written spec review`). This is
**corroboration only, not a PASS** for #183 and not a PASS for G2-LIVE.

Authoritative group-1 corroboration — `git show
collection/1.0.9-b-5/group-1:docs/specs/status/iteration-15-cached-path-183-2.md`
exit=0 (final ACT file, supersedes 183-1):

- line 150 → `## Evidence contract rv=2 (authoritative, final)`
- line 152 → `> rv=1 → rv=2 at ACT (CHECK attempt 3/3 FAIL only on coverage branch baseline/delta absent → Escalated → decision a); predicates/scope unchanged.`
- line 156 → `| G1-PERF | #183 Stage A, всегда | **MEASURED** … | **met** |`
- line 157 → `| G1-CACHE | #183 Stage A, всегда | … coverage line≥85/branch≥75; реальные provider runs (skip ≠ pass) | **met** |`
- line 158 → `| G1-CTE | … predicate=false → N/A с доказательством … | **N/A** — predicate=false |`
- line 246 → `2026-10-04T12:42Z | ACT | revision r2 | iteration 3/3 | … Stage A done (evidence contract rv=2: G1-PERF/G1-CACHE met, G1-CTE N/A); no replan/STOP/fourth CHECK`

The authoritative group-1 revision is **rv=2** (`rv=1` at PLAN was superseded at
ACT by decision a), so group-1 corroborates that the versioned evidence contract
and its `rv` bump/supersession discipline were exercised end-to-end. This remains
**corroboration only** — group-1 is terminal incomplete for #183 and is not a
PASS for G2-LIVE.

### Backups (D:164.5)

`ls -l /tmp/opencode/1.0.9-b-4/baseline/152/` exit=0, 8 entries:
`asset-check.md`, `asset-planner.md`, `issue-152-spec.md`, `live-check.md`,
`live-planner.md`, `manifest.txt`, `nextorm-pdca.SKILL.md`,
`pdca-dotnet.SKILL.md`.

### Session-load provenance (L1–L4)

- (i) The orchestrator loaded the `pdca-dotnet` skill via the `skill` tool in
  this session (2026-10-04); the loaded skill content includes the sections
  `### Versioned evidence contract` and `### CHECK completeness gate — mandatory
  evidence contract` (disk anchors independently confirmed by C3 above).
- (ii) A real `planner` Task ran in this session and returned an `rv=1`-bearing
  plan referencing row `G2-LIVE`; Task id
  `ses_ef9ea7678ffeTDqxNf2u64wYcJ`.
- (iii) The session started 2026-10-04, **after** the global file mtimes of
  2026-10-03 16:32–16:33 +0500. `opencode session list` (exit=0) shows the
  current autonomous session `ses_ef9ebcc0effem4pNYZsz6yoO3U` (1:47 PM) and the
  parent autonomous 1.0.9-b sessions.
- Provenance command: `OPENCODE_SESSION_ID` was **unset** in the executor shell,
  so `opencode export "$OPENCODE_SESSION_ID"` expanded empty, entered the
  interactive `Select session to export` picker and blocked until the shell
  120 s timeout — no export produced (recorded in
  `/tmp/opencode/1.0.9-b-5/164/evidence.log`). `opencode session list` under
  `timeout 20` succeeded (exit=0); fallback = runtime Task provenance above.

### (b) satisfaction — own pin + real cycle status file

Acceptance criterion (b) is satisfied twice over, both non-simulated:

- **(i) This report's own pre-DO pin.** The `## Versioned evidence contract rv=1`
  block above commits row `G2-LIVE` **before** any DO evidence command
  (PLAN-before-DO, D:164.1) — the contract is pinned by this cycle, not merely
  described.
- **(ii) A real cycle status file recording the contract revision.** Group-1
  `docs/specs/status/iteration-15-cached-path-183-2.md:150` carries
  `## Evidence contract rv=2 (authoritative, final)` with G1-PERF/G1-CACHE
  **met** and G1-CTE **N/A** (`git show
  collection/1.0.9-b-5/group-1:docs/specs/status/iteration-15-cached-path-183-2.md`
  exit=0; lines 150/156/157/158), produced by real `planner`/`check` runs in this
  collection's session. That file exercised the same versioned contract
  end-to-end (including the `rv=1 → rv=2` supersession at ACT).

Together (i)+(ii) establish that the versioned evidence contract was actually
applied at runtime in this collection, not just present as text.

## (b) trace artifact

- Artifact: `docs/specs/status/pdca-evidence-164-planner-trace.txt`
  (absolute: `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-2/docs/specs/status/pdca-evidence-164-planner-trace.txt`)
- sha256: `eb6525b462dd07305f9cc0366c02a728b5c1d045a5764ee63bc41218a48215e2`
- Lines: 25
- Content: the verbatim Task result of planner `ses_ef81912e9ffez8Z0xiwWjVCaU6`.
  It contains `rv=1`, `G2-LIVE`, `G2-LIVE-B` matching the contract at line 23
  (`## Versioned evidence contract rv=1 (authoritative)`) of this file.
- Gather command (exit code 0):
  `wrk=/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-2; f="$wrk/docs/specs/status/pdca-evidence-164-planner-trace.txt"; test -s "$f" && sha256sum "$f" && wc -l "$f" && rg -n 'rv=1|G2-LIVE|G2-LIVE-B' "$f"`

### G2-LIVE status under rv=1

- `pass` for captured file/runtime facts: C1–C4 shell exit=0, all required
  anchors present, stale role files clean of a standalone `rv` token, and
  session-load ordering after restart.
- Explicitly **unproven**: no persisted export/transcript of the `skill`-tool
  load or of Task `ses_ef9ea7678ffeTDqxNf2u64wYcJ` (export blocked) — the load
  is asserted from in-session observation plus the on-disk anchors; mtime is
  wall-clock only (no cryptographic ordering); group-1 is terminal incomplete
  and is corroboration only.

## Provenance

Re-gathered at CHECK (worktree `1.0.9-b-5/group-2`, 2026-10-04) with
`sha256sum <path>` and `stat -c '%s %y' <path>` (exit=0 each):

| Ref | Absolute path | sha256 | size (bytes) | mtime |
| --- | --- | --- | --- | --- |
| P1 | `/home/alex/.config/opencode/skills/pdca-dotnet/SKILL.md` | `53e270d31c77372e2e479e62f3f08657636696ad028457acf5c7a243802be0c7` | 97097 | `2026-10-03 16:32:21.341186996 +0500` |
| P2 | `/home/alex/.config/opencode/skills/pdca-dotnet/assets/agents/planner.md` | `0807ff2b8e1d90571341a34da6e7dc28050e2fbcc6af379ca0383dd1b22c0006` | 8084 | `2026-10-03 16:32:35.390177743 +0500` |
| P3 | `/home/alex/.config/opencode/skills/pdca-dotnet/assets/agents/check.md` | `b92b29b69484aabf4de15e81a7d6afb9245dffd0438251cd186005136a675179` | 5395 | `2026-10-03 16:32:43.865721152 +0500` |
| P4 | `/home/alex/.config/opencode/agents/planner.md` | `7b84cdf0380f73ed196694a7e571a6a6fdecafa83acaa8f07ec5e7381fc99522` | 12425 | `2026-10-03 16:32:53.768375963 +0500` |
| P5 | `/home/alex/.config/opencode/agents/check.md` | `d2fdbca2e18c6f6534f50b5f75ad028d0902e56603db9c2508b8a4f2889b7c32` | 8644 | `2026-10-03 16:33:02.477273960 +0500` |

P1 is the **source-of-truth file identity** of the global `SKILL.md` (not the
loaded wrapper); the wrapper actually returned by the `skill` tool is cited in the
provenance statement below. P4/P5 are the host role agents (`name: planner` /
`name: check`) installed from P2/P3.

### Contract anchors, `nl -ba` (exit=0 each)

`SKILL.md`:

- line 886 → `### Versioned evidence contract`
- line 903 → `` - The contract revision `rv`. ``
- line 910 → `**DO ledger:** record actual evidence against each row ID and `rv`: verified test symbols or`
- line 1085 → `### CHECK completeness gate — mandatory evidence contract`
- line 1089 → `` current `rv`. Required artifacts must exist and support the reported result. N/A is valid only ``

Installed `agents/planner.md`:

- line 40 → `` «Versioned evidence contract» глобального скилла `pdca-dotnet`. До DO закрепи контракт ``
- line 44 → `` наблюдаемый предикат применимости и ревизия `rv`. На выбор PLAN — только представление: ``
- line 117 → `` `rv`, стабильные ID, предикаты применимости, бюджет/владелец CHECK re-gather и явный ``

Installed `agents/check.md`:

- line 38 → `` - Закреплённый версионированный evidence contract с текущей `rv`, DO ledger с привязкой ``
- line 67 → `8. **Обязательный гейт полноты evidence:** применяй раздел «CHECK completeness gate —`
- line 69 → `` обязательную строку с фактическим evidence для текущей `rv`; проверь артефакты ``

### Stale role files are not host roles

`grep -cE '(^|[^A-Za-z])rv([^A-Za-z]|$)'` = **0** in both
`/home/alex/.config/opencode/agents/pdca-planner.md` and
`/home/alex/.config/opencode/agents/pdca-check.md`. They carry only the
domain-neutral plan-revision token `revision r` / `revision r1` (frontmatter
`name: pdca-planner` / `pdca-check`) — **no** versioned evidence `rv` contract.
They are **not** the host role agents; the host roles are `agents/planner.md` and
`agents/check.md` (P4/P5), which do carry the `rv` token.

### Provenance statement

Session trace (direct evidence of load): the loaded `pdca-dotnet` skill content is spilled at `/home/alex/.local/share/opencode/tool-output/tool_10746e894001aaPVuuBcdcnZt4`; line 884 = `### Versioned evidence contract`, line 1083 = `### CHECK completeness gate — mandatory evidence contract` (a +2 line offset vs the source file`/home/alex/.config/opencode/skills/pdca-dotnet/SKILL.md` because the tool output wraps the file with a `<skill_content name='pdca-dotnet'>` header line). This proves the two required sections were loaded by the primary OpenCode session `ses_efa0237e9ffe6w6pOI50N73e5H` (created 2026-10-04, after the 2026-10-03 16:32 skill edit).

## CHECK verdict

PASS (rv=1): (a) met — loaded-skill spill tool_10746e894001aaPVuuBcdcnZt4:884/1083 in post-restart session ses_efa0237e9ffe6w6pOI50N73e5H; (b) met — real planner run ses_ef81912e9ffez8Z0xiwWjVCaU6 recorded rv=1 (G2-LIVE/G2-LIVE-B) with trace artifact sha256 eb6525b462dd07305f9cc0366c02a728b5c1d045a5764ee63bc41218a48215e2.

## ACT

## Done / Verified / Incomplete

Done: #164 G2-LIVE + G2-LIVE-B
Verified: CHECK PASS rv=1
Incomplete: —
