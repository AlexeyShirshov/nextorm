using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Sharding;

public sealed class ShardParityTests
{
    [Fact]
    public void UnshardedEntity_ShouldKeepSqlAndParameterNamesWithUseSharding()
    {
        var plain = CreateContext(new DataContextBuilder());
        var topology = new ShardTopology(4096);
        topology.AddShard("east", "Server=east");
        topology.AddShard("west", "Server=west");
        var sharded = CreateContext(new DataContextBuilder().UseSharding(topology));
        using (plain)
        using (sharded)
        {
            var plainQuery = Prepare(plain);
            var shardedQuery = Prepare(sharded);
            plainQuery.Sql.Should().Be(shardedQuery.Sql);
            plainQuery.ParameterNames.Should().Equal(shardedQuery.ParameterNames);

            var plainInsert = plain.CreateInsertBuilder<UnshardedRow>().Value(row => row.Id, 7).ToSql();
            var shardedInsert = sharded.CreateInsertBuilder<UnshardedRow>().Value(row => row.Id, 7).ToSql();
            plainInsert.Should().Be(shardedInsert);
            ParameterNamesIn(plainInsert).Should().Equal(ParameterNamesIn(shardedInsert));
        }
    }

    private static SqliteDataContext CreateContext(DataContextBuilder builder)
        => new(builder.UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:")));

    private static (string Sql, string[] ParameterNames) Prepare(SqliteDataContext context)
    {
        var minimum = 5;
        var query = context.From<UnshardedRow>().Where(row => row.Id > minimum).Select(row => row.Id);
        var prepared = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None);
        return (prepared.DbCommand.CommandText,
            prepared.DbCommand.Parameters.Cast<System.Data.Common.DbParameter>()
                .Select(parameter => parameter.ParameterName).ToArray());
    }

    private static string[] ParameterNamesIn(string sql)
        => sql.Split([' ', ',', '(', ')', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.StartsWith("@", StringComparison.Ordinal)
                || token.StartsWith("$", StringComparison.Ordinal))
            .Select(token => token[1..])
            .ToArray();

    private sealed class UnshardedRow
    {
        public int Id { get; set; }
    }
}
