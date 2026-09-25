using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation of the batch surface on SQLite (no database connection). SQLite has no
/// <see cref="System.Data.Common.DbBatch"/>, so the statements are rendered as one <c>;</c>-joined
/// command; a DML step shares the batch's parameter sequence, and <c>TRUNCATE</c> is rejected.
/// </summary>
public class BatchSqlGenerationTests
{
    [Fact]
    public void Batch_UpdateThenQuery_ShouldJoinWithSemicolon()
    {
        using var ctx = SqliteTestContext.Create();
        var value = 42;
        var min = 5;

        var sql = ctx.Batch()
            .Update(ctx.Update<ISimpleEntity>().Set(x => x.Id, value).Where(x => x.Id > min))
            .Query(ctx.From<ISimpleEntity>().Where(x => x.Id > min).Select(x => new { x.Id }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(2);
        statements[0].Should().Be("update simple_entity set id = $p0 where (id > $b0_min)");
        statements[1].Should().Be("select id from simple_entity\n where (id > $b1_min)");
    }

    [Fact]
    public void Batch_CreateTableDropExisting_ShouldRenderDropThenCreate()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.Batch()
            .CreateTable("archive", ctx.From<ISimpleEntity>().Select(x => new { x.Id }), new CreateTableOptions { DropExisting = true })
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(3);
        statements[0].Should().Be("drop table if exists archive");
        statements[1].Should().Be("create table archive as select id from simple_entity");
        statements[2].Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Batch_CreateTableDropExistingViaBuilder_ShouldRenderDropThenCreate()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.Batch()
            .CreateTable("archive", ctx.From<ISimpleEntity>().Select(x => new { x.Id }), o => o.DropExisting())
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(3);
        statements[0].Should().Be("drop table if exists archive");
        statements[1].Should().Be("create table archive as select id from simple_entity");
        statements[2].Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Batch_CreateTempTableDropExisting_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.Batch()
            .CreateTempTable("t", ctx.From<ISimpleEntity>().Select(x => new { x.Id }), new CreateTableOptions { DropExisting = true })
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*persistent*");
    }

    [Fact]
    public void Batch_TruncateThenQuery_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.Batch()
            .Truncate(ctx.Truncate<ISimpleEntity>())
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }
}
