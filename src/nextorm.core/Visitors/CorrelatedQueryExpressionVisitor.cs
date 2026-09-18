using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Reflection;
namespace nextorm.core;

/// <summary>
/// Collects the outer references of a correlated subquery.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Static readonly reflection-metadata fields (AnyMIGeneric, ConcatMI, ...) are intentionally PascalCase as immutable lookup tables; IDE1006 is a suggestion and is not enforced by the build.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "Reflection is required to bind the private Any/Concat members into expression trees; there is no public API alternative, and the lookups are static and confined to this visitor.")]
public class CorrelatedQueryExpressionVisitor : ExpressionVisitor
{
    private readonly CancellationToken _cancellationToken;
    private readonly bool _forPrepare = false;
    private readonly IQueryMaterializer _dataProvider;
    private readonly ILogger? _logger;
    private readonly IQueryProvider _queryProvider;
    private readonly Type? _entityType;

    //private readonly List<QueryCommand>? _refs;
    private static readonly MethodInfo AnyMIGeneric = typeof(CorrelatedQueryExpressionVisitor).GetMethod(nameof(Any), BindingFlags.NonPublic | BindingFlags.Instance)!;
    //private static MethodInfo ToCommandMI = typeof(EntityBuilder<>).GetMethod("ToCommand", BindingFlags.Public | BindingFlags.Instance)!;
    private static readonly MethodInfo ConcatMI = typeof(string).GetMethods(BindingFlags.Public | BindingFlags.Static).First(it => it.Name == nameof(string.Concat) && it.GetParameters().Length == 2);
    private static readonly PropertyInfo ReferencedQueriesPI = typeof(IQueryProvider).GetProperty(nameof(IQueryProvider.ReferencedQueries), BindingFlags.Public | BindingFlags.Instance)!;
    private static readonly PropertyInfo ItemPI = typeof(IReadOnlyList<QueryCommand>).GetProperty("Item")!;
    private static readonly PropertyInfo SQLPI = typeof(NORM).GetProperty(nameof(NORM.SQL))!;
    private Stack<ParameterExpression>? _outerParams;

    //private ParameterExpression? _p;

    public CorrelatedQueryExpressionVisitor(IQueryMaterializer dataProvider, IQueryProvider queryProvider, CancellationToken cancellationToken, ILogger? logger)
    {
        _dataProvider = dataProvider;
        _logger = logger;
        _cancellationToken = cancellationToken;
        _forPrepare = true;
        _queryProvider = queryProvider;
        //_refs = new();
    }

