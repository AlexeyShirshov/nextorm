using System.Linq.Expressions;

namespace nextorm.core;

public class ExpressionAliasProvider : IAliasProvider
{
    private LambdaExpression _joinCondition;

    public ExpressionAliasProvider(LambdaExpression joinCondition)
    {
        _joinCondition = joinCondition;
    }

    public string FindAlias(ParameterExpression param)
    {
        for(int i = 0;i<_joinCondition.Parameters.Count;i++)
        {
            if (_joinCondition.Parameters[i] == param)
                return "t" + (i+1).ToString();
        }
        return string.Empty;
    }
}
