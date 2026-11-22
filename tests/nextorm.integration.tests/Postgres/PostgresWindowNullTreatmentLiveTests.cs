using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using Npgsql;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class PostgresWindowNullTreatmentLiveTests
{
    [Fact]
    public void WindowNullTreatment_OnPg19_DistinguishesModesAndReusesQueries()
    {
        Assert.SkipUnless(PostgresContainer.IsAvailable, "PostgreSQL is unavailable.");
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        Assert.SkipUnless(connection.PostgreSqlVersion.Major == 19, "Requires PostgreSQL 19.");
        Seed(connection);
        using var ctx = new Pg19Context(connection);
        var e = ctx.From<D403WindowRow>();
        var inline = e.OrderBy(x => x.Id).Select(x => new
        {
            Legacy = SqlFunctions.Sql.lag(x.Value).Over(SqlFunctions.Sql.asc(() => x.Id)),
            Ignore = SqlFunctions.Sql.lag(x.Value).IgnoreNulls().Over(SqlFunctions.Sql.asc(() => x.Id)),
            Respect = SqlFunctions.Sql.lag(x.Value).RespectNulls().Over(SqlFunctions.Sql.asc(() => x.Id))
        });
        var named = e.Window("w", orderBy: [e.Asc(x => x.Id)]).OrderBy(x => x.Id).Select(x => new
        {
            First = SqlFunctions.Sql.first_value(x.Value).IgnoreNulls().Over("w"),
            Last = SqlFunctions.Sql.last_value(x.Value).IgnoreNulls().Over("w"),
            Nth = SqlFunctions.Sql.nth_value(x.Value, 2).IgnoreNulls().Over("w"),
            Lead = SqlFunctions.Sql.lead(x.Value).IgnoreNulls().Over("w")
        });
        var observed = inline.ToList();
        observed.Select(x => x.Legacy).Should().Equal(null, null, 10, null);
        observed.Select(x => x.Respect).Should().Equal(null, null, 10, null);
        observed.Select(x => x.Ignore).Should().Equal(null, null, 10, 10);
        var namedObserved = named.ToList();
        namedObserved.Select(x => x.First).Should().Equal(null, 10, 10, 10);
        namedObserved.Select(x => x.Last).Should().Equal(null, 10, 10, 30);
        namedObserved.Select(x => x.Nth).Should().Equal(null, null, null, 30);
        namedObserved.Select(x => x.Lead).Should().Equal(10, 30, 30, null);
        inline.ToList().Should().Equal(observed);
        named.ToList().Should().Equal(namedObserved);
    }

    [Fact]
    public void WindowNullTreatment_OnPg17_RefusesExplicitRequests()
    {
        Assert.SkipUnless(PostgresContainer.IsAvailable, "PostgreSQL is unavailable.");
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        Assert.SkipUnless(connection.PostgreSqlVersion.Major == 17, "Requires PostgreSQL 17.");
        Seed(connection);
        using var ctx = new Pg17Context(connection);
        var e = ctx.From<D403WindowRow>();
        var legacy = e.OrderBy(x => x.Id)
            .Select(x => SqlFunctions.Sql.lag(x.Value).Over(SqlFunctions.Sql.asc(() => x.Id)));
        legacy.ToList().Should().Equal(null, null, 10, null);
        var ignore = () => e.Select(x => SqlFunctions.Sql.lag(x.Value).IgnoreNulls()
            .Over(SqlFunctions.Sql.asc(() => x.Id))).ToList();
        var respect = () => e.Select(x => SqlFunctions.Sql.lag(x.Value).RespectNulls()
            .Over(SqlFunctions.Sql.asc(() => x.Id))).ToList();
        ignore.Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL 19*");
        respect.Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL 19*");
    }

    private static void Seed(NpgsqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            create temp table d403windowrow (id int not null, value int null);
            insert into d403windowrow values (1, null), (2, 10), (3, null), (4, 30);
            """;
        command.ExecuteNonQuery();
    }

    private sealed class Pg19Context(NpgsqlConnection connection) :
        PostgresDataContext(connection, new DataContextBuilder(), new Version(19, 0));

    private sealed class Pg17Context(NpgsqlConnection connection) :
        PostgresDataContext(connection, new DataContextBuilder(), new Version(17, 0));

    private sealed class D403WindowRow
    {
        public int Id { get; set; }
        public int? Value { get; set; }
    }
}
