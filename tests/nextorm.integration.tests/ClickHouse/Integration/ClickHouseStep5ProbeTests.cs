using System.Linq;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("ClickHouse")]
public sealed class ClickHouseStep5ProbeTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>P4 — VendorName mapping via quantile_tdigest (untested vendor aggregate).</summary>
    [Fact]
    public void P4_QuantileTDigest_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.ClickHouse.quantile_tdigest(0.5, x.Double))
            .First();
        (r is null || !double.IsNaN(r.Value)).Should().BeTrue();
    }

    /// <summary>S3 — additional ClickHouse vendor aggregates (topK, skewPop, kurtSamp).</summary>
    [Fact]
    public void S3_VendorAggregates_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Select(x => new
            {
                A = SqlFunctions.ClickHouse.top_k(2, x.String),
                B = SqlFunctions.ClickHouse.skew_pop(x.Double),
                C = SqlFunctions.ClickHouse.kurt_samp(x.Double)
            })
            .First();
        r.Should().NotBeNull();
    }
}