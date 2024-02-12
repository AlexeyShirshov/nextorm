using System.Linq.Expressions;

namespace nextorm.core;

public interface IAliasProvider
{
    string? FindAlias(int idx);
    string GetNextAlias(FromExpression from);
    string GetNextAlias(QueryCommand queryCommand);
}
