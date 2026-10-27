using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Residual <c>SqlBuilder</c>/<c>SqlSourceRenderer</c> arm coverage (cycle 7, cluster D5) executed
/// against a live SQLite file database. Every fact owns the <c>sq7_*</c> tables created and seeded in
/// the constructor and asserts the exact rows the engine returns.
///
/// <para>
/// Targeted arms: <c>MakeSelect</c>'s star/tag head through the shared <c>Any</c>/<c>Count</c>
/// terminals, <c>MakeExtremeRowOuter</c> (the portable <c>row_number</c>/<c>rank</c> lowering with
/// NULL keys, the <c>All</c> ties arm and the grouped partition arm), <c>MakeNamedWindow</c> with a
/// <c>GROUPS</c> frame, <c>MakeTableFunction</c> (built-in <c>json_each</c> bare and a
/// caller-declared function under a join), <c>MakeWithClause</c> (two declarations plus the legacy
/// recursive form with <c>MaxRecursion</c>), the multi-table <c>UPDATE ... FROM</c>
/// (<c>MakeUpdateJoin</c>) matched/unmatched, the fail-closed multi-table <c>DELETE</c>
/// (<c>MakeDeleteJoin</c>), the join-hint rejection and the condition-less <c>CROSS JOIN</c> arm of
/// <c>MakeJoin</c>.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Coverage6/SqliteRenderingBranchCoverage6Tests</c>
/// owns multi-key named windows, DISTINCT+paging, CTE+derived join, the matched <c>UPDATE ... FROM</c>
/// and the multi-table DELETE guard; <c>Sqlite/WindowFunctionSweepTests</c>/<c>Sqlite/PlanKeyBranchCoverage6Tests</c>
/// own other GROUPS frames and <c>Sqlite/SqliteSpecificTests</c> owns bare <c>json_each</c>. This class
/// adds the star/tag terminal shape, the extremum NULL/ties/grouped arms, the GROUPS named window,
/// the table-function join alias, recursion depth, and the unmatched mutation controls.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class Cycle7SqlBuilderTests : ProviderTestSuite
{
    private const string Ddl = """
        drop table if exists sq7_marker;
        drop table if exists sq7_line;
        drop table if exists sq7_upd;
        drop table if exists sq7_upd_line;
        drop table if exists sq7_del;
        drop table if exists sq7_del_line;
        create table sq7_marker
        (
            id integer primary key,
            grp int not null,
            score int null,
            code text not null
        );
        create table sq7_line
        (
            id integer primary key,
            marker_id int not null,
            qty int not null
        );
        insert into sq7_marker (id, grp, score, code) values
            (1, 1, 10, 'a'),
            (2, 1, null, 'b'),
            (3, 2, 10, 'c'),
            (4, 2, 5, 'd');
        insert into sq7_line (id, marker_id, qty) values
            (11, 1, 1),
            (12, 1, 3),
            (13, 2, 5),
            (14, 3, 7);
        create table sq7_upd
        (
            id integer primary key,
            score int not null,
            code text not null
        );
        create table sq7_upd_line
        (
            id integer primary key,
            target_id int not null,
            qty int not null
        );
        insert into sq7_upd (id, score, code) values
            (1, 1, 'u'),
            (2, 2, 'v');
        insert into sq7_upd_line (id, target_id, qty) values
            (21, 1, 9),
            (22, 2, 8),
            (23, 99, 7);
        create table sq7_del
        (
            id integer primary key,
            label text not null
        );
        create table sq7_del_line
        (
            id integer primary key,
            target_id int not null
        );
        insert into sq7_del (id, label) values
            (1, 'keep'),
            (2, 'drop'),
            (3, 'keep2');
        insert into sq7_del_line (id, target_id) values
            (31, 2),
            (32, 99);
        """;

    public Cycle7SqlBuilderTests()
    {
        ExecuteRaw(Ddl);
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
    /// Distinguishing arm: the shared <c>Any</c>/<c>Count</c> terminals reuse their command and set
    /// <c>IgnoreColumns</c>, which drives the star head of <c>MakeSelect</c>; the tag renders inside the
    /// generated statement. The negative existence probe proves the predicate really reaches the query.
    /// </summary>
    [Fact]
    public void AnyAndCount_WithTag_ShouldReadTheStarShape()
    {
        Context.From<Sq7Marker>()
            .WithTag("sq7_any")
            .Any()
            .Should().BeTrue("the seed holds four markers");

        Context.From<Sq7Marker>()
            .Where(x => x.Id < 0)
            .WithTag("sq7_any_none")
            .Any()
            .Should().BeFalse("no marker has a negative id");

        Context.From<Sq7Marker>()
            .WithTag("sq7_count")
            .Count()
            .Should().Be(4);

        Context.From<Sq7Marker>()
            .Where(x => x.Grp == 1)
            .WithTag("sq7_count_grp")
            .Count()
            .Should().Be(2, "only two markers belong to group 1");
    }

    /// <summary>
    /// Distinguishing arm: the portable extremum lowering with a NULL value selector. The NULL key is
    /// excluded by the injected <c>IS NOT NULL</c> filter, <c>All</c> keeps both rows tied on the
    /// maximum and the plain <c>One</c> form keeps a single winner.
    /// </summary>
    [Fact]
    public void SelectWhereMaxAll_WithNullsAndNoFilter_ShouldKeepEveryTiedWinner()
    {
        var ties = Context.From<Sq7Marker>()
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All)
            .OrderBy(x => x.Id)
            .ToList();

        ties.Select(x => x.Id).Should().Equal(new[] { 1, 3 },
            "rows 1 and 3 tie on the maximum 10 and the NULL row 2 is not a candidate");
        ties.Select(x => x.Score).Should().Equal(new int?[] { 10, 10 });

        var one = Context.From<Sq7Marker>()
            .SelectWhereMax(x => x.Score)
            .ToList();

        one.Should().ContainSingle("ExtremeRowTies.One keeps a single extreme row");
        one[0].Score.Should().Be(10);
        one[0].Id.Should().BeOneOf(1, 3);
    }

    /// <summary>
    /// Distinguishing arm: the grouped portable lowering builds one <c>rank()</c> partition per group;
    /// <c>All</c> over a group key returns the winner of every group and drops the NULL-valued row.
    /// </summary>
    [Fact]
    public void SelectWhereMaxAll_Grouped_ShouldReturnOneWinnerPerGroup()
    {
        var rows = Context.From<Sq7Marker>()
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All, x => x.Grp)
            .OrderBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Id).Should().Equal(new[] { 1, 3 },
            "group 1 peaks at marker 1 and group 2 at marker 3; the NULL score is excluded");
        rows.Select(x => x.Grp).Should().Equal(new[] { 1, 2 });
        rows.Select(x => x.Score).Should().Equal(new int?[] { 10, 10 });
    }

    /// <summary>
    /// Distinguishing arm: a named window with a <c>GROUPS</c> frame plus partition and order keys.
    /// SQLite sorts NULL first, so the frame counts the preceding peer group and the current one:
    /// the first group of a partition has one row, the second accumulates both.
    /// </summary>
    [Fact]
    public void NamedWindowGroupsFrame_ShouldRankAndCountWithinGroups()
    {
        var e = Context.From<Sq7Marker>();

        var rows = e
            .Window(
                "sq7_window",
                partitionBy: [x => x.Grp],
                orderBy: [e.Asc(x => x.Score)],
                frame: WindowFrame.Groups(1, 0))
            .Select(x => new
            {
                x.Id,
                Rank = SqlFunctions.Sql.rank().Over("sq7_window"),
                InFrame = SqlFunctions.Sql.count_over().Over("sq7_window")
            })
            .OrderBy(x => x.Id)
            .ToList();

        rows.Select(r => r.Id).Should().Equal(new[] { 1, 2, 3, 4 });
        rows.Select(r => r.Rank).Should().Equal(new[] { 2, 1, 2, 1 },
            "within each group the NULL/5 row is the first ordering peer");
        rows.Select(r => r.InFrame).Should().Equal(new[] { 2, 1, 2, 1 },
            "GROUPS 1 preceding to current row adds the previous peer group to the frame");
    }

    /// <summary>
    /// Distinguishing arm: a built-in <c>json_each</c> source read bare, and a caller-declared
    /// <see cref="TvfEntityBuilder{T}"/> factory joined to a physical table. The join forces the function alias branch
    /// of <c>MakeTableFunction</c>, and the join condition filters the generated rows live.
    /// </summary>
    [Fact]
    public void TableFunction_JsonEachBareAndJoined_ShouldReturnTheGeneratedRows()
    {
        var plain = Context.From(() => SqlFunctions.Sqlite.json_each("[4,5,6]"))
            .Select(r => r.Value)
            .ToList();

        plain.Should().BeEquivalentTo(new[] { "4", "5", "6" },
            "the built-in function exposes the array elements as text");

        var joined = Context.From(() => Sq7Json.JsonEach("[1,3]"))
            .Join(Context.From<Sq7Marker>(), (j, m) => j.Value == m.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { p.Item2.Id, p.Item2.Code })
            .ToList();

        joined.Should().HaveCount(2);
        joined.Select(r => r.Id).Should().Equal(new[] { 1, 3 });
        joined.Select(r => r.Code).Should().Equal(new[] { "a", "c" });
    }

    /// <summary>
    /// Distinguishing arm: two CTE declarations in one <c>WITH</c> prefix, plus the legacy recursive
    /// form carrying <c>MaxRecursion</c> (SQLite has no depth option, so the renderer still walks the
    /// declaration and the recursive step stops on its own predicate).
    /// </summary>
    [Fact]
    public void Ctes_TwoDeclarationsAndMaxRecursion_ShouldReadTheHoistedRows()
    {
        var high = Context.From<Sq7Marker>().Where(x => x.Grp == 1).Select(x => new { x.Id });
        var low = Context.From<Sq7Marker>().Where(x => x.Grp == 2).Select(x => new { x.Id });

        var twoCtes = Context
            .With("sq7_high", high)
            .With("sq7_low", low)
            .From("sq7_high")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt)
            .ToList();

        twoCtes.Should().Equal(new[] { 1, 2 }, "the outer query reads the first declaration");

        var anchor = Context.From<Sq7Marker>().Where(x => x.Id == 1).Select(x => new { x.Id });
        var step = Context.From("sq7_nums")
            .Where(t => t["id"].AsInt < 3)
            .Select(t => new { Id = t["id"].AsInt + 1 });

        var series = Context
            .WithRecursive("sq7_nums", anchor.Union(step), maxRecursion: 3)
            .From("sq7_nums")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt)
            .ToList();

        series.Should().Equal(new[] { 1, 2, 3 }, "the recursive step stops at 3");
    }

    /// <summary>
    /// Distinguishing arm: the <c>UPDATE ... FROM</c> renderer on a class-owned pair. The unmatched
    /// filter changes nothing and leaves the post-state intact; the matched filter copies the joined
    /// value into one target row.
    /// </summary>
    [Fact]
    public void UpdateJoin_MatchedAndUnmatched_ShouldCopyOnlyMatchedRows()
    {
        var unmatched = Context.From<Sq7Upd>()
            .Join(Context.From<Sq7UpdLine>(), (t, l) => t.Id == l.TargetId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Where(p => p.Item1.Id == 99)
            .Update();

        unmatched.Should().Be(0, "no target row has id 99");

        Context.From<Sq7Upd>()
            .OrderBy(x => x.Id)
            .Select(x => x.Score)
            .ToList()
            .Should().Equal(new[] { 1, 2 }, "the unmatched update changed nothing");

        var matched = Context.From<Sq7Upd>()
            .Join(Context.From<Sq7UpdLine>(), (t, l) => t.Id == l.TargetId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Where(p => p.Item2.Id == 21)
            .Update();

        matched.Should().Be(1, "line 21 selects exactly one target row");

        Context.From<Sq7Upd>()
            .OrderBy(x => x.Id)
            .Select(x => x.Score)
            .ToList()
            .Should().Equal(new[] { 9, 2 }, "target 1 copied line 21's qty");
    }

    /// <summary>
    /// Distinguishing arm: SQLite has no multi-table <c>DELETE</c>, so <c>MakeDeleteJoin</c> fails
    /// closed before any SQL is emitted. The companion single-table delete and the follow-up live read
    /// prove the mutation path still works.
    /// </summary>
    [Fact]
    public void DeleteJoin_ShouldBeRejectedWhileTheSingleTableDeleteRuns()
    {
        var act = () => Context.From<Sq7Del>()
            .Join(Context.From<Sq7DelLine>(), (d, l) => d.Id == l.TargetId)
            .Delete();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*multi-table DELETE*");

        Context.CreateDeleteBuilder<Sq7Del>()
            .Where(x => x.Id == 2)
            .Delete()
            .Should().Be(1);

        Context.From<Sq7Del>()
            .OrderBy(x => x.Id)
            .Select(x => x.Label)
            .ToList()
            .Should().Equal(new[] { "keep", "keep2" }, "only the sentinel row was removed");
    }

    /// <summary>
    /// Distinguishing arm: a join hint on SQLite reaches the <c>MakeJoin</c> capability gate and is
    /// rejected (SQLite has neither inline hints nor join hints).
    /// </summary>
    [Fact]
    public void JoinHint_ShouldBeRejected()
    {
        var act = () => Context.From<Sq7Marker>()
            .Join(Context.From<Sq7Line>(), (m, l) => m.Id == l.MarkerId, j => j.WithJoinHint("hash"))
            .Select(p => new { MarkerId = p.Item1.Id, LineId = p.Item2.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Join hints are not supported*");
    }

    /// <summary>
    /// Distinguishing arm: a condition-less join. <c>CrossJoin</c> reaches the null-condition branch of
    /// <c>MakeJoin</c>; the filter and ordinal read prove the cartesian source rendered correctly.
    /// </summary>
    [Fact]
    public void CrossJoin_WithoutCondition_ShouldRenderTheCartesianSource()
    {
        var rows = Context.From<Sq7Marker>()
            .CrossJoin(Context.From<Sq7Line>())
            .Where(p => p.Item1.Id == 1 && p.Item2.Id == 14)
            .Select(p => new { Marker = p.Item1.Code, Line = p.Item2.Qty })
            .ToList();

        rows.Should().ContainSingle("exactly one cartesian pair matches the filter");
        rows[0].Marker.Should().Be("a");
        rows[0].Line.Should().Be(7);
    }

    /// <summary>
    /// SqlBuilder.cs:108 — SQLite's lock renderer does not opt into source-level locks, so a
    /// <c>FOR UPDATE OF</c> source is rejected before any SQL is assembled. The same projection without
    /// the lock executes and returns the seeded rows.
    /// </summary>
    [Fact]
    public void SourceLock_ShouldBeRejectedWhilePlainReadRuns()
    {
        FluentActions
            .Invoking(() => Context.From<Sq7Marker>(o => o.ForUpdate()).Select(x => x.Id).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Source-level row locking*not supported*");

        Context.From<Sq7Marker>().OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1, 2, 3, 4);
    }

    /// <summary>
    /// SqlBuilder.cs:1715 — the multi-table UPDATE renderer rejects tables-in-scope hints on a dialect
    /// that supports neither structural nor inline hints. The same update without hints executes and
    /// reports no matching rows, leaving the seed intact.
    /// </summary>
    [Fact]
    public void UpdateJoinWithTablesInScopeHint_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => Context.From<Sq7Upd>()
                .WithTablesInScopeHint("d10")
                .Join(Context.From<Sq7UpdLine>(), (t, l) => t.Id == l.TargetId)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Score, p => p.Item2.Qty)
                .Where(p => p.Item1.Id == 99)
                .Update())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Tables-in-scope hints are not supported by this SQL dialect*");

        Context.From<Sq7Upd>()
            .Join(Context.From<Sq7UpdLine>(), (t, l) => t.Id == l.TargetId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Where(p => p.Item1.Id == 99)
            .Update()
            .Should().Be(0);
    }

    /// <summary>Caller-declared <c>json_each</c> row shape: <c>key</c> is the array index, <c>value</c> the element.</summary>
    public interface ISq7JsonRow
    {
        [Column("key")]
        long Key { get; set; }

        [Column("value")]
        long Value { get; set; }
    }

    private static class Sq7Json
    {
        public static TvfEntityBuilder<ISq7JsonRow> JsonEach(string json) => new("json_each");
    }

    [SqlTable("sq7_marker")]
    public sealed class Sq7Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("score")]
        public int? Score { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;
    }

    [SqlTable("sq7_line")]
    public sealed class Sq7Line
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    [SqlTable("sq7_upd")]
    public sealed class Sq7Upd
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("score")]
        public int Score { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;
    }

    [SqlTable("sq7_upd_line")]
    public sealed class Sq7UpdLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("target_id")]
        public int TargetId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    [SqlTable("sq7_del")]
    public sealed class Sq7Del
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    [SqlTable("sq7_del_line")]
    public sealed class Sq7DelLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("target_id")]
        public int TargetId { get; set; }
    }
}
