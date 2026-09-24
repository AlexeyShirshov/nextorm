using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on MySQL. MySQL uses the portable path (no native
/// <c>MySqlBulkCopy</c>) and has no <c>RETURNING</c>; these tests never open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private static InsertEntity Row(string name, int age) => new() { Name = name, Age = age };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldIncludeIdentityColumnWithoutClause()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into insert_entity (id, name, age) values (@p0, @p1, @p2)");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldUseInsertIgnore()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.BulkInsertInto<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert ignore into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => ctx.BulkInsertInto<IInsertEntity>().Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        act.Should().Throw<NotSupportedException>();
    }
}
