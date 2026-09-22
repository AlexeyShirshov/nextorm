
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
    bool HasAliases { get; }

    void Add(Type entityType, bool fromProjection);
    void Add(QueryCommand queryCommand, bool fromProjection);
    int? FindAlias(ParameterExpression param, bool fromProjection);

    /// <summary>
    /// Resolves the alias of <paramref name="param"/>. When <paramref name="includeOuterScopes"/> is
    /// <c>true</c> the current source scope is ignored, so an enclosing command's source can be found
    /// (used for outer-reference markers).
    /// </summary>
    int? FindAlias(ParameterExpression param, bool fromProjection, bool includeOuterScopes);

    /// <summary>
    /// Resolves the alias of a projection occurrence. When <paramref name="includeNestedSources"/> is
    /// <c>true</c> the sources of nested commands that already finished rendering are considered too;
    /// that is what resolving a derived query's output columns needs.
    /// </summary>
    int? FindAlias(ParameterExpression param, bool fromProjection, bool includeOuterScopes, bool includeNestedSources)
        => FindAlias(param, fromProjection, includeOuterScopes);

    int? FindAlias(Type entityType, int? paramIdx, bool fromProjection);

    /// <summary>
    /// Like <see cref="FindAlias(Type, int?, bool)"/> but, when no source visible to the current
    /// command matches, falls back to a source of a nested command. See
    /// <see cref="FindAlias(ParameterExpression, bool, bool, bool)"/>.
    /// </summary>
    int? FindAlias(Type entityType, int? paramIdx, bool fromProjection, bool includeNestedSources)
        => FindAlias(entityType, paramIdx, fromProjection);

    (int, QueryCommand?) FindQueryCommand(Type entityType);

    /// <summary>
    /// Like <see cref="FindQueryCommand(Type)"/> but, when no source visible to the current command
    /// matches, falls back to a source of a nested command. See
    /// <see cref="FindAlias(ParameterExpression, bool, bool, bool)"/>.
    /// </summary>
    (int, QueryCommand?) FindQueryCommand(Type entityType, bool includeNestedSources)
        => FindQueryCommand(entityType);
    void PopScope();
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
