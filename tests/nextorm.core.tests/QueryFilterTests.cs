using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Focal coverage for global query filters (#67) on the in-memory provider: the fluent and attribute
/// declarations, the per-context predicate reading <see cref="IDataContext.Properties"/>, joins,
/// <c>IgnoreFilters</c>, the no-filter fast path and the plan-key shape shared between two contexts.
/// </summary>
public class QueryFilterTests
{
    private const string TenantKey = "tenant";

    public sealed class TenantEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class FluentFilteredEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    [QueryFilter(FilterLambda = nameof(ActiveOnly))]
    public sealed class AttributedEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }

        public static Expression<Func<AttributedEntity, bool>> ActiveOnly => e => !e.IsDeleted;
    }

    public sealed class JoinLeftEntity
    {
        public int Id { get; set; }
    }

    public sealed class JoinRightEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class IgnoredFilterEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class PlainEntity
    {
        public int Id { get; set; }
    }

    public sealed class FirstWinsEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    private static Expression<Func<TenantEntity, IDataContext, bool>> TenantFilter()
        => (e, ctx) => e.TenantId == (int)ctx.Properties[TenantKey];

    private static InMemoryDataContext TenantContext(int tenant, params TenantEntity[] rows)
    {
        var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = tenant;
        ctx.From<TenantEntity>(b => b.HasQueryFilter(TenantFilter())).WithData(rows);
        return ctx;
    }

    /// <summary>
    /// The discriminating test: two contexts hold different tenant values, run the same query shape
    /// (so the plan key is shared) whose filter reads the per-context value. Each context must return
    /// only its own tenant's rows, and the two commands must describe the same plan.
    /// </summary>
    [Fact]
    public void TwoContexts_DifferentTenant_ShouldReturnOwnRows_AndSharePlan()
    {
        using var ctx1 = TenantContext(1,
            new TenantEntity { Id = 10, TenantId = 1 },
            new TenantEntity { Id = 11, TenantId = 1 },
            new TenantEntity { Id = 20, TenantId = 2 });

        using var ctx2 = TenantContext(2,
            new TenantEntity { Id = 10, TenantId = 1 },
            new TenantEntity { Id = 20, TenantId = 2 });

        QueryCommand<int> cmd1 = ctx1.From<TenantEntity>().Select(x => x.Id);
        QueryCommand<int> cmd2 = ctx2.From<TenantEntity>().Select(x => x.Id);

        cmd1.PrepareCommand(false, TestContext.Current.CancellationToken);
        cmd2.PrepareCommand(false, TestContext.Current.CancellationToken);

        var rows1 = ctx1.From<TenantEntity>().Select(x => x.Id).ToList();
        var rows2 = ctx2.From<TenantEntity>().Select(x => x.Id).ToList();

        rows1.Should().BeEquivalentTo([10, 11]);
        rows2.Should().BeEquivalentTo([20]);

        cmd1.GetOrCreatePlanKey(null).Equals(cmd2.GetOrCreatePlanKey(null))
            .Should().BeTrue("both contexts run the same query shape and must share one cached plan");
    }

    [Fact]
    public void FluentOneArgFilter_ShouldApplyWithoutExplicitWhere()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FluentFilteredEntity>(b => b.HasQueryFilter(e => !e.IsDeleted)).WithData(
        [
            new FluentFilteredEntity { Id = 1 },
            new FluentFilteredEntity { Id = 2, IsDeleted = true },
        ]);

        var rows = ctx.From<FluentFilteredEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(1);
    }

    [Fact]
    public void AttributeFilter_ShouldApply()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<AttributedEntity>().WithData(
        [
            new AttributedEntity { Id = 1 },
            new AttributedEntity { Id = 2, IsDeleted = true },
        ]);

        var rows = ctx.From<AttributedEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(1);
    }

    [Fact]
    public void Filter_ShouldApplyToJoinedEntity()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<JoinRightEntity>(b => b.HasQueryFilter((e, c) => e.TenantId == (int)c.Properties[TenantKey]))
            .WithData(
            [
                new JoinRightEntity { Id = 1, TenantId = 1 },
                new JoinRightEntity { Id = 2, TenantId = 2 },
            ]);
        ctx.From<JoinLeftEntity>().WithData([new JoinLeftEntity { Id = 1 }, new JoinLeftEntity { Id = 2 }]);

        var rows = ctx.From<JoinLeftEntity>()
            .Join(ctx.From<JoinRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => new { LeftId = p.Item1.Id, RightId = p.Item2.Id })
            .ToList();

        rows.Should().HaveCount(1);
        rows[0].LeftId.Should().Be(1);
    }

    [Fact]
    public void IgnoreFilters_ShouldDisableFilter()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<IgnoredFilterEntity>(b => b.HasQueryFilter(e => !e.IsDeleted)).WithData(
        [
            new IgnoredFilterEntity { Id = 1 },
            new IgnoredFilterEntity { Id = 2, IsDeleted = true },
        ]);

        var filtered = ctx.From<IgnoredFilterEntity>().Select(x => x.Id).ToList();
        var unfiltered = ctx.From<IgnoredFilterEntity>().IgnoreFilters().Select(x => x.Id).ToList();

        filtered.Should().Equal(1);
        unfiltered.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void EntityWithoutFilter_ShouldBeUnchanged()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<PlainEntity>().WithData(
        [
            new PlainEntity { Id = 1 },
            new PlainEntity { Id = 2 },
        ]);

        var rows = ctx.From<PlainEntity>().Select(x => x.Id).ToList();

        rows.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void SecondRegistration_ShouldBeIgnored_FirstFilterWins()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FirstWinsEntity>(b => b.HasQueryFilter(e => !e.IsDeleted)).WithData(
        [
            new FirstWinsEntity { Id = 1 },
            new FirstWinsEntity { Id = 2, IsDeleted = true },
        ]);

        ctx.From<FirstWinsEntity>(b => b.HasQueryFilter(e => e.Id == 999));

        var rows = ctx.From<FirstWinsEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(1);
    }
}
