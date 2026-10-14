using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.MariaDb;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// The minimal entity the shared typed PIVOT/UNPIVOT capability case maps. The query is rejected while
/// it renders, so the table is never created and the columns only have to resolve to SQL names.
/// </summary>
[SqlTable("t232_pivot_capability")]
internal interface IT232CapabilityEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("category")]
    string? Category { get; set; }

    [Column("quarter")]
    int Quarter { get; set; }

    [Column("margin")]
    decimal? Margin { get; set; }
}

/// <summary>
/// Shared body of the T232 (#232) typed PIVOT/UNPIVOT capability case (test contract row V20,
/// evidence row E232-07): SQL Server is the only provider whose dialect exposes a PIVOT renderer, so
/// every other provider must reject both typed operators with <see cref="NotSupportedException"/> when
/// the query renders — exactly like the untyped overload. The supporting SQL Server path is asserted
/// by the typed SQL Server tests instead.
/// </summary>
/// <remarks>
/// MariaDB and ClickHouse have no <see cref="CommonTestSuite"/>-derived integration class, so the
/// shared case is re-pinned for them by the two provider hosts below (as the shared CSV and
/// JSON-stream facts are). Their container execution belongs to D-05.
/// </remarks>
internal static class T232PivotFixture
{
    /// <summary>
    /// Asserts that <paramref name="context"/> rejects both typed operators while rendering, naming the
    /// provider in the failure message.
    /// </summary>
    /// <param name="context">The provider context under test.</param>
    /// <param name="provider">The provider name, used for the failure message only.</param>
    internal static void AssertTypedOperatorsRejected(IDataContext context, string provider)
    {
        var pivot = () => context.From<IT232CapabilityEntity>()
            .Pivot(PivotAggregate.Sum, e => e.Margin, e => e.Quarter, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") })
            .Select(x => new { x.Category, x.Q1 })
            .ToList();

        pivot.Should().Throw<NotSupportedException>(
                $"{provider} has no PIVOT renderer and must reject the typed PIVOT source")
            .WithMessage("The PIVOT source construct is not supported by this provider.");

        var unpivot = () => context.From<IT232CapabilityEntity>()
            .Unpivot(
                (m, u) => new { m.Category, Name = u.Name<string>("qtr"), Value = u.Value<decimal?>("val") },
                UnpivotColumn.Create("q1"),
                UnpivotColumn.Create("q2"))
            .Select(x => new { x.Category, x.Name, x.Value })
            .ToList();

        unpivot.Should().Throw<NotSupportedException>(
                $"{provider} has no UNPIVOT renderer and must reject the typed UNPIVOT source")
            .WithMessage("The UNPIVOT source construct is not supported by this provider.");
    }
}

/// <summary>
/// Re-pins the shared typed PIVOT/UNPIVOT capability case for MariaDB, which (like ClickHouse) has no
/// <see cref="CommonTestSuite"/>-derived integration class and uses its own container harness.
/// </summary>
public sealed class MariaDbT232PivotTests : IDisposable
{
    private readonly IDataContext _context;

    public MariaDbT232PivotTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _context = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void T232TypedOperators_RespectCapability()
        => T232PivotFixture.AssertTypedOperatorsRejected(_context, "mariadb");
}

/// <summary>
/// Re-pins the shared typed PIVOT/UNPIVOT capability case for ClickHouse, which does not derive
/// <see cref="CommonTestSuite"/>.
/// </summary>
public sealed class ClickHouseT232PivotTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void T232TypedOperators_RespectCapability()
        => T232PivotFixture.AssertTypedOperatorsRejected(_sut.DataProvider, Provider.Name);
}
