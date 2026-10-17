# Collection 1.0.9-rc3-open-2 — status

- collection-id: `1.0.9-rc3-open-2`
- slug: `collection-1.0.9-rc3-open-2`
- input: **open issues of milestone `1.0.9-rc3`**, re-read 2026-10-16 via `gh`: **#207, #234, #241, #242**.
  - **#241** — new work (in-repo): the sorting/grouping cancellation slice split out of #207. **DELIVERED** (`e77d5f41`).
  - **#242** — global canonical evidence-gate admission (Obligation A) + `required_rows` ↔ `rows` coverage fold-in (Obligation B). **DELIVERED** by the owner follow-up (`opencode-config` `063dffa` + nextorm `4df1a03e`); the collection-run DO had stopped `incomplete` on an external host-config blocker, the owner then executed the admission directly and it was independently verified (see lane log).
  - **#207** — delivered (`ec42c960`); remainder split to #241 → admin closure.
  - **#234** — in-repo slice delivered (`7acad56d`/`be0cbadb`) + rv3 closure via #242 → **CLOSED**.
- base: branch `1.0.9-rc3` @ `83e78d37`.
- mode: **autonomous + auto-commit**. Single group → current worktree/branch `1.0.9-rc3`, no worktree/group-branch/merge. Push owner-authorized (`GIT_ALLOW_PUSH=1` for the project pre-push hook).
- phase: **A — collection COMPLETED** (C = PASS on `e77d5f41`; T242 subsequently delivered by the owner and verified).

## Tasks

| id | issue | title | variant | status | plan_state | cycle N | r | rv | status file | baseline ref |
|----|-------|-------|---------|--------|-----------|---|--|----|-------------|--------------|
| T241 | #241 | Uniform cancellation handling during sorting/grouping build | pdca-dotnet | done | closed | 1 | 1 | 1 | docs/specs/status/D241-sorting-cancellation-1.md | 83e78d37 |
| T242 | #242 | Evidence pipeline: canonical global pdca-dotnet admission + required_rows coverage fold-in | pdca-coder | done | closed | 1 | 1 | 3 | docs/specs/status/D242-evidence-canonical-admission-1.md + D234 rv3 | 83e78d37 |

## Groups (allocated only after ALL-barrier)

| group | tasks (topological order) | worktree | branch/ref | status |
|-------|---------------------------|----------|------------|--------|
| G1 | **T241 → T242** | none (single group) | current branch `1.0.9-rc3` (tip `4df1a03e`) | **done** — T241 `e77d5f41`; T242 delivered by the owner (`063dffa` + `4df1a03e`), verified |

## Phase decisions

- **P.1 Bootstrap (2026-10-16):** scope = the 4 remaining open issues of milestone `1.0.9-rc3`. Variant by language rule: #241 → `pdca-dotnet`; #242 → `pdca-coder`. #207/#234 are disposition-only.
- **P.2 Own PLAN per task — DONE (2026-10-16).** Both PLANs `ready` at N=1/r=1/rv=1:
  - **T241** — footprint `QueryCommand.QueryPreparer.cs` (3 sites), tests core/sqlite/integration, `code-smells-review.md`; contract `E241-*` (9 rows) + criteria R241-01..12; evidence `artifacts/pdca/D241/rv1/`; CHECK re-gather budget 2. Oracle = #207 (`ec42c960`).
  - **T242** — footprint: global `~/.config/opencode/skills/pdca-dotnet/{SKILL.md,scripts,tests}` + in-repo `scripts/**` + overlay/D234 docs; contract `E242-01..14` + criteria R242-A..G; evidence `artifacts/pdca/D242/rv1/`; CHECK re-gather budget max 2. Global dir is **not a git repo → mandatory pre-write backup**.
- **P.3 ALL-barrier — PASSED (2026-10-16).**
- **P.4 Clustering (2026-10-16) — ONE group G1: T241 → T242.** Single sequential lane on the current branch; no worktree/group-branch/`merge --no-ff`.
- **D merge strategy:** n/a — single group; commits land in the current branch.

## Lane log

