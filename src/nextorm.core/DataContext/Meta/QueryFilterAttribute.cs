using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Declares a global query filter on an entity type or interface. The filter is applied to every query
/// in which the entity participates (the primary source, joins and subqueries) unless the query calls
/// <c>IgnoreFilters</c>. Multiple attributes are combined with <c>and</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class QueryFilterAttribute : Attribute
{
    /// <summary>
    /// The name of a static member of the attributed type that returns the filter lambda as an
    /// <see cref="Expression{TDelegate}"/> over the entity and, optionally, the
    /// <see cref="IDataContext"/>. The member may be a field, a property or a parameterless method.
    /// </summary>
    public string? FilterLambda { get; set; }
}
