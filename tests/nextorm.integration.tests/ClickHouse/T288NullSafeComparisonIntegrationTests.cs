using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T288 (issue #288): the explicit null-safe comparison helpers against a real ClickHouse server.
/// ClickHouse has no <see cref="CommonTestSuite"/>-derived class because its native
/// <c>isNotDistinctFrom</c>/<c>&lt;=&gt;</c> spellings are rejected outside a JOIN ON section (Code 48), so the
/// shared scenario body runs here over the guarded emulation only.
/// <para>
/// The suite inherits <see cref="ProviderTestSuite"/>, whose constructor fails closed when the provider is
/// unreachable — a missing container must not turn the row into a skip.
/// </para>
/// </summary>
public sealed class T288NullSafeComparisonClickHouseIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void T288_ClickHouse_TruthTable_ShouldMatchByRowsInPredicateAndProjection()
        => T288NullSafeComparisonFixture.TruthTable(_sut.DataProvider);

    [Fact]
    public void T288_ClickHouse_ColumnParameterAndReversedOperands_ShouldAgreeByRows()
        => T288NullSafeComparisonFixture.ColumnParameterAndReversed(_sut.DataProvider);

    [Fact]
    public void T288_ClickHouse_CapturedNullThenValue_ShouldRebindWithoutValueDependentSql()
        => T288NullSafeComparisonFixture.CapturedNullThenValueRebinding(_sut.DataProvider);

    [Fact]
    public void T288_ClickHouse_LiteralAndTypedNull_ShouldFoldAndReturnRows()
        => T288NullSafeComparisonFixture.LiteralNullAndDefault(_sut.DataProvider);

    [Fact]
    public void T288_ClickHouse_OuterJoinNullExtension_ShouldStayNullSafe()
        => T288NullSafeComparisonFixture.OuterJoinNullExtension(_sut.DataProvider);

    [Fact]
    public void T288_ClickHouse_SharedCommandAndPlanCache_ShouldNotLeakState()
        => T288NullSafeComparisonFixture.SharedCommandAndCacheReuse(_sut.DataProvider);

    /// <summary>
    /// The singleton value <c>0</c> is distinct from <c>NULL</c>: the emulation uses guarded equality and
    /// no sentinel normalisation, so a NULL operand can never compare equal to a real zero. Ordinary
    /// equality would have returned <c>false</c> for the NULL row, which is why the assertion is on both
    /// forms over the same rows.
    /// </summary>
    [Fact]
    public void T288_ClickHouse_NullVersusZero_ShouldBeDistinct()
    {
        var ctx = _sut.DataProvider;

        T288NullSafeComparisonFixture.Reset(ctx);
        try
        {
            ctx.CreateInsertBuilder<IT288NullSafeComparisonRow>()
                .Values(new T288NullSafeComparisonRow[]
                {
                    new() { Id = 6, A = null, B = 0 },
                    new() { Id = 7, A = 0, B = 0 },
                })
                .Insert();

            var rows = ctx.From<IT288NullSafeComparisonRow>()
                .Where(x => x.Id >= 6)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Nd = SqlFunctions.Sql.is_not_distinct_from(x.A, x.B),
                    D = SqlFunctions.Sql.is_distinct_from(x.A, x.B),
                })
                .ToList();

            rows.Select(x => x.Id).Should().Equal(6, 7);
            rows.Select(x => x.Nd).Should().Equal(false, true);
            rows.Select(x => x.D).Should().Equal(true, false);
        }
        finally
        {
            T288NullSafeComparisonFixture.Cleanup(ctx);
        }
    }
}
