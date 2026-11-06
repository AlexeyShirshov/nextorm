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
/// Narrow public contract for a query component. A component carries its own slot identity and handler;
/// there is no global registry. The prepared descriptor is immutable and participates in the plan key.
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
/// Optional pre-preparation structural identity of a query component, used by the engine's raw
/// (pre-preparation) recipe cache. The engine captures a command's value-free raw shape before it is
/// prepared and may then reuse another command's prepared plan, so a component that wants that fast
/// path must describe, without a prepare context, which of its state changes the statement it produces.
/// Two components that report an equal <see cref="ShapeState"/> and structurally equal selectors must
/// render the same statement for the same command; that is the contract this interface carries.
/// </summary>
/// <remarks>
/// A component that does not implement this interface stays eligible for every other path but is
/// excluded from the raw recipe cache: its state is invisible before preparation, and reusing a plan
/// prepared for a different component would silently change the SQL it renders.
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
/// An immutable declarative contribution to query preparation. Carried by an
/// <see cref="IQueryPreparationDescriptor"/>; the engine applies it to a separate effective
/// preparation state before <c>PrepareColumns</c>/<c>PrepareGrouping</c> and never mutates the
/// raw <see cref="QueryCommand"/> fields. An active contribution with an empty
/// <see cref="GroupingKeys"/> vector denotes an explicit global aggregation.
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

    /// <summary>Ordered scalar lambda keys of the grouping (empty = explicit global aggregation when active).</summary>
    public IReadOnlyList<LambdaExpression> GroupingKeys { get; }

    /// <summary>How the effective HAVING is handled.</summary>
    public QueryPreparationHavingMode HavingMode { get; }

    /// <summary>The replacement HAVING condition when <see cref="HavingMode"/> is <see cref="QueryPreparationHavingMode.Replace"/>.</summary>
    public LambdaExpression? Having { get; }

    /// <summary>An additive source predicate AND-ed into the effective source condition.</summary>
    public LambdaExpression? SourcePredicate { get; }

    /// <summary>Whether DISTINCT is required (monotonic OR on the effective state).</summary>
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
