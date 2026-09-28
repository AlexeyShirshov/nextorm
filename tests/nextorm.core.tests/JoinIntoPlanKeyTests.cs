using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Slice-B plan-cache identity tests for <c>JoinInto</c> (#105): the declaration identity (child type,
/// keys, collection member, join kind) is part of the join's plan key, so distinct declarations never
/// collide while a repeated declaration hits the same plan.
/// </summary>
public class JoinIntoPlanKeyTests
{
    public JoinIntoPlanKeyTests()
    {
        DataContextCache.Clear();
    }

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<PlanParent>(b => b
            .HasMany(p => p.As, c => c.ParentId)
            .HasMany(p => p.Bs, c => c.ParentId));
        ctx.From<PlanChild>();
        return ctx;
    }

    private static QueryCommand<PlanParent> Command(InMemoryDataContext ctx, bool useAs)
    {
        var builder = ctx.From<PlanParent>()
            .JoinInto(ctx.From<PlanChild>(), (p, c) => p.Id == c.ParentId, useAs ? p => p.As : p => p.Bs);
        var cmd = builder.ToCommand();
        cmd.PrepareCommand(CancellationToken.None);
        return cmd;
    }

    [Fact]
    public void DistinctCollections_ShouldHaveDistinctPlanKeys()
    {
        using var ctx = CreateContext();

        var first = Command(ctx, useAs: true);
        var second = Command(ctx, useAs: false);
        var repeat = Command(ctx, useAs: true);

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeFalse("different collections must not share a cached plan");
        comparer.Equals(repeat, second).Should().BeFalse("a distinct declaration must not equal the repeated one either");
        comparer.Equals(first, repeat).Should().BeTrue("the same declaration must compare equal");
    }

    [Fact]
    public void RepeatedDeclaration_ShouldHitTheSamePlan()
    {
        using var ctx = CreateContext();

        var first = Command(ctx, useAs: true);
        var second = Command(ctx, useAs: true);

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeTrue("a repeated declaration must reuse the cached plan");
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(second));
    }
}

public sealed class PlanParent
{
    public int Id { get; set; }
    public ICollection<PlanChild> As { get; set; } = new List<PlanChild>();
    public ICollection<PlanChild> Bs { get; set; } = new List<PlanChild>();
}

public sealed class PlanChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
}
