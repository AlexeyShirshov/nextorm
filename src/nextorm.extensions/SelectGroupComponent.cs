using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Extensions;

/// <summary>
/// The packaged grouping operator: implements <c>SelectGroup</c> and <c>SelectGroupHaving</c> entirely
/// on the public Core preparation-contribution mechanism.
/// </summary>
/// <remarks>
/// The component claims the exclusive grouping slot, so a command carries at most one grouping owner.
/// <see cref="Prepare"/> infers the grouping keys from the projection, classifies the command into a
/// <see cref="SelectGroupMode"/> and returns an immutable descriptor whose structural hash and equality
/// take part in the plan key. The statement itself is rendered by the engine from the effective prepared
/// state, so <see cref="BuildQuery"/> has nothing to build.
/// </remarks>
public sealed class SelectGroupComponent<TEntity> : IQueryComponent, IQueryComponentShape
{
    private readonly LambdaExpression _projection;
    private readonly LambdaExpression? _condition; // for SelectGroupHaving
    private static readonly object NoConditionState = new();
    private static readonly object HasConditionState = new();
    private readonly object _shapeState;

    /// <summary>Creates the component for <paramref name="projection"/>, optionally with a <c>HAVING</c> condition.</summary>
    /// <param name="projection">The projection whose aggregate-free, row-dependent subexpressions become the grouping keys.</param>
    /// <param name="condition">
    /// An additional <c>HAVING</c> predicate typed on the projection result, AND-ed with any existing source
    /// <c>Having</c>; <see langword="null"/> for <c>SelectGroup</c>. Each member of the result is rebound to
    /// the projection expression that produces it during <see cref="Prepare"/>.
    /// </param>
    public SelectGroupComponent(LambdaExpression projection, LambdaExpression? condition = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        _projection = projection;
        _condition = condition;
        _shapeState = _condition is not null ? HasConditionState : NoConditionState;
    }

    /// <summary>
    /// The slot identity of the operator: <c>SelectGroup</c> and <c>SelectGroupHaving</c> share it, so a
    /// second call replaces the component in place instead of nesting.
    /// </summary>
    public Type Slot => typeof(SelectGroupComponent<TEntity>);

    /// <summary>The contribution mode: <see cref="QueryComponentContribution.ExclusiveGrouping"/> — the component owns the grouping of the command.</summary>
    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveGrouping;

    /// <summary>
    /// The scalar part of the raw-shape identity: whether the component carries a <c>HAVING</c> condition.
    /// The projection itself is already part of the command's raw shape.
    /// </summary>
    public object? ShapeState => _shapeState;

    /// <summary>One selector — the condition — when the component carries a <c>HAVING</c> condition; zero otherwise.</summary>
    // The projection is already carried by the raw command shape (Exp), so only the condition
    // (applied during Prepare, not yet in cmd.Having at raw-shape time) participates here.
    public int ShapeSelectorCount => _condition is null ? 0 : 1;

    /// <summary>Cached shape selectors: the condition lambda, or an empty array when there is no condition.</summary>
    private LambdaExpression[]? _shapeSelectors;
    /// <summary>Cached shape selectors, materialized once per component instance.</summary>
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

    /// <summary>Returns the condition selector; only index 0 is valid, and only when a condition is present.</summary>
    public LambdaExpression GetShapeSelector(int index) => index == 0 && _condition is not null
        ? _condition
        : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>
    /// Infers the grouping keys from the projection, validates it together with the effective <c>HAVING</c>
    /// and the expression ordering, and returns the immutable preparation contribution for the engine.
    /// </summary>
    /// <param name="context">The preparation context over the command being prepared.</param>
    /// <returns>An immutable descriptor carrying the grouping contribution.</returns>
    /// <exception cref="NotSupportedException">The command targets the in-memory provider.</exception>
    /// <exception cref="QueryPreparationException">The command shape cannot be grouped by inference.</exception>
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
        // The condition is typed on the projection result: rebind it onto the source entity before it
        // joins the effective HAVING, so validation and rendering see an ordinary entity predicate.
        var condition = _condition is null
            ? null
            : SelectGroupConditionBinder.Bind(projection, _condition);
        // Combine existing source Having with the contributed condition before validation.
        LambdaExpression? combinedHaving = cmd.Having;
        if (condition is not null)
        {
            combinedHaving = combinedHaving is null
                ? condition
                : CombinePredicates(combinedHaving, condition);
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
                    condition,
                    sourcePredicate: null,
                    requireDistinct: false);
                break;
            case SelectGroupMode.GlobalAggregate:
                contribution = new QueryPreparationContribution(
                    Array.Empty<LambdaExpression>(),
                    QueryPreparationHavingMode.Replace,
                    condition,
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

    /// <summary>Does nothing: the engine renders the statement from the effective prepared state.</summary>
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
        public ParameterReplacer(ParameterExpression old, ParameterExpression @new) { _old = old; _new = @new; }
        protected override Expression VisitParameter(ParameterExpression node) => node == _old ? _new : base.VisitParameter(node);
    }

