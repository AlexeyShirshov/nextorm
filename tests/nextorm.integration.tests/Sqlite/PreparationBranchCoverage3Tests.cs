using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Preparation / raw-descriptor branch coverage (cycle 3, cluster D5) executed against a live SQLite
/// database. Every fact builds real commands over the class-owned <c>pb3_marker</c> table (created and
/// seeded from raw DDL in the constructor), captures their value-free raw descriptors and drives the
/// differential arms of <c>RawShape.Matches</c> in BOTH directions, then executes the commands and
/// asserts the returned rows: a descriptor that aliased two differently shaped graphs would surface as a
/// wrong row set (or a stale plan) rather than as a silent cache hit.
///
/// <para>
/// Targeted surface (<c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c>):
/// <c>RawShape.Matches</c> 3702-3761 (the equality arms the difference-only facts of
/// <c>RawShapeBranchCoverage2Tests</c> never reach), <c>NestedEqual</c>/<c>CtesEqual</c> 3763-3789
/// (the multi-declaration list, the name pairing and the recursive/max-recursion flags),
/// <c>WindowsEqual</c> 3803-3834 (previously untouched),
/// <c>NamingConventionsEqual</c> 3836-3837, <c>StringListsEqual</c> 3842-3852,
/// <c>CaptureRawShape</c> 3889-4030 (nested-source ineligibility), <c>RefreshInValuesShape</c> 195-281
/// and <c>PrepareWhere</c> 3318-3351 (the no-hash arm).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>RawShapeBranchCoverage2Tests</c> owns the
/// self/null short circuits, the entity/projection/column-list arms, the closure ineligibility, the
/// paging/distinct/sorting/join/tag/grouping DIFFERENCE arms, the untyped CTE body/name arms, the union
/// type and absent-union arms and the ROOT raw-SQL/derived source ineligibility;
/// <c>SqlitePlanCacheShapeCoverageTests</c> owns the plan-key separation and the raw candidate for
/// <c>Id &gt; 5</c>; <c>SqlitePreparationShapeCoverageTests</c> owns the projection/preparation shapes;
/// <c>InPreparationBranchCoverage2Tests</c> owns the instance-<c>Contains</c> family and its own
/// <c>storeInCache:false</c>-then-cached fact; <c>TupleInValuesSweepTests</c> owns the tuple lists.
/// </para>
///
/// <para>
/// Deliberately not asserted: the null-equality and <c>is &lt;type&gt;</c> comparison shapes
/// (filed defects #295/#296) - no fact encodes their current rendering. Unreachable on SQLite (recorded,
/// never fabricated): the <c>CteShape.TypedProjection</c> arm (an <c>AsCte</c> read is a column-shape
/// source, so <c>CaptureRawShape</c> is ineligible for it), the <c>LambdaArraysEqual</c>/<c>ArrayJoins</c>
/// arms (no public ArrayJoin entry
/// point), the <c>Settings</c>/<c>GroupingSets</c> arms (no public setter),
/// <c>SampleRatio</c>/<c>IndexHints</c>/<c>TableHints</c>/<c>ForJson</c>/<c>Temporal</c>/<c>LimitBy</c>
/// (internal-only or non-renderable here) and the <c>PreparedPreWhere</c> arms of
/// <c>RefreshInValuesShape</c> (227-240, 267-278: no public PreWhere setter and
/// <c>SqliteDialect.SupportsPreWhere</c> stays false).
/// </para>
/// </summary>
[Collection("Query plan cache")]
public sealed class PreparationBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists pb3_marker
        (
            id integer primary key,
            score int null,
            grp int null,
            rank_key int not null,
            label text not null
        );
        delete from pb3_marker;
        insert into pb3_marker (id, score, grp, rank_key, label) values
            (1, null, 10,   1, 'one'),
            (2, 5,    10,   1, 'two'),
            (3, 9,    10,   1, 'three'),
            (4, 9,    20,   2, 'four'),
            (5, 3,    20,   2, 'five'),
            (6, 1,    20,   3, 'six'),
            (7, null, null, 3, 'seven'),
            (8, 7,    null, 3, 'eight');
        """;

    public PreparationBranchCoverage3Tests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    /// <summary>Executes an already prepared command against the live database.</summary>
    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    /// <summary>Prepares and executes a command in one step.</summary>
    private List<TResult> Run<TResult>(QueryCommand<TResult> command) => Rows(Prepare(command));

    /// <summary>Drops every cached plan (and the raw candidate index) on this context.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>Captures the value-free raw descriptor of a fresh command, or <see langword="null"/>.</summary>
    private static QueryCommand.RawShape? Shape<TResult>(QueryCommand<TResult> command)
        => QueryCommand.CaptureRawShape(command);

    // ---------------------------------------------------------------------------------------------
    // 1. Sorting: the equality walk of a two-key ordering vs a changed direction
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_SortingEqualityAndDifference_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        var ascending = Context.From<Pb3Marker>()
            .Where(x => x.Id >= 1)
            .OrderBy(x => x.Score)
            .OrderBy(x => x.Label)
            .Select(x => x.Id);
        var ascendingAgain = Context.From<Pb3Marker>()
            .Where(x => x.Id >= 1)
            .OrderBy(x => x.Score)
            .OrderBy(x => x.Label)
            .Select(x => x.Id);
        var descending = Context.From<Pb3Marker>()
            .Where(x => x.Id >= 1)
            .OrderByDescending(x => x.Score)
            .OrderBy(x => x.Label)
            .Select(x => x.Id);

        var ascendingShape = Shape(ascending);
        ascendingShape.Should().NotBeNull("a literal predicate over a plain table has a value-free raw shape");

        Shape(ascendingAgain)!.Matches(ascendingShape!, ascendingAgain).Should()
            .BeTrue("an identical two-key sorting list is the same descriptor");
        Shape(descending)!.Matches(ascendingShape!, descending).Should()
            .BeFalse("a changed sort direction is a different descriptor");

        Run(ascending).Should().Equal(1, 7, 6, 5, 2, 8, 4, 3);
        Run(descending).Should().Equal(4, 3, 8, 2, 5, 6, 1, 7);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Grouping and grouping type: identical pairs
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_GroupingAndGroupingTypeEquality_ShouldMatchIdenticalGrouping()
    {
        ResetPlanCache();

        var byGrp = Context.From<Pb3Marker>()
            .GroupBy(x => x.Grp)
            .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() });
        var byGrpAgain = Context.From<Pb3Marker>()
            .GroupBy(x => x.Grp)
            .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() });
        var cube = Context.From<Pb3Marker>()
            .GroupByCube(x => x.Grp)
            .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() });
        var cubeAgain = Context.From<Pb3Marker>()
            .GroupByCube(x => x.Grp)
            .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() });

        Shape(byGrpAgain)!.Matches(Shape(byGrp)!, byGrpAgain).Should()
            .BeTrue("an identical grouping key is the same descriptor");
        Shape(cubeAgain)!.Matches(Shape(cube)!, cubeAgain).Should()
            .BeTrue("an identical grouping type and grouping set are the same descriptor");
        Shape(cube)!.Matches(Shape(byGrp)!, cube).Should()
            .BeFalse("a different grouping type is a different descriptor");

        var rows = Run(byGrp);
        rows.Should().HaveCount(3);
        rows.Single(r => r.Grp == 10).Count.Should().Be(3);
        rows.Single(r => r.Grp == 20).Count.Should().Be(3);
        rows.Single(r => r.Grp == null).Count.Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. CTE declaration list: identical typed pair, typed-vs-untyped, name pairing
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_TwoCteDeclarations_ShouldPairByNameAndCompareTheRecursiveFlags()
    {
        ResetPlanCache();

        QueryCommand<int> TwoCtes()
        {
            var bodyA = Context.From<Pb3Marker>().Where(c => c.Grp == 10).Select(c => new { c.Id });
            var bodyB = Context.From<Pb3Marker>().Where(c => c.Grp == 20).Select(c => new { c.Id });
            return Context.With("pb3_cte_a", bodyA)
                .With("pb3_cte_b", bodyB)
                .From("pb3_cte_a")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        QueryCommand<int> SwappedBodies()
        {
            var bodyA = Context.From<Pb3Marker>().Where(c => c.Grp == 10).Select(c => new { c.Id });
            var bodyB = Context.From<Pb3Marker>().Where(c => c.Grp == 20).Select(c => new { c.Id });
            return Context.With("pb3_cte_a", bodyB)
                .With("pb3_cte_b", bodyA)
                .From("pb3_cte_a")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        QueryCommand<int> RenamedSecond()
        {
            var bodyA = Context.From<Pb3Marker>().Where(c => c.Grp == 10).Select(c => new { c.Id });
            var bodyB = Context.From<Pb3Marker>().Where(c => c.Grp == 20).Select(c => new { c.Id });
            return Context.With("pb3_cte_a", bodyA)
                .With("pb3_cte_c", bodyB)
                .From("pb3_cte_a")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        QueryCommand<int> RecursiveFirst()
        {
            var bodyA = Context.From<Pb3Marker>().Where(c => c.Grp == 10).Select(c => new { c.Id });
            var bodyB = Context.From<Pb3Marker>().Where(c => c.Grp == 20).Select(c => new { c.Id });
            return Context.WithRecursive("pb3_cte_a", bodyA, maxRecursion: 3)
                .With("pb3_cte_b", bodyB)
                .From("pb3_cte_a")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        var twoCtes = TwoCtes();
        var twoCtesAgain = TwoCtes();
        var swapped = SwappedBodies();
        var renamed = RenamedSecond();
        var recursive = RecursiveFirst();

        Shape(twoCtes).Should().NotBeNull("two plain-table CTE declarations are a describable graph");

        Shape(twoCtesAgain)!.Matches(Shape(twoCtes)!, twoCtesAgain).Should()
            .BeTrue("an identical two-element CTE declaration list is the same descriptor");
        Shape(swapped)!.Matches(Shape(twoCtes)!, swapped).Should()
            .BeFalse("CTE declarations are paired by name: the same names carrying swapped bodies never match");
        Shape(renamed)!.Matches(Shape(twoCtes)!, renamed).Should()
            .BeFalse("an undeclared name in the paired list is a different descriptor");
        Shape(recursive)!.Matches(Shape(twoCtes)!, recursive).Should()
            .BeFalse("a recursive declaration compared with a non-recursive one differs in the recursive arm");

        Run(twoCtes).Should().Equal(1, 2, 3);
        Run(renamed).Should().Equal(1, 2, 3);
        Run(swapped).Should().Equal(4, 5, 6);
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Union: identical pair, nested body difference, and the pure live rows
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_UnionEqualityAndNestedBodyDifference_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        QueryCommand<int> UnionAllWide()
        {
            var left = Context.From<Pb3Marker>().Where(x => x.Id <= 2).Select(x => x.Id);
            var right = Context.From<Pb3Marker>().Where(x => x.Id >= 7).Select(x => x.Id);
            return left.UnionAll(right);
        }

        QueryCommand<int> UnionAllNarrow()
        {
            var left = Context.From<Pb3Marker>().Where(x => x.Id <= 2).Select(x => x.Id);
            var right = Context.From<Pb3Marker>().Where(x => x.Id >= 8).Select(x => x.Id);
            return left.UnionAll(right);
        }

        var wide = UnionAllWide();
        var wideAgain = UnionAllWide();
        var narrow = UnionAllNarrow();

        Shape(wide).Should().NotBeNull("a union of two plain-table branches is describable");

        Shape(wideAgain)!.Matches(Shape(wide)!, wideAgain).Should()
            .BeTrue("an identical union type and nested body is the same descriptor");
        Shape(narrow)!.Matches(Shape(wide)!, narrow).Should()
            .BeFalse("a changed nested union body is a different descriptor");

        Run(wide).OrderBy(x => x).Should().Equal(1, 2, 7, 8);
        Run(narrow).OrderBy(x => x).Should().Equal(1, 2, 8);
    }

    // ---------------------------------------------------------------------------------------------
    // 5. DISTINCT and paging: identical pairs vs changed page
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_DistinctAndPagingEqualityAndDifference_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        var distinct = Context.From<Pb3Marker>().Select(x => x.Score).Distinct();
        var distinctAgain = Context.From<Pb3Marker>().Select(x => x.Score).Distinct();
        var plain = Context.From<Pb3Marker>().Select(x => x.Score);
        var page = Context.From<Pb3Marker>().OrderBy(x => x.Id).Limit(2).Offset(1).Select(x => x.Id);
        var pageAgain = Context.From<Pb3Marker>().OrderBy(x => x.Id).Limit(2).Offset(1).Select(x => x.Id);
        var otherPage = Context.From<Pb3Marker>().OrderBy(x => x.Id).Limit(3).Offset(1).Select(x => x.Id);

        Shape(distinctAgain)!.Matches(Shape(distinct)!, distinctAgain).Should()
            .BeTrue("the same DISTINCT flag is the same descriptor");
        Shape(plain)!.Matches(Shape(distinct)!, plain).Should()
            .BeFalse("the DISTINCT flag is part of the descriptor");
        Shape(pageAgain)!.Matches(Shape(page)!, pageAgain).Should()
            .BeTrue("identical paging is the same descriptor");
        Shape(otherPage)!.Matches(Shape(page)!, otherPage).Should()
            .BeFalse("a changed page limit is a different descriptor");

        var distinctValues = Run(distinct);
        distinctValues.Should().HaveCount(6);
        distinctValues.Should().Contain((int?)null);

        Run(page).Should().Equal(2, 3);
        Run(otherPage).Should().Equal(2, 3, 4);
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Tag text and tables-in-scope hint list: identical pairs
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_TagAndScopeHintEquality_ShouldMatchIdenticalLists()
    {
        ResetPlanCache();

        var plain = Context.From<Pb3Marker>().Where(x => x.Id <= 2).Select(x => x.Id);
        var tagged = Context.From<Pb3Marker>().Where(x => x.Id <= 2).Select(x => x.Id).WithTag("pb3_tag");
        var taggedAgain = Context.From<Pb3Marker>().Where(x => x.Id <= 2).Select(x => x.Id).WithTag("pb3_tag");
        var scoped = Context.From<Pb3Marker>()
            .WithTablesInScopeHint("pb3_marker")
            .Where(x => x.Id <= 2)
            .Select(x => x.Id);
        var scopedAgain = Context.From<Pb3Marker>()
            .WithTablesInScopeHint("pb3_marker")
            .Where(x => x.Id <= 2)
            .Select(x => x.Id);

        Shape(taggedAgain)!.Matches(Shape(tagged)!, taggedAgain).Should()
            .BeTrue("an identical tag text is the same descriptor");
        Shape(tagged)!.Matches(Shape(plain)!, tagged).Should()
            .BeFalse("the tag text is part of the descriptor");
        Shape(scopedAgain)!.Matches(Shape(scoped)!, scopedAgain).Should()
            .BeTrue("an identical tables-in-scope hint list is the same descriptor");
        Shape(scoped)!.Matches(Shape(plain)!, scoped).Should()
            .BeFalse("a tables-in-scope hint list is part of the descriptor");

        Run(plain).Should().Equal(1, 2);
        Run(tagged).Should().Equal(new[] { 1, 2 }, "the tagged query is a live query, not a rejection");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Named windows: the descriptor arm and the partition difference
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_WindowsEqualityAndPartitionDifference_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        var firstBuilder = Context.From<Pb3Marker>();
        var windowed = firstBuilder
            .Window(
                "pb3_w",
                partitionBy: [x => x.Grp],
                orderBy: [firstBuilder.Asc(x => x.Id)],
                frame: WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Row = SqlFunctions.Sql.row_number().Over("pb3_w") });

        var secondBuilder = Context.From<Pb3Marker>();
        var windowedAgain = secondBuilder
            .Window(
                "pb3_w",
                partitionBy: [x => x.Grp],
                orderBy: [secondBuilder.Asc(x => x.Id)],
                frame: WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Row = SqlFunctions.Sql.row_number().Over("pb3_w") });

        var thirdBuilder = Context.From<Pb3Marker>();
        var partitionedById = thirdBuilder
            .Window(
                "pb3_w",
                partitionBy: [x => x.Id],
                orderBy: [thirdBuilder.Asc(x => x.Id)],
                frame: WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Row = SqlFunctions.Sql.row_number().Over("pb3_w") });

        Shape(windowed).Should().NotBeNull("a named window over a plain table is a describable graph");

        Shape(windowedAgain)!.Matches(Shape(windowed)!, windowedAgain).Should()
            .BeTrue("an identical window list is the same descriptor (partition, order and frame all equal)");
        Shape(partitionedById)!.Matches(Shape(windowed)!, partitionedById).Should()
            .BeFalse("a different PARTITION BY key is a different descriptor");

        var windowedRows = Run(windowed);
        windowedRows.Select(r => r.Id).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        windowedRows.Select(r => r.Row).Should().Equal(1, 2, 3, 1, 2, 3, 1, 2);
        Run(partitionedById).Select(r => r.Row).Should().Equal(1, 1, 1, 1, 1, 1, 1, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Window frame difference and two windows declared on one builder
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_WindowFrameDifferenceAndMultipleWindows_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        var rowsBuilder = Context.From<Pb3Marker>();
        var frameRows = rowsBuilder
            .Window(
                "pb3_f",
                partitionBy: [x => x.Grp],
                orderBy: [rowsBuilder.Asc(x => x.Id)],
                frame: WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Row = SqlFunctions.Sql.row_number().Over("pb3_f") });

        var rangeBuilder = Context.From<Pb3Marker>();
        var frameRange = rangeBuilder
            .Window(
                "pb3_f",
                partitionBy: [x => x.Grp],
                orderBy: [rangeBuilder.Asc(x => x.Id)],
                frame: WindowFrame.Range(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Row = SqlFunctions.Sql.row_number().Over("pb3_f") });

        Shape(frameRange)!.Matches(Shape(frameRows)!, frameRange).Should()
            .BeFalse("a changed window frame type is a different descriptor");

        // Two windows on one builder: the second is appended to the existing window list of the copy and
        // declared without a PARTITION BY (the null partition list arm).
        var twoWindowsBuilder = Context.From<Pb3Marker>();
        var twoWindows = twoWindowsBuilder
            .Window("pb3_a", partitionBy: [x => x.Grp], orderBy: [twoWindowsBuilder.Asc(x => x.Id)])
            .Window("pb3_b", orderBy: [twoWindowsBuilder.Desc(x => x.Id)])
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                A = SqlFunctions.Sql.row_number().Over("pb3_a"),
                B = SqlFunctions.Sql.row_number().Over("pb3_b")
            });

        var rows = Run(twoWindows);
        rows.Select(r => r.Id).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        rows.Select(r => r.A).Should().Equal(1, 2, 3, 1, 2, 3, 1, 2);
        rows.Select(r => r.B).Should().Equal(8, 7, 6, 5, 4, 3, 2, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 9. Naming convention: explicit declaration vs the context default
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_NamingConventionEqualityAndDifference_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        var plain = Context.From<Pb3Marker>().Where(x => x.Id >= 3).Select(x => x.Id);
        var snake = Context.From<Pb3Marker>()
            .WithNamingConvention(SnakeCaseNamingConvention.Instance)
            .Where(x => x.Id >= 3)
            .Select(x => x.Id);
        var snakeAgain = Context.From<Pb3Marker>()
            .WithNamingConvention(SnakeCaseNamingConvention.Instance)
            .Where(x => x.Id >= 3)
            .Select(x => x.Id);

        Shape(snakeAgain)!.Matches(Shape(snake)!, snakeAgain).Should()
            .BeTrue("the identical naming convention is the same descriptor");
        Shape(snake)!.Matches(Shape(plain)!, snake).Should()
            .BeFalse("a declared naming convention is part of the descriptor");

        Run(plain).Should().Equal(3, 4, 5, 6, 7, 8);
        Run(snake).Should().Equal(new[] { 3, 4, 5, 6, 7, 8 }, "the snake-case query reads the same rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. CaptureRawShape: nested-source and prepared-command ineligibility
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void CaptureRawShape_IneligibleShapes_ShouldReturnNullWhileTheQueriesExecute()
    {
        ResetPlanCache();

        var prepared = Context.From<Pb3Marker>().Where(x => x.Id >= 2).Select(x => x.Id);
        Run(prepared).Should().Equal(2, 3, 4, 5, 6, 7, 8);
        Shape(prepared).Should().BeNull("only a fresh, never-prepared command has a capturable raw graph");

        var rawRight = Context.FromSql("select id from pb3_marker where id >= 7").Select(t => t["id"].AsInt);
        var unioned = Context.From<Pb3Marker>().Where(x => x.Id <= 2).Select(x => x.Id).UnionAll(rawRight);
        Shape(unioned).Should().BeNull("a nested union branch that is not a plain table source is ineligible");
        Run(unioned).OrderBy(x => x).Should().Equal(1, 2, 7, 8);

        var rawBody = Context.FromSql("select id from pb3_marker where grp = 10").Select(t => t["id"].AsInt);
        var cte = Context.With("pb3_raw_cte", rawBody)
            .From("pb3_raw_cte")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt);
        Shape(cte).Should().BeNull("a CTE body that is not a plain table source is ineligible");
        Run(cte).Should().Equal(1, 2, 3);
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Raw candidate: purge and republish for a string-projected shape
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void QueryPlanStore_PurgeAndRepublish_ShouldCycleTheRawCandidateForAStringProjection()
    {
        ResetPlanCache();

        var labelled = Context.From<Pb3Marker>()
            .Where(x => x.RankKey >= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Label);

        Run(labelled).Should().Equal("four", "five", "six", "seven", "eight");

        var probe = Context.From<Pb3Marker>()
            .Where(x => x.RankKey >= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Label);
        QueryPlanStore.TryGetRaw(Context.GetType(), Shape(probe)!, probe, out _).Should()
            .BeTrue("the prepared plan publishes a live candidate for the identical shape");

        ResetPlanCache();
        QueryPlanStore.TryGetRaw(Context.GetType(), Shape(probe)!, probe, out _).Should()
            .BeFalse("a purge drops the raw candidate index with the plan cache");

        Run(probe).Should().Equal("four", "five", "six", "seven", "eight");

        var republished = Context.From<Pb3Marker>()
            .Where(x => x.RankKey >= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Label);
        QueryPlanStore.TryGetRaw(Context.GetType(), Shape(republished)!, republished, out _).Should()
            .BeTrue("a re-run republishes the candidate after the purge");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. IN values: in-place shape change on one context, and the uncached-then-cached fold
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void InValues_CapturedListShapeChangeAndUncachedPreparation_ShouldRebuildThePlanAndReadLiveRows()
    {
        ResetPlanCache();

        var values = new List<int> { 2 };
        QueryCommand<int> Query() => Context.From<Pb3Marker>()
            .Where(x => SqlFunctions.Sql.@in(x.Id, values))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var command = Query();
        Rows(Prepare(command)).Should().Equal(2);

        values.Add(3);
        values.Add(4);
        Rows(Prepare(command)).Should().Equal(2, 3, 4);

        var grown = Query();
        Rows(Prepare(grown)).Should().Equal(2, 3, 4);

        values.Clear();
        Rows(Prepare(command)).Should().BeEmpty("the empty value list folds to an always-false predicate");

        var stable = new List<int> { 5 };
        QueryCommand<int> StableQuery() => Context.From<Pb3Marker>()
            .Where(x => SqlFunctions.Sql.@in(x.Id, stable))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var firstStable = Prepare(StableQuery());
        Rows(firstStable).Should().Equal(5);

        var secondStable = Prepare(StableQuery());
        secondStable.Should().BeSameAs(firstStable, "an unchanged value-list shape reuses the stored plan");
        Rows(secondStable).Should().Equal(5);

        var uncached = new List<int> { 6, 7 };
        QueryCommand<int> UncachedQuery() => Context.From<Pb3Marker>()
            .Where(x => SqlFunctions.Sql.@in(x.Id, uncached))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var uncachedCommand = UncachedQuery();
        var firstPass = ((DataContext)Context).GetPreparedQueryCommand(
            uncachedCommand,
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);
        Rows(firstPass).Should().Equal(6, 7);

        var secondPass = ((DataContext)Context).GetPreparedQueryCommand(
            uncachedCommand,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);
        Rows(secondPass).Should().Equal(6, 7);
    }

    /// <summary>Marker row of the class-owned <c>pb3_marker</c> table.</summary>
    [SqlTable("pb3_marker")]
    public sealed class Pb3Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The nullable comparison value (nulls are its own value-list element).</summary>
        [Column("score")]
        public int? Score { get; set; }

        /// <summary>The nullable grouping key.</summary>
        [Column("grp")]
        public int? Grp { get; set; }

        /// <summary>A non-nullable secondary key used for literal range predicates.</summary>
        [Column("rank_key")]
        public int RankKey { get; set; }

        /// <summary>The non-nullable text payload.</summary>
        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }
}
