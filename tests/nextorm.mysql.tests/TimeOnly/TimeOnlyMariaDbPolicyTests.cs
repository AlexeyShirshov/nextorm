using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.MySql.Tests;

/// <summary>
/// T340 (#340) DB-free provider policy matrix. Every context is constructed with a placeholder connection
/// string (no server is contacted — the policy is a property, and composing a column accessor never opens a
/// connection), so the exact refusal strings can be pinned without a container.
/// <para>
/// MariaDB inherits <see cref="MySqlDataContext"/>, so the interesting cell is that it carries its own text:
/// an inherited MySQL message would name the wrong server.
/// </para>
/// </summary>
public class TimeOnlyMariaDbPolicyTests
{
    private const string PlaceholderConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    private const string MySqlRefusal =
        "TimeOnly projection refused for MySQL: TIME is a duration; use TimeSpan or an explicit value converter.";

    private const string MariaDbRefusal =
        "TimeOnly projection refused for MariaDB: TIME is a duration; use TimeSpan or an explicit value converter.";

    private const string ClickHouseRefusal =
        "TimeOnly projection refused for ClickHouse: there is no native time-of-day type and TimeOnly is bound as String; use an explicit value converter.";

    [Fact]
    public void MySql_ShouldRefuseWithItsOwnText()
    {
        using var context = new MySqlDataContext(PlaceholderConnectionString, new DataContextBuilder());

        context.TimeOnlyProjectionRefusalValue.Should().Be(MySqlRefusal);
        RefusalOf(context).Should().Be(MySqlRefusal);
    }

    [Fact]
    public void MariaDb_ShouldRefuseWithItsOwnText_AndDifferFromMySql()
    {
        using var mySql = new MySqlDataContext(PlaceholderConnectionString, new DataContextBuilder());
        using var mariaDb = new MariaDbDataContext(PlaceholderConnectionString, new DataContextBuilder());

        mariaDb.TimeOnlyProjectionRefusalValue.Should().Be(MariaDbRefusal);
        RefusalOf(mariaDb).Should().Be(MariaDbRefusal);
        mariaDb.TimeOnlyProjectionRefusalValue.Should().NotBe(mySql.TimeOnlyProjectionRefusalValue,
            "MariaDB must not inherit the MySQL wording");
    }

    [Fact]
    public void ClickHouse_ShouldRefuseWithItsOwnText()
    {
        using var context = new ClickHouseDataContext(
            "Host=localhost;Port=8123;Database=nextorm;Username=nextorm;Password=nextorm",
            new DataContextBuilder());

        context.TimeOnlyProjectionRefusalValue.Should().Be(ClickHouseRefusal);
        RefusalOf(context).Should().Be(ClickHouseRefusal);
    }

    [Fact]
    public void SqlServerAndPostgres_ShouldPermitTheProjection()
    {
        using var sqlServer = new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder());
        using var postgres = new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder());

        sqlServer.TimeOnlyProjectionRefusalValue.Should().BeNull("SQL Server has a native time type");
        postgres.TimeOnlyProjectionRefusalValue.Should().BeNull("PostgreSQL has a native time type");

        var record = Expression.Parameter(typeof(IDataRecord), "record");
        Expression? sqlServerAccessor = null;
        Expression? postgresAccessor = null;
        FluentActions.Invoking(() => sqlServerAccessor = sqlServer.MapColumnExpression(Column(), record)).Should().NotThrow();
        FluentActions.Invoking(() => postgresAccessor = postgres.MapColumnExpression(Column(), record)).Should().NotThrow();

        sqlServerAccessor!.Type.Should().Be(typeof(TimeOnly?));
        postgresAccessor!.Type.Should().Be(typeof(TimeOnly?));
    }

    /// <summary>
    /// The provider policy is the whole contract: under a permitted provider the composed
    /// <see cref="TimeOnly"/> mapper keeps the exact day-of-day value and enforces the day domain, while under
    /// a refusing provider the mapper cannot even be composed. Both halves run against the real context type
    /// of each provider (placeholder connection strings, no server contacted), so a provider override that
    /// silently widened or lost the policy fails here.
    /// <para>
    /// SQLite is not part of the matrix: <c>nextorm.sqlite</c> is not a reference of this test project, and
    /// adding one is outside the task's planned footprint. Its refusal is covered by the core/SQLite suites
    /// and by the live integration suite.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("SqlServer", null)]
    [InlineData("PostgreSQL", null)]
    [InlineData("MySQL", MySqlRefusal)]
    [InlineData("MariaDB", MariaDbRefusal)]
    [InlineData("ClickHouse", ClickHouseRefusal)]
    public void MapColumn_DomainGuard_ShouldHoldUnderEveryProviderPolicy(string provider, string? expectedRefusal)
    {
        using var context = CreateContext(provider);
        var record = Expression.Parameter(typeof(IDataRecord), "record");

        if (expectedRefusal is null)
        {
            var body = context.MapColumnExpression(Column(), record);
            body.Type.Should().Be(typeof(TimeOnly?));

            var mapper = Expression.Lambda<Func<IDataRecord, TimeOnly?>>(body, record).Compile();

            // The largest representable time of day keeps every tick: no modulo, no truncation.
            Read(mapper, TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1))!.Value
                .Should().Be(TimeOnly.FromTimeSpan(TimeSpan.FromTicks(863_999_999_999)));

            // A full day is a duration, not a time of day: refused instead of wrapped to midnight.
            FluentActions.Invoking(() => Read(mapper, TimeSpan.FromTicks(TimeSpan.TicksPerDay)))
                .Should().Throw<NotSupportedException>()
                .WithMessage(TimeOnlyProjectionReader.OutOfDomainMessage);

            // Only a native TimeOnly/TimeSpan is a time of day: an integer is never reinterpreted.
            FluentActions.Invoking(() => Read(mapper, 42))
                .Should().Throw<NotSupportedException>()
                .WithMessage(TimeOnlyProjectionReader.AlienValueMessage);
        }
        else
        {
            FluentActions.Invoking(() => context.MapColumnExpression(Column(), record))
                .Should().Throw<NotSupportedException>()
                .WithMessage(expectedRefusal);
        }
    }

    private static DataContext CreateContext(string provider) => provider switch
    {
        "SqlServer" => new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder()),
        "PostgreSQL" => new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder()),
        "MySQL" => new MySqlDataContext(PlaceholderConnectionString, new DataContextBuilder()),
        "MariaDB" => new MariaDbDataContext(PlaceholderConnectionString, new DataContextBuilder()),
        "ClickHouse" => new ClickHouseDataContext(
            "Host=localhost;Port=8123;Database=nextorm;Username=nextorm;Password=nextorm",
            new DataContextBuilder()),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider"),
    };

    // One single-column reader whose GetValue returns the supplied boxed value.
    private static TimeOnly? Read(Func<IDataRecord, TimeOnly?> mapper, object value)
    {
        var table = new DataTable();
        table.Columns.Add("T", typeof(object));
        table.Rows.Add(value);
        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();
        return mapper(reader);
    }

    private static string RefusalOf(DataContext context)
        => FluentActions.Invoking(() => context.MapColumnExpression(Column(), Expression.Parameter(typeof(IDataRecord), "record")))
            .Should().Throw<NotSupportedException>()
            .Which.Message;

    // The constructor derives Nullable from the declared type, so the mapper emits the nullable wrapper.
    private static SelectExpression Column() => new(typeof(TimeOnly?))
    {
        Index = 0,
        PropertyName = "T",
    };
}
