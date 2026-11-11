namespace NextORM.Integration.Tests;

/// <summary>
/// Live coverage of <c>SelectGroup</c>/<c>SelectGroupHaving</c> on every container-backed provider.
/// The expected values live in <see cref="SelectGroupLiveOracles"/> and are derived from the shared
/// <c>extrema_entity</c> fixture, so a provider-specific SQL defect cannot pass as "some rows".
/// </summary>
public abstract class SelectGroupLiveTestsBase : ProviderTestSuite
{
    private EntityBuilder<ExtremaEntity> Extrema => _sut.DataProvider.From<ExtremaEntity>();

    [Fact]
    public void KeyedGrouping_ExactRows() => SelectGroupLiveOracles.KeyedGrouping(Extrema);

    [Fact]
    public void SelectGroupHaving_Keyed_ExactRows() => SelectGroupLiveOracles.SelectGroupHaving(Extrema);

    [Fact]
    public void SelectGroupHaving_GlobalAggregation_ExactTotalAndHavingOutcome() => SelectGroupLiveOracles.SelectGroupHavingGlobalAggregation(Extrema);

    [Fact]
    public void SelectGroupHaving_ConstantProjection_OneRowAndEmptyInput() => SelectGroupLiveOracles.SelectGroupHavingConstantProjection(Extrema);

    [Fact]
    public void HavingCombined_ExactSurvivingGroups() => SelectGroupLiveOracles.HavingCombined(Extrema);

    [Fact]
    public void GlobalAggregation_ExactTotalAndHavingOutcome() => SelectGroupLiveOracles.GlobalAggregation(Extrema);

    [Fact]
    public void ConstantProjection_OneRowAndEmptyInput() => SelectGroupLiveOracles.ConstantProjection(Extrema);

    [Fact]
    public void NullableKey_NullGroupOnce() => SelectGroupLiveOracles.NullableKey(Extrema);

    [Fact]
    public void OrderingTake_ExactPage() => SelectGroupLiveOracles.OrderingTake(Extrema);
}
