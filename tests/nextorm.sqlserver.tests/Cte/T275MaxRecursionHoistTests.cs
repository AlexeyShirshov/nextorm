using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

[SqlTable("t275_merge_target")]
public sealed class T275MergeTarget
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("age")]
    public int Age { get; set; }
}

/// <summary>
/// #275: when a recursive CTE is hoisted into a mutation/CTAS/batch source, the recursion limit must
/// survive hoisting — <c>option (maxrecursion n)</c> belongs to the statement tail, not to the
/// <c>WITH</c> prefix. These tests pin the six consumers: exactly one option per statement, at the tail
/// (before an optional terminal <c>;</c>), with the caller's value; and no change for a source without a
/// limit, a non-hoisted source, plain SELECT and the existing UPDATE-JOIN/DELETE-JOIN tails.
/// No database is opened: the placeholder connection is never used.
/// </summary>
public class T275MaxRecursionHoistTests
{
    private const string Option = "option (maxrecursion 7)";

    private static readonly Regex Tail = new(@"option \(maxrecursion 7\)\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex TerminatedTail = new(@"option \(maxrecursion 7\);\s*$", RegexOptions.IgnoreCase);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static Cte<int> Recursive(IDataContext ctx, int maxRecursion = 7)
        => ctx.From<ISimpleEntity>().Where(s => s.Id == 1).Select(s => s.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1), maxRecursion);

    private static int OptionCount(string sql)
        => Regex.Matches(sql, "option \\(maxrecursion", RegexOptions.IgnoreCase).Count;

    private static void AssertSingleTail(string sql, bool terminated)
    {
        OptionCount(sql).Should().Be(1, "the option must be emitted exactly once per statement");
        (terminated ? TerminatedTail : Tail).IsMatch(sql)
            .Should().BeTrue($"'{sql}' must end with '{Option}'{(terminated ? " before the terminal ';'" : string.Empty)}");
    }

    [Fact]
    public void InsertSelect_HoistedRecursiveCte_AppendsMaxRecursionOnce()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = Normalize(ctx.CreateInsertBuilder<ISimpleEntity>()
            .Values(ctx.From(Recursive(ctx)), n => new { Id = n })
            .ToSql());

        sql.Should().StartWith("with nums as (");
        AssertSingleTail(sql, terminated: false);
    }

    [Fact]
    public void MergeKeyPath_QuerySource_RemainsRejected()
    {
        using var ctx = SqlServerTestContext.Create();

        // The key-upsert form accepts an entity/batch source only: a query source requires the full-MERGE
        // branch form, so this attachment point is unreachable through the public builder. The existing
        // guard is pinned here so the D275 propagation there stays structural.
        FluentActions.Invoking(() => ctx.CreateMergeBuilder<T275MergeTarget>()
                .Using(ctx.From(Recursive(ctx)).Select(n => new T275MergeTarget { Id = n, Name = "x", Age = n }))
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .ToSql())
            .Should().Throw<NotSupportedException>().WithMessage("*full-MERGE*");
    }

    [Fact]
    public void MergeSource_HoistedRecursiveCte_AppendsMaxRecursionOnce()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateMergeBuilder<T275MergeTarget>()
            .Using(ctx.From(Recursive(ctx)).Select(n => new T275MergeTarget { Id = n, Name = "x", Age = n }))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        sql.Should().StartWith("with nums as (");
        AssertSingleTail(sql, terminated: true);
    }

    [Fact]
    public void CreateTableAsSelect_HoistedRecursiveCte_AppendsMaxRecursionOnce()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = Normalize(ctx.From(Recursive(ctx)).Select(n => new { Id = n }).ToTableSql("t275_archive"));

        sql.Should().StartWith("with nums as (").And.Contain("select [Id] into t275_archive");
        AssertSingleTail(sql, terminated: false);
    }

    [Fact]
    public void Batch_CtasStep_PreservesStatementLocalMaxRecursion()
    {
        using var ctx = SqlServerTestContext.Create();

        var statements = ctx.CreateBatchBuilder()
            .CreateTable("t275_archive", ctx.From(Recursive(ctx)).Select(n => new { Id = n }))
            .Query(ctx.From("t275_archive").Select(t => new { Id = t["id"].AsInt }))
            .ToSql()
            .Split("; ");

        statements.Should().HaveCount(2);
        AssertSingleTail(statements[0], terminated: false);
        OptionCount(statements[1]).Should().Be(0);
    }

    [Fact]
    public void Batch_SelectStep_PreservesStatementLocalMaxRecursion()
    {
        using var ctx = SqlServerTestContext.Create();

        var statements = ctx.CreateBatchBuilder()
            .Update(ctx.CreateUpdateBuilder<ISimpleEntity>().Set(x => x.Id, 1).Where(x => x.Id > 0))
            .Query(ctx.From(Recursive(ctx)).Select(n => new { Id = n }))
            .ToSql()
            .Split("; ");

        statements.Should().HaveCount(2);
        OptionCount(statements[0]).Should().Be(0);
        AssertSingleTail(statements[1], terminated: false);
    }

    [Fact]
    public void Batch_MultipleSteps_DoesNotDuplicateOrLeakMaxRecursion()
    {
        using var ctx = SqlServerTestContext.Create();

        var statements = ctx.CreateBatchBuilder()
            .AddQuery(ctx.From(Recursive(ctx)).Select(n => new { Id = n }))
            .AddQuery(ctx.From(Recursive(ctx)).Select(n => new { Id = n }))
            .ToSql()
            .Split("; ");

        statements.Should().HaveCount(2);
        AssertSingleTail(statements[0], terminated: false);
        AssertSingleTail(statements[1], terminated: false);
    }

    [Fact]
    public void WithoutMaxRecursion_NoOption()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();
        var anchor = e.Where(x => x.Id == 1).Select(x => new { n = x.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new { n = t["n"].AsInt + 1 });

        var sql = Normalize(ctx.WithRecursive("nums", anchor.UnionAll(step))
            .From("nums").Select(t => new { Id = t["n"].AsInt }).ToTableSql("t275_archive"));

        sql.Should().NotContain("maxrecursion");
        sql.Should().Be("with nums as (select id as [n]\n from simple_entity\n where id = 1\n union all \nselect (n + 1) as [n]\n from nums\n where (n < 5)) select n into t275_archive\n from nums");
    }

    [Fact]
    public void NonHoistedSource_SqlUnchanged()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = Normalize(ctx.From<ISimpleEntity>().Select(x => new { x.Id }).ToTableSql("t275_archive"));

        sql.Should().Be("select id into t275_archive\n from simple_entity");
    }

    [Fact]
    public void PlainSelect_ExistingTailUnchanged()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From(Recursive(ctx)).Select(n => n));

        AssertSingleTail(sql, terminated: false);
        sql.Should().NotContain("option (maxrecursion 7) option");
    }

    [Fact]
    public void UpdateJoin_ExistingTailUnchanged()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.From<ISimpleEntity>()
            .Join(ctx.From(Recursive(ctx)), (t, c) => t.Id == c)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Id, 0)
            .ToSql();

        sql.Should().Contain("update [t1] set t1.id = @p0");
        AssertSingleTail(sql, terminated: false);
    }

}
