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

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1");
    }

    [Fact]
    public void Delete_All_ShouldRenderNoWhere()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .All()
            .ToSql()
            .Should().Be("delete from merge_entity");
    }

    [Fact]
    public void Delete_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = MySqlTestContext.CreateQuoted();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from `merge_entity` where `id` = 1");
    }

    [Fact]
    public void Delete_Returning_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => ctx.DeleteFrom<IMergeEntity>().Where(x => x.Id == 1).Returning().ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Truncate_ShouldRenderTruncateTable()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.Truncate<IMergeEntity>().ToSql().Should().Be("truncate table merge_entity");
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
}
