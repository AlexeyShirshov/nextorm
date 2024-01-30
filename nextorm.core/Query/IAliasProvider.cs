using System.Linq.Expressions;

namespace nextorm.core;

public interface IAliasProvider
{
    string? FindAlias(ISourceProvider sourceProvider);
    string GetNextAlias(FromExpression from);
    string GetNextAlias(QueryCommand queryCommand);
}