- 2026-10-16 | T241 | **PLAN ready** | `docs/specs/status/D241-sorting-cancellation-1.md` (290 lines, CRLF); 9-row contract; budget 2.
- 2026-10-16 | T242 | **PLAN ready** | `docs/specs/status/D242-evidence-canonical-admission-1.md`; 14-row contract `E242-01..14`; budget max 2; global write owner-authorized; backup mandatory.
- 2026-10-16 | T241 | **done** | commit **`e77d5f41`** (6 files: `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` — 3 sites → `ThrowIfCancellationRequested` (`:2716` sorting, `:3488` key-columns, `:3528` grouping); new `tests/nextorm.core.tests/SortingGroupingCancellationTests.cs`, `tests/nextorm.sqlite.tests/SortingGroupingCancellationTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite.SortingGroupingCancellation.cs`; `docs/specs/design/code-smells-review.md`; D241 status). CHECK PASS — 9/9 `E241-*` met, both linters rc 0 (manifest tree `2a6519e5…`), re-gather 0/2. RED core 3/3 + sqlite 4/4 → GREEN core 5/5 + sqlite 6/6 + integration 4/4. Build Debug/Release 0W/0E; 12/13 projects rc 0 (12175 selected / 0 failed / 207 skipped); coverage 88.2618→88.2656 % line, 80.3412→80.3444 % branch; mutation 26 killed / 8 survived / 2 no-cov (72.22 %); perf 7/7, ratio 1.661 (baseline 1.87, trigger 2.244). Deferred: `V241-18` (mid-loop `k>0` rollback atomicity); `:3528` guard is dominated by `BuildKeyColumns:3488` on pre-cancelled public calls (documented).
- 2026-10-16 | T242 | **incomplete (STOP)** | DO stopped on the **mandatory pre-install global baseline**; `escalate` returned STOP; **no CHECK, no ACT, no commit**. Global dir **untouched** (`diff -qr` rc 0 vs backup `~/.config/opencode/pdca-backups/T242/20261016T175730.561527070Z/pdca-dotnet`). In-repo changes preserved as `artifacts/pdca/D242/rv1/task.patch` (32443 B, md5 `eb4520b1db89cfcb2bbf6c16be32d8e7`; 5 files incl. new `scripts/tests/test_pdca_global_admission.py`) and **rolled back** from the tree. In-repo units that DID pass before the stop: `repo-unit` rc 0 (62 tests), `repo-negative` rc 0 (24 cases incl. `open`/stale/extra/missing-row ⇒ lint rc 2), coverage 95.35 %, `repo-mutate` 7/7 killed, `validator-unchanged` rc 0.
- 2026-10-16 | **C** | **PASS** on `1.0.9-rc3` @ `e77d5f41` | build Debug/Release 0W/0E; unit suites core 2607/0, sqlite 1765/1, postgres 981/0, sqlserver 972/8, mysql 459/0, mariadb 341/0, clickhouse 738/0; integration 5 providers rc 0 (3989 total / 0 failed / 198 capability skips; no flake; T241 case ×4 discovered+passed); coverage line **87.4 %** / branch **79.3 %**; DocFX 0 errors (2 pre-existing warnings); suppressions 0/0; `scripts/**` porcelain empty. Evidence `artifacts/pdca/collection-1.0.9-rc3-open-2/C/gather.md` + `check` verdict.
- 2026-10-17 | T242 | **done (owner follow-up, verified)** | Obligation A: canonical admission in `opencode-config` commit **`063dffa`** (pushed to `origin/main`, tree clean) — 5 files promoted; deployed copy source-identical (md5 of all five admitted files match; only `__pycache__/*.pyc` differ). `SKILL.md` gained §Versioned evidence contract → **Mechanical evidence pipeline** and a mandatory both-helper gate in §CHECK completeness gate. Obligation B: two-way `required_rows` ↔ `rows` coverage in `pdca_evidence.py lint` — independently reproduced on `artifacts/pdca/T234/rv3` (clean rc 0; drop from `rows` → rc 2 `required row missing from manifest: 'E234-05'`; drop from `required_rows` → rc 2 `manifest row not declared in required_rows: 'E234-05'`). rv3 manifest: `contract_rv 3`, tree `a5bd0251…`, sha256 `e77750df…` (second generate byte-identical), both gates rc 0 (also re-checked after the landing commit). Skill suite: 63/63 unit, 18/18 negatives, coverage **95.29 %** (526/552, threshold 90), 6/6 mutants killed, full suite 316 OK / 28 skipped. nextorm record: commit **`4df1a03e`**. Remaining external note: the host-enabled suite still shows the **pre-existing** 14F/6E (`config/agents/*.md` plural `permissions:` vs the test's singular `permission:`), unrelated to this admission.

## General verification & recovery

- state: **`pass`**.
- last C result / evidence: **PASS** — `1.0.9-rc3` @ `e77d5f41`; `artifacts/pdca/collection-1.0.9-rc3-open-2/C/gather.md` + `check` verdict. Build Debug/Release 0W/0E; 7 unit suites rc 0; integration 5 providers rc 0; coverage 87.4 % / 79.3 %; DocFX 0 errors; suppressions 0; tree identity `e77d5f41`.
- T242 post-C verification (not part of C, which ran before the owner follow-up): canonical admission verified independently (see the 2026-10-17 lane entry); rv3 gates rc 0; issue closure acknowledged.
- defect id / history: no product defect found by C. The T242 blocker encountered during the collection run was **EXTERNAL** (canonical host repo `~/sources/opencode-config`: plural `permissions:` vs the test's singular `permission:` → 14F/6E in the host-enabled baseline); it was bypassed by the owner's direct canonical admission and remains a pre-existing, unrelated host-config issue.
- correction-task status file: none.
- next allowed step: **A — done**; milestone `1.0.9-rc3` has no open issues in this collection's scope.

## Done / Verified / Incomplete

- **Done:** **T241 (#241, `e77d5f41`)** — uniform `OperationCanceledException` in sorting/grouping preparation, mirroring #207; **T242 (#242, `063dffa` + `4df1a03e`)** — canonical global admission + coverage fold-in (rv3).
- **Verified:** collection C = **PASS** on `1.0.9-rc3` @ `e77d5f41`; T242 follow-up verified post-C (both gates rc 0, rv3 digest idempotent).
- **Incomplete:** none.
- **Accepted residuals (not defects):** T241 `V241-18` mid-loop `k>0` atomicity (deferred with trigger); T241 `:3528` guard domination (documented).
- **Issue disposition:** **#241 → CLOSED**; **#207 → CLOSED**; **#242 → CLOSED** (delivered + verified); **#234 → CLOSED** (rv3 closure).
- **Pre-existing, unrelated:** `opencode-config/config/agents/*.md` use plural `permissions:` while `config/skills/pdca-dotnet/tests/test_routing_consistency.py` parses singular `permission:` → host-enabled skill suite 14F/6E. Not introduced by #242; needs a separate fix in `opencode-config`.
