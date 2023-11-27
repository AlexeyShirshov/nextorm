using System.Linq.Expressions;

namespace nextorm.core;

public interface IAliasProvider
{
    string FindAlias(ParameterExpression param);
}
