using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQLite SQL-rendering branch coverage (cycle 6, cluster D3) executed against a live file database.
/// Every fact owns its <c>sr6_sqlite_*</c> tables (created and seeded in the constructor) and asserts
/// the exact rows the engine returns.
///
/// <para>
/// Targeted arms: <c>SqlBuilder.MakeNamedWindow</c> (multi-column partition, multi-key ordering, a
/// descending key, a frame without partition/order), <c>SqlBuilder.MakeSelect</c> (DISTINCT plus
/// ORDER BY/LIMIT/OFFSET and an ordinal sort key), <c>SqlSourceRenderer.MakeWithClause</c>/<c>MakeFrom</c>/
/// <c>MakeJoin</c> (two CTE declarations and a derived join source under a filter), the multi-table
/// <c>UPDATE ... FROM</c> (<c>SqlBuilder.MakeUpdateJoin</c>) and the fail-closed multi-table
/// <c>DELETE</c> (<c>MakeDeleteJoin</c>), and the per-parent top-N lowering
/// (<c>MakePerParentRankExpression</c>/<c>MakePerParentPagingOuter</c>) with an offset and a descending
/// child key.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Sqlite/StructuralPlanBranchCoverage5Tests</c>
/// already executes plain pages, UNION/INTERSECT/EXCEPT heads, ROLLUP guards, physical-vs-derived plan
/// keys and a per-parent top-1 page; <c>Sqlite/WindowFunctionSweepTests.NamedWindowWithFrame</c> owns a
/// single named window with a frame. This class adds the multi-window/multi-key combination, the
/// DISTINCT+paging composition, the CTE+derived join and the mutation/offset arms.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteRenderingBranchCoverage6Tests : ProviderTestSuite
{
    private const string Ddl = """
        drop table if exists sr6_sqlite_child;
        drop table if exists sr6_sqlite_parent;
        drop table if exists sr6_sqlite_line;
        drop table if exists sr6_sqlite_marker;
        create table sr6_sqlite_marker
        (
            id integer primary key,
            grp int not null,
            tag int not null,
            code text not null,
            score int not null
        );
        create table sr6_sqlite_line
        (
            id integer primary key,
            marker_id int not null,
            qty int not null
        );
        insert into sr6_sqlite_marker (id, grp, tag, code, score) values
            (1, 1, 10, 'a', 5),
            (2, 1, 10, 'b', 7),
            (3, 2, 20, 'a', 9),
            (4, 2, 20, 'b', 11);
        insert into sr6_sqlite_line (id, marker_id, qty) values
            (11, 1, 1),
            (12, 1, 3),
            (13, 2, 5),
            (14, 3, 7);
        create table sr6_sqlite_parent
        (
            id integer primary key,
            label text not null
        );
        create table sr6_sqlite_child
        (
            id integer primary key,
            parent_id int not null,
            sort_key int not null
        );
        insert into sr6_sqlite_parent (id, label) values
            (9701, 'many'),
            (9702, 'single');
        insert into sr6_sqlite_child (id, parent_id, sort_key) values
            (9711, 9701, 1),
            (9712, 9701, 2),
            (9721, 9702, 1);
        """;

    public SqliteRenderingBranchCoverage6Tests()
    {
        ExecuteRaw(Ddl);

        // The eager graph must be registered before the parent is mapped, so the collection member is
        // recognized as a navigation and not as a physical column.
        Context.From<Sr6SqliteChild>();
        Context.From<Sr6SqliteParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
    }

    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Distinguishing arm: three named windows rendered in one <c>WINDOW</c> clause. The first carries a
    /// two-column partition, a two-key ordering and a descending key; the second has only an ordering;
    /// the third has only a frame. Two aggregate expressions reuse the first window.
    /// </summary>
    [Fact]
    public void NamedWindows_MultiKeyAndFrameOnly_ShouldRenderOneWindowClauseAndReadTheWindowValues()
    {
        var e = Context.From<Sr6SqliteMarker>();

        var rows = e
            .Window(
                "sr6_sqlite_w_multi",
                partitionBy: [x => x.Grp, x => x.Tag],
                orderBy: [e.Asc(x => x.Id), e.Desc(x => x.Score)])
            .Window(
                "sr6_sqlite_w_order",
                orderBy: [e.Asc(x => x.Id)])
            .Window(
                "sr6_sqlite_w_frame",
                frame: WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.UnboundedFollowing))
            .Select(x => new
            {
                x.Id,
                Rank = SqlFunctions.Sql.row_number().Over("sr6_sqlite_w_multi"),
                InWindow = SqlFunctions.Sql.count_over().Over("sr6_sqlite_w_multi"),
                Running = SqlFunctions.Sql.count_over().Over("sr6_sqlite_w_order"),
                Whole = SqlFunctions.Sql.count_over().Over("sr6_sqlite_w_frame")
            })
            .OrderBy(x => x.Id)
            .ToList();

        rows.Should().HaveCount(4);
        rows.Select(r => r.Rank).Should().Equal(new[] { 1, 2, 1, 2 },
            "each (grp, tag) partition holds two seeded rows ordered by id");
        rows.Select(r => r.InWindow).Should().Equal(new[] { 1, 2, 1, 2 },
            "the first window is reused by a second expression and accumulates within its partition");
        rows.Select(r => r.Running).Should().Equal(new[] { 1, 2, 3, 4 },
            "the ordering-only window accumulates over the whole source");
        rows.Select(r => r.Whole).Should().Equal(new[] { 4, 4, 4, 4 },
            "the frame-only window covers the whole partition");

        // SQLite is not a PREWHERE dialect: the ClickHouse extension is rejected by the capability gate
        // before any SQL is emitted.
        FluentActions.Invoking(() => Context.From<Sr6SqliteMarker>()
                .PreWhere(x => x.Grp == 1)
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*PREWHERE clause*");
    }

    /// <summary>
    /// Distinguishing arm: <c>MakeSelect</c>'s DISTINCT head composed with ORDER BY and a page. A limit
    /// page, a limit+offset page and an ordinal (column-index) descending page all read distinct live
    /// values in a different order.
    /// </summary>
    [Fact]
    public void DistinctOrderAndPage_ShouldReturnDistinctValuesPerWindow()
    {
        QueryCommand<int> DistinctTags() => Context.From<Sr6SqliteMarker>()
            .Select(x => x.Tag)
            .Distinct();

        var first = DistinctTags().OrderBy(1).Limit(1).ToList();
        var second = DistinctTags().OrderBy(1).Limit(1).Offset(1).ToList();
        var descending = DistinctTags().OrderBy(1, OrderDirection.Desc).Limit(2).ToList();

        first.Should().Equal(new[] { 10 }, "the distinct tags are 10 and 20; the first ascending page keeps 10");
        second.Should().Equal(new[] { 20 }, "the offset page skips 10 and keeps 20");
        descending.Should().Equal(new[] { 20, 10 }, "the ordinal sort key reverses the distinct page");
    }

    /// <summary>
    /// Distinguishing arm: two CTE declarations emitted in one <c>WITH</c> prefix (<c>MakeWithClause</c>
    /// including the comma separator) and a DERIVED join source (<c>MakeFrom</c>/<c>MakeJoin</c>) under
    /// a predicate, executed live.
    /// </summary>
    [Fact]
    public void CteWithDerivedJoinAndWhere_ShouldJoinTheHoistedSources()
    {
        var high = Context.From<Sr6SqliteMarker>()
            .Where(x => x.Grp == 1)
            .Select(x => new { x.Id, x.Tag });
        var tags = Context.From<Sr6SqliteMarker>()
            .Where(x => x.Tag == 10)
            .Select(x => new { x.Id });

        var derived = Context.From(Context.From<Sr6SqliteLine>()
            .Where(l => l.Qty > 1)
            .Select(l => new { l.Id, l.MarkerId, l.Qty }));

        var rows = Context
            .With("sr6_sqlite_high", high)
            .With("sr6_sqlite_tags", tags)
            .From("sr6_sqlite_high")
            .Join(derived, (h, d) => h["id"].AsInt == d.MarkerId)
            .Where(p => p.Item2.Qty >= 3)
            .OrderBy(p => p.Item1["id"].AsInt)
            .Select(p => new { Id = p.Item1["id"].AsInt, Line = p.Item2.Id, p.Item2.Qty })
            .ToList();

        rows.Should().HaveCount(2, "both group-1 markers match a line with qty >= 3");
        rows.Select(r => r.Id).Should().Equal(new[] { 1, 2 });
        rows.Select(r => r.Line).Should().Equal(new[] { 12, 13 });
        rows.Select(r => r.Qty).Should().Equal(new[] { 3, 5 });
    }

    /// <summary>
    /// Distinguishing arm: the multi-table <c>UPDATE ... FROM</c> renderer (<c>MakeUpdateJoin</c>) on a
    /// class-owned pair. The joined value is copied into the target row and the live read proves the two
    /// scopes stayed distinct.
    /// </summary>
    [Fact]
    public void UpdateJoin_ShouldCopyTheJoinedValueAndAffectOneRow()
    {
        var updated = Context.From<Sr6SqliteMarker>()
            .Join(Context.From<Sr6SqliteLine>(), (m, l) => m.Id == l.MarkerId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Where(p => p.Item2.Id == 14)
            .Update();

        updated.Should().Be(1, "exactly line 14 selects one target row");

        var scores = Context.From<Sr6SqliteMarker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Score)
            .ToList();

        scores.Should().Equal(new[] { 5, 7, 7, 11 }, "marker 3 copies line 14's qty (7); every other row keeps its score");
    }

    /// <summary>
    /// Distinguishing arm: SQLite has no multi-table <c>DELETE</c>, so <c>MakeDeleteJoin</c> fails
    /// closed before any SQL is emitted. The companion single-table delete and the follow-up live read
    /// prove the mutation path still works on this context.
    /// </summary>
    [Fact]
    public void DeleteJoin_ShouldBeRejectedWhileTheSingleTableDeleteRuns()
    {
        var act = () => Context.From<Sr6SqliteMarker>()
            .Join(Context.From<Sr6SqliteLine>(), (m, l) => m.Id == l.MarkerId)
            .Delete();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*multi-table DELETE*");

        Context.CreateDeleteBuilder<Sr6SqliteMarker>()
            .Where(x => x.Id == 4)
            .Delete()
            .Should().Be(1);

        Context.From<Sr6SqliteMarker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(new[] { 1, 2, 3 }, "only the sentinel row was removed");
    }

    /// <summary>
    /// Distinguishing arm: a per-parent top-N child page with BOTH bounds (offset and limit) and a
    /// DESCENDING child key, which drives the ranked inner expression
    /// (<c>MakePerParentRankExpression</c>) and the outer page filter/order
    /// (<c>MakePerParentPagingOuter</c>).
    /// </summary>
    [Fact]
    public void PerParentPaging_WithOffsetAndDescendingKey_ShouldPageEachParentSeparately()
    {
        var parents = Context.From<Sr6SqliteParent>()
            .OrderBy(p => p.Id)
            .LoadWith(
                p => p.Children,
                c => c.From<Sr6SqliteChild>().OrderByDescending(x => x.SortKey).Limit(1).Offset(1),
                p => p.Id,
                c => c.ParentId)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(new[] { 9701, 9702 }, "both parents are read");
        parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 9711 },
            "descending order over (2, 1) with offset 1 keeps the second row, 9711");
        parents[1].Children.Should().BeEmpty("the single-child parent has no row at offset 1");
    }

    [SqlTable("sr6_sqlite_marker")]
    public sealed class Sr6SqliteMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }

    [SqlTable("sr6_sqlite_line")]
    public sealed class Sr6SqliteLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    [SqlTable("sr6_sqlite_parent")]
    public sealed class Sr6SqliteParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;

        public List<Sr6SqliteChild> Children { get; set; } = [];
    }

    [SqlTable("sr6_sqlite_child")]
    public sealed class Sr6SqliteChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }
    }
}
