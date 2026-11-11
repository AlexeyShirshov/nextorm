using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Postgres;
using NextORM.SqlServer;
using NextORM.MySql;
using NextORM.MariaDb;
using NextORM.ClickHouse;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public sealed class DialectShapeTests
{
    private sealed class Entity
    {
        public int Id { get; set; }
        public int? GroupKey { get; set; }
        public int Value { get; set; }
    }

    [Fact]
    public void NullableGroupedAll_SelectsExpectedShapeForEachDialect()
    {
        AssertShape(new PostgresDataContext("Host=localhost;Database=none", new DataContextBuilder()), rank: false);
        AssertShape(new SqlServerDataContext("Server=localhost;Database=none", new DataContextBuilder()), rank: false);
        AssertShape(new MySqlDataContext("Server=localhost;Database=none", new DataContextBuilder()), rank: false);
        AssertShape(new MariaDbDataContext("Server=localhost;Database=none", new DataContextBuilder()), rank: false);
        AssertShape(new SqliteDataContext("Data Source=:memory:", new DataContextBuilder()), rank: false);
        AssertShape(new ClickHouseDataContext("Host=localhost", new DataContextBuilder()), rank: true);
    }

    private static void AssertShape(DataContext context, bool rank)
    {
        using (context)
        {
            var command = context.From<Entity>()
                .SelectWhereMin(x => x.Value, ExtremeRowTies.All, x => x.GroupKey)
                .ToCommand();
            var prepared = (DbPreparedQueryCommand<Entity>)context.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
            var sql = prepared.SqlStmt!;
            if (rank)
                sql.Should().Contain("rank()");
            else
                sql.Should().Contain(" join ").And.NotContain("rank()");
        }
    }
}
