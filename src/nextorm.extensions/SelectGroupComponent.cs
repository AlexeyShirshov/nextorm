using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Extensions;

/// <summary>
/// The component used by <c>SelectGroup</c> and <c>SelectGroupHaving</c> to infer grouping keys from a
/// projection and add the resulting grouping state during query preparation.
/// </summary>
/// <remarks>
/// It owns the command's exclusive grouping slot. <see cref="Prepare"/> infers the keys, resolves the
/// <see cref="SelectGroupMode"/>, and returns a descriptor used in the plan key. The engine renders the
/// statement from the prepared state, so <see cref="BuildQuery"/> does not modify the command.
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
    /// The component's scalar shape state: whether it carries a <c>HAVING</c> condition. The projection is
    /// already part of the command's raw shape.
    /// </summary>
    public object? ShapeState => _shapeState;

    /// <summary>One selector — the condition — when the component carries a <c>HAVING</c> condition; zero otherwise.</summary>
    // The projection is already carried by the raw command shape (Exp), so only the condition
    // (applied during Prepare, not yet in cmd.Having at raw-shape time) participates here.
    public int ShapeSelectorCount => _condition is null ? 0 : 1;

    /// <summary>Cached shape selectors: the condition lambda, or an empty array when there is no condition.</summary>
    private LambdaExpression[]? _shapeSelectors;
    /// <summary>The cached condition selector, or an empty array when there is no condition.</summary>
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
    /// Infers grouping keys from the projection and validates the projection, effective <c>HAVING</c>, and
    /// expression ordering against the supported grouping rules. It returns the immutable preparation
    /// descriptor for the engine.
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
            : SelectGroupConditionBinder.Bind<TEntity>(projection, _condition);
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
                // The key's scalar type is a run-time value, so the non-generic Expression.Lambda overload
                // would close Expression<Func<TEntity, TKey>> at run time — a generic instantiation the
                // NativeAOT compiler never sees, which throws "missing native code or metadata". The
                // delegate type here is closed by the component's own compile-time TEntity instead: the
                // key body is boxed to object (a convert the SQL renderer unwraps), so no run-time
                // generic type is ever created. See docs/specs/design/D370-stream-a-class3-selectgroup.md.
                var keyLambdas = keys
                    .Select(k => (LambdaExpression)Expression.Lambda<Func<TEntity, object?>>(
                        Expression.Convert(k, typeof(object)), projection.Parameters))
                    .ToList();
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
        // Both sides are entity-typed predicates; close the delegate type with the component's
        // compile-time TEntity so no run-time Expression<Func<TEntity, bool>> is created.
        return Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(leftBody!, rightBody!), param);
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
/// Adds query operations that group rows by expressions in a projection and optionally filter groups
/// with a <c>HAVING</c> predicate.
/// </summary>
public static class SelectGroupExtensions
{
    /// <summary>
    /// Groups database rows by the non-aggregate expressions in <paramref name="projection"/> and returns
    /// each group's projected result. Grouping runs in the database, and the projection keeps its declared
    /// shape.
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
    /// Groups rows by the non-aggregate expressions in <paramref name="projection"/> and filters groups
    /// with <paramref name="condition"/>. The condition is evaluated as a <c>HAVING</c> predicate over the
    /// projected result and is combined with any existing <c>HAVING</c> predicate.
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
