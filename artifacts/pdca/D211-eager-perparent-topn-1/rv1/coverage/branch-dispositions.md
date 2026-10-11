# D211 changed-P1 branch dispositions (final corrected tree)

Base for the change set: `b527be6f37b9301ed9d2245b803a1bd3e5e43ffc` (pre-T211), working tree
= `e98ae60e` + r1-n2 corrections + the two added tests below. Coverage collected by the
CI-equivalent command (`.github/workflows/dotnet.yml` + `coverage.settings.xml`):
`dotnet-coverage collect -s coverage.settings.xml -f cobertura -o coverage.xml "dotnet test --no-build --verbosity normal"`,
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, 2026-10-12.
Run: total 10846, succeeded 10648, failed 0, skipped 198 (exit 0); no required provider skipped.
Aggregate: line **88.5%** (threshold 85), branch **80.6%** (27651/34299, threshold 75).
Changed: lines **254/279**; branch conditions **160/194**.

Numbers are the Cobertura condition coverage from `coverage.xml`, cross-checked by
`changed_branch_delta.py` (see `branch-delta.json`/`branch-delta.txt`).

## Resolved by tests added in this correction

| Final line | cc | Disposition |
|---|---|---|
| `SqlBuilder.cs:734` (rv1 label `:724`, partition separator `i == 0 ? "partition by " : ", "`) | **100% (2/2)** | **Covered.** Both the first and subsequent partition columns are rendered by `Split_CompositeKey_PagedSql` (`partition by tenant, parent_id`) and the composite integration tests. The prior "composite split unreachable / defensive" claim is **withdrawn** and contradicted by the committed composite tests. |
| `QueryPlanEqualityComparer.cs:166` (paged vs unpaged) | **100% (2/2)** | **Covered** by `tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.PerParentPaging_PlanKey_SeparatesPagedPartitionAndEmbeddedShapes` (new): paged vs unpaged, plus an equal-shape control. |
| `QueryPlanEqualityComparer.cs:167` (partition selector) | **100% (4/4)** | **Covered** by the same test: two paged commands with different partition selectors, plus control. |
| `QueryPlanEqualityComparer.cs:170` (embedded vs split) | **100% (4/4)** | **Covered** by the same test: embedded vs top-level paged shape, plus control. REQ-05 now has a closing test. |
| `EntityBuilder.cs:1650` (`combined ??= parentSorting is null ? [] : [..]`, null `parentSorting`) | **100% (4/4)** | **Covered** by `SingleQuery_PagedChild_WithoutParentOrder_KeepsChildPageOrder` (new): a single-query paged child with **no parent ORDER BY**, so the empty-parent-ordering branch is taken; children stay per-parent ordered. |

## Defensive / unreachable (no public path can reach; guard or mode mirror)

Group B root cause (unchanged, see `mutation/groupb-classification.md`): the parameter mirror is
only entered on a child-command plan-cache hit via `QueryPlanner.RefreshParameters`, but the split
loader rebuilds the child with a fresh `Expression.Constant(List<TKey>)` key-chunk predicate whose
`Equals` is reference equality, so the child is a cache miss on every execution. `PerParentPaging`
etc. are `internal`, so no public path can force a cache-hit refresh.

| Final line | cc | Disposition / evidence |
|---|---|---|
| `SqlBuilder.cs:834` | 0% (0/2) | **Group B** param-mode mirror — unreachable (above). |
| `SqlBuilder.cs:837` | 0% (0/6) | **Group B** param-mode mirror — unreachable. |
| `SqlBuilder.cs:839` | 0% (0/2) | **Group B** param-mode mirror — unreachable. |
| `SqlBuilder.cs:841` | 0% (0/2) | **Group B** param-mode mirror — unreachable. |
| `SqlBuilder.cs:643` | 1/2 | **Defensive guard** — `selectInto is not null` (SELECT … INTO) is never produced by the eager child path. |
| `SqlBuilder.cs:646` | 2/4 | **Defensive guard** — `From is null || SubQuery is not null`; `ValidatePerParentPagingChild` rejects non-physical sources before rendering, so this is a redundant safety net. |
| `SqlBuilder.cs:666` | 1/2 | **Param-mode mirror** — this renderer is only entered in SQL mode (same root cause as Group B); the `ParamMode` side is dead. |
| `SqlBuilder.cs:683` | 1/2 | **Param-mode mirror** — `inner` is `null` only in param mode. |
| `SqlBuilder.cs:706` | 1/2 | **Defensive** — `PerParentPartitionColumns ?? []` null fallback; the eager pipeline always sets a partition, so the empty fallback is untaken. |
| `SqlBuilder.cs:707` | 1/2 | **Defensive** — `Sorting ?? []` null fallback; a per-parent page requires ordering (validated), so non-null. |
| `SqlBuilder.cs:714` | 1/2 | **Defensive guard** — partition-empty throw is pre-empted by preparation-time validation; a per-parent child always has a partition key. |
| `SqlBuilder.cs:717` | 1/2 | **Defensive guard** — sorting-empty throw is pre-empted by the unordered-page rejection at preparation. |
| `SqlBuilder.cs:722` | 1/2 | **Param-mode mirror** — `builder` null side. |
| `SqlBuilder.cs:747` | 1/2 | **Defensive** — `wrotePartition` is always `true` (partition mandatory), so the no-partition `"order by "` branch is unreachable. |
| `SqlBuilder.cs:762` | 3/4 | **Param-mode mirror** — missing quarter is the `builder is not null &&` short-circuit; both Asc and Desc are otherwise covered. |
| `SqlBuilder.cs:769` | 2/4 | **Param-mode mirror** — `null` builder → `string.Empty`. |
| `SqlBuilder.cs:796` | 1/2 | **Param-mode mirror** — `outer` null side. |
| `SqlBuilder.cs:801` | 1/2 | **Param-mode mirror** — `!ParamMode` false side. |
| `SqlBuilder.cs:804` | 2/4 | **Defensive** — tag-present side; the eager child command is built by the child factory and carries no tag. |
| `SqlBuilder.cs:847` | 1/2 | **Param-mode mirror** — `outer` null side. |
| `SqlBuilder.cs:875` | 3/4 | **Defensive** — `!hasLower && !hasUpper` empty-page side; `PerParentPaging` is only set when paging is non-empty (`Paging.IsEmpty`), so unreachable (self-documented, code-audit S-1). |
| `EntityBuilderEagerLoading.cs:363` | 1/2 | **Defensive** — in-memory throw inside `BuildChildCommand`; the in-memory rejection is enforced earlier (`ValidateLoadWithForJoinInto` `:632-633` and `QueryCommand.QueryPreparer.cs:48`), so line 364 is not reached; fail-closed is retained. |
| `EntityBuilderEagerLoading.cs:577` | 1/2 | **Defensive** — `!HasPerParentTopN` early return; `ValidatePerParentPagingChild` is only invoked for paged specs (gated by `RequiresPerParentTopN`), so the no-op return is unreachable. |

## Uncovered changed lines

The uncovered changed lines (throws and param-mode mirror bodies) are the bodies of the defensive
branches above: `SqlBuilder.cs:644,647,715,718-720,805,832,834,835,837-845,876,879-881` and
`EntityBuilderEagerLoading.cs:364,578`. They have no independent behaviour beyond the branch
dispositions recorded here; no feasible changed P1 branch is left without a disposition.

## Regression / aggregate

No comparable aggregate coverage regression: prior CI run 88.5% line / 80.5% branch; this run
88.5% / 80.6%. Both thresholds met. The two added tests are the only tree change relative to the
D:06 coverage artifact.
