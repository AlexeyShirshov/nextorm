# Task #152 — PDCA mandatory versioned evidence contract at PLAN

- issue: [#152](https://github.com/AlexeyShirshov/nextorm/issues/152) (milestone `1.0.9-b`)
- collection: `1.0.9-b-4`, task 2, group-1, branch `1.0.9-b` (current worktree)
- variant: `pdca-coder` + nextorm-pdca invariants; r=1, n=1
- approved spec: `docs/specs/design/issue-152-pdca-evidence-contract.md` (untracked)
- evidence root: `/tmp/opencode/1.0.9-b-4/`
- global files (outside VCS, `/home/alex/.config/opencode` is NOT a git repo):
  `skills/pdca-dotnet/SKILL.md`, `skills/pdca-dotnet/assets/agents/{planner,check}.md`, `agents/{planner,check}.md`

## Plan (planner P:r2)

- D:152.1 baseline all five global files; baseline pressure scenarios before edits.
- D:152.2 normative scheme in global `skills/pdca-dotnet/SKILL.md` (contract/ledger/CHECK completeness/bounded gather/rv rules).
- D:152.3 REQUIRED output slots in source `assets/agents/{planner,check}.md` + installed `agents/{planner,check}.md`.
- D:152.4 overlay `.opencode/skills/nextorm-pdca/SKILL.md:85-86` — pin application without weakening project obligations.
- D:152.5 include approved spec as deliverable.
- D:152.6 structural/proxy checks + CHECK + ACT; post-restart live validation deferred-with-trigger (does NOT yield a false PASS).

## DO ledger

- D:152.1 baseline: 7 files backed up byte-identical to `/tmp/opencode/1.0.9-b-4/baseline/152/` (manifest.txt). `/home/alex/.config/opencode` is NOT a git repo.
- D:152.2 global `skills/pdca-dotnet/SKILL.md`: +54 (new `### Versioned evidence contract` at :886-919, new `### CHECK completeness gate` at :1085-1104); additions-only.
- D:152.3 source assets `skills/pdca-dotnet/assets/agents/{planner,check}.md` (+12/+12) and installed `agents/{planner,check}.md` (+15/+13); R8 source EN = live RU, no contradiction, no competing definition.
- D:152.4 overlay `.opencode/skills/nextorm-pdca/SKILL.md` +16 (new `### Обязательный evidence contract` at :206-221, reference-only).
- D:152.5 spec `docs/specs/design/issue-152-pdca-evidence-contract.md` is the deliverable (untracked).
- D:152.6 checks: additions-only, frontmatter/EOL preserved (`/tmp/opencode/1.0.9-b-4/152/structural-audit.md`); behavior sims baseline-vs-updated + S5 microtest (`/tmp/opencode/1.0.9-b-4/152/scenarios.md`); live hashes `/tmp/opencode/1.0.9-b-4/152/live-hashes.txt`. Post-restart live validation DEFERRED with trigger (first OpenCode session loading the global skill).
- D:152.7 blank-line formatting fix in overlay `.opencode/skills/nextorm-pdca/SKILL.md`: one blank line inserted before `### Перф-приёмка cached path` (now 290 lines, +17 vs baseline, CRLF preserved); fresh re-verification of all six edited files + spec — only the overlay changed, sha256 `a9dad20b6bf32c9607c4a39481e13691baf7886bb97a8e33d6c1c0bcd43fa7e2`; frontmatter head-5 identical, EOL clean, grep scope confined to edited files, `git diff --check` exit 0 — full log `/tmp/opencode/1.0.9-b-4/152/verify.log` (live hashes refreshed in `/tmp/opencode/1.0.9-b-4/152/live-hashes.txt`).

## CHECK

- 2026-10-03: CHECK PASS (independent `check`, full R1–R9 matrix, all P1, exact global anchors, fresh verification). 9 met / 0 unmet / 0 unverified. Accepted limitations: behavior is single-shot simulation (not live loaded skill); live validation deferred-with-trigger. No D:/P: loop-back.

## ACT

- 2026-10-03: repo deliverable committed on `1.0.9-b` (commit <H1>): overlay `.opencode/skills/nextorm-pdca/SKILL.md` (+17) and spec `docs/specs/design/issue-152-pdca-evidence-contract.md`.
- Global edits (OUTSIDE VCS — `/home/alex/.config/opencode` is not a git repo), recorded by path + sha256 + backup:
  - `skills/pdca-dotnet/SKILL.md` sha256 `53e270d3…`; `skills/pdca-dotnet/assets/agents/planner.md` `0807ff2b…`; `skills/pdca-dotnet/assets/agents/check.md` `b92b29b6…`; `agents/planner.md` `7b84cdf0…`; `agents/check.md` `d2fdbca2…`; backups `/tmp/opencode/1.0.9-b-4/baseline/152/`; live hashes `/tmp/opencode/1.0.9-b-4/152/live-hashes.txt`.
- Deferred-with-trigger (milestone 1.0.9-b): live loaded-skill validation at the first OpenCode session that loads the edited global skill; tracked in follow-up issue <FUP>.
- Repo commit does NOT include the global edits; it is not a portable delivery of #152 by itself.
