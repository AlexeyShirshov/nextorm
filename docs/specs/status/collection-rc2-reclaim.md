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
| G1-query-loadwith-json-reclaim | R184, R175, R177, R190 | R184 -> R175 -> R177 -> R190 | (single lane: current worktree) | `1.0.9-rc2` | done |

DAG edges: R184->R190; R175->R190; R177->R190 (freshness / final-tip verification). No material edge among R184, R175, R177.

## Tasks

| id | group | branch | status | plan_state | selected_variant | cycle_id | status_file | plan_revision | baseline ref | refs |
|---|---|---|---|---|---|---|---|---|---|---|
| R184 | G1 | `1.0.9-rc2` | done | ready | pdca-dotnet | 1 | `docs/specs/status/rc2-184-eagerloadmode-reclaim-1.md` | r=1 (rv=2) | cf34f910 | #184; ref patch `artifacts/pdca/rc2-184/D184-blocked.patch` |
| R175 | G1 | `1.0.9-rc2` | done | ready | pdca-dotnet | 1 | `docs/specs/status/rc2-175-joininto-quality-reclaim-1.md` | r=1 (rv=2) | cf34f910 | #175; `docs/specs/status/rc2-175-evidence/` |
| R177 | G1 | `1.0.9-rc2` | done | ready | pdca-dotnet | 1 | `docs/specs/status/rc2-177-native-json-reclaim-1.md` | r=2 (rv=2) | cf34f910 | #177; product `2ac20818`; evidence `docs/specs/status/rc2-177-reclaim-evidence/` |
| R190 | G1 | `1.0.9-rc2` | done | ACT complete | pdca-dotnet | 1 | `docs/specs/status/rc2-190-reverify-1.md` | r=2 (rv=2) | cf34f910 | #190 (stays closed); follow-up #207 (carried, OPEN); commit `25395140`; evidence `artifacts/pdca/rc2-190-reverify-1/` |

## Collection phase decisions

- P.PLAN: DONE — all four own PLANs persisted (`plan_state=ready`) at the PLAN->DO boundary.
- ALL-barrier: PASSED (all four plans ready and persisted before DAG/clustering).
- Clustering: DONE — single group `G1-query-loadwith-json-reclaim`; order `R184 -> R175 -> R177 -> R190`.
- Integration: none (single lane; no merge).

## Schedule binding / PLAN revision vector

- vector: R184 r=1/rv=2; R175 r=1/rv=2; R177 r=2/rv=2; R190 r=2/rv=2 (all pdca-dotnet; baseline cf34f910)
- group -> ref: `G1-query-loadwith-json-reclaim` -> current branch `1.0.9-rc2`
- schedule: [R184, R175, R177, R190]
- residual risks: R190 certification is tied to the FINAL verified tip (must follow R184/R175/R177); R184 apply-vs-port outcome to be recorded; R175 H01-H03 mapping and R177 prototype/discovery-count evidence preserved; accepted D190 residuals + #207 carried; R190 not a substitute for R177 perf/live-SQL evidence.

## Overall verification and recovery

- last overall CHECK: PASS — collection C-phase on the integrated tree @ 93fb9255
- state: verified
- next allowed step: — (collection rc2-reclaim complete; single lane; no merge; branch 1.0.9-rc2 left for the user)

## Collection C-phase (verification) — PASS

- C verdict: **PASS** (3-row closure + per-row inheritance audit; no product defect).
- Evidence contract: `docs/specs/status/collection-rc2-reclaim-C-evidence.md`.
- AC1 build Debug/Release 0W/0E; AC2 unit Σ6265 passed / 0 failed / 1 skip (6266 total, 11 projects); AC3 integration 3561 / 0 errors / 0 failed / 197 capability-skip, all 6 providers; AC4 coverage line 88.3% / branch 80.3%; AC5 DocFX exit 0 (2 pre-existing warnings); perf 7/7 cases, time ratio 2.017 vs 1.87 (+7.9%, <20%).
- Closures: G1–G4 (verbatim `rv=2` heading + `## Priority matrix` for R184/R175/R177); provider-count 3433 + 68 + 60 = 3561 reconciled; G5 mutation closed-by-disclosure (bounded Stryker 5.0.0 attempt exit 124, 33894 created / 0 tested, coverage capture Dubious, Safe-Mode CS0165/CS8081; no usable score; no kill fabricated).
- Evidence root: `artifacts/pdca/collection-rc2-reclaim/C/` (integration.log, integration-providers.txt, coverage-Summary.txt, perf-acceptance.log, provider-count-reconciliation.md, stryker-attempt.log, stryker-attempt-summary.md).
- Debt: #207 carried OPEN (milestone NOT recorded); #208 separate OPEN (out of the R177 tests-only footprint); #190 stays CLOSED.

## Done / Verified / Incomplete

- Done: bootstrap; 4 own PLANs; ALL-barrier; clustering + schedule binding; R184 done (#184); R175 done (#175, commit 0ee60012, CHECK PASS r=1/rv=2/n=2; evidence `docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/`; contract `docs/specs/status/collection-1.0.9-rc2-C-evidence.md`); R177 done (#177, commit d47b84fa, CHECK PASS r=2/rv=2/n=1; evidence `docs/specs/status/rc2-177-reclaim-evidence/`; closed A1/A9/R7/R15/R16/A2-E177-14; product `2ac20818`; defect #208 separate OPEN); R190 done (#190 stays closed, commit 25395140974ab091d7349fcbea1c31fe40d4f2f6, CHECK PASS r=2/rv=2/n=1; no-op re-verification on F=ee42f183, predecessor `docs/specs/status/rc2-190-join-whole-entity-1.md` preserved unchanged; evidence `artifacts/pdca/rc2-190-reverify-1/`; #207 carried OPEN).
- Verified: C-phase PASS on the integrated tree at 93fb9255 — all four tasks done (R184 #184, R175 #175, R177 #177, R190 #190), no product defect; evidence contract `docs/specs/status/collection-rc2-reclaim-C-evidence.md`; debt #207 carried OPEN, #208 separate OPEN, #190 stays CLOSED.
- Incomplete: — (G1 all four tasks done)
