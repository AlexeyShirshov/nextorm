
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace nextorm.core;

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
