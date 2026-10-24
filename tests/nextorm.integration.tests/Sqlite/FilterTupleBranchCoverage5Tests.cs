using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// QueryPreparer filter/projection/raw-shape branch coverage (cycle 5, cluster C3/D3) executed against a
/// live SQLite file database. Every fact runs real queries over the class-owned <c>ft5_marker</c> /
/// <c>ft5_line</c> tables (dropped, created and seeded by this class only) and asserts the returned
/// rows; no <see cref="InMemoryDataContext"/>, no reflection and no global seed data.
///
/// <para>
/// Targeted components of <c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c>:
/// <c>InjectMainSourceFilters</c> :2110 and <c>FilterColumnDependencyVisitor</c> :2476 (constructed
/// :2233 in the <c>BindEntity</c> path) with <c>TryResolvePhysicalColumn</c> :2633,
/// <c>IsTransparentMember</c> :2653 (the <c>Nullable&lt;T&gt;.Value</c> and <c>string.Length</c> arms),
/// <c>TryMapColumn</c> :2670, <c>TryMapAnyColumn</c> :2685 and <c>TryFormatColumn</c> :2697 (the
/// naming-convention arm <c>IsColumnNameAuto &amp;&amp; _convention is not null</c>); the compatibility
/// decision <c>TryGetBoundFilterBody</c> :2215 (compatible, <c>MissingColumns</c> and
/// <c>UndeterminedColumns</c> skips); <c>PrepareColumns</c> :507 for the identity/entity, scalar,
/// scalar-subquery, joined-item and grouped derivative projections; and <c>RawShape.Matches</c> :3703
/// for the select-list, join, grouping, having, IN-shape and CTE-body arms.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Sqlite/PreparerBatteryBranchCoverage4Tests.cs</c>
/// owns the grouping-set, paging/WITH TIES, named-window (name/count/order/frame), CTE declaration
/// length, tables-in-scope hint string lists and extreme-row raw-shape facts;
/// <c>Sqlite/PreparationDifferentialBranchCoverage4Tests.cs</c> owns the filter scope, FOR JSON clause,
/// unkeyed derived-source scans and projection-cast arms; <c>Sqlite/RawShapeBranchCoverage2Tests.cs</c>
/// owns the raw/bound/derived ROOT-source eligibility and the <c>FromBinding</c> arm. This class never
/// repeats a window, hint, string-list, grouping-set or root-source-eligibility fact.
/// </para>
///
/// <para>
/// Recorded, never fabricated: a derived ROOT source is not capture-eligible
/// (<c>CaptureRawShape</c> :3908 requires a plain table source), so its <c>Matches</c> arm cannot be
/// compared at all; cycle 2 owns that ineligibility arm. The public reachability was observed in this
/// class, so it is recorded in <c>rv1/inputs/deferred.json</c> instead of being asserted here.
/// </para>
/// </summary>
[Collection("Query plan cache")]
public sealed class FilterTupleBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists ft5_line;
        drop table if exists ft5_marker;
        create table ft5_marker
        (
            id integer primary key,
            name text not null,
            grp int not null,
            tag int not null,
            score int null,
            snake_col int not null
        );
        create table ft5_line
        (
            id integer primary key,
            marker_id int not null,
            qty int not null
        );
        insert into ft5_marker (id, name, grp, tag, score, snake_col) values
            (1, 'alpha', 1, 10, 5,    100),
            (2, 'beta',  1, 20, null, 100),
            (3, 'gamma', 2, 10, 9,    200),
            (4, 'delta', 2, 20, null, 200);
        insert into ft5_line (id, marker_id, qty) values
            (10, 1, 1),
            (11, 1, 3),
            (12, 2, 5),
            (13, 3, 7);
        drop table if exists ft5_kinds;
        create table ft5_kinds
        (
            id integer primary key,
            created text not null,
            duration_ticks integer not null,
            token text null,
            score int null,
            name text not null,
            base_code int not null
        );
        -- Duration is stored as integer ticks (no native duration on SQLite): 60/1/120/5 minutes.
        insert into ft5_kinds (id, created, duration_ticks, token, score, name, base_code) values
            (1, '2024-03-01 10:00:00', 36000000000, '11111111-1111-1111-1111-111111111111', 7,    'alpha', 1),
            (2, '2023-07-02 11:00:00', 600000000,   '22222222-2222-2222-2222-222222222222', 2,    'beta',  2),
            (3, '2024-11-03 12:00:00', 72000000000, '33333333-3333-3333-3333-333333333333', 9,    'gamma', 1),
            (4, '2025-01-04 13:00:00', 3000000000,  null,                                   null, 'd',     2);
        """;

    private static int _filtersRegistered;

    public FilterTupleBranchCoverage5Tests()
    {
        ExecuteRaw(Ddl);

        // The first registration per entity type wins process-wide, so register once for the class and
        // BEFORE any BindEntity call (BindEntity auto-publishes an unfiltered mapping otherwise).
        if (Interlocked.Exchange(ref _filtersRegistered, 1) == 0)
        {
            Context.From<Ft5Simple>(b => b.HasQueryFilter(e => e.Grp == 1));
            Context.From<Ft5Nullable>(b => b.HasQueryFilter(e => e.Score!.Value > 3));
            Context.From<Ft5Length>(b => b.HasQueryFilter(e => e.Name.Length > 4));
            Context.From<Ft5Convention>(b => b.HasQueryFilter(e => e.SnakeCol == 100));
            Context.From<Ft5Bound>(b => b
                .HasQueryFilter("grp", (e, _) => e.Grp == 2)
                .HasQueryFilter("score", (e, _) => e.Score != null));
            Context.From<Ft5DateTime>(b => b.HasQueryFilter(e => e.Created.Year == 2024));
            Context.From<Ft5Duration>(b => b.HasQueryFilter(e => e.Duration > TimeSpan.FromMinutes(30)));
            Context.From<Ft5Token>(b => b.HasQueryFilter(e => e.Token != null));
            Context.From<Ft5ScoreValue>(b => b.HasQueryFilter(e => e.Score!.Value > 4));
            Context.From<Ft5NameLength>(b => b.HasQueryFilter(e => e.Name.Length > 4));
            Context.From<Ft5NamePrefix>(b => b.HasQueryFilter(e => e.Name.StartsWith("ga")));
            Context.From<Ft5BaseMember>(b => b.HasQueryFilter(e => e.BaseCode == 1));
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

    /// <summary>The rendered SQL of a prepared command.</summary>
    private static string SqlOf<TResult>(IPreparedQueryCommand<TResult> prepared)
        => ((DbPreparedQueryCommand<TResult>)prepared).DbCommand.CommandText;

    // ---------------------------------------------------------------------------------------------
    // 1. Query filter: a simple mapped column, with the IgnoreFilters counterpart
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A fluent <c>HasQueryFilter</c> over one mapped column restricts the live read to the matching
    /// rows, and <c>IgnoreFilters()</c> on the same entity returns every row.
    /// </summary>
    [Fact]
    public void Filter_SimpleColumnAndIgnoreFilters_ShouldFilterAndReturnAll()
    {
        ResetPlanCache();

        Run(Context.From<Ft5Simple>().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2 }, "the registered Grp == 1 filter restricts the source");
        Run(Context.From<Ft5Simple>().IgnoreFilters().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the registered predicate");
        Run(Context.From<Ft5Simple>().Where(x => x.Grp == 2).OrderBy(x => x.Id).Select(x => x.Id))
            .Should().BeEmpty("the injected filter AND the explicit predicate leave no row");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Query filter: the Nullable<T>.Value transparent member
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A filter whose root member chain goes through <c>Nullable&lt;T&gt;.Value</c> is transparent for
    /// the dependency walk and renders against the underlying column; the live rows follow the score.
    /// </summary>
    [Fact]
    public void Filter_NullableValueTransparentMember_ShouldFilterOnTheColumn()
    {
        ResetPlanCache();

        Run(Context.From<Ft5Nullable>().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Equal(new long[] { 1, 3 }, "only the non-null scores above 3 match");
        Run(Context.From<Ft5Nullable>().IgnoreFilters().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "the transparent member is the whole filter");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Query filter: the string.Length transparent member
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A filter rooted at <c>string.Length</c> is transparent for the dependency walk; SQLite renders
    /// <c>length(name)</c> and the live rows follow the name lengths.
    /// </summary>
    [Fact]
    public void Filter_StringLengthTransparentMember_ShouldFilterOnTheColumn()
    {
        ResetPlanCache();

        Run(Context.From<Ft5Length>().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Equal(new long[] { 1, 3, 4 }, "only names longer than four characters match");
        Run(Context.From<Ft5Length>().IgnoreFilters().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "the length filter is the whole predicate");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Bound raw source with an explicit column set: applied, missing and undetermined filters
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A raw source bound with <c>BindEntity&lt;T&gt;(["id","grp"])</c> applies the filter whose column
    /// is declared, skips the one whose column is missing (the live rows prove the skip, not a silent
    /// error) and skips a column-dependent filter against an EMPTY declared shape; IgnoreFilters()
    /// disables both.
    /// </summary>
    [Fact]
    public void Filter_BoundSourceExplicitColumns_ShouldApplyCompatibleAndSkipMissing()
    {
        ResetPlanCache();

        var bound = Context.FromSql("select id, grp, score from ft5_marker")
            .BindEntity<Ft5Bound>(["id", "grp"])
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Run(bound).Should().Equal(new long[] { 3, 4 },
            "grp == 2 is applied while score IS NOT NULL is skipped because score is not declared");

        var ignored = Context.FromSql("select id, grp, score from ft5_marker")
            .BindEntity<Ft5Bound>(["id", "grp"])
            .IgnoreFilters()
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Run(ignored).Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() disables the bound-source filters");

        var emptyShape = Context.FromSql("select id, grp, score from ft5_marker")
            .BindEntity<Ft5Bound>(Array.Empty<string>())
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Run(emptyShape).Should().Equal(new long[] { 1, 2, 3, 4 },
            "an empty declared shape cannot prove any column dependency, so both filters are skipped");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Bound raw source: the naming-convention arm of the dependency formatter
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The dependency formatter applies the resolved naming convention only to an AUTO column name: with
    /// <c>SnakeCaseNamingConvention</c> the filter's <c>SnakeCol</c> resolves to the declared
    /// <c>snake_col</c> and is applied; without the convention the name does not match the declared
    /// shape and the filter is skipped. Both sides execute live.
    /// </summary>
    [Fact]
    public void Filter_BoundSourceNamingConvention_ShouldMatchTheConventionalColumn()
    {
        ResetPlanCache();

        var conventional = Context.FromSql("select id, snake_col from ft5_marker")
            .BindEntity<Ft5Convention>(["id", "snake_col"])
            .WithNamingConvention(SnakeCaseNamingConvention.Instance)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Run(conventional).Should().Equal(new long[] { 1, 2 }, "the convention maps SnakeCol to snake_col and the filter applies");

        var unconventioned = Context.FromSql("select id, snake_col from ft5_marker")
            .BindEntity<Ft5Convention>(["id", "snake_col"])
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Run(unconventioned).Should().Equal(new long[] { 1, 2, 3, 4 },
            "without the convention the auto name SnakeCol is not among the declared columns, so the filter is skipped");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. PrepareColumns: an entity projection (whole-row materialisation)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A whole-entity projection prepares its columns from the mapping and materialises every live row.
    /// </summary>
    [Fact]
    public void PrepareColumns_EntityProjection_ShouldMaterializeRows()
    {
        ResetPlanCache();

        var rows = Run(Context.From<Ft5Marker>().OrderBy(x => x.Id).ToCommand());

        rows.Should().HaveCount(4);
        rows.Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4 });
        rows.Select(r => r.Name).Should().Equal("alpha", "beta", "gamma", "delta");
        rows[1].Score.Should().BeNull("the nullable column survives the whole-entity materialisation");
        rows[0].Score.Should().Be(5);
    }

    // ---------------------------------------------------------------------------------------------
    // 7. PrepareColumns: a scalar projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A single-column scalar projection prepares exactly one select expression and reads every row.
    /// </summary>
    [Fact]
    public void PrepareColumns_ScalarProjection_ShouldReadOneColumn()
    {
        ResetPlanCache();

        Run(Context.From<Ft5Marker>().OrderBy(x => x.Id).Select(x => x.Grp))
            .Should().Equal(new[] { 1, 1, 2, 2 }, "the scalar projection reads the mapped grp column");
        Run(Context.From<Ft5Marker>().OrderBy(x => x.Id).Select(x => x.Score))
            .Should().Equal(new int?[] { 5, null, 9, null }, "a nullable scalar projection keeps the null row");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. PrepareColumns: a scalar subquery projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A correlated scalar-subquery projection prepares as one select expression and reads the first
    /// matching line id per marker row.
    /// </summary>
    [Fact]
    public void PrepareColumns_ScalarSubqueryProjection_ShouldReadTheCorrelatedValue()
    {
        ResetPlanCache();

        var rows = Run(Context.From<Ft5Marker>()
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => Context.From<Ft5Line>().Where(l => l.MarkerId == x.Id).Select(l => l.Id).First()));

        rows.Should().Equal(new long[] { 10, 12, 13 }, "each marker row reads the first line id of its own correlated subquery");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. PrepareColumns: a joined whole-entity item projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A whole-entity projection of a joined item (<c>Select(p =&gt; p.Item1)</c>) expands that item's
    /// mapped columns and materialises the joined rows.
    /// </summary>
    [Fact]
    public void PrepareColumns_JoinedEntityProjection_ShouldMaterializeTheItem()
    {
        ResetPlanCache();

        var rows = Run(Context.From<Ft5Marker>()
            .Join(Context.From<Ft5Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1));

        rows.Select(r => r.Id).Should().Equal(new long[] { 1, 1, 2, 3 }, "one entity per joined row");
        rows.Select(r => r.Name).Should().Equal("alpha", "alpha", "beta", "gamma");
        rows.Select(r => r.Score).Should().Equal(new int?[] { 5, 5, null, 9 });
    }

    // ---------------------------------------------------------------------------------------------
    // 10. PrepareColumns: a projection over a grouped shape
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A grouped derivative projection prepares its own select list over the derived source and reads
    /// the grouped rows; the grouped command itself is executed too.
    /// </summary>
    [Fact]
    public void PrepareColumns_GroupedShapeProjection_ShouldReadTheGroupRows()
    {
        ResetPlanCache();

        var grouped = Context.From<Ft5Marker>()
            .GroupBy(x => new { x.Grp })
            .Select(g => new { g.Grp, Cnt = SqlFunctions.Sql.count() });

        var groupedRows = Run(grouped);
        groupedRows.Select(r => r.Grp).Should().Equal(new[] { 1, 2 });
        groupedRows.Select(r => r.Cnt).Should().Equal(new[] { 2, 2 });

        var projected = Context.From(grouped)
            .OrderBy(x => x.Grp)
            .Select(x => x.Grp);

        Run(projected).Should().Equal(new[] { 1, 2 }, "the outer projection reads the derived grouped rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. RawShape.Matches: select-list, join, grouping, having and IN-shape arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Two structurally equal commands compare equal through <c>RawShape.Matches</c> and reuse ONE
    /// cached plan holder; each one-field difference (select list, join type, grouping list, HAVING
    /// predicate, IN value list) compares unequal, gets its own holder with different SQL, and both
    /// sides of every pair are executed against the live database.
    /// </summary>
    [Fact]
    public void RawShapeMatches_SelectJoinGroupingHavingInArms_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        // Select-list arm: same source/order/result type, a different projected column.
        QueryCommand<long> SelectId() => Context.From<Ft5Marker>().OrderBy(x => x.Id).Select(x => x.Id);
        QueryCommand<long> SelectTag() => Context.From<Ft5Marker>().OrderBy(x => x.Id).Select(x => (long)x.Tag);

        var idA = SelectId();
        var idB = SelectId();
        var tag = SelectTag();

        Shape(idA).Should().NotBeNull("a plain-table scalar select is a capturable raw shape");
        Shape(idB)!.Matches(Shape(idA)!, idB).Should().BeTrue("identical select lists are the same descriptor");
        Shape(tag)!.Matches(Shape(idA)!, tag).Should().BeFalse("a different projected column is a different select list");

        var idHolder = Prepare(idA);
        Rows(idHolder).Should().Equal(new long[] { 1, 2, 3, 4 });
        Prepare(idB).Should().BeSameAs(idHolder, "the equal raw shape reuses the cached plan holder");

        var tagHolder = Prepare(tag);
        tagHolder.Should().NotBeSameAs(idHolder, "a different select list is a cache miss");
        SqlOf(tagHolder).Should().NotBe(SqlOf(idHolder), "the miss renders its own select list");
        Rows(tagHolder).Should().Equal(new long[] { 10, 20, 10, 20 });

        // Join arm: INNER vs LEFT changes the join descriptor, the rows follow.
        QueryCommand<long> Inner() => Context.From<Ft5Marker>()
            .Join(Context.From<Ft5Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1.Id);
        QueryCommand<long> Left() => Context.From<Ft5Marker>()
            .LeftJoin(Context.From<Ft5Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1.Id);

        var inner = Inner();
        var innerAgain = Inner();
        var left = Left();

        Shape(innerAgain)!.Matches(Shape(inner)!, innerAgain).Should().BeTrue("identical join chains are the same descriptor");
        Shape(left)!.Matches(Shape(inner)!, left).Should().BeFalse("a different join type is a different descriptor");

        var innerHolder = Prepare(inner);
        Rows(innerHolder).Should().Equal(new long[] { 1, 1, 2, 3 });
        Prepare(innerAgain).Should().BeSameAs(innerHolder, "the equal joined shape reuses its cached holder");
        Rows(Prepare(left)).Should().Equal(new long[] { 1, 1, 2, 3, 4 }, "the LEFT join adds the unmatched marker");

        // Grouping arm: a different grouping list is a different descriptor.
        QueryCommand<int> GroupByGrp() => Context.From<Ft5Marker>()
            .GroupBy(x => new { x.Grp })
            .Select(g => SqlFunctions.Sql.count());
        QueryCommand<int> GroupByTag() => Context.From<Ft5Marker>()
            .GroupBy(x => new { x.Tag })
            .Select(g => SqlFunctions.Sql.count());

        Shape(GroupByTag())!.Matches(Shape(GroupByGrp())!, GroupByTag()).Should().BeFalse("a different grouping list is a different descriptor");
        Run(GroupByGrp()).Should().Equal(new[] { 2, 2 });
        Run(GroupByTag()).Should().Equal(new[] { 2, 2 });

        // HAVING arm: the grouping prefix is equal, only the predicate differs.
        QueryCommand<long> HavingOne() => Context.From<Ft5Marker>()
            .GroupBy(x => new { x.Grp })
            .Having(g => SqlFunctions.Sql.count() > 1)
            .Select(g => SqlFunctions.Sql.sum(g.Id));
        QueryCommand<long> HavingThree() => Context.From<Ft5Marker>()
            .GroupBy(x => new { x.Grp })
            .Having(g => SqlFunctions.Sql.count() > 3)
            .Select(g => SqlFunctions.Sql.sum(g.Id));

        Shape(HavingThree())!.Matches(Shape(HavingOne())!, HavingThree()).Should().BeFalse("a different HAVING predicate is a different descriptor");
        Run(HavingOne()).Should().Equal(new long[] { 3, 7 });
        Run(HavingThree()).Should().BeEmpty();

        // IN-shape arm: the same predicate shape with a different literal list.
        QueryCommand<long> TagInBoth() => Context.From<Ft5Marker>()
            .Where(x => SqlFunctions.Sql.@in(x.Tag, 10, 20))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        QueryCommand<long> TagInOne() => Context.From<Ft5Marker>()
            .Where(x => SqlFunctions.Sql.@in(x.Tag, 10))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Shape(TagInOne())!.Matches(Shape(TagInBoth())!, TagInOne()).Should().BeFalse("a different IN value-list shape is a different descriptor");
        Run(TagInBoth()).Should().Equal(new long[] { 1, 2, 3, 4 });
        Run(TagInOne()).Should().Equal(new long[] { 1, 3 });
    }

    // ---------------------------------------------------------------------------------------------
    // 12. RawShape.Matches: the CTE body arm
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The CTE declaration body is compared recursively: an identical body matches, a different body
    /// predicate does not, and both CTE reads execute their own live rows.
    /// </summary>
    [Fact]
    public void RawShapeMatches_CteBodyArms_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        var oneBody = Context.From<Ft5Marker>().Where(m => m.Grp == 1).Select(m => new { m.Id });
        var oneAgainBody = Context.From<Ft5Marker>().Where(m => m.Grp == 1).Select(m => new { m.Id });
        var twoBody = Context.From<Ft5Marker>().Where(m => m.Grp == 2).Select(m => new { m.Id });

        var one = Context.With("ft5_c", oneBody).From("ft5_c").OrderBy(t => t["id"].AsInt).Select(t => t["id"].AsInt);
        var oneAgain = Context.With("ft5_c", oneAgainBody).From("ft5_c").OrderBy(t => t["id"].AsInt).Select(t => t["id"].AsInt);
        var two = Context.With("ft5_c", twoBody).From("ft5_c").OrderBy(t => t["id"].AsInt).Select(t => t["id"].AsInt);

        Shape(one).Should().NotBeNull("a plain-table CTE declaration is a capturable raw shape");
        Shape(oneAgain)!.Matches(Shape(one)!, oneAgain).Should().BeTrue("an identical CTE body is the same descriptor");
        Shape(two)!.Matches(Shape(one)!, two).Should().BeFalse("a different CTE body predicate is a different descriptor");

        var oneHolder = Prepare(one);
        Rows(oneHolder).Should().Equal(new[] { 1, 2 });
        Prepare(oneAgain).Should().BeSameAs(oneHolder, "the equal CTE body reuses the cached plan holder");
        Rows(Prepare(two)).Should().Equal(new[] { 3, 4 });
    }

    // ---------------------------------------------------------------------------------------------
    // 13. ScanUnkeyedTupleInFrom: an unkeyed tuple value list in a derived source's projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>ScanUnkeyedTupleInFrom</c> :3007 takes its <c>from.SubQuery</c> branch,
    /// because the tuple membership sits in the DERIVED source's projection and is therefore not folded
    /// into that source's own plan key. Two executions of the same derived shape with a different
    /// captured tuple list must each read their own rows: the first list matches only id 1, the grown
    /// list matches id 1 and 2. A stale cached plan would keep the first result.
    /// </summary>
    [Fact]
    public void ScanUnkeyedTupleInFrom_DerivedTupleProjection_ShouldReadEachCapturedList()
    {
        ResetPlanCache();

        var pairs = new List<(long Id, int Tag)> { (1L, 10) };

        var innerOne = Context.From<Ft5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Tag, Hit = pairs.Contains(new ValueTuple<long, int>(x.Id, x.Tag)) });
        var outerOne = Context.From(innerOne)
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Hit });

        var oneRows = Run(outerOne);
        oneRows.Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4 });
        oneRows.Select(r => r.Hit).Should().Equal(new[] { true, false, false, false },
            "the one-entry tuple list matches only marker 1");

        pairs.Add((2L, 20));

        var innerTwo = Context.From<Ft5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Tag, Hit = pairs.Contains(new ValueTuple<long, int>(x.Id, x.Tag)) });
        var outerTwo = Context.From(innerTwo)
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Hit });

        var twoRows = Run(outerTwo);
        twoRows.Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4 });
        twoRows.Select(r => r.Hit).Should().Equal(new[] { true, true, false, false },
            "the grown tuple list matches marker 1 and marker 2 instead of reusing the first SQL");
    }

    // ---------------------------------------------------------------------------------------------
    // 14. ScanUnkeyedScalarInFrom: an unkeyed scalar value list in a derived source's projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>ScanUnkeyedScalarInFrom</c> :3263 takes its <c>from.SubQuery</c> branch
    /// for a scalar <c>Contains</c> in the derived source's projection. The captured list starts as
    /// <c>[1]</c> and grows to <c>[1, 2]</c>; both executions of the same derived shape must read their
    /// own rows, so a stale plan carrying the first list would be caught by the second result.
    /// </summary>
    [Fact]
    public void ScanUnkeyedScalarInFrom_DerivedScalarProjection_ShouldReadEachCapturedList()
    {
        ResetPlanCache();

        var ids = new List<long> { 1 };

        var innerOne = Context.From<Ft5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Hit = ids.Contains(x.Id) });
        var outerOne = Context.From(innerOne)
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Hit });

        var oneRows = Run(outerOne);
        oneRows.Select(r => r.Hit).Should().Equal(new[] { true, false, false, false },
            "the one-value scalar list matches only marker 1");

        ids.Add(2);

        var innerTwo = Context.From<Ft5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Hit = ids.Contains(x.Id) });
        var outerTwo = Context.From(innerTwo)
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Hit });

        var twoRows = Run(outerTwo);
        twoRows.Select(r => r.Hit).Should().Equal(new[] { true, true, false, false },
            "the grown scalar list matches marker 1 and marker 2 instead of reusing the first SQL");
    }

    // ---------------------------------------------------------------------------------------------
    // 15. ScanUnkeyedScalarInValues: an unkeyed scalar value list in an ORDER BY key
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>ScanUnkeyedScalarInValues</c> :3098 takes its ORDER BY arm
    /// (:3130-3140) for a captured scalar <c>Contains</c> in a sort key, because ordering keys are not
    /// shape-keyed. Two executions of the SAME command shape with a grown captured list must therefore
    /// each render their own ORDER BY and read their own ordering (a stale cached plan would keep the
    /// first list). The shape-clean companion - the same projection ordered by the mapped key alone -
    /// takes the scan's false arm and reuses one cached plan holder.
    /// </summary>
    [Fact]
    public void ScanUnkeyedScalarInValues_SortKeyMembership_ShouldReadEachCapturedListWithCleanCompanion()
    {
        ResetPlanCache();

        var ids = new List<long> { 1 };

        QueryCommand<long> OrderedByMembership() => Context.From<Ft5Marker>()
            .OrderBy(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Run(OrderedByMembership()).Should().Equal(new long[] { 2, 3, 4, 1 },
            "the one-value membership key sorts the matching row last and the rest by id");

        ids.Add(2);

        Run(OrderedByMembership()).Should().Equal(new long[] { 3, 4, 1, 2 },
            "the grown list renders its own ORDER BY instead of reusing the first plan's SQL");

        QueryCommand<long> OrderedById() => Context.From<Ft5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var cleanHolder = Prepare(OrderedById());
        Rows(cleanHolder).Should().Equal(new long[] { 1, 2, 3, 4 },
            "the shape-clean sort key reads every row in id order");
        Prepare(OrderedById()).Should().BeSameAs(cleanHolder,
            "without an unkeyed value list the scan does not suppress the cache, so one plan holder is reused");
    }

    // ---------------------------------------------------------------------------------------------
    // 16. Query filters over several member kinds of a bound raw source (IsTransparentMember arms)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>FilterColumnDependencyVisitor.IsTransparentMember</c> (:2653-2668) only
    /// runs on the BOUND raw-source path (a mapped entity's fluent filter never walks it). One
    /// entity/filter per member kind, each read through its own live bound shape: a <c>DateTime</c>
    /// part, a nullable <c>.Value</c> and <c>string.Length</c> are transparent (the chain continues to
    /// the mapped column and hits the <c>continue</c> arm :2641), while a <c>TimeSpan</c> comparison, a
    /// nullable <c>Guid</c> null test, a <c>StartsWith</c> call and an inherited base-class member are
    /// ordinary mapped roots. Every filter restricts the live rows to a subset and its
    /// <c>IgnoreFilters()</c> counterpart returns all four rows.
    /// </summary>
    [Fact]
    public void Filter_BoundSourceMemberKinds_ShouldApplyEachFilterLive()
    {
        ResetPlanCache();

        List<long> Ids(QueryCommand<long> query) => Run(query);

        // (a) DateTime.Year: transparent member over the mapped `created` column.
        Ids(Context.FromSql("select id, created from ft5_kinds")
                .BindEntity<Ft5DateTime>(["id", "created"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 3 }, "only the rows created in 2024 match");
        Ids(Context.FromSql("select id, created from ft5_kinds")
                .BindEntity<Ft5DateTime>(["id", "created"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the DateTime filter");

        // (b) TimeSpan comparison: a single mapped member, so IsTransparentMember falls through.
        Ids(Context.FromSql("select id, duration_ticks from ft5_kinds")
                .BindEntity<Ft5Duration>(["id", "duration_ticks"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 3 }, "60 and 120 minutes are above the 30-minute filter");
        Ids(Context.FromSql("select id, duration_ticks from ft5_kinds")
                .BindEntity<Ft5Duration>(["id", "duration_ticks"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the duration filter");

        // (c) Nullable Guid: a mapped reference member tested for presence.
        Ids(Context.FromSql("select id, token from ft5_kinds")
                .BindEntity<Ft5Token>(["id", "token"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3 }, "the NULL token row is excluded");
        Ids(Context.FromSql("select id, token from ft5_kinds")
                .BindEntity<Ft5Token>(["id", "token"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the token filter");

        // (d) Nullable<T>.Value: transparent member, the dependency is the mapped `score` column.
        Ids(Context.FromSql("select id, score from ft5_kinds")
                .BindEntity<Ft5ScoreValue>(["id", "score"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 3 }, "only the non-null scores above 4 match");
        Ids(Context.FromSql("select id, score from ft5_kinds")
                .BindEntity<Ft5ScoreValue>(["id", "score"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the nullable-value filter");

        // (e) string.Length: transparent member, the dependency is the mapped `name` column.
        Ids(Context.FromSql("select id, name from ft5_kinds")
                .BindEntity<Ft5NameLength>(["id", "name"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 3 }, "alpha and gamma are longer than four characters");
        Ids(Context.FromSql("select id, name from ft5_kinds")
                .BindEntity<Ft5NameLength>(["id", "name"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the length filter");

        // (f) A string method call: the argument member is the dependency.
        Ids(Context.FromSql("select id, name from ft5_kinds")
                .BindEntity<Ft5NamePrefix>(["id", "name"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 3 }, "only gamma starts with ga");
        Ids(Context.FromSql("select id, name from ft5_kinds")
                .BindEntity<Ft5NamePrefix>(["id", "name"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the prefix filter");

        // (g) An inherited base-class member: the dependency is resolved through the derived metadata.
        Ids(Context.FromSql("select id, base_code from ft5_kinds")
                .BindEntity<Ft5BaseMember>(["id", "base_code"])
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 3 }, "only the base_code == 1 rows match");
        Ids(Context.FromSql("select id, base_code from ft5_kinds")
                .BindEntity<Ft5BaseMember>(["id", "base_code"])
                .IgnoreFilters()
                .OrderBy(x => x.Id)
                .Select(x => x.Id))
            .Should().Equal(new long[] { 1, 2, 3, 4 }, "IgnoreFilters() drops the base-class filter");
    }

    // ---------------------------------------------------------------------------------------------
    // 17. Unkeyed captured lists in derived/joined/CTE/referenced sources
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>ScanUnkeyedScalarInFrom</c>/<c>ScanUnkeyedTupleInFrom</c> (:3007-3316)
    /// reached through public sources that render inline: (a) a JOINED derived source whose projection
    /// carries a scalar <c>Contains</c> (the <c>joins[i].From</c> arm :3125), (b) a CTE body gated by a
    /// scalar list (the <c>_ctes</c> arm :3219-3224), (c) a CTE body gated by a tuple list (the tuple
    /// <c>_ctes</c> arm :2968-2973) and (d) a correlated referenced subquery whose own condition
    /// carries a scalar list (the descendant-condition arm :3105 plus the <c>_referencedQueries</c> arm
    /// :3234-3239). Every shape is executed twice with a grown captured list and each run must read its
    /// own rows, so a reused plan from the first list would be caught by the second result. The joined
    /// projection names the <see cref="Ft5JoinedHit"/> DTO: a <c>ValueTuple</c> terminal projection is
    /// rejected by the row-materializer path (#307, recorded as D2-TUPLE-PROJECTION in
    /// <c>deferred.json</c>), while the tuple is legal as a direct comparison operand in (c).
    /// </summary>
    [Fact]
    public void ScanUnkeyedSources_JoinedDerivedCteAndReferenced_ShouldFollowEachCapturedList()
    {
        ResetPlanCache();

        var ids = new List<long> { 1 };
        var pairs = new List<(long Id, int Qty)> { (1L, 1) };

        // (a) scalar list in a JOINED derived source's projection.
        QueryCommand<Ft5JoinedHit> JoinedHit() => Context.From<Ft5Marker>()
            .Join(
                Context.From(Context.From<Ft5Marker>().OrderBy(x => x.Id).Select(x => new { x.Id, Hit = ids.Contains(x.Id) })),
                (m, d) => m.Id == d.Id)
            .OrderBy(p => p.Item1.Id)
            .Select(p => new Ft5JoinedHit { Id = p.Item1.Id, Hit = p.Item2.Hit });

        var joinedOne = Run(JoinedHit());
        joinedOne.Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4 });
        joinedOne.Select(r => r.Hit).Should().Equal(new[] { true, false, false, false },
            "the one-value list matches only marker 1 inside the joined derived source");

        ids.Add(2);

        var joinedTwo = Run(JoinedHit());
        joinedTwo.Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4 });
        joinedTwo.Select(r => r.Hit).Should().Equal(new[] { true, true, false, false },
            "the grown list re-renders the joined derived source instead of reusing the first plan");

        // (b) a CTE body gated by a scalar list.
        var scalarCte = Context.With("ft5_scan_scalar",
                Context.From<Ft5Line>()
                    .Join(Context.From<Ft5Marker>(), (l, m) => l.MarkerId == m.Id)
                    .Where(p => ids.Contains(p.Item1.Qty))
                    .Select(p => new { p.Item1.Id }))
            .From("ft5_scan_scalar")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt);

        Run(scalarCte).Should().Equal(new[] { 10 }, "with ids {1,2} only line 10 has a matching qty (1); line 12 has qty 5");

        ids.Add(5);

        var scalarCteGrown = Context.With("ft5_scan_scalar",
                Context.From<Ft5Line>()
                    .Join(Context.From<Ft5Marker>(), (l, m) => l.MarkerId == m.Id)
                    .Where(p => ids.Contains(p.Item1.Qty))
                    .Select(p => new { p.Item1.Id }))
            .From("ft5_scan_scalar")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt);

        Run(scalarCteGrown).Should().Equal(new[] { 10, 12 },
            "the grown list adds the qty-5 line 12 instead of reusing the first CTE plan");

        // (c) a CTE body gated by a tuple list.
        var tupleCte = Context.With("ft5_scan_tuple",
                Context.From<Ft5Line>()
                    .Where(l => pairs.Contains(new ValueTuple<long, int>(l.MarkerId, l.Qty)))
                    .Select(l => new { l.Id }))
            .From("ft5_scan_tuple")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt);

        Run(tupleCte).Should().Equal(new[] { 10 }, "only the (1, 1) pair matches line 10");

        pairs.Add((3L, 7));

        var tupleCteGrown = Context.With("ft5_scan_tuple",
                Context.From<Ft5Line>()
                    .Where(l => pairs.Contains(new ValueTuple<long, int>(l.MarkerId, l.Qty)))
                    .Select(l => new { l.Id }))
            .From("ft5_scan_tuple")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt);

        Run(tupleCteGrown).Should().Equal(new[] { 10, 13 }, "the grown tuple list adds the (3, 7) pair");

        // (d) a correlated referenced subquery whose own condition carries a scalar list.
        var quantities = new List<int> { 1, 7 };

        QueryCommand<long> Correlated() => Context.From<Ft5Marker>()
            .Where(m => m.Id <= 3)
            .Where(m => SqlFunctions.Sql.exists(
                Context.From<Ft5Line>().Where(l => l.MarkerId == m.Id && quantities.Contains(l.Qty))))
            .OrderBy(m => m.Id)
            .Select(m => m.Id);

        Run(Correlated()).Should().Equal(new long[] { 1, 3 }, "quantities 1 and 7 sit under markers 1 and 3");

        quantities.Add(5);

        Run(Correlated()).Should().Equal(new long[] { 1, 2, 3 },
            "the grown list reaches marker 2 through qty 5, so the referenced query is re-rendered");
    }

    /// <summary>Named terminal projection of the joined derived source (a <c>ValueTuple</c> is rejected, #307).</summary>
    public sealed class Ft5JoinedHit
    {
        public long Id { get; set; }

        public bool Hit { get; set; }
    }

    [SqlTable("ft5_marker")]
    public sealed class Ft5Marker
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

        [Column("snake_col")]
        public int SnakeCol { get; set; }
    }

    [SqlTable("ft5_marker")]
    public sealed class Ft5Simple
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

        [Column("snake_col")]
        public int SnakeCol { get; set; }
    }

    [SqlTable("ft5_marker")]
    public sealed class Ft5Nullable
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

        [Column("snake_col")]
        public int SnakeCol { get; set; }
    }

    [SqlTable("ft5_marker")]
    public sealed class Ft5Length
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

        [Column("snake_col")]
        public int SnakeCol { get; set; }
    }

    [SqlTable("ft5_marker")]
    public sealed class Ft5Bound
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("score")]
        public int? Score { get; set; }
    }

    [SqlTable("ft5_marker")]
    public sealed class Ft5Convention
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        /// <summary>Auto-mapped: no <c>[Column]</c>, so the resolved naming convention supplies the name.</summary>
        public int SnakeCol { get; set; }
    }

    [SqlTable("ft5_line")]
    public sealed class Ft5Line
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("marker_id")]
        public long MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    /// <summary>Marker entity whose filter walks a <c>DateTime</c> part (transparent member).</summary>
    [SqlTable("ft5_kinds")]
    public sealed class Ft5DateTime
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("created")]
        public DateTime Created { get; set; }
    }

    /// <summary>Marker entity whose filter compares a mapped <see cref="TimeSpan"/> column.</summary>
    [SqlTable("ft5_kinds")]
    public sealed class Ft5Duration
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        /// <summary>Stored as integer ticks: SQLite has no native duration type.</summary>
        [Column("duration_ticks")]
        public TimeSpan Duration { get; set; }
    }

    /// <summary>Marker entity whose filter tests a nullable <see cref="Guid"/> member.</summary>
    [SqlTable("ft5_kinds")]
    public sealed class Ft5Token
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("token")]
        public Guid? Token { get; set; }
    }

    /// <summary>Marker entity whose filter walks <c>Nullable&lt;T&gt;.Value</c>.</summary>
    [SqlTable("ft5_kinds")]
    public sealed class Ft5ScoreValue
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("score")]
        public int? Score { get; set; }
    }

    /// <summary>Marker entity whose filter walks <c>string.Length</c>.</summary>
    [SqlTable("ft5_kinds")]
    public sealed class Ft5NameLength
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Marker entity whose filter is a string method call over the mapped name.</summary>
    [SqlTable("ft5_kinds")]
    public sealed class Ft5NamePrefix
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Unmapped base type of <see cref="Ft5BaseMember"/>: its member is inherited.</summary>
    public abstract class Ft5KindBase
    {
        [Column("base_code")]
        public int BaseCode { get; set; }
    }

    /// <summary>Marker entity whose filter reads a member declared on its base class.</summary>
    [SqlTable("ft5_kinds")]
    public sealed class Ft5BaseMember : Ft5KindBase
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
    }
}
