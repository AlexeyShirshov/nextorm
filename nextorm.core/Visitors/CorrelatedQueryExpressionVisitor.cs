using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
namespace nextorm.core;

public class CorrelatedQueryExpressionVisitor : ExpressionVisitor
{
    private readonly CancellationToken _cancellationToken;
    private readonly IDataProvider _dataProvider;
    private readonly List<QueryCommand>? _refs;
    private static MethodInfo AnyMIGeneric = typeof(CorrelatedQueryExpressionVisitor).GetMethod(nameof(Any), BindingFlags.NonPublic | BindingFlags.Instance)!;
    public CorrelatedQueryExpressionVisitor(IDataProvider dataProvider, CancellationToken cancellationToken)
    {
        _dataProvider = dataProvider;
        _cancellationToken = cancellationToken;
        _refs = new();
    }

    public CorrelatedQueryExpressionVisitor(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public List<QueryCommand>? ReferencedQueries => _refs;

    // protected override Expression VisitConstant(ConstantExpression node)
    // {
    //     if (node.Value is QueryCommand cmd)
    //     {
    //         if (_dataProvider is not null)
    //         {

    //             // var key = new ExpressionKey(lambda);

    //             // if (!_dataProvider)


    //         }
    //         return base.VisitConstant(node);
    //     }
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(NORM.NORM_SQL))
        {
            if (node.Method.Name == nameof(NORM.NORM_SQL.exists)
                && node.Arguments is [Expression exp] && exp.Type.IsAssignableTo(typeof(QueryCommand)))
            {
                QueryCommand cmd;
                var keyCmd = new ExpressionKey(exp);
                if (!_dataProvider.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                {
                    if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Exists expression miss");

                    var d = Expression.Lambda<Func<QueryCommand>>(exp).Compile();
                    _dataProvider.ExpressionsCache[keyCmd] = d;
                    cmd = d();
                }
                else
                    cmd = ((Func<QueryCommand>)dCmd)();

                if (_refs is null)
                {
                    var asEnumMI = AnyMIGeneric.MakeGenericMethod(cmd.EntityType);
                    var dp = Expression.Constant(this);
                    var p1 = Expression.Parameter(typeof(QueryCommand));
                    var body = Expression.Call(dp, asEnumMI, exp);
                    var lambda = Expression.Lambda<Func<QueryCommand, bool>>(body, p1);

                    Func<QueryCommand, bool> del;
                    var key = new ExpressionKey(lambda);
                    if (!_dataProvider.ExpressionsCache.TryGetValue(key, out var dLambda))
                    {
                        var d = lambda.Compile();
                        _dataProvider.ExpressionsCache[key] = d;
                        del = d;
                    }
                    else
                        del = (Func<QueryCommand, bool>)dLambda;

                    return Expression.Constant(del(cmd));
                }
                else
                {
                    if (!cmd.IsPrepared)
                        cmd.PrepareCommand(_cancellationToken);

                    _refs.Add(cmd);
                }
            }
        }
        return base.VisitMethodCall(node);
    }
    // protected override Expression VisitMember(MemberExpression node)
    // {
    //     if (node.Expression.Type.IsAssignableTo(typeof(QueryCommand)))
    //     {

    //     }
    //     return base.VisitMember(node);
    // }
    bool Any<TResult>(QueryCommand cmd)
    {
        var ee = (IEnumerable<TResult>)_dataProvider.CreateEnumerator((QueryCommand<TResult>)cmd, null);
        return ee.Any();
    }
}