using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// The kind of structural contribution a query component makes to a command. Additive predicates are
/// combined with the source condition; an exclusive row-source reshape replaces the row source and at
/// most one such slot may be present.
/// </summary>
public enum QueryComponentContribution
{
    /// <summary>An additive source predicate combined with AND.</summary>
    AdditivePredicate = 0,

    /// <summary>An exclusive reshape of the row source.</summary>
    ExclusiveRowSource = 1,

    /// <summary>An exclusive grouping slot: at most one owner per command; a second owner is rejected.</summary>
    ExclusiveGrouping = 2,
}

/// <summary>
/// Defines a query component's slot identity and behavior without a global registry. Preparing a
/// component returns an immutable descriptor that participates in the plan key.
/// </summary>
public interface IQueryComponent
{
    /// <summary>Stable slot identity (typically the component type).</summary>
    Type Slot { get; }

    /// <summary>The kind of contribution this component makes.</summary>
    QueryComponentContribution Contribution { get; }

    /// <summary>Prepares and validates the component against the command, returning an immutable descriptor.</summary>
    IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context);

    /// <summary>Builds the component's structural contribution into the command on a cache miss.</summary>
    void BuildQuery(IQueryComponentBuildContext context);
}

/// <summary>
/// Describes the parts of a query component's structure that affect its statement, before the component
/// is prepared. The engine can use this identity in its raw-recipe cache to reuse a prepared plan.
/// Components with equal <see cref="ShapeState"/> values and structurally equal selectors must render
/// the same statement for the same command.
/// </summary>
/// <remarks>
/// A component that does not implement this interface can use other query paths but is excluded from the
/// raw-recipe cache because its state is not available before preparation. Reusing a plan prepared for a
/// different component could otherwise change the SQL it renders.
/// </remarks>
public interface IQueryComponentShape
{
    /// <summary>
    /// The scalar part of the structural identity — direction, tie policy, flags. Compared with
    /// <see cref="object.Equals(object)"/>, so the value must implement a stable equality and hash code.
    /// </summary>
    object? ShapeState { get; }

    /// <summary>The number of selector expressions that participate in the structural identity.</summary>
    int ShapeSelectorCount { get; }

    /// <summary>
    /// The selector expressions that participate in the structural identity, as a cached array. Implementations
    /// should cache this once per component instance to avoid per-capture allocation.
    /// </summary>
    LambdaExpression[] ShapeSelectors { get; }

    /// <summary>Returns the selector expression at <paramref name="index"/>; compared with the engine expression comparer.</summary>
    /// <param name="index">Zero-based selector index, below <see cref="ShapeSelectorCount"/>.</param>
    /// <returns>The selector expression.</returns>
    LambdaExpression GetShapeSelector(int index);
}

/// <summary>
/// Immutable prepared state of a query component. It participates in the plan-cache key through
/// <see cref="StructuralHash"/> and <see cref="StructurallyEquals"/>.
/// </summary>
public interface IQueryComponentDescriptor
{
    /// <summary>The slot this component occupies.</summary>
    Type Slot { get; }

    /// <summary>The concrete component type.</summary>
    Type ComponentType { get; }

    /// <summary>The contribution kind.</summary>
    QueryComponentContribution Contribution { get; }

    /// <summary>A semantic discriminator (for example min/max).</summary>
    string Discriminator { get; }

    /// <summary>Structural hash of the full component state.</summary>
    int StructuralHash { get; }

    /// <summary>Determines structural equality with another prepared descriptor.</summary>
    bool StructurallyEquals(IQueryComponentDescriptor? other);
}

/// <summary>
/// Context supplied to <see cref="IQueryComponent.Prepare"/>. It exposes the command being prepared
/// and the engine's expression comparer.
/// </summary>
public interface IQueryComponentPrepareContext
{
    /// <summary>The command being prepared.</summary>
    QueryCommand Command { get; }

    /// <summary>The engine expression comparer.</summary>
    IEqualityComparer<Expression> ExpressionComparer { get; }
}

/// <summary>
/// Context supplied to <see cref="IQueryComponent.BuildQuery"/>. It exposes the command being built
/// and the engine's expression comparer.
/// </summary>
public interface IQueryComponentBuildContext : IQueryComponentPrepareContext
{
}

