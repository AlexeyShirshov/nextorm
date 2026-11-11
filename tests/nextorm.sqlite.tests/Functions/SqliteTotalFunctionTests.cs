using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation for the SQLite <c>total()</c> aggregate: it must keep its native spelling (never be
/// rewritten to <c>sum</c> or another aggregate), accept a column or a parameterised argument, keep the
/// projection alias and stay next to the JSON1 aggregates it ships with.
/// </summary>
public class SqliteTotalFunctionTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void Total_OnColumn_ShouldEmitNativeAggregate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { T = SqlFunctions.Sqlite.total(x.Id) }));

        sql.Should().Contain("total(id)");
        sql.Should().NotContain("sum(");
        sql.Should().NotContain("avg(");
    }

    [Fact]
    public void Total_OnParameterisedArgument_ShouldEmitNativeAggregate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var limit = 10.0;

        var prepared = (DbPreparedQueryCommand<double>)ctx.GetPreparedQueryCommand(
            e.Select(x => SqlFunctions.Sqlite.total(x.Int + limit)), false, false, CancellationToken.None);
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("total(");
        sql.Should().Contain("$limit");
        sql.Should().NotContain("sum(");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Contain(10.0);
    }

    [Fact]
    public void Total_ShouldKeepProjectionAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { RunningTotal = SqlFunctions.Sqlite.total(x.Id) }));

        sql.Should().Contain("total(id) as 'RunningTotal'");
    }

    [Fact]
    public void Total_ShouldExposeNonNullableDoubleReturnType()
    {
        var method = typeof(SqliteFunctions).GetMethod(nameof(SqliteFunctions.total));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(double));
        new NullabilityInfoContext().Create(method.ReturnParameter).ReadState.Should().Be(NullabilityState.NotNull);

        // A lambda assigning the call to a double compiles only because the member's return type is
        // double; the marker itself is never invoked (its surface is the null SqlFunctions.Sqlite).
        Func<int, double> marker = value => SqlFunctions.Sqlite.total(value);
        marker.Should().NotBeNull();
    }

    [Fact]
    public void JsonAggregates_ShouldStillRenderNextToTotal()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Sqlite.json_group_array(x.String),
            O = SqlFunctions.Sqlite.json_group_object(x.Int, x.String),
            T = SqlFunctions.Sqlite.total(x.Int)
        }));

        sql.Should().Contain("json_group_array(somestring)");
        sql.Should().Contain("json_group_object(nullableint, somestring)");
        sql.Should().Contain("total(nullableint)");
    }
}
