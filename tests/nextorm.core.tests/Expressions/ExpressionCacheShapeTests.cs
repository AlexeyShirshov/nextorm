using System.Linq.Expressions;
using System.Runtime.CompilerServices;
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

    [Fact]
    public void ExpressionKey_DoesNotRootRegistryAfterHashing()
    {
        var (key, registry) = BuildKey();
        var cache = new ExpressionCache<Func<int>> { [key] = static () => 42 };

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        registry.IsAlive.Should().BeFalse("the constant in a cached expression key does not need the command registry");
        using var context = new InMemoryDataContext();
        var equivalentKey = new ExpressionKey(Expression.Constant(42), context.From<SimpleEntity>().Select(row => row.Id));
        cache[equivalentKey]().Should().Be(42);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ExpressionKey Key, WeakReference Registry) BuildKey()
    {
        using var context = new InMemoryDataContext();
        var registry = context.From<SimpleEntity>().Select(row => row.Id);
        return (new ExpressionKey(Expression.Constant(42), registry), new WeakReference(registry));
    }
}
