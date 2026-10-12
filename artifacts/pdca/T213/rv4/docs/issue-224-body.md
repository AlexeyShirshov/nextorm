# JoinInto: composite-junction M:N docs delta (AC07 handoff, T213)

Follow-up carved out of #213 (JoinInto — composite keys, composite junction selectors, M:N under
single-query). **AC07 is carried to this issue**, with its **original documentation criteria
unchanged**. T213 narrowed its accepted scope to **scalar M:N** (standalone and through
`SingleQuery`) plus **composite relationship keys**; the composite-junction M:N link carrier is
carried by #223. T213 requires a **verified issue and patch-artifact handoff; it does not claim
those documentation criteria satisfied.** Milestone **1.0.9**.

## Why it is carried (not dropped)

The AC07 documentation footprint is **externally owned**: at the T213 boundary the EN/RU article
tree and the benchmark artifacts carry the owner's uncommitted churn. Per the collection directive
for `1.0.9-todo`, **the EN/RU docs + benchmark artifacts are external and must not be touched or
committed by the cycle** (auto-commit is limited to task/collection status files and task paths,
explicit paths only, never `git add -A`). T213 therefore produced the documentation change as a
**ready-to-apply patch artifact** instead of editing the working tree.

## Trigger — when to apply

Apply the pre-staged patch **only after the external owner commits or discards the churn**, so that
`git status --porcelain` is **clean on all 15 footprint files** listed below. Until then the patch
must not be applied.

Pre-staged artifacts (T213 handoff):

- `artifacts/pdca/T213/rv4/docs/AC07.patch` — unified diff, baseline (`HEAD`) → proposed.
- `artifacts/pdca/T213/rv4/docs/baseline/<path>` — `git show HEAD:<path>` snapshots.
- `artifacts/pdca/T213/rv4/docs/proposed/<path>` — the narrowed-capability text.
- `artifacts/pdca/T213/rv4/docs/manifest.json` — per-file `sha256` (baseline / proposed / current
  working tree) and the footprint `git status --porcelain`.
- `artifacts/pdca/T213/rv4/docs/handoff-check.log` — `git apply --cached --check` verification.

Verified against `HEAD` = `20a67a1c` with a temporary index:
`GIT_INDEX_FILE=/tmp/t213-idx git read-tree 20a67a1c && GIT_INDEX_FILE=/tmp/t213-idx git apply --cached --check artifacts/pdca/T213/rv4/docs/AC07.patch` → **exit 0**.
Apply with `git apply artifacts/pdca/T213/rv4/docs/AC07.patch`.

## Required statement (original AC07 criteria, unchanged)

- **scalar M:N** `JoinInto` works **standalone and under `EagerLoadMode.SingleQuery`**;
- **composite relationship keys** are supported (1:N / 1:1, and the scalar M:N junction);
- **composite-junction M:N** is **rejected in SQL generation** with
  `NotSupportedException("composite-junction M:N is not supported; see #223")` → tracked by **#223**;
- the **in-memory** provider still stitches a **composite junction**.

## Footprint (15 externally-dirty + 1 clean, all to be edited)

Public EN:

- `docs/advanced/relationships.md`
- `docs/advanced/limitations.md`
- `docs/advanced/eager-loading.md`
- `docs/guide/02-joins.md`
- `docs/guide/29-implicit-navigation.md`

Public RU (exact counterparts):

- `docs/ru/advanced/relationships.md`
- `docs/ru/advanced/limitations.md`
- `docs/ru/advanced/eager-loading.md`
- `docs/ru/guide/02-joins.md`
- `docs/ru/guide/29-implicit-navigation.md`

Internal specs:

- `docs/specs/roadmap/todo_eager_loading_pipeline.md`
- `docs/specs/comparison/capability-matrix.md`
- `docs/specs/comparison/linq2db-comparison.md`
- `docs/specs/comparison/linq2db-backlog-gap-analysis.md`
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md`

Clean (no external churn; proposed edit still included):

- `docs/specs/roadmap/todo_navigation_properties.md`

## Authorization citation

Collection `1.0.9-todo` directive (`docs/specs/status/collection-1.0.9-todo.md`), mode and
working-tree note: auto-commit is authorized for **task/collection status files and task paths
only, explicit paths, never `-A`**, and the branch carries external uncommitted entries
(benchmark artifacts + ~20 EN/RU docs) that **the collection does not touch or commit**. The
T213 r=5 planner handoff restates this: the EN/RU docs and benchmark artifacts are external and
must not be touched/committed.

## References

- Parent task: #213.
- Composite-junction carrier: #223 (milestone 1.0.9).
- Deciding evidence: `artifacts/pdca/T213/rv3/nested-projection/RESULT.md`.
