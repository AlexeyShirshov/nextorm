using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// #279: a selector-free <c>LongCount()</c> in a correlated projection is an aggregate terminal, so it
/// renders an aggregate scalar subquery instead of the receiver's entity projection. MySQL's <c>count</c>
/// already returns a 64-bit value, so the wide function is the same <c>count(*)</c>, and the single-row
/// restriction is <c>limit 1</c>. These tests never open a database connection.
/// </summary>
public class T279LongCountSqlTests
{
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void LongCountProjection_ShouldRenderWideAggregateScalarSubquery()
    {
        using var ctx = MySqlTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).LongCount()
        }));

        sql.Should().Be("select t1.id, (select count(*)\n from simple_entity as `t2`\n"
            + " where cast(t2.id as signed) = t1.id\n"
            + "limit 1) as `c`\n from complex_entity as `t1`");
        sql.Should().Contain("count(*)");
        sql.Should().Contain("limit 1");
    }

    /// <summary>
    /// Sibling parity: <c>Count()</c> keeps rendering its own aggregate subquery independently.
    /// </summary>
    [Fact]
    public void CountProjection_ShouldStillRenderItsOwnAggregate()
    {
        using var ctx = MySqlTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var countSql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).Count()
        }));

        countSql.Should().Contain("(select count(*)\n from simple_entity as `t2`");
        countSql.Should().Contain("limit 1");
    }

    /// <summary>
    /// The wide count counts rows, not a projected column: the subquery must not fall back to the
    /// receiver's entity projection and must not filter on a column value, so rows whose projected value
    /// is null are still counted.
    /// </summary>
    [Fact]
    public void LongCountProjection_ShouldCountRowsNotNullableColumns()
    {
        using var ctx = MySqlTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).LongCount()
        }));

        sql.Should().Contain("count(*)");
        sql.Should().NotContain("(select id\n from simple_entity");
        sql.Should().NotContain("count(t2.");
        sql.Should().NotContain("is not null");
    }

    /// <summary>
    /// MariaDB uses the MySQL dialect family, so the projection <c>LongCount()</c> lowering must render
    /// the same wide aggregate scalar subquery there (R8). This is SQL-generation evidence only: no live
    /// MariaDB server is exercised here.
    /// </summary>
    [Fact]
    public void MariaDbLongCountProjection_ShouldUseSharedMySqlDialect()
    {
        using var mysql = new MySqlDataContext(ConnectionString, new DataContextBuilder());
        using var mariaDb = new MariaDbDataContext(ConnectionString, new DataContextBuilder());
        var outerMySql = mysql.From<IComplexEntity>();
        var innerMySql = mysql.From<ISimpleEntity>();
        var outerMaria = mariaDb.From<IComplexEntity>();
        var innerMaria = mariaDb.From<ISimpleEntity>();

        var mysqlSql = SqlOf(mysql, outerMySql.Select(it => new
        {
            it.Id,
            c = innerMySql.Where(s => s.Id == it.Id).LongCount()
        }));
        var mariaDbSql = SqlOf(mariaDb, outerMaria.Select(it => new
        {
            it.Id,
            c = innerMaria.Where(s => s.Id == it.Id).LongCount()
        }));

        mariaDbSql.Should().Be(mysqlSql);
        mariaDbSql.Should().Contain("(select count(*)\n from simple_entity as `t2`");
        mariaDbSql.Should().NotContain("(select id\n from simple_entity");
    }
}
