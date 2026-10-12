# T213 / #213 — r=5 n=2/3 corrective evidence ledger (DO)

Scope: candidate-3 removal of the broken `Projection<mappedEntity,scalar>` widening. Contract rows are
frozen in `manifest.json` (contract rv=4). This ledger maps each acceptance row to concrete test ids,
**quoted** assertions and the producing log (path + exit + counts). Every quote is an exact source line
(`file:line`); nothing here is invented.

Producing-run roots:
- `artifacts/pdca/T213/rv4/n2-3/` — corrective runs (build, focused, 7 full suites, ClickHouse scope).
- `artifacts/pdca/T213/rv4/d07/` — D07 six-provider SQL-gen + integration runs.
- `artifacts/pdca/T213/rv4/perf-final/` — AC08 acceptance + in-memory bench.

Log-quoting format: `path — exit N; total/total (skipped)`. All cited runs were invoked with
`--no-build` after `dotnet build nextorm.slnx -c Debug` (exit 0, 0 warnings / 0 errors;
`n2-3/build.log`).

---

## AC01 — declared/local composite 1:N / 1:1 (incl. explicit-key); crossed components and malformed counts fail deterministically

| Test id | Quoted assertion | Producing log |
|---|---|---|
| `NextORM.Core.Tests.JoinIntoCompositeRemainderTests.CompositeOneToMany_ShouldGroupChildrenByFullKey` | `parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 10 }, "only the full composite-key match is grouped");` (`JoinIntoCompositeRemainderTests.cs:129`) | `d07/joininto-core.log` — exit 0; 150/150 |
| `…CompositeOneToOne_ShouldAssignByFullKey` | `parents[0].Profile!.Id.Should().Be(50, "the full composite key selects the matching reference");` (`:179`) | `d07/joininto-core.log` — exit 0; 150/150 |
| Neg (crossed components) `…CompositeParentIdentity_SameFirstComponentDifferentLater_ShouldStayDistinct` | `parents.Should().HaveCount(2, "same first key component with a different later component must stay separate");` (`:474`) | `d07/joininto-core.log` — exit 0 |
| Neg (malformed counts) `JoinIntoCompositeJunctionRejectionTests.CompositeJunctionWhoseForeignKeysDoNotCoverEveryKeyComponent_ShouldThrowDeterministically` | `act.Should().Throw<NotSupportedException>().WithMessage("*do not cover every composite key component*");` + `ctx.ConnectionsRequested.Should().Be(0, "a malformed junction key is rejected at declaration; no SQL is executed");` (`:189-190`) | `n2-3/focused-core.log` — exit 0; 7/7 |
| Null/absent defaults `JoinIntoCompositeNullSemanticsTests.CompositeKey_NullComponent_InMemoryMatchesNullEqualsNull_DivergesFromSql`, `…AbsentLeftChild_ShouldBeNullNotDefault`, `…AbsentLeftChild_ShouldBeEmpty` | `parents[0].Profile.Should().BeNull(…);` (`:184`); `parents[0].Children.Should().BeEmpty("an absent LEFT child contributes no element");` (`:207`) | `d07/joininto-core.log` — exit 0 |

Composite-key metadata resolution (declared-only, structural) is pinned by
`JoinIntoIdentitySelectorCacheTests.CachesAndRebuildsSingleAndCompositeKeySelectors`:
`first.Should().BeOfType<JoinIntoKey>("a composite key is represented structurally");` and
`first.Should().NotBe(composite!(new CompositeKeyEntity { First = 1, Second = 3 }));`
(`JoinIntoIdentitySelectorCacheTests.cs:106,108`; `d07/joininto-core.log`).

## AC02 — parent identity deduplicates by the complete identity; same first component stays separate; keyless unchanged

- Complete-identity grouping: `CompositeOneToMany_ShouldGroupChildrenByFullKey` (quote above, AC01).
- Same-prefix/different-suffix separation: `CompositeParentIdentity_SameFirstComponentDifferentLater_ShouldStayDistinct`
  — `parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10);` and
  `parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(11);` (`:475-476`).
- Keyless-parent behavior unchanged: `JoinIntoOneToOneTests.KeylessChild_WithMoreThanOneChild_ShouldNotThrowAndKeepTheFirstChild`
  (`JoinIntoOneToOneTests.cs`; `d07/joininto-core.log` — exit 0; 150/150).
