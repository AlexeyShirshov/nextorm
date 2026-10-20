using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Serialises this class against every other integration collection: the cached-path diagnostic
/// counters (<c>QueryPlanner.FallbackRefreshes</c>, <c>ParamRefreshRecipe.FastBindHits</c>) are
/// process-wide, so the parameter-pass proof is read without a concurrent mutation.
/// </summary>
[CollectionDefinition("Cycle 8 grouping", DisableParallelization = true)]
public sealed class Cycle8GroupingCollectionDefinition;

/// <summary>
/// Grouping-modifier and portable extreme-row coverage (cycle 8, unit D3) executed against live
/// PostgreSQL and SQL Server containers. Every theory row is one engine variant of the same fact:
/// the grouping facts assert the exact row multisets the server returns, the extreme-row fact asserts
/// that a warmed plan re-binds a changed captured value.
///
/// <para>
/// Targeted arms of <c>src/nextorm.core/DataContext/SqlBuilder.cs</c>: the out-of-range grouping-set
/// index guard (:294-295, both operands of the range test) and the parameter pass of the portable
/// extreme-row outer statement (:1351-1367 — the select-list walk and the sort walk, reached only when
/// <c>SqlContext.ParamMode</c> is true), together with the parameter-mode rank-expression walk it
/// depends on (:1421-1453). The parameter pass runs on a plan-cache hit through
/// <c>QueryPlanner.ExtractParams</c>; a sort keeps <c>ParamRefreshRecipe.TryCreate</c> at
/// <c>null</c>, so the second execution of the same shape re-renders in parameter mode instead of
/// binding through the fast recipe.
/// </para>
///
/// <para>
/// Live capability re-proof (no new line coverage, kept because the D3 contract names it): the public
/// <c>GroupByCube</c> / <c>GroupByGroupingSets</c> builders render and execute on both engines, which
/// is why <c>D2-GROUPING-CUBE</c> and <c>D2-GROUPING-SETS-LIVE</c> stay reopened: the capability and
/// the public route exist, and the already-covered arms are
/// <c>Postgres/RenderingBranchCoverage5Tests.cs:248</c>, <c>SqlServer/RenderingBranchCoverage5Tests.cs:207</c>
/// and <c>Coverage6/PostgresRenderingBranchCoverage6Tests.cs:315</c>.
/// </para>
///
/// <para>
/// Recorded, never asserted (no route on this provider set): the unsupported-grouping rejection at
/// :243-250 is unreachable on PostgreSQL and SQL Server because both dialects report
/// <c>SupportsRollup</c>/<c>SupportsCube</c>/<c>SupportsGroupingSets</c> as <c>true</c>
/// (<c>PostgresDialect.cs:289-293</c>, <c>SqlServerDialect.cs:222-227</c>); the
/// <c>GROUPING SETS</c> + <c>WITH TOTALS</c> rejection at :275-276 has no public chain here because
/// <c>WithTotals</c> is a ClickHouse-only extension and the ClickHouse dialect never reports
/// <c>SupportsGroupingSets</c>, so :272 rejects first. No skip is declared: both containers are
/// required and the class fails closed.
/// </para>
/// </summary>
[Collection("Cycle 8 grouping")]
public sealed class Cycle8GroupingTests : ProviderTestSuite
{
    private const string PostgresEngine = "postgres";

    private const string SqlServerEngine = "sqlserver";

    // Seed: groups 1 and 2 hold two rows each, group 3 holds one row with a NULL tag, and one row has
    // a NULL group. Six rows in total, three distinct tags (10, 20 and NULL). The real NULL group is
    // what separates a subtotal row from a genuine NULL-keyed group in the grouping-set facts.
    private const string PostgresDdl = """
        drop table if exists c8g_marker;
        create table c8g_marker
        (
            id int not null primary key,
            grp int null,
            tag int null,
            num int null
        );
        insert into c8g_marker (id, grp, tag, num) values
            (1, 1, 10, 10),
            (2, 1, 10, 10),
            (3, 2, 20, 20),
            (4, 2, 20, 20),
            (5, 3, null, null),
            (6, null, null, null);
        """;

