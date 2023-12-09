using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
namespace nextorm.core;

public class CorrelatedQueryExpressionVisitor : ExpressionVisitor
{
    private readonly CancellationToken _cancellationToken;
    private readonly bool _forPrepare = false;
    private readonly IDataContext _dataProvider;
    private readonly IQueryProvider _queryProvider;
    private readonly Type? _entityType;

    //private readonly List<QueryCommand>? _refs;
    private static MethodInfo AnyMIGeneric = typeof(CorrelatedQueryExpressionVisitor).GetMethod(nameof(Any), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static MethodInfo ToCommandMI = typeof(Entity<>).GetMethod("ToCommand", BindingFlags.Public | BindingFlags.Instance)!;
    private static MethodInfo ExistsMI = typeof(NORM.NORM_SQL).GetMethod(nameof(NORM.NORM_SQL.exists), BindingFlags.Public | BindingFlags.Instance)!;
    private static PropertyInfo ReferencedQuieriesPI = typeof(IQueryProvider).GetProperty(nameof(IQueryProvider.ReferencedQueries), BindingFlags.Public | BindingFlags.Instance)!;
    private static PropertyInfo ItemPI = typeof(IReadOnlyList<QueryCommand>).GetProperty("Item")!;
    private static PropertyInfo SQLPI = typeof(NORM).GetProperty(nameof(NORM.SQL))!;
    private ParameterExpression? _p;

    public CorrelatedQueryExpressionVisitor(IDataContext dataProvider, IQueryProvider queryProvider, CancellationToken cancellationToken)
    {
        _dataProvider = dataProvider;
        _cancellationToken = cancellationToken;
        _forPrepare = true;
        _queryProvider = queryProvider;
        //_refs = new();
    }

    public CorrelatedQueryExpressionVisitor(IDataContext dataProvider, IQueryProvider queryProvider, Type entityType)
    {
        _dataProvider = dataProvider;
        _queryProvider = queryProvider;
        _entityType = entityType;
        _forPrepare = false;
    }

    //public List<QueryCommand>? ReferencedQueries => _refs;

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
                && node.Arguments is [Expression exp] && exp.Type.IsAssignableTo(typeof(QueryCommand)) && exp.Has<ConstantExpression>(out var ce))
            {
                QueryCommand cmd;

                if (ce.Type.IsClass)
                {
                    var keyCmd = new ExpressionKey(exp, _dataProvider.ExpressionsCache, _queryProvider);
                    if (!_dataProvider.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                    {
                        // if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Exists expression miss");

                        var pExp = Expression.Parameter(typeof(object));
                        var replace = new ReplaceConstantVisitor(Expression.Convert(pExp, ce!.Type));
                        var body = replace.Visit(exp);
                        var d = Expression.Lambda<Func<object?, object>>(body, pExp).Compile();

                        _dataProvider.ExpressionsCache[keyCmd] = d;
                        cmd = (QueryCommand)d(ce.Value);

                        if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                        {
                            _dataProvider.Logger.LogTrace("Exists expression miss. hascode: {hash}, value: {value}", keyCmd.GetHashCode(), d(ce.Value));
                        }
                        else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Exists expression miss");
                    }
                    else
                        cmd = (QueryCommand)((Func<object?, object>)dCmd)(ce!.Value);
                }
                else if (ce.Value is int idx)
                {
                    cmd = _queryProvider.ReferencedQueries[idx];
                }
                else
                    throw new InvalidOperationException();

                if (!_forPrepare)
                {
                    var asEnumMI = AnyMIGeneric.MakeGenericMethod(cmd.EntityType);
                    var dp = Expression.Constant(this);
                    var p1 = Expression.Parameter(typeof(QueryCommand));
                    var body = Expression.Call(dp, asEnumMI, p1);
                    var lambda = Expression.Lambda<Func<QueryCommand, bool>>(body, p1);

                    Func<QueryCommand, bool> del;
                    var key = new ExpressionKey(lambda, _dataProvider.ExpressionsCache, _queryProvider);
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

                    var idx = _queryProvider!.AddCommand(cmd);

                    //Expression dfg = (IQueryProvider queryProvider) => NORM.SQL.exists(queryProvider.ReferencedColumns[idx]);
                    //var replace = new ReplaceArgumentVisitor(0, (IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx]);
                    //node.Arguments[0]=(Expression)(IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx];
                    _p = Expression.Parameter(typeof(IQueryProvider));
                    var lambda = Expression.Lambda(Expression.Call(node.Object, node.Method, Expression.Property(Expression.Property(_p, ReferencedQuieriesPI), ItemPI, Expression.Constant(idx))), _p);
                    return lambda;
                }
            }
        }
        else if (node.Object is not null && node.Object.Type.IsGenericType && node.Object.Type.GetGenericTypeDefinition().IsAssignableTo(typeof(Entity<>)))
        {
            QueryCommand cmd;

            var keyCmd = new ExpressionKey(node.Object, _dataProvider.ExpressionsCache, _queryProvider);
            if (!_dataProvider.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
            {
                //var d = Expression.Lambda<Func<QueryCommand>>(Expression.Call(node.Object, ToCommandMI)).Compile();
                var d = Expression.Lambda<Func<QueryCommand>>(Expression.Convert(node.Object, typeof(QueryCommand))).Compile();

                _dataProvider.ExpressionsCache[keyCmd] = d;
                cmd = d();

                if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    _dataProvider.Logger.LogTrace("Subquery expression miss. hascode: {hash}, value: {value}", keyCmd.GetHashCode(), d());
                }
                else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Subquery expression miss");
            }
            else
                cmd = ((Func<QueryCommand>)dCmd)();

            return ReplaceQueryCommand(node, cmd);
        }
        else if (node.Object?.Type.IsAssignableTo(typeof(QueryCommand)) ?? false)
        {
            var tv = new TwoTypeExpressionVisitor<ParameterExpression, ConstantExpression>();
            tv.Visit(node.Object);
            if (tv.Has1)
            {
                //throw new NotImplementedException();
            }

            if (tv.Has2)
            {
                QueryCommand cmd;

                var keyCmd = new ExpressionKey(node.Object, _dataProvider.ExpressionsCache, _queryProvider);
                if (!_dataProvider.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                {
                    var p = Expression.Parameter(typeof(object));
                    var replace = new ReplaceConstantVisitor(Expression.Convert(p, tv.Target2!.Type));
                    var body = replace.Visit(node.Object);

                    var d = Expression.Lambda<Func<object?, QueryCommand>>(body, p).Compile();

                    _dataProvider.ExpressionsCache[keyCmd] = d;
                    cmd = d(tv.Target2!.Value);

                    if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                    {
                        _dataProvider.Logger.LogTrace("Subquery expression miss. hascode: {hash}, value: {value}", keyCmd.GetHashCode(), d(tv.Target2!.Value));
                    }
                    else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Subquery expression miss");
                }
                else
                    cmd = ((Func<object?, QueryCommand>)dCmd)(tv.Target2!.Value);

                return ReplaceQueryCommand(node, cmd);
            }
        }

        return base.VisitMethodCall(node);
    }

