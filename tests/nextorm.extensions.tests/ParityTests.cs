using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class ParityTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
    }

    private static (SqliteConnection Connection, SqliteDataContext Context) CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE Entity (
  Id INTEGER PRIMARY KEY,
  Score INTEGER NULL,
  Name TEXT NULL
);
INSERT INTO Entity VALUES
  (1, 10, 'a'),
  (2, 10, 'b'),
  (3, 20, 'c'),
  (4, NULL, 'd'),
  (5, 20, 'e'),
  (6, 30, 'f'),
  (7, 5, 'g');";
        cmd.ExecuteNonQuery();
        var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();
        return (connection, ctx);
    }

    private static long[] Ids<T>(IEnumerable<T> rows, Func<T, long> id)
        => rows.Select(id).OrderBy(x => x).ToArray();

    [Fact]
    public void All_Global_Scalar_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<Entity>().SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        var ext = ctx.From<Entity>().SelectWhereMaxEx(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(ext, x => x.Id).Should().Equal(Ids(built, x => x.Id));
    }

    [Fact]
    public void All_Composite_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<Entity>().SelectWhereMin(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.All).ToList();
        var ext = ctx.From<Entity>().SelectWhereMinEx(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.All).ToList();
        Ids(ext, x => x.Id).Should().Equal(Ids(built, x => x.Id));
    }
}