    private const string SqlServerDdl = """
        drop table if exists c8g_marker;
        create table c8g_marker
        (
            id int not null primary key,
            grp int null,
            tag int null,
            num int null
        );
        insert into c8g_marker (id, grp, tag, num) values
            (1, 1, 10, 10),
            (2, 1, 10, 10),
            (3, 2, 20, 20),
            (4, 2, 20, 20),
            (5, 3, null, null),
            (6, null, null, null);
        """;

    public Cycle8GroupingTests()
    {
        Execute(Context, PostgresDdl);

        using var sqlServer = CreateSqlServerContext();
        Execute(sqlServer, SqlServerDdl);
    }

    /// <summary>
    /// The base suite (fail-closed availability, shared seed) is declared for PostgreSQL; the SQL
    /// Server context is created per theory row because one harness instance owns one provider. Both
    /// providers are mandatory for this class, so an unavailable SQL Server fails the fact instead of
    /// skipping it.
    /// </summary>
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    public static TheoryData<string> Engines => new() { PostgresEngine, SqlServerEngine };

    private IDataContext Context => _sut.DataProvider;

    private static IDataContext CreateSqlServerContext()
    {
        var provider = SqlServerTestProvider.Instance;
        provider.IsAvailable.Should().BeTrue(
            $"provider '{provider.Name}' is unavailable ({provider.SkipReason}); the D3 grouping arms need a live SQL Server");

        return provider.CreateContext();
    }

    /// <summary>
    /// Runs one theory body on the named engine: the shared PostgreSQL suite context, or a dedicated
    /// SQL Server context disposed with the body.
    /// </summary>
    private void OnEngine(string engine, Action<IDataContext> body)
    {
        if (string.Equals(engine, PostgresEngine, StringComparison.Ordinal))
        {
            body(Context);
            return;
        }

        using var context = CreateSqlServerContext();
        body(context);
    }

