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
    public void Batch_MutationThenTwoAddQueries_ShouldRenderBothSelectsInOrder()
    {
        using var ctx = SqliteTestContext.Create();
        var value = 42;
        var min = 5;
        var max = 6;

        var sql = ctx.Batch()
            .Update(ctx.Update<ISimpleEntity>().Set(x => x.Id, value).Where(x => x.Id > min))
            .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > min).Select(x => new { x.Id }))
            .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id < max).Select(x => new { x.Id }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(3);
        statements[0].Should().StartWith("update simple_entity set id = $p0");
        statements[1].Should().Contain("select id from simple_entity").And.Contain("$b1_min");
        statements[2].Should().Contain("select id from simple_entity").And.Contain("$b2_max");
    }

    [Fact]
    public void Batch_ToSql_WithoutResultQuery_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var batch = ctx.Batch();
        batch.Delete(ctx.DeleteFrom<ISimpleEntity>().Where(x => x.Id == 1));

        var act = () => batch.ToSql();

        act.Should().Throw<InvalidOperationException>();
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

    [Fact]
    public void Batch_RawThenQuery_ShouldRenderRawVerbatimBeforeSelect()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.Batch()
            .Raw("drop table if exists archive")
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        sql.Should().Be("drop table if exists archive; select id from simple_entity");
    }

    [Fact]
    public void Batch_MultipleRawThenQuery_ShouldRenderInInsertionOrder()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.Batch()
            .Raw("create table raw_a (id integer)")
            .Raw("create table raw_b (id integer)")
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        sql.Should().Be("create table raw_a (id integer); create table raw_b (id integer); select id from simple_entity");
    }

    [Fact]
    public void Batch_RawAfterResultQuery_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var afterQuery = ctx.Batch();
        afterQuery.Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }));
        var actAfterQuery = () => afterQuery.Raw("drop table if exists archive");

        actAfterQuery.Should().Throw<InvalidOperationException>();

        var afterAddQuery = ctx.Batch();
        afterAddQuery.AddQuery(ctx.From<ISimpleEntity>().Select(x => new { x.Id }));
        var actAfterAddQuery = () => afterAddQuery.Raw("drop table if exists archive");

        actAfterAddQuery.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Batch_RawOnly_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var batch = ctx.Batch();
        batch.Raw("drop table if exists archive");

        // A raw-only batch carries no result-bearing query, so it is rejected when rendered: ToSql()
        // throws InvalidOperationException (and Execute()/ExecuteAsync() via the same guard).
        var act = () => batch.ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Batch_RawNullOrWhitespace_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var actNull = () => ctx.Batch().Raw(null!);
        var actWhitespace = () => ctx.Batch().Raw("  ");

        actNull.Should().Throw<ArgumentException>();
        actWhitespace.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Batch_RawPlaceholderText_ShouldRenderVerbatim()
    {
        using var ctx = SqliteTestContext.Create();

        // Raw binds no parameters, so placeholder-looking text must survive byte-for-byte: `@p`, `{0}`
        // and `$1` are emitted as written rather than being rewritten into the batch's parameter scheme.
        var sql = ctx.Batch()
            .Raw("select @p as v, {0}, $1")
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        sql.Should().Be("select @p as v, {0}, $1; select id from simple_entity");
    }
}
