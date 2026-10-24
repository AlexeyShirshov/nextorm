using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Plan-key branch coverage (cycle 5, cluster C1/D1) executed against a live SQLite database. Every
/// fact builds two DISTINCT commands over the class-owned <c>pk5_*</c> tables, prepares both through the
/// cached path (the prepared state is what <c>QueryPlanEqualityComparer.EqualsCore</c> reads), compares
/// them with the public <see cref="QueryCommand.GetQueryPlanEqualityComparer"/>, and executes the same
/// shapes so a wrong cache reuse would surface as a wrong row set. No <see cref="InMemoryDataContext"/>,
/// no reflection, no global seed rows.
///
/// <para>
/// Targeted arms: <c>QueryPlanEqualityComparer.EqualsCore</c>
/// (<c>src/nextorm.core/Query/QueryPlanEqualityComparer.cs:67-232</c>) reached field by field on the
/// equal path, then with exactly one differing component per pair (ResultType, IsDistinct,
/// Paging.Limit/Offset, SingleRow, UnionType, From, SelectList, PreparedCondition,
/// InValuesShapeHash, Joins, Sorting, GroupingList, Having, Tag, ResolvedQuoteIdentifiers,
/// ResolvedNamingConvention, ResolvedKeywordCase, ResolvedCommandTimeout); and
/// <c>WindowsEqual</c> :249-284 with the six-way frame cluster :274 (type, start kind, start offset,
/// end kind, end offset, exclusion), the present/absent frame, partition and order counts, order
/// direction and the window name.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>PlanEqualityDifferentialBranchCoverage4Tests</c>
/// owns the expression-comparer member/null/text facts, the plan-key index-hint string lists and the
/// grouping-set comparer; <c>PlanKeyBranchCoverage3Tests</c> owns projection nodes, tag presence, paging
/// limit, distinct and eager-mode keys; <c>PreparerBatteryBranchCoverage4Tests</c> owns the
/// translation-only <c>WithTies</c> flag (SQLite rejects <c>WITH TIES</c> at generation, so that arm is
/// recorded in <c>rv1/inputs/deferred.json</c>, never asserted here).
/// </para>
/// </summary>
[Collection("Query plan cache")]
public sealed class SqlitePlanKeyBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists pk5_line;
        drop table if exists pk5_marker_copy;
        drop table if exists pk5_marker;
        create table pk5_marker
        (
            id integer primary key,
            name text not null,
            code text not null,
            grp int not null,
            tag int not null,
            score int null
        );
        create table pk5_marker_copy
        (
            id integer primary key,
            name text not null,
            code text not null,
            grp int not null,
            tag int not null,
            score int null
        );
        create table pk5_line
        (
            id integer primary key,
            marker_id int not null,
            qty int not null
        );
        insert into pk5_marker (id, name, code, grp, tag, score) values
            (1, 'alpha', 'a', 1, 10, 5),
            (2, 'beta',  'b', 1, 20, 5),
            (3, 'gamma', 'a', 2, 10, 9),
            (4, 'delta', 'b', 2, 20, null);
        insert into pk5_marker_copy select * from pk5_marker;
        insert into pk5_line (id, marker_id, qty) values
            (10, 1, 1),
            (11, 1, 3),
            (12, 2, 5),
            (13, 3, 7);
        create index pk5_grp_idx on pk5_marker (grp);
        create index pk5_tag_idx on pk5_marker (tag);
        """;

    public SqlitePlanKeyBranchCoverage5Tests() => ExecuteRaw(Ddl);

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    /// <summary>Drops every cached plan on this context.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    // ---------------------------------------------------------------------------------------------
    // 1. Scalar shape flags: equal prefix, then ResultType / IsDistinct / paging / SingleRow
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void ScalarFlags_EqualPrefixAndOneFieldNegatives_ShouldKeySeparately()
    {
        ResetPlanCache();

        var equalA = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Select(x => x.Id);
        var equalB = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Select(x => x.Id);

        Rows(Prepare(equalA)).Should().Equal(1, 2, 3, 4);
        Prepare(equalB).Should().BeSameAs(Prepare(equalA), "an identical shape reuses one cached plan");

        var comparer = equalA.GetQueryPlanEqualityComparer();
        equalA.Should().NotBeSameAs(equalB, "the equality below is structural, not a reference match");
        comparer.Equals(equalA, equalB).Should().BeTrue("every compared field of the equal prefix matches");
        comparer.GetHashCode(equalA).Should().Be(comparer.GetHashCode(equalB));

        var asInt = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Select(x => x.Id);
        var asLong = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Select(x => (double)x.Id);

        Rows(Prepare(asInt)).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(asLong)).Should().Equal(1.0, 2.0, 3.0, 4.0);
        comparer.Equals(asInt, asLong).Should().BeFalse("a different projected CLR type is a different plan key");

        var plainGroup = Context.From<Pk5Marker>().OrderBy(x => x.Grp).Select(x => x.Grp);
        var distinctGroup = Context.From<Pk5Marker>().OrderBy(x => x.Grp).Select(x => x.Grp).Distinct();

        Rows(Prepare(plainGroup)).Should().Equal(1, 1, 2, 2);
        Rows(Prepare(distinctGroup)).Should().Equal(1, 2);
        comparer.Equals(plainGroup, distinctGroup).Should().BeFalse("the DISTINCT flag is part of the plan key");

        var pageTwo = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Limit(2).Select(x => x.Id);
        var pageThree = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Limit(3).Select(x => x.Id);

        Rows(Prepare(pageTwo)).Should().Equal(1, 2);
        Rows(Prepare(pageThree)).Should().Equal(1, 2, 3);
        comparer.Equals(pageTwo, pageThree).Should().BeFalse("a different page limit is a different plan key");

        var fromStart = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Page(3, 0).Select(x => x.Id);
        var skipped = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Page(3, 1).Select(x => x.Id);

        Rows(Prepare(fromStart)).Should().Equal(1, 2, 3);
        Rows(Prepare(skipped)).Should().Equal(2, 3, 4);
        comparer.Equals(fromStart, skipped).Should().BeFalse("a different page offset is a different plan key");

        var limited = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Limit(1).Select(x => x.Id);
        var singleRow = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Limit(1).Select(x => x.Id);
        singleRow.SingleRow = true;

        Rows(Prepare(limited)).Should().Equal(1);
        Prepare(singleRow).First(Context).Should().Be(1);
        comparer.Equals(limited, singleRow).Should().BeFalse("a single-row terminal flag is part of the plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Compound shape: UnionType and a derived FROM source
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void CompoundFlags_UnionTypeAndDerivedSource_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        QueryCommand<long> Left() => Context.From<Pk5Marker>().Where(x => x.Id <= 2).Select(x => x.Id);
        QueryCommand<long> Lines() => Context.From<Pk5Line>().Where(l => l.Qty > 2).Select(l => l.Id);

        var union = Left().Union(Lines());
        var intersect = Left().Intersect(Lines());

        Rows(Prepare(union)).Should().Equal(new long[] { 1, 2, 11, 12, 13 });
        Rows(Prepare(intersect)).Should().BeEmpty();
        comparer.Equals(union, intersect).Should().BeFalse("a different set operator is a different plan key");

        var physical = Context.From<Pk5Marker>().Where(x => x.Id > 0).OrderBy(x => x.Id).Select(x => x.Id);
        var derived = Context.From(Context.From<Pk5Marker>().Where(x => x.Id > 1))
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Rows(Prepare(physical)).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(derived)).Should().Equal(2, 3, 4);
        comparer.Equals(physical, derived).Should()
            .BeFalse("a physical table source and a derived subquery source are different plan keys");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Select list, condition, value-list shape and sorting
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void ProjectionConditionAndSorting_OneFieldNegatives_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        var names = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Name);
        var codes = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Code);

        Rows(Prepare(names)).Should().Equal("alpha", "beta", "gamma", "delta");
        Rows(Prepare(codes)).Should().Equal("a", "b", "a", "b");
        comparer.Equals(names, codes).Should().BeFalse("a different selected column is a different plan key");

        var inGroupOne = Context.From<Pk5Marker>().Where(x => x.Grp == 1).OrderBy(x => x.Id).Select(x => x.Id);
        var inGroupTwo = Context.From<Pk5Marker>().Where(x => x.Grp == 2).OrderBy(x => x.Id).Select(x => x.Id);

        Rows(Prepare(inGroupOne)).Should().Equal(1, 2);
        Rows(Prepare(inGroupTwo)).Should().Equal(3, 4);
        comparer.Equals(inGroupOne, inGroupTwo).Should().BeFalse("a different WHERE constant is a different plan key");

        // The same closure field is read by both commands; only its evaluated length (and therefore the
        // value-list shape hash) differs. The condition expression itself is shape-identical.
        var values = new List<long> { 1, 2 };
        var twoValues = Context.From<Pk5Marker>()
            .Where(x => values.Contains((long)x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        Rows(Prepare(twoValues)).Should().Equal(1, 2);

        values.Add(3);
        var threeValues = Context.From<Pk5Marker>()
            .Where(x => values.Contains((long)x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        Rows(Prepare(threeValues)).Should().Equal(1, 2, 3);

        comparer.Equals(twoValues, threeValues).Should()
            .BeFalse("the evaluated value-list shape hash is compared explicitly because the closure access is shape independent");

        var ascending = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id);
        var descending = Context.From<Pk5Marker>().OrderByDescending(x => x.Id).Select(x => x.Id);

        Rows(Prepare(ascending)).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(descending)).Should().Equal(4, 3, 2, 1);
        comparer.Equals(ascending, descending).Should().BeFalse("a different sort direction is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Join shape, grouping list and HAVING
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void JoinGroupingAndHaving_OneFieldNegatives_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        var inner = Context.From<Pk5Marker>()
            .Join(Context.From<Pk5Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(x => x.Item1.Id)
            .Select(x => new { Marker = x.Item1.Id, Line = (long?)x.Item2.Id });
        var left = Context.From<Pk5Marker>()
            .LeftJoin(Context.From<Pk5Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(x => x.Item1.Id)
            .Select(x => new { Marker = x.Item1.Id, Line = (long?)x.Item2.Id });

        Rows(Prepare(inner)).Should().HaveCount(4);
        var leftRows = Rows(Prepare(left));
        leftRows.Should().HaveCount(5, "the unjoined marker row survives a left join with a NULL line id");
        leftRows.Should().ContainSingle(r => r.Marker == 4 && r.Line == null);
        comparer.Equals(inner, left).Should().BeFalse("a different join type is a different plan key");

        var byGroup = Context.From<Pk5Marker>()
            .GroupBy(x => new { x.Grp })
            .Select(e => SqlFunctions.Sql.sum(e.Id));
        var byTag = Context.From<Pk5Marker>()
            .GroupBy(x => new { x.Tag })
            .Select(e => SqlFunctions.Sql.sum(e.Id));

        Rows(Prepare(byGroup)).Should().BeEquivalentTo(new long[] { 3, 7 });
        Rows(Prepare(byTag)).Should().BeEquivalentTo(new long[] { 4, 6 });
        comparer.Equals(byGroup, byTag).Should().BeFalse("a different grouping list is a different plan key");

        var havingOne = Context.From<Pk5Marker>()
            .GroupBy(x => new { x.Grp })
            .Having(e => SqlFunctions.Sql.count() > 1)
            .Select(e => SqlFunctions.Sql.sum(e.Id));
        var havingTwo = Context.From<Pk5Marker>()
            .GroupBy(x => new { x.Grp })
            .Having(e => SqlFunctions.Sql.count() > 2)
            .Select(e => SqlFunctions.Sql.sum(e.Id));

        Rows(Prepare(havingOne)).Should().BeEquivalentTo(new long[] { 3, 7 });
        Rows(Prepare(havingTwo)).Should().BeEmpty();
        comparer.Equals(havingOne, havingTwo).Should().BeFalse("a different HAVING predicate is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Per-query rendering overrides (no row difference; both sides still execute live)
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void ContextOverrides_OneFieldNegatives_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        var tagA = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithTag("pk5-a");
        var tagB = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithTag("pk5-b");
        Rows(Prepare(tagA)).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(tagB)).Should().Equal(1, 2, 3, 4);
        comparer.Equals(tagA, tagB).Should().BeFalse("the tag text is part of the plan key");

        var quoted = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithQuotedIdentifiers(true);
        var unquoted = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithQuotedIdentifiers(false);
        Rows(Prepare(quoted)).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(unquoted)).Should().Equal(1, 2, 3, 4);
        comparer.Equals(quoted, unquoted).Should().BeFalse("the resolved identifier-quoting flag is part of the plan key");

        var snake = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id)
            .WithNamingConvention(SnakeCaseNamingConvention.Instance);
        var inherited = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id)
            .WithNamingConvention(null);
        Rows(Prepare(snake)).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(inherited)).Should().Equal(1, 2, 3, 4);
        comparer.Equals(snake, inherited).Should().BeFalse("the resolved naming convention is part of the plan key");

        var upper = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithKeywordCase(KeywordCase.Upper);
        var lower = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithKeywordCase(KeywordCase.Lower);
        Rows(Prepare(upper)).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(lower)).Should().Equal(1, 2, 3, 4);
        comparer.Equals(upper, lower).Should().BeFalse("the resolved keyword case is part of the plan key");

        var timeout = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithCommandTimeout(7);
        var noTimeout = Context.From<Pk5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithCommandTimeout(0);
        Rows(Prepare(timeout)).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(noTimeout)).Should().Equal(1, 2, 3, 4);
        comparer.Equals(timeout, noTimeout).Should().BeFalse("the resolved command timeout is part of the plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Window frames: unit, start kind/offset, end kind/offset
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void WindowFrames_UnitAndBoundaries_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        var rowsRunningFrame = WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow);

        var rowsRunning = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Grp), rowsRunningFrame)
            });
        var rangeRunning = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Grp),
                    WindowFrame.Range(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            });

        Rows(Prepare(rowsRunning)).Select(r => r.Running).Should().Equal(1L, 3L, 6L, 10L);
        Rows(Prepare(rangeRunning)).Select(r => r.Running).Should()
            .Equal(new long[] { 3L, 3L, 10L, 10L }, "RANGE includes the whole peer group of the current row");
        comparer.Equals(rowsRunning, rangeRunning).Should().BeFalse("a different frame unit is a different plan key");

        var startOne = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow))
            });
        var startTwo = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow))
            });

        Rows(Prepare(startOne)).Select(r => r.Running).Should().Equal(1L, 3L, 5L, 7L);
        Rows(Prepare(startTwo)).Select(r => r.Running).Should().Equal(1L, 3L, 6L, 9L);
        comparer.Equals(startOne, startTwo).Should().BeFalse("a different start offset is a different plan key");

        var startUnbounded = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            });
        comparer.Equals(startUnbounded, startOne).Should().BeFalse("a different start boundary kind is a different plan key");
        Rows(Prepare(startUnbounded)).Select(r => r.Running).Should().Equal(1L, 3L, 6L, 10L);

        var endCurrent = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            });
        var endFollowingOne = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.Following(1)))
            });
        var endFollowingTwo = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.Following(2)))
            });

        Rows(Prepare(endFollowingOne)).Select(r => r.Running).Should().Equal(3L, 6L, 10L, 10L);
        Rows(Prepare(endFollowingTwo)).Select(r => r.Running).Should().Equal(6L, 10L, 10L, 10L);
        comparer.Equals(endFollowingOne, endFollowingTwo).Should()
            .BeFalse("a different end offset is a different plan key");

        var endUnbounded = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.UnboundedFollowing))
            });

        Rows(Prepare(endUnbounded)).Select(r => r.Running).Should().Equal(10L, 10L, 10L, 10L);
        comparer.Equals(endCurrent, endUnbounded).Should().BeFalse("a different end boundary kind is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Window frame exclusion and the present/absent frame
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void WindowFrames_ExclusionAndPresence_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        // count_over is 0 (never NULL) on an empty frame, so the exclusion rows stay readable as an int.
        var noOthers = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.count_over().Over(
                    SqlFunctions.Sql.asc(() => x.Grp),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            });
        var currentRow = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.count_over().Over(
                    SqlFunctions.Sql.asc(() => x.Grp),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)
                        .WithExclusion(WindowFrameExclusion.CurrentRow))
            });

        Rows(Prepare(noOthers)).Select(r => r.Running).Should().Equal(1, 2, 3, 4);
        Rows(Prepare(currentRow)).Select(r => r.Running).Should()
            .Equal(new[] { 0, 1, 2, 3 }, "EXCLUDE CURRENT ROW removes the current row from the running frame");
        comparer.Equals(noOthers, currentRow).Should().BeFalse("a frame exclusion is part of the plan key");

        var explicitNoOthers = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.count_over().Over(
                    SqlFunctions.Sql.asc(() => x.Grp),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)
                        .WithExclusion(WindowFrameExclusion.NoOthers))
            });
        Rows(Prepare(explicitNoOthers)).Select(r => r.Running).Should().Equal(1, 2, 3, 4);
        comparer.Equals(noOthers, explicitNoOthers).Should()
            .BeFalse("an absent exclusion and the explicit EXCLUDE NO OTHERS frame compare differently");

        var excludeGroup = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.count_over().Over(
                    SqlFunctions.Sql.asc(() => x.Grp),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)
                        .WithExclusion(WindowFrameExclusion.Group))
            });
        var excludeTies = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.count_over().Over(
                    SqlFunctions.Sql.asc(() => x.Grp),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)
                        .WithExclusion(WindowFrameExclusion.Ties))
            });

        Rows(Prepare(excludeGroup)).Select(r => r.Running).Should().Equal(0, 0, 2, 2);
        Rows(Prepare(excludeTies)).Select(r => r.Running).Should().Equal(1, 1, 3, 3);
        comparer.Equals(currentRow, excludeGroup).Should().BeFalse("each exclusion value is a different frame");
        comparer.Equals(currentRow, excludeTies).Should().BeFalse("each exclusion value is a different frame");

        var frameAbsent = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(SqlFunctions.Sql.asc(() => x.Grp))
            });
        var framePresent = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Grp),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            });

        Rows(Prepare(frameAbsent)).Select(r => r.Running).Should()
            .Equal(new long[] { 3L, 3L, 10L, 10L }, "the default frame is RANGE UNBOUNDED PRECEDING TO CURRENT ROW");
        comparer.Equals(frameAbsent, framePresent).Should()
            .BeFalse("a frame present on one side only is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Window partitions, order keys, independent equal pair and window name
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void WindowFrames_PartitionOrderAndName_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        var runningFrame = WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow);

        var partitioned = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    new Expression<Func<object?>>[] { () => x.Grp },
                    new[] { SqlFunctions.Sql.asc(() => x.Id) },
                    runningFrame)
            });
        var unpartitioned = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    (Expression<Func<object?>>[]?)null,
                    new[] { SqlFunctions.Sql.asc(() => x.Id) },
                    runningFrame)
            });

        Rows(Prepare(partitioned)).Select(r => r.Running).Should().Equal(1L, 3L, 3L, 7L);
        Rows(Prepare(unpartitioned)).Select(r => r.Running).Should().Equal(1L, 3L, 6L, 10L);
        comparer.Equals(partitioned, unpartitioned).Should()
            .BeFalse("a different partition count is a different window definition");

        var oneOrder = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    new[] { SqlFunctions.Sql.asc(() => x.Grp) },
                    runningFrame)
            });
        var twoOrders = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    new[] { SqlFunctions.Sql.asc(() => x.Grp), SqlFunctions.Sql.asc(() => x.Id) },
                    runningFrame)
            });

        Rows(Prepare(oneOrder)).Select(r => r.Running).Should().Equal(1L, 3L, 6L, 10L);
        Rows(Prepare(twoOrders)).Select(r => r.Running).Should().Equal(1L, 3L, 6L, 10L);
        comparer.Equals(oneOrder, twoOrders).Should().BeFalse("a different order-key count is a different window definition");

        var ascending = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    new[] { SqlFunctions.Sql.asc(() => x.Grp) },
                    runningFrame)
            });
        var descending = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    new[] { SqlFunctions.Sql.desc(() => x.Grp) },
                    runningFrame)
            });

        Rows(Prepare(descending)).Select(r => r.Running).Should().Equal(10L, 9L, 7L, 4L);
        comparer.Equals(ascending, descending).Should().BeFalse("a different window order direction is a different key");

        var equalA = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    new Expression<Func<object?>>[] { () => x.Grp },
                    new[] { SqlFunctions.Sql.asc(() => x.Id) },
                    runningFrame)
            });
        var equalB = Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Running = SqlFunctions.Sql.sum_over(x.Id).Over(
                    new Expression<Func<object?>>[] { () => x.Grp },
                    new[] { SqlFunctions.Sql.asc(() => x.Id) },
                    runningFrame)
            });

        Rows(Prepare(equalA)).Select(r => r.Running).Should().Equal(1L, 3L, 3L, 7L);
        Prepare(equalB).Should().BeSameAs(Prepare(equalA), "an independently built equal framed window reuses one plan");
        comparer.Equals(equalA, equalB).Should()
            .BeTrue("every window component (name, partition, order, frame) matches");

        var seed = Context.From<Pk5Marker>();
        var namedA = seed
            .Window(
                "pk5w_a",
                partitionBy: new Expression<Func<Pk5Marker, object?>>[] { x => x.Grp },
                orderBy: new[] { seed.Asc(x => x.Id) },
                frame: runningFrame)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Running = SqlFunctions.Sql.sum_over(x.Id).Over("pk5w_a") });
        var seedB = Context.From<Pk5Marker>();
        var namedB = seedB
            .Window(
                "pk5w_b",
                partitionBy: new Expression<Func<Pk5Marker, object?>>[] { x => x.Grp },
                orderBy: new[] { seedB.Asc(x => x.Id) },
                frame: runningFrame)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Running = SqlFunctions.Sql.sum_over(x.Id).Over("pk5w_b") });

        Rows(Prepare(namedA)).Select(r => r.Running).Should()
            .Equal(new long[] { 1L, 3L, 3L, 7L }, "the declared window partitions by Grp, so each partition restarts its running sum");
        Rows(Prepare(namedB)).Select(r => r.Running).Should()
            .Equal(new long[] { 1L, 3L, 3L, 7L }, "the second named window reads the same partitioned rows");
        comparer.Equals(namedA, namedB).Should().BeFalse("the declared window name is part of the window definition");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. Extreme-row clause: present/absent, kind, ties, value selector, group-by and projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EqualsCore</c> <c>ExtremeRow</c> :98-113 reached through the public
    /// <c>SelectWhereMax</c>/<c>SelectWhereMin</c> builders. The equal pair compares TRUE and shares
    /// one cached plan; the clause present on one side only and every one-field difference (kind,
    /// ties, value selector, group-by, projection) compare FALSE, and each side executes its own
    /// live rows.
    /// </summary>
    [Fact]
    public void ExtremeRow_PresenceKindTiesSelectorGroupingAndProjection_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        QueryCommand<Pk5Marker> MaxByScore(ExtremeRowTies ties)
            => Context.From<Pk5Marker>().SelectWhereMax(x => x.Score, ties).ToCommand();

        var maxAll = MaxByScore(ExtremeRowTies.All);
        var maxAllAgain = MaxByScore(ExtremeRowTies.All);

        Rows(Prepare(maxAll)).Select(r => r.Id).Should().Equal(new long[] { 3 });
        Prepare(maxAllAgain).Should().BeSameAs(Prepare(maxAll), "an identical extreme-row clause reuses one cached plan");
        comparer.Equals(maxAll, maxAllAgain).Should().BeTrue("every compared extreme-row component matches");

        var plain = Context.From<Pk5Marker>().OrderBy(x => x.Id).ToCommand();
        Rows(Prepare(plain)).Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4 });
        comparer.Equals(maxAll, plain).Should().BeFalse("an extreme-row clause present on one side only is a different plan key");

        var maxOne = MaxByScore(ExtremeRowTies.One);
        Prepare(maxOne).First(Context).Id.Should().Be(3);
        comparer.Equals(maxAll, maxOne).Should().BeFalse("a different ties mode is a different extreme-row plan");

        var minOne = Context.From<Pk5Marker>().SelectWhereMin(x => x.Score).ToCommand();
        Prepare(minOne).First(Context).Id.Should().BeOneOf(new long[] { 1L, 2L }, "both rows carry the minimum score 5");
        comparer.Equals(maxOne, minOne).Should().BeFalse("a different extreme kind is a different extreme-row plan");

        var maxTagAll = Context.From<Pk5Marker>().SelectWhereMax(x => x.Tag, ExtremeRowTies.All).ToCommand();
        Rows(Prepare(maxTagAll)).Select(r => r.Id).OrderBy(id => id).Should()
            .Equal(new long[] { 2, 4 }, "both rows carry the maximum tag 20");
        comparer.Equals(maxAll, maxTagAll).Should().BeFalse("a different value selector is a different extreme-row plan");

        var idProjection = Context.From<Pk5Marker>()
            .SelectWhereMax(x => x.Score, x => new { Value = (long?)x.Id }, ExtremeRowTies.All);
        var tagProjection = Context.From<Pk5Marker>()
            .SelectWhereMax(x => x.Score, x => new { Value = (long?)x.Tag }, ExtremeRowTies.All);

        Rows(Prepare(idProjection)).Select(r => r.Value).Should().Equal(new long?[] { 3 });
        Rows(Prepare(tagProjection)).Select(r => r.Value).Should().Equal(new long?[] { 10 }, "the same winning row reads its tag");
        comparer.Equals(idProjection, tagProjection).Should().BeFalse("a different extreme-row projection is a different plan");

        var ungrouped = Context.From<Pk5Marker>()
            .SelectWhereMax(x => x.Score, x => new { Value = (long?)x.Id }, ExtremeRowTies.All);
        var grouped = Context.From<Pk5Marker>()
            .SelectWhereMax(
                x => x.Score,
                x => new { Value = (long?)x.Id },
                ExtremeRowTies.All,
                x => new { x.Grp });

        Rows(Prepare(ungrouped)).Select(r => r.Value).Should().Equal(new long?[] { 3 });
        Rows(Prepare(grouped)).Select(r => r.Value).OrderBy(v => v).Should()
            .Equal(new long?[] { 1, 2, 3 }, "each group keeps its own maximum-scoring row");
        comparer.Equals(ungrouped, grouped).Should().BeFalse("an extreme-row group-by key is part of the plan");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Correlated projection: referenced-query list and outer-reference targets
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EqualsCore</c> <c>ReferencedQueries</c> :223 and <c>OuterReferences</c>
    /// :229 through a correlated scalar subquery projected next to the outer id. Two independently
    /// built equal commands compare TRUE and reuse one plan. A different inner predicate changes the
    /// referenced query; a different outer column leaves the select list and the joins identical but
    /// changes the outer reference target. Both negatives compare FALSE and read different rows.
    /// </summary>
    [Fact]
    public void CorrelatedProjection_ReferencedQueriesAndOuterReferences_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        QueryCommand<Pk5CorrelatedRow> ById() => Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new Pk5CorrelatedRow(
                x.Id,
                Context.From<Pk5Line>().Where(l => l.MarkerId == x.Id).Count()));

        QueryCommand<Pk5CorrelatedRow> ByGroup() => Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new Pk5CorrelatedRow(
                x.Id,
                Context.From<Pk5Line>().Where(l => l.MarkerId == x.Grp).Count()));

        QueryCommand<Pk5CorrelatedRow> ByIdAboveOne() => Context.From<Pk5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new Pk5CorrelatedRow(
                x.Id,
                Context.From<Pk5Line>().Where(l => l.MarkerId == x.Id && l.Qty > 1).Count()));

        var equalA = ById();
        var equalB = ById();

        Rows(Prepare(equalA)).Select(r => r.Value).Should().Equal(new[] { 2, 1, 1, 0 });
        Prepare(equalB).Should().BeSameAs(Prepare(equalA), "an identical correlated projection reuses one plan");
        comparer.Equals(equalA, equalB).Should().BeTrue("the referenced query and the outer references match");

        var byGroup = ByGroup();
        Rows(Prepare(byGroup)).Select(r => r.Value).Should()
            .Equal(new[] { 2, 2, 1, 1 }, "the same values correlated on the group column match other rows");
        comparer.Equals(equalA, byGroup).Should()
            .BeFalse("a different outer reference column is a different plan key even though the select list is identical");

        var byQty = ByIdAboveOne();
        Rows(Prepare(byQty)).Select(r => r.Value).Should().Equal(new[] { 1, 1, 1, 0 });
        comparer.Equals(equalA, byQty).Should()
            .BeFalse("a different predicate inside the referenced query is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Correlated First/FirstOrDefault: the DefaultOnEmpty materializer flag
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EqualsCore</c> <c>DefaultOnEmpty</c> :175. The flag is set on the
    /// REFERENCED subquery command by the correlated terminal (the only public path: a root command's
    /// First/FirstOrDefault share both SQL and paging), so the comparer reaches it while comparing the
    /// nested commands. First and FirstOrDefault render identical SQL and return the same non-empty
    /// rows, but compare FALSE; on an empty correlated set FirstOrDefault reads the default row while
    /// the two commands can never share the materializer.
    /// </summary>
    [Fact]
    public void CorrelatedFirstAndFirstOrDefault_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk5Marker>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        QueryCommand<Pk5CorrelatedRow> FirstRow() => Context.From<Pk5Marker>()
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => new Pk5CorrelatedRow(
                x.Id,
                Context.From<Pk5Line>().Where(l => l.MarkerId == x.Id).OrderBy(l => l.Id).Select(l => l.Qty).First()));

        QueryCommand<Pk5CorrelatedRow> FirstOrDefaultRow() => Context.From<Pk5Marker>()
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => new Pk5CorrelatedRow(
                x.Id,
                Context.From<Pk5Line>().Where(l => l.MarkerId == x.Id).OrderBy(l => l.Id).Select(l => l.Qty).FirstOrDefault()));

        var equalA = FirstRow();
        var equalB = FirstRow();

        Rows(Prepare(equalA)).Select(r => r.Value).Should().Equal(new[] { 1, 5, 7 });
        Prepare(equalB).Should().BeSameAs(Prepare(equalA), "an identical First terminal reuses one plan");
        comparer.Equals(equalA, equalB).Should().BeTrue("every compared component including DefaultOnEmpty matches");

        var firstOrDefault = FirstOrDefaultRow();
        Rows(Prepare(firstOrDefault)).Select(r => r.Value).Should()
            .Equal(new[] { 1, 5, 7 }, "the same non-empty correlated rows read the same first value");
        comparer.Equals(equalA, firstOrDefault).Should()
            .BeFalse("First and FirstOrDefault share SQL and paging but not the DefaultOnEmpty materializer flag");

        var emptyDefault = Context.From<Pk5Marker>()
            .Where(x => x.Id == 4)
            .Select(x => new Pk5CorrelatedRow(
                x.Id,
                Context.From<Pk5Line>().Where(l => l.MarkerId == x.Id).Select(l => l.Qty).FirstOrDefault()));
        Rows(Prepare(emptyDefault)).Select(r => r.Value).Should()
            .Equal(new[] { 0 }, "the empty correlated set materializes the default value, which the flag selects");
    }

    /// <summary>Named result shape for the correlated-projection facts (a direct select, not a derived source).</summary>
    public sealed record Pk5CorrelatedRow(long Id, int Value);

    [SqlTable("pk5_marker")]
    public sealed class Pk5Marker
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("score")]
        public int? Score { get; set; }
    }

    [SqlTable("pk5_line")]
    public sealed class Pk5Line
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("marker_id")]
        public long MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }
}
