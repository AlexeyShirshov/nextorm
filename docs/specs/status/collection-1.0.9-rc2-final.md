# Collection 1.0.9-rc2-final

- collection-id: `rc2-final`
- input: milestone `1.0.9-rc2` (milestone #20) — re-scoped to **5 plannable tasks**: #208, #206, #170, #160, #151; **excluded-gap (2):** #171, #172
- base: branch `1.0.9-rc2` @ `9a2a28717dc48db535c551e6c197803731d74b80`; resume HEAD `be2652af8f5e985de4bb3a8618378b51c9ee09cc`
- mode: autonomous; auto-commit authorized (collection/task status files only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- evidence contract reference: `docs/specs/status/collection-1.0.9-rc2-C-evidence.md`
- phase: **P complete — re-scoped; T171/T172 excluded-gap removed from barrier; ALL-barrier PASSED; schedule BOUND (single lane G1); D authorized**

## Input task list

Plannable tasks (each `plan_state=ready`, `selected_variant=pdca-dotnet`, cycle `N=1`, `plan_revision r=1`, `rv1`, base `9a2a2871`):

| id | issue | title | state |
|---|---|---|---|
| T160 | #160 | Join aliases: free mixing positional/alias + root alias (`.WithAlias`) | plannable — `ready`; preserved on branch `wip/d160-incomplete` |
| T206 | #206 | Source generator: CS0111 signature collision when two alias joins share `Alias.X` with different projection types | plannable — `ready` |
| T208 | #208 | SQL Server native JSON: projected `SqlFunctions.Parameter<T>` admitted native-eligible but rendered without a required alias | plannable — `ready` |
| T170 | #170 | Navigation: validate FK uniqueness for OneToOne | plannable — `ready` |
| T151 | #151 | Re-run Stryker mutation testing for native extreme-row | plannable — `ready` |

Excluded gaps (removed from the barrier; not planned to DO; no `ready` placeholder):

| id | issue | title | reason | status file |
|---|---|---|---|---|
| T171 | #171 | JoinInto over As/derived source | `excluded-gap` — no concrete consumer, no agreed API/semantics, no derivable acceptance criteria | docs/specs/status/rc2-171-joininto-as-derived-1.md |
| T172 | #172 | Child-collection projection into anonymous/derived form | `excluded-gap` — no concrete consumer, no agreed API/semantics, no new testable acceptance criterion | docs/specs/status/rc2-172-child-collection-projection-1.md |

## Groups

| group | tasks (order) | mode | worktree | branch/ref | status |
|---|---|---|---|---|---|
| G1 | T160 → T206 → T208 → T170 → T151 | single lane, sequential | current worktree (no group worktree) | `1.0.9-rc2` @ `be2652af` | in-progress |

## Tasks

| id | issue | group | order | branch | status | plan_state | selected_variant | cycle_id | plan_revision | status file | reason + patch |
|---|---|---|---|---|---|---|---|---|---|---|---|
| T160 | #160 | G1 | 1 | 1.0.9-rc2 | incomplete | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-160-join-alias-mixing-1.md | own PLAN persisted (commit `1675a978`); selective port + R02 red↔green; terminal incomplete — fresh redo not executable; over-preserving red only in rejected patch; patch docs/specs/status/rc2-160-evidence/D160-STOP-incomplete.patch |
| T206 | #206 | G1 | 2 | 1.0.9-rc2 | incomplete | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-206-sourcegen-cs0111-alias-join-1.md | own PLAN persisted (commit `22f0171d`); NORMGEN007 guard red↔green; terminal incomplete — NORMGEN007 conflict unreachable (bodies determined by signatures); no product change; evidence artifacts/pdca/D206/rv1/ |
| T208 | #208 | G1 | 3 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-208-sqlserver-native-json-param-alias-1.md | own PLAN persisted (commit `27da4fd6`) |
| T170 | #170 | G1 | 4 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-170-onetoone-fk-uniqueness-1.md | own PLAN persisted (commit `2f7836a6`); documented trust + closed runtime oracle |
| T151 | #151 | G1 | 5 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-151-stryker-mutation-1.md | own PLAN persisted (commit `7a65a15e`); frozen raw-artifact evidence bundle |

## Phase decisions

- **Bootstrap:** `6f01bc6545d335edaa2291acf9680b336db504fc` — input list, ids, selected variants; no code, no branches.
- **Own PLANs (P):** each task ran its own real PLAN (scout gather → planner decide → coder persist), stopping at the PLAN→DO boundary (no DO). Persistence commits: T208 `27da4fd6`, T206 `22f0171d`, T170 `2f7836a6`, T160 `1675a978`, T151 `7a65a15e`; T171 `d3bbfbda`, T172 `3c7dfcf6`.
- **Re-scope (P resume):** T171/T172 are genuine **excluded-gap** — no concrete consumer, no agreed API/semantics, no derivable acceptance criteria (incl. a negative case). They are removed from the barrier and carry no `ready` placeholder. The collection is re-scoped to the 5 `ready` tasks.
- **ALL-barrier: PASSED** over the 5 plannable tasks.
- **Clustering (collection planner):** single group **G1** — sequential lane in the **current worktree/branch** (`1.0.9-rc2`); no group/task worktrees, no group branch, no `git merge --no-ff`. Order **T160 → T206 → T208 → T170 → T151**. T160/T206 share `JoinAliasGenerator.cs` and the joins docs → same lane, sequential; the remaining edges are scheduling choices, not prerequisites. No semantic cross-task dependencies recorded.
- **PLAN-revision vector:** T160 r=1/rv1; T206 r=1/rv1; T208 r=1/rv1; T170 r=1/rv1; T151 r=1/rv1. (T171/T172 n/a — excluded-gap.)
- **Merge:** not applicable (single lane); `push` never performed.
- **Autocommit:** task/status files only, explicit paths; message prefix `#<issue>` for tasks; collection-status commits use the `rc2-final:` prefix.

## Общая верификация и восстановление

- Verification state: `not-started` (collection C runs on the integrated tree after the D lane completes).
- Next allowed step: **D lane G1** — continue each saved PLAN from DO in order (T160, T206, T208, T170, T151), full cycle per task (DO → CHECK → ACT), auto-commit after each, keep the branch green at every task boundary; a terminal `incomplete` stops the group and preserves its patch under `docs/specs/status/rc2-<n>-evidence/`. After the lane: collection C-phase on the integrated tree.
