using System.Linq.Expressions;
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

    [Fact]
    public void DistinctOneToOneNavigations_ShouldHaveDistinctPlanKeys()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<PlanOneToOneParent>(b => b
            .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
            .HasOneToOne(p => p.Secondary, p => p.Id, c => c.ParentId));
        ctx.From<PlanOneToOneChild>();

        static QueryCommand<PlanOneToOneParent> Command(InMemoryDataContext ctx, bool primary)
        {
            var cmd = ctx.From<PlanOneToOneParent>()
                .JoinInto(ctx.From<PlanOneToOneChild>(), (p, c) => p.Id == c.ParentId, primary ? p => p.Primary : p => p.Secondary)
                .ToCommand();
            cmd.PrepareCommand(CancellationToken.None);
            return cmd;
        }

        var first = Command(ctx, primary: true);
        var second = Command(ctx, primary: false);
        var repeat = Command(ctx, primary: true);
        var comparer = first.GetQueryPlanEqualityComparer();

        comparer.Equals(first, second).Should().BeFalse("different one-to-one navigations must not share a cached plan");
        comparer.Equals(first, repeat).Should().BeTrue("the same one-to-one declaration must reuse the cached plan");
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(repeat));
    }

    [Fact]
    public void DistinctManyToManyJunctionTypes_ShouldHaveDistinctPlanKeys()
    {
        using var ctx = CreateManyToManyContext();

        var first = M2MCommand(ctx, p => p.As);
        var second = M2MCommand(ctx, p => p.Bs);
        var repeat = M2MCommand(ctx, p => p.As);

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeFalse("different junctions must not share a cached plan");
        comparer.Equals(first, repeat).Should().BeTrue("the same many-to-many declaration must reuse the cached plan");
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(repeat));
    }

    [Fact]
    public void DistinctManyToManyForeignKeyMapping_ShouldHaveDistinctPlanKeys()
    {
        using var ctx = CreateManyToManyContext();

        // Cs and Ds use the same junction type and key members but map the parent side to different
        // junction foreign keys, changing the occurrence scope of the many-to-many multiplicity token.
        var first = M2MCommand(ctx, p => p.Cs);
        var second = M2MCommand(ctx, p => p.Ds);

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeFalse("different junction foreign-key mappings must not share a cached plan");
    }

    [Fact]
    public void StructurallyIdenticalManyToManyDeclarations_ShouldReuseOnePlan()
    {
        using var ctx = CreateManyToManyContext();

        // Two independent builder instances declare the same collection, junction, keys and join kind,
        // so the plan key is equal and the second declaration must reuse the cached prepared command.
        var first = M2MCommand(ctx, p => p.As);
        var second = M2MCommand(ctx, p => p.As);

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeTrue("structurally identical many-to-many declarations must reuse the cached plan");
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(second));
    }

    [Fact]
    public void DistinctManyToManyOccurrenceScope_ShouldHaveDistinctPlanKeys()
    {
        using var ctx = CreateManyToManyContext();

        // Both commands target the very same collection (Cs), so the collection member and join kind are
        // held constant; the local mapping only changes the junction parent foreign key, which fixes the
        // occurrence partition scope of the many-to-many multiplicity token. OccurrenceScope cannot vary
        // in isolation from JunctionKeyMembers (it is a pure function of the same two foreign-key names),
        // so this is the tightest isolation the public declaration API allows.
        var byParent = M2MCommandWithOccurrenceKey(ctx, l => l.ParentId);
        var byAltParent = M2MCommandWithOccurrenceKey(ctx, l => l.AltParentId);

        var comparer = byParent.GetQueryPlanEqualityComparer();
        comparer.Equals(byParent, byAltParent).Should().BeFalse(
            "a different occurrence partition scope must not share a cached plan");
    }

    [Fact]
    public void DistinctManyToManyJoinType_ShouldHaveDistinctPlanKeys()
    {
        using var ctx = CreateManyToManyContext();

        var first = M2MCommand(ctx, p => p.As, JoinType.Left);
        var second = M2MCommand(ctx, p => p.As, JoinType.Inner);

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeFalse("a many-to-many LEFT and INNER declaration must not share a cached plan");
    }

    private static InMemoryDataContext CreateManyToManyContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<PlanManyToManyParent>(b => b
            .HasManyThrough<PlanManyToManyChild, PlanManyToManyLinkA, int, int>(
                p => p.As, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .HasManyThrough<PlanManyToManyChild, PlanManyToManyLinkB, int, int>(
                p => p.Bs, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .HasManyThrough<PlanManyToManyChild, PlanManyToManyLinkAlt, int, int>(
                p => p.Cs, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .HasManyThrough<PlanManyToManyChild, PlanManyToManyLinkAlt, int, int>(
                p => p.Ds, p => p.Id, l => l.AltParentId, c => c.Id, l => l.ChildId));
        ctx.From<PlanManyToManyChild>();
        return ctx;
    }

    private static QueryCommand<PlanManyToManyParent> M2MCommand(
        InMemoryDataContext ctx,
        Expression<Func<PlanManyToManyParent, ICollection<PlanManyToManyChild>>> collection,
        JoinType joinType = JoinType.Left)
    {
        var cmd = ctx.From<PlanManyToManyParent>()
            .JoinInto(ctx.From<PlanManyToManyChild>(), (p, c) => true, collection, joinType)
            .ToCommand();
        cmd.PrepareCommand(CancellationToken.None);
        return cmd;
    }

    private static QueryCommand<PlanManyToManyParent> M2MCommandWithOccurrenceKey(
        InMemoryDataContext ctx,
        Expression<Func<PlanManyToManyLinkAlt, int>> junctionParentForeignKey)
    {
        var cmd = ctx.From<PlanManyToManyParent>()
            .JoinInto(
                ctx.From<PlanManyToManyChild>(),
                (p, c) => true,
                p => p.Cs,
                j => j.ManyToMany<PlanManyToManyParent, PlanManyToManyChild, PlanManyToManyLinkAlt, int, int>(
                    p => p.Id, junctionParentForeignKey, c => c.Id, l => l.ChildId))
            .ToCommand();
        cmd.PrepareCommand(CancellationToken.None);
        return cmd;
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

public sealed class PlanOneToOneParent
{
    public int Id { get; set; }
    public PlanOneToOneChild? Primary { get; set; }
    public PlanOneToOneChild? Secondary { get; set; }
}

public sealed class PlanOneToOneChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
}

public sealed class PlanManyToManyParent
{
    public int Id { get; set; }
    public ICollection<PlanManyToManyChild> As { get; set; } = new List<PlanManyToManyChild>();
    public ICollection<PlanManyToManyChild> Bs { get; set; } = new List<PlanManyToManyChild>();
    public ICollection<PlanManyToManyChild> Cs { get; set; } = new List<PlanManyToManyChild>();
    public ICollection<PlanManyToManyChild> Ds { get; set; } = new List<PlanManyToManyChild>();
}

public sealed class PlanManyToManyChild
{
    public int Id { get; set; }
}

public sealed class PlanManyToManyLinkA
{
    public int ParentId { get; set; }
    public int ChildId { get; set; }
}

public sealed class PlanManyToManyLinkB
{
    public int ParentId { get; set; }
    public int ChildId { get; set; }
}

public sealed class PlanManyToManyLinkAlt
{
    public int ParentId { get; set; }
    public int AltParentId { get; set; }
    public int ChildId { get; set; }
}
