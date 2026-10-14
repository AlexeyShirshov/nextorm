# D225 / T225 — §3 D2 disposition map (26 original AC07 hunks vs HEAD)

Baseline B = `aea0a9ba644a8be34f5e9bf39e4daf4a33861f17` (verified: `git cat-file -e` ok, ancestor of HEAD).
DO start HEAD = `ee3ee9fe96b81fa8b8bbcac124aee550ec98ac3b` (branch `1.0.9-rc3`).
Footprint pre-DO state: `git status --porcelain -- <16 paths>` → **empty** (clean).
Source: `artifacts/pdca/T213/rv4/docs/AC07.patch` (16 files / 26 hunks / +42 −35) and the
`baseline/`→`proposed/` snapshots under `artifacts/pdca/T213/rv4/docs/docs/`.

`AC07.patch` does **not** apply as-is (context drift after #224/#226 and the external reorg, e.g. the
patch expects `../providers/postgres.md#server-version`, HEAD has
`../connecting-providers/postgres.md#server-version`). It was **not** force-applied.

## Decisive intervening change

`d76c5ae3  #223 Composite-junction M:N SQL link carrier (T223, r=1 rv=1)` (2026-10-13 14:49 +0500)
landed **after** PLAN r=2 froze (B = 2026-10-13 08:37 +0500) and is an ancestor of HEAD. It (a)
implemented the composite-junction M:N SQL link carrier and (b) rewrote 7 of the 16 footprint docs to
the **support** statement. Consequences:

- `src/` no longer contains the AC07/C3 message: `git grep -n "composite-junction M:N is not supported" -- 'src/**'` → **no match**.
- The T213 rejection oracle was re-pointed by T223 to assert **support**:
  `tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:113`
  `act.Should().NotThrow("the composite-junction M:N link source is supported again (#223)");` → run exit 0, 4/4.
- `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs:205-206` routes a multi-component junction FK to
  `BuildCompositeSource` instead of throwing.

Therefore a hunk whose added text **asserts the #223 rejection** is `obsolete`. A hunk whose intent was
already landed by T223 (in a more accurate, support-oriented wording) is `already-present`. A hunk that
must *remove* pre-T213 stale text but whose *replacement* text is the obsolete rejection wording is
`conflict-reworked` (removal still required, replacement content belongs to a new plan revision).

## Disposition per hunk

