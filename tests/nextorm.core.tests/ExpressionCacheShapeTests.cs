using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Guards the shape of the shared <see cref="DataContextCache.ExpressionsCache"/>: the same
/// <see cref="ExpressionKey"/> can hold compiled delegates of several delegate shapes, and a retrieval
/// must never blind-cast an incompatible shape.
/// </summary>
public class ExpressionCacheShapeTests
{
    [Fact]
    public void IncompatibleCachedShape_ShouldNotBeInvoked()
    {
        Delegate incompatible = (Func<object?, object, object>)((_, _) => 42);

        var evaluated = BaseExpressionVisitor.TryEvaluateCachedExpression(incompatible, Expression.Constant(1), out var value);

        evaluated.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void PlainCachedShape_ShouldBeInvoked()
    {
        Delegate compatible = (Func<object>)(() => 42);

        var evaluated = BaseExpressionVisitor.TryEvaluateCachedExpression(compatible, Expression.Constant(1), out var value);

        evaluated.Should().BeTrue();
        value.Should().Be(42);
    }
}
