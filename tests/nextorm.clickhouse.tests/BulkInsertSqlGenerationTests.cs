using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on ClickHouse. ClickHouse uses the portable path and has
/// no identity concept; these tests never open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private static InsertEntity Row(string name, int age) => new() { Name = name, Age = age };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (id, name, age) values (@p0, @p1, @p2), (@p3, @p4, @p5)");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (id, name, age) values (@p0, @p1, @p2)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldNotEmitIdentityInsertToggle()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().NotContain("identity_insert");
    }
}
