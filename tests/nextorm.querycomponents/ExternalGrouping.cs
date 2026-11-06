using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.QueryComponents;

/// <summary>
/// A third-party grouping component built solely on the public Core API (no IVT, no reflection emit,
/// no raw SQL, no global registry). Own slot, distinct from the packaged operators.
/// </summary>
public sealed class ExternalGroupingComponent<TEntity> : IQueryComponent, IQueryComponentShape
{
    private readonly IReadOnlyList<LambdaExpression> _groupingKeys;
    private readonly LambdaExpression? _having;
    private readonly LambdaExpression? _sourcePredicate;
    private readonly object _shapeState;

    public ExternalGroupingComponent(
        IReadOnlyList<LambdaExpression> groupingKeys,
        LambdaExpression? having = null,
        LambdaExpression? sourcePredicate = null)
    {
        _groupingKeys = groupingKeys ?? throw new ArgumentNullException(nameof(groupingKeys));
        _having = having;
        _sourcePredicate = sourcePredicate;
        _shapeState = (_groupingKeys.Count, _having is not null);
    }

    public ExternalGroupingComponent(
        LambdaExpression groupingKey,
        LambdaExpression? having = null,
        LambdaExpression? sourcePredicate = null)
        : this(new[] { groupingKey }, having, sourcePredicate)
    {
        ArgumentNullException.ThrowIfNull(groupingKey);
    }

    public Type Slot => typeof(ExternalGroupingComponent<TEntity>);

    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveGrouping;

    public object? ShapeState => _shapeState;

    // Grouping keys are already carried by the raw command shape (Exp), so only the source predicate
    // (applied during Prepare, not in the raw shape) participates here.
    public int ShapeSelectorCount => _sourcePredicate is null ? 0 : 1;

    private LambdaExpression[]? _shapeSelectors;
    public LambdaExpression[] ShapeSelectors
    {
        get
        {
            if (_shapeSelectors is null)
            {
                if (ShapeSelectorCount == 0)
                {
                    _shapeSelectors = Array.Empty<LambdaExpression>();
                }
                else
                {
                    var arr = new LambdaExpression[1];
                    arr[0] = _sourcePredicate!;
                    _shapeSelectors = arr;
                }
            }
            return _shapeSelectors;
        }
    }

    public LambdaExpression GetShapeSelector(int index) => index == 0 && _sourcePredicate is not null
        ? _sourcePredicate
        : throw new ArgumentOutOfRangeException(nameof(index));

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Command.DataContext is InMemoryDataContext)
            throw new NotSupportedException("ExternalGrouping is not supported by the in-memory provider.");

        // Read the immutable snapshot (public surface only).
        var snapshot = new QueryPreparationContext(context.Command);

        var keys = new List<LambdaExpression>(_groupingKeys);
        var havingMode = _having is null
            ? QueryPreparationHavingMode.Unchanged
            : QueryPreparationHavingMode.Replace;
        var contribution = new QueryPreparationContribution(
            keys,
            havingMode,
            _having,
            _sourcePredicate,
            requireDistinct: false);

        var comparer = context.ExpressionComparer;
        var hash = new HashCode();
        hash.Add(Slot);
        hash.Add(GetType());
        foreach (var k in _groupingKeys) hash.Add(k, comparer);
        if (_having is not null) hash.Add(_having, comparer);
        if (_sourcePredicate is not null) hash.Add(_sourcePredicate, comparer);

        return new Descriptor(this, contribution, hash.ToHashCode(), comparer, _groupingKeys.Count > 0 ? _groupingKeys[0] : null!, _having, _sourcePredicate);
    }

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        // The engine renders GROUP BY/HAVING from the prepared effective state; nothing to do here.
    }

    private sealed class Descriptor : IQueryPreparationDescriptor
    {
        private readonly ExternalGroupingComponent<TEntity> _component;
        public Descriptor(ExternalGroupingComponent<TEntity> component, QueryPreparationContribution prep, int hash,
            IEqualityComparer<Expression> comparer, LambdaExpression key, LambdaExpression? having, LambdaExpression? sourcePredicate)
        {
            _component = component;
            Preparation = prep;
            StructuralHash = hash;
        }
        public Type Slot => _component.Slot;
        public Type ComponentType => _component.GetType();
        public QueryComponentContribution Contribution => _component.Contribution;
        public string Discriminator => "ExternalGrouping";
        public int StructuralHash { get; }
        public QueryPreparationContribution Preparation { get; }
        public bool StructurallyEquals(IQueryComponentDescriptor? other)
            => other is Descriptor rhs && Slot == rhs.Slot && StructuralHash == rhs.StructuralHash;
    }
}

public static class ExternalGroupingExtensions
{
    public static QueryCommand<TResult> ExternalGroup<TEntity, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TResult>> projection,
        Expression<Func<TEntity, object>> groupingKey,
        Expression<Func<TEntity, bool>>? having = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(groupingKey);
        return source.WithComponent(new ExternalGroupingComponent<TEntity>(groupingKey, having))
            .Select(projection);
    }
}