/// <summary>
/// Default implementation of the prepare/build contexts.
/// </summary>
public sealed class QueryComponentContext : IQueryComponentPrepareContext, IQueryComponentBuildContext
{
    /// <summary>Creates a context over <paramref name="command"/>.</summary>
    public QueryComponentContext(QueryCommand command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        ExpressionComparer = command.GetExpressionPlanEqualityComparer();
    }

    /// <inheritdoc />
    public QueryCommand Command { get; }

    /// <inheritdoc />
    public IEqualityComparer<Expression> ExpressionComparer { get; }
}

/// <summary>
/// Engine-side compatibility gate for query components. An exclusive row-source reshape produces the
/// same ranked-derived-source shape as the built-in <c>SelectWhereMin/Max</c> lowering, so the exact set
/// of query modifiers that lowering cannot express is rejected for every such component, independently
/// of which checks a component can express through the public surface.
/// </summary>
internal static class QueryComponentCompatibility
{
    /// <summary>
    /// Applies <see cref="ExtremeRowCompatibility.EnsureModifiersCompatible"/> when the command carries
    /// an exclusive row-source reshape component. A command with only additive components is untouched.
    /// </summary>
    /// <param name="cmd">The command being prepared or rendered.</param>
    internal static void EnsureExclusiveReshapeCompatible(QueryCommand cmd)
    {
        if (cmd.PreparedComponents is not { Count: > 0 } prepared)
            return;

        for (var i = 0; i < prepared.Count; i++)
        {
            if (prepared[i].Contribution == QueryComponentContribution.ExclusiveRowSource)
            {
                ExtremeRowCompatibility.EnsureModifiersCompatible(cmd);
                return;
            }
        }
    }
}
/// <summary>
/// The three-state handling of the effective HAVING clause by a grouping contribution.
/// </summary>
public enum QueryPreparationHavingMode
{
    /// <summary>Leave the effective HAVING unchanged.</summary>
    Unchanged = 0,

    /// <summary>Clear the effective HAVING (used by a constant projection).</summary>
    Clear = 1,

    /// <summary>Replace the effective HAVING with the contribution's condition.</summary>
    Replace = 2,
}

/// <summary>
/// An immutable set of changes to effective query preparation, carried by an
/// <see cref="IQueryPreparationDescriptor"/>. The engine applies it before <c>PrepareColumns</c> and
/// <c>PrepareGrouping</c> without changing the raw <see cref="QueryCommand"/> fields. An active
/// contribution with no <see cref="GroupingKeys"/> requests a global aggregation.
/// </summary>
public sealed class QueryPreparationContribution
{
    /// <summary>Creates an immutable contribution.</summary>
    public QueryPreparationContribution(
        IReadOnlyList<LambdaExpression> groupingKeys,
        QueryPreparationHavingMode havingMode,
        LambdaExpression? having,
        LambdaExpression? sourcePredicate,
        bool requireDistinct)
    {
        GroupingKeys = groupingKeys ?? throw new ArgumentNullException(nameof(groupingKeys));
        HavingMode = havingMode;
        Having = having;
        SourcePredicate = sourcePredicate;
        RequireDistinct = requireDistinct;
    }

    /// <summary>Ordered grouping-key expressions. An empty list requests a global aggregation when the contribution is active.</summary>
    public IReadOnlyList<LambdaExpression> GroupingKeys { get; }

    /// <summary>How the effective HAVING is handled.</summary>
    public QueryPreparationHavingMode HavingMode { get; }

    /// <summary>The replacement HAVING condition when <see cref="HavingMode"/> is <see cref="QueryPreparationHavingMode.Replace"/>.</summary>
    public LambdaExpression? Having { get; }

    /// <summary>An additional source predicate combined with the effective source condition using <c>AND</c>.</summary>
    public LambdaExpression? SourcePredicate { get; }

    /// <summary>Whether the prepared query requires <c>DISTINCT</c>.</summary>
    public bool RequireDistinct { get; }
}

/// <summary>
/// Optional prepared descriptor carrying a <see cref="QueryPreparationContribution"/>. Components
/// that do not implement this interface leave the raw preparation state untouched (legacy fallback).
/// </summary>
public interface IQueryPreparationDescriptor : IQueryComponentDescriptor
{
    /// <summary>The immutable declarative contribution to apply during preparation.</summary>
    QueryPreparationContribution Preparation { get; }
}
