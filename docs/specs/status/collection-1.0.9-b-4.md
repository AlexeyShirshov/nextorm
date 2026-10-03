# Collection 1.0.9-b-4

- collection-id: `1.0.9-b-4`
- input: open issues of GitHub milestone `1.0.9-b` (#158, #152)
- mode: autonomous + auto-commit; single group → no worktree/branch, commits go into the current branch `1.0.9-b`; push never
- cap: 1 (single group; flat primary, no nested Task)
- base: `f3d1ece`
- execution: flat primary (nested Task unavailable), current worktree
- status: IN-PROGRESS

## Group

| id | tasks (order) | worktree | branch | status |
|---|---|---|---|---|
| group-1 | 158,152 | (current worktree) | `1.0.9-b` (current) | done |

## Tasks

| id | issue | group | branch | status | task status file |
|---|---|---|---|---|---|
| 1 | #158 | group-1 | 1.0.9-b | done | brainstorm-preflight-158-1.md |
| 2 | #152 | group-1 | 1.0.9-b | done | pdca-evidence-152-1.md |

## Decisions

- Clustering (planner, P:r2, 2026-10-03): one group, order #158 → #152. Order = risk order under group-stop semantics: #158's diff already exists (small, low risk); #152 is large and touches global files.
- Variant per task: `pdca-coder` (Markdown skill/config edits, no C# code) + applicable `nextorm-pdca` invariants.
- #152 global edits live under `/home/alex/.config/opencode/` which is NOT a git repo → those edits are outside VCS; only repo-side paths are auto-committed. Post-restart live validation deferred-with-trigger (first OpenCode session after changes are loaded).
- #158 baseline/behavior scenarios are process evidence in the evidence bundle outside the repo; repo commit for #158 = the SKILL.md deliverable (+ this status bookkeeping).
- Evidence root: `/tmp/opencode/1.0.9-b-4/`.

## Общая верификация и восстановление

- last C: (none)
- reason: (none)
- defect id/history: (none)
- corrective task status file: (none)
- next allowed step: collection C (aggregate all task reports -> check verdict)

## Done / Verified / Incomplete

- #158 done — preflight of prior design decisions added to `.opencode/skills/nextorm-brainstorming/SKILL.md` (+31/−4); R1–R7 met; CHECK PASS; commit 8722a7f30dd6aecb5643155cd0d96af7f1d6ccf6; issue closed.
- #152 done — versioned evidence contract pinned at PLAN in global `pdca-dotnet` (+ its planner/check source+installed mirrors) and project overlay; approved spec included; CHECK PASS (R1–R9 met); repo commit 77660b1220356990d03d3b04879e9a89ff5ecc89; global edits outside VCS (hashes in task status). Deferred-with-trigger: post-restart live loaded-skill validation (follow-up 164). Issue #152 closed.
