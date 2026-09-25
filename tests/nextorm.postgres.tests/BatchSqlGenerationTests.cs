using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of the batch surface on PostgreSQL (no database connection): the CTAS statement and
/// the reading query share one parameter sequence, and a captured member is prefixed per statement so
/// two statements capturing the same variable do not collide.
/// </summary>
public class BatchSqlGenerationTests
{
    [Fact]
    public void CreateTempTableThenQuery_ShouldRenderCreateAndReadAsOneBatch()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.Batch()
            .CreateTempTable("recent_ids", ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .Query(ctx.From("recent_ids").Select(t => new { Id = t["id"].AsInt }))
            .ToSql();

        sql.Should().Be("create temporary table recent_ids as select id from simple_entity; select id from recent_ids");
    }

    [Fact]
    public void CreateTempTableThenQuery_WithCapturedParameter_ShouldPrefixTheParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var min = 5;

        var sql = ctx.Batch()
            .CreateTempTable("recent_ids", ctx.From<ISimpleEntity>().Where(x => x.Id > min).Select(x => new { x.Id }))
            .Query(ctx.From("recent_ids").Select(t => new { Id = t["id"].AsInt }))
            .ToSql();

        sql.Should().Be("create temporary table recent_ids as select id from simple_entity\n where (id > @b0_min); select id from recent_ids");
    }

    [Fact]
    public void CreateTableThenQuery_ShouldRenderPersistentCreateAndRead()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.Batch()
            .CreateTable("archive_ids", ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .Query(ctx.From("archive_ids").Select(t => new { Id = t["id"].AsInt }))
            .ToSql();

        sql.Should().Be("create table archive_ids as select id from simple_entity; select id from archive_ids");
    }

    [Fact]
    public void Batch_CapturedMemberName_ShouldBePrefixedPerStatement()
    {
        using var ctx = PostgresTestContext.Create();
        var min = 5;

        var sql = ctx.Batch()
            .CreateTempTable("recent_ids", ctx.From<ISimpleEntity>().Where(x => x.Id > min).Select(x => new { x.Id }))
            .Query(ctx.From<ISimpleEntity>().Where(x => x.Id > min).Select(x => new { x.Id }))
            .ToSql();

        sql.Should().Contain("@b0_min");
        sql.Should().Contain("@b1_min");
    }

    [Fact]
    public void Batch_MultipleMaterialisations_ShouldPrecedeTheResult()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.Batch()
            .CreateTempTable("first_ids", ctx.From<ISimpleEntity>().Where(x => x.Id > 1).Select(x => new { x.Id }))
            .CreateTempTable("second_ids", ctx.From("first_ids").Select(t => new { Id = t["id"].AsInt }))
            .Query(ctx.From("second_ids").Select(t => new { Id = t["id"].AsInt }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(3);
        statements[0].Should().StartWith("create temporary table first_ids as ");
        statements[1].Should().Be("create temporary table second_ids as select id from first_ids");
        statements[2].Should().Be("select id from second_ids");
    }

    [Fact]
    public void Batch_MaterialisationAfterQuery_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var batch = ctx.Batch();
        batch.Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }));

        var act = () => batch.CreateTempTable("late_ids", ctx.From<ISimpleEntity>().Select(x => new { x.Id }));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Batch_SecondQuery_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var batch = ctx.Batch();
        batch.Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }));

        var act = () => batch.Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dialect_ShouldReportBatchSupport()
    {
        using var ctx = PostgresTestContext.Create();
        var dialect = ((DataContext)ctx).Dialect;

        dialect.SupportsBatch.Should().BeTrue();
        dialect.BatchUsesJoinedCommand.Should().BeFalse();
    }

    [Fact]
    public void Batch_StatementFromAnotherContext_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        using var other = PostgresTestContext.Create();
        var batch = ctx.Batch();

        var act = () => batch.Query(other.From<ISimpleEntity>().Select(x => new { x.Id }));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Batch_UpdateThenQuery_ShouldShareParameterSequence()
    {
        using var ctx = PostgresTestContext.Create();
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

    [Fact]
    public void Batch_InsertThenQuery_ShouldRenderInsertFirst()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.Batch()
            .Insert(ctx.InsertInto<IInsertEntity>().Value(x => x.Name, "a").Value(x => x.Age, 5))
            .Query(ctx.From<IInsertEntity>().Select(x => new { x.Name }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(2);
        statements[0].Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
        statements[1].Should().Be("select name from insert_entity");
    }

    [Fact]
    public void Batch_DeleteThenQuery_ShouldPrefixTheCapturedMember()
    {
        using var ctx = PostgresTestContext.Create();
        var max = 5;

        var sql = ctx.Batch()
            .Delete(ctx.DeleteFrom<ISimpleEntity>().Where(x => x.Id > max))
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(2);
        statements[0].Should().Be("delete from simple_entity where (id > @b0_max)");
        statements[1].Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Batch_TruncateThenQuery_ShouldRenderTruncateFirst()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.Batch()
            .Truncate(ctx.Truncate<ISimpleEntity>())
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(2);
        statements[0].Should().Be("truncate table simple_entity");
        statements[1].Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Batch_ExecuteAfterQuery_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var batch = ctx.Batch();
        batch.Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }));

        var act = () => batch.Update(ctx.Update<ISimpleEntity>().Set(x => x.Id, 1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Batch_MutationFromAnotherContext_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        using var other = PostgresTestContext.Create();

        var act = () => ctx.Batch().Update(other.Update<ISimpleEntity>().Set(x => x.Id, 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Batch_Default_ShouldRenderStatementsOnOneLine()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.Batch()
            .CreateTempTable("first_ids", ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .Query(ctx.From("first_ids").Select(t => new { Id = t["id"].AsInt }))
            .ToSql();

        sql.Should().Contain("; ");
        sql.Should().NotContain(";\n");
    }

    [Fact]
    public void Batch_Multiline_ShouldPlaceEachStatementOnItsOwnLine()
    {
        using var ctx = PostgresTestContext.CreateMultiline();

        var sql = ctx.Batch()
            .CreateTempTable("first_ids", ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .Query(ctx.From("first_ids").Select(t => new { Id = t["id"].AsInt }))
            .ToSql();

        sql.Should().Contain(";\n");
        sql.Should().NotContain("; ");
        sql.Split(";\n").Should().HaveCount(2);
    }
}
