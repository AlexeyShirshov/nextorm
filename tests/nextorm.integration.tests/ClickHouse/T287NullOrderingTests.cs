using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T287 (issue #287): ClickHouse has no <see cref="CommonTestSuite"/>-derived integration class, so the
/// shared live NULL-placement matrix is re-pinned here against the ClickHouse container. Every fact
/// delegates to <see cref="T287NullOrderingFixture"/>, so the oracle is the same portable one; the
/// provider default asserted by the control is ClickHouse's own (NULLS LAST for both directions).
/// The base <see cref="ProviderTestSuite"/> fails closed when the container is unavailable.
/// </summary>
public sealed class T287NullOrderingClickHouseTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void T287NullOrdering_LiveMatrix_ShouldMatchThePortableOracle()
        => T287NullOrderingFixture.AssertLiveMatrix(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveNullableStringParity_ShouldMatchTheIntegerShape()
        => T287NullOrderingFixture.AssertNullableStringParity(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveAllNullKeys_ShouldFallBackToTheSecondaryOrder()
        => T287NullOrderingFixture.AssertAllNullKeys(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveNoNullKeys_ShouldOrderValuesByDirection()
        => T287NullOrderingFixture.AssertNoNullKeys(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveEmptyTable_ShouldReturnNoRows()
        => T287NullOrderingFixture.AssertEmptyTable(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveDuplicateKeys_ShouldBreakTiesByTheSecondaryKey()
        => T287NullOrderingFixture.AssertDuplicateKeys(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveParameterizedKey_ShouldBindTheRepeatedCaseOccurrence()
        => T287NullOrderingFixture.AssertParameterizedKey(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LivePerParentRank_ShouldPlaceEachPartitionsNullsAsRequested()
        => T287NullOrderingFixture.AssertPerParentRankPlacement(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveExtremeRow_ShouldPlaceTheSurvivorsNullsAsRequested()
        => T287NullOrderingFixture.AssertExtremeRowPlacement(_sut.DataProvider, Provider.Name);
}
