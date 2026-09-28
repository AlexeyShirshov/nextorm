namespace NextORM.Core;

/// <summary>
/// Declares a global query filter on an entity type or interface. The filter is applied to every query
/// in which the entity participates (the primary source, joins and subqueries) unless the query calls
/// <c>IgnoreFilters</c>. Multiple attributes are combined with <c>and</c>.
/// </summary>
/// <remarks>
/// A same-key attribute on a derived type replaces the one inherited from a base type (derived
/// overrides base). Attributes are applied from the base type down to the most derived one. Two
/// attributes that share a non-empty <see cref="FilterKey"/> on the <b>same</b> type are rejected when
/// the metadata is built: the runtime does not guarantee attribute declaration order, so a repeated key
/// on one type has no deterministic winner. Anonymous attributes (no key, or a whitespace key) are
/// additive and may be repeated.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class QueryFilterAttribute : Attribute
{
    /// <summary>
    /// The optional filter key. When omitted, empty or whitespace the filter is anonymous
    /// (<see cref="QueryFilters.AnonymousKey"/>); a key names the filter so it can be targeted by the
    /// key-based <c>IgnoreFilters</c>. Repeating a key replaces the earlier filter; a null
    /// <see cref="FilterLambda"/> on a keyed attribute removes it.
    /// </summary>
    public string? FilterKey { get; set; }

    /// <summary>
    /// The name of a static member of the attributed type that returns the filter lambda as an
    /// <see cref="System.Linq.Expressions.Expression{TDelegate}"/> over the entity and, optionally, the
    /// <see cref="IDataContext"/>. The member may be a field, a property or a parameterless method.
    /// </summary>
    public string? FilterLambda { get; set; }
}
