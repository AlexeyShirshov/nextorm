using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// #320 (T320) PostgreSQL SQL generation for the runtime TVF source under a correlated APPLY. These
/// tests never open a database connection: the connection string is a placeholder and only the
/// rendered statement and its bound parameters are inspected.
/// </summary>
internal static class T320PgTvf
{
    public static TvfEntityBuilder<ISimpleEntity> Echo(long id) => new("t320_echo");

    public static TvfEntityBuilder<ISimpleEntity> EchoPair(long id, int extra) => new("t320_echo_pair");
}

[Trait("Category", "T320")]
public class T320RuntimeTvfSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    [Fact]
    public void CorrelatedRuntimeTvfApply_ShouldRenderLateralWithTheOuterColumnAndAParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var extra = 7;

        var prepared = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .CrossApply(row => T320PgTvf.EchoPair(row.Id, extra))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            storeInCache: false);

        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("cross join lateral", "PostgreSQL renders the correlated APPLY as a lateral join");
        sql.Should().Contain("t320_echo_pair", "the runtime factory supplies the applied source name");
        sql.Should().Contain("t1.id", "the correlated argument stays an outer column reference");
        sql.Should().Contain("@extra", "a captured argument is a bound parameter");
        sql.Should().NotContain("t320_echo_pair(7", "the outer column must never be evaluated into a constant");

        ParametersOf(prepared).Should().Equal("extra=7");
    }

    [Fact]
    public void CorrelatedRuntimeTvfApply_CacheHit_ShouldRefreshTheCaptureAndKeepTheOuterColumn()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();
        var extra = 7;

        QueryCommand<int> Build() => ctx.From<ISimpleEntity>()
            .CrossApply(row => T320PgTvf.EchoPair(row.Id, extra))
            .Select(p => p.Item1.Id);

        var first = Prepare(ctx, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        ParametersOf(first).Should().Equal("extra=7");
        firstSql.Should().Contain("t1.id");

        extra = 8;
        var second = Prepare(ctx, Build(), storeInCache: true);

        ReferenceEquals(first, second).Should().BeTrue(
            "the same correlated shape must reuse the cached plan so the refresh path is exercised");
        Normalize(second.DbCommand.CommandText).Should().Be(firstSql, "a capture change must not re-render the statement");
        ParametersOf(second).Should().Equal(new[] { "extra=8" }, "the cache hit must bind the new capture");
        Normalize(second.DbCommand.CommandText).Should().Contain(
            "t1.id",
            "the correlated column stays a column reference, never an evaluated value");
    }
}
