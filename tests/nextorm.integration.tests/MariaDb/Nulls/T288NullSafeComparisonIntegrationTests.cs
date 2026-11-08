using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T288 (issue #288): the explicit null-safe comparison helpers against a real MariaDB server. MariaDB has
/// no <see cref="CommonTestSuite"/>-derived integration class and inherits the MySQL dialect, so the shared
/// scenario body is re-pinned here through the established harness route
/// (<see cref="MariaDbContainer.ConnectionString"/> + <see cref="MariaDbDataContext"/>); MariaDB is never
/// run through the MySQL container.
/// <para>
/// An unavailable container fails the facts instead of skipping them: a skip is not a pass, and the
/// MariaDB row stays unproven until the harness is reachable.
/// </para>
/// </summary>
public sealed class T288NullSafeComparisonMariaDbIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public T288NullSafeComparisonMariaDbIntegrationTests(ITestOutputHelper output)
    {
        MariaDbContainer.IsAvailable.Should().BeTrue(
            "the MariaDB container must be reachable for the T288 live matrix: " +
            $"{MariaDbContainer.Failure ?? "unavailable without a reported reason"}");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());

        using var version = _ctx.ExecuteRaw("select version() as version");
        var banner = version.Read<string>().Single();
        banner.Should().Contain("MariaDB",
            "the live matrix must run against the MariaDB container, never through the MySQL container");
        output.WriteLine($"T288 MariaDB server version: {banner}");
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void T288_MariaDb_TruthTable_ShouldMatchByRowsInPredicateAndProjection()
        => T288NullSafeComparisonFixture.TruthTable(_ctx);

    [Fact]
    public void T288_MariaDb_ColumnParameterAndReversedOperands_ShouldAgreeByRows()
        => T288NullSafeComparisonFixture.ColumnParameterAndReversed(_ctx);

    [Fact]
    public void T288_MariaDb_CapturedNullThenValue_ShouldRebindWithoutValueDependentSql()
        => T288NullSafeComparisonFixture.CapturedNullThenValueRebinding(_ctx);

    [Fact]
    public void T288_MariaDb_LiteralAndTypedNull_ShouldFoldAndReturnRows()
        => T288NullSafeComparisonFixture.LiteralNullAndDefault(_ctx);

    [Fact]
    public void T288_MariaDb_OuterJoinNullExtension_ShouldStayNullSafe()
        => T288NullSafeComparisonFixture.OuterJoinNullExtension(_ctx);

    [Fact]
    public void T288_MariaDb_SharedCommandAndPlanCache_ShouldNotLeakState()
        => T288NullSafeComparisonFixture.SharedCommandAndCacheReuse(_ctx);
}
