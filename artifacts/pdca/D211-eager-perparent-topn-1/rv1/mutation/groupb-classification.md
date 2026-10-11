# D211 Group B — `SqlBuilder.MakePerParentPagingOuter` parameter-mode mirror

**Changed branches:** `src/nextorm.core/DataContext/SqlBuilder.cs:822,824,825,827,828,829,830,831,832,833,834,835`
(0/2, 0/6, 0/2, 0/2 condition coverage after the new `Feature=D211` tests).

## Classification

**Unreachable / defensive-by-design — root cause: plan-key divergence.**

The parameter pass of the per-parent paging outer statement is only entered on a **plan cache hit**
of the child command via `QueryPlanner.RefreshParameters` → `ExtractParams`
(`QueryPlanner.cs:790-799`, `QueryPlanner.cs:69-75`). The split eager loader rebuilds the child
command on every execution (`EntityBuilderEagerLoading.BuildChildCommand`, lines 339-357) with a
fresh key-chunk predicate `keys.Contains(childKey)` whose `keys` is an
`Expression.Constant(List<TKey>)` (`EntityBuilderEagerLoading.cs:431-434`).
`ExpressionPlanEqualityComparer.CompareConstant` compares constants with `Equals`
(`ExpressionPlanEqualityComparer.cs:362-368`); for two distinct `List<TKey>` instances this is
reference equality, so the freshly built child plan key never equals the stored one. The child
command is therefore a **plan cache miss on every execution**, `RefreshParameters` never runs for
it, and the parameter mirror is never reached. `PerParentPaging` / `PerParentPartition` are
`internal`, so no public path can reuse a per-parent child command to force a cache-hit refresh.

## Options ruled out

- **Cache suppression** — ruled out: the miss is logged only under `storeInCache && Cache`
  (`QueryPlanner.cs:625-626`), so `storeInCache` was true.
- **`HasUnkeyedScalarInValues`** — ruled out: that flag sets `storeInCache = false`
  (`QueryPlanner.cs:584`) and would silence the miss log; the log fired.
- **Genuinely unreachable by construction** — ruled out: the mirror is reachable from a cache hit;
  it is the fresh-constant predicate that prevents the hit.

## Probe evidence

| Probe | Observation |
|---|---|
| `groupb-probe.log` | `TOTAL STATEMENTS=4` over 2 runs: parent + child each run. |
| `groupb-probe2.log` | run1 parent miss `1064916916` + child miss `-537947611`; run2 parent **hit**, child miss `1982198788` (**different hash**), both `Making sql with param mode False`. |
| `groupb-probe3.log` | paged child miss `-665948566` → `1417499762`; unpaged child miss `383837502` → `817739875`; parent identical. The divergence is pre-existing and independent of paging. |

## Disposition

No `Feature=D211` test can cover `SqlBuilder.cs:822-835` through the public API; the new
`Split_PagedChild_RepeatedOnSameContext_StaysCorrectThroughThePlanCache` test still leaves
`824=0/2, 827=0/6, 829=0/2, 831=0/2`. No production change is made (production edits only for a
confirmed defect). The pre-existing child plan-key divergence is a **cache-miss performance
characteristic, not a correctness defect**, and is outside D211 scope: recorded as a non-blocking
candidate observation, not a DO→PLAN blocker.
