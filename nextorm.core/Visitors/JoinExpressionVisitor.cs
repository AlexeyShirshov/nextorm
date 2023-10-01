using System.Linq.Expressions;

namespace nextorm.core;

public class JoinExpressionVisitor : WhereExpressionVisitor
{
    private Type _joinType;
    private Expression _joinCondition;
    public Type JoinType => _joinType;
    public Expression JoinCondition => _joinCondition;
    public JoinExpressionVisitor(Type entityType, SqlDataProvider dataProvider, FromExpression from, int dim) : base(entityType, dataProvider, from, dim)
    {
    }
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _joinCondition = node.Body;
        _joinType = node.Parameters[1].Type;
        return node;
    }
}