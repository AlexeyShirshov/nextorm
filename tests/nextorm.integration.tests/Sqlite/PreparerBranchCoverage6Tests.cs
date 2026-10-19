using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// QueryPreparer branch coverage (cycle 6, cluster D2) executed against a live SQLite file database.
/// Every fact runs real queries over the class-owned <c>pr6_marker</c> / <c>pr6_line</c> tables
/// (dropped, created and seeded by this class only) and asserts the returned rows; no
/// <see cref="InMemoryDataContext"/>, no reflection and no global seed data.
///
/// <para>
/// Targeted components of <c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c>:
/// <c>PrepareColumns</c> :507 (nested scalar subquery, arithmetic/cast composition, aggregate over a
/// derived joined source, member-init column subset, an entity item inside an anonymous projection, the
/// SQLite scalar-cardinality guard, a scalar typed-CTE identity read and the metadata-less derived
/// single/multi-column pass-through), <c>ScanUnkeyedTupleInFrom</c> :3007 and
/// <c>ScanUnkeyedScalarInFrom</c> :3263 (the <c>ColumnShape</c> arm of a typed CTE read and the
/// <c>TempTable</c> arm of a lazy temporary table), <c>ScanUnkeyedTupleInValues</c> :2853 (HAVING,
/// GROUP BY, JOIN ON, ORDER BY, WINDOW PARTITION/ORDER and set-operation operand) and
/// <c>RefreshInValuesShape</c> :196 (grow/shrink/reorder of a captured list on one command object plus
/// the shape-clean cache holder).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Sqlite/FilterTupleBranchCoverage5Tests.cs</c>
/// owns the HasQueryFilter dependency walk, the bound-source filter compatibility arms, five
/// <c>PrepareColumns</c> projection shapes (entity, scalar, scalar subquery, joined item, grouped
/// derivative), the raw select/join/grouping/having/IN/CTE descriptor pairs and the
/// <c>ScanUnkeyedScalarInValues</c> ORDER BY membership key; <c>Sqlite/PlanKeyBranchCoverage5Tests.cs</c>
/// owns the plan-key comparer and raw-shape battery. This class never repeats those shapes.
/// </para>
///
/// <para>
/// Recorded, never fabricated: a <c>ValueTuple</c> terminal projection stays blocked by #307, so the
/// tuple is used only as an IN key or inside a comparison; the <c>PreparedPreWhere</c> arms of
/// <c>RefreshInValuesShape</c> (:227-240 and :267-278) need a public PreWhere entry point that SQLite
/// does not have; the <c>LinqSource</c>, <c>XmlNodes</c>, PIVOT and table-function arms of the FROM
/// scans are provider- or in-memory-only; and the in-memory entity-item branch (:625-646) needs
/// <c>NeedMapping == false</c>. All of them are recorded in <c>rv1/inputs/preparer-arms.md</c>.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class PreparerBranchCoverage6Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists pr6_line;
        drop table if exists pr6_marker;
        create table pr6_marker
        (
            id integer primary key,
            name text not null,
            grp int not null,
            tag int not null,
            score int null
        );
        create table pr6_line
        (
            id integer primary key,
            marker_id int not null,
            qty int not null
        );
        insert into pr6_marker (id, name, grp, tag, score) values
            (1, 'alpha', 1, 10, 5),
            (2, 'beta',  1, 20, null),
            (3, 'gamma', 2, 10, 9),
            (4, 'delta', 2, 20, null);
        insert into pr6_line (id, marker_id, qty) values
            (10, 1, 1),
            (11, 1, 3),
            (12, 2, 5),
            (13, 3, 7);
        drop table if exists pr6_kinds;
        create table pr6_kinds
        (
            id integer primary key,
            created text not null,
            token text null
        );
        -- created is TEXT; the DateTime/Guid converters read it as the CLR types the filters use.
        insert into pr6_kinds (id, created, token) values
            (1, '2024-03-01 10:00:00', '11111111-1111-1111-1111-111111111111'),
            (2, '2023-07-02 11:00:00', '22222222-2222-2222-2222-222222222222'),
            (3, '2024-11-03 12:00:00', '33333333-3333-3333-3333-333333333333'),
            (4, '2025-01-04 13:00:00', null);
        """;

    private static int _filtersRegistered;

    public PreparerBranchCoverage6Tests()
    {
        ExecuteRaw(Ddl);

        // The first registration per entity type wins process-wide, so register once for the class and
        // BEFORE any BindEntity call (BindEntity auto-publishes an unfiltered mapping otherwise).
        if (Interlocked.Exchange(ref _filtersRegistered, 1) == 0)
        {
            Context.From<Pr6KindsMonth>(b => b.HasQueryFilter(e => e.Created.Month >= 7));
            Context.From<Pr6KindsTokenValue>(b => b.HasQueryFilter(e => e.Token!.Value != Guid.Empty));
            Context.From<Pr6KindsInherited>(b => b.HasQueryFilter(e => e.BaseCreated.DayOfYear >= 100));
        }
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
        => Prepare(command, storeInCache: true);

    /// <summary>
    /// Prepares through the requested cache mode. <c>storeInCache: false</c> is the local, call-scoped
    /// no-hash route (the planner passes <c>dontCalculateHash: true</c>); it never touches the sticky
    /// <c>QueryCommand.Cache</c> flag.
    /// </summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command, bool storeInCache)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: storeInCache,
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
    // 1. PrepareColumns: a nested scalar subquery and a CASE projection inside one shape
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A select list that combines a correlated scalar subquery (with its own ORDER BY) and a
    /// conditional CASE expression prepares both columns and reads the live rows.
    /// </summary>
    [Fact]
    public void PrepareColumns_NestedScalarSubqueryAndCase_ShouldReadEachRow()
    {
        ResetPlanCache();

        var rows = Run(Context.From<Pr6Marker>()
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                FirstQty = Context.From<Pr6Line>()
                    .Where(l => l.MarkerId == x.Id)
                    .OrderBy(l => l.Id)
                    .Select(l => l.Qty)
                    .First(),
                Bucket = x.Grp == 1 ? "one" : "two",
            }));

        rows.Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3 });
        rows.Select(r => r.FirstQty).Should().Equal(new[] { 1, 5, 7 },
            "each marker row reads the first line quantity of its own correlated subquery");
        rows.Select(r => r.Bucket).Should().Equal(new[] { "one", "one", "two" },
            "the CASE projection follows the group column");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. PrepareColumns: arithmetic and cast composition
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A projection of composed arithmetic (a casted multiplication plus a mapped column) and a
    /// self-addition prepares both computed columns and reads the live values.
    /// </summary>
    [Fact]
    public void PrepareColumns_ArithmeticAndCastComposition_ShouldReadComputedValues()
    {
        ResetPlanCache();

        var rows = Run(Context.From<Pr6Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Weighted = (int)(x.Id * 10) + x.Tag,
                Doubled = x.Id + x.Id,
            }));

        rows.Select(r => r.Weighted).Should().Equal(new[] { 20, 40, 40, 60 },
            "id * 10 plus the tag is computed per row");
        rows.Select(r => r.Doubled).Should().Equal(new long[] { 2, 4, 6, 8 },
            "the self-addition is a composed arithmetic column");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. PrepareColumns: an aggregate over a derived joined source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A grouped projection over a derived JOIN source prepares its own select list and reads the
    /// aggregated rows of the derived source.
    /// </summary>
    [Fact]
    public void PrepareColumns_AggregateOverDerivedJoin_ShouldReadGroupTotals()
    {
        ResetPlanCache();

        var joined = Context.From<Pr6Marker>()
            .Join(Context.From<Pr6Line>(), (m, l) => m.Id == l.MarkerId)
            .Select(p => new { p.Item1.Grp, p.Item2.Qty });

        var rows = Run(Context.From(joined)
            .GroupBy(x => new { x.Grp })
            .Select(g => new { g.Grp, Total = SqlFunctions.Sql.sum(g.Qty), Cnt = SqlFunctions.Sql.count() }))
            .OrderBy(r => r.Grp)
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Grp.Should().Be(1);
        rows[0].Total.Should().Be(9, "group 1 sums the derived quantities 1 + 3 + 5");
        rows[0].Cnt.Should().Be(3);
        rows[1].Grp.Should().Be(2);
        rows[1].Total.Should().Be(7);
        rows[1].Cnt.Should().Be(1);
    }

    // ---------------------------------------------------------------------------------------------
    // 4. PrepareColumns: an entity projection restricted to a column subset
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A member-init DTO projection prepares exactly the assigned columns (a subset of the entity) and
    /// materialises the live rows.
    /// </summary>
    [Fact]
    public void PrepareColumns_MemberInitColumnSubset_ShouldMaterializeTheSubset()
    {
        ResetPlanCache();

        var rows = Run(Context.From<Pr6Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new Pr6Subset { Id = x.Id, Name = x.Name }));

        rows.Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4 });
        rows.Select(r => r.Name).Should().Equal("alpha", "beta", "gamma", "delta");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. PrepareColumns: an entity item inside an anonymous projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// An entity-typed member of an anonymous projection (<c>Right = p.Item2</c>) expands the joined
    /// item into its mapped columns and the materializer rebuilds the entity per row.
    /// </summary>
    [Fact]
    public void PrepareColumns_EntityItemInsideAnonymousProjection_ShouldExpandTheItem()
    {
        ResetPlanCache();

        var rows = Run(Context.From<Pr6Marker>()
            .Join(Context.From<Pr6Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { p.Item1.Id, Right = p.Item2 }));

        rows.Select(r => r.Id).Should().Equal(new long[] { 1, 1, 2, 3 });
        rows.Select(r => r.Right.Id).Should().Equal(new long[] { 10, 11, 12, 13 },
            "the entity item is rebuilt from its expanded columns");
        rows.Select(r => r.Right.Qty).Should().Equal(new[] { 1, 3, 5, 7 });
    }

    // ---------------------------------------------------------------------------------------------
    // 6. PrepareColumns: the SQLite scalar-cardinality guard
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// On SQLite a <c>Single()</c> scalar projection is wrapped in a cardinality guard: a correlated
    /// subquery with exactly one row reads the value, while a second row raises a database error
    /// instead of silently returning the first.
    /// </summary>
    [Fact]
    public void PrepareColumns_SingleScalarCardinalityGuard_ShouldReadOneRowAndRejectTwo()
    {
        ResetPlanCache();

        var one = Run(Context.From<Pr6Marker>()
            .Where(x => x.Id == 2)
            .Select(x => Context.From<Pr6Line>()
                .Where(l => l.MarkerId == x.Id)
                .Select(l => l.Qty)
                .Single()));

        one.Should().Equal(new[] { 5 }, "marker 2 has exactly one line");

        var act = () => Run(Context.From<Pr6Marker>()
            .Where(x => x.Id == 1)
            .Select(x => Context.From<Pr6Line>()
                .Where(l => l.MarkerId == x.Id)
                .Select(l => l.Qty)
                .Single()));

        act.Should().Throw<DbException>(
            "the SQLite cardinality guard raises a database error when a Single scalar sees two rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. PrepareColumns: a scalar typed-CTE identity read
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A typed CTE that projects one scalar column, read as a whole value without a projection,
    /// reuses the shape's single readable column and reads every row.
    /// </summary>
    [Fact]
    public void PrepareColumns_ScalarTypedCteIdentityRead_ShouldReuseTheShapeColumn()
    {
        ResetPlanCache();

        var cte = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .AsCte("pr6_scalar_cte");

        Run(Context.From(cte).ToCommand()).Should().Equal(new long[] { 1, 2, 3 },
            "the scalar identity read resolves the CTE's one readable column");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. PrepareColumns: a metadata-less derived single-column pass-through
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A metadata-less derived source that exposes one scalar column, read without a projection, passes
    /// that column through as the whole row.
    /// </summary>
    [Fact]
    public void PrepareColumns_DerivedScalarPassThrough_ShouldReuseTheSingleColumn()
    {
        ResetPlanCache();

        var inner = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Grp);

        Run(Context.From(inner).ToCommand()).Should().Equal(new[] { 1, 1 },
            "the derived scalar shape stands for the whole row");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. PrepareColumns: a metadata-less derived multi-column pass-through
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A metadata-less derived source with several projected columns, read without a projection, clones
    /// each column into the consumer's own select list and reads the derived rows.
    /// </summary>
    [Fact]
    public void PrepareColumns_DerivedMultiColumnPassThrough_ShouldReadTheDerivedRows()
    {
        ResetPlanCache();

        var inner = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => new { Id = x.Id + 1, Tag = x.Tag * 2 });

        var rows = Run(Context.From(inner).ToCommand());

        rows.Select(r => r.Id).Should().Equal(new long[] { 2, 3 });
        rows.Select(r => r.Tag).Should().Equal(new[] { 20, 40 });
    }

    // ---------------------------------------------------------------------------------------------
    // 10. ScanUnkeyedTupleInFrom: the ColumnShape arm of a typed CTE read
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A tuple membership in the projection of a TYPED CTE body is unkeyed for the reading statement;
    /// the reader reaches it through the <c>ColumnShape</c> arm. Two runs with different captured pair
    /// lists must each read their own booleans.
    /// </summary>
    [Fact]
    public void ScanUnkeyedTupleInFrom_TypedCteColumnShape_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var pairs = new List<(long Id, int Tag)> { (1L, 10) };

        List<int> Groups() => Run(Context.From(Context.From<Pr6Marker>()
                .GroupBy(x => new { x.Grp, Hit = pairs.Contains(new ValueTuple<long, int>(x.Id, x.Tag)) })
                .Select(g => new { g.Grp, Cnt = SqlFunctions.Sql.count() })
                .AsCte("pr6_tuple_cte_scan"))
            .OrderBy(x => x.Grp)
            .Select(x => x.Grp))
            .ToList();

        Groups().Should().Equal(new[] { 1, 1, 2 },
            "the one-entry pair list matches only marker 1, so group 1 splits in two");

        pairs.Add((2L, 20));

        Groups().Should().Equal(new[] { 1, 2 },
            "the grown pair list re-renders the typed CTE body instead of reusing the first plan");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. ScanUnkeyedTupleInFrom: the TempTable arm of a lazy temporary table
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A tuple membership in the materialisation query of a lazy temporary table is unkeyed for the
    /// reading statement; the reader reaches it through the <c>TempTable</c> arm. The table is
    /// re-created per execution, so each captured list must be visible.
    /// </summary>
    [Fact]
    public void ScanUnkeyedTupleInFrom_TempTable_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var pairs = new List<(long Id, int Tag)> { (1L, 10) };

        List<int> Groups()
        {
            var source = Context.From<Pr6Marker>()
                .GroupBy(x => new { x.Grp, Hit = pairs.Contains(new ValueTuple<long, int>(x.Id, x.Tag)) })
                .Select(g => new { g.Grp, Cnt = SqlFunctions.Sql.count() })
                .AsTempTable();

            return Run(Context.From(source)
                    .OrderBy(t => t.GetInt32("grp"))
                    .Select(t => t.GetInt32("grp")))
                .ToList();
        }

        Groups().Should().Equal(new[] { 1, 1, 2 },
            "the one-entry pair list matches only marker 1, so group 1 splits in two");

        pairs.Add((2L, 20));

        Groups().Should().Equal(new[] { 1, 2 },
            "the grown pair list is visible through the re-materialised temporary table");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. ScanUnkeyedScalarInFrom: the ColumnShape arm of a typed CTE read
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A scalar membership in the projection of a TYPED CTE body is unkeyed for the reading statement;
    /// the reader reaches it through the <c>ColumnShape</c> arm and each captured list reads its rows.
    /// </summary>
    [Fact]
    public void ScanUnkeyedScalarInFrom_TypedCteColumnShape_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var ids = new List<long> { 1 };

        List<int> Groups() => Run(Context.From(Context.From<Pr6Marker>()
                .GroupBy(x => new { x.Grp, Hit = ids.Contains(x.Id) })
                .Select(g => new { g.Grp, Cnt = SqlFunctions.Sql.count() })
                .AsCte("pr6_scalar_cte_scan"))
            .OrderBy(x => x.Grp)
            .Select(x => x.Grp))
            .ToList();

        Groups().Should().Equal(new[] { 1, 1, 2 },
            "the one-value list matches only marker 1, so group 1 splits in two");

        ids.Add(2);

        Groups().Should().Equal(new[] { 1, 2 },
            "the grown list re-renders the typed CTE body instead of reusing the first plan");
    }

    // ---------------------------------------------------------------------------------------------
    // 13. ScanUnkeyedScalarInFrom: the TempTable arm of a lazy temporary table
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A scalar membership in the materialisation query of a lazy temporary table is unkeyed for the
    /// reading statement; the reader reaches it through the <c>TempTable</c> arm and each captured list
    /// reads its own rows.
    /// </summary>
    [Fact]
    public void ScanUnkeyedScalarInFrom_TempTable_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var ids = new List<long> { 1 };

        List<int> Groups()
        {
            var source = Context.From<Pr6Marker>()
                .GroupBy(x => new { x.Grp, Hit = ids.Contains(x.Id) })
                .Select(g => new { g.Grp, Cnt = SqlFunctions.Sql.count() })
                .AsTempTable();

            return Run(Context.From(source)
                    .OrderBy(t => t.GetInt32("grp"))
                    .Select(t => t.GetInt32("grp")))
                .ToList();
        }

        Groups().Should().Equal(new[] { 1, 1, 2 },
            "the one-value list matches only marker 1, so group 1 splits in two");

        ids.Add(2);

        Groups().Should().Equal(new[] { 1, 2 },
            "the grown list is visible through the re-materialised temporary table");
    }

    // ---------------------------------------------------------------------------------------------
    // 14. ScanUnkeyedTupleInValues: the HAVING and GROUP BY arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A tuple membership in HAVING and a tuple membership inside the GROUP BY expression are both
    /// unkeyed: each must be re-evaluated for the current captured list. The first run keeps the
    /// rows whose constant HAVING membership holds and splits the grouping by the pair match; the
    /// second run drops every row from HAVING and re-groups with the grown list.
    /// </summary>
    [Fact]
    public void ScanUnkeyedTupleInValues_HavingAndGrouping_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var marks = new List<(long Id, int Tag)> { (1L, 10) };

        List<int> HavingCounts()
            => Run(Context.From<Pr6Marker>()
                    .GroupBy(x => new { x.Grp })
                    .Having(g => g.Grp > 0 && marks.Contains(new ValueTuple<long, int>(1L, 10)))
                    .Select(g => new { g.Grp, Cnt = SqlFunctions.Sql.count() }))
                .Select(r => r.Cnt)
                .OrderBy(c => c)
                .ToList();

        HavingCounts().Should().Equal(new[] { 2, 2 },
            "the constant HAVING membership holds for both groups while the list contains (1, 10)");

        List<(int Grp, int Cnt)> GroupRows()
            => Run(Context.From<Pr6Marker>()
                    .GroupBy(x => new { x.Grp, Hit = marks.Contains(new ValueTuple<long, int>(x.Id, x.Tag)) })
                    .Select(g => new { g.Grp, Cnt = SqlFunctions.Sql.count() }))
                .Select(r => (r.Grp, r.Cnt))
                .OrderBy(r => r.Grp)
                .ThenBy(r => r.Cnt)
                .ToList();

        GroupRows().Should().Equal(
            new[] { (1, 1), (1, 1), (2, 2) },
            "the grouping list splits group 1 by the current pair match");

        marks.Add((2L, 20));

        GroupRows().Should().Equal(
            new[] { (1, 2), (2, 2) },
            "the grown list re-groups the markers instead of reusing the first plan");

        marks.Clear();

        HavingCounts().Should().BeEmpty(
            "an empty captured list makes the constant HAVING membership false for every group");
    }

    // ---------------------------------------------------------------------------------------------
    // 15. ScanUnkeyedTupleInValues: the JOIN condition arm
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A tuple membership in a JOIN condition is unkeyed. Each run with a different captured pair
    /// list must filter according to its own list.
    /// </summary>
    [Fact]
    public void ScanUnkeyedTupleInValues_JoinCondition_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var pairs = new List<(long MarkerId, int Qty)> { (1L, 1), (3L, 7) };

        List<(long Id, int Qty)> Joined()
            => Run(Context.From<Pr6Marker>()
                    .Join(
                        Context.From<Pr6Line>(),
                        (m, l) => m.Id == l.MarkerId
                            && pairs.Contains(new ValueTuple<long, int>(l.MarkerId, l.Qty)))
                    .OrderBy(p => p.Item1.Id)
                    .OrderBy(p => p.Item2.Id)
                    .Select(p => new { p.Item1.Id, p.Item2.Qty }))
                .Select(r => (r.Id, r.Qty))
                .ToList();

        Joined().Should().Equal(
            new[] { (1L, 1), (3L, 7) },
            "the JOIN condition keeps only the lines whose pair is in the captured list");

        pairs.Clear();
        pairs.Add((1L, 3));
        pairs.Add((2L, 5));

        Joined().Should().Equal(
            new[] { (1L, 3), (2L, 5) },
            "the replaced pair list re-renders the JOIN condition instead of reusing the first plan");
    }

    // ---------------------------------------------------------------------------------------------
    // 16. ScanUnkeyedTupleInValues: the WINDOW PARTITION BY and ORDER BY arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A tuple membership inside a named window's PARTITION BY and ORDER BY expressions is unkeyed:
    /// the window is re-rendered for the current list, so the running count per partition follows it.
    /// </summary>
    [Fact]
    public void ScanUnkeyedTupleInValues_WindowPartitionAndOrder_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var pairs = new List<(long Id, int Tag)> { (1L, 10) };

        List<(long Id, int Cnt)> Windowed()
        {
            var builder = Context.From<Pr6Marker>();

            return Run(builder
                    .Window(
                        "pr6_w",
                        new Expression<Func<Pr6Marker, object?>>[]
                        {
                            x => pairs.Contains(new ValueTuple<long, int>(x.Id, x.Tag)),
                        },
                        new[] { builder.Asc(x => pairs.Contains(new ValueTuple<long, int>(x.Id, x.Tag))) },
                        null)
                    .OrderBy(x => x.Id)
                    .Select(x => new { x.Id, Cnt = SqlFunctions.Sql.count_over().Over("pr6_w") }))
                .Select(r => (r.Id, r.Cnt))
                .ToList();
        }

        Windowed().Should().Equal(
            new[] { (1L, 1), (2L, 3), (3L, 3), (4L, 3) },
            "the matching marker is its own partition while the other three share one");

        pairs.Add((2L, 20));

        Windowed().Should().Equal(
            new[] { (1L, 2), (2L, 2), (3L, 2), (4L, 2) },
            "the grown list moves marker 2 into the matching partition instead of reusing the first plan");
    }

    // ---------------------------------------------------------------------------------------------
    // 17. ScanUnkeyedTupleInValues: a set-operation operand
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A tuple membership in the RIGHT operand of a set operation is unkeyed for the composed
    /// statement; each run with a different list must read its own union rows.
    /// </summary>
    [Fact]
    public void ScanUnkeyedTupleInValues_UnionOperand_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var pairs = new List<(long Id, int Tag)> { (1L, 10) };

        List<(long Id, bool Hit)> Unioned()
        {
            var left = Context.From<Pr6Marker>()
                .Where(x => x.Grp == 2)
                .Select(x => new { x.Id, Hit = false });
            var right = Context.From<Pr6Marker>()
                .Where(x => x.Grp == 1)
                .Select(x => new { x.Id, Hit = pairs.Contains(new ValueTuple<long, int>(x.Id, x.Tag)) });

            return Run(left.Union(right))
                .OrderBy(r => r.Id)
                .Select(r => (r.Id, r.Hit))
                .ToList();
        }

        Unioned().Should().Equal(
            new[] { (1L, true), (2L, false), (3L, false), (4L, false) },
            "the right operand's tuple list flags only marker 1");

        pairs.Add((2L, 20));

        Unioned().Should().Equal(
            new[] { (1L, true), (2L, true), (3L, false), (4L, false) },
            "the grown list re-renders the set-operation operand instead of reusing the first plan");
    }

    // ---------------------------------------------------------------------------------------------
    // 18. RefreshInValuesShape: grow, shrink and reorder on one command object
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// One command object is re-executed while its captured list grows, shrinks and is reordered; the
    /// plan key follows the current shape, so every execution reads its own membership.
    /// </summary>
    [Fact]
    public void RefreshInValuesShape_GrowShrinkReorder_ShouldReadEachMembership()
    {
        ResetPlanCache();

        var ids = new List<long> { 1 };
        var command = Context.From<Pr6Marker>()
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Run(command).Should().Equal(new long[] { 1 }, "the initial one-value list");

        ids.Add(2);
        Run(command).Should().Equal(new long[] { 1, 2 }, "the grown list");

        ids.Clear();
        ids.Add(4);
        Run(command).Should().Equal(new long[] { 4 }, "the shrunk and replaced list");

        ids.Insert(0, 3);
        Run(command).Should().Equal(new long[] { 3, 4 }, "the reordered list");
    }

    // ---------------------------------------------------------------------------------------------
    // 19. RefreshInValuesShape: the shape-clean companion reuses one holder
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A shape-clean command without a captured value list takes the stable-shape path of the refresh,
    /// so a second preparation reuses the same cached plan holder.
    /// </summary>
    [Fact]
    public void RefreshInValuesShape_ShapeCleanCommand_ShouldReuseOneHolder()
    {
        ResetPlanCache();

        QueryCommand<long> Clean() => Context.From<Pr6Marker>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var holder = Prepare(Clean());
        Rows(holder).Should().Equal(new long[] { 1, 2 });

        Prepare(Clean()).Should().BeSameAs(holder,
            "without a captured value list the shape is stable, so the cached holder is reused");
    }

    // ---------------------------------------------------------------------------------------------
    // 20. PrepareColumns: a correlated subquery in ORDER BY
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A correlated scalar aggregate used as the primary ORDER BY key prepares and executes, ordering
    /// the markers by their own maximum line quantity.
    /// </summary>
    [Fact]
    public void PrepareColumns_CorrelatedSubqueryInOrderBy_ShouldOrderByTheAggregate()
    {
        ResetPlanCache();

        var rows = Run(Context.From<Pr6Marker>()
            .OrderBy(x => Context.From<Pr6Line>()
                .Where(l => l.MarkerId == x.Id)
                .OrderBy(l => l.Id)
                .Select(l => l.Qty)
                .First())
            .OrderBy(x => x.Id)
            .Select(x => x.Id));

        rows.Should().Equal(new long[] { 4, 1, 2, 3 },
            "marker 4 has no line (null max) and sorts first, then markers 1, 2 and 3 by their maximum quantity");
    }

    // ---------------------------------------------------------------------------------------------
    // 21. ScanUnkeyedTupleInValues: the referenced-subquery arm
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A tuple membership in the projection of a correlated referenced subquery is unkeyed for the
    /// owning statement; each run with a different captured list must read its own correlation flags.
    /// </summary>
    [Fact]
    public void ScanUnkeyedTupleInValues_ReferencedSubqueryProjection_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var pairs = new List<(long MarkerId, int Qty)> { (1L, 1) };

        List<bool> Flags() => Run(Context.From<Pr6Marker>()
                .Where(m => m.Id <= 2)
                .OrderBy(m => m.Id)
                .Select(m => Context.From<Pr6Line>()
                    .Where(l => l.MarkerId == m.Id)
                    .Select(l => pairs.Contains(new ValueTuple<long, int>(l.MarkerId, l.Qty)))
                    .First()))
            .ToList();

        Flags().Should().Equal(new[] { true, false },
            "only marker 1 has a line whose pair is in the captured list");

        pairs.Add((2L, 5));

        Flags().Should().Equal(new[] { true, true },
            "the grown list re-renders the referenced subquery instead of reusing the first plan");
    }

    // ---------------------------------------------------------------------------------------------
    // 22. Bound-source filter dependency: the DateTime transparent member (Month)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>FilterColumnDependencyVisitor.IsTransparentMember</c> :2652 DateTime
    /// branch through the <c>Month</c> arm (cycle 5 covered <c>Year</c>). The dependency walk continues
    /// to the mapped <c>created</c> column, so the bound filter is applied; <c>IgnoreFilters()</c>
    /// returns every row. Both sides execute live.
    /// </summary>
    [Fact]
    public void BoundFilter_DateTimeMonthTransparentMember_ShouldFilterOnTheColumn()
    {
        ResetPlanCache();

        Run(Context.FromSql("select id, created from pr6_kinds")
                .BindEntity<Pr6KindsMonth>(["id", "created"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 2, 3 }, "July and November are the only months >= 7");

        Run(Context.FromSql("select id, created from pr6_kinds")
                .BindEntity<Pr6KindsMonth>(["id", "created"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the DateTime month filter");
    }

    // ---------------------------------------------------------------------------------------------
    // 23. Bound-source filter dependency: Nullable<Guid>.Value
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>IsTransparentMember</c> :2658 <c>Nullable&lt;T&gt;.Value</c> with a
    /// <c>Guid</c> underlying type (cycle 5 exercised the int underlying type and a null test, not
    /// <c>Guid?</c> <c>.Value</c>). The dependency is the mapped <c>token</c> column and the NULL row is
    /// excluded by the predicate. Both sides execute live.
    /// </summary>
    [Fact]
    public void BoundFilter_NullableGuidValueTransparentMember_ShouldFilterOnTheColumn()
    {
        ResetPlanCache();

        Run(Context.FromSql("select id, token from pr6_kinds")
                .BindEntity<Pr6KindsTokenValue>(["id", "token"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3 }, "the NULL token row is excluded by the Value comparison");

        Run(Context.FromSql("select id, token from pr6_kinds")
                .BindEntity<Pr6KindsTokenValue>(["id", "token"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the nullable-value filter");
    }

    // ---------------------------------------------------------------------------------------------
    // 24. Bound-source filter dependency: an inherited member chain ending in a DateTime part
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the dependency chain starts on a member declared by the entity's BASE class
    /// and ends in the <c>DayOfYear</c> transparent member (an arm not used by cycle 5). The walk skips
    /// the transparent member, maps the inherited <c>created</c> column and applies the filter; the
    /// <c>IgnoreFilters()</c> counterpart returns all rows.
    /// </summary>
    [Fact]
    public void BoundFilter_InheritedMemberWithDateTimePart_ShouldFilterOnTheColumn()
    {
        ResetPlanCache();

        Run(Context.FromSql("select id, created from pr6_kinds")
                .BindEntity<Pr6KindsInherited>(["id", "created"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 2, 3 }, "only July (183) and November (308) are past day 100");

        Run(Context.FromSql("select id, created from pr6_kinds")
                .BindEntity<Pr6KindsInherited>(["id", "created"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the inherited-member filter");
    }

    // ---------------------------------------------------------------------------------------------
    // 25. RawShape.Matches: the nested UNION body
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>RawShape.Matches</c> :3703 reaches its nested <c>Union</c> descriptor
    /// (<c>NestedEqual</c> :3763) for a set operation whose both operands are plain-table shapes (cycle 5
    /// covered select/join/grouping/having/IN and the CTE body, not a UNION body). Identical operands
    /// match; a different right-hand predicate does not, and each union reads its own live rows.
    /// </summary>
    [Fact]
    public void RawShapeMatches_UnionBody_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        QueryCommand<long> Left() => Context.From<Pr6Marker>().Where(x => x.Grp == 1).Select(x => x.Id);
        QueryCommand<long> TagTen() => Context.From<Pr6Marker>().Where(x => x.Tag == 10).Select(x => x.Id);
        QueryCommand<long> TagTwenty() => Context.From<Pr6Marker>().Where(x => x.Tag == 20).Select(x => x.Id);

        var unionOne = Left().Union(TagTen());
        var unionOneAgain = Left().Union(TagTen());
        var unionTwenty = Left().Union(TagTwenty());

        Shape(unionOne).Should().NotBeNull("both UNION operands are plain-table shapes");
        Shape(unionOneAgain)!.Matches(Shape(unionOne)!, unionOneAgain).Should()
            .BeTrue("identical UNION operands are the same descriptor");
        Shape(unionTwenty)!.Matches(Shape(unionOne)!, unionTwenty).Should()
            .BeFalse("a different right-hand operand predicate is a different UNION descriptor");

        Run(unionOne).Should().BeEquivalentTo(new long[] { 1, 2, 3 },
            "UNION keeps the shared marker once and adds the tag-10 marker");
        Run(unionTwenty).Should().BeEquivalentTo(new long[] { 1, 2, 4 },
            "the tag-20 operand contributes marker 4 instead of marker 3");
    }

    // ---------------------------------------------------------------------------------------------
    // 26. RawShape.Matches: DISTINCT and ORDER BY + LIMIT
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>RawShape.Matches</c> :3708 <c>IsDistinct</c> and :3737
    /// <c>Sorting</c>/<c>Paging</c> (arms cycle 5 never compared). A DISTINCT pair and an ordered-page
    /// pair each differ in one component; every side executes live and reads its own exact rows.
    /// </summary>
    [Fact]
    public void RawShapeMatches_DistinctAndPagedOrderBy_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        QueryCommand<int> PlainGroups() => Context.From<Pr6Marker>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Grp);
        QueryCommand<int> DistinctGroups() => Context.From<Pr6Marker>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Grp)
            .Distinct();

        Shape(PlainGroups()).Should().NotBeNull("a plain scalar shape is describable");
        Shape(DistinctGroups())!.Matches(Shape(PlainGroups())!, DistinctGroups()).Should()
            .BeFalse("the DISTINCT flag alone is a different descriptor");

        Run(PlainGroups()).Should().Equal(new[] { 1, 1 }, "the plain projection keeps both group-1 rows");
        Run(DistinctGroups()).Should().Equal(new[] { 1 }, "DISTINCT collapses the duplicate");

        QueryCommand<long> FirstTwo() => Context.From<Pr6Marker>()
            .OrderBy(x => x.Id)
            .Limit(2)
            .Select(x => x.Id);
        QueryCommand<long> LastTwo() => Context.From<Pr6Marker>()
            .OrderByDescending(x => x.Id)
            .Limit(2)
            .Select(x => x.Id);
        QueryCommand<long> FirstThree() => Context.From<Pr6Marker>()
            .OrderBy(x => x.Id)
            .Limit(3)
            .Select(x => x.Id);

        Shape(FirstTwo())!.Matches(Shape(LastTwo())!, FirstTwo()).Should()
            .BeFalse("a different sort direction is a different descriptor");
        Shape(FirstTwo())!.Matches(Shape(FirstThree())!, FirstTwo()).Should()
            .BeFalse("a different page limit is a different descriptor");

        Run(FirstTwo()).Should().Equal(new long[] { 1, 2 });
        Run(LastTwo()).Should().Equal(new long[] { 4, 3 });
        Run(FirstThree()).Should().Equal(new long[] { 1, 2, 3 });
    }

    // ---------------------------------------------------------------------------------------------
    // 27. CaptureRawShape: an eligible plain-table graph with where + join + order
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>CaptureRawShape</c> :3892 takes the eligible path for a plain-table graph
    /// carrying a where, an inner join and an ordering (all sources plain, no captured value), returns a
    /// descriptor, and two equal graphs match and reuse one cached plan. Both sides read exact rows.
    /// </summary>
    [Fact]
    public void CaptureRawShape_WhereJoinOrder_ShouldMatchAndReuseThePlan()
    {
        ResetPlanCache();

        QueryCommand<Pr6JoinRow> Joined() => Context.From<Pr6Marker>()
            .Join(Context.From<Pr6Line>(), (m, l) => m.Id == l.MarkerId)
            .Where(p => p.Item1.Grp == 1)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new Pr6JoinRow { Marker = p.Item1.Id, Line = p.Item2.Id, Qty = p.Item2.Qty });

        var equalA = Joined();
        var equalB = Joined();

        Shape(equalA).Should().NotBeNull("a plain-table where+join+order graph is an eligible raw shape");
        Shape(equalB)!.Matches(Shape(equalA)!, equalB).Should()
            .BeTrue("two equal where+join+order graphs capture the same descriptor");

        var prepared = Prepare(equalA);
        Prepare(equalB).Should().BeSameAs(prepared, "the equal raw shape reuses the cached plan holder");

        equalA.GetQueryPlanEqualityComparer().Equals(equalA, equalB).Should()
            .BeTrue("the prepared plan keys agree for the equal capture");

        Rows(prepared).Select(r => (r.Marker, r.Line, r.Qty)).Should()
            .Equal(new[] { (1L, 10L, 1), (1L, 11L, 3), (2L, 12L, 5) },
                "the join reads the group-1 marker/line pairs in join order");
    }

    // ---------------------------------------------------------------------------------------------
    // 28. CaptureRawShape: a second capture differing in one component
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the same eligible plain-table where+join+order graph with only the WHERE
    /// predicate changed captures a different descriptor (<c>RawShape.Matches</c> FALSE), so the
    /// specialized reuse refuses the cached plan holder and each side reads its own live rows. The
    /// prepared-form <c>QueryPlanEqualityComparer</c> is deliberately not asserted here: it compares
    /// prepared expressions only (see the descriptor comment above <c>RawShape</c>), and both prepared
    /// forms are structurally identical because the differing literal is a parameter.
    /// </summary>
    [Fact]
    public void CaptureRawShape_OneComponentDifference_ShouldNotReuseHolder()
    {
        ResetPlanCache();

        // A captured predicate parameter would make the descriptor value-bearing; the two predicates are
        // separate closure-free lambdas so only the raw comparison is under test.
        QueryCommand<Pr6JoinRow> One() => Context.From<Pr6Marker>()
            .Join(Context.From<Pr6Line>(), (m, l) => m.Id == l.MarkerId)
            .Where(p => p.Item1.Grp == 1)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new Pr6JoinRow { Marker = p.Item1.Id, Line = p.Item2.Id, Qty = p.Item2.Qty });

        QueryCommand<Pr6JoinRow> Two() => Context.From<Pr6Marker>()
            .Join(Context.From<Pr6Line>(), (m, l) => m.Id == l.MarkerId)
            .Where(p => p.Item1.Grp == 2)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new Pr6JoinRow { Marker = p.Item1.Id, Line = p.Item2.Id, Qty = p.Item2.Qty });

        var one = One();
        var two = Two();

        Shape(one).Should().NotBeNull("both captures are eligible plain-table graphs");
        Shape(two).Should().NotBeNull();
        Shape(two)!.Matches(Shape(one)!, two).Should()
            .BeFalse("only the WHERE predicate differs, which the raw descriptor compares");

        var preparedOne = Prepare(one);
        Prepare(two).Should().NotBeSameAs(preparedOne,
            "a different captured predicate is a different raw descriptor, so the cached holder is refused");

        Rows(preparedOne).Select(r => (r.Marker, r.Line, r.Qty)).Should()
            .Equal(new[] { (1L, 10L, 1), (1L, 11L, 3), (2L, 12L, 5) });
        Rows(Prepare(two)).Select(r => (r.Marker, r.Line, r.Qty)).Should()
            .Equal(new[] { (3L, 13L, 7) },
                "the group-2 predicate keeps only marker 3, which joins line 13");
    }

    // ---------------------------------------------------------------------------------------------
    // 29. Prepare :31: a detached cache clone has no data context
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// <c>QueryPreparer.Prepare</c> :31 fails closed for a command without a live data context.
    /// <c>CloneForCache()</c> is the public detached-clone factory, so preparing its result must raise
    /// the pinned preparation error before any state is touched; the live command still reads its rows.
    /// </summary>
    [Fact]
    public void PrepareDetachedCacheClone_ShouldRejectPreparationWhileTheLiveCommandRuns()
    {
        ResetPlanCache();

        var command = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var detached = command.CloneForCache();

        FluentActions.Invoking(() => detached.PrepareCommand(CancellationToken.None))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot prepare command in cache*");

        Run(command).Should().Equal(new long[] { 1, 2 }, "the live command still prepares and reads its rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 30. Prepare :36: the command-timeout fallback chain
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The resolved timeout prefers a positive command override (WithCommandTimeout(7)), treats a
    /// zero override as absent, and falls back to a positive context default (UseCommandTimeout(9)) on
    /// a context built for this fact. Every step executes and reads its own rows.
    /// </summary>
    [Fact]
    public void TimeoutResolution_CommandAndContextDefaults_ShouldReadTheSameRows()
    {
        ResetPlanCache();

        var overridden = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .WithCommandTimeout(7);
        Rows(Prepare(overridden)).Should().Equal(new long[] { 1, 2 }, "a positive command override resolves itself");

        var zeroOverride = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .WithCommandTimeout(0);
        Rows(Prepare(zeroOverride)).Should()
            .Equal(new long[] { 1, 2 }, "a zero override is absent and the (null) context default resolves to none");

        using var timed = new SqliteDataContext(
            $"Data Source='{SqliteTestProvider.DatabasePath}'",
            new DataContextBuilder().UseCommandTimeout(9));
        timed.CommandTimeout.Should().Be(9, "the builder installs the context-wide default");

        timed.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(new long[] { 1, 2 }, "the positive context default resolves during preparation and the query reads its rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 31. AttachPrimarySourceLock :341-343: a source lock on a derived JOIN source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the source-level lock guard over the JOIN loop (:341). A lock attached to a
    /// derived join source is not a physical table and must be rejected at preparation with the
    /// APPLY/derived message; the same cross join without the lock executes and reads every pair.
    /// </summary>
    [Fact]
    public void SourceLockOnDerivedJoinSource_ShouldBeRejectedWhileThePlainCrossJoinRuns()
    {
        ResetPlanCache();

        var derived = Context.From<Pr6Line>()
            .Where(l => l.MarkerId <= 2)
            .Select(l => new { l.Id, l.MarkerId });

        var unlocked = Rows(Prepare(Context.From<Pr6Marker>()
            .CrossJoin(derived)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item2.Id)));
        unlocked.Should().HaveCount(12, "four markers cross three derived lines");
        unlocked.Distinct().OrderBy(x => x).Should().Equal(new long[] { 10, 11, 12 });

        FluentActions.Invoking(() => Prepare(Context.From<Pr6Marker>()
                .Join(Context.From(derived), (m, d) => m.Id == d.Id, o => o.ForUpdate())
                .Select(p => p.Item1.Id)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*can only target a physical table source, not an APPLY/derived/table-valued join source*");
    }

    // ---------------------------------------------------------------------------------------------
    // 32. TransferNestedFilterSkips :374-378: a skip collected by a nested derived command
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A bound raw source declares only <c>id</c>, so the registered <c>created</c>-dependent filter is
    /// skipped with a <c>MissingColumns</c> reason while the derived child prepares. The child's skip is
    /// transferred to the owning statement (:374-378) instead of replaying on a later preparation; the
    /// live rows prove the filter was skipped (every kind row is visible).
    /// </summary>
    [Fact]
    public void NestedFilterSkipTransfer_BoundDerivedSource_ShouldSkipTheFilterAndReadEveryRow()
    {
        ResetPlanCache();

        var inner = Context.FromSql("select id, created from pr6_kinds")
            .BindEntity<Pr6KindsMonth>(["id"])
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Rows(Prepare(Context.From(inner).OrderBy(x => x).Select(x => x)))
            .Should().Equal(new long[] { 1, 2, 3, 4 },
                "the declared shape lacks 'created', so the bound filter is skipped and every kind row is read");
    }

    // ---------------------------------------------------------------------------------------------
    // 33. PrepareCtes :407-408: a fresh typed recursive step body
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A typed recursive CTE whose step body has never been prepared takes the
    /// <c>!cte.Query.IsPrepared</c> arm (:407) and prepares the step before validating the anchor/step
    /// contract; the bounded series is materialized by SQLite.
    /// </summary>
    [Fact]
    public void TypedRecursiveCte_FreshStep_ShouldPrepareTheBodyAndMaterializeTheSeries()
    {
        ResetPlanCache();

        var nums = Context.From<Pr6Marker>()
            .Where(x => x.Id == 1)
            .Select(x => x.Id)
            .AsRecursiveCte("pr6_typed_nums", self => Context.From(self)
                .Where(n => n < 5)
                .Select(n => n + 1));

        var rows = Context.From(nums).Limit(20).Select(n => n).ToList();

        rows.OrderBy(n => n).Should().Equal(new long[] { 1, 2, 3, 4, 5 },
            "the fresh step body is prepared once and the anchor/step series materializes");
    }

    // ---------------------------------------------------------------------------------------------
    // 34. PrepareHints :479: hints present on the no-hash (uncached) route
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// With statement hints present and the call-local no-hash route active (<c>storeInCache: false</c>),
    /// <c>PrepareHints</c> returns at :479 without folding a hint hash. SQLite then rejects the hint at
    /// SQL assembly with its pinned message, proving the preparation path ran first; the unhinted
    /// counterpart reads its rows.
    /// </summary>
    [Fact]
    public void QueryHintUncachedPreparation_ShouldReachTheHintGuardBeforeSqlAssembly()
    {
        ResetPlanCache();

        var hinted = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Hint("recompile");

        FluentActions.Invoking(() => Prepare(hinted, storeInCache: false))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Query hints are not supported by this SQL dialect*");

        Run(Context.From<Pr6Marker>().Where(x => x.Id <= 2).OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2 }, "the unhinted live counterpart still reads its rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 35. JSON shape capture :1029-1037 through the public WriteJson terminal
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A nested anonymous projection is captured as a JSON shape and its leaves are lowered into the
    /// select list, so the JSON-only plan-hash block (:1029-1037) runs and the streamed document equals
    /// the serializer's document for the same values.
    /// </summary>
    [Fact]
    public void JsonNestedProjection_ShouldCaptureTheShapeAndStreamTheRows()
    {
        ResetPlanCache();

        var command = Context.From<Pr6Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Grp, x.Tag } });

        using var buffer = new MemoryStream();
        command.WriteJson(buffer);

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1L, Child = new { Grp = 1, Tag = 10 } },
            new { Id = 2L, Child = new { Grp = 1, Tag = 20 } },
            new { Id = 3L, Child = new { Grp = 2, Tag = 10 } },
            new { Id = 4L, Child = new { Grp = 2, Tag = 20 } },
        });

        Encoding.UTF8.GetString(buffer.ToArray()).Should()
            .Be(expected, "the captured nested JSON shape streams one object per marker with its child object");
    }

    // ---------------------------------------------------------------------------------------------
    // 36. Cached -> uncached -> cached on ONE context (join, grouping, having, sorting)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Same-context differential: the join/write/group/having/sort plan-hash gates are exercised through
    /// a cached preparation, an uncached preparation (<c>storeInCache: false</c>, so the
    /// <c>!_dontCache &amp;&amp; !noHash</c> guards take their no-hash arm) and a cached preparation again.
    /// The three runs must agree on the rows, and the two cached runs must reuse one plan holder; the
    /// other holder belongs to the uncached route.
    /// </summary>
    [Fact]
    public void CachedUncachedCachedDifferential_JoinAndGrouping_ShouldAgreeOnTheSameContext()
    {
        ResetPlanCache();

        QueryCommand<int> Joined() => Context.From<Pr6Marker>()
            .Join(Context.From<Pr6Line>(), (m, l) => m.Id == l.MarkerId)
            .Where(p => p.Item1.Grp == 1)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item2.Qty);
        QueryCommand<int> Grouped() => Context.From<Pr6Marker>()
            .GroupBy(x => new { x.Grp })
            .Having(g => g.Grp > 0)
            .Select(g => g.Grp);

        var joinedCached = Prepare(Joined());
        var joinedRows = Rows(joinedCached);
        joinedRows.Should().Equal(new[] { 1, 3, 5 }, "the group-1 markers join their three lines in line order");

        var joinedUncached = Prepare(Joined(), storeInCache: false);
        joinedUncached.Should().NotBeSameAs(joinedCached, "the uncached route builds a call-local plan");
        Rows(joinedUncached).Should().Equal(joinedRows, "the uncached preparation reads the same rows");

        var joinedCachedAgain = Prepare(Joined());
        Rows(joinedCachedAgain).Should().Equal(joinedRows);
        joinedCachedAgain.Should().BeSameAs(joinedCached, "the third, cached preparation reuses the first holder");

        var groupedCached = Prepare(Grouped());
        Rows(groupedCached).Should().Equal(new[] { 1, 2 });
        Rows(Prepare(Grouped(), storeInCache: false)).Should()
            .Equal(new[] { 1, 2 }, "the uncached grouped/having preparation reads the same rows");
        Prepare(Grouped()).Should().BeSameAs(groupedCached, "the cached grouped preparation reuses its holder");
    }

    // ---------------------------------------------------------------------------------------------
    // 37. FilterScope setter :540,554-586: reset preparation on a real scope change
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The public <c>IgnoreFilters</c> property is the filter-scope setter. An equal assignment is a
    /// no-op (:578), a real change discards the prepared filter injection (:583-584) and the cleared
    /// scope prepares the filtered plan again. Every step reads its own live rows.
    /// </summary>
    [Fact]
    public void FilterScopeProperty_AfterPreparation_ShouldResetAndReadBothScopes()
    {
        ResetPlanCache();

        var command = Context.FromSql("select id, created from pr6_kinds")
            .BindEntity<Pr6KindsMonth>(["id", "created"])
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        command.IgnoreFilters = false;
        Rows(Prepare(command)).Should().Equal(new long[] { 2, 3 }, "the registered month filter keeps July and November");

        command.IgnoreFilters = true;
        Rows(Prepare(command)).Should()
            .Equal(new long[] { 1, 2, 3, 4 }, "the changed scope discards the prepared filter injection");

        command.IgnoreFilters = false;
        Rows(Prepare(command)).Should().Equal(new long[] { 2, 3 }, "clearing the scope prepares the filtered plan again");
    }

    // ---------------------------------------------------------------------------------------------
    // 38. CaptureRawShape :3901,3912,3942: ineligible graphs return null
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Three ineligibility arms of <c>CaptureRawShape</c>: an already-prepared command (:3901), a lazy
    /// temporary-table source (:3912) and a CTE body that declares its own CTEs (:3942). Each captured
    /// descriptor is null, while the same graph executes live (including the hoisted nested CTE).
    /// </summary>
    [Fact]
    public void CaptureRawShape_IneligibleGraphs_ShouldReturnNullWhileTheirLiveFormsRun()
    {
        ResetPlanCache();

        var prepared = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        Rows(Prepare(prepared)).Should().Equal(new long[] { 1, 2 });
        Shape(prepared).Should().BeNull("an already-prepared command is not capturable");

        var temp = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .Select(x => new { x.Id })
            .AsTempTable();
        var tempCommand = Context.From(temp)
            .OrderBy(t => t.GetInt64("id"))
            .Select(t => t.GetInt64("id"));
        Shape(tempCommand).Should().BeNull("a lazy temporary-table source is not capturable");
        Run(tempCommand).Should().Equal(new long[] { 1, 2 }, "the temporary table is materialized and read");

        var innerScope = Context.With("pr6_nested_inner", Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .Select(x => x.Id));
        var body = innerScope.From("pr6_nested_inner")
            .OrderBy(t => t.GetInt64("id"))
            .Select(t => t.GetInt64("id"));
        var outerScope = Context.With("pr6_nested_outer", body);
        var nestedCommand = outerScope.From("pr6_nested_outer")
            .OrderBy(t => t.GetInt64("id"))
            .Select(t => t.GetInt64("id"));
        Shape(nestedCommand).Should().BeNull("a CTE body that declares its own CTEs is not a flat capturable graph");
        Run(nestedCommand).Should().Equal(new long[] { 1, 2 }, "the hoisted nested declaration still executes");
    }

    // ---------------------------------------------------------------------------------------------
    // 39. HasApplicableFiltersInRawGraph :4091-4151,4179: every filter-bearing source kind
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The metadata-only pre-preparation filter probe: the main source, a join source, a set-operation
    /// operand and a CTE body each make the raw graph ineligible, while an all-filters scope leaves no
    /// applicable filter. Every reported graph also executes and reads its own rows.
    /// </summary>
    [Fact]
    public void RawGraphFilterProbe_ShouldReportApplicableFiltersPerSourceAndStayLive()
    {
        ResetPlanCache();

        var filtered = Context.From<Pr6KindsMonth>()
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var filteredShape = Shape(filtered);
        filteredShape.Should().NotBeNull("a filtered plain-table graph is still describable");
        QueryCommand.HasApplicableFiltersInRawGraph(filteredShape!, Context)
            .Should().BeTrue("the registered filter on the main source makes the raw graph ineligible");
        Run(filtered).Should().Equal(new long[] { 2, 3 });

        var unfiltered = Context.From<Pr6KindsMonth>()
            .IgnoreFilters()
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        QueryCommand.HasApplicableFiltersInRawGraph(Shape(unfiltered)!, Context)
            .Should().BeFalse("the all-filters scope leaves no applicable filter");
        Run(unfiltered).Should().Equal(new long[] { 1, 2, 3, 4 });

        var joined = Context.From<Pr6Marker>()
            .Join(Context.From<Pr6KindsMonth>(), (m, k) => m.Id == k.Id)
            .OrderBy(p => p.Item1.Id)
            .Select(p => p.Item1.Id);
        QueryCommand.HasApplicableFiltersInRawGraph(Shape(joined)!, Context)
            .Should().BeTrue("the filtered entity is a join source, so the whole graph is ineligible");
        Run(joined).Should().Equal(new long[] { 2, 3 }, "the joined filter reads only the filtered markers");

        var union = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .Select(x => x.Id)
            .Union(Context.From<Pr6KindsMonth>().Select(x => x.Id));
        QueryCommand.HasApplicableFiltersInRawGraph(Shape(union)!, Context)
            .Should().BeTrue("the right set-operation operand carries the filter, so the union graph is ineligible");
        Run(union).Should().BeEquivalentTo(new long[] { 1, 2, 3 }, "the filtered operand contributes July and November");

        var cteScope = Context.With("pr6_probe_cte", Context.From<Pr6KindsMonth>().OrderBy(x => x.Id).Select(x => x.Id));
        var cteCommand = cteScope.From("pr6_probe_cte")
            .OrderBy(t => t.GetInt64("id"))
            .Select(t => t.GetInt64("id"));
        QueryCommand.HasApplicableFiltersInRawGraph(Shape(cteCommand)!, Context)
            .Should().BeTrue("a filter-bearing CTE body makes the whole graph ineligible");
        Run(cteCommand).Should().Equal(new long[] { 2, 3 }, "the CTE body keeps its filter");
    }

    // ---------------------------------------------------------------------------------------------
    // 40. QueryCommand.Clone.cs :174-185: a live clone prepares independently
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// <c>Clone()</c> takes the live copy-constructor path (<c>CreateSelf</c> :174-185). The clone keeps
    /// the shape but owns its preparation state, so it prepares and reads the same rows as the original.
    /// </summary>
    [Fact]
    public void LiveClone_ShouldPrepareIndependentlyAndReadTheSameRows()
    {
        ResetPlanCache();

        var original = Context.From<Pr6Marker>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var clone = (QueryCommand<long>)original.Clone();

        Rows(Prepare(original)).Should().Equal(new long[] { 1, 2 });
        Rows(Prepare(clone)).Should().Equal(new long[] { 1, 2 }, "the independent live clone reads the same rows");
    }

    /// <summary>Projection subset DTO used by the member-init fact.</summary>
    public sealed class Pr6Subset
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    [SqlTable("pr6_marker")]
    public sealed class Pr6Marker
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("score")]
        public int? Score { get; set; }
    }

    [SqlTable("pr6_line")]
    public sealed class Pr6Line
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("marker_id")]
        public long MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    /// <summary>Joined-projection shape for the raw-descriptor facts (a named type, never a tuple: #307).</summary>
    public sealed class Pr6JoinRow
    {
        public long Marker { get; set; }

        public long Line { get; set; }

        public int Qty { get; set; }
    }

    /// <summary>Bound-source entity whose filter walks the <c>DateTime.Month</c> transparent member.</summary>
    [SqlTable("pr6_kinds")]
    public sealed class Pr6KindsMonth
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("created")]
        public DateTime Created { get; set; }
    }

    /// <summary>Bound-source entity whose filter walks <c>Nullable&lt;Guid&gt;.Value</c>.</summary>
    [SqlTable("pr6_kinds")]
    public sealed class Pr6KindsTokenValue
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("token")]
        public Guid? Token { get; set; }
    }

    /// <summary>Unmapped base type of <see cref="Pr6KindsInherited"/>: its mapped member is inherited.</summary>
    public abstract class Pr6KindsBase
    {
        [Column("created")]
        public DateTime BaseCreated { get; set; }
    }

    /// <summary>Bound-source entity whose filter chain starts on a base-class member.</summary>
    [SqlTable("pr6_kinds")]
    public sealed class Pr6KindsInherited : Pr6KindsBase
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
    }
}
