# Collection 1.0.9-rc2

- collection-id: `1.0.9-rc2`
- input: open issues of GitHub milestone `1.0.9-rc2` (milestone #20): 151, 153, 154, 157, 159, 160, 162, 167, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 184, 185, 188, 189, 190, 191 (26 tasks)
- brief source: `/tmp/opencode/rc2/briefs.md`
- mode: autonomous; auto-commit authorized by explicit user request and the repo AGENTS `pdca-collection` exception; **push never**
- base: branch `1.0.9-rc2` at `18659e41` (merge of PR #205 from `1.0.9-rc1`)
- collection revision: r1; evidence revision: rv1; PLAN-revision vector: all admitted tasks r=1/rv=1
- collection status: **in progress** — done: D153, D154, D157, D159, D191, D162, D174, D173, D190 (CHECK PASS r1/rv1 evidence-completion; mutation closed by authorized no-tool disclosure; #190 closed), D176 (CHECK PASS r2/rv2; #176 closed), D178 (CHECK PASS r=1 n=2 rv=1; #178 closed; commit `31c9e9d9`); incomplete/set-aside: D160 (terminal `incomplete`, preserved on branch `wip/d160-incomplete`), D170 (terminal `incomplete`, patch preserved), D184 (blocked, evidence-contract gap, preserved as patch), D175 (terminal `incomplete`, escalation decision (c) STOP, patch preserved); remaining pending: D180, D177, D179, D185, D167, D169, D189, D188, D151
- User stop: run paused by explicit user request immediately after D162 (2026-10-08); no further task DO is started. Bookkeeping-only stop — no push performed.

## Admission / gaps (P)

- **Admitted (24, `plan_state=ready`):** D151, D153, D154, D157, D159, D160, D162, D167, D169, D170, D173, D174, D175, D176, D177, D178, D179, D180, D184, D185, D188, D189, D190, D191.
- **Excluded-gap (2):**
  - **D171** (#171) — `revisit on demand` placeholder: no consumer, no agreed API/semantics, no derivable acceptance criteria; existing guard already rejects (`EntityBuilder.cs:779-784`). Not done, not superseded; issue stays **OPEN** in milestone `1.0.9-rc2`. Trigger: concrete consumer + agreed API.
  - **D172** (#172) — awaiting-consumer: issue's own precondition unmet; docs `relationships.md:67` record the feature as *not provided*. Not done, not superseded; issue stays **OPEN** in milestone `1.0.9-rc2`. Trigger: consuming scenario + agreed API/semantics.
- Rationale: both are uninventable-scope placeholders; excluding them lets the ALL-barrier be evaluated over the 24 executable tasks without inventing scope. (Decided by `planner`, r=1.)

## Groups

Single connected component by shared core footprint (`EntityBuilder`, `SqlBuilder`, `DataContext`, mapping/JoinInto, JSON cluster, shared docs/tests). Disjoint-footprint groups are impossible. Per `pdca-collection`, a single group works in the **current worktree/branch**: no group/task worktrees, no group branches, no `git merge --no-ff`; authorized commits go to branch `1.0.9-rc2`.

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| G1 | 23 active tasks (D160 set aside) | D153 → D154 → D157 → D159 → [D160 set aside] → D184 → D191 (done) → D162 → D170 → D174 → D175 → D173 → D176 → D178 (done) → D180 → D177 → D179 → D185 → D167 → D169 → D189 → D188 → D151 | current worktree | 1.0.9-rc2 | in-progress |

Intra-group order is a serialization dependency (footprint overlap) plus one functional edge `D189 → D188`; it is not a functional chain for the rest.

## Recovery (D160 set-aside) — user-directed

- **D160 STOP:** after r=4 (the sealed terminal revision) CHECK failed on the same R02 over-preservation family, the task was marked final `incomplete` (no r=5). Its full state is preserved on branch `wip/d160-incomplete` (commit chain through `40b1a159` + STOP record) and as patch `docs/specs/status/rc2-160-evidence/D160-STOP-incomplete.patch`; #160 stays OPEN.
- **Branch recovery (non-destructive):** local branch `1.0.9-rc2` was reset to the clean pre-D160 tip `4bd18c82` (D159 checkpoint) after preserving D160 on `wip/d160-incomplete`. No product code lost.
- **Continuation policy (user-authorized deviation from the strict group-stop rule):** the remaining 19 tasks are executed in the fixed order; if one task finally ends `incomplete`, it is marked `incomplete`, its product/test paths are restored to the last verified-green tip (patch preserved), and the lane **continues** with the next task (a single failure does not stop the rest).
- **PUSH FINDING (rule violation):** `origin/1.0.9-rc2` was created/advanced by a push during the run to `40b1a159` (remote-tracking reflog: `update by push`), although the repo rule is "push never". The remote was NOT touched by this recovery; local `1.0.9-rc2` now diverges from `origin/1.0.9-rc2`. Resolving the remote is the user's manual action (do not push from the cycle).
- **D184 blocked (evidence-contract gap):** escalation decided a real r=2 evidence-tracing replan and CHECK invoked STOP on recurrence; all product gates were green but CHECK could not certify. Product/test/docs paths were reset to the last verified-green tip `4bd18c82` (tree reset), the complete change is preserved uncommitted as patch `artifacts/pdca/rc2-184/D184-blocked.patch` with artifacts `artifacts/pdca/rc2-184/{r1,r2}/`, and #184 stays OPEN. The lane continues with D191 (next pending) in the fixed order.

## Tasks

| task | issue | group | branch | status | plan_state | selected_variant | cycle_id | plan_revision | status file | reason + patch |
|---|---|---|---|---|---|---|---|---|---|---|
| D151 | #151 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-151-stryker-mutation-1.md | Stryker re-run native extreme-row |
| D153 | #153 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-153-public-extensibility-1.md | closed #153 (8401ca51) |
| D154 | #154 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-154-dto-public-ctors-1.md | closed #154 (5d72a79) |
| D157 | #157 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-157-register-finding-25-1.md | closed #157 (5978556f) |
| D159 | #159 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-159-cte-direct-joins-1.md | closed #159 (078a7a2d) |
| D160 | #160 | G1 | 1.0.9-rc2 | incomplete | ready | pdca-dotnet | N=1 | r=4 (terminal) | docs/specs/status/rc2-160-join-alias-mixing-1.md | terminal r=4 STOP (R02 over-preservation of SourceEntityType/BindArrayJoinElement); set aside per explicit user direction; preserved on branch `wip/d160-incomplete` + patch `docs/specs/status/rc2-160-evidence/D160-STOP-incomplete.patch`; #160 stays OPEN |
| D161 | - | - | - | - | - | - | - | - | - | (reference only: #130/#161 hint APIs already shipped) |
| D162 | #162 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=2 | docs/specs/status/rc2-162-navigation-temp-tvp-fail-closed-1.md | closed #162 (ae946293) |
| D167 | #167 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-167-csv-chunked-read-1.md | CSV chunked read for byte[] (P0 validator prerequisite) |
| D169 | #169 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-169-dml-scope-hints-1.md | DML scope hints on join paths |
| D170 | #170 | G1 | 1.0.9-rc2 | incomplete | ready | pdca-dotnet | N=1 | r=3 (terminal) | docs/specs/status/rc2-170-onetoone-fk-uniqueness-1.md | terminal STOP (CHECK r=3 fail, same defect family; R170-02/R170-03 variant closure unmet; patch `docs/specs/status/rc2-170-evidence/D170-STOP-incomplete.patch`); #170 stays OPEN |
| D171 | #171 | - | 1.0.9-rc2 | excluded-gap | gap | pdca-dotnet | N=1 | - | docs/specs/status/rc2-171-joininto-as-derived-1.md | revisit-on-demand placeholder; issue OPEN in milestone |
| D172 | #172 | - | 1.0.9-rc2 | excluded-gap | gap | pdca-dotnet | N=1 | - | docs/specs/status/rc2-172-child-collection-projection-1.md | awaiting-consumer; issue OPEN in milestone |
| D173 | #173 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-173-projection-folding-member-1.md | closed #173 (29e794fd) |
| D174 | #174 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-174-identity-selector-cache-1.md | closed #174 (6bc4ae41) |
| D175 | #175 | G1 | 1.0.9-rc2 | incomplete | ready | pdca-dotnet | N=1 | r=1 (terminal) | docs/specs/status/rc2-175-joininto-test-quality-1.md | terminal STOP (escalation decision (c)); defect family «rv1 evidence-completeness / row-bound ledger provenance», 4× CHECK fail; no product defect demonstrated; patch `docs/specs/status/rc2-175-evidence/D175-STOP-incomplete.patch`; #175 stays OPEN |
| D176 | #176 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=2 | docs/specs/status/rc2-176-json-nested-1.md | JSON streaming Phase 2 implemented; CHECK PASS r=2/rv=2; commit f760e5521f7630e56a2841e6b1e097aaae92273d; evidence docs/specs/status/evidence/rc2-176/ |
| D177 | #177 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-177-native-json-1.md | JSON streaming Phase 3 DB-side fast-path |
| D178 | #178 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-178-json-enum-1.md | JSON streaming enum; CHECK PASS r=1 n=2 rv=1; commit 31c9e9d9d0157706167a49a42546499affcaea07; #178 closed; boundary 9548/9350/0/198; integration enum 30/30 across 6 providers; coverage DataContext 89.1/83.3, JsonShapePlan 93.1/91.3, JsonRowWriterFactory 94.8/82.8; perf acceptance 7/7; mutation 73.51% nonblocking |
| D179 | #179 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-179-json-error-policy-1.md | JSON streaming error policy |
| D180 | #180 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-180-json-stream-provider-conversion-1.md | JSON provider column conversion |
| D184 | #184 | G1 | 1.0.9-rc2 | incomplete | ready | pdca-dotnet | N=1 | r=2 | docs/specs/status/rc2-184-loadwith-eagerloadmode-1.md | blocked: evidence-contract gap — CHECK could not certify despite all product gates green (build Debug/Release 0/0, core 1772/0/0, sqlite 1161/1skip, eager integration 52/0/0 four providers, full container 3337/0 failed, coverage 85.9/76.5, code audit 0 Critical/0 product Warnings); escalation decided real r=2 evidence-tracing replan; CHECK invoked STOP on recurrence; patch `artifacts/pdca/rc2-184/D184-blocked.patch`; artifacts `artifacts/pdca/rc2-184/{r1,r2}/` |
| D185 | #185 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-185-bindentity-registration-1.md | BindEntity registration |
| D188 | #188 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-188-comparison-benchmarks-tier1-1.md | Comparison benchmarks tier 1 (needs D189) |
| D189 | #189 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-189-sqlite-datareader-1.md | SQLite ToDataReader verification |
| D190 | #190 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-190-join-whole-entity-1.md | done — CHECK PASS r1/rv1 evidence-completion; mutation closed by authorized disclosure; commit 1ff47762; #190 closed |
| D191 | #191 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-191-provider-extensions-1.md | done — provider-specific fluent API relocated out of core into provider packages; CHECK PASS snapshot-n2 `7e9fad9`/diff `f652c19e`; commit `e25f558e`; issue #191 closed (evidence artifacts/pdca/rc2-191/r1/) |

## Decisions

- **Gap exclusion:** D171/D172 excluded from the actionable set (not-actionable-now, awaiting external input); issues stay OPEN in milestone `1.0.9-rc2`; no scope invented (planner, r=1).
- **Single group G1:** the footprint graph over the 24 admitted tasks is one connected component through shared core files; disjoint groups impossible. Single-group mode ⇒ current worktree/branch, no worktrees/branches, no merge.
- **Order** is footprint serialization plus `D189 → D188`.
- **Blockers:**
  - **D191 — external user-approval gate.** `docs/superpowers/specs/2026-10-05-provider-specific-extensions-design.md` declares `written spec — AWAITING USER REVIEW` and `implementation — NOT AUTHORIZED`. Autonomous DO cannot satisfy this; requires explicit user review/approval before DO of D191 (and, since G1 is sequential, before any task ordered after D191).
  - **D167 — P0 prerequisite:** in-repo `scripts/validate_inner_loop.py` absent (host copy exists at `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py`); resolve inside the lane or provide the manual evidence gate; do not weaken acceptance.

## Evidence contract r1/rv1

| req | row | check | owner | applicability |
|---|---|---|---|---|
| truthful admission | C-E01 | Validate admission snapshot for milestone `1.0.9-rc2`: 26 open issues, 24 admitted `ready`, D171/D172 excluded-gap and still OPEN in the milestone. | collection CHECK | at admission; re-check exclusions at completion |
| exclusive ownership | C-E02 | Expand the 24 planned footprints (incl. conditional paths), normalize C# identities via Roslyn, verify a single non-overlapping G1 ownership manifest and no concurrent writers. | scout (facts) + CHECK (verdict) | before first DO and on footprint change |
| verified completion | C-E03 | For each admitted task verify its frozen pdca-dotnet evidence contract, final CHECK and ACT on the final tree; missing/stale evidence is not completion; skipped providers ≠ pass. | CHECK | per task and before integration |
| safe integration | C-E04 | Single group ⇒ no group branch/merge; row is **N/A** unless a group branch exists. Integration = the authorized commits on `1.0.9-rc2` (patch/commit route recorded before execution). | coder (op) + CHECK (verdict) | only if a group branch exists |

CHECK re-gather budget: ≤2 targeted evidence requests per collection CHECK invocation, owned by CHECK.

## Общая верификация и восстановление

- Verification state: `unverified` (P done; DO not started).
- Defect id and history: none.
- Next allowed step: continue G1 DO at next pending task **D180** (D178 done, CHECK PASS r=1 n=2 rv=1, commit `31c9e9d9d0157706167a49a42546499affcaea07`, #178 closed; D176 done, CHECK PASS r=2/rv=2, commit `f760e5521f7630e56a2841e6b1e097aaae92273d`, #176 closed; D173 done, CHECK PASS r1/rv1/n2, commit `29e794fd`; D175 terminal incomplete — escalation decision (c) STOP, patch preserved; D174 done, CHECK PASS r1/n2, commit `6bc4ae41`; D162 done, CHECK PASS rv=2; D191 done; D190 done, CHECK PASS r1/rv1 evidence-completion, commit `1ff47762`, mutation closed by authorized no-tool disclosure, #190 closed). D160 set-aside, D170, D184 and D175 incomplete remain preserved and are not resumed here.
- **D170 (continued per the user-authorized continuation policy):** marked **incomplete** — terminal STOP (CHECK r=3 fail, same defect family as r=1/r=2; unmet R170-02/R170-03 variant/oracle closure; ClickHouse row satisfied but matrix not closed). Full change preserved as patch `docs/specs/status/rc2-170-evidence/D170-STOP-incomplete.patch` with artifacts `artifacts/pdca/rc2-170/{r1,r2,r3}/`; the task's product/test/doc paths were restored to the last verified-green tip `5b7fb9e7`; the lane continues with the next pending task. #170 stays **OPEN**.

## Done / Verified / Incomplete

- Done: D153 (#153, commit 8401ca51); D154 (#154, commit 5d72a79; issue #154 close blocked by GitHub API HTTP 500, retry pending); D157 (#157, commit 5978556f); D159 (#159, commit 078a7a2d; issue closed); D191 (#191, commit `e25f558e`; CHECK PASS snapshot-n2 `7e9fad9`/diff `f652c19e`; issue #191 closed; evidence artifacts/pdca/rc2-191/r1/). D162 (#162, commit ae946293, CHECK PASS rv=2). D174 (#174, commit `6bc4ae41`, CHECK PASS r1/n2; issue #174 closed; evidence `artifacts/pdca/rc2-174/`). D173 (#173, commit `29e794fd`, CHECK PASS r1/rv1/n2; issue #173 closed). D190 (#190, commit `1ff47762`, CHECK PASS r1/rv1 evidence-completion; mutation closed by authorized no-tool disclosure; issue #190 closed; residual risk: no direct SQL assertion for a present row whose every non-PK mapped column is NULL/default — coverage gap, not a defect; mapper-instance identity asserted indirectly). D176 (#176, commit f760e5521f7630e56a2841e6b1e097aaae92273d, CHECK PASS r=2/rv=2 (evidence-only re-gather, n unchanged); 6/6 mutants killed; coverage 86.8/79.3; container 3511/0/197; issue #176 closed; evidence docs/specs/status/evidence/rc2-176/). D178 (#178, commit `31c9e9d9d0157706167a49a42546499affcaea07`, CHECK PASS r=1 n=2 rv=1; issue #178 closed; boundary 9548/9350/0/198; integration enum 30/30 across 6 providers, 0 skipped; coverage DataContext 89.1/83.3, JsonShapePlan 93.1/91.3, JsonRowWriterFactory 94.8/82.8; perf acceptance 7/7, cached/prepared 2.02x; mutation 73.51% nonblocking, 34 observed survivors; evidence artifacts/pdca/178/r1/).
- Verified: no.
- Incomplete: **D160** (terminal r=4 STOP; preserved on `wip/d160-incomplete` + patch); **D170** (terminal r=3 STOP, same defect family; patch `docs/specs/status/rc2-170-evidence/D170-STOP-incomplete.patch`); **D184** (blocked, evidence-contract gap; patch `artifacts/pdca/rc2-184/D184-blocked.patch`); **D175** (terminal r=1 STOP, escalation decision (c), defect family «rv1 evidence-completeness / row-bound ledger provenance», 4× CHECK fail; patch `docs/specs/status/rc2-175-evidence/D175-STOP-incomplete.patch`). Excluded-gap: D171, D172 (issues OPEN).