- Structural composite selector identity cache: `CachesAndRebuildsSingleAndCompositeKeySelectors`
  (`JoinIntoIdentitySelectorCacheTests.cs:102-108`).
- Log: `d07/joininto-core.log` — exit 0; 150/150; `n2-3/full-core.log` — exit 0; 2394/2394.

## AC03 — scalar M:N standalone and through SingleQuery (composite-junction M:N → #223)

| Test id | Quoted assertion | Producing log |
|---|---|---|
| `JoinIntoCompositeRemainderTests.ScalarManyToMany_Standalone_ShouldPreserveDuplicateLinks` | `parents[0].Children.Where(c => c.Id == 10).Should().HaveCount(2, "duplicate junction rows are distinct occurrences");` (`:361`) | `d07/joininto-core.log` — exit 0 |
| `…ManyToMany_SingleQuery_ShouldStitchBothCollections` (sync) | `parents[0].Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);` + `parents[0].Notes.Select(n => n.Id).Should().Equal(20);` (`:306-307`) | `d07/joininto-core.log` — exit 0 |
| `…ManyToMany_SingleQuery_Async_ShouldStitchBothCollections` | same assertions (`:321-322`) | `d07/joininto-core.log` — exit 0 |
| `…ScalarManyToMany_NonListTerminal_ShouldCountParents` | `joined.Count().Should().Be(2, "the non-list terminal counts parents, not denormalized rows");` (`:399`) | `d07/joininto-core.log` — exit 0 |
| `…ScalarManyToMany_RepeatedCachedExecution_ShouldStayStable` | stability across repeated cached execution (`JoinIntoCompositeRemainderTests.cs:409-415`) | `d07/joininto-core.log` — exit 0 |
| `JoinIntoManyToManySingleQueryTests.PlainJoinInto_WithLoadWithAndSingleQuery_ShouldStitchBothCollections` | stitches both collections under SingleQuery | `d07/joininto-core.log` — exit 0 |
| Integration `CommonTestSuite.JoinInto.JoinInto_ManyToMany_WithSingleQueryLoadWith_ShouldStitchTagsAndNotes` | shared-suite scalar M:N under SingleQuery | `d07/int-joininto-{sqlite,postgres,sqlserver,mysql}.log` — exit 0 |
| Integration `ClickHouseJoinIntoRemainderTests.JoinInto_ManyToMany_WithSingleQueryLoadWith_ShouldStitchTagsAndNotes` | ClickHouse scalar M:N under SingleQuery | `d07/int-joininto-clickhouse.log` — exit 0; 3/3 |
| Integration `MariaDbJoinIntoCompositeKeyIntegrationTests` (scalar M:N / composite-key control) | MariaDB-specific; MySQL is not a substitute | `d07/int-joininto-mariadb.log` — exit 0; 2/2 |

Composite-junction M:N is **not** implemented in T213 and is carried by #223; its fail-closed row is
Direction-D below.

## AC04 — filters/ordering/paging/multi-declaration/terminals/sync-async/repeated cached execution; no cache leakage or extra SingleQuery execution

| Test id | Quoted assertion | Producing log |
|---|---|---|
| `NextORM.Sqlite.Tests.JoinIntoExecutionTests.RepeatedCachedExecution_ShouldReusePlan_AndIssueOneRoundTripPerRun` | `Parents(ctx).JoinInto(…).ToCommand().Cache.Should().BeTrue("declaring a JoinInto must not disable the plan cache");` and `CountingInterceptor.Executions.Should().Be(2, "the cached plan is reused and each repeated execution still issues exactly one denormalized round trip");` (`JoinIntoExecutionTests.cs:183,195`) | `n2-3/focused-sqlite.log` — exit 0; 10/10 |
| `…ListTerminal_ShouldExecuteOneRoundTrip_AndGroupChildren`, `…ParentPaging_ShouldLimitParents_NotDenormalizedRows`, `…MultipleCollections_ShouldGroupEachIndependently`, `…CountAnyFirst_ShouldBeParentOnly`, `…ListTerminalAsync_…` | one round trip / paging scope / independent grouping / parent-only terminals | `n2-3/full-sqlite.log` — exit 0; 1645 total (1 skipped: opt-in `SqliteRowIdLobProbeTests`, unrelated) |
| `JoinIntoTerminalBoundaryTests.*` (`ToList`/`ToArray` stitch; `ToHashSet`/`ToEnumerable`/async-enumerable/`Prepare` stay parent-only) | terminal boundary | `n2-3/full-sqlite.log` — exit 0 |
| Six provider SQL-gen cache control `CompositeOneToMany_ShouldRenderComponentwiseJoin` (and siblings) | `command.Cache.Should().BeTrue("declaring a composite JoinInto must not disable the plan cache");` (`tests/nextorm.<p>.tests/JoinIntoCompositeSqlGenerationTests.cs:147,170,200`) | `d07/joininto-{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.log` — all exit 0 |

