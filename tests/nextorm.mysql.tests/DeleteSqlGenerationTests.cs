using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>SQL generation of the delete builder on MySQL (no database connection).</summary>
public class DeleteSqlGenerationTests
{
    [Fact]
    public void Delete_Where_ShouldRenderPredicate()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.CreateDeleteBuilder<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1");
    }

    [Fact]
    public void Delete_All_ShouldRenderNoWhere()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.CreateDeleteBuilder<IMergeEntity>()
            .All()
            .ToSql()
            .Should().Be("delete from merge_entity");
    }

    [Fact]
    public void Delete_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = MySqlTestContext.CreateQuoted();

        ctx.CreateDeleteBuilder<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from `merge_entity` where `id` = 1");
    }

    [Fact]
    public void Delete_Returning_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => ctx.CreateDeleteBuilder<IMergeEntity>().Where(x => x.Id == 1).Returning().ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Truncate_ShouldRenderTruncateTable()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.CreateTruncateBuilder<IMergeEntity>().ToSql().Should().Be("truncate table merge_entity");
    }

    [Fact]
    public void DeleteJoin_Where_ShouldRenderMultiTableDelete()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Where(p => p.Item1.String == "x")
            .ToSql()
            .Should().Be("delete `t1` from complex_entity as `t1` join simple_entity as `t2` on t1.id = cast(t2.id as signed) where t1.somestring = 'x'");
    }

    [Fact]
    public void DeleteJoin_DmlScopeHint_ShouldFoldIntoInlineComment()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.From<IComplexEntity>()
            .WithTablesInScopeHint("NO_RANGE_OPTIMIZATION(t1)")
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .ToSql();

        sql.Should().Be("delete /*+ NO_RANGE_OPTIMIZATION(t1) */ `t1` from complex_entity as `t1` join simple_entity as `t2` on t1.id = cast(t2.id as signed)");
        (sql.Split("/*+").Length - 1).Should().Be(1);
    }

    [Fact]
    public void DeleteJoin_DmlScopeHint_ShouldComposeWithJoinHintInOneComment()
    {
        using var ctx = MySqlTestContext.Create();

        // V08 DELETE side (inline): the scope hint and the per-join hint fold into one comment, once.
        var sql = ctx.From<IComplexEntity>()
            .WithTablesInScopeHint("NO_RANGE_OPTIMIZATION(t1)")
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id, j => j.WithJoinHint("JOIN_ORDER(t1,t2)"))
            .ToSql();

        sql.Should().Be("delete /*+ NO_RANGE_OPTIMIZATION(t1) JOIN_ORDER(t1,t2) */ `t1` from complex_entity as `t1` join simple_entity as `t2` on t1.id = cast(t2.id as signed)");
        (sql.Split("/*+").Length - 1).Should().Be(1);
    }

    [Fact]
    public void DeleteJoin_DmlScopeHint_FromCte_ShouldPlaceCommentAfterDeleteVerb()
    {
        using var ctx = MySqlTestContext.Create();

        // Finding 6 (inline): the hoisted WITH prefix precedes the statement, so the comment follows DELETE.
        var scope = ctx.With("c", ctx.From<ISimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }));

        var sql = ctx.From<IComplexEntity>()
            .WithTablesInScopeHint("NO_RANGE_OPTIMIZATION(t1)")
            .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
            .ToSql();

        sql.Should().StartWith("with c as (select id from simple_entity");
        sql.Should().Contain("delete /*+ NO_RANGE_OPTIMIZATION(t1) */ `t1` from complex_entity as `t1` join c as `t2`");
        (sql.IndexOf("/*+")).Should().BeGreaterThan(sql.IndexOf("delete"));
    }
}
