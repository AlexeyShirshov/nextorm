using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>SQL generation of the delete builder on SQL Server (no database connection).</summary>
public class DeleteSqlGenerationTests
{
    [Fact]
    public void Delete_Where_ShouldRenderPredicate()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1");
    }

    [Fact]
    public void Delete_All_ShouldRenderNoWhere()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .All()
            .ToSql()
            .Should().Be("delete from merge_entity");
    }

    [Fact]
    public void Delete_UppercaseKeywords_ShouldCaseDelete()
    {
        using var ctx = SqlServerTestContext.CreateUppercase();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("DELETE FROM merge_entity WHERE id = 1");
    }

    [Fact]
    public void Delete_ReturningEntity_ShouldRenderDeletedOutput()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Be("delete from merge_entity output deleted.id, deleted.name, deleted.age, deleted.total where id = 1");
    }

    [Fact]
    public void Delete_ReturningProjection_ShouldRenderDeletedOutput()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("delete from merge_entity output deleted.id, deleted.name where id = 1");
    }

    [Fact]
    public void Delete_OutputInto_ShouldRenderDeletedInto()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .OutputInto("audit_log")
            .ToSql()
            .Should().Be("delete from merge_entity output deleted.id, deleted.name into audit_log (id, name) where id = 1");
    }

    [Fact]
    public void Delete_OutputIntoThenOutput_ShouldRenderBoth()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .OutputIntoThenOutput("audit_log")
            .ToSql()
            .Should().Be("delete from merge_entity output deleted.id, deleted.name into audit_log (id, name) output deleted.id, deleted.name where id = 1");
    }

    [Fact]
    public void Truncate_ShouldRenderTruncateTable()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.Truncate<IMergeEntity>().ToSql().Should().Be("truncate table merge_entity");
    }

    [Fact]
    public void DeleteJoin_Where_ShouldRenderTargetAliasFromJoin()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Where(p => p.Item1.String == "x")
            .ToSql()
            .Should().Be("delete [t1] from complex_entity as [t1] join simple_entity as [t2] on t1.id = cast(t2.id as bigint) where t1.somestring = 'x'");
    }

    [Fact]
    public void DeleteJoin_NoFilter_ShouldRenderWithoutWhere()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .ToSql()
            .Should().Be("delete [t1] from complex_entity as [t1] join simple_entity as [t2] on t1.id = cast(t2.id as bigint)");
    }
}
