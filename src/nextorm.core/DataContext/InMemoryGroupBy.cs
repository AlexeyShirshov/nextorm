using System.Collections;
using System.Globalization;
using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Rewrites the body of a grouped projection or <c>HAVING</c> predicate by replacing every
/// <c>NORM.SQL</c> aggregate call with a constant computed over the current group. Group-key
/// members are left alone: they are constant within a group, so the representative row evaluates
/// them correctly.
/// </summary>
internal sealed class InMemoryGroupAggregateVisitor<TEntity> : ExpressionVisitor
{
    private readonly List<TEntity> _group;
    private readonly ParameterExpression _entity;

    public InMemoryGroupAggregateVisitor(List<TEntity> group, ParameterExpression entity)
    {
        _group = group;
        _entity = entity;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(NORM.NORM_SQL) || !InMemoryAggregates.IsAggregate(node.Method.Name))
            return base.VisitMethodCall(node);

        if (node.Arguments.Count > 0 && node.Arguments[^1] is LambdaExpression { Parameters.Count: 0 })
            throw new NotSupportedException("Filtered aggregates are not supported by the in-memory provider.");

        Expression? selectorBody = node.Arguments.Count switch
        {
            0 => null,
            _ => node.Arguments[0] switch
            {
                // count() is emitted as a single empty-array argument for the params array.
                NewArrayExpression { Expressions.Count: 0 } => null,
                NewArrayExpression { Expressions.Count: 1 } array => array.Expressions[0],
                NewArrayExpression => throw new NotSupportedException($"Aggregate '{node.Method.Name}' over multiple properties is not supported by the in-memory provider."),
                var arg => arg,
            },
        };

        var valueType = selectorBody?.Type ?? typeof(object);
        Delegate? selector = null;
        if (selectorBody is not null)
            selector = InMemoryAggregates.CompileSelector<TEntity>(selectorBody, _entity, valueType);

        var boxed = InMemoryAggregates.Compute(_group, node.Method.Name, selector, typeof(TEntity), node.Type, valueType);
        if (boxed is null)
            return Expression.Default(node.Type);

        var targetType = Nullable.GetUnderlyingType(node.Type) ?? node.Type;
        var value = Convert.ChangeType(boxed, targetType, CultureInfo.InvariantCulture);
        Expression constant = Expression.Constant(value, targetType);
        if (targetType != node.Type)
            constant = Expression.Convert(constant, node.Type);

        return constant;
    }
}

/// <summary>
/// Buffered enumerator over an already-materialised result set (grouped queries produce one row per
/// group, so the whole set exists before enumeration starts).
/// </summary>
internal sealed class InMemoryListEnumerator<T> : IAsyncEnumerator<T>, IEnumerator<T>, IEnumerable<T>
{
    private readonly IReadOnlyList<T> _items;
    private readonly HashSet<T>? _seen;
    private int _index = -1;

    public InMemoryListEnumerator(IReadOnlyList<T> items, bool distinct)
    {
        _items = items;
        if (distinct)
            _seen = new HashSet<T>(InMemoryDistinct.GetComparer<T>());
    }

    public T Current => _items[_index];
    object? IEnumerator.Current => Current;

    public bool MoveNext()
    {
        while (++_index < _items.Count)
        {
            if (_seen is null || _seen.Add(_items[_index]))
                return true;
        }

        return false;
    }

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(MoveNext());

    public void Reset() => _index = -1;

    public void Dispose() => GC.SuppressFinalize(this);

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public IEnumerator<T> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;
}
