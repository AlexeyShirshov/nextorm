using System.Linq.Expressions;
namespace nextorm.core;

public class ReplaceMemberVisitor : ExpressionVisitor
{
    private readonly Type _entityType;
    private readonly DbContext _sqlDataProvider;
    private readonly IQueryProvider _queryProvider;
    private readonly ParameterExpression _param;

    public ReplaceMemberVisitor(Type entityType, DbContext sqlDataProvider, IQueryProvider queryProvider, ParameterExpression param)
    {
        _entityType = entityType;
        _sqlDataProvider = sqlDataProvider;
        _queryProvider = queryProvider;
        _param = param;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Member.DeclaringType == _entityType)
            return _sqlDataProvider.MapColumn(new SelectExpression(node.Type, _sqlDataProvider.ExpressionsCache, _queryProvider) { Index = 0, PropertyName = node.Member.Name }, _param);

        return base.VisitMember(node);
    }
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(NORM.NORM_SQL))
        {
            if (node.Method.Name == nameof(NORM.NORM_SQL.exists))
            {
                return _sqlDataProvider.MapColumn(new SelectExpression(node.Type, _sqlDataProvider.ExpressionsCache, _queryProvider) { Index = 0 }, _param);
            }
        }
        return base.VisitMethodCall(node);
    }
}