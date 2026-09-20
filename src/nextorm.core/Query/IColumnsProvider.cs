
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
    int? FindAlias(Type entityType, int? paramIdx, bool fromProjection);
    (int, QueryCommand?) FindQueryCommand(Type entityType);
    void PopScope();
    void PushScope(ReadOnlyCollection<ParameterExpression> parameters);

    /// <summary>
    /// Marks the source entries added from now on as belonging to the command currently being
    /// rendered, so that <see cref="FindAlias(ParameterExpression, bool)"/> of a nested command does
    /// not resolve to a same-typed source of an unrelated sibling command (or of the outer query).
    /// </summary>
    void PushSourceScope();

    /// <summary>Ends the scope opened by <see cref="PushSourceScope"/>.</summary>
    void PopSourceScope();
}
