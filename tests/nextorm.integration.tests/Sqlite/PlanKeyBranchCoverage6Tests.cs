using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Plan-key branch coverage (cycle 6, cluster D1) executed against a live SQLite database. Every fact
/// builds two DISTINCT commands over the class-owned <c>pk6_item</c> table, prepares both through the
/// cached path (the prepared state is what <c>QueryPlanEqualityComparer.EqualsCore</c> reads), compares
/// them with the public <see cref="QueryCommand.GetQueryPlanEqualityComparer"/>, and executes both
/// shapes so a wrong cache reuse would surface as a wrong row set. No <see cref="InMemoryDataContext"/>,
/// no reflection, no global seed rows.
///
/// <para>
/// Targeted arms: <c>QueryPlanEqualityComparer.WindowsEqual</c>
/// (<c>src/nextorm.core/Query/QueryPlanEqualityComparer.cs:248</c>) through NAMED windows declared with
/// the public <c>EntityBuilder.Window(name, partitionBy, orderBy, frame)</c> and referenced with
/// <c>Over(name)</c>: the six-way frame cluster :274 (unit, start kind, start offset, end kind, end
/// offset, exclusion), frame presence, window name, window count, partition count and expression, order
/// count/direction/expression. Inline <c>Over(frame)</c> does not populate <c>QueryCommand.Windows</c>,
/// so the cycle-4/5 inline batteries never reached this method's loop body.
/// </para>
///
/// <para>
/// Also targeted: <c>EqualsCore</c> <c>StringListsEqual</c> :234 through the plan-key hint list and the
/// SQLite index-hint list (present/absent, element, length, index-hint kind), <c>CteDefinitionsEqual</c>
/// :336 with <c>CteBodiesEqual</c> :58 (equal pair, body difference, declaration count, recursive flag,
/// max-recursion), and <c>ExpressionPlanEqualityComparer.ExpressionComparer.CompareBinding</c> :571 with
/// <c>Compare2</c> :311 through member-init projections (equal, different member, different assignment
/// expression, different binding count), plus <c>Visitor.VisitIndex</c> :807 through a string-indexer
/// predicate.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>PlanKeyBranchCoverage5Tests</c> owns the
/// inline <c>Over(frame)</c> battery, the scalar/paging/grouping/eager/CTE-body and correlated facts;
/// <c>PlanEqualityDifferentialBranchCoverage4Tests</c> owns the raw-shape window descriptor and the
/// inline frame comparisons; <c>PlanEqualityBranchCoverage2Tests</c> owns the member/null/text
/// expression-comparer facts. This class drives the plan-key list comparers over NAMED windows and the
/// hint, CTE and member-init binding axes only.
/// </para>
/// </summary>
[Collection("Query plan cache")]
public sealed class PlanKeyBranchCoverage6Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists pk6_item;
        create table pk6_item
        (
            id integer primary key,
            name text not null,
            code text not null,
            grp int not null,
            tag int not null,
            score int null
        );
        insert into pk6_item (id, name, code, grp, tag, score) values
            (1, 'alpha',   'a', 1, 10, 5),
            (2, 'beta',    'b', 1, 20, 5),
            (3, 'gamma',   'a', 1, 10, 9),
            (4, 'delta',   'b', 2, 20, 1),
            (5, 'epsilon', 'c', 2, 10, 2),
            (6, 'zeta',    'c', 2, 20, 3);
        create index pk6_grp_idx on pk6_item (grp);
        create index pk6_tag_idx on pk6_item (tag);
        drop table if exists pk6_item_copy;
        create table pk6_item_copy
        (
            id integer primary key,
            name text not null,
            code text not null,
            grp int not null,
            tag int not null,
            score int null
        );
        insert into pk6_item_copy (id, name, code, grp, tag, score) values
            (1, 'alpha',   'a', 1, 10, 5),
            (2, 'beta',    'b', 1, 20, 5),
            (3, 'gamma',   'a', 1, 10, 9),
            (4, 'delta',   'b', 2, 20, 1),
            (5, 'epsilon', 'c', 2, 10, 2),
            (6, 'zeta',    'c', 2, 20, 3);
        drop table if exists pk6_child;
        drop table if exists pk6_parent;
        create table pk6_parent
        (
            id integer primary key,
            name text not null
        );
        create table pk6_child
        (
            id integer primary key,
            parent_id integer not null,
            sort_key integer not null
        );
        insert into pk6_parent (id, name) values
            (9801, 'p-many'),
            (9802, 'p-one');
        insert into pk6_child (id, parent_id, sort_key) values
            (9811, 9801, 1),
            (9812, 9801, 2),
            (9813, 9801, 3),
            (9821, 9802, 1);
        """;

    public PlanKeyBranchCoverage6Tests()
    {
        ExecuteRaw(Ddl);

        // The eager graph must be registered before any mapping of the parent type, so the collection
        // member is recognized as a navigation and not as a physical column.
        Context.From<Pk6Child>();
        Context.From<Pk6Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
    }

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

    /// <summary>
    /// A named window over <c>pk6_item</c> projected through <c>sum_over(id)</c>. Every argument selects
    /// exactly one window component, so a pair of calls can differ in a single compared field.
    /// </summary>
    /// <param name="name">Declared window name (also used after <c>OVER</c>).</param>
    /// <param name="frame">Frame, or <c>null</c> for an absent frame.</param>
    /// <param name="partitions">0 = no partition, 1 = <c>grp</c>, 2 = <c>tag</c>.</param>
    /// <param name="orders">0 = id asc, 1 = id desc, 2 = id+grp, 3 = score asc.</param>
    /// <param name="secondWindow">When true, declares an extra unused window.</param>
    private QueryCommand<long> RunningSum(
        string name = "pk6_w",
        WindowFrame? frame = null,
        int partitions = 1,
        int orders = 0,
        bool secondWindow = false)
    {
        var builder = Context.From<Pk6Item>();

        Expression<Func<Pk6Item, object?>>[]? partitionBy = partitions switch
        {
            1 => new Expression<Func<Pk6Item, object?>>[] { x => x.Grp },
            2 => new Expression<Func<Pk6Item, object?>>[] { x => x.Tag },
            _ => null
        };

        NamedWindowOrderKey[] orderBy = orders switch
        {
            1 => new[] { builder.Desc(x => x.Id) },
            2 => new[] { builder.Asc(x => x.Id), builder.Asc(x => x.Grp) },
            3 => new[] { builder.Asc(x => x.Score) },
            _ => new[] { builder.Asc(x => x.Id) }
        };

        var query = builder.Window(name, partitionBy, orderBy, frame);
        if (secondWindow)
            query = query.Window("pk6_w_extra", partitionBy, orderBy, frame);

        // Over("...") is evaluated while the expression tree is built, so the window name must be an
        // inline literal (a captured local is not recognised by the window parser and renders OVER ()).
        if (name == "pk6_wb")
            return query.OrderBy(x => x.Id).Select(x => SqlFunctions.Sql.sum_over(x.Id).Over("pk6_wb"));

        return query.OrderBy(x => x.Id).Select(x => SqlFunctions.Sql.sum_over(x.Id).Over("pk6_w"));
    }

    private static WindowFrame RowsRunning() =>
        WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow);

    // ---------------------------------------------------------------------------------------------
    // 1. Named-window equal pair: every window component compares TRUE and both sides read rows
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void NamedWindow_EqualFramePair_ShouldSharePlanAndReadRunningSums()
    {
        ResetPlanCache();

        var equalA = RunningSum("pk6_w", RowsRunning(), partitions: 1, orders: 0);
        var equalB = RunningSum("pk6_w", RowsRunning(), partitions: 1, orders: 0);

        var preparedA = Prepare(equalA);
        Prepare(equalB).Should().BeSameAs(preparedA, "an identical named window reuses one cached plan");

        Rows(preparedA).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 },
            "the running sum restarts at each Grp partition");

        var comparer = equalA.GetQueryPlanEqualityComparer();
        equalA.Should().NotBeSameAs(equalB, "the equality below is structural, not a reference match");
        comparer.Equals(equalA, equalB).Should().BeTrue(
            "name, partition, order and all six frame components match, so the whole WindowsEqual body is the equal path");
        comparer.GetHashCode(equalA).Should().Be(comparer.GetHashCode(equalB));
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Frame unit, start kind, start offset
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void NamedWindow_FrameUnitAndStartAxes_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = RunningSum().GetQueryPlanEqualityComparer();

        var rows = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));
        var groups = RunningSum("pk6_w", WindowFrame.Groups(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));

        Rows(Prepare(rows)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(groups)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 },
            "with a unique order key the GROUPS frame covers the same rows as the ROWS frame");
        comparer.Equals(rows, groups).Should().BeFalse("a different frame unit is a different plan key");

        var unboundedStart = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));
        var precedingStart = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow));

        Rows(Prepare(unboundedStart)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(precedingStart)).Should().Equal(new long[] { 1, 3, 5, 4, 9, 11 },
            "ONE PRECEDING drops the first row of each partition from the frame");
        comparer.Equals(unboundedStart, precedingStart).Should().BeFalse("a different start boundary kind is a different plan key");

        var onePreceding = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow));
        var twoPreceding = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow));

        Rows(Prepare(onePreceding)).Should().Equal(new long[] { 1, 3, 5, 4, 9, 11 });
        Rows(Prepare(twoPreceding)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 },
            "TWO PRECEDING reaches the whole three-row partition");
        comparer.Equals(onePreceding, twoPreceding).Should().BeFalse("a different start offset is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Frame end kind, end offset, exclusion
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void NamedWindow_FrameEndAndExclusionAxes_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = RunningSum().GetQueryPlanEqualityComparer();

        var currentEnd = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));
        var followingEnd = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.Following(1)));

        Rows(Prepare(currentEnd)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(followingEnd)).Should().Equal(new long[] { 3, 6, 6, 9, 15, 15 },
            "ONE FOLLOWING adds the next row to every frame");
        comparer.Equals(currentEnd, followingEnd).Should().BeFalse("a different end boundary kind is a different plan key");

        var followingOne = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.Following(1)));
        var followingTwo = RunningSum("pk6_w", WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.Following(2)));

        Rows(Prepare(followingOne)).Should().Equal(new long[] { 3, 6, 6, 9, 15, 15 });
        Rows(Prepare(followingTwo)).Should().Equal(new long[] { 6, 6, 6, 15, 15, 15 },
            "TWO FOLLOWING covers each whole three-row partition");
        comparer.Equals(followingOne, followingTwo).Should().BeFalse("a different end offset is a different plan key");

        QueryCommand<int> RunningCount(string name, WindowFrame frame)
        {
            var builder = Context.From<Pk6Item>();
            return builder
                .Window(name, new Expression<Func<Pk6Item, object?>>[] { x => x.Grp }, new[] { builder.Asc(x => x.Id) }, frame)
                .OrderBy(x => x.Id)
                .Select(x => SqlFunctions.Sql.count_over().Over("pk6_w"));
        }

        var excludeGroup = RunningCount("pk6_w", RowsRunning().WithExclusion(WindowFrameExclusion.Group));
        var excludeTies = RunningCount("pk6_w", RowsRunning().WithExclusion(WindowFrameExclusion.Ties));

        Rows(Prepare(excludeGroup)).Should().Equal(new[] { 0, 1, 2, 0, 1, 2 },
            "EXCLUDE GROUP removes the current row and its peers from the running frame");
        Rows(Prepare(excludeTies)).Should().Equal(new[] { 1, 2, 3, 1, 2, 3 },
            "EXCLUDE TIES keeps the current row; the unique order key has no peers");
        comparer.Equals(excludeGroup, excludeTies).Should().BeFalse("a frame exclusion is part of the plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Frame presence, partition count, partition expression
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void NamedWindow_PresenceAndPartitionAxes_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = RunningSum().GetQueryPlanEqualityComparer();

        var frameAbsent = RunningSum("pk6_w", frame: null, partitions: 1);
        var framePresent = RunningSum("pk6_w", RowsRunning(), partitions: 1);

        Rows(Prepare(frameAbsent)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 },
            "the default frame is RANGE UNBOUNDED PRECEDING TO CURRENT ROW");
        Rows(Prepare(framePresent)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        comparer.Equals(frameAbsent, framePresent).Should().BeFalse("a frame present on one side only is a different plan key");

        var partitioned = RunningSum("pk6_w", RowsRunning(), partitions: 1);
        var unpartitioned = RunningSum("pk6_w", RowsRunning(), partitions: 0);

        Rows(Prepare(partitioned)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(unpartitioned)).Should().Equal(new long[] { 1, 3, 6, 10, 15, 21 },
            "without PARTITION BY the running sum crosses the Grp boundary");
        comparer.Equals(partitioned, unpartitioned).Should().BeFalse("a different partition count is a different window definition");

        var byGroup = RunningSum("pk6_w", RowsRunning(), partitions: 1);
        var byTag = RunningSum("pk6_w", RowsRunning(), partitions: 2);

        Rows(Prepare(byGroup)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(byTag)).Should().Equal(new long[] { 1, 2, 4, 6, 9, 12 },
            "partitioning by Tag produces different running sums from partitioning by Grp");
        comparer.Equals(byGroup, byTag).Should().BeFalse("a different partition expression is a different window definition");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Order count, direction, expression, window name, window count
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void NamedWindow_OrderNameAndCountAxes_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = RunningSum().GetQueryPlanEqualityComparer();

        var oneOrder = RunningSum("pk6_w", RowsRunning(), partitions: 1, orders: 0);
        var twoOrders = RunningSum("pk6_w", RowsRunning(), partitions: 1, orders: 2);

        Rows(Prepare(oneOrder)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(twoOrders)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 },
            "the extra tie-breaker key does not change the rows because Id is unique");
        comparer.Equals(oneOrder, twoOrders).Should().BeFalse("a different order-key count is a different window definition");

        var ascending = RunningSum("pk6_w", RowsRunning(), partitions: 1, orders: 0);
        var descending = RunningSum("pk6_w", RowsRunning(), partitions: 1, orders: 1);

        Rows(Prepare(ascending)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(descending)).Should().Equal(new long[] { 6, 5, 3, 15, 11, 6 },
            "a descending window order reverses the running sum inside each partition");
        comparer.Equals(ascending, descending).Should().BeFalse("a different window order direction is a different key");

        var byId = RunningSum("pk6_w", RowsRunning(), partitions: 2, orders: 0);
        var byScore = RunningSum("pk6_w", RowsRunning(), partitions: 2, orders: 3);

        Rows(Prepare(byId)).Should().Equal(new long[] { 1, 2, 4, 6, 9, 12 });
        Rows(Prepare(byScore)).Should().Equal(new long[] { 6, 12, 9, 4, 5, 10 },
            "ordering the Tag partitions by Score visits the rows in a different order");
        comparer.Equals(byId, byScore).Should().BeFalse("a different window order expression is a different key");

        var namedA = RunningSum("pk6_w", RowsRunning());
        var namedB = RunningSum("pk6_wb", RowsRunning());

        Rows(Prepare(namedA)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(namedB)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 },
            "the renamed window reads the same partitioned rows");
        comparer.Equals(namedA, namedB).Should().BeFalse("the declared window name is part of the window definition");

        var singleWindow = RunningSum("pk6_w", RowsRunning());
        var twoWindows = RunningSum("pk6_w", RowsRunning(), secondWindow: true);

        Rows(Prepare(singleWindow)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 });
        Rows(Prepare(twoWindows)).Should().Equal(new long[] { 1, 3, 6, 4, 9, 15 },
            "the extra unused window does not change the projected rows");
        comparer.Equals(singleWindow, twoWindows).Should().BeFalse("a different window-definition count is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Index-hint lists (StringListsEqual) and the SQLite index-hint kind
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void QueryPlan_IndexHintListsAndKind_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Plain() => Context.From<Pk6Item>()
            .Where(x => x.Grp >= 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        QueryCommand<long> Forced() => Context.From<Pk6Item>(o => o.WithIndex(IndexHintKind.Force, "pk6_grp_idx"))
            .Where(x => x.Grp >= 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        QueryCommand<long> Ignored() => Context.From<Pk6Item>(o => o.WithIndex(IndexHintKind.Ignore, "pk6_grp_idx"))
            .Where(x => x.Grp >= 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var plain = Plain();
        var forced = Forced();
        var ignored = Ignored();

        Rows(Prepare(plain)).Should().Equal(new long[] { 1, 2, 3, 4, 5, 6 });
        Rows(Prepare(forced)).Should().Equal(new long[] { 1, 2, 3, 4, 5, 6 },
            "INDEXED BY keeps the same result set");
        Rows(Prepare(ignored)).Should().Equal(new long[] { 1, 2, 3, 4, 5, 6 },
            "NOT INDEXED keeps the same result set");

        var comparer = plain.GetQueryPlanEqualityComparer();
        comparer.Equals(plain, forced).Should()
            .BeFalse("an index-hint list present on one side only (and a different index-hint kind) is a different plan key");
        comparer.Equals(forced, ignored).Should().BeFalse("a different index-hint kind is a different plan key");

        var otherIndex = Context.From<Pk6Item>(o => o.WithIndex(IndexHintKind.Force, "pk6_tag_idx"))
            .Where(x => x.Grp >= 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        Rows(Prepare(otherIndex)).Should().Equal(new long[] { 1, 2, 3, 4, 5, 6 });
        comparer.Equals(forced, otherIndex).Should().BeFalse("a different index-hint element is a different plan key");

    }

    // ---------------------------------------------------------------------------------------------
    // 7. CTE declarations: body, count, recursive flag, max recursion
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void QueryPlan_CteDefinitions_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<int> OneCte(string name)
        {
            var body = Context.From<Pk6Item>().Where(x => x.Grp == 1).Select(x => new { x.Id });
            Prepare(body);
            return Context.With(name, body)
                .From(name)
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        QueryCommand<int> TwoCtes(int grp)
        {
            var first = Context.From<Pk6Item>().Where(x => x.Grp == grp).Select(x => new { x.Id });
            var second = Context.From<Pk6Item>().Where(x => x.Tag == 10).Select(x => new { x.Id });
            Prepare(first);
            Prepare(second);
            return Context.With("pk6_cte_a", first)
                .With("pk6_cte_b", second)
                .From("pk6_cte_a")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        var equalA = OneCte("pk6_cte");
        var equalB = OneCte("pk6_cte");

        var prepared = Prepare(equalA);
        Prepare(equalB).Should().BeSameAs(prepared, "an identical CTE declaration reuses one cached plan");
        Rows(prepared).Should().Equal(new[] { 1, 2, 3 });

        var comparer = equalA.GetQueryPlanEqualityComparer();
        comparer.Equals(equalA, equalB).Should().BeTrue("name, flag, recursion limit and body all match");

        QueryCommand<int> OneCteByTag()
        {
            var body = Context.From<Pk6Item>().Where(x => x.Tag == 10).Select(x => new { x.Id });
            Prepare(body);
            return Context.With("pk6_cte", body)
                .From("pk6_cte")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        var otherBody = OneCteByTag();
        Rows(Prepare(otherBody)).Should().Equal(new[] { 1, 3, 5 });
        comparer.Equals(equalA, otherBody).Should().BeFalse("a different CTE body is a different plan key");

        var twoDeclarations = TwoCtes(1);
        Rows(Prepare(twoDeclarations)).Should().Equal(new[] { 1, 2, 3 },
            "the outer query reads the first declaration");
        comparer.Equals(equalA, twoDeclarations).Should().BeFalse("a different CTE declaration count is a different plan key");

        QueryCommand<int> RecursiveCte(int? maxRecursion)
        {
            var body = Context.From<Pk6Item>().Where(x => x.Grp == 1).Select(x => new { x.Id });
            Prepare(body);
            return Context.WithRecursive("pk6_cte_r", body, maxRecursion)
                .From("pk6_cte_r")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        var recursiveThree = RecursiveCte(3);
        var recursiveFive = RecursiveCte(5);

        Rows(Prepare(recursiveThree)).Should().Equal(new[] { 1, 2, 3 });
        Rows(Prepare(recursiveFive)).Should().Equal(new[] { 1, 2, 3 });
        comparer.Equals(recursiveThree, recursiveFive).Should()
            .BeFalse("a different recursion limit is a different CTE declaration");

        var plainSameBody = OneCte("pk6_cte_r");
        Rows(Prepare(plainSameBody)).Should().Equal(new[] { 1, 2, 3 });
        comparer.Equals(plainSameBody, recursiveThree).Should()
            .BeFalse("a recursive and a non-recursive CTE declaration are a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Member-init bindings: member identity, assignment expression, binding count
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void ExpressionComparer_MemberInitBindings_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk6Item>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        var equalA = Context.From<Pk6Item>()
            .OrderBy(x => x.Id)
            .Select(x => new Pk6Dto { Id = x.Id, Text = x.Name });
        var equalB = Context.From<Pk6Item>()
            .OrderBy(x => x.Id)
            .Select(x => new Pk6Dto { Id = x.Id, Text = x.Name });

        Rows(Prepare(equalA)).Select(r => (r.Id, r.Text)).Should()
            .Equal((1L, "alpha"), (2L, "beta"), (3L, "gamma"), (4L, "delta"), (5L, "epsilon"), (6L, "zeta"));
        Prepare(equalB).Should().BeSameAs(Prepare(equalA), "an identical member-init projection reuses one plan");
        comparer.Equals(equalA, equalB).Should().BeTrue("every binding member and assignment expression matches");

        var otherMember = Context.From<Pk6Item>()
            .OrderBy(x => x.Id)
            .Select(x => new Pk6Dto { Id = x.Id, Number = x.Tag });

        Rows(Prepare(otherMember)).Select(r => (r.Id, r.Number)).Should()
            .Equal((1L, 10), (2L, 20), (3L, 10), (4L, 20), (5L, 10), (6L, 20));
        comparer.Equals(equalA, otherMember).Should()
            .BeFalse("the second binding targets a different member, which CompareBinding rejects");

        var otherExpression = Context.From<Pk6Item>()
            .OrderBy(x => x.Id)
            .Select(x => new Pk6Dto { Id = x.Id, Text = x.Code });

        Rows(Prepare(otherExpression)).Select(r => (r.Id, r.Text)).Should()
            .Equal((1L, "a"), (2L, "b"), (3L, "a"), (4L, "b"), (5L, "c"), (6L, "c"));
        comparer.Equals(equalA, otherExpression).Should()
            .BeFalse("the same binding member with a different assignment expression is a different plan key");

        var oneBinding = Context.From<Pk6Item>()
            .OrderBy(x => x.Id)
            .Select(x => new Pk6Dto { Id = x.Id });

        Rows(Prepare(oneBinding)).Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4, 5, 6 });
        comparer.Equals(equalA, oneBinding).Should()
            .BeFalse("a different binding count is rejected by the member-binding list comparer");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. EntityType: the same shape over two entity types
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EqualsCore</c> <c>EntityType</c> :68. Two entity types over equal-shaped
    /// tables with the same predicate and projection produce the same result type and row values, but
    /// the comparer rejects them on the entity type before any other component; both sides run live.
    /// </summary>
    [Fact]
    public void QueryPlan_EntityType_ShouldKeySeparately()
    {
        ResetPlanCache();

        var comparer = Context.From<Pk6Item>().Select(x => x.Id).GetQueryPlanEqualityComparer();

        QueryCommand<long> Items() => Context.From<Pk6Item>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        QueryCommand<long> Mirrors() => Context.From<Pk6Mirror>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var equalA = Items();
        var equalB = Items();

        Rows(Prepare(equalA)).Should().Equal(new long[] { 1, 2, 3 });
        Prepare(equalB).Should().BeSameAs(Prepare(equalA), "an identical entity query reuses one cached plan");
        comparer.Equals(equalA, equalB).Should().BeTrue("every compared component including EntityType matches");

        var mirror = Mirrors();
        Rows(Prepare(mirror)).Should().Equal(new long[] { 1, 2, 3 },
            "the mirror table carries the same seed, so the rows prove the second side executes");
        comparer.Equals(equalA, mirror).Should()
            .BeFalse("a different entity type is a different plan key even when the shape and rows are identical");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. CteDefinitionsEqual: the declaration name alone
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>CteDefinitionsEqual</c> :338 <c>Name</c>. Two commands declare the same
    /// prepared body with the same recursion settings under different CTE names; the comparer rejects
    /// them on the name, and each name reads the same live rows.
    /// </summary>
    [Fact]
    public void QueryPlan_CteDeclarationName_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<int> Cte(string name)
        {
            var body = Context.From<Pk6Item>().Where(x => x.Grp == 1).Select(x => new { x.Id });
            Prepare(body);
            return Context.With(name, body)
                .From(name)
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        var equalA = Cte("pk6_cte_name");
        var equalB = Cte("pk6_cte_name");
        var otherName = Cte("pk6_cte_other");

        var prepared = Prepare(equalA);
        Prepare(equalB).Should().BeSameAs(prepared, "an identical declaration name reuses one cached plan");
        Rows(prepared).Should().Equal(new[] { 1, 2, 3 });

        var comparer = equalA.GetQueryPlanEqualityComparer();
        comparer.Equals(equalA, equalB).Should().BeTrue("the body, flags, recursion limit and name all match");

        Rows(Prepare(otherName)).Should().Equal(new[] { 1, 2, 3 },
            "the same body under another name still reads the same live rows");
        comparer.Equals(equalA, otherName).Should()
            .BeFalse("a different CTE declaration name alone is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. UnionQuery: the set-operation operand command
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EqualsCore</c> <c>UnionQuery</c> :196. Both set operations are UNION, so
    /// <c>UnionType</c> matches and the comparison reaches the recursive operand command; only the
    /// right-hand predicate differs. The two commands read different live row sets.
    /// </summary>
    [Fact]
    public void QueryPlan_UnionOperand_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Left() => Context.From<Pk6Item>()
            .Where(x => x.Grp == 1)
            .Select(x => x.Id);
        QueryCommand<long> TagTen() => Context.From<Pk6Item>()
            .Where(x => x.Tag == 10)
            .Select(x => x.Id);
        QueryCommand<long> TagTwenty() => Context.From<Pk6Item>()
            .Where(x => x.Tag == 20)
            .Select(x => x.Id);

        var unionA = Left().Union(TagTen());
        var unionA2 = Left().Union(TagTen());
        var unionB = Left().Union(TagTwenty());

        Rows(Prepare(unionA)).Should().BeEquivalentTo(new long[] { 1, 2, 3, 5 });
        Prepare(unionA2).Should().BeSameAs(Prepare(unionA), "the same UNION operands reuse one cached plan");

        var comparer = unionA.GetQueryPlanEqualityComparer();
        comparer.Equals(unionA, unionA2).Should().BeTrue("both UNIONs have the same type and operand commands");

        Rows(Prepare(unionB)).Should().BeEquivalentTo(new long[] { 1, 2, 3, 4, 6 },
            "the right-hand tag-20 operand adds markers 2, 4 and 6 under the grp-1 left side");
        comparer.Equals(unionA, unionB).Should()
            .BeFalse("a different set-operation operand command is a different plan key under the same UNION type");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. EagerLoadShape: the eager graph identity
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EqualsCore</c> <c>EagerLoadShape</c> :206. The parent query is identical;
    /// one command declares a child load and the other does not, so the eager graph shape is the only
    /// difference. The loaded side materializes its children from the live database.
    /// </summary>
    [Fact]
    public void QueryPlan_EagerLoadShape_ShouldKeySeparately()
    {
        ResetPlanCache();

        EntityBuilder<Pk6Parent> Plain() => Context.From<Pk6Parent>().OrderBy(p => p.Id);
        EntityBuilder<Pk6Parent> Loaded() => Context.From<Pk6Parent>()
            .LoadWith(
                p => p.Children,
                c => c.From<Pk6Child>().OrderBy(x => x.SortKey),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id);

        var equalA = Loaded().ToCommand();
        var equalB = Loaded().ToCommand();
        var plain = Plain().ToCommand();

        Prepare(equalA);
        Prepare(equalB).Should().BeSameAs(Prepare(equalA), "an identical eager graph reuses one cached plan");

        var comparer = equalA.GetQueryPlanEqualityComparer();
        comparer.Equals(equalA, equalB).Should().BeTrue("both commands carry the same eager-load identity list");

        var plainParents = Plain().ToList();
        plainParents.Select(p => p.Id).Should().Equal(new long[] { 9801, 9802 });
        plainParents.Should().OnlyContain(p => p.Children.Count == 0,
            "without a declared load the navigation stays empty");

        Rows(Prepare(plain)).Select(p => p.Id).Should().Equal(new long[] { 9801, 9802 },
            "the unloaded shape still executes the live parent query");
        comparer.Equals(equalA, plain).Should()
            .BeFalse("an empty eager graph and a declared child load are different plan keys");

        var loadedParents = Loaded().ToList();
        loadedParents.Select(p => p.Id).Should().Equal(new long[] { 9801, 9802 });
        loadedParents[0].Children.Select(c => c.Id).Should().Equal(new long[] { 9811, 9812, 9813 });
        loadedParents[1].Children.Select(c => c.Id).Should().Equal(new long[] { 9821 });
    }

    // ---------------------------------------------------------------------------------------------
    // 13. ResolvedEagerLoadMode: the same graph under SplitQuery and SingleQuery
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EqualsCore</c> <c>ResolvedEagerLoadMode</c> :204. Both commands declare the
    /// same eager graph (the same <c>EagerLoadShape</c>), so only the resolved strategy differs; both
    /// strategies return the same parents with the same children through their own live execution path.
    /// The child declares <c>OrderBy(...).Limit(10)</c> (per-parent top-N), which is the shape
    /// <c>SingleQuery</c> can fold into its ranked derived source; a bare child <c>OrderBy</c> is
    /// rejected by the product's fail-closed guard, so only the top-N form keeps one graph valid for
    /// both strategies.
    /// </summary>
    [Fact]
    public void QueryPlan_ResolvedEagerLoadMode_ShouldKeySeparately()
    {
        ResetPlanCache();

        EntityBuilder<Pk6Parent> Loaded(EagerLoadMode mode) => Context.From<Pk6Parent>()
            .LoadWith(
                p => p.Children,
                c => c.From<Pk6Child>().OrderBy(x => x.SortKey).Limit(10),
                p => p.Id,
                c => c.ParentId,
                mode)
            .OrderBy(p => p.Id);

        var singleA = Loaded(EagerLoadMode.SingleQuery).ToCommand();
        var singleB = Loaded(EagerLoadMode.SingleQuery).ToCommand();
        var split = Loaded(EagerLoadMode.SplitQuery).ToCommand();

        Prepare(singleA);
        Prepare(singleB).Should().BeSameAs(Prepare(singleA), "the same resolved mode reuses one cached plan");

        var comparer = singleA.GetQueryPlanEqualityComparer();
        comparer.Equals(singleA, singleB).Should().BeTrue("identical strategy, graph and shape");

        Prepare(split);
        comparer.Equals(singleA, split).Should()
            .BeFalse("SplitQuery and SingleQuery resolve differently for the same eager graph, so the keys differ");

        var splitParents = Loaded(EagerLoadMode.SplitQuery).ToList();
        splitParents.Select(p => p.Id).Should().Equal(new long[] { 9801, 9802 });
        splitParents[0].Children.Select(c => c.Id).Should().Equal(new long[] { 9811, 9812, 9813 },
            "split loading reads the children in the declared order");

        var singleParents = Loaded(EagerLoadMode.SingleQuery).ToList();
        singleParents.Select(p => p.Id).Should().Equal(new long[] { 9801, 9802 });
        singleParents[0].Children.Select(c => c.Id).Should().Equal(new long[] { 9811, 9812, 9813 },
            "single-query loading stitches the same live children");
        singleParents[1].Children.Select(c => c.Id).Should().Equal(new long[] { 9821 });
    }

    // ---------------------------------------------------------------------------------------------
    // 14. SingleScalar: the scalar terminal flag on an identical SQL shape
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EqualsCore</c> <c>SingleScalar</c> :177. A non-numeric projection is not
    /// wrapped in the cardinality guard, so the flag leaves the rendered SQL byte-identical; it must
    /// still key separately because the correlated Single terminal would otherwise share the plain
    /// materializer. Both commands read the same live rows.
    /// </summary>
    [Fact]
    public void QueryPlan_SingleScalarFlag_ShouldKeySeparately()
    {
        ResetPlanCache();

        var plain = Context.From<Pk6Item>().OrderBy(x => x.Id).Select(x => x.Name);
        var scalar = Context.From<Pk6Item>().OrderBy(x => x.Id).Select(x => x.Name);
        scalar.SingleScalar = true;

        var plainPrepared = Prepare(plain);
        var scalarPrepared = Prepare(scalar);

        Rows(plainPrepared).Should().Equal("alpha", "beta", "gamma", "delta", "epsilon", "zeta");
        Rows(scalarPrepared).Should().Equal(
            new[] { "alpha", "beta", "gamma", "delta", "epsilon", "zeta" },
            "the non-numeric projection is not guarded, so both sides read the same live rows");

        plain.GetQueryPlanEqualityComparer().Equals(plain, scalar).Should()
            .BeFalse("the SingleScalar terminal flag is part of the plan key even when the SQL is identical");

        var equalA = Context.From<Pk6Item>().OrderBy(x => x.Id).Select(x => x.Name);
        var equalB = Context.From<Pk6Item>().OrderBy(x => x.Id).Select(x => x.Name);
        Prepare(equalB).Should().BeSameAs(Prepare(equalA), "two plain scalar projections share one cached plan");
        equalA.GetQueryPlanEqualityComparer().Equals(equalA, equalB).Should().BeTrue("two equal plain shapes match");
    }

    // ---------------------------------------------------------------------------------------------
    // 15. XxHash32.Combine: every public arity, determinism, position/order sensitivity, zero and null
    // ---------------------------------------------------------------------------------------------
    /// <summary>Arm A: the one-argument <c>XxHash32.Combine</c> (:41).</summary>
    [Fact]
    public void XxHash32_CombineOne_ShouldBeDeterministicAndValueSensitive()
    {
        ResetPlanCache();

        var same = XxHash32.Combine(42);
        XxHash32.Combine(42).Should().Be(same, "the same single value hashes identically");
        XxHash32.Combine(43).Should().NotBe(same, "a different value changes the hash");
        XxHash32.Combine<string?>(null).Should().Be(XxHash32.Combine(0),
            "a null reference contributes zero, exactly like the zero value");

        Rows(Prepare(Context.From<Pk6Item>().OrderBy(x => x.Id).Select(x => x.Id))).Should()
            .Equal(new long[] { 1, 2, 3, 4, 5, 6 }, "the class fixture stays live behind the hash facts");
    }

    /// <summary>Arm A: the two-argument <c>XxHash32.Combine</c> (:67).</summary>
    [Fact]
    public void XxHash32_CombineTwo_ShouldBeDeterministicPositionAndOrderSensitive()
    {
        var same = XxHash32.Combine(1, 2);
        XxHash32.Combine(1, 2).Should().Be(same);
        XxHash32.Combine(9, 2).Should().NotBe(same, "a changed first value changes the hash");
        XxHash32.Combine(1, 9).Should().NotBe(same, "a changed second value changes the hash");
        XxHash32.Combine(2, 1).Should().NotBe(same, "swapped operands hash differently");
        XxHash32.Combine<string?, int>(null, 0).Should().Be(XxHash32.Combine(0, 0));
        XxHash32.Combine<int, string?>(0, null).Should().Be(XxHash32.Combine(0, 0));
    }

    /// <summary>Arm A: the three-argument <c>XxHash32.Combine</c> (:90).</summary>
    [Fact]
    public void XxHash32_CombineThree_ShouldBeDeterministicPositionAndOrderSensitive()
    {
        var same = XxHash32.Combine(1, 2, 3);
        XxHash32.Combine(1, 2, 3).Should().Be(same);
        XxHash32.Combine(9, 2, 3).Should().NotBe(same, "a changed first value changes the hash");
        XxHash32.Combine(1, 9, 3).Should().NotBe(same, "a changed second value changes the hash");
        XxHash32.Combine(1, 2, 9).Should().NotBe(same, "a changed third value changes the hash");
        XxHash32.Combine(3, 2, 1).Should().NotBe(same, "reversed operands hash differently");
        XxHash32.Combine<string?, int, int>(null, 0, 0).Should().Be(XxHash32.Combine(0, 0, 0));
    }

    /// <summary>Arm A: the four-argument <c>XxHash32.Combine</c> (:117).</summary>
    [Fact]
    public void XxHash32_CombineFour_ShouldBeDeterministicPositionAndOrderSensitive()
    {
        var same = XxHash32.Combine(1, 2, 3, 4);
        XxHash32.Combine(1, 2, 3, 4).Should().Be(same);
        XxHash32.Combine(9, 2, 3, 4).Should().NotBe(same, "a changed first value changes the hash");
        XxHash32.Combine(1, 9, 3, 4).Should().NotBe(same, "a changed second value changes the hash");
        XxHash32.Combine(1, 2, 9, 4).Should().NotBe(same, "a changed third value changes the hash");
        XxHash32.Combine(1, 2, 3, 9).Should().NotBe(same, "a changed fourth value changes the hash");
        XxHash32.Combine(4, 3, 2, 1).Should().NotBe(same, "reversed operands hash differently");
        XxHash32.Combine<string?, int, int, int>(null, 0, 0, 0).Should().Be(XxHash32.Combine(0, 0, 0, 0));
    }

    /// <summary>Arm A: the five-argument <c>XxHash32.Combine</c> (:150).</summary>
    [Fact]
    public void XxHash32_CombineFive_ShouldBeDeterministicPositionAndOrderSensitive()
    {
        var same = XxHash32.Combine(1, 2, 3, 4, 5);
        XxHash32.Combine(1, 2, 3, 4, 5).Should().Be(same);
        XxHash32.Combine(9, 2, 3, 4, 5).Should().NotBe(same, "a changed first value changes the hash");
        XxHash32.Combine(1, 9, 3, 4, 5).Should().NotBe(same, "a changed second value changes the hash");
        XxHash32.Combine(1, 2, 9, 4, 5).Should().NotBe(same, "a changed third value changes the hash");
        XxHash32.Combine(1, 2, 3, 9, 5).Should().NotBe(same, "a changed fourth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 9).Should().NotBe(same, "a changed fifth value changes the hash");
        XxHash32.Combine(5, 4, 3, 2, 1).Should().NotBe(same, "reversed operands hash differently");
        XxHash32.Combine<string?, int, int, int, int>(null, 0, 0, 0, 0).Should().Be(XxHash32.Combine(0, 0, 0, 0, 0));
    }

    /// <summary>Arm A: the six-argument <c>XxHash32.Combine</c> (:188).</summary>
    [Fact]
    public void XxHash32_CombineSix_ShouldBeDeterministicPositionAndOrderSensitive()
    {
        var same = XxHash32.Combine(1, 2, 3, 4, 5, 6);
        XxHash32.Combine(1, 2, 3, 4, 5, 6).Should().Be(same);
        XxHash32.Combine(9, 2, 3, 4, 5, 6).Should().NotBe(same, "a changed first value changes the hash");
        XxHash32.Combine(1, 9, 3, 4, 5, 6).Should().NotBe(same, "a changed second value changes the hash");
        XxHash32.Combine(1, 2, 9, 4, 5, 6).Should().NotBe(same, "a changed third value changes the hash");
        XxHash32.Combine(1, 2, 3, 9, 5, 6).Should().NotBe(same, "a changed fourth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 9, 6).Should().NotBe(same, "a changed fifth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 5, 9).Should().NotBe(same, "a changed sixth value changes the hash");
        XxHash32.Combine(6, 5, 4, 3, 2, 1).Should().NotBe(same, "reversed operands hash differently");
        XxHash32.Combine<string?, int, int, int, int, int>(null, 0, 0, 0, 0, 0).Should().Be(XxHash32.Combine(0, 0, 0, 0, 0, 0));
    }

    /// <summary>Arm A: the seven-argument <c>XxHash32.Combine</c> (:230).</summary>
    [Fact]
    public void XxHash32_CombineSeven_ShouldBeDeterministicPositionAndOrderSensitive()
    {
        var same = XxHash32.Combine(1, 2, 3, 4, 5, 6, 7);
        XxHash32.Combine(1, 2, 3, 4, 5, 6, 7).Should().Be(same);
        XxHash32.Combine(9, 2, 3, 4, 5, 6, 7).Should().NotBe(same, "a changed first value changes the hash");
        XxHash32.Combine(1, 9, 3, 4, 5, 6, 7).Should().NotBe(same, "a changed second value changes the hash");
        XxHash32.Combine(1, 2, 9, 4, 5, 6, 7).Should().NotBe(same, "a changed third value changes the hash");
        XxHash32.Combine(1, 2, 3, 9, 5, 6, 7).Should().NotBe(same, "a changed fourth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 9, 6, 7).Should().NotBe(same, "a changed fifth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 5, 9, 7).Should().NotBe(same, "a changed sixth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 5, 6, 9).Should().NotBe(same, "a changed seventh value changes the hash");
        XxHash32.Combine(7, 6, 5, 4, 3, 2, 1).Should().NotBe(same, "reversed operands hash differently");
        XxHash32.Combine<string?, int, int, int, int, int, int>(null, 0, 0, 0, 0, 0, 0).Should().Be(XxHash32.Combine(0, 0, 0, 0, 0, 0, 0));
    }

    /// <summary>Arm A: the eight-argument <c>XxHash32.Combine</c> (:276).</summary>
    [Fact]
    public void XxHash32_CombineEight_ShouldBeDeterministicPositionAndOrderSensitive()
    {
        var same = XxHash32.Combine(1, 2, 3, 4, 5, 6, 7, 8);
        XxHash32.Combine(1, 2, 3, 4, 5, 6, 7, 8).Should().Be(same);
        XxHash32.Combine(9, 2, 3, 4, 5, 6, 7, 8).Should().NotBe(same, "a changed first value changes the hash");
        XxHash32.Combine(1, 9, 3, 4, 5, 6, 7, 8).Should().NotBe(same, "a changed second value changes the hash");
        XxHash32.Combine(1, 2, 9, 4, 5, 6, 7, 8).Should().NotBe(same, "a changed third value changes the hash");
        XxHash32.Combine(1, 2, 3, 9, 5, 6, 7, 8).Should().NotBe(same, "a changed fourth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 9, 6, 7, 8).Should().NotBe(same, "a changed fifth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 5, 9, 7, 8).Should().NotBe(same, "a changed sixth value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 5, 6, 9, 8).Should().NotBe(same, "a changed seventh value changes the hash");
        XxHash32.Combine(1, 2, 3, 4, 5, 6, 7, 9).Should().NotBe(same, "a changed eighth value changes the hash");
        XxHash32.Combine(8, 7, 6, 5, 4, 3, 2, 1).Should().NotBe(same, "reversed operands hash differently");
        XxHash32.Combine<string?, int, int, int, int, int, int, int>(null, 0, 0, 0, 0, 0, 0, 0).Should().Be(XxHash32.Combine(0, 0, 0, 0, 0, 0, 0, 0));
    }

    // ---------------------------------------------------------------------------------------------
    // 16. CteBodiesEqual: the reference short-circuit and the null arms, plus the CTE-body hash
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (CTE): <c>CteBodiesEqual</c> :60-61 and <c>GetCteBodyHashCode</c> :384-385. The body is
    /// prepared on this context; the same instance short-circuits as equal, a missing side is unequal and
    /// the null body hashes to zero. The live baseline query behind the fact still executes.
    /// </summary>
    [Fact]
    public void QueryPlan_CteBodyComparerReferenceAndNullArms_ShouldCompareAndHash()
    {
        ResetPlanCache();

        var body = Context.From<Pk6Item>().Where(x => x.Grp == 1).Select(x => new { x.Id });
        Prepare(body);

        var comparer = body.GetQueryPlanEqualityComparer();

        comparer.CteBodiesEqual(body, body).Should().BeTrue("the same body instance is equal by reference");
        comparer.CteBodiesEqual(body, null).Should().BeFalse("a missing right-hand body is never equal");
        comparer.CteBodiesEqual(null, body).Should().BeFalse("a missing left-hand body is never equal");
        comparer.CteBodiesEqual(null, null).Should().BeTrue("two missing bodies are equal");

        comparer.GetCteBodyHashCode(null).Should().Be(0, "the null CTE body hashes to zero");
        comparer.GetCteBodyHashCode(body).Should().Be(comparer.GetHashCode(body),
            "a command without own declarations hashes the same with and without the CTE flag");

        Rows(Prepare(Context.From<Pk6Item>().OrderBy(x => x.Id).Select(x => x.Id))).Should()
            .Equal(new long[] { 1, 2, 3, 4, 5, 6 });
    }

    // ---------------------------------------------------------------------------------------------
    // 17. CTE declarations attached to an equal entity source: presence, body and mutation identity
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (CTE): <c>CteDefinitionsEqual</c> :339 (a declaration list present on one side only) and
    /// :352-355 (a data-modifying declaration compares its mutation by reference). Both pairs share this
    /// one context; the read-CTE pair also executes and shares one cached plan. The data-modifying pair
    /// is prepared only: SQLite rejects the body at render, but the plan key is provider independent.
    /// </summary>
    [Fact]
    public void QueryPlan_CtePresenceAndMutationIdentity_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Plain() => Context.From<Pk6Item>().OrderBy(x => x.Id).Select(x => x.Id);

        var bodyA = Context.From<Pk6Item>().Where(x => x.Grp == 1).Select(x => new { x.Id });
        var bodyB = Context.From<Pk6Item>().Where(x => x.Grp == 1).Select(x => new { x.Id });
        var bodyC = Context.From<Pk6Item>().Where(x => x.Tag == 10).Select(x => new { x.Id });
        Prepare(bodyA);
        Prepare(bodyB);
        Prepare(bodyC);

        var withCteA = Plain();
        withCteA.Ctes = [new CteDefinition("pk6_unused_cte", bodyA)];
        var withCteB = Plain();
        withCteB.Ctes = [new CteDefinition("pk6_unused_cte", bodyB)];
        var withOtherBody = Plain();
        withOtherBody.Ctes = [new CteDefinition("pk6_unused_cte", bodyC)];
        var plain = Plain();

        var prepared = Prepare(withCteA);
        Rows(prepared).Should().Equal(new long[] { 1, 2, 3, 4, 5, 6 },
            "an unused CTE declaration does not change the live rows");
        Prepare(withCteB).Should().BeSameAs(prepared, "two structurally equal declarations share one plan");
        Rows(Prepare(plain)).Should().Equal(new long[] { 1, 2, 3, 4, 5, 6 },
            "the declaration-free counterpart executes live on the same context");

        var comparer = withCteA.GetQueryPlanEqualityComparer();
        comparer.Equals(withCteA, withCteB).Should().BeTrue("equal name, flags and structurally equal bodies");
        comparer.GetHashCode(withCteA).Should().Be(comparer.GetHashCode(withCteB));
        comparer.Equals(withCteA, plain).Should()
            .BeFalse("a declaration list present on one side only is a different plan key");
        comparer.GetHashCode(withCteA).Should().NotBe(comparer.GetHashCode(plain));
        Rows(Prepare(withOtherBody)).Should().Equal(new long[] { 1, 2, 3, 4, 5, 6 });
        comparer.Equals(withCteA, withOtherBody).Should().BeFalse("a different CTE body is a different plan key");

        var shapeA = Context.From<Pk6Item>().Select(x => new { x.Id });
        var shapeB = Context.From<Pk6Item>().Select(x => new { x.Id });
        var shapeC = Context.From<Pk6Item>().Select(x => new { x.Id });

        var mutation = new CteMutation(Context.CreateUpdateBuilder<Pk6Item>()
            .Set(x => x.Grp, x => x.Grp)
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id })
            .BuildMutationCommand());
        var otherMutation = new CteMutation(Context.CreateUpdateBuilder<Pk6Item>()
            .Set(x => x.Grp, x => x.Grp)
            .Where(x => x.Id == 2)
            .Returning(x => new { x.Id })
            .BuildMutationCommand());

        var mutA = Plain();
        mutA.Ctes = [new CteDefinition("pk6_mut_cte", shapeA, mutation)];
        var mutB = Plain();
        mutB.Ctes = [new CteDefinition("pk6_mut_cte", shapeB, mutation)];
        var mutC = Plain();
        mutC.Ctes = [new CteDefinition("pk6_mut_cte", shapeC, otherMutation)];

        mutA.PrepareCommand(dontCalculateHash: false, CancellationToken.None);
        mutB.PrepareCommand(dontCalculateHash: false, CancellationToken.None);
        mutC.PrepareCommand(dontCalculateHash: false, CancellationToken.None);

        var mutationComparer = mutA.GetQueryPlanEqualityComparer();
        mutationComparer.Equals(mutA, mutB).Should()
            .BeTrue("a data-modifying declaration with the same mutation instance compares equal");
        mutationComparer.GetHashCode(mutA).Should().Be(mutationComparer.GetHashCode(mutB));
        mutationComparer.Equals(mutA, mutC).Should()
            .BeFalse("a different data-modifying mutation is a different plan key");
    }

    /// <summary>Named result shape for the member-init binding facts.</summary>
    public sealed class Pk6Dto
    {
        public long Id { get; set; }

        public string Text { get; set; } = string.Empty;

        public int Number { get; set; }
    }

    [SqlTable("pk6_item")]
    public sealed class Pk6Item
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

    /// <summary>Second entity type over an equal-shaped table, used by the <c>EntityType</c> arm.</summary>
    [SqlTable("pk6_item_copy")]
    public sealed class Pk6Mirror
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

    /// <summary>Eager-load parent used by the eager-shape and resolved-mode arms.</summary>
    [SqlTable("pk6_parent")]
    public sealed class Pk6Parent
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public List<Pk6Child> Children { get; set; } = [];
    }

    /// <summary>Eager-load child of <see cref="Pk6Parent"/>.</summary>
    [SqlTable("pk6_child")]
    public sealed class Pk6Child
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("parent_id")]
        public long ParentId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }
    }
}
