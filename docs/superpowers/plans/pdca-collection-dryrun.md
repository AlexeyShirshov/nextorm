# pdca-collection — dry-run checklist

**Status:** live run **DEFERRED**. The skill and the `lane` agent are loaded only
at opencode start (config is load-once), so this session cannot execute the new
skill live. The mechanical parts are covered now by
`tests/pdca-collection/git-model-sim.sh` (green); the agent-orchestration path
runs after a restart.

## Input (synthetic)

- `collection-id`: `dryrun-1`
- **Group A:** T1 (adds `a.txt`), T2 (adds `b.txt`) — both expected to pass.
- **Group B:** T3 (adds `c.txt`, passes), T4 (intentionally fails its own `C`).
- Expected outcome per spec §5/§8: A merges fully; B's lane stops at T4, its
  committed tip (T3) merges, T4 is reported `blocked`; A and B run in parallel.

## Checklist (spec §10)

- [ ] 1. Lanes start in parallel, each in its own worktree (`<repo>-worktrees/dryrun-1/<group>`).
- [ ] 2. Inside a group, task branches chain (`collection/dryrun-1/task-<g>-<k>`) with auto-commit per task.
- [ ] 3. Successful groups merge `--no-ff` into the current branch, one at a time.
- [ ] 4. The failing task stops only its lane; the other group reaches merge.
- [ ] 5. Final report marks T4 `blocked`; merge-commit messages carry `#<n>` when issue-bound.
- [ ] 6. `pdca-dotnet` gates were not skipped (gate 1 criteria/test-strategy/docs/perf; gate 2 all streams; gate 3 all lenses).
- [ ] 7. A task that keeps looping back (>3 attempts) → `escalate`, then the lane stops; no 4th attempt.
- [ ] 8. Auto-commit stages only the task's own files (`git add <paths>`), never `git add -A` (no foreign/build-artifact files).
- [ ] 9. A single-group (or no-independent-tasks) input degrades to one lane and does not fail; an empty input is reported, not crashed.
- [ ] 10. Installing/writing global `~/.config/opencode/**` handles the `external_directory` approval, not a silent failure.

## Verified now (no restart)

- [x] Git mechanics — `bash tests/pdca-collection/git-model-sim.sh` → `OK: group-A merged, group-B merged, 2 merge commits` and `OK: conflict detected, group marked failed`.
- [x] Skill frontmatter valid; sections `Коллекция (P/D/C/A)`, `Лейн`, `Git-модель`, `Сбой`, `Статус коллекции`, `Отчёт`, `Параметры`, `Связь с pdca-dotnet`, `Fallback` present.
- [x] `lane` agent frontmatter valid (`mode: subagent`, `edit/bash: deny`, task allow-list).
- [x] `AGENTS.md` §Git exception present, surrounding text unchanged.

## Deferred (after restart)

- Live parallel lane execution and agent orchestration (nested `Task`), auto-commit inside the lane flow, and the report format.
- Regression check of checklist item 4 with a real failing task.
