using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// MySQL joined-DML branch coverage (cycle 7, cluster D5) executed against a live Testcontainers
/// server. Every fact owns the <c>my7_*</c> objects created and seeded in the constructor and asserts
/// the exact affected-row counts and post-state rows.
///
/// <para>
/// Targeted arms: <c>SqlBuilder.MakeUpdateJoin</c> (the <c>UPDATE ... JOIN ... SET</c> form, matched
/// and unmatched filters), <c>MakeDeleteJoin</c> (the <c>DELETE alias FROM ... JOIN</c> form, matched
/// and unmatched filters, and the <c>RETURNING</c> rejection), the inline-hint folding in
/// <c>ApplyInlineHints</c> (<c>WithJoinHint</c>) and the <c>MakeJoin</c> full-join capability gate
/// (MySQL has no FULL JOIN).
/// </para>
///
/// <para>
/// Overlap avoided: <c>MySql/Dml/UpdateJoinSqlGenerationTests</c> and <c>MySql/Dml/DeleteSqlGenerationTests</c>
/// are generation-only unit tests; this class executes the same renderers live and asserts the rows.
/// </para>
/// </summary>
[Collection("MySql")]
public sealed class Cycle7JoinedDmlTests : ProviderTestSuite
{
    public Cycle7JoinedDmlTests()
    {
        foreach (var statement in Statements())
            Execute(statement);
    }

    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private static IEnumerable<string> Statements()
    {
        yield return "drop table if exists my7_marker";
        yield return "drop table if exists my7_line";
        yield return "drop table if exists my7_upd";
        yield return "drop table if exists my7_upd_line";
        yield return "drop table if exists my7_del";
        yield return "drop table if exists my7_del_line";
        yield return "create table my7_marker (id int not null primary key, code varchar(20) not null)";
        yield return "create table my7_line (id int not null primary key, marker_id int not null, qty int not null)";
        yield return "create table my7_upd (id int not null primary key, score int not null)";
        yield return "create table my7_upd_line (id int not null primary key, target_id int not null, qty int not null)";
        yield return "create table my7_del (id int not null primary key, label varchar(20) not null)";
        yield return "create table my7_del_line (id int not null primary key, target_id int not null)";
        yield return "insert into my7_marker (id, code) values (1, 'a'), (2, 'b'), (3, 'c')";
        yield return "insert into my7_line (id, marker_id, qty) values (11, 1, 1), (12, 1, 3), (13, 2, 5), (14, 3, 7)";
        yield return "insert into my7_upd (id, score) values (1, 1), (2, 2), (3, 3)";
        yield return "insert into my7_upd_line (id, target_id, qty) values (21, 1, 9), (22, 2, 8), (23, 3, 7)";
        yield return "insert into my7_del (id, label) values (1, 'keep'), (2, 'drop'), (3, 'keep2')";
        yield return "insert into my7_del_line (id, target_id) values (31, 1), (32, 2), (33, 3)";
    }

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Distinguishing arm: the MySQL <c>UPDATE ... JOIN ... SET</c> renderer copies the joined column
    /// into two matched targets; the unmatched filter then affects no row and leaves the post-state.
    /// </summary>
    [Fact]
    public void UpdateJoin_MatchedThenUnmatched_ShouldCopyTwoRowsAndLeaveTheRest()
    {
        var matched = Context.From<My7Upd>()
            .Join(Context.From<My7UpdLine>(), (t, l) => t.Id == l.TargetId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Where(p => p.Item2.Id < 23)
            .Update();

        matched.Should().Be(2, "lines 21 and 22 select targets 1 and 2");

        Context.From<My7Upd>()
            .OrderBy(x => x.Id)
            .Select(x => x.Score)
            .ToList()
            .Should().Equal(new[] { 9, 8, 3 }, "targets 1 and 2 copied the joined qty, target 3 kept its score");

        var unmatched = Context.From<My7Upd>()
            .Join(Context.From<My7UpdLine>(), (t, l) => t.Id == l.TargetId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Where(p => p.Item1.Id == 99)
            .Update();

        unmatched.Should().Be(0, "no target row has id 99");

        Context.From<My7Upd>()
            .OrderBy(x => x.Id)
            .Select(x => x.Score)
            .ToList()
            .Should().Equal(new[] { 9, 8, 3 }, "the unmatched update changed nothing");
    }

    /// <summary>
    /// Distinguishing arm: the MySQL <c>DELETE alias FROM ... JOIN</c> renderer removes the matched
    /// target row; the unmatched filter removes nothing and keeps the post-state.
    /// </summary>
    [Fact]
    public void DeleteJoin_MatchedThenUnmatched_ShouldRemoveOneRowAndLeaveTheRest()
    {
        var matched = Context.From<My7Del>()
            .Join(Context.From<My7DelLine>(), (d, l) => d.Id == l.TargetId)
            .Where(p => p.Item2.Id == 32)
            .Delete();

        matched.Should().Be(1, "line 32 selects exactly one target row");

        Context.From<My7Del>()
            .OrderBy(x => x.Id)
            .Select(x => x.Label)
            .ToList()
            .Should().Equal(new[] { "keep", "keep2" }, "only the sentinel row was removed");

        var unmatched = Context.From<My7Del>()
            .Join(Context.From<My7DelLine>(), (d, l) => d.Id == l.TargetId)
            .Where(p => p.Item2.Id == 99)
            .Delete();

        unmatched.Should().Be(0, "no line has id 99");

        Context.From<My7Del>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(new[] { 1, 3 }, "the unmatched delete changed nothing");
    }

    /// <summary>
    /// Distinguishing arm: MySQL cannot return rows from a multi-table mutation, so both joined
    /// <c>RETURNING</c> terminals fail closed with a cannot-return <c>NotSupportedException</c>.
    /// </summary>
    [Fact]
    public void JoinedReturning_ShouldBeRejected()
    {
        var update = () => Context.From<My7Upd>()
            .Join(Context.From<My7UpdLine>(), (t, l) => t.Id == l.TargetId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Returning()
            .ToSql();

        update.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot return*");

        var delete = () => Context.From<My7Del>()
            .Join(Context.From<My7DelLine>(), (d, l) => d.Id == l.TargetId)
            .CreateDeleteJoinBuilder()
            .Returning()
            .ToSql();

        delete.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot return*");
    }

    /// <summary>
    /// Distinguishing arm: a per-join hint on an inline-comment dialect is folded into one
    /// statement-level <c>/*+ ... */</c> comment (it is not rendered by <c>MakeJoin</c> itself), and the
    /// hinted query still returns the exact joined rows.
    /// </summary>
    [Fact]
    public void JoinHint_ShouldFoldIntoOneInlineCommentAndReadRows()
    {
        var sql = Context.From<My7Marker>()
            .Join(Context.From<My7Line>(), (m, l) => m.Id == l.MarkerId, j => j.WithJoinHint("JOIN_ORDER(t1,t2)"))
            .ToSql();

        sql.Should().Contain("/*+ JOIN_ORDER(t1,t2) */");
        (sql.Split("/*+").Length - 1).Should().Be(1, "the hint is folded exactly once");

        var rows = Context.From<My7Marker>()
            .Join(Context.From<My7Line>(), (m, l) => m.Id == l.MarkerId, j => j.WithJoinHint("JOIN_ORDER(t1,t2)"))
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { p.Item1.Code, p.Item2.Qty })
            .ToList();

        rows.Should().HaveCount(4, "every seeded line joins its marker");
        rows.Select(r => r.Code).Should().Equal(new[] { "a", "a", "b", "c" });
        rows.Select(r => r.Qty).Should().Equal(new[] { 1, 3, 5, 7 });
    }

    /// <summary>
    /// Distinguishing arm: MySQL supports a right join but no full join, so <c>MakeJoin</c> rejects the
    /// full-join request before any SQL reaches the server.
    /// </summary>
    [Fact]
    public void FullJoin_ShouldBeRejected()
    {
        var act = () => Context.From<My7Marker>()
            .FullJoin(Context.From<My7Line>(), (m, l) => m.Id == l.MarkerId)
            .Select(p => new { MarkerId = p.Item1.Id, LineId = p.Item2.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Full join is not supported*");
    }

    [SqlTable("my7_marker")]
    public sealed class My7Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;
    }

    [SqlTable("my7_line")]
    public sealed class My7Line
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    [SqlTable("my7_upd")]
    public sealed class My7Upd
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("score")]
        public int Score { get; set; }
    }

    [SqlTable("my7_upd_line")]
    public sealed class My7UpdLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("target_id")]
        public int TargetId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    [SqlTable("my7_del")]
    public sealed class My7Del
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    [SqlTable("my7_del_line")]
    public sealed class My7DelLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("target_id")]
        public int TargetId { get; set; }
    }
}
