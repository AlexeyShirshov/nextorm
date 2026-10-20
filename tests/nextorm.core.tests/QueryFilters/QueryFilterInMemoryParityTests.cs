using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// In-memory parity for the SELECT cases: the attribute-declared keyed and anonymous filters and every
/// selective <c>IgnoreFilters</c> form must resolve exactly like the database providers. The fixtures
/// mirror the provider-backed ones in the integration suite (<c>CommonTestSuite.QueryFilters</c>).
/// </summary>
public sealed class QueryFilterInMemoryParityTests
{
    private const string TenantKey = "nextorm_query_filter_tenant";

    [QueryFilter(FilterLambda = nameof(ActiveOnly))]
    [QueryFilter(FilterKey = "tenant", FilterLambda = nameof(Tenant))]
    public sealed class ParityFilterEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public bool IsDeleted { get; set; }

        public static Expression<Func<ParityFilterEntity, bool>> ActiveOnly() => e => !e.IsDeleted;

        public static Expression<Func<ParityFilterEntity, IDataContext, bool>> Tenant()
            => (e, c) => e.TenantId == (int)c.Properties[TenantKey];
    }

    /// <summary>The same keyed tenant filter on a second type, which scopes the intersection test.</summary>
    [QueryFilter(FilterKey = "tenant", FilterLambda = nameof(Tenant))]
    public sealed class ParityFilterSiblingEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        public static Expression<Func<ParityFilterSiblingEntity, IDataContext, bool>> Tenant()
            => (e, c) => e.TenantId == (int)c.Properties[TenantKey];
    }

    [Fact]
    public void SelectiveIgnores_ShouldMatchProviderFilterSemantics()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<ParityFilterEntity>().WithData(
        [
            new ParityFilterEntity { Id = 1, TenantId = 1, IsDeleted = false },
            new ParityFilterEntity { Id = 2, TenantId = 1, IsDeleted = true },
            new ParityFilterEntity { Id = 3, TenantId = 2, IsDeleted = false },
        ]);

        ctx.From<ParityFilterEntity>().Select(x => x.Id).ToList().Should().BeEquivalentTo([1]);
        ctx.From<ParityFilterEntity>().IgnoreFilters().Select(x => x.Id).ToList().Should().BeEquivalentTo([1, 2, 3]);
        ctx.From<ParityFilterEntity>().IgnoreFilters(["tenant"]).Select(x => x.Id).ToList().Should().BeEquivalentTo([1, 3]);
        ctx.From<ParityFilterEntity>().IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id).ToList().Should().BeEquivalentTo([1, 2]);
        ctx.From<ParityFilterEntity>().IgnoreFilters(typeof(ParityFilterEntity)).Select(x => x.Id).ToList().Should().BeEquivalentTo([1, 2, 3]);
        ctx.From<ParityFilterEntity>()
            .IgnoreFilters(["tenant"], typeof(ParityFilterSiblingEntity))
            .Select(x => x.Id)
            .ToList()
            .Should()
            .BeEquivalentTo([1]);
    }
}
