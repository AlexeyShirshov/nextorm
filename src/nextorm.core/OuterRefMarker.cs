using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Marker for a reference to an element of an outer (ancestor) query scope, used while building
/// correlated subqueries. It is only meaningful inside a query expression: the SQL translator rewrites
/// <c>OuterRefMarker&lt;T&gt;(idx).Ref.Member</c> into a reference to the outer alias's column.
/// </summary>
/// <typeparam name="T">The type of the referenced outer element.</typeparam>
/// <param name="i">The zero-based index of the outer element in the correlated query scope.</param>
public class OuterRefMarker<T>(int i)
{
    /// <summary>
    /// The zero-based index of the referenced outer element, as passed to the constructor.
    /// </summary>
    public int I { get; } = i;
    /// <summary>
    /// Sentinel member that the translator looks for to identify an outer reference. It is never
    /// assigned, so at runtime its value is always the default for <typeparamref name="T"/>.
    /// </summary>
    public T? Ref { get; }

}