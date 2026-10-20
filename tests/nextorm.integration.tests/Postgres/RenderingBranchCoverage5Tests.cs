using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL source-rendering coverage (cycle 5, cluster C6/D6) executed against a live Testcontainers
/// server. Every fact creates, seeds and drops its own <c>pg5r_marker</c>/<c>pg5r_line</c> tables and
/// asserts the rows the server returns.
///
/// <para>
/// Targeted arms: APPLY over a correlated DERIVED query (PostgreSQL renders <c>CROSS JOIN LATERAL</c> /
/// <c>LEFT JOIN LATERAL ... ON true</c>) and over a PLAIN physical table, where
/// <c>SqlSourceRenderer.MakeApplyJoin</c> (:399-414) takes the plain-table fallback because
/// <c>SupportsApplyOnPlainTable</c> is false on this dialect; the <c>SelectWhereMax</c>/<c>SelectWhereMin</c>
/// native-versus-portable dispatch (<c>SqlBuilder.MakeExtremeRowSelect</c> :942) over a nullable key
/// with ties (portable rank lowering) and with a single winner (native renderer); a row lock attached
/// to the primary source of a joined statement versus a single-source lock.
/// </para>
///
/// <para>
/// Overlap avoided: the correlated APPLY family is proved live by
/// <c>CommonTestSuite.CorrelatedQuery.cs:341</c>/<c>:379</c>, the derived join and per-table-hint
/// rejection by <c>PostgresSourceRenderingBranchCoverage4Tests</c>, the plan-key lock differential by
/// <c>PostgresPlanKeyBranchCoverage5Tests</c>, and the <c>DISTINCT ON</c> rendering arm by
/// <c>PostgresDialectBranchCoverage3Tests:107</c> - none of those is repeated here.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class PostgresRenderingBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateMarker()
    {
        Execute("drop table if exists pg5r_line");
        Execute("drop table if exists pg5r_marker");
        Execute("create table pg5r_marker (id int not null primary key, num int null, grp int null)");
        Execute("create table pg5r_line (id int not null primary key, marker_id int not null, amount int null)");
        Execute("insert into pg5r_marker (id, num, grp) values (1, 10, 1), (2, 10, 1), (3, 20, 2), (4, 20, 2), (5, null, 3)");
        Execute("insert into pg5r_line (id, marker_id, amount) values (11, 1, 5), (12, 1, 7), (13, 2, 9)");
    }

    private void DropMarker()
    {
        Execute("drop table if exists pg5r_line");
        Execute("drop table if exists pg5r_marker");
    }

    /// <summary>
    /// Distinguishing arm: the LATERAL apply over a class-owned correlated derived source (the outer
    /// value filters the inner rows) and the plain-table fallback spelling on the same context. The
    /// correlated form drops the left row with no match; the plain fallback keeps every pair.
    /// </summary>
    [Fact]
    public void CrossApply_DerivedAndPhysical_ShouldTakeTheLateralAndFallbackArms()
    {
        CreateMarker();
        try
        {
            var derived = Context.From<Pg5rMarker>()
                .Where(m => m.Grp == 1)
                .CrossApply(m => Context.From<Pg5rMarker>()
                    .Where(x => x.Num == m.Num && x.Id > m.Id)
                    .Select(x => new { x.Id, x.Num }))
                .OrderBy(p => p.Item1.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();

            derived.Should().ContainSingle("only marker 1 has a later row with the same num");
            derived[0].Left.Should().Be(1);
            derived[0].Right.Should().Be(2);

            var physical = Context.From<Pg5rMarker>()
                .CrossApply(Context.From<Pg5rMarker>())
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();

            physical.Should().HaveCount(25, "five marker rows times five marker rows as a plain cross join");
            physical.Select(r => r.Left).Should().Contain(5, "the row with no correlated match still participates in the fallback");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the same two sources under OUTER APPLY. The correlated left row without a
    /// match survives with a NULL right side; the plain-table fallback is a LEFT JOIN ... ON true and
    /// keeps every left row with a non-NULL right side.
    /// </summary>
    [Fact]
    public void OuterApply_DerivedAndPhysical_ShouldKeepTheUnmatchedRows()
    {
        CreateMarker();
        try
        {
            var derived = Context.From<Pg5rMarker>()
                .Where(m => m.Grp == 1)
                .OuterApply(m => Context.From<Pg5rMarker>()
                    .Where(x => x.Num == m.Num && x.Id > m.Id)
                    .Select(x => new { x.Id }))
                .OrderBy(p => p.Item1.Id)
                .Select(p => new { Left = p.Item1.Id, Right = (int?)p.Item2.Id })
                .ToList();

            derived.Should().HaveCount(2, "both group-1 marker rows survive the OUTER APPLY");
            derived.Select(r => r.Left).Should().Equal(1, 2);
            derived.Select(r => r.Right).Should().Equal(new int?[] { 2, null });

            var physical = Context.From<Pg5rMarker>()
                .OuterApply(Context.From<Pg5rMarker>())
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();

            physical.Should().HaveCount(25, "a LEFT JOIN ... ON true over five times five rows");
            physical.Should().OnlyContain(r => r.Right > 0, "the fallback right side is never NULL");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the extreme-row dispatch over a nullable key with ties (every tied row) and
    /// with a single winner (the native renderer's single-row form); NULL key rows never win.
    /// </summary>
    [Fact]
    public void Extreme_TiesAndSingle_ShouldKeepTheExtremeRowsOnly()
    {
        CreateMarker();
        try
        {
            var maxAll = Context.From<Pg5rMarker>()
                .SelectWhereMax(x => x.Num, ExtremeRowTies.All)
                .OrderBy(x => x.Id)
                .ToList();

            maxAll.Select(r => r.Id).Should().Equal(new[] { 3, 4 }, "both rows carry the maximum 20");
            maxAll.Should().OnlyContain(r => r.Num == 20);

            var maxOne = Context.From<Pg5rMarker>().SelectWhereMax(x => x.Num).ToList();
            maxOne.Should().ContainSingle();
            maxOne[0].Num.Should().Be(20);
            maxOne[0].Id.Should().BeOneOf(3, 4);

            var minAll = Context.From<Pg5rMarker>()
                .SelectWhereMin(x => x.Num, ExtremeRowTies.All)
                .OrderBy(x => x.Id)
                .ToList();

            minAll.Select(r => r.Id).Should().Equal(new[] { 1, 2 }, "both rows carry the minimum 10");
            minAll.Should().OnlyContain(r => r.Num == 10);

            var minOne = Context.From<Pg5rMarker>().SelectWhereMin(x => x.Num).ToList();
            minOne.Should().ContainSingle();
            minOne[0].Num.Should().Be(10);
            minOne[0].Id.Should().BeOneOf(1, 2);

            maxAll.Select(r => r.Id).Should().NotContain(5);
            minAll.Select(r => r.Id).Should().NotContain(5, "a NULL comparison value never wins");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: a row lock attached to the PRIMARY SOURCE of a joined statement versus a
    /// single-source lock. Both execute; two structurally identical locked commands compare as the same
    /// plan, and executing the same command on the reused context yields the same rows.
    /// </summary>
    [Fact]
    public void RowLock_OnJoinedSourceAndSingleSource_ShouldReadTheLockedRowsAndReuseThePlan()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> LockedJoin() => Context.From<Pg5rMarker>(o => o.ForUpdate())
                .Join(Context.From<Pg5rLine>(), (m, l) => m.Id == l.MarkerId)
                .OrderBy(p => p.Item1.Id)
                .OrderBy(p => p.Item2.Id)
                .Select(p => p.Item1.Id);

            QueryCommand<int> LockedSingle() => Context.From<Pg5rMarker>()
                .ForUpdate()
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            var joined = LockedJoin();
            var joinedAgain = LockedJoin();
            var single = LockedSingle();

            joined.PrepareCommand(dontCalculateHash: false, CancellationToken.None);
            joinedAgain.PrepareCommand(dontCalculateHash: false, CancellationToken.None);
            single.PrepareCommand(dontCalculateHash: false, CancellationToken.None);

            joined.GetQueryPlanEqualityComparer().Equals(joined, joinedAgain).Should()
                .BeTrue("two identical locked joined commands are the same plan");
            joined.GetQueryPlanEqualityComparer().Equals(joined, single).Should()
                .BeFalse("a joined source lock and a single-source lock are different plans");

            joined.ToList().Should().Equal(new[] { 1, 1, 2 }, "the joined lock keeps the line multiplicity");
            joinedAgain.ToList().Should().Equal(new[] { 1, 1, 2 }, "re-preparing the same command reuses the plan and rows");
            single.ToList().Should().Equal(new[] { 1, 2, 3, 4, 5 }, "the single-source lock reads every marker row");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the ANSI super-aggregate renderings <c>GROUP BY ROLLUP (...)</c> and
    /// <c>GROUP BY CUBE (...)</c> executed live on this provider. The class-owned seed has three groups
    /// with 2, 2 and 1 rows, so every form must return the three group counts plus the grand total 5.
    /// </summary>
    [Fact]
    public void GroupingRollupAndCube_ShouldReturnGroupAndGrandTotalAggregates()
    {
        CreateMarker();
        try
        {
            var rollup = Context.From<Pg5rMarker>()
                .GroupByRollup(x => new { x.Grp })
                .Select(g => SqlFunctions.Sql.count())
                .ToList();

            rollup.Should().BeEquivalentTo(new[] { 2, 2, 1, 5 },
                "three group counts plus the rollup grand total");

            var cube = Context.From<Pg5rMarker>()
                .GroupByCube(x => new { x.Grp })
                .Select(g => SqlFunctions.Sql.count())
                .ToList();

            cube.Should().BeEquivalentTo(new[] { 2, 2, 1, 5 },
                "one grouping column makes CUBE and ROLLUP return the same aggregate rows");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the extreme-row dispatch on a GROUPED and FILTERED source. The single form
    /// takes the native grouped <c>DISTINCT ON</c> renderer (one winning row per group), the tie form
    /// keeps the portable rank lowering partitioned by the group (every tied winner per group), and the
    /// all-NULL group never surfaces. Both executions project through the explicit projection overload.
    /// </summary>
    [Fact]
    public void Extreme_GroupedTiesAndSingle_ShouldKeepPerGroupExtremes()
    {
        CreateMarker();
        try
        {
            var maxOnePerGroup = Context.From<Pg5rMarker>()
                .Where(x => x.Num != null)
                .SelectWhereMax(x => x.Num, x => new { x.Id, x.Num, x.Grp }, ExtremeRowTies.One, x => x.Grp)
                .OrderBy(r => r.Grp)
                .ToList();

            maxOnePerGroup.Should().HaveCount(2, "one winning row per non-NULL group");
            maxOnePerGroup.Select(r => r.Grp).Should().Equal(new int?[] { 1, 2 });
            maxOnePerGroup.Select(r => r.Num).Should().Equal(new int?[] { 10, 20 });
            maxOnePerGroup[0].Id.Should().BeOneOf(new[] { 1, 2 }, "both group-1 rows tie at the maximum 10");
            maxOnePerGroup[1].Id.Should().BeOneOf(new[] { 3, 4 }, "both group-2 rows tie at the maximum 20");

            var minAllPerGroup = Context.From<Pg5rMarker>()
                .Where(x => x.Num != null)
                .SelectWhereMin(x => x.Num, x => new { x.Id, x.Num, x.Grp }, ExtremeRowTies.All, x => x.Grp)
                .OrderBy(r => r.Grp)
                .OrderBy(r => r.Id)
                .ToList();

            minAllPerGroup.Should().HaveCount(4, "every tied group minimum survives");
            minAllPerGroup.Select(r => r.Id).Should().Equal(new[] { 1, 2, 3, 4 });
            minAllPerGroup.Select(r => r.Num).Should().Equal(new int?[] { 10, 10, 20, 20 });
            minAllPerGroup.Select(r => r.Grp).Should().Equal(new int?[] { 1, 1, 2, 2 });
            minAllPerGroup.Select(r => r.Id).Should().NotContain(5, "the all-NULL group has no extreme row");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The PostgreSQL <c>TABLESAMPLE</c> renderer through both public shapes (SYSTEM with a seed and
    /// BERNOULLI without one) at 100 %, plus the documented argument guard on the option itself.
    /// </summary>
    [Fact]
    public void TableSample_BothMethodsAndInvalidPercent_ShouldRenderAndGuard()
    {
        CreateMarker();
        try
        {
            var system = Context.From<Pg5rMarker>(o => o.TableSample(100, TableSampleMethod.System, seed: 42))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();
            system.Should().Equal(new[] { 1, 2, 3, 4, 5 }, "a 100 % SYSTEM sample keeps every row");

            var bernoulli = Context.From<Pg5rMarker>(o => o.TableSample(100, TableSampleMethod.Bernoulli))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();
            bernoulli.Should().Equal(system, "a 100 % BERNOULLI sample keeps every row");

            var invalid = () => Context.From<Pg5rMarker>(o => o.TableSample(0));
            invalid.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*TABLESAMPLE percent must be in (0, 100]*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The PostgreSQL <c>DISTINCT ON</c> renderer executed live (first row per key), and the
    /// extreme-row compatibility guard that rejects the combination before any SQL is assembled.
    /// </summary>
    [Fact]
    public void DistinctOn_ShouldKeepTheFirstRowPerKeyAndRejectExtremeComposition()
    {
        CreateMarker();
        try
        {
            var rows = Context.From<Pg5rMarker>()
                .DistinctOn(x => x.Grp)
                .OrderBy(x => x.Grp)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Grp })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(new[] { 1, 3, 5 },
                "DISTINCT ON keeps the first ordered row of every group");
            rows.Select(r => r.Grp).Should().Equal(new int?[] { 1, 2, 3 });

            var extreme = () => Context.From<Pg5rMarker>()
                .DistinctOn(x => x.Grp)
                .SelectWhereMax(x => x.Num)
                .Select(x => x.Id)
                .ToList();
            extreme.Should().Throw<BuildSqlCommandException>()
                .WithMessage("*SelectWhereMax/SelectWhereMin cannot be combined with DISTINCT ON*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// A derived-source sub-query hint renders and executes on PostgreSQL, an empty hint is rejected by
    /// the option, and the dialect's default join-hint arm rejects an unsupported hint.
    /// </summary>
    [Fact]
    public void SubQueryHint_AndUnsupportedHintArms_ShouldExecuteOrGuard()
    {
        CreateMarker();
        try
        {
            var derived = Context.From<Pg5rMarker>()
                .Where(x => x.Grp == 1)
                .Select(x => new { x.Id, x.Grp });

            var rows = Context.From(derived, o => o.WithSubQueryHint("NestLoop(t1)"))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();
            rows.Should().Equal(new[] { 1, 2 }, "the hint is an optimizer directive, not a row filter");

            var emptyHint = () => new FromOptions().WithSubQueryHint(" ");
            emptyHint.Should().Throw<ArgumentException>().WithMessage("*A subquery hint must be a non-empty string*");

            var dialect = new PostgresDialect();
            var unsupportedHint = () => dialect.MakeJoinKeyword(JoinType.Inner, JoinStrictness.Default, false, "loop");
            unsupportedHint.Should().Throw<NotSupportedException>().WithMessage("*Join hints are not supported*");

            var limitBy = () => Context.From<Pg5rMarker>()
                .OrderBy(x => x.Id)
                .LimitBy(1, x => x.Grp)
                .Select(x => x.Id)
                .ToList();
            limitBy.Should().Throw<NotSupportedException>()
                .WithMessage("*The LIMIT BY clause is not supported by this SQL dialect*");
        }
        finally
        {
            DropMarker();
        }
    }

    [SqlTable("pg5r_marker")]
    public sealed class Pg5rMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("num")]
        public int? Num { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }

    [SqlTable("pg5r_line")]
    public sealed class Pg5rLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }
    }
}
