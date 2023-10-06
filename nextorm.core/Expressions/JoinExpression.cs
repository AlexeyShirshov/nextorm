using System.Linq.Expressions;

namespace nextorm.core;

public enum JoinType
{
    Inner = 0,
    Left = 1,
    Right = 2,
    Full = 3,
    Cross = 4,
    FullCross = 5
}

public class JoinExpression
{
    public JoinType JoinType {get;set;}
    public LambdaExpression JoinCondition{get;set;}
    public QueryCommand Query { get; init; }
    public JoinExpression(LambdaExpression joinCondition)
    {
        JoinCondition = joinCondition;
    }
}