| # | File | What the hunk does | Current-file state (HEAD) | Disposition | Evidence |
|---|---|---|---|---|---|
| H01 | docs/advanced/eager-loading.md | +adds scalar-M:N supported / composite-junction M:N **rejected in SQL** | sentence absent (`grep 'supports a **scalar** many-to-many'` → 0) | **obsolete** (rejection clause false; support clause pending replan) | `sed -n '229,233p'`; `JunctionLinkSourceFactory.cs:205-206` |
| H02 | docs/advanced/limitations.md:19 | rewrites the `JoinInto` limitation clause; asserts rejection | T223 already rewrote it to the **support** statement ("junction whose key spans several components … single-query dispatcher handles a many-to-many junction too") | **already-present** (superseded by T223) | `d76c5ae3 -- docs/advanced/limitations.md`; current L19 |
| H03 | docs/advanced/relationships.md:194 | +scalar supported / composite junction rejected in SQL | T223 wrote "A junction whose parent and/or child foreign key spans several **components** is fully supported" | **already-present** (superseded by T223) | `d76c5ae3 -- docs/advanced/relationships.md`; current L194 |
| H04 | docs/advanced/relationships.md:292 | replaces the rejection bullets with "#223 in SQL generation" | T223 wrote "Key **arity** is not one of these boundaries … supported on the explicit `JoinInto` path" | **already-present** (superseded by T223) | current L292 |
| H05 | docs/guide/02-joins.md | +loader supports scalar M:N/composite keys; composite-junction M:N rejected | `#223` occurs 0× in the file | **obsolete** (rejection clause false; support clause pending replan) | `grep -c '#223'` → 0 |
| H06 | docs/guide/29-implicit-navigation.md:257 | removes "only single-column … keys are supported", replaces with rejection wording | **stale text still present** (`* **Composite relationship keys** — only single-column foreign/principal/junction keys are supported.`) | **conflict-reworked** (removal required; replacement must be support wording) | `sed -n '257p'` |
| H07 | docs/ru/advanced/eager-loading.md | RU mirror of H01 | absent | **obsolete** | `grep -c 'скалярный'` → 0 |
| H08 | docs/ru/advanced/limitations.md:19 | RU mirror of H02 | T223 rewrote it to the RU support statement | **already-present** (superseded by T223) | `d76c5ae3 -- docs/ru/advanced/limitations.md` |
| H09 | docs/ru/advanced/relationships.md:194 | RU mirror of H03 | T223 RU support statement present | **already-present** (superseded by T223) | `d76c5ae3 -- docs/ru/advanced/relationships.md` |
| H10 | docs/ru/advanced/relationships.md:292 | RU mirror of H04 | T223 RU support statement present | **already-present** (superseded by T223) | idem |
| H11 | docs/ru/guide/02-joins.md | RU mirror of H05 | `#223` 0× | **obsolete** | `grep -c '#223'` → 0 |
| H12 | docs/ru/guide/29-implicit-navigation.md:263 | RU mirror of H06 | **stale text still present** (`только одноколоночные внешние/principal/junction-ключи`) | **conflict-reworked** | `sed -n '263p'` |
| H13 | docs/specs/comparison/capability-matrix.md:110 | rewrites the navigation row to "composite junction selector rejected in SQL (#223)" | stale pre-T213 tail still present: "composite keys, a composite junction selector or a many-to-many `JoinInto` under `AsSingleQuery` are rejected" | **conflict-reworked** (stale removal required; replacement wording obsolete) | `sed -n '110p'` |
| H14 | docs/specs/comparison/linq2db-backlog-gap-analysis.md:54 | removes the eager-load remainder list | stale text present: "остатки: составные junction-селекторы, составные ключи, M:N под `AsSingleQuery`" | **conflict-reworked** | `sed -n '54p'` |
| H15 | docs/specs/comparison/linq2db-backlog-gap-analysis.md:345 | removes the Inheritance/TPH remainder list | stale text present (same remainder list) | **conflict-reworked** | `sed -n '345p'` |
| H16 | docs/specs/comparison/linq2db-backlog-gap-analysis.md:404 | rewrites the Association/eager-load row | stale remainder text present | **conflict-reworked** | `sed -n '404p'` |
| H17 | docs/specs/comparison/linq2db-backlog-gap-analysis.md:504 | rewrites the axis-remainder prose | stale remainder text present | **conflict-reworked** | `sed -n '504p'` |
| H18 | docs/specs/comparison/linq2db-comparison.md:109 | rewrites the navigation row | stale tail present: "what remains is convention-over-FK inference …, composite keys, a composite junction selector and a many-to-many `JoinInto` under `EagerLoadMode.SingleQuery`" | **conflict-reworked** | `sed -n '109p'` |
| H19 | docs/specs/roadmap/sql-capabilities-gap-analysis.md:27 | sets row 13 to "composite-junction SQL link carrier (#223) remains open" | T223 landed "composite relationship/junction keys shipped (#213/#223)" | **already-present** (superseded by T223) | `d76c5ae3 -- …sql-capabilities-gap-analysis.md` |
| H20 | docs/specs/roadmap/sql-capabilities-gap-analysis.md:721 | same, row 13 (second table) | T223 landed the shipped wording | **already-present** (superseded by T223) | idem |
| H21 | docs/specs/roadmap/todo_eager_loading_pipeline.md:105 | §6 remainder resolved through #223 | T223 landed "**решено** (#213 …; #223 …)" | **already-present** (superseded by T223) | `d76c5ae3 -- …todo_eager_loading_pipeline.md` |
| H22 | docs/specs/roadmap/todo_navigation_properties.md:28 | non-goals: composite keys supported (#213) | T223 landed "**реализованы** (#213/#223)" | **already-present** (superseded by T223) | `d76c5ae3 -- …todo_navigation_properties.md` |
| H23 | docs/specs/roadmap/todo_navigation_properties.md:77 | §3.1 M2M remainder | T223 landed the "#213/#223 реализованы" wording | **already-present** (superseded by T223) | idem |
| H24 | docs/specs/roadmap/todo_navigation_properties.md:258 | §6 rejection list | T223 landed "**реализовано** (#213/#223)" | **already-present** (superseded by T223) | idem |
| H25 | docs/specs/roadmap/todo_navigation_properties.md:290 | docs plan note in the spec | T223 landed a superseding phrasing | **already-present** (superseded by T223) | idem |
| H26 | docs/specs/roadmap/todo_navigation_properties.md:320 | §10 open points | T223 landed a superseding phrasing | **already-present** (superseded by T223) | idem |

## Roll-up

- `already-present` (T223 landed a superseding, more accurate wording): H02, H03, H04, H08, H09, H10, H19, H20, H21, H22, H23, H24, H25, H26 → **14 hunks**.
- `obsolete` (adds text asserting the #223 rejection, now false): H01, H05, H07, H11 → **4 hunks**.
- `conflict-reworked` (must remove pre-T213 stale text; replacement content belongs to a new plan revision): H06, H12, H13, H14, H15, H16, H17, H18 → **8 hunks**.
- `carried`: **0** — DO stopped on the DO→PLAN candidate before the carry; no footprint file was edited.

Real remaining docs work exists (the 8 `conflict-reworked` hunks + the 4 `obsolete` support clauses), but it
must be expressed as **#213/#223 support**, which the frozen C3 forbids. Hence the DO→PLAN candidate
(`DO_TO_PLAN.md`).
