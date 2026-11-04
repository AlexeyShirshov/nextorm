using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Extensions.Tests;

/// <summary>
/// A second exclusive row-source component in a slot of its own, used to prove that a foreign reshape
/// is rejected at preparation. Kept in the test assembly: it is fixture code, not part of the
/// <c>nextorm.extensions</c> public surface.
/// </summary>
internal sealed class ConflictingReshapeComponent<TEntity> : IQueryComponent
{
    public Type Slot => typeof(ConflictingReshapeComponent<TEntity>);

    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) =>
        new SimpleDescriptor(Slot, GetType(), Contribution, "Reshape", 0, (_, _) => true);

    public void BuildQuery(IQueryComponentBuildContext context) { }
}

/// <summary>
/// Minimal descriptor for the fixture components. Its hash and equality are deliberately degenerate,
/// which is fine for a fixture that never shares a plan key with another instance.
/// </summary>
internal sealed class SimpleDescriptor : IQueryComponentDescriptor
{
    private readonly Func<IQueryComponentDescriptor, IQueryComponentDescriptor, bool> _equals;

    public SimpleDescriptor(
        Type slot,
        Type componentType,
        QueryComponentContribution contribution,
        string discriminator,
        int hash,
        Func<IQueryComponentDescriptor, IQueryComponentDescriptor, bool> equals)
    {
        Slot = slot;
        ComponentType = componentType;
        Contribution = contribution;
        Discriminator = discriminator;
        StructuralHash = hash;
        _equals = equals;
    }

    public Type Slot { get; }

    public Type ComponentType { get; }

    public QueryComponentContribution Contribution { get; }

    public string Discriminator { get; }

    public int StructuralHash { get; }

    public bool StructurallyEquals(IQueryComponentDescriptor? other) => other is not null && _equals(this, other);
}

/// <summary>
/// Test-only additive component that counts its own <see cref="BuildQuery"/> calls. The engine
/// contract under test is that <c>BuildQuery</c> runs exactly once on a plan-cache miss and never on
/// a confirmed warm hit; counting inside the component is the real contract, independent of any
/// product-side instrumentation.
/// </summary>
internal sealed class CountingComponent<TEntity> : IQueryComponent
{
    private int _buildCount;

    public int BuildCount => Volatile.Read(ref _buildCount);

    public Type Slot => typeof(CountingComponent<TEntity>);

    public QueryComponentContribution Contribution => QueryComponentContribution.AdditivePredicate;

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) =>
        new CountingDescriptor(Slot, GetType(), Contribution);

    public void BuildQuery(IQueryComponentBuildContext context) => Interlocked.Increment(ref _buildCount);

    private sealed class CountingDescriptor : IQueryComponentDescriptor
    {
        public CountingDescriptor(Type slot, Type componentType, QueryComponentContribution contribution)
        {
            Slot = slot;
            ComponentType = componentType;
            Contribution = contribution;
        }

        public Type Slot { get; }

        public Type ComponentType { get; }

        public QueryComponentContribution Contribution { get; }

        public string Discriminator => "Count";

        public int StructuralHash => 0;

        public bool StructurallyEquals(IQueryComponentDescriptor? other) =>
            other is CountingDescriptor rhs
            && Slot == rhs.Slot
            && ComponentType == rhs.ComponentType
            && Contribution == rhs.Contribution;
    }
}

/// <summary>
/// Additive-predicate fixture: ANDs a predicate into the source condition through the public
/// component contract. Kept in the test assembly: it is fixture code, not part of the
/// <c>nextorm.extensions</c> public surface.
/// </summary>
internal sealed class AdditivePredicateComponent<TEntity> : IQueryComponent
{
    private readonly LambdaExpression _predicate;

    public AdditivePredicateComponent(LambdaExpression predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
    }

    public Type Slot => typeof(AdditivePredicateComponent<TEntity>);

    public QueryComponentContribution Contribution => QueryComponentContribution.AdditivePredicate;

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) =>
        new SimpleDescriptor(Slot, GetType(), Contribution, "Predicate", 0, (_, _) => true);

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var entityType = typeof(TEntity);
        var param = Expression.Parameter(entityType, "x");

        Expression? body = null;
        var existing = cmd.GetComponentPreparedCondition();
        if (existing is LambdaExpression existingLambda)
            body = new Replacer(existingLambda.Parameters[0], param).Visit(existingLambda.Body);

        var predicateBody = new Replacer(_predicate.Parameters[0], param).Visit(_predicate.Body);
        var combined = body is null ? predicateBody : Expression.AndAlso(body, predicateBody);
        cmd.SetComponentPreparedCondition(Expression.Lambda(combined, param));
    }

    private sealed class Replacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public Replacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}
