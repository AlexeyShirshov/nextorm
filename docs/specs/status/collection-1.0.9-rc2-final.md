# Collection 1.0.9-rc2-final

- collection-id: `rc2-final`
- input: open issues of GitHub milestone `1.0.9-rc2` (milestone #20): #208, #206, #170, #160, #151, #171, #172 (7 tasks)
- base: branch `1.0.9-rc2` @ `9a2a28717dc48db535c551e6c197803731d74b80`
- mode: autonomous; auto-commit authorized (collection/task status files only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- evidence contract reference: `docs/specs/status/collection-1.0.9-rc2-C-evidence.md`
- phase: **P complete — own PLANs persisted; ALL-barrier NOT passed (T171/T172 GAP) → STOP; no DAG/groups, no clustering, no DO, no branches/worktrees, no merge**

## Input task list

| id | issue | title | known state |
|---|---|---|---|
| T208 | #208 | SQL Server native JSON: projected `SqlFunctions.Parameter<T>` admitted native-eligible but rendered without a required alias | OPEN defect (#208); filed during R177; reproducible; baseline `2ac20818` |
| T206 | #206 | Source generator: CS0111 signature collision when two alias joins share `Alias.X` with different projection types | OPEN defect (#206) |
| T170 | #170 | Navigation: validate FK uniqueness for OneToOne | OPEN (#170); prior defect family R170-02/03; terminal r=3 STOP; patch preserved |
| T160 | #160 | Join aliases: free mixing positional/alias + root alias (`.WithAlias`) | OPEN (#160); terminal STOP (R02 regression); preserved on branch `wip/d160-incomplete` |
| T151 | #151 | Re-run Stryker mutation testing for native extreme-row | OPEN (#151); campaign works; prior evidence-certification STOP |
| T171 | #171 | JoinInto over As/derived source: rejected now, revisit on demand | OPEN (#171); prior excluded-gap (no consumer/AC) |
| T172 | #172 | Child-collection projection into anonymous/derived form (NORM.ChildCollection) | OPEN (#172); prior excluded-gap (awaiting consumer) |

## Tasks

| id | issue | group | branch | status | plan_state | selected_variant | cycle_id | plan_revision | status file | reason + patch |
|---|---|---|---|---|---|---|---|---|---|---|
| T208 | #208 | — | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-208-sqlserver-native-json-param-alias-1.md | own PLAN persisted (commit `27da4fd6`) |
| T206 | #206 | — | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-206-sourcegen-cs0111-alias-join-1.md | own PLAN persisted (commit `22f0171d`) |
| T170 | #170 | — | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-170-onetoone-fk-uniqueness-1.md | own PLAN persisted (commit `2f7836a6`); option (b) documented trust + closed runtime oracle |
| T160 | #160 | — | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-160-join-alias-mixing-1.md | own PLAN persisted (commit `1675a978`); sealed after 2 bounded recons; fresh selective port + R02 red↔green |
| T151 | #151 | — | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-151-stryker-mutation-1.md | own PLAN persisted (commit `7a65a15e`); frozen raw-artifact evidence bundle |
| T171 | #171 | — | 1.0.9-rc2 | pending | **gap** | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-171-joininto-as-derived-1.md | **GAP** — no consumer / agreed API / derivable AC; no `ready` placeholder (commit `d3bbfbda`) |
| T172 | #172 | — | 1.0.9-rc2 | pending | **gap** | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-172-child-collection-projection-1.md | **GAP** — no consumer / agreed API / derivable AC; no `ready` placeholder (commit `3c7dfcf6`) |

## Phase decisions

- **Bootstrap:** `6f01bc6545d335edaa2291acf9680b336db504fc` — input list, ids, selected variants; no code, no branches.
- **Own PLANs (P):** each task ran its own real PLAN (scout gather → planner decide → coder persist), stopping at the PLAN→DO boundary (no DO). Persistence commits above.
- **ALL-barrier: NOT passed.** T171 and T172 are declared **GAP** by their own PLAN: no concrete consumer, no agreed API/semantics, and no derivable acceptance criteria (including a negative case); re-pinning the already-shipped rejection guard or restating the documentation would manufacture scope. Per the collection contract a genuine gap fails the ALL-barrier, so the collection **STOPS** at P: DAG/groups/order not built, no clustering, no lanes/worktrees/branches, no DO, no merge.
- T208/T206/T170/T151/T160 are `plan_state=ready` (gate-1 complete, incl. acceptance + negative cases, closed variant matrix, docs plan, perf and reconnaissance decisions, execution mode, design checklist, pinned rv1 evidence contract).
- T160 required two bounded reconnaissance rounds (R160-01..11 verbatim; branch/patch tree identities; Roslyn state-transfer boundaries; legacy contract retrieval) before a genuinely revised r=1 plan could be sealed.
- **PLAN-revision vector:** T208 r=1; T206 r=1; T170 r=1; T160 r=1; T151 r=1; T171 n/a (gap); T172 n/a (gap).
- No `git merge` (no groups formed); `push` never performed.

## Общая верификация и восстановление

- Verification state: `not-started` (integrated CHECK not reached; collection stopped at the ALL-barrier).
- Next allowed step: **resolve the T171/T172 gaps** — supply a concrete consumer + agreed API/semantics for each (or decide they remain excluded from the milestone) and re-evaluate the barrier policy; only then may clustering and DO dispatch be considered. Do not create `ready` placeholders. The five ready plans are archived and reusable as-is; no re-plan needed unless their premises change.
