using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Plan-key coverage for <see cref="SelectExpressionPlanEqualityComparer"/>: two projections identical
/// except for their physical column name must compare unequal and hash apart, so the dynamic-columns
/// store can skip a mapped column by its physical name without sharing a cached plan.
/// </summary>
public class SelectExpressionPlanEqualityComparerTests
{
    [Fact]
    public void Comparer_ShouldDistinguishPhysicalColumnNames()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var first = new SelectExpression(typeof(string))
        {
            Index = 0,
            PropertyName = "Name",
            PhysicalColumnName = "name",
            ProviderType = typeof(string),
        };
        var second = new SelectExpression(typeof(string))
        {
            Index = 0,
            PropertyName = "Name",
            PhysicalColumnName = "other_name",
            ProviderType = typeof(string),
        };

        comparer.Equals(first, first).Should().BeTrue();
        comparer.Equals(first, second).Should().BeFalse();
        comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));
    }

    [Fact]
    public void Comparer_ShouldTreatEqualProjectionItemShapesAsEqual()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());

        // Distinct ProjectionEntityItem instances with the same shape (entity type + slot) must still
        // compare equal, so two separately built identical projections share a cached plan.
        var left = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));
        var right = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));

        comparer.Equals(left, right).Should().BeTrue("an identical projection shape must share a cached plan");
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    [Fact]
    public void Comparer_ShouldDistinguishEntityItemFromScalarColumn()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var withItem = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));
        var withoutItem = Column(0, null);

        comparer.Equals(withItem, withoutItem).Should()
            .BeFalse("an entity item and a scalar column are different projection shapes");
        comparer.GetHashCode(withItem).Should().NotBe(comparer.GetHashCode(withoutItem));
    }

    [Fact]
    public void Comparer_ShouldTreatEqualScalarColumnsAsEqual()
    {
        // Both scalar columns (ProjectionItem == null), otherwise equal: distinct instances must compare
        // equal and hash alike, closing the "Both scalar columns" matrix row with a two-instance control
        // instead of the reflexive self-equality only.
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var left = Column(0, null);
        var right = Column(0, null);

        left.Should().NotBeSameAs(right);
        comparer.Equals(left, right).Should().BeTrue("equal scalar columns are the same projection shape");
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    [Fact]
    public void Comparer_ShouldDistinguishProjectionItemSlots()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var first = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));
        var second = Column(0, new ProjectionEntityItem(1, typeof(PlanEqualityEntity), null));

        comparer.Equals(first, second).Should()
            .BeFalse("different projection slots rebuild the row into a different shape");
        comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));
    }

    [Fact]
    public void Comparer_ShouldDistinguishProjectionItemMembers()
    {
        // #173: two entity items with the same entity type and slot but different target members
        // rebuild the row into different shapes, so they must not share a cached plan.
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var id = typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Id))!;
        var name = typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Name))!;
        var first = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), id));
        var second = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), name));

        comparer.Equals(first, second).Should()
            .BeFalse("different projection members rebuild the row into a different shape");
        comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));
    }

    [Fact]
    public void Comparer_ShouldTreatEqualProjectionItemMembersAsEqual()
    {
        // An equal member identity (including a repeated reflection lookup of the same property) must
        // compare equal and hash alike, so a separately built identical projection reuses the plan.
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var left = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity),
            typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Id))!));
        var right = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity),
            typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Id))!));

        comparer.Equals(left, right).Should().BeTrue("equal member identities must share a cached plan");
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    [Fact]
    public void Comparer_ShouldDistinguishNullFromNonNullProjectionItemMember()
    {
        // A null member marks a constructor-parameter position; it must not alias a named member.
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var member = typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Id))!;
        var withMember = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), member));
        var withoutMember = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), null));

        comparer.Equals(withMember, withoutMember).Should().BeFalse("a named member and a ctor position differ");
        comparer.Equals(withoutMember, withMember).Should().BeFalse("the result is order independent");
        comparer.GetHashCode(withMember).Should().NotBe(comparer.GetHashCode(withoutMember));
    }

    [Fact]
    public void Comparer_ShouldNotConflateSameMemberNameOnDifferentDeclaringTypes()
    {
        // Member identity is the PropertyInfo, not the name alone: two same-named members on different
        // declaring types must stay distinct even when the entity type and slot are equal.
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var first = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity),
            typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Id))!));
        var second = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity),
            typeof(PlanEqualityOtherEntity).GetProperty(nameof(PlanEqualityOtherEntity.Id))!));

        comparer.Equals(first, second).Should().BeFalse("same-named members of different types are distinct");
        comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));
    }

    [Fact]
    public void Comparer_ShouldDistinguishProjectionItemEntityTypes()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var first = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));
        var second = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), null));

        comparer.Equals(first, second).Should()
            .BeFalse("different entity types rebuild the row into a different shape");
        comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));
    }

    [Fact]
    public void Comparer_ShouldApplyMemberIdentityToValueTypeEntityItems()
    {
        // #173 variant matrix: a value-type mapped entity follows the same member-identity rule as a
        // reference type — a different target member separates the plan, an equal member shares it.
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var id = typeof(PlanEqualityValueEntity).GetProperty(nameof(PlanEqualityValueEntity.Id))!;
        var name = typeof(PlanEqualityValueEntity).GetProperty(nameof(PlanEqualityValueEntity.Name))!;
        var first = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityValueEntity), id));
        var second = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityValueEntity), name));

        comparer.Equals(first, second).Should()
            .BeFalse("a different target member on a value-type entity rebuilds a different shape");
        comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));

        var equalMember = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityValueEntity), id));
        comparer.Equals(first, equalMember).Should()
            .BeTrue("equal member identities apply to value types like reference types");
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(equalMember));
    }

    private static SelectExpression Column(int index, ProjectionEntityItem? item)
        => new(typeof(int))
        {
            Index = index,
            PropertyName = "Id",
            ProjectionItem = item,
        };
}

public sealed class PlanEqualityEntity
{
    public int Id { get; set; }
}

public sealed class PlanEqualityMemberEntity
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

public sealed class PlanEqualityOtherEntity
{
    public int Id { get; set; }
}

public struct PlanEqualityValueEntity
{
    public int Id { get; set; }

    public string? Name { get; set; }
}