    private Expression ReplaceQueryCommand(MethodCallExpression node, QueryCommand cmd)
    {
        if (!_forPrepare)
        {
            throw new NotImplementedException();
        }
        else
        {
            cmd = cmd.Clone();

            if (node.Method.Name.StartsWith("First"))
            {
                cmd.Paging.Limit = 1;
            }
            else if (node.Method.Name.StartsWith("Single"))
            {
                cmd.Paging.Limit = 1;
            }
            else if (node.Method.Name.StartsWith("Any"))
            {
                //cmd.Paging.Limit = 1;
                cmd.IgnoreColumns = true;
            }

            if (!cmd.IsPrepared)
                cmd.PrepareCommand(_cancellationToken);

            var idx = _queryProvider!.AddCommand(cmd);

            // Expression dfg = (IQueryProvider queryProvider) => NORM.SQL.exists(queryProvider.ReferencedQueries[idx]);
            //var replace = new ReplaceArgumentVisitor(0, (IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx]);
            //node.Arguments[0]=(Expression)(IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx];
            _p = Expression.Parameter(typeof(IQueryProvider));
            LambdaExpression lambda;

            if (node.Method.Name.StartsWith("Any"))
            {
                lambda = Expression.Lambda(
                    Expression.Call(Expression.Property(null, SQLPI), ExistsMI,
                        Expression.Property(Expression.Property(_p, ReferencedQuieriesPI), ItemPI, Expression.Constant(idx))
                    ), _p
                );
            }
            else
                lambda = Expression.Lambda(Expression.Property(Expression.Property(_p, ReferencedQuieriesPI), ItemPI, Expression.Constant(idx)), _p);

            return lambda;
        }
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        if (node is LambdaExpression lambdaExpression && lambdaExpression.Parameters is [Expression exp])
        {
            if (exp.Type == typeof(TableAlias))
                return Visit(lambdaExpression.Body);
            else if (!_forPrepare && typeof(IQueryProvider).IsAssignableTo(exp.Type))
            {
                return Expression.Lambda(Visit(lambdaExpression.Body), Expression.Parameter(_entityType!));
            }
        }

        return base.VisitLambda(node);
    }
    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Convert)
        {
            var r = Visit(node.Operand);
            if (r is LambdaExpression lambda && lambda.Body.Type.IsAssignableTo(typeof(QueryCommand)))
                return r;

            return Expression.Convert(r, node.Type);
        }

        return base.VisitUnary(node);
    }
    bool Any<TResult>(QueryCommand cmd)
    {
        var ee = (IEnumerable<TResult>)_dataProvider.CreateEnumerator((QueryCommand<TResult>)cmd, null);
        return ee.Any();
    }
}