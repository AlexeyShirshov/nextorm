using FluentAssertions;
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
}
