using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// Pins the residual percentile capability contract of T285b: the ordered-set percentile/median gate
/// (<see cref="ISqlDialect.SupportsOrderedPercentiles"/>) is opt-in for PostgreSQL and fail-closed
/// everywhere else, and it never widens the pre-existing analytic
/// (<see cref="ISqlDialect.SupportsPercentileWindow"/>) or ordered-aggregate
/// (<see cref="ISqlDialect.SupportsOrderedAggregates"/>) gates.
/// </summary>
public sealed class T285bResidualAggregateContractTests
{
    [Fact]
    public void SupportsOrderedPercentiles_ShouldBeDefaultInterfaceMember_SoExternalDialectsKeepCompiling()
    {
        // A new abstract member would break every third-party ISqlDialect implementation; the default
        // member keeps them compiling and fail-closed.
        var getter = typeof(ISqlDialect).GetProperty(nameof(ISqlDialect.SupportsOrderedPercentiles))!.GetMethod!;

        getter.IsAbstract.Should().BeFalse();
        getter.GetBaseDefinition().DeclaringType.Should().Be(typeof(ISqlDialect));
    }

    [Fact]
    public void SupportsOrderedPercentiles_ShouldBeTrueOnlyForPostgres()
    {
        PostgresDialect.Instance.SupportsOrderedPercentiles.Should().BeTrue();

        SqlServerDialect.Instance.SupportsOrderedPercentiles.Should().BeFalse("the SQL Server shape is analytic");
        SqliteDialect.Instance.SupportsOrderedPercentiles.Should().BeFalse("SQLite has no percentile function");
        MySqlDialect.Instance.SupportsOrderedPercentiles.Should().BeFalse("MySQL has no WITHIN GROUP");
        MariaDbDialect.Instance.SupportsOrderedPercentiles.Should().BeFalse("the MariaDB shape is analytic");
        ClickHouseDialect.Instance.SupportsOrderedPercentiles.Should().BeFalse("ClickHouse has no WITHIN GROUP");
    }

    [Fact]
    public void SupportsOrderedPercentiles_ShouldNotRepurposeTheExistingGates()
    {
        // mode() is an ordered-set aggregate but not a percentile, so the new flag must stay separate
        // from the ordered-aggregate umbrella, and PostgreSQL keeps its analytic percentile gate closed.
        PostgresDialect.Instance.SupportsOrderedAggregates.Should().BeTrue();
        PostgresDialect.Instance.SupportsPercentileWindow.Should().BeFalse();

        SqlServerDialect.Instance.SupportsPercentileWindow.Should().BeTrue();
        MariaDbDialect.Instance.SupportsPercentileWindow.Should().BeTrue();
        MySqlDialect.Instance.SupportsPercentileWindow.Should().BeFalse();
        SqliteDialect.Instance.SupportsPercentileWindow.Should().BeFalse();
        ClickHouseDialect.Instance.SupportsPercentileWindow.Should().BeFalse();
    }
}
