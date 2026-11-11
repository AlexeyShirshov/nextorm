using System.Globalization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T291 (#291) live arm: an integer AVG must expose the fractional mean. Every fact runs against the
/// real database of the provider subclassing <see cref="CommonTestSuite"/> (SQLite, PostgreSQL,
/// SQL Server, MySQL; MariaDB and ClickHouse have their own classes) over the deterministic
/// <c>avg_fixture</c> rows.
/// <para>
/// The assertions take the result as <see cref="object"/> at the Call site, so the same source compiles
/// both before and after the public result-type change: the pre-fix run fails on the value (<c>1.5</c>
/// vs a truncated integer) and on the runtime type (<see cref="int"/> instead of <see cref="double"/>),
/// which is exactly the defect; the post-fix run proves the fractional value and the nullable-double
/// declared result. The selector itself stays a typed scalar projection: an explicit
/// <c>(object?)</c> cast inside <c>Select</c> renders malformed aggregate SQL today.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    private static void ShouldBeDouble(object? value, double expected, string scenario, double tolerance = 1e-12)
    {
        Convert.ToDouble(value, CultureInfo.InvariantCulture).Should().BeApproximately(
            expected, tolerance, "T291: {0} must keep the fractional average", scenario);
        value.Should().BeOfType<double>(
            "T291: {0} must materialize an integral AVG as System.Double (nullable at the API level)", scenario);
    }

    private static void ShouldBeNullDouble(object? value, string scenario)
        => value.Should().BeNull("T291: {0} over an empty set is SQL NULL", scenario);

    [Fact]
    public void T291_OrdinaryAverage_ShouldKeepFraction()
    {
        var e = _sut.AvgFixture;

        ShouldBeDouble(e.Where(x => x.Id <= 2).Select(x => SqlFunctions.Sql.avg(x.B)).First(), 1.5, "byte [1,2]");
        ShouldBeDouble(e.Where(x => x.Id <= 2).Select(x => SqlFunctions.Sql.avg(x.S)).First(), 1.5, "short [1,2]");
        ShouldBeDouble(e.Where(x => x.Id <= 2).Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1.5, "int [1,2]");
        ShouldBeDouble(e.Where(x => x.Id <= 2).Select(x => SqlFunctions.Sql.avg(x.L)).First(), 1.5, "long [1,2]");
    }

    [Fact]
    public void T291_OrdinaryAverageTerminal_ShouldKeepFraction()
    {
        var e = _sut.AvgFixture.Where(x => x.Id <= 2);

        ShouldBeDouble(e.Avg(x => x.I), 1.5, "terminal int [1,2]");
        ShouldBeDouble(e.Avg(x => x.B), 1.5, "terminal byte [1,2]");
        ShouldBeDouble(e.Avg(x => x.S), 1.5, "terminal short [1,2]");
    }

    [Fact]
    public async Task T291_OrdinaryAverageTerminalAsync_ShouldKeepFraction()
    {
        var e = _sut.AvgFixture.Where(x => x.Id <= 2);

        ShouldBeDouble(await e.AvgAsync(x => x.I), 1.5, "async terminal int [1,2]");
    }

    [Fact]
    public void T291_DistinctAverage_ShouldKeepFraction()
    {
        var e = _sut.AvgFixture.Where(x => x.Id <= 3);

        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg_distinct(x.I)).First(), 1.5, "distinct int [1,1,2]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg_distinct(x.B)).First(), 1.5, "distinct byte [1,1,2]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg_distinct(x.L)).First(), 1.5, "distinct long [1,1,2]");

        // Ordinary control over the same three rows: (1 + 2 + 1) / 3, not 1.5. The expectation is
        // exact per provider, never a wide band: MySQL and MariaDB evaluate AVG over an exact
        // integer argument as DECIMAL(14,4), so 4/3 is rendered exactly as 1.3333; the
        // binary-double dialects keep the unrounded quotient 4d/3d.
        if (Provider.Name is "mysql" or "mariadb")
            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1.3333, "ordinary int [1,2,1] DECIMAL(14,4)");
        else
            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 4d / 3d, "ordinary int [1,2,1]");
    }

    [Fact]
    public void T291_NullableAverage_ShouldIgnoreNulls()
    {
        var e = _sut.AvgFixture.Where(x => x.Id <= 3);

        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.NB)).First(), 1.5, "nullable byte [NULL,1,2]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.NS)).First(), 1.5, "nullable short [NULL,1,2]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.NI)).First(), 1.5, "nullable int [NULL,1,2]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.NL)).First(), 1.5, "nullable long [NULL,1,2]");
    }

    [Fact]
    public void T291_OneRowAverage_ShouldBeOne()
    {
        var e = _sut.AvgFixture.Where(x => x.Id == 1);

        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1d, "one row int [1]");

        // Row 1 seeds a NULL nullable byte, so the single-row nullable arm uses row 2 (nb = 1).
        var nullable = _sut.AvgFixture.Where(x => x.Id == 2);
        ShouldBeDouble(nullable.Select(x => SqlFunctions.Sql.avg(x.NB)).First(), 1d, "one row nullable byte [1]");
    }

    [Fact]
    public void T291_EmptySetAverage_ShouldBeNull()
    {
        var e = _sut.AvgFixture.Where(x => x.Id < 0);

        // A scalar AVG over an empty set still yields one row whose value is SQL NULL; FirstOrDefault
        // observes that NULL (First() rejects a NULL scalar by contract).
        ShouldBeNullDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).FirstOrDefault(), "int");
        ShouldBeNullDouble(e.Select(x => SqlFunctions.Sql.avg(x.NI)).FirstOrDefault(), "nullable int");
    }

    [Fact]
    public void T291_RunningWindowAverage_ShouldKeepFractions()
    {
        var rows = _sut.AvgFixture
            .Where(x => x.Id != 3)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                R = SqlFunctions.Sql.avg_over(x.I).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)),
                RL = SqlFunctions.Sql.avg_over(x.L).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 4);
        ShouldBeDouble(rows[0].R, 1d, "running window int row 1");
        ShouldBeDouble(rows[1].R, 1.5, "running window int row 2");
        ShouldBeDouble(rows[2].R, 2d, "running window int row 3");
        ShouldBeDouble(rows[2].RL, 2d, "running window long row 3");
    }

    [Fact]
    public void T291_WholePartitionWindowAverage_ShouldBeTwo()
    {
        var rows = _sut.AvgFixture
            .Where(x => x.Id != 3)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                R = SqlFunctions.Sql.avg_over(x.I).Over()
            })
            .ToList();

        rows.Should().HaveCount(3);
        ShouldBeDouble(rows[0].R, 2d, "whole-partition int row 1");
        ShouldBeDouble(rows[1].R, 2d, "whole-partition int row 2");
        ShouldBeDouble(rows[2].R, 2d, "whole-partition int row 3");
    }

    /// <summary>
    /// The ANSI <c>filter (where ...)</c> clause exists on SQLite and PostgreSQL; SQL Server and MySQL
    /// reject it with the existing unsupported-operation guard, which is unchanged by T291.
    /// </summary>
    [Fact]
    public void T291_FilteredAverage_ShouldKeepFraction()
    {
        if (Provider.Name is not ("sqlite" or "postgres"))
            return;

        var value = _sut.AvgFixture
            .Where(x => x.Id != 3)
            .Select(x => SqlFunctions.Sql.avg(x.I, () => x.Id <= 2))
            .First();

        ShouldBeDouble(value, 1.5, "filtered int [1,2,3] where <= 2");
    }
}
