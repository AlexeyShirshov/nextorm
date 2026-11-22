using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using Xunit;

namespace NextORM.Core.Tests;

public sealed class PostgresWindowNullTreatmentTests
{
    [Fact]
    public void ExplicitModesAndLegacyCallsKeepDistinctSql()
    {
        using var ctx = new Pg19Context();
        var rows = ctx.From<WindowRow>();
        var query = rows.Select(x => new
        {
            Lag = SqlFunctions.Sql.lag(x.Value).IgnoreNulls().Over(SqlFunctions.Sql.asc(() => x.Id)),
            Lead = SqlFunctions.Sql.lead(x.Value).RespectNulls().Over(SqlFunctions.Sql.asc(() => x.Id)),
            First = SqlFunctions.Sql.first_value(x.Value).IgnoreNulls().Over(SqlFunctions.Sql.asc(() => x.Id)),
            Last = SqlFunctions.Sql.last_value(x.Value).RespectNulls().Over(SqlFunctions.Sql.asc(() => x.Id)),
            Nth = SqlFunctions.Sql.nth_value(x.Value, 2).IgnoreNulls().Over(SqlFunctions.Sql.asc(() => x.Id)),
            Legacy = SqlFunctions.Sql.lag(x.Value).Over(SqlFunctions.Sql.asc(() => x.Id))
        });
        var sql = SqlOf(ctx, query);
        sql.Should().ContainEquivalentOf(") ignore nulls over (").And.ContainEquivalentOf(") respect nulls over (")
            .And.ContainEquivalentOf(") over (");
        sql.Should().ContainEquivalentOf("lag(").And.ContainEquivalentOf("lead(")
            .And.ContainEquivalentOf("first_value(").And.ContainEquivalentOf("last_value(")
            .And.ContainEquivalentOf("nth_value(");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pg17RefusesBothExplicitModes(bool ignore)
    {
        using var ctx = new Pg17Context();
        var rows = ctx.From<WindowRow>();
        var query = ignore
            ? rows.Select(x => SqlFunctions.Sql.lag(x.Value).IgnoreNulls().Over(SqlFunctions.Sql.asc(() => x.Id)))
            : rows.Select(x => SqlFunctions.Sql.lag(x.Value).RespectNulls().Over(SqlFunctions.Sql.asc(() => x.Id)));
        var prepare = () => SqlOf(ctx, query);
        prepare.Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL 19*");
    }

    [Fact]
    public void NamedWindowEmitsBothModifiersBeforeOver()
    {
        using var ctx = new Pg19Context();
        var rows = ctx.From<WindowRow>();
        var sql = SqlOf(ctx, rows.Window("w", orderBy: [rows.Asc(x => x.Id)]).Select(x => new
        {
            Ignore = SqlFunctions.Sql.lag(x.Value).IgnoreNulls().Over("w"),
            Respect = SqlFunctions.Sql.lag(x.Value).RespectNulls().Over("w"),
            Legacy = SqlFunctions.Sql.lag(x.Value).Over("w")
        }));
        sql.Should().ContainEquivalentOf(") ignore nulls over w")
            .And.ContainEquivalentOf(") respect nulls over w").And.ContainEquivalentOf(") over w");
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> query) =>
        ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None))
        .DbCommand.CommandText;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheReusesEachShapeWithoutAliasingOtherModes(bool named)
    {
        using var ctx = new Pg19Context();
        QueryCommand<int?> Query(int mode)
        {
            var rows = ctx.From<CacheRow>();
            if (named)
            {
                var window = rows.Window("w", orderBy: [rows.Asc(x => x.Id)]);
                return mode switch
                {
                    1 => window.Select(x => SqlFunctions.Sql.lag(x.Value).IgnoreNulls().Over("w")),
                    2 => window.Select(x => SqlFunctions.Sql.lag(x.Value).RespectNulls().Over("w")),
                    _ => window.Select(x => SqlFunctions.Sql.lag(x.Value).Over("w"))
                };
            }

            return mode switch
            {
                1 => rows.Select(x => SqlFunctions.Sql.lag(x.Value).IgnoreNulls().Over(SqlFunctions.Sql.asc(() => x.Id))),
                2 => rows.Select(x => SqlFunctions.Sql.lag(x.Value).RespectNulls().Over(SqlFunctions.Sql.asc(() => x.Id))),
                _ => rows.Select(x => SqlFunctions.Sql.lag(x.Value).Over(SqlFunctions.Sql.asc(() => x.Id)))
            };
        }

        var legacy = ctx.GetPreparedQueryCommand(Query(0), false, true, CancellationToken.None);
        var ignore = ctx.GetPreparedQueryCommand(Query(1), false, true, CancellationToken.None);
        var respect = ctx.GetPreparedQueryCommand(Query(2), false, true, CancellationToken.None);

        ignore.Should().NotBeSameAs(legacy);
        respect.Should().NotBeSameAs(legacy).And.NotBeSameAs(ignore);
        ctx.GetPreparedQueryCommand(Query(2), false, true, CancellationToken.None).Should().BeSameAs(respect);
        ctx.GetPreparedQueryCommand(Query(0), false, true, CancellationToken.None).Should().BeSameAs(legacy);
        ctx.GetPreparedQueryCommand(Query(1), false, true, CancellationToken.None).Should().BeSameAs(ignore);
    }

    [Fact]
    public void InvalidModifierUsageIsRejectedBeforeWindowRendering()
    {
        using var ctx = new Pg19Context();
        var rows = ctx.From<WindowRow>();
        var ranking = () => SqlOf(ctx, rows.Select(x => SqlFunctions.Sql.row_number().IgnoreNulls()
            .Over(SqlFunctions.Sql.asc(() => x.Id))));
        var duplicate = () => SqlOf(ctx, rows.Select(x => SqlFunctions.Sql.lag(x.Value).IgnoreNulls().RespectNulls()
            .Over(SqlFunctions.Sql.asc(() => x.Id))));
        ranking.Should().Throw<NotSupportedException>().WithMessage("*Specify one null treatment*");
        duplicate.Should().Throw<NotSupportedException>().WithMessage("*Specify one null treatment*");
    }

    private sealed class Pg19Context() : PostgresDataContext(
        "Host=localhost", new DataContextBuilder(), new Version(19, 0));

    private sealed class Pg17Context() : PostgresDataContext(
        "Host=localhost", new DataContextBuilder(), new Version(17, 0));

    private sealed class WindowRow
    {
        public int Id { get; set; }
        public int? Value { get; set; }
    }

    private sealed class CacheRow
    {
        public int Id { get; set; }
        public int? Value { get; set; }
    }
}
