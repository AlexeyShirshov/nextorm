using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse-specific coverage of the typed CTE surface (#146 slice B). ClickHouse does not derive
/// <see cref="CommonTestSuite"/>; the new typed recursion must be refused before any database command,
/// while an ordinary (non-recursive) typed CTE must keep working end-to-end.
/// </summary>
public sealed class ClickHouseTypedCteIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void TypedCte_Ordinary_ShouldStillReturnData()
    {
        var ctx = _sut.DataProvider;

        var recent = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.RequiredString })
            .AsCte("typed_recent");

        var rows = ctx.From(recent).Select(r => new { r.Id, r.RequiredString }).ToList();

        rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void TypedRecursiveCte_ShouldThrowNotSupportedBeforeAnyCommand()
    {
        var ctx = _sut.DataProvider;

        var nums = ctx.From<IComplexEntity>()
            .Where(c => c.Id == 1)
            .Select(c => c.Id)
            .AsRecursiveCte("typed_nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1));

        var act = () => ctx.From(nums).Limit(20).Select(n => n).ToList();

        act.Should().Throw<NotSupportedException>();
    }
}
