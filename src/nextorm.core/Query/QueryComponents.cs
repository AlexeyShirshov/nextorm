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