## AC05 — composite keys (no composite junction) and scalar M:N verified on all six providers incl. MariaDB

Componentwise `AND` ON-predicate asserted identically by each provider's
`JoinIntoCompositeSqlGenerationTests.CompositeOneToMany_ShouldRenderComponentwiseJoin`
(`sqlite:146`, `postgres:146`, `sqlserver:146`, `mysql:146`, `mariadb:169`, `clickhouse:146`):

> `.And.Contain("left join ck_child as t2 on (t1.tenant_id = t2.tenant_id and t1.id = t2.parent_id)");`

Strict MariaDB whole-SQL (`JoinIntoCompositeSqlGenerationTests.ManyToMany_WithSingleQueryLoadWith_ShouldRenderBothCollections`,
`mariadb:61-66`) — exact `.Be(...)`, so a preparation/materialization failure cannot pass:

> `sql.Should().Be(` / `"select t1.id, t1.name, t2.parentkey, t2.childkey, t2.occurrence, t3.id, t3.name, t4.id, "` + … / `"t4.parent_id as parentid, t4.text from jn_comp_parent as t1 left join (select parent_id as "` … `"as occurrence from jn_comp_link) as t2 on t1.id = t2.parentkey left join jn_comp_child as t3 on "` / `"(t3.id = t2.childkey and 1) left join jn_comp_note as t4 on t1.id = t4.parent_id");`

and `ManyToMany_Standalone_ShouldRenderDistinctDerivedLinkSql` (`mariadb:80-84`, same `row_number()` derived
link subquery + both edges).

Full-suite runs (all exit 0, no required skips):
`n2-3/full-sqlite.log` 1644/1645 (1 opt-in skip), `full-postgres.log` 888/888,
`full-sqlserver.log` 805/805, `full-mysql.log` 371/371, `full-mariadb.log` 252/252,
`full-clickhouse.log` 653/653.

Real integration: MariaDB `MariaDbJoinIntoCompositeKeyIntegrationTests` 2/2
(`d07/int-joininto-mariadb.log`), ClickHouse `ClickHouseJoinIntoRemainderTests` 3/3
(`d07/int-joininto-clickhouse.log`); SQLite/PostgreSQL/SQLServer/MySQL shared suite in previous rv4 runs.

## AC06 — metadata-only resolution and unrelated restrictions intact (negatives)

All quotes from `tests/nextorm.core.tests/JoinIntoRejectionTests.cs`; run `d07/joininto-core.log` — exit 0; 150/150.

| Negative | Quoted assertion |
|---|---|
| invalid explicit-key property types | `act.Should().Throw<NotSupportedException>().WithMessage("*same property type*");` (`:43`) |
| undeclared relationship (collection overload) | `…WithMessage("*requires an explicitly declared relationship*");` (`:66`) |
| many-to-many without junction | `…WithMessage("*no junction metadata*");` (`:101`) |
| one-to-one on collection overload | `…WithMessage("*expects a OneToMany relationship*");` (`:198`) |
| derived/joined-projection source | `…WithMessage("*derived (As) or joined projection source*");` (`:230`) |
| `Select`/`As` after `JoinInto` | `…WithMessage("*JoinInto cannot be combined with Select*");` (`:260`); `…WithMessage("*JoinInto cannot be combined with As*");` (`:272`) |
| arity overflow (>7 collections / M:N) | `…WithMessage("*at most 7 child collections*");` (`:319,365`) |
| combination with other joins (semi/anti/join) | `…WithMessage("*cannot be combined with other joins*");` (`:378,391,404`) |
| composite relationship key now resolves (positive) | `act.Should().NotThrow("a composite declared relationship key is supported since #213");` (`:217`) |
| composite junction FK now resolves (positive) | `act.Should().NotThrow("a composite junction foreign key is supported since #213");` (`:173`) |

