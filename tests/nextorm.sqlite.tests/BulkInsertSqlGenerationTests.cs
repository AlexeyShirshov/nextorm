using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on SQLite. The native path does not exist on SQLite,
/// so the portable <c>INSERT ... VALUES</c> form is always rendered; these tests do not open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private static InsertEntity Row(string name, int age, string description) => new() { Name = name, Age = age, Description = description };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1, "d"), Row("b", 2, "e")]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2), ($p3, $p4, $p5)");
    }

    [Fact]
    public void BulkInsert_MaxBatchSize_ShouldRenderOnlyTheFirstBatch()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.MaxBatchSize(1)).Values([Row("a", 1, "d"), Row("b", 2, "e")]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldIncludeIdentityColumn()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert into insert_entity (id, name, age, description) values ($p0, $p1, $p2, $p3)");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldUseInsertOrIgnore()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert or ignore into insert_entity (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldEmitReturning()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1, "d")]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2) returning id");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_WithReturningKey_ShouldEmitInsertOrIgnoreWithReturning()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1, "d")]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert or ignore into insert_entity (name, age, description) values ($p0, $p1, $p2) returning id");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_OnNonIdentityEntity_ShouldIncludeExplicitColumnOnly()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.BulkInsertInto<ISimpleEntity>(o => o.KeepIdentity()).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into simple_entity (id) values ($p0)");
    }
}
