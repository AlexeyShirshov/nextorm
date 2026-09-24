using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Exercises SQL Server specific query shapes that are only covered by SQL-generation tests today:
/// the native <c>PIVOT</c>/<c>UNPIVOT</c> operators and <c>DATEFROMPARTS</c>.
/// </summary>
public sealed class SqlServerFeaturesTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Fact]
    public void Pivot_ShouldReshape()
    {
        var rows = _sut.DataProvider.From("pivot_entity")
            .Pivot(PivotAggregate.Sum, e => e["q1"], e => e["id"], PivotValue.Create("1"), PivotValue.Create("2"))
            .Select(e => new { A = e.GetNullableDecimal("[1]"), B = e.GetNullableDecimal("[2]") })
            .ToList();

        // q2 is carried through the pivot, so each source row becomes its own result row.
        rows.Should().HaveCount(2);
        rows.Sum(r => (r.A ?? 0m) + (r.B ?? 0m)).Should().Be(40m);
    }

    [Fact]
    public void Unpivot_ShouldStackColumns()
    {
        var rows = _sut.DataProvider.From("pivot_entity")
            .Unpivot("val", "nm", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(e => new { Val = e["val"].AsInt, Nm = e["nm"].AsString })
            .ToList();

        // two rows x two columns unpivoted.
        rows.Should().HaveCount(4);
        rows.Should().Contain(r => r.Nm == "q1" && r.Val == 10);
        rows.Should().Contain(r => r.Nm == "q2" && r.Val == 40);
    }

    [Fact]
    public void DateFromParts_ShouldBuildDate()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_from_parts(2023, 5, 17))
            .First();

        r.Should().Be(new DateTime(2023, 5, 17));
    }

    [Fact]
    public void TableHint_ShouldExecute()
    {
        var withTableHint = _sut.SimpleEntity
            .WithTableHint("nolock")
            .Select(x => x.Id)
            .ToList();
        withTableHint.Should().HaveCount(10);
    }
}
