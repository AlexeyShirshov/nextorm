# D225 — T225 (#225): JoinInto composite-junction M:N docs delta (AC07 handoff, T213)

- Task: T225 / issue [#225](https://github.com/AlexeyShirshov/nextorm/issues/225)
- Collection: `1.0.9-todo`, group **G1** (single group — no worktree, no branch, no merge); branch `1.0.9`
- Variant: `pdca-dotnet`; cycle **N=1**; plan_revision **r=1**; evidence-contract **rv=1 (not frozen)**
- **Plan state: terminal `incomplete`** — PLAN r=1 = gap → escalation decision (B)

## Phase state

`PLAN` → gap → `escalate` → **terminal incomplete**. DO was not started; no product or documentation writes were made.

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

## Done / Verified

None. T225 is terminal `incomplete`; its contribution to the collection ALL-barrier is **not** satisfied.
