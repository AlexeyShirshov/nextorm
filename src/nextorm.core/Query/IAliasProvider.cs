using System.Linq.Expressions;

namespace NextORM.Core;

public interface IAliasProvider
{
    string? FindAlias(int idx);
    string GetNextAlias(FromExpression from);
    string GetNextAlias(QueryCommand queryCommand);
}
