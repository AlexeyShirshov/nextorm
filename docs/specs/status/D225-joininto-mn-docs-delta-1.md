# D225 — T225 (#225): JoinInto composite-junction M:N docs delta (AC07 handoff, T213)

- Task: T225 / issue [#225](https://github.com/AlexeyShirshov/nextorm/issues/225)
- Collection: `1.0.9-todo`, group **G1** (single group — no worktree, no branch, no merge); branch `1.0.9-rc3`
- Variant: `pdca-dotnet`; cycle **N=1**; plan_revision **r=3**; evidence-contract **rv=2 (frozen by PLAN r=3)**
- **Plan state: `ready`** (re-open of T225) — history: PLAN r=1 = gap → escalation decision (B) → terminal `incomplete` (record preserved below); r=2 → DO→PLAN candidate (#223 d76c5ae3) → PLAN r=3 (record preserved below)

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
  oracle-remainder.log, scope.json, evidence.json, manifest.json}`. Validators
  (`scripts/validate_inner_loop.py brief|report|manifest`) all exit 0; the manifest carries E01–E05 as `open`
  with reasons (the DO→CHECK boundary was not reached), which forbids PASS.
- DO-phase runs at HEAD `ee3ee9fe`: `dotnet build tests/nextorm.core.tests -c Debug` → 0 Warning(s)/0
  Error(s); the two oracle selectors exit 0 (4/4 and 17/17, 0 skipped).
- Requested: a `planner` revision **r=3** — re-baseline to HEAD, re-scope C3 and E01 to the #213/#223
  support statement, re-derive the 4 obsolete + 8 conflict-reworked hunks. DO must not improvise that scope.

---

## Plan r=3 (planner, medium) — READY (re-open; supersedes r=2)

Cycle **N=1**; plan revision **r=3**; attempt **n=1/3**; evidence contract **rv=2 (frozen by PLAN r=3)**. r=3 takes the verified r=2 DO→PLAN candidate as mandatory input and replaces the obsolete C3/E01 semantics with a HEAD-based documentation correction.

- **Administrative correction:** persisted branch/milestone labels corrected to **`1.0.9-rc3`** (collection `1.0.9-todo` renamed them; the r=1/r=2 records below keep their historical `1.0.9` text).
- **Supersession:** rv2 explicitly supersedes rv1; E01–E05 IDs and all surviving obligations are retained; E01's false-rejection oracle and E05's obsolete-baseline replay are superseded; E06/E07 are added for malformed-key and implicit-scope variants. rv1 artifacts under `artifacts/pdca/D225/rv1/**` are preserved unchanged.
- **Baseline:** `$B = 0ed8e01dae44122276056f9f89676d913891ff09`, captured and verified at DO start (`git rev-parse HEAD`), with all 16 footprint files verified clean before any edit or commit. `$A = artifacts/pdca/D225/rv2`.
- **DO→PLAN candidate that r=3 resolves:** #223 (`d76c5ae3`) delivered composite-junction M:N SQL support; the literal `composite-junction M:N is not supported` is absent from `src/**`; the designated oracle now asserts support.

### P: Reconcile AC07 with delivered #223 support, replacing obsolete rejection and patch-replay work with a HEAD-based documentation correction.
**Goal:** Accurate EN/RU `JoinInto` documentation, preserving the externally owned reorganisation and all still-required acceptance obligations.
**Minimal solution:** correct five stale authored files; documentation-only, fixed footprint; edit current content, not replay T213's superseded patch.
| Alternative | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Replay T213 against `aea0a9ba` | Preserves historical patch | Reintroduces false rejection; conflicts with #223 | Reject |
| Rewrite all 16 documents | Broad consistency | Unnecessary churn and reorg risk | Reject |
| Correct residual claims against `0ed8e01d` | Small, truthful delta | Requires explicit/implicit-path distinction | Choose |

### Acceptance criteria — stable requirement IDs
| Requirement / criterion | Acceptance and negative case |
|---|---|
| AC01 / C1 | Scalar M:N `JoinInto`, standalone and SingleQuery, described correctly; fail if either is called unsupported. |
| AC02 / C2 | Composite 1:N/1:1 relationship keys and scalar M:N junction described correctly; fail on a blanket single-column restriction covering explicit `JoinInto`. |
| AC03 / C3, revised | Composite-junction M:N SQL support described; malformed metadata still rejected by current validations; fail on the obsolete #223 rejection or an unconditional "all composite keys accepted" claim. |
| AC04 / C4 | In-memory composite-junction stitching retained in the documentation model; fail if SQL support is presented as removing it. |
| AC05 / C5 | EN/RU equivalent; changed authored files CRLF; links, numbering and TOCs consistent; no public→specs links; fail on any mismatch or structural violation. |
| AC06 / C6 | One Debug solution build: exit 0, zero warnings/errors; fail otherwise. |
| AC06 / C7 | Filtered coverage ≥85% line and ≥75% branch; required database providers actually run; fail below either threshold or on provider skips. These remain acceptance gates on this non-main branch. |
| AC05 / C8 | Baseline-relative documentation patch applies through a temporary index, preserves reorg and stays within scope; fail on conflict, path movement or unrelated change. |

### Gaps, assumptions and task continuity
- Implicit-navigation composite-key support is **not established**. Gather its actual path first; absent proof, publish only explicit-`JoinInto` support, not an implicit capability or restriction.
- `$S` = the existing T225-owned status file, retaining its current path.
- `$A=artifacts/pdca/D225/rv2`; `$B=0ed8e01d`; `$F` = exactly the 16 documentation paths below.
- Actual scope replacement: unfinished r=2 D1→D5, D2→D6, D3→D7, D4→D8; replacements remain active and inherit all surviving criteria and unfinished work. No blocker report or candidate marks work done.
- Correct persisted branch/milestone labels to `1.0.9-rc3`; do not edit collection status or GitHub administration.

### Explicit footprint and per-file documentation plan
| Authored files | Disposition |
|---|---|
| `docs/advanced/relationships.md`; `docs/ru/advanced/relationships.md` | Audit; retain accurate composite support statements. |
| `docs/advanced/limitations.md`; `docs/ru/advanced/limitations.md` | Audit; retain accurate content. |
| `docs/advanced/eager-loading.md`; `docs/ru/advanced/eager-loading.md` | Audit; no invented composite-junction section. |
| `docs/guide/02-joins.md`; `docs/ru/guide/02-joins.md` | Audit; no invented composite-junction section. |
| `docs/guide/29-implicit-navigation.md:257`; `docs/ru/guide/29-implicit-navigation.md:263` | Re-scope the blanket key limitation using the evidence rule above. |
| `docs/specs/comparison/capability-matrix.md:110` | Remove obsolete composite/scalar-M:N SingleQuery gaps; retain independently valid limitations. |
| `docs/specs/comparison/linq2db-comparison.md:109` | Same correction against delivered behaviour. |
| `docs/specs/comparison/linq2db-backlog-gap-analysis.md:54,346,405,505` | Correct every stale residual-gap statement consistently. |
| `docs/specs/roadmap/sql-capabilities-gap-analysis.md:30,730` | Audit; retain T223 corrections. |
| `docs/specs/roadmap/todo_eager_loading_pipeline.md:110-111` | Audit; retain accurate content. |
| `docs/specs/roadmap/todo_navigation_properties.md:31` | Audit; retain accurate content. |

Only these 16 files, `$A/**` and `$S` are authored-change scope. Generated DocFX/coverage output is not committed; T213 artifacts remain immutable. No moves, renumbering, TOC-path changes, product/API/tests, unrelated churn or other task statuses.

### Ordered DO tasks
- **D5 — fix now:** At first DO entry, capture `git rev-parse HEAD`, require `$B`, and verify the 16 files clean before task edits/commits. Persist this complete plan, supersession, contract, scope and administrative correction in `$S` before implementation. Baseline mismatch is a prerequisite failure, never silent rebasing.
- **D6 — fix now:** Load `running-integration-tests`; prepare Podman, starting its machine if necessary. Carry forward the verified 26-hunk disposition as provenance, not a patch to replay. Gather Roslyn-first explicit/implicit paths; correct the three comparison specs and both guide bullets under the claim guard.
- **D7 — fix now:** Independently gather EN/RU semantic comparisons against current implementation and existing tests; record all C1–C4 mappings, malformed-key negatives and implicit-scope decisions. Resolve factual mismatches within scope; no new tests or unverified assertions.
- **D8 — fix now:** After edits stop, perform the **single boundary sweep**: structural/DocFX checks, solution build, existing oracles, coverage, container integration, portability and manifest validation. Freeze evidence at DO→CHECK; CHECK determines acceptance.
- No residual correction is deferred. Collection-authorized autocommit may include explicit task paths only, prefix `#225`; never `-A`, push, branch or merge.

### Execution, variants and priorities
**Mode:** Sequential, one current tree; overlapping documentation and one evidence contract make parallel editing/worktrees unnecessary.
| Variant | Closure |
|---|---|
| Scalar M:N: standalone / SingleQuery / AsSingleQuery spelling | Test existing suite plus documentation audit, E01. |
| Composite 1:N / 1:1 / scalar-junction M:N | Test existing suite plus implementation audit, E01. |
| Composite junction: multi-component parent / child / both | Test existing composite oracles plus routing audit, E01. |
| In-memory versus SQL materialisation | Test existing suite and provider/source mapping, E01/E04. |
| Incomplete FK coverage / contradictory component order | Test existing negative oracles, E06. |
| Missing parent/child components or junction metadata | Guard current validation sites; do not imply valid-junction support accepts malformed metadata, E06. |
| Explicit `JoinInto` versus implicit navigation | Guard evidence-scoped claims, E07. |
| Null/default and value/reference components | Guard: introduce no new guarantees; audit claims against existing semantics, E01/E07. |
| SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse | Test existing integration suite, E04; other-provider-specific guarantees guarded out of this delta. |
**Test strategy:** No new tests; existing core oracles, solution coverage and real-provider integration. CHECK rebuild policy **none**; all required execution belongs to D8.
**Priority matrix:** E01–E07 are P1 by acceptance invariants; CHECK cannot downgrade them. Cached-path acceptance is inapplicable to this unrelated documentation-only cycle.
**Performance:** No benchmark: edits at comparison `:110/:109` and guide `:257/:263` affect authored prose, not per-row execution.
**Reconnaissance:** No design spike; #223 implementation and passing oracles establish the solution. D6's bounded path audit establishes documentation scope, not a new design.
**Risks:** Unsupported implicit-path inference, provider skips, baseline drift and broad staging; claim guards, frozen baseline, provider logs and explicit-path commits control them.

### Versioned evidence contract — rv=2
All sources/artifacts below are **planned**, not claimed produced. Every row is unconditional, predicate **true: T225 acceptance**, owner **DO/coder** with independent CHECK review, revision **2**.
`SEM` invocation: `tools.roslyn({action:"structure"})`, `tools.roslyn({action:"types"})`, then `members`/`refs`/`callers` with `target:T`, where each `T` is a discovered fully qualified symbol; record exact resolved calls and successful responses, never invent symbols.
D5 creates planned artifact-local `check_docs.py`, `check_patch.py`, `check_coverage.py`: structural invariants, baseline/temp-index portability, and Summary.txt thresholds respectively; their invocations are fixed below.
| Row / requirement | Required scenario, exact invocation and result | Expected evidence/artifacts under `$A` |
|---|---|---|
| E01 / AC01–AC04 | `SEM` audit against current definitions/tests; `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~JoinInto"`: exit 0, nonzero executed, no failures/skips; all C1–C4 source/claim mappings accurate. | Semantic report `semantic-review.md`, Roslyn receipts `roslyn.json`, `logs/joininto.log`; source anchors include `EntityBuilder.cs:736,757,815,838,883`, factory `:202-206`, existing positive tests. |
| E02 / AC05 | `git diff --check "$B" -- "${F[@]}" "$S" "$A"`; `dotnet docfx docs/docfx.json`; `python3 "$A/check_docs.py" --scope "$A/scope.json" --baseline "$B" --output "$A/structural.json"`: all exit 0; zero structural/broken-link failures and no DocFX errors. | `logs/diff-check.log`, `logs/docfx.log`, `structural.json`; CRLF, EN/RU, links/TOCs/numbering, public→specs prohibition and generated-path exclusion. |
| E03 / AC06-C6 | `dotnet build -c Debug`: exit 0, zero warnings/errors, once at boundary. | `logs/build.log`, build result in `evidence.json`. |
| E04 / AC06-C7 | Exact coverage and integration commands below: all exit 0; coverage ≥85/75; SQLite plus all four container-backed providers executed, none skipped. | `logs/coverage.log`, copied `coverage.cobertura.xml`, `Summary.txt`, `logs/integration.log`, provider execution ledger `providers.json`. |
| E05 / AC05-C8 | `python3 "$A/check_patch.py" --baseline "$B" --scope "$A/scope.json" --patch "$A/docs.patch" --report "$A/portability.json"`: exit 0; generates baseline-relative docs patch, temporary-index `read-tree`/`apply --cached --check` succeeds; no scope/reorg violation. | `scope.json`, `baseline.json`, `docs.patch`, `portability.json`, exact git-command receipts. |
| E06 / AC03-negative | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~JoinIntoCompositeJunctionRejectionTests\|FullyQualifiedName~JoinIntoCompositeRemainderTests"`: exit 0, both selectors executed, no failures/skips; `SEM` confirms malformed-key guards. | `logs/composite-oracles.log`, `malformed-review.md`; rejection tests `:226,:308`, spec `:371-384`, composite factory `:83`, metadata guards `:91,:198-199`. |
| E07 / AC05-scope | `SEM` resolves explicit and implicit entry/call paths; successful receipts plus independent claim audit establish every guide assertion, or verify implicit capabilities remain unasserted. | `implicit-scope.md`, `roslyn.json`, EN/RU claim ledger; no unresolved capability presented as fact. |

Coverage: `mkdir -p tests/coverage && dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`; then `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`; then `python3 "$A/check_coverage.py" tests/coverage/report/Summary.txt --line 85 --branch 75` (all exit 0).
Integration: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`; missing socket prerequisite: `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start`, wait/recheck, then execute.
Manifest: **`artifacts/pdca/D225/rv2/manifest.json`**; required keys `task`, `tree`, `contract_rv`, `status_file`, `required_rows`, `rows[]`; each row has stable `id`, `status`, and nonempty `{kind,path,command?,exit?,value?}` evidence when met, reason when open, predicate when na.
Freeze baseline independently of subsequent commits; bind manifest to exact evaluated tree revision and rv2 at DO→CHECK. CHECK evidence is append-only and cannot silently rebind tree, baseline or obligations. Validate: `python3 scripts/validate_inner_loop.py manifest "$A/manifest.json"` → 0 required; 2 violation, 1 error. No PASS with any required row open.
CHECK re-gather budget: **2 targeted gathers total, owner CHECK**, existing-tree evidence only; exhaustion leaves acceptance unproven. Missing reports alone justify neither contract revision nor another DO.
**Supersession:** rv2 explicitly supersedes rv1; retain E01–E05 IDs and surviving obligations, supersede E01's false rejection oracle and E05's obsolete-baseline replay, add E06/E07 for malformed-key and implicit-scope variants. Preserve rv1 artifacts unchanged.

**Decisions:** C3 — describe delivered composite-junction M:N SQL support; separately document genuine malformed-key rejection, never the removed #223 message. Guide — re-scope EN:257/RU:263; first gather Roslyn explicit-versus-implicit paths, then assert only verified behaviour — no implicit blanket claim without proof. Comparison — correct all identified stale occurrences in the three comparison specs against delivered support, retaining independently valid gaps. Footprint — unchanged at 16 documentation files plus `$A/**` and `$S`. Baseline — `0ed8e01d`; capture and verify HEAD and clean footprint at DO start before any task edit/commit, then freeze. Contract — rv1→rv2 supersedes obsolete E01/E05 semantics, retains IDs and all surviving obligations, adds E06/E07.

### Scope (validated before edits)
`$A/scope.json` — projects `tests/nextorm.core.tests`; selectors `FullyQualifiedName~JoinInto`, `FullyQualifiedName~JoinIntoCompositeJunctionRejectionTests`, `FullyQualifiedName~JoinIntoCompositeRemainderTests`; the 16 footprint files; `rebuild: none`; boundary = D8 single sweep. Validated: `python3 scripts/validate_inner_loop.py brief artifacts/pdca/D225/rv2/scope.json` → exit 0.

### DO r=3 progress log (appended)

- 2026-10-14 23:22 UTC | PLAN | revision r=3 | iteration 1/3 | Replanned: planner issued r=3 (C3 superseded by T223 d76c5ae3 -> rv=2); r=2 DO→PLAN candidate verified; contract rv=2 frozen; administrative labels corrected to 1.0.9-rc3 | docs/specs/status/D225-joininto-mn-docs-delta-1.md
- 2026-10-14 23:23 UTC | DO | revision r=3 | iteration 1/3 | DO started; HEAD `0ed8e01d` == required `$B`; 16 footprint files clean (`git diff --stat $B -- <footprint>` empty); scope validated | artifacts/pdca/D225/rv2/{baseline.json,scope.json} (validate_inner_loop.py brief exit 0)
- Notice: host tool catalog exposes no todowrite; sidebar todo mirror not updated this cycle; status file is the sole progress source.
- Notice: branch/milestone labels corrected to 1.0.9-rc3 (administrative drift).

### DO r=3 — closed units (D5–D8)

- 2026-10-14 23:25 UTC | D5 | revision r=3 | iteration 1/3 | **D5 closed**: `git rev-parse HEAD` = `0ed8e01dae44122276056f9f89676d913891ff09` == required `$B` (branch `1.0.9-rc3`); all 16 footprint files clean (no `M`, empty `git diff --stat $B -- <footprint>`); plan r=3 + supersession + contract rv=2 + scope + administrative correction persisted here; `python3 scripts/validate_inner_loop.py brief artifacts/pdca/D225/rv2/scope.json` → exit 0. | artifacts/pdca/D225/rv2/{baseline.json,scope.json,check_docs.py,check_patch.py,check_coverage.py}
- 2026-10-14 23:29 UTC | D6 | revision r=3 | iteration 1/3 | **D6 closed**: `running-integration-tests` skill loaded; Podman socket present and `curl --unix-socket ... _ping` → `OK` (machine already running, no start needed); the verified 26-hunk disposition carried as provenance only (no patch replay); Roslyn-first explicit/implicit entry-and-call paths gathered; the three comparison specs and both guide bullets corrected under the claim guard — 5 authored files changed, 11 audited unchanged. | artifacts/pdca/D225/rv2/{roslyn.json,semantic-review.md,implicit-scope.md}
- 2026-10-14 23:33 UTC | D7 | revision r=3 | iteration 1/3 | **D7 closed**: EN/RU semantic comparison against current implementation + existing tests recorded, with C1–C4 source/claim mappings, the malformed-key negatives and the implicit-scope decision; factual mismatches resolved inside scope; no new tests and no unverified assertion added. | artifacts/pdca/D225/rv2/{semantic-review.md,malformed-review.md,implicit-scope.md}
- 2026-10-14 23:45 UTC | D8 | revision r=3 | iteration 1/3 | **D8 closed (single boundary sweep; evidence frozen at DO→CHECK)**: `git diff --check` exit 0; `check_docs.py` 16/16, 0 failures; `dotnet docfx` exit 0 with 0 errors (2 pre-existing source-generator duplicate-source warnings, unrelated to the delta); `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; `FullyQualifiedName~JoinInto` 159 selected / 159 passed / 0 skipped; composite oracles 4/4 + 17/17, 0 skipped; coverage `line 87.30% (>=85)`, `branch 79.90% (>=75)`; integration `Total 3904, Errors 0, Failed 0, Skipped 198` with all 198 skips provider-capability (no provider-unavailable skip) and 5 provider containers actually started (PostgreSQL `ff3bb6dd042f`, SQL Server `1b5207b30a62`, MySQL `d703aedb9b21`, MariaDB `914f06a0ba5f`, ClickHouse `6bf39546e2b9`; ClickHouse confirmed by an isolated `-class …ClickHouseIntegrationTests` run: 148/148, 0 skipped); baseline-relative temp-index `read-tree` + `apply --cached --check` exit 0 (16 files, 8 hunks, 0 out-of-scope paths). Validators: `validate_inner_loop.py brief` 0, `report` 0, `manifest` 0; no manifest row is `open`. | artifacts/pdca/D225/rv2/{manifest.json,evidence.json,portability.json,providers.json,structural.json,Summary.txt,coverage.cobertura.xml,docs.patch,logs/**}
- Notice (r=3, out of scope, reported not edited): the product XML doc at `src/nextorm.core/Builders/EntityBuilder.cs:730-732` still states that a "composite-junction many-to-many (carried by issue #223) is rejected with `NotSupportedException`", which contradicts the delivered behaviour asserted by `JoinIntoCompositeJunctionRejectionTests.cs:113`. It is product source, outside this documentation-only footprint, and was left untouched.
- Notice (r=3): the E06 selector pair is recorded and executed as two separate in-scope filters (`…~JoinIntoCompositeJunctionRejectionTests` → 4/4; `…~JoinIntoCompositeRemainderTests` → 17/17); a `|`-joined filter is a compound command token and additionally fails the report validator's scope-conformance rule, so the split form is the evidence of record (the combined-equivalent run `logs/composite-oracles.log` also exits 0 with 21/21).

### DO r=3 — artifact index (`artifacts/pdca/D225/rv2`)

`scope.json`, `baseline.json`, `check_docs.py`, `check_patch.py`, `check_coverage.py`,
`docs.patch`, `portability.json`, `structural.json`, `providers.json`, `coverage.cobertura.xml`,
`Summary.txt`, `manifest.json`, `evidence.json`, `semantic-review.md`, `malformed-review.md`,
`implicit-scope.md`, `roslyn.json`, `logs/{diff-check,docfx,build,joininto,coverage,coverage-threshold,integration,clickhouse,composite-oracles,composite-rejection,composite-remainder,patch,discovery}.log`.

- 2026-10-14 23:47 UTC | D8 | revision r=3 | iteration 1/3 | **DO→CHECK handoff**: the swept working tree was committed as `e9bea5f7cd29e34ee58428a846dd950c55dd2106` (parent `0ed8e01dae44122276056f9f89676d913891ff09`, the frozen baseline `$B`). The manifest binds `tree: 0ed8e01d` + `contract_rv: 2`, and every execution records `source_revision == artifact_revision == 0ed8e01d`; no provider/API/test or out-of-footprint file was staged (36 paths: 5 footprint docs + this status file + `artifacts/pdca/D225/rv2/**`). CHECK evidence is append-only and must not silently rebind the tree, baseline or obligations.
