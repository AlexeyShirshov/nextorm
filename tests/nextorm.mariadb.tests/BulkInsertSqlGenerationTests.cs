using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on MariaDB. MariaDB uses the portable path (no native
/// bulk copy) and has no identity-insert toggle; these tests never open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    [Fact]
    public void BulkInsert_ShouldRenderExplicitValues()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.BulkInsertInto<ISimpleEntity>().Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into simple_entity (id) values (@p0)");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.BulkInsertInto<ISimpleEntity>(o => o.Table("staging", "bulk_target")).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (id) values (@p0)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldNotEmitIdentityInsertToggle()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.BulkInsertInto<ISimpleEntity>(o => o.KeepIdentity()).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().NotContain("identity_insert");
    }
}
