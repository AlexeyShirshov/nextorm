using System.Linq.Expressions;
namespace nextorm.core;

public class ReplaceMemberVisitor : ExpressionVisitor
{
    private readonly Type _entityType;
    //private readonly IQueryProvider _queryProvider;
    private readonly ParameterExpression _param;
    private readonly DbContext _dataProvider;

    public ReplaceMemberVisitor(Type entityType, ParameterExpression param, DbContext dataProvider)
    {
        _entityType = entityType;
        // _queryProvider = queryProvider;
        _param = param;
        _dataProvider = dataProvider;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Member.DeclaringType == _entityType)
            return _dataProvider.MapColumnExpression(new SelectExpression(node.Type) { Index = 0, PropertyName = node.Member.Name }, _param);

        return base.VisitMember(node);
    }
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(NORM.NORM_SQL))
        {
            if (node.Method.Name == nameof(NORM.NORM_SQL.exists))
            {
                return _dataProvider.MapColumnExpression(new SelectExpression(node.Type) { Index = 0 }, _param);
            }
        }
        return base.VisitMethodCall(node);
    }
}