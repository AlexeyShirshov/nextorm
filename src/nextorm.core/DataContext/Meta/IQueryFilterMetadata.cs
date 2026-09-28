using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A query filter declared for an entity type: the optional key and the predicate lambda that
/// nextorm applies to every query in which the entity participates.
/// </summary>
public interface IQueryFilterMetadata
{
    /// <summary>
    /// The filter key. A filter declared without a key reports <see cref="QueryFilters.AnonymousKey"/>;
    /// a named filter reports the key it was registered with.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// The filter predicate. Its first parameter is the entity; an optional second parameter is the
    /// executing <see cref="IDataContext"/>, so the filter can read per-context state such as a tenant
    /// identifier. <see langword="null"/> for a builder-function filter (not implemented yet).
    /// </summary>
    LambdaExpression? Lambda { get; }

    /// <summary>
    /// The builder-function form of the filter, or <see langword="null"/> for a predicate filter. The
    /// builder-function form is not implemented yet and always reports <see langword="null"/>.
    /// </summary>
    LambdaExpression? Func => null;
}
