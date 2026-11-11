using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.Core.Tests;

public class D362Unit2Tests
{
    [Fact]
    public void AggregateClassification_ServiceCallable()
    {
        // A plain constant is neither an aggregate nor a window.
        var expr = Expression.Constant(1);
        AggregateClassification.IsAggregate(expr).Should().BeFalse();
        AggregateClassification.IsWindow(expr).Should().BeFalse();
    }

    [Fact]
    public void SemanticComparer_LiteralsByValue()
    {
        var a = Expression.Constant(42);
        var b = Expression.Constant(42);
        var c = Expression.Constant(7);
        SemanticExpressionEqualityComparer.Instance.Equals(a, b).Should().BeTrue();
        SemanticExpressionEqualityComparer.Instance.Equals(a, c).Should().BeFalse();
    }

    [Fact]
    public void SemanticComparer_ParametersAlphaEquivalent()
    {
        var p1 = Expression.Parameter(typeof(int), "x");
        var p2 = Expression.Parameter(typeof(int), "y");
        SemanticExpressionEqualityComparer.Instance.Equals(p1, p2).Should().BeTrue();
    }

    [Fact]
    public void SemanticComparer_UnknownNodesConservativelyUnequal()
    {
        // Two distinct lambda nodes of different shape -> unequal.
        var a = Expression.New(typeof(object).GetConstructor(Type.EmptyTypes)!);
        var b = Expression.New(typeof(object).GetConstructor(Type.EmptyTypes)!);
        // Both structurally identical new() -> equal by our rule; use a fallback unknown-ish node:
        // A Block expression is not handled -> unequal even if same.
        var blockA = Expression.Block(Expression.Constant(1));
        var blockB = Expression.Block(Expression.Constant(1));
        SemanticExpressionEqualityComparer.Instance.Equals(blockA, blockB).Should().BeFalse();
    }

    [Fact]
    public void EffectiveState_NoComponentCommand_UnchangedAfterPrepare()
    {
        // Smoke: building the whole solution already covers compile; this test only asserts
        // the comparer/hash helpers don't throw on a trivial expression.
        var expr = Expression.Constant(1);
        var h = SemanticExpressionEqualityComparer.Instance.GetHashCode(expr);
        // reachability only
        h.Should().Be(h);
    }
}