## Direction-D — composite-junction M:N fails closed; carried by #223

`JoinIntoCompositeJunctionRejectionTests` (`JoinIntoCompositeJunctionRejectionTests.cs`), run
`n2-3/focused-core.log` — exit 0; 7/7 and `d07/joininto-core.log` — exit 0; 150/150:

- Message constant: `private const string CompositeRejectionMessage = "composite-junction M:N is not supported; see #223";` (`:24`)
- Rejection: `act.Should().Throw<NotSupportedException>().WithMessage(CompositeRejectionMessage);` (`:107`)
- No SQL: `ctx.ConnectionsRequested.Should().Be(0, "the rejection happens at preparation; no SQL is executed");` (`:108`)
- Scalar control: `act.Should().NotThrow("the scalar junction link source is not rejected");` (`:141`)
- `T213-EC-HANDOFF`: #223 exists in milestone 1.0.9 (`artifacts/pdca/T213/rv4/issue-view.json`).

## D03 removal (candidate 3) — the broken widening is gone

`QueryCommand.QueryPreparer.cs`: `IsSupportedProjectionScalar` and the mixed mapped-entity+scalar admission
branch were deleted; `BuildProjectionItems` now requires every item to be a mapped entity (fail-closed),
keeping only the `IdentitySlotAliases` mutation-CTE fallback.

| Test id | Assertion | Producing log |
|---|---|---|
| `JoinIntoCompositeCarrierSpikeTests.MixedProjection_EntityPlusValueScalar_ShouldFailClosed` (+ reference / nullable / all-NULL-entity) | `ShouldFailClosed(ctx, …)` = `((Action)(() => Prepare(ctx, joined))).Should().Throw<QueryPreparationException>(because);` (`JoinIntoCompositeCarrierSpikeTests.cs:25-26`) | `n2-3/focused-core.log` — exit 0; 7/7 |
| `JoinIntoMixedProjectionPublicTests.MixedProjection_EntityPlusScalar_ThroughPublicJoin_ShouldFailClosed` (+ nullable) | `act.Should().Throw<QueryPreparationException>("a mapped entity plus scalar whole-projection has no working public terminal and must fail closed");` (`JoinIntoMixedProjectionPublicTests.cs:29-30`) | `n2-3/focused-sqlite.log` — exit 0; 10/10 |

Prior D03 evidence (admission + `met`) is superseded: the admitted shape emitted invalid SQL
(`Item2.` column, unterminated join predicate) and had no working public terminal.

## AC08 — acceptance + in-memory benchmarks (corrected, honest)

- `E-PERF-FINAL` / `artifacts/pdca/T213/rv4/perf-final/acceptance-summary.md` (+ `acceptance.log`,
  `acceptance-rerun.log`, both exit 0) and `joininto-bench.md` (+ `joininto-bench.log`,
  `joininto-bench-rerun.log`, both exit 0).
- Corrected allocation facts: 1:N `Allocated` identical (3.94 MB) but Gen2 differs (140.6250 → 125.0000);
  M:N `Allocated` 4.27 → 4.26 MB (≈0.2%, fewer) with all GC counters differing (Gen0 484.3750 → 468.7500,
  Gen1/Gen2 468.7500/234.3750 → 437.5000/218.7500). The earlier "byte-identical Allocated" claim was
  wrong and is retracted in both files.
- Mean-time deltas (+80.5%/+51.2% in-memory; 27–61% on the 7 acceptance cases) are **not**
  code-attributable (allocation deltas ≤0.2%; untouched acceptance paths move identically); no
  regression/improvement claim.

## ClickHouse scope (#150 flaky test) — recorded, not T213

`ClickHouseExtremeRowNativeSpecificTests.FloatNativeParity_GroupedSingleFloat64_MinAndMax_AcrossDatasets`
is pre-existing flaky (`UNKNOWN_TABLE` in isolation; `QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING` and a
group-keys mismatch in full/shared runs). Test/provider/suite files are byte-identical base
`20a67a1c`→HEAD; the whole class passes 35/35 at HEAD. Filed as **#227** (milestone 1.0.9).
Evidence: `n2-3/ch-head-isolation.log` (exit 2), `n2-3/ch-head-class.log` (exit 0; 35/35),
`n2-3/clickhouse-issue-body.md`.
