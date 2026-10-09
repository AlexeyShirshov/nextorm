# Collection status: rc2-reclaim

- collection-id: `rc2-reclaim`
- repository: `/home/alex/sources/nextorm`
- branch: `1.0.9-rc2` @ baseline `cf34f910`
- mode: single lane (current worktree/branch); no worktrees, no group/task branches, no merge
- autocommit: AUTHORIZED (task files only, `git add <paths>`, message prefix `#<issue>`); push never
- evidence contract: `docs/specs/status/collection-1.0.9-rc2-C-evidence.md`
- skills: pdca-collection + pdca-dotnet + nextorm-pdca

## Input task list

| id | issue | title | selected_variant | prior state |
|---|---|---|---|---|
| R184 | #184 | LoadWith: fold `AsSingleQuery` into an `EagerLoadMode` parameter | pdca-dotnet | OPEN, prior terminal r=2, product not in tree |
| R175 | #175 | JoinInto test-quality debt: hash-inequality checks / `EagerLoadSpec.Assign` visibility | pdca-dotnet | OPEN, prior terminal r=1, product reverted |
| R177 | #177 | JSON streaming Phase 3: DB-side JSON fast-path (`FOR JSON`/`json_agg`/`JSONEachRow`) | pdca-dotnet | OPEN, prior terminal r=2/rv=2, product in tree (`2ac20818`), missing native-path tests |
| R190 | #190 | Fix whole-entity selection from JOIN projections | pdca-dotnet | CLOSED 2026-10-08, product in tree (re-verification task) |

## Groups

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| G1-query-loadwith-json-reclaim | R184, R175, R177, R190 | R184 -> R175 -> R177 -> R190 | (single lane: current worktree) | `1.0.9-rc2` | in-progress |

DAG edges: R184->R190; R175->R190; R177->R190 (freshness / final-tip verification). No material edge among R184, R175, R177.

## Tasks

| id | group | branch | status | plan_state | selected_variant | cycle_id | status_file | plan_revision | baseline ref | refs |
|---|---|---|---|---|---|---|---|---|---|---|
| R184 | G1 | `1.0.9-rc2` | done | ready | pdca-dotnet | 1 | `docs/specs/status/rc2-184-eagerloadmode-reclaim-1.md` | r=1 (rv=2) | cf34f910 | #184; ref patch `artifacts/pdca/rc2-184/D184-blocked.patch` |
| R175 | G1 | `1.0.9-rc2` | done | ready | pdca-dotnet | 1 | `docs/specs/status/rc2-175-joininto-quality-reclaim-1.md` | r=1 (rv=2) | cf34f910 | #175; `docs/specs/status/rc2-175-evidence/` |
| R177 | G1 | `1.0.9-rc2` | pending | ready | pdca-dotnet | 1 | `docs/specs/status/rc2-177-native-json-reclaim-1.md` | r=1 (rv=1) | cf34f910 | #177; product `2ac20818` |
| R190 | G1 | `1.0.9-rc2` | pending | ready | pdca-dotnet | 1 | `docs/specs/status/rc2-190-reverify-1.md` | r=1 (rv=1) | cf34f910 | #190 (closed); follow-up #207 |

## Collection phase decisions

- P.PLAN: DONE — all four own PLANs persisted (`plan_state=ready`) at the PLAN->DO boundary.
- ALL-barrier: PASSED (all four plans ready and persisted before DAG/clustering).
- Clustering: DONE — single group `G1-query-loadwith-json-reclaim`; order `R184 -> R175 -> R177 -> R190`.
- Integration: none (single lane; no merge).

## Schedule binding / PLAN revision vector

- vector: R184 r=1/rv=2; R175 r=1/rv=2; R177 r=1/rv=1; R190 r=1/rv=1 (all pdca-dotnet; baseline cf34f910)
- group -> ref: `G1-query-loadwith-json-reclaim` -> current branch `1.0.9-rc2`
- schedule: [R184, R175, R177, R190]
- residual risks: R190 certification is tied to the FINAL verified tip (must follow R184/R175/R177); R184 apply-vs-port outcome to be recorded; R175 H01-H03 mapping and R177 prototype/discovery-count evidence preserved; accepted D190 residuals + #207 carried; R190 not a substitute for R177 perf/live-SQL evidence.

## Overall verification and recovery

- last overall CHECK: none
- state: unverified
- next allowed step: dispatch the single D lane for G1; continue each task's saved PLAN from DO.

## Done / Verified / Incomplete

- Done: bootstrap; 4 own PLANs; ALL-barrier; clustering + schedule binding; R184 done (#184); R175 done (#175, commit 0ee60012, CHECK PASS r=1/rv=2/n=2; evidence `docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/`; contract `docs/specs/status/collection-1.0.9-rc2-C-evidence.md`).
- Verified: —
- Incomplete: —
