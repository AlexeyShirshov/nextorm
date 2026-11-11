using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// One <c>ORDER BY</c> key of a named window definition: the key expression together with its
/// direction. Instances are produced by <c>EntityBuilder&lt;TEntity&gt;.Asc</c>/<c>Desc</c>.
/// </summary>
public sealed class NamedWindowOrderKey
{
    internal NamedWindowOrderKey(LambdaExpression expression, OrderDirection direction)
    {
        Expression = expression;
        Direction = direction;
    }

    /// <summary>The key expression (a lambda over the query's entity parameter).</summary>
    public LambdaExpression Expression { get; }

    /// <summary>Ascending or descending.</summary>
    public OrderDirection Direction { get; }
}

/// <summary>
/// A named window declared on a query with <c>EntityBuilder&lt;TEntity&gt;.Window(...)</c> and rendered
/// as part of the query's <c>WINDOW</c> clause (<c>w AS (PARTITION BY ... ORDER BY ... frame)</c>).
/// A window function references it through <see cref="WindowFunction{T}.Over(string)"/>.
/// </summary>
public sealed class WindowDefinition
{
    internal WindowDefinition(string name, IReadOnlyList<LambdaExpression> partitionBy, IReadOnlyList<NamedWindowOrderKey> orderBy, WindowFrame? frame)
    {
        Name = name;
        PartitionBy = partitionBy;
        OrderBy = orderBy;
        Frame = frame;
    }

    /// <summary>The window name as written in the <c>WINDOW</c> clause and after <c>OVER</c>.</summary>
    public string Name { get; }

    /// <summary>The <c>PARTITION BY</c> key expressions; empty when the window has no partition.</summary>
    public IReadOnlyList<LambdaExpression> PartitionBy { get; }

    /// <summary>The <c>ORDER BY</c> keys; empty when the window has no ordering.</summary>
    public IReadOnlyList<NamedWindowOrderKey> OrderBy { get; }

    /// <summary>The optional frame, or <c>null</c> when the window has none.</summary>
    public WindowFrame? Frame { get; }
}
