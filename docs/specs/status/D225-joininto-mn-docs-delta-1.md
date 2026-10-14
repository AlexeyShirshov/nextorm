# D225 — T225 (#225): JoinInto composite-junction M:N docs delta (AC07 handoff, T213)

- Task: T225 / issue [#225](https://github.com/AlexeyShirshov/nextorm/issues/225)
- Collection: `1.0.9-todo`, group **G1** (single group — no worktree, no branch, no merge); branch `1.0.9`
- Variant: `pdca-dotnet`; cycle **N=1**; plan_revision **r=2**; evidence-contract **rv=1 (frozen by PLAN r=2)**
- **Plan state: `ready`** (re-open of T225) — history: PLAN r=1 = gap → escalation decision (B) → terminal `incomplete` (record preserved below)

## Phase state

`PLAN` → gap → `escalate` → **terminal incomplete**. DO was not started; no product or documentation writes were made.

- **r=2 (re-open):** a fresh `planner` revision took the prior terminal cause as mandatory input (invariant 12) and issued a genuinely executable plan — the prior r=1 was a gap declaration, not a plan. Phase state is now `PLAN` → **`ready`**; DO not yet started.

## Goal

Land the AC07 documentation delta handed off from #213 (`D05-HANDOFF2`) so EN+RU documentation accurately describes the delivered `JoinInto` behavior, without incorporating or overwriting the externally owned documentation reorganization.

## Acceptance criteria (carried AC07, unchanged; `docs/specs/status/D213-joininto-remainder-1.md:34`)

- AC01: scalar M:N `JoinInto` works standalone and under `EagerLoadMode.SingleQuery`.
- AC02: composite relationship keys supported (1:N / 1:1, and the scalar M:N junction).
- AC03: composite-junction M:N rejected in SQL generation with `NotSupportedException("composite-junction M:N is not supported; see #223")`.
- AC04: the in-memory provider still stitches a composite junction.
- AC05: EN/RU accurate; changed authored files CRLF; numbering/links/TOCs consistent; public pages do not link into `docs/specs/**`.
- AC06: Debug solution build 0 warnings / 0 errors; filtered coverage line >=85% and branch >=75%.

## Plan r=1 (planner, medium) — GAP

- Goal/criteria as above; `AC07.patch` left **as-is** (no reapply, no re-baseline against HEAD); working-tree application **deferred** on the verbatim trigger.
- Trigger: "the external owner commits/discards the churn so `git status --porcelain` is clean on all 15 footprint files." — **unmet**.
- Deferred EN footprint: `docs/advanced/{eager-loading,limitations,relationships}.md`, `docs/guide/{02-joins,29-implicit-navigation}.md`.
- Deferred RU footprint: `docs/ru/advanced/{eager-loading,limitations,relationships}.md`, `docs/ru/guide/{02-joins,29-implicit-navigation}.md`.
- Deferred internal footprint: `docs/specs/comparison/{capability-matrix,linq2db-backlog-gap-analysis,linq2db-comparison}.md`, `docs/specs/roadmap/{sql-capabilities-gap-analysis,todo_eager_loading_pipeline,todo_navigation_properties}.md`.
- Classification: **outside-cycle ownership blocker + evidence gaps**; confidence high that editing now would violate the handoff boundary, low that any supplied source proves completed documentation integration.
- Entanglement avoidance: zero writes to the 16 footprint paths, to any existing artifact, or to any TOC; no application to the real index/working tree.

## Escalation decision (escalate, strong tier) — (B)

Record T225 as **terminal `incomplete`** now; do **not** redefine the oracle (A would be a weaker proxy that marks AC07 satisfied without the landed docs, i.e. false completion) and do **not** apply the patch to the working tree. No revised plan is possible (planner r=1 already ruled out every in-scope edit). Routing: coder writes this status only; group G1 stops.

## Terminal cause

- AC07 trigger unmet: `git status --porcelain` shows 15 of 16 patch targets as ` M`; only `docs/specs/roadmap/todo_navigation_properties.md` is clean.
- External reorg of `docs/**`: 183 files changed (1444 ins / 11850 del); all four `toc.yml` modified; new untracked dirs `docs/engine/`, `docs/connecting-providers/`, `docs/guide/provider-specific-sql/` (plus RU mirrors); several `docs/providers/*`, `docs/querying/*`, `docs/infrastructure/01-05` deleted. The 5 EN + 5 RU patch targets are inside this dirty set.
- Oracle (A) rejected — it would alter the carried AC07 criteria.
- Applicability to current HEAD `706a83d1` unverified; T213's temp-index exit-0 was against baseline `20a67a1c` (8 commits behind).

## AC07 status

**Not satisfied and not claimed.** No EN+RU landing; Debug build, coverage (line/branch) and line-ending checks were not run, because no landing happened.

## Retained artifacts (unchanged)

- `artifacts/pdca/T213/rv4/docs/AC07.patch` — 16 files / 26 hunks / +42 −35; baseline `20a67a1c`; EN 5, RU 5, `docs/specs/**` 6.
- `artifacts/pdca/T213/rv4/docs/manifest.json` — `head_ref: 20a67a1c…`, `footprint_count_dirty: 15`, `footprint_count_total_in_patch: 16`, per-file baseline/proposed/current sha256.
- `artifacts/pdca/T213/rv4/docs/handoff-check.log` — temp-index `git read-tree 20a67a1c` + `git apply --cached --check` → exit 0.

## Required statement (verbatim — content to land)

- `scalar M:N JoinInto works standalone and under EagerLoadMode.SingleQuery;`
- `composite relationship keys are supported (1:N / 1:1, and the scalar M:N junction);`
- `composite-junction M:N is rejected in SQL generation with NotSupportedException("composite-junction M:N is not supported; see #223")`
- `the in-memory provider still stitches a composite junction.`

## Behavior anchors (for the later accuracy check)

`src/nextorm.core/Builders/EntityBuilder.cs:736,757`; `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs:125-126`; `tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:95`; `tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:201`; `tests/nextorm.core.tests/JoinIntoManyToManyInMemoryTests.cs`; `tests/nextorm.core.tests/JoinIntoManyToManySingleQueryTests.cs`; `tests/nextorm.sqlite.tests/JoinIntoManyToManySqlGenerationTests.cs:48-55`.

## Re-entry conditions (invariant 12 — prior terminal cause is mandatory input)

1. The external owner commits/discards the churn so `git status --porcelain` is clean on all 15 footprint files.
2. Re-baseline `AC07.patch` against the then-current HEAD (toc.yml changes and moved/deleted pages may invalidate hunks).
3. Re-run the temp-index `git apply --cached --check` against that HEAD.
4. Run the full AC07 oracle: EN+RU accuracy, Debug build 0 warnings/0 errors, coverage line >=85% / branch >=75%, CRLF.
5. Re-entry runs as a fresh `planner` revision that takes this cause as input. T225 stays an open issue in milestone **1.0.9** (invariant 8; not deferred to another milestone).

## Evidence contract

`rv=1`; intended path `artifacts/pdca/D225/rv1/manifest.json` — **not frozen** (blocked: no approved integration oracle and no supplied global unconditional row set). No manifest was created; no row was marked `na`.

## Progress log

- PLAN gather: `scout` × 3 (issue #225 + T213 AC07 handoff; docs footprint + external reorg; `AC07.patch` + trigger condition).
- PLAN decide (planner, medium): r=1 = gap, classification outside-cycle blocker; routed to escalate.
- Escalate (strong): decision (B) — terminal `incomplete`.
- Status persisted by coder; committed (`#225`).
- 2026-10-13 03:46 UTC | PLAN | revision r=2 | iteration 1/3 | re-open: planner issued a genuinely new executable plan (r=1 was a gap declaration); plan_state=ready, contract rv=1 frozen | docs/specs/status/D225-joininto-mn-docs-delta-1.md
- 2026-10-14 18:03 UTC | DO | revision r=2 | iteration 1/3 | DO started (docs carry, footprint-limited): B `aea0a9ba` verified present + ancestor of HEAD `ee3ee9fe`; 16 footprint paths clean (`git status --porcelain` empty); scope validated | artifacts/pdca/D225/rv1/scope.json (`validate_inner_loop.py brief` exit 0)
- Notice: host tool catalog exposes no todowrite; sidebar todo mirror not updated this cycle; status file is the sole progress source.
- Notice: plan r=2 text says milestone 1.0.9 / branch 1.0.9; the collection re-read renamed them to 1.0.9-rc3 / 1.0.9-rc3. Cosmetic drift; goal, criteria and footprint unaffected.
- 2026-10-14 18:06 UTC | D2 | revision r=2 | iteration 1/3 | D2 blocked → **DO→PLAN candidate**: commit `d76c5ae3` (#223 Composite-junction M:N SQL link carrier, T223, 2026-10-13 14:49 +0500) is an ancestor of HEAD and landed after plan r=2 froze, so criterion C3 (document the SQL-generation rejection with the exact message) is obsolete — the message is absent from `src/**`, `JunctionLinkSourceFactory.cs:205-206` builds the composite carrier, and the designated oracle now asserts **support** (exit 0, selected 4, 4 passed). No footprint file was edited; D2 stays blocked/pending (not done, not superseded). | artifacts/pdca/D225/rv1/{disposition.md,semantic-review.md,DO_TO_PLAN.md,oracle-rejection.log,oracle-remainder.log}

## Done / Verified

None. T225 is terminal `incomplete`; its contribution to the collection ALL-barrier is **not** satisfied.

---

## Plan r=2 (planner, medium) — READY (re-open)

- `plan_state: ready`. This is a genuine replan of **cycle N=1** (revision **r=2**): the prior **r=1** was a gap declaration, not an executable plan; the new plan takes the terminal cause as mandatory input.
- **Goal:** land the missing AC07 documentation over the now-committed external reorg, keeping the reorg, matching T213's delivered behavior; milestone stays **1.0.9**.
- **Constraints:** footprint-limited; no #223 implementation, no API/test changes, no commits/branches/merge.
- **Baseline B** = `aea0a9ba644a8be34f5e9bf39e4daf4a33861f17`; **artifact dir A** = `artifacts/pdca/D225/rv1`.
- Source snapshots for the carry: `baseline/`→`proposed/` at `artifacts/pdca/T213/rv4/docs/`.

### Acceptance criteria (each with a negative case), P1 by construction

- **C1:** scalar M:N `JoinInto` documented working standalone AND under `EagerLoadMode.SingleQuery`; negative — one mode missing or wrongly forbidden.
- **C2:** composite relationship keys documented for 1:N, 1:1 and scalar M:N junction; negative — conflation with composite-junction M:N.
- **C3:** SQL-generation rejection recorded exactly as `NotSupportedException("composite-junction M:N is not supported; see #223")`; negative — promising SQL support or a different text/exception type.
- **C4:** in-memory composite-junction stitching retained/documented; negative — extending the SQL prohibition to in-memory.
- **C5:** EN/RU consistent, CRLF preserved, numbering/links/TOCs correct, no public→`docs/specs/**` links; negative — translation drift, LF/mixed endings, numbering hole, forbidden/broken link.
- **C6:** Debug build exit 0 with 0 warnings / 0 errors; negative — nonzero exit or any warning.
- **C7:** coverage line ≥85%, branch ≥75%; negative — below either threshold, incomplete report, or skipped mandatory database-provider tests.
- **C8:** the new patch applies conflict-free to the frozen baseline B, preserves the committed reorg and is limited to the footprint; negative — conflict, out-of-footprint file, loss of committed text, or a silently changed baseline.

### Footprint (16 files)

`docs/advanced/{eager-loading,limitations,relationships}.md`, `docs/guide/{02-joins,29-implicit-navigation}.md`, their exact `docs/ru/**` mirrors, `docs/specs/comparison/{capability-matrix,linq2db-backlog-gap-analysis,linq2db-comparison}.md`, `docs/specs/roadmap/{sql-capabilities-gap-analysis,todo_eager_loading_pipeline,todo_navigation_properties}.md`. Additional writes allowed only in `$A/**`, this status file, and the collection status (which this task must NOT edit). Old T213 artifacts are immutable. Do not move/renumber public pages or TOC paths.

### Ordered tasks

- **D1** (this step, PLAN record): persist this full PLAN r=2, `plan_state=ready`, contract `rv=1` into this status file; freeze baseline B, footprint, source-artifact hashes, CHECK re-gather budget.
- **D2** (DO, after the allowed DO start): verify B unchanged / footprint clean; load `running-integration-tests` skill; prepare toolchain/Podman; build a disposition map of the 26 original hunks and carry the semantic delta onto all 16 files (8 conflict files first, then sync mirrors), using the `baseline/`→`proposed/` snapshots at `artifacts/pdca/T213/rv4/docs/`.
- **D3**: independent semantic audit EN/RU against C1–C4 and T213 code/tests — `src/nextorm.core/Builders/EntityBuilder.cs:736,757,815,838,883`, `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs:126`, `tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:201`, `JoinIntoCompositeJunctionRejectionTests.cs:95,145`; symbols Roslyn-first. Structural audit covers TOCs/links without widening the write-footprint.
- **D4**: produce the rebased patch, run temp-index apply-check, structural checks, build/coverage, fill the manifest with actual results; then CHECK.
- **Deferred:** implementing SQL composite-junction M:N — separate #223; a delivery trigger would require a new docs delta, not a criterion here.

### Variant matrix (drift class × surface)

- 8 conflict files → test: EN eager-loading, limitations; RU eager-loading, limitations, 02-joins; EN specs three comparison files; guard: RU specs has none.
- 8 clean files → test: EN relationships, 02-joins, 29-implicit-navigation; RU relationships, 29-implicit-navigation; EN specs three roadmap files; guard: RU specs has none.
- Behavioral matrix: scalar M:N standalone/SingleQuery, composite 1:N/1:1, SQL composite-junction rejection, in-memory composite-junction — test/audit via C1–C4 and existing T213 suites across sqlite/sqlserver/mysql/mariadb/postgres/clickhouse; `null`/default/value-reference and new flags unchanged — guard (diff touches no code/API).

### Test strategy

No new unit/integration tests; the existing solution suite supplies regression/coverage evidence. CHECK rebuild policy = `none` (docs-only); the one mandatory Debug build is run once for C6 and its evidence is consumed by CHECK.

### Docs plan

All 16 footprint files audited; only missing/inaccurate statements changed; generated docs not edited.

### Perf-measurement decision

Not required — documentation-only change, not a per-row/runtime path (`JunctionLinkSourceFactory.cs:126`, `EntityBuilder.cs:736` unchanged); cached-path acceptance inapplicable per nextorm overlay.

### Reconnaissance decision

Not needed — clean HEAD, available snapshots and measured conflicts suffice; D2 is a carry-and-verify, not an architecture spike.

### Unit execution mode

Single tree, sequential (mirrors, semantics and one shared manifest overlap); no worktrees.

### Evidence contract (frozen rv=1)

Manifest `artifacts/pdca/D225/rv1/manifest.json`, materialized at DO→CHECK; CHECK re-gather budget = 2 targeted re-gathers, owner CHECK.

- **E01/C1–C4:** independent check audit of every footprint file + the 26-hunk disposition vs C1–C4, T213 baseline/proposed and cited implementation/tests (Roslyn-first) → `$A/semantic-review.md`, `$A/disposition.md`.
- **E02/C5:** `git diff --check`; `dotnet docfx docs/docfx.json`; structural check (raw CRLF, EN/RU parity, consecutive guide numbering, links/TOCs, no public→specs links) → `$A/diff-check.log`, `$A/docfx.log`, `$A/structural-review.md`.
- **E03/C6:** `dotnet build -c Debug` → exit 0, 0 warnings, 0 errors → `$A/build.log`.
- **E04/C7:** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect ... ; dotnet reportgenerator ...` requiring line≥85/branch≥75 and PostgreSQL/SQLServer/MySQL/ClickHouse executed (not skipped) → `$A/coverage.xml`, `$A/coverage-report/**`, `$A/coverage-tests.log`.
- **E05/C8:** `git diff --binary <B> -- <footprint> > $A/AC07-rebased.patch`; temp-index `git read-tree <B>` + `git apply --cached --check` → all exit 0, conflict-free, reorg preserved → `$A/AC07-rebased.patch`, `$A/apply-check.log`, `$A/footprint-review.md`.
- **Decisive oracle:** E01 proves the original AC07 meaning in the landed EN/RU docs against the implementation and declared snapshots; E05 proves the new patch's portability onto committed B; E02–E04 close the remaining original obligations. All are reproducible; success of the OLD patch is not required and does not substitute.

### Prerequisites / assumptions

T213 delivered behavior + its artifacts are sufficient inputs; #223 is NOT a dependency; milestone unchanged (1.0.9); SDK/tools/Podman availability is an in-cycle verifiable precondition, not a promised future result.

---

## DO r=2 — DO→PLAN candidate (blocked)

- Phase state: `PLAN` → `DO` → **DO→PLAN candidate** (DO not completed; no footprint edit made).
- D2 remains **blocked/pending** — not `done`, not `superseded`. The frozen plan r=2 cannot be executed
  coherently at HEAD.
- Cause: commit `d76c5ae363e4e92b3bb77f57fbe7a878113a4d97` (#223 Composite-junction M:N SQL link carrier,
  T223, 2026-10-13 14:49 +0500) is an ancestor of HEAD and landed after plan r=2 froze (B `aea0a9ba`,
  2026-10-13 08:37 +0500). It implemented the composite-junction M:N SQL link carrier and rewrote 7 of the
  16 footprint docs to the **support** wording. Criterion **C3** (document the SQL-generation rejection with
  the exact message) is therefore obsolete, and **E01** (“prove the original AC07 meaning … against the
  implementation”) is unsatisfiable.
- Hunk roll-up (`disposition.md`): 14 `already-present` (superseded by T223), 4 `obsolete`, 8
  `conflict-reworked`; **0 carried**.
- Evidence: `artifacts/pdca/D225/rv1/{disposition.md, semantic-review.md, DO_TO_PLAN.md, oracle-rejection.log,
  oracle-remainder.log, scope.json}`.
- DO-phase runs at HEAD `ee3ee9fe`: `dotnet build tests/nextorm.core.tests -c Debug` → 0 Warning(s)/0
  Error(s); the two oracle selectors exit 0 (4/4 and 17/17, 0 skipped).
- Requested: a `planner` revision **r=3** — re-baseline to HEAD, re-scope C3 and E01 to the #213/#223
  support statement, re-derive the 4 obsolete + 8 conflict-reworked hunks. DO must not improvise that scope.
