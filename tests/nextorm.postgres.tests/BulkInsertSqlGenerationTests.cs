using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on PostgreSQL. These tests never open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private static InsertEntity Row(string name, int age) => new() { Name = name, Age = age };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void BulkInsert_MaxBatchSize_ShouldRenderOnlyTheFirstBatch()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.MaxBatchSize(1)).Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldAddOverridingSystemValue()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into insert_entity (id, name, age) overriding system value values (@p0, @p1, @p2)");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldUseOnConflictDoNothing()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1) on conflict do nothing");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldEmitReturning()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1) returning id");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_WithReturningKey_ShouldEmitOnConflictDoNothingThenReturning()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1) on conflict do nothing returning id");
    }

    [Fact]
    public void BulkInsert_TableOverride_ShouldTargetOverriddenTable()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.Table("bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into bulk_target (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverride_WithReturningKey_ShouldUseOverriddenTarget()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age) values (@p0, @p1) returning id");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_OnNonIdentityEntity_ShouldNotAddOverridingSystemValue()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.BulkInsertInto<ISimpleEntity>(o => o.KeepIdentity()).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into simple_entity (id) values (@p0)");
    }
}
