using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of the delete builder on PostgreSQL (no database connection): the predicate form
/// renders <c>DELETE FROM ... WHERE ...</c> and <c>All()</c> renders an unfiltered delete.
/// </summary>
public class DeleteSqlGenerationTests
{
    [Fact]
    public void Delete_WhereLiteral_ShouldRenderPredicate()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1");
    }

    [Fact]
    public void Delete_WhereCapturedValue_ShouldParameterise()
    {
        using var ctx = PostgresTestContext.Create();
        var id = 5L;

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == id)
            .ToSql()
            .Should().Be("delete from merge_entity where id = @id");
    }

    [Fact]
    public void Delete_All_ShouldRenderNoWhere()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .All()
            .ToSql()
            .Should().Be("delete from merge_entity");
    }

    [Fact]
    public void Delete_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from \"merge_entity\" where \"id\" = 1");
    }

    [Fact]
    public void Delete_WithoutFilter_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.DeleteFrom<IMergeEntity>().ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Delete_ReturningEntity_ShouldRenderReturning()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1 returning id, name, age, total");
    }

    [Fact]
    public void Delete_ReturningProjection_ShouldRenderSelectedColumns()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1 returning id, name");
    }

    [Fact]
    public void Delete_AllReturning_ShouldRenderReturningWithoutWhere()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .All()
            .Returning(x => x.Id)
            .ToSql()
            .Should().Be("delete from merge_entity returning id");
    }

    [Fact]
    public void Truncate_ShouldRenderTruncateTable()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Truncate<IMergeEntity>().ToSql().Should().Be("truncate table merge_entity");
    }

    [Fact]
    public void Truncate_QuotedIdentifiers_ShouldQuoteTable()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.Truncate<IMergeEntity>().ToSql().Should().Be("truncate table \"merge_entity\"");
    }

    [Fact]
    public void DeleteJoin_Where_ShouldRenderUsing()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Where(p => p.Item1.String == "x")
            .ToSql()
            .Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint) and t1.somestring = 'x'");
    }

    [Fact]
    public void DeleteJoin_NoFilter_ShouldRenderUsingWithoutWhere()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .ToSql()
            .Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint)");
    }
    [Fact]
    public void DeleteJoin_CapturedValue_ShouldParameterise()
    {
        using var ctx = PostgresTestContext.Create();
        var text = "x";

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Where(p => p.Item1.String == text)
            .ToSql()
            .Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint) and t1.somestring = @text");
    }

    [Fact]
    public void DeleteJoin_ThreeTables_ShouldChainJoins()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Join(ctx.From<IArrayEntity>(), (p, a) => p.Item2.Id == a.Id)
            .ToSql();
        sql.Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\", array_entity as \"t3\" where t1.id = cast(t2.id as bigint) and t2.id = t3.id");
    }
}
