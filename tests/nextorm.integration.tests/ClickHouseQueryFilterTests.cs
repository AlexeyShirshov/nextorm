using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Global query filter coverage (#108 D6) for ClickHouse. ClickHouse does not derive the shared
/// <see cref="CommonTestSuite"/> (Memory has no mutation/identity support), so the filter-relevant
/// SELECT and INSERT-validation cases are re-pinned here against the container-backed provider.
/// </summary>
public sealed class ClickHouseQueryFilterTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static int _queryFilterIdSeed = -700_000_000;

    private static int NextQueryFilterBase() => Interlocked.Add(ref _queryFilterIdSeed, -10);

    private IDataContext QueryFilterContext(int tenant)
    {
        var ctx = _sut.DataProvider;
        ctx.Properties[QueryFilterFixtures.TenantKey] = tenant;
        return ctx;
    }

    private void SeedQueryFilterRows(params QueryFilterEntity[] rows)
        => _sut.DataProvider.InsertInto<QueryFilterEntity>().IgnoreFilters().Values(rows).Insert();

    private static QueryFilterEntity Active(int id, string name)
        => new() { Id = id, TenantId = 1, IsDeleted = false, Name = name };

    private static QueryFilterEntity SoftDeleted(int id, string name)
        => new() { Id = id, TenantId = 1, IsDeleted = true, Name = name };

    private static QueryFilterEntity ForeignTenant(int id, string name)
        => new() { Id = id, TenantId = 2, IsDeleted = false, Name = name };

    [Fact]
    public void QueryFilter_Select_KeyedAndAnonymousFilters_ShouldApply()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        EntityBuilder<QueryFilterEntity> Range() =>
            ctx.From<QueryFilterEntity>().Where(x => x.Id >= b - 2 && x.Id <= b);

        Range().Select(x => x.Id).ToList().Should().BeEquivalentTo([b]);
        Range().IgnoreFilters().Select(x => x.Id).ToList().Should().BeEquivalentTo([b, b - 1, b - 2]);
        Range().IgnoreFilters(["tenant"]).Select(x => x.Id).ToList().Should().BeEquivalentTo([b, b - 2]);
        Range().IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id).ToList().Should().BeEquivalentTo([b, b - 1]);
        Range().IgnoreFilters(typeof(QueryFilterEntity)).Select(x => x.Id).ToList().Should().BeEquivalentTo([b, b - 1, b - 2]);
    }

    [Fact]
    public void QueryFilter_Insert_ViolatingFilter_ShouldThrowBeforeExecution()
    {
        var ctx = QueryFilterContext(1);

        var act = () => ctx.InsertInto<QueryFilterEntity>()
            .Values(ForeignTenant(NextQueryFilterBase(), "ch-foreign"))
            .Insert();

        act.Should().Throw<QueryFilterException>();
    }

    [Fact]
    public void QueryFilter_Insert_IgnoreFilters_SkipsValidation()
    {
        var ctx = QueryFilterContext(1);
        var id = NextQueryFilterBase();

        ctx.InsertInto<QueryFilterEntity>()
            .IgnoreFilters()
            .Values(ForeignTenant(id, "ch-foreign-ignored"))
            .Insert();

        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == id).Select(x => x.TenantId).Single().Should().Be(2);
    }
}
