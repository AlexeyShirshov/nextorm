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
    public void Comparer_ShouldDistinguishProjectionItemSlots()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var first = Column(0, new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));
        var second = Column(0, new ProjectionEntityItem(1, typeof(PlanEqualityEntity), null));

        comparer.Equals(first, second).Should()
            .BeFalse("different projection slots rebuild the row into a different shape");
        comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));
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