    public CorrelatedQueryExpressionVisitor(IQueryMaterializer dataProvider, IQueryProvider queryProvider, Type entityType, ILogger? logger)
    {
        _dataProvider = dataProvider;
        _logger = logger;
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
            if ((node.Method.Name == nameof(NORM.NORM_SQL.exists)
                || node.Method.Name == nameof(NORM.NORM_SQL.all)
                || node.Method.Name == nameof(NORM.NORM_SQL.any)
            )
                && node.Arguments is [Expression exp] && exp.Type.IsAssignableTo(typeof(QueryCommand)))
            {
                var oldRefCnt = _queryProvider.OuterReferences?.Count ?? 0;

                QueryCommand cmd = GetQueryCommand(exp);

                if (!_forPrepare)
                {
                    var asEnumMI = AnyMIGeneric.MakeGenericMethod(cmd.EntityType!);
                    var dp = Expression.Constant(this);
                    var p1 = Expression.Parameter(typeof(QueryCommand));
                    var body = Expression.Call(dp, asEnumMI, p1);
                    var lambda = Expression.Lambda<Func<QueryCommand, bool>>(body, p1);

                    Func<QueryCommand, bool> del;
                    var key = new ExpressionKey(lambda, _queryProvider);
                    if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var dLambda))
                    {
                        var d = lambda.Compile();
                        DataContextCache.ExpressionsCache[key] = d;
                        del = d;
                    }
                    else
                        del = (Func<QueryCommand, bool>)dLambda;

                    return Expression.Constant(del(cmd));
                }
                else
                {
                    if (node.Method.Name == nameof(NORM.NORM_SQL.exists))
                        cmd.IgnoreColumns = true;

                    if (!cmd.IsPrepared)
                    {
                        // var shouldAliasFrom = (_queryProvider.OuterReferences?.Count ?? 0) > oldRefCnt;

                        cmd.PrepareCommand(false, _cancellationToken);
                    }

                    var idx = _queryProvider!.AddCommand(cmd);

                    //Expression dfg = (IQueryProvider queryProvider) => NORM.SQL.exists(queryProvider.ReferencedColumns[idx]);
                    //var replace = new ReplaceArgumentVisitor(0, (IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx]);
                    //node.Arguments[0]=(Expression)(IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx];
                    var p = Expression.Parameter(typeof(IQueryProvider));
                    var lambda = Expression.Lambda(
                        Expression.Call(node.Object, node.Method,
                            Expression.Convert(
                                Expression.Property(
                                    Expression.Property(p, ReferencedQueriesPI)
                                    , ItemPI
                                    , Expression.Constant(idx)
                                )
                                , exp.Type
                            )
                        )
                        , p
                    );
                    return lambda;
                }
            }
            else if ((node.Method.Name == nameof(NORM.NORM_SQL.@in))
                && node.Arguments is [Expression propExp, Expression cmdExp] && cmdExp.Type.IsAssignableTo(typeof(QueryCommand)))
            {
                QueryCommand cmd = GetQueryCommand(cmdExp);

                if (!_forPrepare)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    if (!cmd.IsPrepared)
                        cmd.PrepareCommand(false, _cancellationToken);

                    var idx = _queryProvider!.AddCommand(cmd);

                    //Expression dfg = (IQueryProvider queryProvider) => NORM.SQL.exists(queryProvider.ReferencedColumns[idx]);
                    //var replace = new ReplaceArgumentVisitor(0, (IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx]);
                    //node.Arguments[0]=(Expression)(IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx];
                    var p = Expression.Parameter(typeof(IQueryProvider));
                    var lambda = Expression.Lambda(
                        Expression.Call(node.Object, node.Method,
                            propExp,
                            Expression.Convert(
                                Expression.Property(
                                    Expression.Property(p, ReferencedQueriesPI)
                                    , ItemPI
                                    , Expression.Constant(idx)
                                )
                            , cmdExp.Type
                            )
                        )
                        , p
                    );

                    return lambda;
                }
            }

            return node;
        }
        else if (node.Object is not null && node.Object.Type.IsGenericType && node.Object.Type.GetGenericTypeDefinition().IsAssignableTo(typeof(EntityBuilder<>)))
        {
            QueryCommand cmd;

            var keyCmd = new ExpressionKey(node.Object, _queryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
            {
                //var d = Expression.Lambda<Func<QueryCommand>>(Expression.Call(node.Object, ToCommandMI)).Compile();
                var d = Expression.Lambda<Func<QueryCommand>>(Expression.Convert(node.Object, typeof(QueryCommand))).Compile();

                DataContextCache.ExpressionsCache[keyCmd] = d;
                cmd = d();

                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    _logger.LogTrace("Subquery expression miss. hashcode: {hash}, value: {value}", keyCmd.GetHashCode(), d());
                }
                else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Subquery expression miss");
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

                var keyCmd = new ExpressionKey(node.Object, _queryProvider);
                if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                {
                    var p = Expression.Parameter(typeof(object));
                    var replace = new ReplaceConstantVisitor(Expression.Convert(p, tv.Target2!.Type));
                    var body = replace.Visit(node.Object);

                    var d = Expression.Lambda<Func<object?, QueryCommand>>(body, p).Compile();

                    DataContextCache.ExpressionsCache[keyCmd] = d;
                    cmd = d(tv.Target2!.Value);

                    if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                    {
                        _logger.LogTrace("Subquery expression miss. hashcode: {hash}, value: {value}", keyCmd.GetHashCode(), d(tv.Target2!.Value));
                    }
                    else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Subquery expression miss");
                }
                else
                    cmd = ((Func<object?, QueryCommand>)dCmd)(tv.Target2!.Value);

                return ReplaceQueryCommand(node, cmd);
            }
        }

        return base.VisitMethodCall(node);
    }

    private QueryCommand GetQueryCommand(Expression exp)
    {
        QueryCommand cmd;
        var predVisitor = new PredicateExpressionVisitor<int>((exp, storeValue) => exp is IndexExpression idxExp
            && idxExp.Object is MemberExpression propExp && propExp.Member == ReferencedQueriesPI && propExp.Expression is ParameterExpression && exp.Type.IsAssignableTo(typeof(IQueryProvider))
            && idxExp.Arguments is [ConstantExpression c] && c.Value is int idx && storeValue(idx)
        );
        predVisitor.Visit(exp);

        if (predVisitor.Result)
        {
            cmd = _queryProvider.ReferencedQueries[predVisitor.Value];
        }
        else
        {
            var constRepl = new ReplaceConstantsExpressionVisitor(_outerParams, _queryProvider);
            var body = constRepl.Visit(exp);

            if (constRepl.Params.Count > 0)
            {
                var keyCmd = new ExpressionKey(exp, _queryProvider);
                if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                {
                    var pp = constRepl.Params.Select(it => it.Item1);
                    // if (_outerParams is not null)
                    //     pp = pp.Concat(_outerParams);

                    var d = Expression.Lambda(body, pp).Compile();

                    DataContextCache.ExpressionsCache[keyCmd] = d;
                    cmd = (QueryCommand)d.DynamicInvoke(constRepl.Params.Select(it => it.Item2).ToArray())!;

                    if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                    {
                        _logger.LogTrace("Subquery expression miss: {exp}", exp);
                    }
                    else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Subquery expression miss");
                }
                else
                    cmd = (QueryCommand)dCmd.DynamicInvoke(constRepl.Params.Select(it => it.Item2).ToArray())!;
            }
            else
                throw new InvalidOperationException();
        }

        return cmd;
    }

    private Expression ReplaceQueryCommand(MethodCallExpression node, QueryCommand cmd)
    {
        if (!_forPrepare)
        {
            throw new NotImplementedException();
        }
        else
        {
            //cmd = cmd.Clone();

            if (node.Method.Name.StartsWith("First", StringComparison.Ordinal))
            {
                cmd.Paging.Limit = 1;
            }
            else if (node.Method.Name.StartsWith("Single", StringComparison.Ordinal))
            {
                cmd.Paging.Limit = 1;
            }
            else if (node.Method.Name.StartsWith("Any", StringComparison.Ordinal))
            {
                //cmd.Paging.Limit = 1;
                cmd.IgnoreColumns = true;
            }

            if (!cmd.IsPrepared)
                cmd.PrepareCommand(false, _cancellationToken);

            var idx = _queryProvider!.AddCommand(cmd);

            // Expression dfg = (IQueryProvider queryProvider) => NORM.SQL.exists(queryProvider.ReferencedQueries[idx]);
            //var replace = new ReplaceArgumentVisitor(0, (IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx]);
            //node.Arguments[0]=(Expression)(IQueryProvider queryProvider) => queryProvider.ReferencedColumns[idx];
            var p = Expression.Parameter(typeof(IQueryProvider));
            LambdaExpression lambda;

            if (node.Method.Name.StartsWith("Any", StringComparison.Ordinal))
            {
                lambda = Expression.Lambda(
                    Expression.Call(Expression.Property(null, SQLPI), NORM.NORM_SQL.ExistsMI,
                        Expression.Property(Expression.Property(p, ReferencedQueriesPI), ItemPI, Expression.Constant(idx))
                    ), p
                );
            }
            else
                lambda = Expression.Lambda(Expression.Property(Expression.Property(p, ReferencedQueriesPI), ItemPI, Expression.Constant(idx)), p);

            return lambda;
        }
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        if (node is LambdaExpression lambdaExpression && lambdaExpression.Parameters is [ParameterExpression exp])
        {
            if (exp.Type == typeof(TableAlias))
                return Visit(lambdaExpression.Body);
            else if (!_forPrepare && typeof(IQueryProvider).IsAssignableTo(exp.Type))
            {
                return Expression.Lambda(Visit(lambdaExpression.Body), Expression.Parameter(_entityType!));
            }
            else if (lambdaExpression.Body is MethodCallExpression mc
                && mc.Object?.Type is { } objectType
                && typeof(NORM.NORM_SQL).IsAssignableFrom(objectType))
            {
                _outerParams ??= [];
                _outerParams.Push(exp);
                try
                {
                    return Expression.Lambda(Visit(lambdaExpression.Body), exp);
                }
                finally
                {
                    _outerParams.Pop();
                }
            }
        }

        return base.VisitLambda(node);
    }
    protected override Expression VisitBinary(BinaryExpression node)
    {
        Expression? leftNode = null;
        Expression? rightNode = null;

        if (node.Left.Type == node.Right.Type && node.Left.Type == typeof(string) && node.NodeType == ExpressionType.Add)
        {
            leftNode = Visit(node.Left);
            rightNode = Visit(node.Right);
            return Expression.Call(ConcatMI, leftNode, rightNode);
        }
        // else if (!node.Left.Type.Similar(node.Right.Type))
        // {
        //     leftNode = Visit(node.Left);
        //     rightNode = Visit(node.Right);
        //     return Expression.MakeBinary(node.NodeType, Expression.Convert(leftNode, typeof(object)), rightNode);
        // }

        if (node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual)
        {
            if (node.Left.NeedToConvert())
            {
                leftNode = Visit(node.Left);
            }

            if (node.Right.NeedToConvert())
            {
                rightNode = Visit(node.Right);
            }

            if (leftNode is not null || rightNode is not null)
            {
                // Both operands have to be boxed: a value-typed operand (for example the scalar
                // returned by an array any/all or an aggregate) has no == operator against object.
                return Expression.MakeBinary(node.NodeType,
                    Expression.Convert(leftNode ?? node.Left, typeof(object)),
                    Expression.Convert(rightNode ?? node.Right, typeof(object)));
            }
        }

        return base.VisitBinary(node);
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
        var preparedCommand = _dataProvider.GetPreparedQueryCommand((QueryCommand<TResult>)cmd, false, true, CancellationToken.None);
        var ee = (IEnumerable<TResult>)_dataProvider.CreateEnumerator<TResult>(preparedCommand, null);
        return ee.Any();
    }
}