    private sealed class Descriptor : IQueryPreparationDescriptor
    {
        private readonly SelectGroupComponent<TEntity> _component;
        public Descriptor(SelectGroupComponent<TEntity> component, QueryPreparationContribution prep, int hash)
        {
            _component = component;
            Preparation = prep;
            StructuralHash = hash;
        }
        public Type Slot => _component.Slot;
        public Type ComponentType => _component.GetType();
        public QueryComponentContribution Contribution => _component.Contribution;
        public string Discriminator => "SelectGroup";
        public int StructuralHash { get; }
        public QueryPreparationContribution Preparation { get; }
        public bool StructurallyEquals(IQueryComponentDescriptor? other)
            => other is Descriptor rhs && Slot == rhs.Slot && StructuralHash == rhs.StructuralHash;
    }
}

/// <summary>
/// The grouping operators of the <c>nextorm.extensions</c> package: <c>SelectGroup</c> and
/// <c>SelectGroupHaving</c>, both built on the public query-component contract.
/// </summary>
public static class SelectGroupExtensions
{
    /// <summary>
    /// Projects each group of <paramref name="source"/>, inferring the <c>GROUP BY</c> keys from
    /// <paramref name="projection"/>: every aggregate-free, row-dependent subexpression becomes a key and
    /// the aggregate calls fold each group. The projection keeps its exact shape — no client-side grouping
    /// and no rewriting of its members.
    /// </summary>
    /// <typeparam name="TEntity">The mapped source entity.</typeparam>
    /// <typeparam name="TResult">The shape of the projection.</typeparam>
    /// <param name="source">The entity builder to group.</param>
    /// <param name="projection">The same projection lambda <c>Select</c> takes.</param>
    /// <returns>A typed command for the grouped projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is <see langword="null"/>.</exception>
    /// <exception cref="QueryPreparationException">
    /// The command already carries explicit grouping, the projection contains a window function or a
    /// subquery, or a <c>Having</c>/expression <c>OrderBy</c> member is not covered by an inferred key.
    /// </exception>
    /// <exception cref="NotSupportedException">The command targets the in-memory provider.</exception>
    public static QueryCommand<TResult> SelectGroup<TEntity, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return source.WithComponent(new SelectGroupComponent<TEntity>(projection))
            .Select(projection);
    }

    /// <summary>
    /// Same inference as <see cref="SelectGroup{TEntity, TResult}"/>, with <paramref name="condition"/>
    /// AND-ed into the effective <c>HAVING</c>. The condition is typed on the projection result
    /// (<typeparamref name="TResult"/>), never on the source entity: a member of the result stands for the
    /// projection expression that produces it, so <c>f => f.Total > 5</c> becomes the same <c>HAVING</c> as
    /// the underlying <c>sum(...) &gt; 5</c>. Keys are inferred only from <paramref name="projection"/>;
    /// the condition never adds a key.
    /// </summary>
    /// <typeparam name="TEntity">The mapped source entity.</typeparam>
    /// <typeparam name="TResult">The shape of the projection.</typeparam>
    /// <param name="source">The entity builder to group.</param>
    /// <param name="projection">The same projection lambda <c>Select</c> takes.</param>
    /// <param name="condition">A <c>HAVING</c> predicate over the projection result, AND-ed with any existing source <c>Having</c>.</param>
    /// <returns>A typed command for the grouped projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> or <paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="QueryPreparationException">
    /// The command already carries explicit grouping, the projection contains a window function or a
    /// subquery, the condition references a member the projection does not bind (or the result as a
    /// whole), or a row member of the projection or of the effective <c>HAVING</c> is not covered by an
    /// inferred key.
    /// </exception>
    /// <exception cref="NotSupportedException">The command targets the in-memory provider.</exception>
    public static QueryCommand<TResult> SelectGroupHaving<TEntity, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TResult>> projection,
        Expression<Func<TResult, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(condition);
        return source.WithComponent(new SelectGroupComponent<TEntity>(projection, condition))
            .Select(projection);
    }
}
