using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_sqlite_tests;

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

    [Fact]
    public void CrossJoin_BindsNamesOnRootAndBothTableFunctions()
    {
        using var ctx = new SqliteDataContext("Data Source=unused.db", new DataContextBuilder());
        var query = ctx.From<IComplexEntity>()
            .WithAlias(Alias.StateSource)
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.StateSource.String), Alias.States)
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.States.Value), Alias.References)
            .Select(row => row.References.Value);
        var prepared = (DbPreparedQueryCommand<string?>)ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText;

        sql.Should().MatchRegex(@"(?i)select (?<last>\w+)\.value\s+from complex_entity as '(?<root>\w+)'\s+cross join json_each\(\k<root>\.somestring\) as '(?<first>\w+)'\s+cross join json_each\(\k<first>\.value\) as '\k<last>'");
        sql.Should().NotContainEquivalentOf("lateral").And.NotContainEquivalentOf("(select");
    }

    [Fact]
    public void CrossJoin_CanNameATableFunctionAfterAPositionalJoin()
    {
        using var ctx = new SqliteDataContext("Data Source=unused.db", new DataContextBuilder());
        var query = ctx.From<IComplexEntity>()
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.String))
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.Item2.Value), Alias.References)
            .CrossJoin(ctx.From<IComplexEntity>(), Alias.Other)
            .Select(row => row.Other.String);
        var prepared = (DbPreparedQueryCommand<string?>)ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText;

        sql.Should().MatchRegex(@"(?i)select (?<other>\w+)\.somestring as 'String'\s+from complex_entity as '(?<root>\w+)'\s+cross join json_each\(\k<root>\.somestring\) as '(?<states>\w+)'\s+cross join json_each\(\k<states>\.value\) as '\w+'\s+cross join complex_entity as '\k<other>'");
    }
}
