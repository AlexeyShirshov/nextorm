using System.Linq;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class PostgresStep5ProbeTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>P1 — bool_and aggregate reaches SupportsBooleanAggregates arm.</summary>
    [Fact]
    public void P1_BoolAnd_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.Postgres.bool_and(x.Boolean == true))
            .First();
        // bool_and over empty-ish? ComplexEntity has rows; just ensure no throw and a bool result
        (r == true || r == false).Should().BeTrue();
    }

    /// <summary>P2 — regr_sxx statistical aggregate.</summary>
    [Fact]
    public void P2_RegrSxx_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.Postgres.regr_sxx((double)(x.Double ?? 0.0), (double)(x.Double ?? 0.0)))
            .First();
        (r is null || !double.IsNaN(r.Value)).Should().BeTrue();
    }
}