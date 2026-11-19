using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.ClickHouse;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

public sealed class BoundedDeleteSqlTests
{
    [Theory]
    [InlineData("sqlite", "limit 2")]
    [InlineData("postgres", "limit 2")]
    [InlineData("sqlserver", "top(2)")]
    [InlineData("mysql", "limit 2")]
    [InlineData("mariadb", "limit 2")]
    public void BoundedDeleteRendersOneStatementWithAnOrderedCompositeKeySelection(string provider, string cap)
    {
        var options = new DataContextBuilder();
        using var sqlite = provider == "sqlite" ? new SqliteConnection("Data Source=:memory:") : null;
        using var context = CreateContext(provider, options, sqlite);
        var tenant = 41;
        var sql = context.CreateDeleteBuilder<SqlRow>().Where(row => row.Tenant == tenant)
            .OrderByDescending(row => row.Id).Limit(2).ToSql();

        sql.Should().StartWith("delete from ").And.NotContain(";").And
            .Contain("select ").And.Contain("nextorm_key_0").And.Contain("nextorm_key_1").And
            .Contain(" order by ").And.Contain(" desc").And.Contain(cap);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("mariadb")]
    public void BoundedDeleteUsesDistinctNamesForTargetAndKeySelectionAcrossDialects(string provider)
    {
        foreach (var quoted in new[] { false, true })
        {
            using var sqlite = provider == "sqlite" ? new SqliteConnection("Data Source=:memory:") : null;
            using var context = CreateContext(provider, new DataContextBuilder().UseQuotedIdentifiers(quoted), sqlite);
            var sql = context.CreateDeleteBuilder<CollisionRow>().Where(row => row.Tenant == 1)
                .OrderBy(row => row.Key).Limit(1).ToSql();

            var target = !quoted ? "nextorm_keys.nextorm_key_0" : provider switch
            {
                "sqlserver" => "[nextorm_keys].[nextorm_key_0]",
                "mysql" or "mariadb" => "`nextorm_keys`.`nextorm_key_0`",
                _ => "\"nextorm_keys\".\"nextorm_key_0\"",
            };
            sql.Should().StartWith("delete from ").And.NotContain(";").And
                .Contain($"as bounded_delete_selection where bounded_delete_selection.nextorm_key_0 = {target}");
        }
    }

    private static DataContext CreateContext(string provider, DataContextBuilder options, SqliteConnection? sqlite) =>
        provider switch
        {
            "sqlite" => new SqliteDataContext(sqlite!, options),
            "postgres" => new PostgresDataContext("Host=localhost", options),
            "sqlserver" => new SqlServerDataContext("Server=localhost", options),
            "mysql" => new MySqlDataContext("Server=localhost", options),
            "mariadb" => new MariaDbDataContext("Server=localhost", options),
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

    [Fact]
    public void ClickHouseRejectsBoundedDeleteButStillRendersPlainMutation()
    {
        using var context = new ClickHouseDataContext("Host=localhost", new DataContextBuilder());
        var delete = context.CreateDeleteBuilder<SqlRow>().Where(row => row.Tenant == 1);

        delete.ToSql().Should().StartWith("alter table ");
        var bounded = () => delete.Limit(1).ToSql();
        bounded.Should().Throw<NotSupportedException>().WithMessage("*bounded DELETE*");
    }

    [Fact]
    public void KeylessTableSupportsUnboundedPredicateButNotABound()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        var delete = context.CreateDeleteBuilder<KeylessRow>().Where(row => row.Value == 1);

        delete.ToSql().Should().StartWith("delete from ").And.Contain(" where ");
        var bounded = () => delete.Limit(1).ToSql();
        bounded.Should().Throw<InvalidOperationException>().WithMessage("*declared unique non-null key*");
        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Table("d187_delete_sql")]
    private sealed class SqlRow
    {
        [Key]
        public int Tenant { get; set; }

        [Key]
        public int Id { get; set; }
    }

    private sealed class KeylessRow
    {
        public int Value { get; set; }
    }

    [Table("nextorm_keys")]
    private sealed class CollisionRow
    {
        [Key, Column("nextorm_key_0")]
        public int Key { get; set; }

        [Column("tenant")]
        public int Tenant { get; set; }
    }
}
