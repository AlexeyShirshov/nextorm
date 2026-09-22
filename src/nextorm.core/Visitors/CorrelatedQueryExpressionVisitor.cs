using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Reflection;
namespace NextORM.Core;

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
    private readonly IQueryRegistry _queryProvider;
    private readonly Type? _entityType;

    //private readonly List<QueryCommand>? _refs;
    private static readonly MethodInfo AnyMIGeneric = typeof(CorrelatedQueryExpressionVisitor).GetMethod(nameof(Any), BindingFlags.NonPublic | BindingFlags.Instance)!;
    //private static MethodInfo ToCommandMI = typeof(EntityBuilder<>).GetMethod("ToCommand", BindingFlags.Public | BindingFlags.Instance)!;
    private static readonly MethodInfo ConcatMI = typeof(string).GetMethods(BindingFlags.Public | BindingFlags.Static).First(it => it.Name == nameof(string.Concat) && it.GetParameters().Length == 2);
    private static readonly PropertyInfo ReferencedQueriesPI = typeof(IQueryRegistry).GetProperty(nameof(IQueryRegistry.ReferencedQueries), BindingFlags.Public | BindingFlags.Instance)!;
    private static readonly PropertyInfo ItemPI = typeof(IReadOnlyList<QueryCommand>).GetProperty("Item")!;
    private static readonly PropertyInfo SQLPI = typeof(SqlFunctions).GetProperty(nameof(SqlFunctions.Sql))!;
    private Stack<ParameterExpression>? _outerParams;

    //private ParameterExpression? _p;

    /// <summary>
    /// Pushes <paramref name="parameter"/> as an outer parameter for the lifetime of the returned
    /// scope. A subquery translated while the scope is active may reference the parameter's members;
    /// they are registered as outer references of the command this visitor prepares. Used for the
    /// positions that are visited per projection argument / sorting item rather than as a whole lambda
    /// (the projection lambda parameter is never seen by <see cref="VisitLambda{T}"/> there).
    /// </summary>
    internal IDisposable PushOuter(ParameterExpression parameter)
    {
        (_outerParams ??= []).Push(parameter);
        return new OuterScope(this);
    }

    private sealed class OuterScope(CorrelatedQueryExpressionVisitor owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._outerParams!.Pop();
        }
    }

    /// <summary>
    /// Rewrites a member access of a currently-pushed outer parameter (for example the XML column a
    /// correlated <c>CROSS/OUTER APPLY</c> source is built from) into an <see cref="OuterRefMarker{T}"/>
    /// registered on the command this visitor prepares. Used by
    /// <see cref="XmlNodesExpression.TryCreate"/> for the <c>xml.nodes()</c> rowset operand, whose
    /// rendered form is <c>alias.column</c>.
    /// </summary>
    internal Expression RewriteOuterReference(Expression expression)
        => new ReplaceConstantsExpressionVisitor(_outerParams, _queryProvider).Visit(expression);

    /// <summary>True when <paramref name="expression"/> references any parameter currently treated as outer.</summary>
    private bool ReferencesOuter(Expression expression)
    {
        var detector = new OuterReferenceDetector(_outerParams!);
        detector.Visit(expression);
        return detector.Found;
    }

    private sealed class OuterReferenceDetector(IEnumerable<ParameterExpression> outer) : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _outer = [.. outer];
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_outer.Contains(node))
                Found = true;

            return base.VisitParameter(node);
        }
    }

    public CorrelatedQueryExpressionVisitor(IQueryMaterializer dataProvider, IQueryRegistry queryProvider, CancellationToken cancellationToken, ILogger? logger)
    {
        _dataProvider = dataProvider;
        _logger = logger;
        _cancellationToken = cancellationToken;
        _forPrepare = true;
        _queryProvider = queryProvider;
        //_refs = new();
    }

    public CorrelatedQueryExpressionVisitor(IQueryMaterializer dataProvider, IQueryRegistry queryProvider, Type entityType, ILogger? logger)
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
        if (node.Method.DeclaringType == typeof(CommonFunctions)
            || node.Method.DeclaringType == typeof(ClickHouseFunctions))
        {
            if ((node.Method.Name == nameof(CommonFunctions.exists)
                || node.Method.Name == nameof(CommonFunctions.all)
                || node.Method.Name == nameof(CommonFunctions.any)
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
                    if (node.Method.Name == nameof(CommonFunctions.exists))
                        cmd.IgnoreColumns = true;

                    if (!cmd.IsPrepared)
                    {
                        // var shouldAliasFrom = (_queryProvider.OuterReferences?.Count ?? 0) > oldRefCnt;

                        cmd.PrepareCommand(false, _cancellationToken);
                    }

                    var idx = _queryProvider!.AddCommand(cmd);

                    //Expression dfg = (IQueryRegistry queryProvider) => SqlFunctions.Sql.exists(queryProvider.ReferencedColumns[idx]);
                    //var replace = new ReplaceArgumentVisitor(0, (IQueryRegistry queryProvider) => queryProvider.ReferencedColumns[idx]);
                    //node.Arguments[0]=(Expression)(IQueryRegistry queryProvider) => queryProvider.ReferencedColumns[idx];
                    var p = Expression.Parameter(typeof(IQueryRegistry));
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
            else if ((node.Method.Name == nameof(CommonFunctions.@in)
                || node.Method.Name == nameof(ClickHouseFunctions.global_in))
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

                    //Expression dfg = (IQueryRegistry queryProvider) => SqlFunctions.Sql.exists(queryProvider.ReferencedColumns[idx]);
                    //var replace = new ReplaceArgumentVisitor(0, (IQueryRegistry queryProvider) => queryProvider.ReferencedColumns[idx]);
                    //node.Arguments[0]=(Expression)(IQueryRegistry queryProvider) => queryProvider.ReferencedColumns[idx];
                    var p = Expression.Parameter(typeof(IQueryRegistry));
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
        else if (GetEntityBuilderReceiver(node) is { } builderReceiver)
        {
            if (IsAggregateTerminal(node.Method.Name))
                return ReplaceAggregateTerminal(node, builderReceiver);

            QueryCommand cmd;

            var keyCmd = new ExpressionKey(builderReceiver, _queryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
            {
                //var d = Expression.Lambda<Func<QueryCommand>>(Expression.Call(node.Object, ToCommandMI)).Compile();
                var d = Expression.Lambda<Func<QueryCommand>>(Expression.Convert(builderReceiver, typeof(QueryCommand))).Compile();

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
            // A correlated scalar subquery: the inner query references a member of an outer parameter
            // (the immediate one, or - at correlation depth greater than one - a marker an ancestor
            // scope already resolved). Route it through GetQueryCommand so the outer member is replaced
            // with an OuterRefMarker and registered on the root command; the non-correlated closure path
            // below would either leave the outer parameter free and fail at Compile(), or rewrite the
            // marker's index constant as a closure value and corrupt the marker.
            if (_forPrepare && ((_outerParams is { Count: > 0 } && ReferencesOuter(node.Object)) || ContainsOuterRefMarker(node.Object)))
            {
                var outerCmd = GetQueryCommand(node.Object);
                return ReplaceQueryCommand(node, outerCmd);
            }

            var tv = new TypeExpressionVisitor<ParameterExpression, ConstantExpression>();
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
                    var replace = new ReplaceConstantExpressionVisitor(Expression.Convert(p, tv.Target2!.Type));
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

    /// <summary>
    /// Returns the <see cref="EntityBuilder{TEntity}"/> a method call is invoked on, whether the
    /// terminal is still an instance method (<paramref name="node"/>.<c>Object</c>) or an extension
    /// method (the receiver is the first argument).
    /// </summary>
    private static Expression? GetEntityBuilderReceiver(MethodCallExpression node) => node.Object switch
    {
        { Type.IsGenericType: true } instance when IsEntityBuilder(instance.Type) => instance,
        null when node.Arguments.Count > 0 && node.Arguments[0].Type.IsGenericType && IsEntityBuilder(node.Arguments[0].Type) => node.Arguments[0],
        _ => null
    };

    private static bool IsEntityBuilder(Type type) => type.GetGenericTypeDefinition().IsAssignableTo(typeof(EntityBuilder<>));

    /// <summary>
    /// True when <paramref name="expression"/> is a correlated scalar subquery whose terminal is a
    /// <c>*OrDefault</c> method. The SQL is identical to the non-<c>OrDefault</c> terminal, so this is
    /// how the projection learns that a NULL result must become <c>default</c> instead of throwing.
    /// </summary>
    internal static bool IsOrDefaultScalar(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;

        return expression is MethodCallExpression call
            && call.Method.Name.EndsWith("OrDefault", StringComparison.Ordinal)
            && call.Object?.Type.IsAssignableTo(typeof(QueryCommand)) == true;
    }

    /// <summary>
    /// True for the <see cref="EntityBuilder{TEntity}"/> terminal methods that execute an aggregate
    /// immediately. Inside a subquery they are rewritten to the equivalent aggregate projection (see
    /// <see cref="ReplaceAggregateTerminal"/>) instead of being executed as a separate query.
    /// </summary>
    private static bool IsAggregateTerminal(string methodName) => methodName switch
    {
        nameof(EntityBuilderExtensions.Count) => true,
        nameof(EntityBuilderExtensions.Min) => true,
        nameof(EntityBuilderExtensions.Max) => true,
        nameof(EntityBuilderExtensions.Avg) => true,
        nameof(EntityBuilderExtensions.Sum) => true,
        nameof(EntityBuilderExtensions.Stdev) => true,
        nameof(EntityBuilderExtensions.Stdevp) => true,
        nameof(EntityBuilderExtensions.Var) => true,
        nameof(EntityBuilderExtensions.Varp) => true,
        _ => false
    };

    /// <summary>
    /// Rewrites an aggregate terminal used inside a subquery (<c>inner.Where(...).Count()</c>) into
    /// the equivalent scalar subquery and reads it back with the single-row <c>First</c> terminal, so
    /// the query renders as <c>(select count(*)/sum(...) from ...)</c> instead of being rejected.
    /// </summary>
    private Expression ReplaceAggregateTerminal(MethodCallExpression node, Expression builderReceiver)
    {
        var selectCall = AggregateTerminalRewriter.Rewrite(node, builderReceiver);
        var cmd = GetQueryCommand(selectCall);
        var firstCall = Expression.Call(selectCall, selectCall.Type.GetMethod("First", Type.EmptyTypes)!);
        return ReplaceQueryCommand(firstCall, cmd);
    }

    /// <summary>True when the expression already contains an <see cref="OuterRefMarker{T}"/> from an ancestor scope.</summary>
    private static bool ContainsOuterRefMarker(Expression expression)
    {
        var detector = new OuterRefMarkerDetector();
        detector.Visit(expression);
        return detector.Found;
    }

    private sealed class OuterRefMarkerDetector : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitNew(NewExpression node)
        {
            if (node.Type.IsGenericType && node.Type.GetGenericTypeDefinition() == typeof(OuterRefMarker<>))
                Found = true;

            return base.VisitNew(node);
        }
    }

    /// <summary>
    /// True when the expression contains a nested subquery of its own (a member call on a
    /// <see cref="QueryCommand"/>). Such a command must share the root registry and cannot be cached
    /// through <c>ExpressionsCache</c>, because the reference indices its preparation bakes in are
    /// relative to the registry it is rendered with.
    /// </summary>
    private static bool ContainsSubqueryExpression(Expression expression)
    {
        var detector = new NestedSubqueryDetector();
        detector.Visit(expression);
        return detector.Found;
    }

    private sealed class NestedSubqueryDetector : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object?.Type.IsAssignableTo(typeof(QueryCommand)) == true
                || (node.Object is null && node.Arguments.Count > 0 && node.Arguments[0].Type.IsAssignableTo(typeof(QueryCommand))))
                Found = true;

            return Found ? node : base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// Replaces the delegate parameters a correlated subquery body was compiled with by their captured
    /// values, so the built command holds closure accesses instead of free parameters. A nested
    /// subquery in its projection can then still be recognised (and prepared) when the command itself
    /// is prepared, which is what correlation depth greater than one needs.
    /// </summary>
    private sealed class ReplaceParametersByValueVisitor(List<(ParameterExpression Parameter, object? Value)> parameters) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            for (var (i, count) = (0, parameters.Count); i < count; i++)
            {
                var (parameter, value) = parameters[i];
                if (ReferenceEquals(parameter, node))
                    return Expression.Constant(value, node.Type);
            }

            return base.VisitParameter(node);
        }
    }

    private QueryCommand GetQueryCommand(Expression exp)
    {
        // Nested commands resolve ReferencedQueries/OuterReferences against the root registry, so a
        // marker registered by an ancestor scope (correlation depth > 1) is looked up there too. A
        // marker that is already baked into `exp` is left in place; it stays valid because every
        // command created below this point shares the root registry (see QueryCommand.OuterRegistry).
        var registry = (_queryProvider as QueryCommand)?.RootRegistry ?? _queryProvider;

        QueryCommand cmd;
        var predVisitor = new PredicateExpressionVisitor<int>((exp, storeValue) => exp is IndexExpression idxExp

            && idxExp.Object is MemberExpression propExp && propExp.Member == ReferencedQueriesPI && propExp.Expression is ParameterExpression && exp.Type.IsAssignableTo(typeof(IQueryRegistry))
            && idxExp.Arguments is [ConstantExpression c] && c.Value is int idx && storeValue(idx)
        );
        predVisitor.Visit(exp);

        if (predVisitor.Result)
        {
            cmd = registry.ReferencedQueries[predVisitor.Value];
        }
        else
        {
            var constRepl = new ReplaceConstantsExpressionVisitor(_outerParams, _queryProvider);
            var body = constRepl.Visit(exp);

            if (constRepl.Params.Count > 0)
            {
                var pp = constRepl.Params.Select(it => it.Item1);
                var args = constRepl.Params.Select(it => it.Item2).ToArray();

                // A body that carries an outer reference bakes the marker index - a position in the
                // *current* outer command's OuterReferences - into the compiled delegate. The same
                // member can map to a different index on another command (depending on how many
                // references that command registered first), so sharing the delegate through
                // ExpressionsCache would resolve the marker against the wrong list (wrong SQL or an
                // out-of-range index). Compile such bodies per outer command instead, and inline the
                // captured values rather than passing them as delegate parameters: a nested subquery in
                // the projection keeps its closure members (which the subquery visitor must still
                // recognise when this command itself is prepared), instead of free parameters it cannot
                // resolve (see ReplaceParametersByValueVisitor).
                if (ContainsOuterRefMarker(body) || ContainsSubqueryExpression(body))
                {
                    var inlinedBody = new ReplaceParametersByValueVisitor(constRepl.Params).Visit(body);
                    var d = Expression.Lambda<Func<QueryCommand>>(inlinedBody).Compile();
                    cmd = d();
                    cmd.OuterRegistry = _queryProvider;
                }
                else
                {
                    var keyCmd = new ExpressionKey(exp, _queryProvider);
                    if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                    {
                        var d = Expression.Lambda(body, pp).Compile();

                        DataContextCache.ExpressionsCache[keyCmd] = d;
                        cmd = (QueryCommand)d.DynamicInvoke(args)!;

                        if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                        {
                            _logger.LogTrace("Subquery expression miss: {exp}", exp);
                        }
                        else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Subquery expression miss");
                    }
                    else
                        cmd = (QueryCommand)dCmd.DynamicInvoke(args)!;
                }
            }
            else
            {
                // No captured value at all (a fully constant/outer-referenced subquery). The outer
                // members have already been rewritten to OuterRefMarker nodes by the visitor above, so
                // the command can be built without closure parameters.
                var d = Expression.Lambda<Func<QueryCommand>>(body).Compile();
                cmd = d();
                if (ContainsOuterRefMarker(body) || ContainsSubqueryExpression(body))
                    cmd.OuterRegistry = _queryProvider;
            }
        }

        return cmd;
    }

    /// <summary>
    /// Builds the command the expression <paramref name="body"/> represents while the currently pushed
    /// outer parameters are in scope: their member accesses become <see cref="OuterRefMarker{T}"/>
    /// nodes registered on the command this visitor was created for. Used by a correlated
    /// <c>CROSS/OUTER APPLY</c> source, whose derived query is not part of a projection and therefore
    /// never reaches <see cref="ReplaceQueryCommand"/>.
    /// </summary>
    internal QueryCommand BuildQueryCommand(Expression body)
    {
        var cmd = GetQueryCommand(body);
        cmd.OuterRegistry ??= _queryProvider;
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
            // Every subquery command shares the root registry, so the reference indices baked while it
            // is prepared resolve against the registry the renderer uses (the root command). This also
            // covers commands built outside GetQueryCommand (the closure path below).
            cmd.OuterRegistry ??= _queryProvider;

            var methodName = node.Method.Name;
            if (methodName.StartsWith("First", StringComparison.Ordinal))
            {
                cmd.Paging.Limit = 1;
            }
            else if (methodName.StartsWith("Single", StringComparison.Ordinal))
            {
                // Single allows at most one row. Render two so a cardinality-enforcing dialect rejects
                // the second row; the renderer rejects it up front on dialects that do not (SQLite).
                cmd.SingleScalar = true;
                cmd.Paging.Limit = 2;
            }
            else if (methodName.StartsWith("Any", StringComparison.Ordinal))
            {
                //cmd.Paging.Limit = 1;
                cmd.IgnoreColumns = true;
            }

            // The terminal method is the only place that distinguishes First from FirstOrDefault (the
            // SQL is identical), so carry it to the materializer through the command.
            cmd.DefaultOnEmpty = methodName.EndsWith("OrDefault", StringComparison.Ordinal);

            if (!cmd.IsPrepared)
                cmd.PrepareCommand(false, _cancellationToken);

            var idx = _queryProvider!.AddCommand(cmd);

            // Expression dfg = (IQueryRegistry queryProvider) => SqlFunctions.Sql.exists(queryProvider.ReferencedQueries[idx]);
            //var replace = new ReplaceArgumentVisitor(0, (IQueryRegistry queryProvider) => queryProvider.ReferencedColumns[idx]);
            //node.Arguments[0]=(Expression)(IQueryRegistry queryProvider) => queryProvider.ReferencedColumns[idx];
            var p = Expression.Parameter(typeof(IQueryRegistry));
            LambdaExpression lambda;

            if (node.Method.Name.StartsWith("Any", StringComparison.Ordinal))
            {
                lambda = Expression.Lambda(
                    Expression.Call(Expression.Property(null, SQLPI), CommonFunctions.ExistsMI,
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
            else if (!_forPrepare && typeof(IQueryRegistry).IsAssignableTo(exp.Type))
            {
                return Expression.Lambda(Visit(lambdaExpression.Body), Expression.Parameter(_entityType!));
            }
            else if (_forPrepare)
            {
                // Any entity lambda prepared by this visitor (for example the WHERE predicate)
                // exposes its parameter as an outer parameter for any subquery nested in its body.
                // Previously this was only done when the body was itself a SqlFunctions.Sql call, so an
                // exists/scalar subquery combined with, say, a binary predicate could not correlate.
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