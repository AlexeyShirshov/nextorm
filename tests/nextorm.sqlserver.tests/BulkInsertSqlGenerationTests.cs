using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on SQL Server. These tests never open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private static InsertEntity Row(string name, int age) => new() { Name = name, Age = age };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldWrapWithIdentityInsert()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("set identity_insert insert_entity on; insert into insert_entity (id, name, age) values (@p0, @p1, @p2); set identity_insert insert_entity off");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.BulkInsertInto<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*skip conflicting rows*");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldEmitOutput()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into insert_entity (name, age) output inserted.id values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverride_ShouldTargetOverriddenTable()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.Table("bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into bulk_target (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverride_KeepIdentity_ShouldWrapOverriddenTable()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.Table("staging", "bulk_target").KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("set identity_insert staging.bulk_target on; insert into staging.bulk_target (id, name, age) values (@p0, @p1, @p2); set identity_insert staging.bulk_target off");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_OnNonIdentityEntity_ShouldNotWrapWithIdentityInsert()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.BulkInsertInto<ISimpleEntity>(o => o.KeepIdentity()).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into simple_entity (id) values (@p0)");
    }

    [Fact]
    public void MapBulkCopyOptions_ShouldSetEveryEnabledFlag()
    {
        var options = SqlServerDataContext.MapBulkCopyOptions(checkConstraints: true, tableLock: true, keepNulls: true, fireTriggers: true);

        options.Should().HaveFlag(SqlBulkCopyOptions.CheckConstraints);
        options.Should().HaveFlag(SqlBulkCopyOptions.TableLock);
        options.Should().HaveFlag(SqlBulkCopyOptions.KeepNulls);
        options.Should().HaveFlag(SqlBulkCopyOptions.FireTriggers);
    }

    [Fact]
    public void MapBulkCopyOptions_None_ShouldBeDefault()
    {
        var options = SqlServerDataContext.MapBulkCopyOptions(checkConstraints: false, tableLock: false, keepNulls: false, fireTriggers: false);

        options.Should().Be(SqlBulkCopyOptions.Default);
    }

    [Fact]
    public void BulkInsert_BulkCopyFlag_ShouldNotChangeRenderedSql()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.TableLock()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_Returning_WithBulkCopyFlag_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.BulkInsertInto<IInsertEntity>(o => o.TableLock())
            .Values([Row("a", 1)])
            .ReturningKey<long>()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock*SqlBulkCopy*");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_WithBulkCopyFlag_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity().FireTriggers())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*FireTriggers*");
    }
}
