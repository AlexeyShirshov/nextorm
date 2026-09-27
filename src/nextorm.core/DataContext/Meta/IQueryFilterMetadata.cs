using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A query filter declared for an entity type: the optional key and the predicate lambda that
/// nextorm applies to every query in which the entity participates.
/// </summary>
public interface IQueryFilterMetadata
{
    /// <summary>
    /// The filter key, or <see langword="null"/> for an anonymous filter. Named (keyed) filters are
    /// introduced in a later phase; an anonymous filter always reports <see langword="null"/>.
    /// </summary>
    string? Key { get; }

    /// <summary>
    /// The filter predicate. Its first parameter is the entity; an optional second parameter is the
    /// executing <see cref="IDataContext"/>, so the filter can read per-context state such as a tenant
    /// identifier.
    /// </summary>
    LambdaExpression Lambda { get; }
}