    private static void Execute(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Distinguishing arm: <c>GROUP BY CUBE</c> over one and over two columns. The one-column CUBE
    /// returns the four real groups (including the NULL-keyed one) plus the grand total; the
    /// two-column CUBE returns the four pair rows, the four per-group rows, the three per-tag rows and
    /// the grand total, whose counts sum to four times the six seeded rows.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines))]
    public void GroupByCube_ShouldReturnEveryGroupingCombinationAndTheGrandTotal(string engine)
        => OnEngine(engine, context =>
        {
            var oneColumn = context.From<C8gMarker>()
                .GroupByCube(x => new { x.Grp })
                .Select(g => SqlFunctions.Sql.count())
                .ToList();

            oneColumn.Should().BeEquivalentTo(
                new[] { 2, 2, 1, 1, 6 },
                "groups 1 and 2 hold two rows, group 3 and the NULL group one row each, and the grand total is six");

            var twoColumns = context.From<C8gMarker>()
                .GroupByCube(x => new { x.Grp, x.Tag })
                .Select(g => SqlFunctions.Sql.count())
                .ToList();

            twoColumns.Should().HaveCount(12, "a two-column CUBE yields (grp, tag), (grp), (tag) and () over the seed");
            twoColumns.Should().BeEquivalentTo(
                new[] { 2, 2, 1, 1, 2, 2, 1, 1, 2, 2, 2, 6 },
                "the pair, group, tag and grand-total rows of a two-column CUBE over six seeded rows");
            twoColumns.Sum().Should().Be(24, "every seeded row is counted once per grouping set");
        });

    /// <summary>
    /// Distinguishing arm: <c>GROUP BY GROUPING SETS ((grp), ())</c>. The real NULL-keyed group and
    /// the empty grouping set both surface a NULL <c>grp</c>, so the row counts — one versus six — are
    /// what proves the subtotal is distinct from a genuine NULL group. The second query adds the
    /// multiple-set form over two grouping columns.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines))]
    public void GroupByGroupingSets_ShouldSeparateTheRealNullGroupFromTheGrandTotal(string engine)
        => OnEngine(engine, context =>
        {
            var subtotalVersusNull = context.From<C8gMarker>()
                .GroupByGroupingSets(x => new { x.Grp }, [0], [])
                .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() })
                .ToList();

            subtotalVersusNull.Should().HaveCount(5, "four (grp) rows plus one () grand-total row");
            subtotalVersusNull.Where(r => r.Grp is null).Select(r => r.Count).Should().BeEquivalentTo(
                new[] { 1, 6 },
                "the real NULL group counts one row while the empty grouping set is the six-row grand total");
            subtotalVersusNull.Where(r => r.Grp is not null).Select(r => r.Count).Should().BeEquivalentTo(
                new[] { 2, 2, 1 },
                "groups 1 and 2 hold two rows each and group 3 holds one");

            var twoSets = context.From<C8gMarker>()
                .GroupByGroupingSets(x => new { x.Grp, x.Tag }, [0], [1])
                .Select(g => SqlFunctions.Sql.count())
                .ToList();

            twoSets.Should().HaveCount(7, "four (grp) rows plus three (tag) rows, with no grand total");
            twoSets.Should().BeEquivalentTo(
                new[] { 2, 2, 1, 1, 2, 2, 2 },
                "the per-group counts (2, 2, 1, 1) and the per-tag counts (2, 2, 2)");
        });

    /// <summary>
    /// Distinguishing arm: the same grouping-set shape over an empty input. PostgreSQL keeps the empty
    /// grouping set as exactly one row whose <c>COUNT(*)</c> is zero — a grand total is not derived from
    /// the input rows. SQL Server folds the empty grouping set away when the input is empty (raw probe
    /// on the same container: <c>select count(*) from c8g_marker where id &lt; 0 group by grouping sets
    /// ((grp), ())</c> returns no row), so the engine difference is asserted instead of being smoothed
    /// over. Either way the grouping-set renderer executes live on both engines.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines))]
    public void GroupByGroupingSets_OnAnEmptyInput_ShouldKeepTheZeroGrandTotal(string engine)
        => OnEngine(engine, context =>
        {
            var emptyInput = context.From<C8gMarker>()
                .Where(x => x.Id < 0)
                .GroupByGroupingSets(x => new { x.Grp }, [0], [])
                .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() })
                .ToList();

            if (string.Equals(engine, PostgresEngine, StringComparison.Ordinal))
            {
                emptyInput.Should().ContainSingle("PostgreSQL keeps the empty grouping set as one grand-total row over an empty input");
                emptyInput[0].Grp.Should().BeNull("the grand-total row has no grouping key");
                emptyInput[0].Count.Should().Be(0, "COUNT(*) over an empty input is zero, not a missing row");
            }
            else
            {
                emptyInput.Should().BeEmpty("SQL Server emits no grand-total row for grouping sets over an empty input");
            }
        });

    /// <summary>
    /// Distinguishing arm: the grouping-set index guard of <c>SqlBuilder.MakeSelect</c> (:294-295).
    /// An index at the declared-list length and a negative index exercise both operands of the range
    /// test; each throws <see cref="BuildSqlCommandException"/> before any SQL reaches the server.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines))]
    public void GroupByGroupingSets_OutOfRangeIndex_ShouldThrowBeforeExecution(string engine)
        => OnEngine(engine, context =>
        {
            FluentActions.Invoking(() => context.From<C8gMarker>()
                    .GroupByGroupingSets(x => new { x.Grp, x.Tag }, new[] { new[] { 0 }, new[] { 5 } })
                    .Select(g => SqlFunctions.Sql.count())
                    .ToList())
                .Should().Throw<BuildSqlCommandException>()
                .WithMessage("Grouping set index 5 is out of range 0..1.");

            FluentActions.Invoking(() => context.From<C8gMarker>()
                    .GroupByGroupingSets(x => new { x.Grp, x.Tag }, new[] { -1 })
                    .Select(g => SqlFunctions.Sql.count())
                    .ToList())
                .Should().Throw<BuildSqlCommandException>()
                .WithMessage("Grouping set index -1 is out of range 0..1.");
        });

    /// <summary>
    /// Distinguishing arm: the parameter pass of the portable extreme-row outer statement
    /// (<c>SqlBuilder.cs:1351-1367</c>) on a warmed plan. The <c>All</c> ties form keeps the portable
    /// ranking, and the sort keeps <c>ParamRefreshRecipe</c> at <c>null</c>, so the second execution of
    /// the same shape hits the plan cache and re-renders in parameter mode through
    /// <c>QueryPlanner.ExtractParams</c>. The max shape projects the whole entity (the select-list walk
    /// is skipped) and sorts by an expression; the min shape projects explicitly (the select-list walk
    /// runs) and sorts by ordinal.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines))]
    public void ExtremeRowAll_OnAWarmedPlan_ShouldReRenderTheOuterStatementInParameterMode(string engine)
        => OnEngine(engine, context =>
        {
            ResetCachedPathCounters();
            ((DataContext)context).PurgeQueryCache();

            List<int> MaxIds(int floor)
            {
                var threshold = floor;
                return context.From<C8gMarker>()
                    .Where(x => x.Num >= threshold)
                    .SelectWhereMax(x => x.Num, ExtremeRowTies.All)
                    .OrderBy(x => x.Id)
                    .ToList()
                    .Select(row => row.Id)
                    .ToList();
            }

            MaxIds(0).Should().Equal(new[] { 3, 4 }, "rows 3 and 4 tie at the maximum 20");
            QueryPlanner.FallbackRefreshes.Should().Be(0, "the cold render runs in SQL mode and enters no refresh path");

            MaxIds(21).Should().BeEmpty(
                "the cache hit re-binds the changed capture: no row reaches the maximum when the threshold excludes the maximum 20");
            QueryPlanner.FallbackRefreshes.Should().Be(
                1,
                "the cache hit refreshes through ExtractParams, the only parameter-mode render root (SqlBuilder.cs:1351-1367)");
            ParamRefreshRecipe.FastBindHits.Should().Be(0, "a sorted extreme-row shape has no ParamRefreshRecipe");

            ResetCachedPathCounters();
            ((DataContext)context).PurgeQueryCache();

            List<int> MinIds(int ceiling)
            {
                var limit = ceiling;
                return context.From<C8gMarker>()
                    .Where(x => x.Num <= limit)
                    .SelectWhereMin(x => x.Num, x => new { x.Id, x.Num }, ExtremeRowTies.All)
                    .OrderBy(1, OrderDirection.Asc)
                    .ToList()
                    .Select(row => row.Id)
                    .ToList();
            }

            MinIds(100).Should().Equal(new[] { 1, 2 }, "rows 1 and 2 tie at the minimum 10");
            MinIds(9).Should().BeEmpty(
                "the cache hit re-binds the second capture: nothing is below the minimum 10");
            QueryPlanner.FallbackRefreshes.Should().Be(
                1,
                "the explicit-projection variant re-renders the select list in the same parameter pass");
            ParamRefreshRecipe.FastBindHits.Should().Be(0, "the ordinal sort also keeps the fast recipe off");
        });

    private static void ResetCachedPathCounters()
    {
        QueryPlanner.ResetCounters();
        ParamRefreshRecipe.ResetCounters();
    }

    /// <summary>Class-owned fixture: one physical table per engine carrying the same six rows.</summary>
    [SqlTable("c8g_marker")]
    public sealed class C8gMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }

        [Column("tag")]
        public int? Tag { get; set; }

        [Column("num")]
        public int? Num { get; set; }
    }
}
