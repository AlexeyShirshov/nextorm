using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T287 (issue #287): the live NULL-placement matrix against a real MariaDB server. MariaDB has no
/// <see cref="CommonTestSuite"/>-derived integration class and inherits the MySQL dialect, so the shared
/// oracle is re-pinned here through the established harness route
/// (<see cref="MariaDbContainer.ConnectionString"/> + <see cref="MariaDbDataContext"/>); MariaDB is never
/// run through the MySQL container.
/// <para>
/// An unavailable container fails the facts instead of skipping them: a skip is not a pass, and the
/// V17–V20 cells stay unproven until the harness is reachable.
/// </para>
/// </summary>
public sealed class T287NullOrderingMariaDbTests : IDisposable
{
    private const string ProviderName = "mariadb";

    private readonly IDataContext _ctx;

    public T287NullOrderingMariaDbTests(ITestOutputHelper output)
    {
        MariaDbContainer.IsAvailable.Should().BeTrue(
            $"the MariaDB container must be reachable for the T287 live matrix: " +
            $"{MariaDbContainer.Failure ?? "unavailable without a reported reason"}");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());

        using var version = _ctx.ExecuteRaw("select version() as version");
        var banner = version.Read<string>().Single();
        banner.Should().Contain("MariaDB",
            "the live matrix must run against the MariaDB container, never through the MySQL container");
        output.WriteLine($"T287 MariaDB server version: {banner}");
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void T287NullOrdering_LiveMatrix_ShouldMatchThePortableOracle()
        => T287NullOrderingFixture.AssertLiveMatrix(_ctx, ProviderName);

    [Fact]
    public void T287NullOrdering_LiveNullableStringParity_ShouldMatchTheIntegerShape()
        => T287NullOrderingFixture.AssertNullableStringParity(_ctx, ProviderName);

    [Fact]
    public void T287NullOrdering_LiveAllNullKeys_ShouldFallBackToTheSecondaryOrder()
        => T287NullOrderingFixture.AssertAllNullKeys(_ctx, ProviderName);

    [Fact]
    public void T287NullOrdering_LiveNoNullKeys_ShouldOrderValuesByDirection()
        => T287NullOrderingFixture.AssertNoNullKeys(_ctx, ProviderName);

    [Fact]
    public void T287NullOrdering_LiveEmptyTable_ShouldReturnNoRows()
        => T287NullOrderingFixture.AssertEmptyTable(_ctx, ProviderName);

    [Fact]
    public void T287NullOrdering_LiveDuplicateKeys_ShouldBreakTiesByTheSecondaryKey()
        => T287NullOrderingFixture.AssertDuplicateKeys(_ctx, ProviderName);

    [Fact]
    public void T287NullOrdering_LiveParameterizedKey_ShouldBindTheRepeatedCaseOccurrence()
        => T287NullOrderingFixture.AssertParameterizedKey(_ctx, ProviderName);

    [Fact]
    public void T287NullOrdering_LivePerParentRank_ShouldPlaceEachPartitionsNullsAsRequested()
        => T287NullOrderingFixture.AssertPerParentRankPlacement(_ctx, ProviderName);

    [Fact]
    public void T287NullOrdering_LiveExtremeRow_ShouldPlaceTheSurvivorsNullsAsRequested()
        => T287NullOrderingFixture.AssertExtremeRowPlacement(_ctx, ProviderName);
}
