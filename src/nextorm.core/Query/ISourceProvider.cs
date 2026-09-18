
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Supplies the columns available on a query source.
/// </summary>
/// <remarks>
/// Declared in <c>ISourceProvider.cs</c>, which no longer matches the type name — prefer a matching
/// file name. See <c>API-NAMING-REVIEW.md</c> finding P2-20.
/// </remarks>
public interface IColumnsProvider
{
    bool HasAliases { get; }

    void Add(Type entityType, bool fromProjection);
    void Add(QueryCommand queryCommand, bool fromProjection);
    int? FindAlias(ParameterExpression param, bool fromProjection);
    int? FindAlias(Type entityType, int? paramIdx, bool fromProjection);
    (int, QueryCommand?) FindQueryCommand(Type entityType);
    void PopScope();
    void PushScope(ReadOnlyCollection<ParameterExpression> parameters);
}
