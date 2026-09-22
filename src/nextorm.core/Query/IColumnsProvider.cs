
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Supplies the columns available on a query source.
/// </summary>
/// <remarks>
/// Declared in <c>ISourceProvider.cs</c>, which no longer matches the type name — prefer a matching
/// file name. See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P2-20.
/// </remarks>
public interface IColumnsProvider
{
    /// <summary>Whether any column source has been registered.</summary>
    bool HasAliases { get; }

    /// <summary>Registers an entity type as a source that contributes columns.</summary>
    /// <param name="entityType">The CLR type of the source entity.</param>
    /// <param name="fromProjection">Whether the source comes from a projection rather than a table.</param>
    void Add(Type entityType, bool fromProjection);
    /// <summary>Registers a nested command as a source that contributes its projection columns.</summary>
    /// <param name="queryCommand">The source command.</param>
    /// <param name="fromProjection">Whether the source comes from a projection rather than a table.</param>
    void Add(QueryCommand queryCommand, bool fromProjection);
    /// <summary>
    /// Resolves the alias index of <paramref name="param"/> among the sources in the current scope.
    /// Returns <c>null</c> when the parameter does not map to a registered source.
    /// </summary>
    /// <param name="param">The lambda parameter to resolve.</param>
    /// <param name="fromProjection">Whether to match projection sources.</param>
    int? FindAlias(ParameterExpression param, bool fromProjection);

    /// <summary>
    /// Resolves the alias of <paramref name="param"/>. When <paramref name="includeOuterScopes"/> is
    /// <c>true</c> the current source scope is ignored, so an enclosing command's source can be found
    /// (used for outer-reference markers).
    /// </summary>
    int? FindAlias(ParameterExpression param, bool fromProjection, bool includeOuterScopes);
    /// <summary>
    /// Resolves the alias index of a source by entity type, optionally selecting the
    /// <paramref name="paramIdx"/>-th same-typed source. Returns <c>null</c> when no matching source
    /// is in scope.
    /// </summary>
    /// <param name="entityType">The source entity type.</param>
    /// <param name="paramIdx">The zero-based index among same-typed sources, or <c>null</c> for the first.</param>
    /// <param name="fromProjection">Whether to match projection sources.</param>
    int? FindAlias(Type entityType, int? paramIdx, bool fromProjection);
    /// <summary>
    /// Returns the alias index and nested command of the source of <paramref name="entityType"/>,
    /// preferring an in-scope source and falling back to the most recently added out-of-scope one.
    /// </summary>
    /// <param name="entityType">The source entity type.</param>
    (int, QueryCommand?) FindQueryCommand(Type entityType);
    /// <summary>Pops the most recently pushed parameter scope.</summary>
    void PopScope();
    /// <summary>Pushes a parameter scope that disambiguates same-typed sources from the enclosing scope.</summary>
    /// <param name="parameters">The lambda parameters that belong to the scope.</param>
    void PushScope(ReadOnlyCollection<ParameterExpression> parameters);

    /// <summary>
    /// Marks the source entries added from now on as belonging to the command currently being
    /// rendered, so that <see cref="FindAlias(ParameterExpression, bool)"/> of a nested command does
    /// not resolve to a same-typed source of an unrelated sibling command (or of the outer query).
    /// </summary>
    void PushSourceScope();

    /// <summary>
    /// Ends the scope opened by <see cref="PushSourceScope"/>. Its entries remain addressable by
    /// index (aliases are assigned globally), but stop satisfying any enclosing command's lookups.
    /// </summary>
    void PopSourceScope();
}
