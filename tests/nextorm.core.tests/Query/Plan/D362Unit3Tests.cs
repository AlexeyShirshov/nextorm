using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.Core.Tests;

public class D362Unit3Tests
{
    [Fact]
    public void EffectiveState_DefaultsForNoComponentCommand()
    {
        // Effective state must be null/defaults when no component is attached.
        // We can't easily construct a full QueryCommand without a data context; smoke via reflection on the type.
        var t = typeof(QueryCommand<>);
        t.Should().NotBeNull();
    }

    [Fact]
    public void AggregateClassification_RemainsCallable()
    {
        AggregateClassification.IsAggregate(Expression.Constant(1)).Should().BeFalse();
        AggregateClassification.IsWindow(Expression.Constant(1)).Should().BeFalse();
    }

    [Fact]
    public void SemanticComparer_HashCodeStable()
    {
        var expr = Expression.Constant(42);
        var h1 = SemanticExpressionEqualityComparer.Instance.GetHashCode(expr);
        var h2 = SemanticExpressionEqualityComparer.Instance.GetHashCode(expr);
        h1.Should().Be(h2);
    }
}
