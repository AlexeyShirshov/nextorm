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

    [Fact]
    public void InValuesAccessor_ReusesShapeWithoutRetainingTheFirstOwner()
    {
        using var context = new InMemoryDataContext();
        var registry = context.From<SimpleEntity>().Select(row => row.Id);
        var originalAccessors = DataContextCache.InValuesCache.Values.ToArray();
        var firstOwner = WarmCollectionAccessor(registry);
        var cachedAccessor = DataContextCache.InValuesCache.Values.Except(originalAccessors).Should().ContainSingle().Which;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        firstOwner.IsAlive.Should().BeFalse("the cached accessor and its key need only the shape");
        var currentOwner = new CollectionOwner([99]);
        var read = Expression.Property(Expression.Constant(currentOwner), nameof(CollectionOwner.Values));
        InValuesEvaluator.Evaluate(read, registry).Should().BeSameAs(currentOwner.Values);
        DataContextCache.InValuesCache.Values.Except(originalAccessors).Should().ContainSingle().Which.Should().BeSameAs(cachedAccessor);
    }

    [Fact]
    public void InValuesAccessor_UsesTheExactMemberWhenAPropertyIsHidden()
    {
        using var context = new InMemoryDataContext();
        var registry = context.From<SimpleEntity>().Select(row => row.Id);
        var owner = Expression.Constant(new DerivedCollection());
        var baseMember = Expression.Property(owner, typeof(BaseCollection).GetProperty(nameof(BaseCollection.Values))!);
        var derivedMember = Expression.Property(owner, typeof(DerivedCollection).GetProperty(nameof(DerivedCollection.Values))!);

        ((int[])InValuesEvaluator.Evaluate(baseMember, registry)!).Should().Equal(41);
        ((int[])InValuesEvaluator.Evaluate(derivedMember, registry)!).Should().Equal(99);
    }

    [Fact]
    public void InValuesAccessor_UsesCurrentConstantsInTheirOriginalOrder()
    {
        using var context = new InMemoryDataContext();
        var registry = context.From<SimpleEntity>().Select(row => row.Id);
        var originalAccessors = DataContextCache.InValuesCache.Values.ToArray();

        Read(41, 42).Should().Equal(41, 42);
        var accessor = DataContextCache.InValuesCache.Values.Except(originalAccessors).Should().ContainSingle().Which;
        Read(99, 98).Should().Equal(99, 98);
        DataContextCache.InValuesCache.Values.Except(originalAccessors).Should().ContainSingle().Which.Should().BeSameAs(accessor);

        int[] Read(int first, int second)
        {
            var array = Expression.NewArrayInit(typeof(int), Expression.Constant(first), Expression.Constant(second));
            return (int[])InValuesEvaluator.Evaluate(array, registry)!;
        }
    }

    [Fact]
    public void InValuesAccessor_EvaluatesUnsupportedScopesWithoutPublishingThem()
    {
        using var context = new InMemoryDataContext();
        var registry = context.From<SimpleEntity>().Select(row => row.Id);
        var originalCount = DataContextCache.InValuesCache.Count;
        int[] values = [1, 2, 3];
        var threshold = 1;
        Expression<Func<int[]>> expression = () => values.Where(value => value > threshold).ToArray();

        ((int[])InValuesEvaluator.Evaluate(expression.Body, registry)!).Should().Equal(2, 3);
        threshold = 2;
        ((int[])InValuesEvaluator.Evaluate(expression.Body, registry)!).Should().Equal(3);
        DataContextCache.InValuesCache.Count.Should().Be(originalCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WarmCollectionAccessor(IQueryRegistry registry)
    {
        var owner = new CollectionOwner([41]);
        var read = Expression.Property(Expression.Constant(owner), nameof(CollectionOwner.Values));
        InValuesEvaluator.Evaluate(read, registry).Should().BeSameAs(owner.Values);
        return new WeakReference(owner);
    }

    private sealed record CollectionOwner(int[] Values);

    private class BaseCollection
    {
        public int[] Values => [41];
    }

    private sealed class DerivedCollection : BaseCollection
    {
        public new int[] Values => [99];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ExpressionKey Key, WeakReference Registry) BuildKey()
    {
        using var context = new InMemoryDataContext();
        var registry = context.From<SimpleEntity>().Select(row => row.Id);
        return (new ExpressionKey(Expression.Constant(42), registry), new WeakReference(registry));
    }
}
