using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// D-232 (#232, rv=5) V18 closure: the typed PIVOT value that is absent from the data must be proven
/// against a real SQL Server, not against the in-memory fixture. The typed overload declares the value
/// list verbatim (no membership validation), so a value absent from every source row still renders its
/// column and the database returns SQL <c>NULL</c> for that cell while the grouping row stays present
/// through a declared value that does occur. The class name contains <c>T232SqlServerTests</c>, so the
/// <c>FullyQualifiedName~T232SqlServerTests</c> selector runs it together with the rv4 execution suite.
/// The database is provided by <see cref="SqlServerTestProvider"/> (Testcontainers unless
/// <c>NEXTORM_SQLSERVER_CONNECTION</c> wins); a missing server skips the whole class rather than
/// passing it.
/// </summary>
[Collection("SqlServer")]
public sealed class T232SqlServerTestsRv5 : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    /// <summary>The shared context with the T232 fixture tables created and seeded.</summary>
    private IDataContext Ctx
    {
        get
        {
            var ctx = _sut.DataProvider;
            T232SqlServerFixture.EnsureSeeded(ctx);
            return ctx;
        }
    }

    [Fact]
    public void TypedPivot_ValueAbsentFromTheData_MaterializesTheCellAsNull_FromTheDatabase()
    {
        var ctx = Ctx;

        // The frozen fixture holds quarters 1 and 2 only (North 1/10, North 2/20, South 1/5). The
        // declaration mixes the present values "1"/"2" with the absent value "3", so the grouping rows
        // must survive through "1" while the "3" cell can only come from the database.
        var rows = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1"), Q2 = pv.Value<decimal?>("2"), Q3 = pv.Value<decimal?>("3") })
            .Select(x => new { x.Category, x.Q1, x.Q2, x.Q3 })
            .ToList()
            .OrderBy(r => r.Category)
            .ToList();

        // The rows and the present cells are materialized from the executed statement.
        rows.Should().HaveCount(2);
        rows.Select(r => r.Category).Should().Equal("North", "South");
        rows.Select(r => r.Q1).Should().Equal(new decimal?[] { 10m, 5m }, "quarter 1 occurs for both groups");
        rows.Single(r => r.Category == "North").Q2.Should().Be(20m, "quarter 2 occurs for North");
        rows.Single(r => r.Category == "South").Q2.Should().BeNull("quarter 2 does not occur for South");

        // The absent value's actual database result is NULL for every materialized row.
        rows.Should().OnlyContain(r => r.Q3 == null, "a PIVOT value absent from the data returns SQL NULL");
    }

    [Fact]
    public void TypedPivot_OnlyAnAbsentValue_StillReturnsTheGroupedRowsWithNullCells()
    {
        var ctx = Ctx;

        // The single declared value is absent from every source row: the grouping rows must still be
        // returned by SQL Server (one row per category) with a NULL cell, never dropped or collapsed.
        var rows = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q3 = pv.Value<decimal?>("3") })
            .Select(x => new { x.Category, x.Q3 })
            .ToList()
            .OrderBy(r => r.Category)
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Category).Should().Equal("North", "South");
        rows.Should().OnlyContain(r => r.Q3 == null);
    }
}
