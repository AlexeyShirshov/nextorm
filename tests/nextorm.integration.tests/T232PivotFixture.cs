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

/// <summary>
/// One deterministic row of the shared SQL Server <c>t232_sales</c> fixture (the frozen
/// <c>fixture_rows</c> of <c>artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/test-contract.json</c>).
/// </summary>
internal readonly record struct T232SalesRow(string Category, int Quarter, decimal? Margin);

/// <summary>
/// The PIVOT source entity of the shared SQL Server T232 fixture. It deliberately has no key column:
/// T-SQL <c>PIVOT</c> implicitly groups by every source column that is not the aggregate or the
/// <c>FOR</c> column, so an extra <c>id</c> column would split each category into one row per source
/// row and break the category/quarter grouping the oracle pins.
/// </summary>
[SqlTable(T232SqlServerFixture.SalesTable)]
internal interface IT232Sales
{
    [Column("category")]
    string? Category { get; set; }

    [Column("quarter")]
    int Quarter { get; set; }

    [Column("margin")]
    decimal? Margin { get; set; }
}

/// <summary>The UNPIVOT source entity of the shared SQL Server T232 fixture.</summary>
[SqlTable(T232SqlServerFixture.QuarterlyTable)]
internal interface IT232Quarterly
{
    [Column("category")]
    string? Category { get; set; }

    [Column("q1")]
    decimal? Q1 { get; set; }

    [Column("q2")]
    decimal? Q2 { get; set; }
}

/// <summary>
/// Shared SQL Server container fixture for the T232 (#232) typed PIVOT/UNPIVOT integration facts
/// (test contract row V01/V11/V14/V22/V24, evidence row E232-08). It owns the two deterministic
/// tables and the frozen rows the typed and legacy paths are compared over; it never asserts — the
/// assertions live in <c>T232SqlServerTests.cs</c>.
/// </summary>
internal static class T232SqlServerFixture
{
    internal const string SalesTable = "t232_sales";
    internal const string QuarterlyTable = "t232_quarterly";

    private static readonly object Gate = new();
    private static bool _seeded;

    /// <summary>The frozen PIVOT fixture; quarters 1 and 2 define the IN-list/order contract.</summary>
    internal static readonly T232SalesRow[] SalesRows =
    [
        new("North", 1, 10m),
        new("North", 2, 20m),
        new("South", 1, 5m)
    ];

    /// <summary>Creates and seeds the fixture tables once per process (idempotent).</summary>
    /// <param name="ctx">The SQL Server context to execute the DDL/DML through.</param>
    internal static void EnsureSeeded(IDataContext ctx)
    {
        if (_seeded)
            return;

        lock (Gate)
        {
            if (_seeded)
                return;

            Exec(ctx, $"drop table if exists {SalesTable}");
            Exec(ctx, $"drop table if exists {QuarterlyTable}");

            // No id/primary-key column: see IT232Sales — it would become part of the PIVOT grouping.
            Exec(ctx, $"create table {SalesTable} (category nvarchar(50) not null, quarter int not null, margin decimal(18,2) null)");
            Exec(ctx, $"insert into {SalesTable} (category, quarter, margin) values (N'North', 1, 10), (N'North', 2, 20), (N'South', 1, 5)");

            Exec(ctx, $"create table {QuarterlyTable} (category nvarchar(50) not null, q1 decimal(18,2) null, q2 decimal(18,2) null)");
            Exec(ctx, $"insert into {QuarterlyTable} (category, q1, q2) values (N'North', 10, 20), (N'South', 5, null)");

            _seeded = true;
        }
    }

    private static void Exec(IDataContext ctx, string sql)
    {
        using var _ = ctx.ExecuteRaw(sql);
    }
}
