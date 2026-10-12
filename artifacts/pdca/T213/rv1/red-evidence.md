# T213 D01 — red evidence (unmodified build, r=1/rv=1)

source revision (pre-fix): `fc149bff2bfed855ccad81c158e871103ccbd7ae` (branch `1.0.9`)
build gate: `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors (`build.log`)

Inner-loop red subset is filtered by the frozen scope `D01-scope.json`
(`selectors: ["FullyQualifiedName~JoinInto"]`), run per project with `--no-build`.

| project | log | exit | total | failed | succeeded | failing tests (all attributable to the unsupported composite / M:N shapes) |
|---|---|---|---|---|---|---|
| core | red-core.log | 2 | 11 | 5 | 6 | CompositeOneToMany_ShouldGroupChildrenByFullKey; CompositeOneToOne_ShouldAssignByFullKey; CompositeJunctionManyToMany_ShouldStitchByFullJunctionKey; ManyToMany_SingleQuery_ShouldStitchBothCollections; ManyToMany_SingleQuery_Async_ShouldStitchBothCollections |
| sqlite | red-sqlite.log | 2 | 1 | 1 | 0 | JoinIntoCompositeSqlGenerationTests.ManyToMany_WithSingleQueryLoadWith_ShouldRenderBothCollections |
| postgres | red-postgres.log | 2 | 1 | 1 | 0 | Same |
| sqlserver | red-sqlserver.log | 2 | 1 | 1 | 0 | Same |
| mysql | red-mysql.log | 2 | 1 | 1 | 0 | Same |
| mariadb | red-mariadb.log | 2 | 2 | 1 | 1 | ManyToMany_WithSingleQuery_ShouldNotBeRejectedAtBuildTime |
| clickhouse | red-clickhouse.log | 2 | 1 | 1 | 0 | Same |

Failure causes (all `NotSupportedException` from the targeted guards):
- `JoinInto does not support a composite key on the relationship ...` (EntityBuilder.cs:989 collection / :1082 reference)
- `JoinInto does not support a composite junction key on the relationship ...` (EntityBuilder.cs:971)
- `SingleQuery eager loading does not support a many-to-many JoinInto declaration ...` (EntityBuilder.cs:1362)

Scalar controls pass now (core: 6 succeeded): composite parent identity dedup (same first component,
different later component stays distinct), scalar standalone M:N with duplicate junction rows, INNER +
child filter, async parity, non-list terminal parent count, repeated cached execution.

Integration red (authored, not run in the inner loop; boundary evidence is DO->CHECK):
`CommonTestSuite.JoinInto.cs` `JoinInto_ManyToMany_WithSingleQueryLoadWith_ShouldStitchTagsAndNotes`,
`ClickHouseJoinIntoRemainderTests`, `MariaDbJoinIntoRemainderIntegrationTests`.
