using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D19 (F19 boundary): live boundary checks against the pinned <c>25.8-alpine</c> (25.8.33.6)
/// server. The §1 table functions still execute; every table function the server exposes outside the
/// thirteen delivered names — the whole §4 external-engine family included — is rejected by the
/// dialect, so the §4 categorization hides no delivered member and implements none of the
/// out-of-scope external-I/O surface.
/// </summary>
public sealed class T286bF19TableFunctionBoundaryTests : ProviderTestSuite
{
    private static readonly string[] DeliveredTableFunctions =
    [
        "numbers", "numbers_mt", "zeros", "zeros_mt", "generateRandom", "values",
        "url", "s3", "file", "remote", "remoteSecure", "cluster", "clusterAllReplicas",
    ];

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void DeliveredTableFunctions_ShouldStillExecuteOnThePinnedServer()
    {
        using var context = ClickHouseTestProvider.Instance.CreateContext();

        var numbers = context.FromTableFunction(() => SqlFunctions.ClickHouse.numbers(3))
            .Select(r => new { r.Value })
            .ToList();
        numbers.Select(r => r.Value).Should().Equal(0L, 1L, 2L);

        var zeros = context.FromTableFunction(() => SqlFunctions.ClickHouse.zeros(2))
            .Select(r => new { r.Value })
            .ToList();
        zeros.Select(r => r.Value).Should().Equal((byte)0, (byte)0);
    }

    [Fact]
    public void EveryServerTableFunctionOutsideTheDeliveredSet_ShouldRemainUnimplemented()
    {
        using var context = ClickHouseTestProvider.Instance.CreateContext();
        using var result = context.ExecuteRaw("select name from system.table_functions");
        var vendorNames = result.Read<string>().Distinct().ToArray();

        vendorNames.Should().Contain(["numbers", "mysql", "postgresql", "s3Cluster"],
            "the server exposes both the delivered and the §4 external-engine table functions");

        using (new AssertionScope())
        {
            foreach (var name in vendorNames)
                ClickHouseDialect.Instance.SupportsTableFunction(name)
                    .Should().Be(DeliveredTableFunctions.Contains(name),
                        "'{0}' is outside the thirteen delivered table functions", name);
        }
    }
}
