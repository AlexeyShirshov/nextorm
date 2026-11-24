using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

public sealed class CorrelatedTvfCrossJoinSqlTests
{
    [Fact]
    public void CrossJoin_UsesTheImmediatelyPrecedingTvfAlias()
    {
        using var ctx = new SqliteDataContext("Data Source=unused.db", new DataContextBuilder());
        var query = ctx.From<IComplexEntity>()
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.String))
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.Item2.Value))
            .Select(row => row.Item3.Value);
        var prepared = (DbPreparedQueryCommand<string?>)ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText;

        sql.Should().MatchRegex(@"(?i)from complex_entity as '(?<root>\w+)'\s+cross join json_each\(\k<root>\.somestring\) as '(?<first>\w+)'\s+cross join json_each\(\k<first>\.value\) as '\w+'");
        sql.Should().NotContainEquivalentOf("lateral").And.NotContainEquivalentOf("(select");
    }
}
