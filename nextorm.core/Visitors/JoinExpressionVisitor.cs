using System.Linq.Expressions;

namespace nextorm.core;

public class JoinExpressionVisitor : BaseExpressionVisitor
{
    private Type _joinType;
    private Expression _joinCondition;
    public Type JoinType => _joinType;
    public Expression JoinCondition => _joinCondition;
    public JoinExpressionVisitor(Type entityType, SqlDataProvider dataProvider, FromExpression from) : base(entityType, dataProvider, from)
    {
    }
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        return base.VisitLambda(node);
    }
}