using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Extensions;

/// <summary>
/// The packaged grouping operator component: implements <c>SelectGroup</c> and
/// <c>SelectGroupHaving</c> entirely on the public Core preparation-contribution mechanism.
/// </summary>
public sealed class SelectGroupComponent<TEntity> : IQueryComponent, IQueryComponentShape
{
    private readonly LambdaExpression _projection;
    private readonly LambdaExpression? _condition; // for SelectGroupHaving
    private static readonly object NoConditionState = new();
    private static readonly object HasConditionState = new();
    private readonly object _shapeState;

    /// <summary>x.</summary>
    public SelectGroupComponent(LambdaExpression projection, LambdaExpression? condition = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        _projection = projection;
        _condition = condition;
        _shapeState = _condition is not null ? HasConditionState : NoConditionState;
    }

    /// <summary>x.</summary>
    public Type Slot => typeof(SelectGroupComponent<TEntity>);

    /// <summary>x.</summary>
    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveGrouping;

    /// <summary>x.</summary>
    public object? ShapeState => _shapeState;

    /// <summary>x.</summary>
    // The projection is already carried by the raw command shape (Exp), so only the condition
    // (applied during Prepare, not yet in cmd.Having at raw-shape time) participates here.
    public int ShapeSelectorCount => _condition is null ? 0 : 1;

    /// <summary>Cached shape selectors.</summary>
    private LambdaExpression[]? _shapeSelectors;
    /// <summary>Cached shape selectors.</summary>
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
                    arr[0] = _condition!;
                    _shapeSelectors = arr;
                }
            }
            return _shapeSelectors;
        }
    }

    /// <summary>x.</summary>
    public LambdaExpression GetShapeSelector(int index) => index == 0 && _condition is not null
        ? _condition
        : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>x.</summary>
    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Command.DataContext is InMemoryDataContext)
            throw new NotSupportedException("SelectGroup is not supported by the in-memory provider.");

        var cmd = context.Command;
        var snapshot = new QueryPreparationContext(cmd);
        // Reject explicit grouping modifiers read from the immutable snapshot.
        if (snapshot.GroupBy is not null || snapshot.GroupByWithTotals
            || snapshot.GroupingType != NextORM.Core.GroupingType.None || snapshot.GroupingSets is { Count: > 0 })
            throw new QueryPreparationException("SelectGroup cannot be combined with explicit grouping.");
        var projection = snapshot.Projection ?? _projection;
        // Combine existing source Having with the contributed condition before validation.
        LambdaExpression? combinedHaving = cmd.Having;
        if (_condition is not null)
        {
            combinedHaving = combinedHaving is null
                ? _condition
                : CombinePredicates(combinedHaving, _condition);
        }

        var (keys, outputs, mode) = SelectGroupExpressionAnalyzer.Analyze(
            projection, combinedHaving, cmd.Sorting, CancellationToken.None);

        QueryPreparationContribution contribution;
        switch (mode)
        {
            case SelectGroupMode.Keyed:
                var keyLambdas = keys.Select(k => (LambdaExpression)Expression.Lambda(k, projection.Parameters)).ToList();
                contribution = new QueryPreparationContribution(
                    keyLambdas,
                    QueryPreparationHavingMode.Replace,
                    // Replace carries only the ADDED condition: the engine ANDs the raw source Having
                    // exactly once (QueryPreparer's effective-state application). The full combined
                    // predicate is used above only for coverage validation and the mode decision.
                    _condition,
                    sourcePredicate: null,
                    requireDistinct: false);
                break;
            case SelectGroupMode.GlobalAggregate:
                contribution = new QueryPreparationContribution(
                    Array.Empty<LambdaExpression>(),
                    QueryPreparationHavingMode.Replace,
                    _condition,
                    sourcePredicate: null,
                    requireDistinct: false);
                break;
            case SelectGroupMode.ConstantProjection:
                contribution = new QueryPreparationContribution(
                    Array.Empty<LambdaExpression>(),
                    QueryPreparationHavingMode.Clear,
                    combinedHaving,
                    sourcePredicate: null,
                    requireDistinct: true);
                break;
            default:
                throw new QueryPreparationException("SelectGroup projection analysis is missing.");
        }

        var comparer = context.ExpressionComparer;
        var hash = new HashCode();
        hash.Add(Slot);
        hash.Add(GetType());
        hash.Add(_projection, comparer);
        if (_condition is not null) hash.Add(_condition, comparer);

        return new Descriptor(this, contribution, hash.ToHashCode());
    }

    /// <summary>x.</summary>
    public void BuildQuery(IQueryComponentBuildContext context)
    {
        // Engine renders from effective prepared state; nothing to do here.
    }

    private static LambdaExpression CombinePredicates(LambdaExpression left, LambdaExpression right)
    {
        var param = left.Parameters.Count > 0 ? left.Parameters[0] : right.Parameters[0];
        var leftBody = left.Parameters.Count > 0
            ? new ParameterReplacer(left.Parameters[0], param).Visit(left.Body)
            : left.Body;
        var rightBody = right.Parameters.Count > 0
            ? new ParameterReplacer(right.Parameters[0], param).Visit(right.Body)
            : right.Body;
        return Expression.Lambda(Expression.AndAlso(leftBody!, rightBody!), param);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _old;
        private readonly ParameterExpression _new;
        /// <summary>x.</summary>
        public ParameterReplacer(ParameterExpression old, ParameterExpression @new) { _old = old; _new = @new; }
        protected override Expression VisitParameter(ParameterExpression node) => node == _old ? _new : base.VisitParameter(node);
    }

    private sealed class Descriptor : IQueryPreparationDescriptor
    {
        private readonly SelectGroupComponent<TEntity> _component;
        /// <summary>x.</summary>
        public Descriptor(SelectGroupComponent<TEntity> component, QueryPreparationContribution prep, int hash)
        {
            _component = component;
            Preparation = prep;
            StructuralHash = hash;
        }
        /// <summary>x.</summary>
        public Type Slot => _component.Slot;
        /// <summary>x.</summary>
        public Type ComponentType => _component.GetType();
        /// <summary>x.</summary>
        public QueryComponentContribution Contribution => _component.Contribution;
        /// <summary>x.</summary>
        public string Discriminator => "SelectGroup";
        /// <summary>x.</summary>
        public int StructuralHash { get; }
        /// <summary>x.</summary>
        public QueryPreparationContribution Preparation { get; }
        /// <summary>x.</summary>
        public bool StructurallyEquals(IQueryComponentDescriptor? other)
            => other is Descriptor rhs && Slot == rhs.Slot && StructuralHash == rhs.StructuralHash;
    }
}

/// <summary>x.</summary>
public static class SelectGroupExtensions
{
    /// <summary>x.</summary>
    public static QueryCommand<TResult> SelectGroup<TEntity, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return source.WithComponent(new SelectGroupComponent<TEntity>(projection))
            .Select(projection);
    }

    /// <summary>x.</summary>
    public static QueryCommand<TResult> SelectGroupHaving<TEntity, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TResult>> projection,
        Expression<Func<TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(condition);
        return source.WithComponent(new SelectGroupComponent<TEntity>(projection, condition))
            .Select(projection);
    }
}
