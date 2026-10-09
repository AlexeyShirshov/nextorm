---
# Collection 1.0.9-rc2-final

- collection-id: `rc2-final`
- input: open issues of GitHub milestone `1.0.9-rc2` (milestone #20): #208, #206, #170, #160, #151, #171, #172 (7 tasks)
- base: branch `1.0.9-rc2` @ `9a2a28717dc48db535c551e6c197803731d74b80`
- mode: autonomous; auto-commit authorized (collection/task status files only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- evidence contract reference: `docs/specs/status/collection-1.0.9-rc2-C-evidence.md`
- phase: **P — own PLANs running; ALL-barrier pending; no DO started; no branches/worktrees created**

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
| T208 | #208 | TBD | TBD | pending | pending | pdca-dotnet | N=1 | r=1 (baseline) | docs/specs/status/rc2-208-sqlserver-native-json-param-alias-1.md | concrete defect (native SQL Server JSON) |
| T206 | #206 | TBD | TBD | pending | pending | pdca-dotnet | N=1 | r=1 (baseline) | docs/specs/status/rc2-206-sourcegen-cs0111-alias-join-1.md | concrete defect (source-generator CS0111) |
| T170 | #170 | TBD | TBD | pending | pending | pdca-dotnet | N=1 | r=1 (baseline) | docs/specs/status/rc2-170-onetoone-fk-uniqueness-1.md | prior r=3 STOP; patch preserved |
| T160 | #160 | TBD | TBD | pending | pending | pdca-dotnet | N=1 | r=1 (baseline) | docs/specs/status/rc2-160-join-alias-mixing-1.md | prior r=4 STOP; preserved on `wip/d160-incomplete` |
| T151 | #151 | TBD | TBD | pending | pending | pdca-dotnet | N=1 | r=1 (baseline) | docs/specs/status/rc2-151-stryker-mutation-1.md | prior evidence-certification STOP |
| T171 | #171 | TBD | TBD | pending | pending | pdca-dotnet | N=1 | r=1 (baseline) | docs/specs/status/rc2-171-joininto-as-derived-1.md | barrier-risk (prior excluded-gap) |
| T172 | #172 | TBD | TBD | pending | pending | pdca-dotnet | N=1 | r=1 (baseline) | docs/specs/status/rc2-172-child-collection-projection-1.md | barrier-risk (prior excluded-gap) |

## Phase decisions

- Bootstrap: collection input list + ids + selected variants persisted; per-task own PLAN (COLLECTION TASK PLAN) starts. No task code; no group/branch/worktree until ALL-barrier.
- **ALL-barrier:** pending (0/7 `plan_state=ready`). DAG/groups/order not built.
- **Clustering:** pending.
- **PLAN-revision vector:** pending (populated after ALL-barrier).

## Общая верификация и восстановление

- Verification state: `not-started` (this collection's integrated CHECK not yet run).
- Next allowed step: run own PLAN per task; reach ALL-barrier.
---
