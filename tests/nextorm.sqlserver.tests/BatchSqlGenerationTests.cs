using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL generation of the batch surface on SQL Server (no database connection): the materialisation is
/// the <c>SELECT ... INTO</c> form, and the reading query follows in the same batch.
/// </summary>
public class BatchSqlGenerationTests
{
    [Fact]
    public void CreateTableThenQuery_ShouldRenderSelectIntoAndRead()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.Batch()
            .CreateTableAs("archive_ids", ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .Query(ctx.From("archive_ids").Select(t => new { Id = t["id"].AsInt }))
            .ToSql();

        sql.Should().Be("select id into archive_ids from simple_entity; select id from archive_ids");
    }

    [Fact]
    public void CreateTempTableThenQuery_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.Batch()
            .CreateTempTableAs("recent_ids", ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .Query(ctx.From("recent_ids").Select(t => new { Id = t["id"].AsInt }))
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*temporary table*");
    }

    [Fact]
    public void Dialect_ShouldReportBatchSupport()
    {
        using var ctx = SqlServerTestContext.Create();
        var dialect = ((DataContext)ctx).Dialect;

        dialect.SupportsBatch.Should().BeTrue();
        dialect.BatchUsesJoinedCommand.Should().BeTrue();
    }

    [Fact]
    public void Batch_UpdateThenQuery_ShouldJoinWithSemicolon()
    {
        using var ctx = SqlServerTestContext.Create();
        var value = 42;
        var min = 5;

        var sql = ctx.Batch()
            .Update(ctx.Update<ISimpleEntity>().Set(x => x.Id, value).Where(x => x.Id > min))
            .Query(ctx.From<ISimpleEntity>().Where(x => x.Id > min).Select(x => new { x.Id }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(2);
        statements[0].Should().Be("update simple_entity set id = @p0 where (id > @b0_min)");
        statements[1].Should().Be("select id from simple_entity\n where (id > @b1_min)");
    }
}
