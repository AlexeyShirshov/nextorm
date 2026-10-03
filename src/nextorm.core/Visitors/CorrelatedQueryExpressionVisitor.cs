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
    private static readonly MethodInfo CountBigMI = typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.count_big), [typeof(object[])])!;
    // #148-B R2.1: `SqlFunctions.Sql.@in<T>(column, QueryCommand<T>)`, the child-key existence
    // predicate appended to a many-to-many correlation on the SQL providers.
    private static readonly MethodInfo InCommandMI = typeof(CommonFunctions)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .First(m => m.Name == nameof(CommonFunctions.@in)
            && m.IsGenericMethodDefinition
            && m.GetParameters().Length == 2
            && m.GetParameters()[1].ParameterType.IsGenericType
            && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(QueryCommand<>));
    // #148-B D-R3-5: resolves the principal key that feeds a collection reached through a reference
    // prefix, walking the registered datasets at execution time.
    private static readonly MethodInfo ResolveReferencePrefixKeyMI = typeof(InMemoryNavigationChainEvaluator)
        .GetMethod(nameof(InMemoryNavigationChainEvaluator.ResolveReferencePrefixKey), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
    private Stack<ParameterExpression>? _outerParams;
    private ParameterExpression? _registryParameter;
    // #148-B D6: while visiting an in-memory projection, an unlifted non-nullable scalar read through
    // a reference navigation is rejected (A7) unless a nullable lift or a coalesce compensates it.
    private int _compensated;

    /// <summary>
    /// True while the visitor prepares the command's projection: only there does the A7 guard apply,
    /// because a reference scalar used in a predicate is legitimately nullable (an absent principal
    /// makes the predicate false) rather than a materialization failure.
    /// </summary>
    internal bool ProjectionMode { get; set; }

    private bool IsInMemory => _forPrepare && _dataProvider is InMemoryDataContext;

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

    /// <summary>
    /// Creates the visitor used while preparing a command: it rewrites correlated subqueries and
    /// registers their outer references on <paramref name="queryProvider"/>.
    /// </summary>
    /// <param name="dataProvider">The materializer used to execute non-prepared (Any) subqueries.</param>
    /// <param name="queryProvider">The registry the subqueries and outer references are registered on.</param>
    /// <param name="cancellationToken">The token passed to prepared subcommands.</param>
    /// <param name="logger">The optional logger for cache-miss diagnostics.</param>
    public CorrelatedQueryExpressionVisitor(IQueryMaterializer dataProvider, IQueryRegistry queryProvider, CancellationToken cancellationToken, ILogger? logger)
    {
        _dataProvider = dataProvider;
        _logger = logger;
        _cancellationToken = cancellationToken;
        _forPrepare = true;
        _queryProvider = queryProvider;
        //_refs = new();
    }

    /// <summary>
    /// Creates the visitor used to evaluate a subquery against a concrete entity type rather than to
    /// prepare a command.
    /// </summary>
    /// <param name="dataProvider">The materializer used to execute the subquery.</param>
    /// <param name="queryProvider">The registry the subquery is resolved against.</param>
    /// <param name="entityType">The entity type the resulting subquery lambda is parameterised on.</param>
    /// <param name="logger">The optional logger for cache-miss diagnostics.</param>
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
    /// <inheritdoc/>
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
        else if (TryLowerCollectionTerminalCall(node, out var collectionResult))
        {
            return collectionResult;
        }
        else if (GetEntityBuilderReceiver(node) is { } builderReceiver)
        {
            // D4: the AsEntityBuilder adapter resolves a declared navigation receiver; it must be handled
            // before the generic builder terminals, which would otherwise try to compile (and execute) the
            // adapter body and throw outside the query expression.
            if (TryGetAdapterNavigation(builderReceiver, out var adapterPath))
                return LowerAdapterTerminal(node.Method.Name, adapterPath, node);

            if (IsAggregateTerminal(node.Method.Name))
                return ReplaceAggregateTerminal(node, builderReceiver);

            // A non-aggregate builder terminal (Any/First/Single) whose receiver references a current
            // outer parameter must be built while the outer members are rewritten to markers; compiling
            // the raw receiver would leave the outer parameter free. Route it through GetQueryCommand,
            // which applies the same outer-reference rewrite as the aggregate terminals.
            if (_forPrepare && _outerParams is { Count: > 0 } && ReferencesOuter(builderReceiver))
            {
                var outerCmd = GetQueryCommand(Expression.Convert(builderReceiver, typeof(QueryCommand)));
                return ReplaceQueryCommand(node, outerCmd);
            }

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

    /// <summary>
    /// D4: lowers the <c>Count</c> property of a declared collection navigation to a correlated scalar
    /// count (same command as the <c>Count()</c> terminal), preserving the checked Int32 narrowing.
    /// </summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (_forPrepare
            && node.Member.Name == "Count"
            && node.Expression is { } receiver
            && TryResolveCollectionNavigation(receiver, out var path))
        {
            var (sourceType, condition, childFilter) = BuildCollectionCorrelation(path);
            return LowerCollectionCount(sourceType, condition, wide: true, childFilter);
        }

        // #148-B D6: in memory a declared reference scalar (or a scalar member reached through it) is
        // lowered to a metadata-based correlated scalar, replacing the raw CLR-graph read.
        if (TryLowerReferenceScalar(node, out var referenceScalar))
            return referenceScalar;

        // The in-memory provider must not fall back to the CLR navigation graph (A10): a collection
        // navigation that is not one of the supported terminals/count-property/adapter is rejected.
        if (IsInMemory
            && TryResolveCollectionNavigation(node, out var collectionPath))
        {
            throw new NotSupportedException(
                $"The collection navigation '{collectionPath.FinalHop!.Navigation.Name}' is supported only through " +
                "parameterless Any()/Count()/LongCount(), the Count property or an AsEntityBuilder adapter; " +
                "direct collection access and LINQ composition are not supported by the in-memory provider.");
        }

        return base.VisitMember(node);
    }

    /// <summary>
    /// D4: recognises a parameterless <see cref="Enumerable.Any{T}(IEnumerable{T})"/> /
    /// <see cref="Enumerable.Count{T}(IEnumerable{T})"/> /
    /// <see cref="Enumerable.LongCount{T}(IEnumerable{T})"/> whose receiver is a declared collection
    /// navigation rooted at a current outer parameter, and lowers it to a correlated subquery.
    /// </summary>
    private bool TryLowerCollectionTerminalCall(MethodCallExpression node, out Expression result)
    {
        result = null!;

        if (!_forPrepare
            || node.Method.DeclaringType != typeof(Enumerable)
            || node.Method.Name is not ("Any" or "Count" or "LongCount")
            || node.Arguments is not [Expression receiver]
            || !TryResolveCollectionNavigation(receiver, out var path))
            return false;

        result = LowerCollectionTerminal(node.Method.Name, path, node);
        return true;
    }

    /// <summary>
    /// D4/D-R3-6: recognises the <c>AsEntityBuilder&lt;T&gt;</c> adapter whose receiver is a declared
    /// navigation and resolves that navigation. Returns <see langword="false"/> when the receiver is not
    /// the adapter (for example a composed builder), leaving the generic terminal path in place; throws
    /// when the receiver is the adapter but not a declared navigation.
    /// </summary>
    private bool TryGetAdapterNavigation(Expression builderReceiver, out ResolvedNavigationPath path)
    {
        path = null!;
        if (TypeFacts.UnwrapConvert(builderReceiver) is not MethodCallExpression call
            || call.Method.DeclaringType != typeof(EntityBuilderExtensions)
            || call.Method.Name != nameof(EntityBuilderExtensions.AsEntityBuilder)
            || call.Arguments is not [Expression navigation])
            return false;

        if (TryResolveNavigation(navigation, out path))
            return true;

        throw new NotSupportedException(
            "AsEntityBuilder is only supported on a declared navigation inside a query expression.");
    }

    /// <summary>
    /// D-R3-6: lowers a terminal of an <c>AsEntityBuilder</c> adapter. A collection receiver keeps the
    /// existing correlated collection lowering; a reference receiver (the <c>object?</c> overload) is
    /// lowered with reference semantics — presence plus chain/null behaviour — never reinterpreted as a
    /// collection.
    /// </summary>
    private Expression LowerAdapterTerminal(string methodName, ResolvedNavigationPath path, MethodCallExpression node)
    {
        if (path.FinalHop is { IsCollection: true })
            return LowerCollectionTerminal(methodName, path, node);

        if (!IsInMemory)
            throw new NotSupportedException(
                "AsEntityBuilder on a reference navigation is not supported; only the collection form is supported.");

        return methodName switch
        {
            "Any" => BuildReferenceExists(path),
            "Count" => Expression.Convert(BuildReferenceExists(path), typeof(int)),
            "LongCount" => Expression.Convert(BuildReferenceExists(path), typeof(long)),
            _ => throw new NotSupportedException(
                $"The reference AsEntityBuilder adapter does not support the terminal '{methodName}'; " +
                "only Any/Count/LongCount are supported."),
        };
    }

    private Expression LowerCollectionTerminal(string methodName, ResolvedNavigationPath path, MethodCallExpression node)
    {
        if (methodName is not ("Any" or "Count" or "LongCount"))
            throw new NotSupportedException(
                $"The navigation terminal '{methodName}' is not supported; only parameterless Any/Count/LongCount and the Count property are supported.");

        var (sourceType, condition, childFilter) = BuildCollectionCorrelation(path);

        if (methodName == "Any")
        {
            var anyCommand = ((IDataContext)_dataProvider).CreateCommand(sourceType, new QueryDefinition
            {
                SrcType = sourceType,
                Condition = condition,
                Logger = _logger,
            });
            anyCommand.IgnoreColumns = true;
            anyCommand.JunctionChildFilter = childFilter;
            return ReplaceQueryCommand(node, anyCommand);
        }

        var wide = methodName == "Count";
        return LowerCollectionCount(sourceType, condition, wide, childFilter);
    }

    /// <summary>
    /// #148-B r3 A3′: lowers a collection count to the wide (64-bit) backing scalar plus a separate
    /// CLR checked Int32 narrowing for the <c>Count</c>/property-<c>Count</c> forms. The backing
    /// subquery/typed scalar is always <see cref="long"/> (no in-database int cast); the checked
    /// conversion is applied outside it, so a SQL predicate/boolean/arithmetic stays exact at 64-bit
    /// while a materialized <see cref="int"/> narrows with a uniform <see cref="OverflowException"/>.
    /// <c>LongCount</c> gets no wrapper. The same shape is used by SQL and in-memory; only the SQL
    /// renderer suppresses the (outer, CLR-only) int cast for this provenance.
    /// </summary>
    private Expression LowerCollectionCount(Type sourceType, LambdaExpression condition, bool wide, JunctionChildFilter? childFilter)
    {
        var countCommand = BuildCollectionCountCommand(sourceType, condition, childFilter);
        var typed = RegisterScalarTyped(countCommand, typeof(long));
        return wide ? WideCountNarrowing.ForCount(typed) : typed;
    }

    /// <summary>
    /// Resolves <paramref name="receiver"/> as a declared navigation member chain rooted at one of the
    /// currently pushed outer parameters. Returns <see langword="false"/> for a captured receiver or a
    /// non-navigation chain, so those keep their existing translation.
    /// </summary>
    private bool TryResolveCollectionNavigation(Expression receiver, out ResolvedNavigationPath path)
    {
        if (!TryResolveNavigation(receiver, out path))
            return false;

        if (path.FinalHop is not { IsCollection: true })
        {
            path = null!;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves <paramref name="receiver"/> as a declared navigation member chain rooted at one of the
    /// currently pushed outer parameters, whether the final relation is a reference or a collection.
    /// Returns <see langword="false"/> for a captured receiver or a non-navigation chain.
    /// </summary>
    private bool TryResolveNavigation(Expression receiver, out ResolvedNavigationPath path)
    {
        path = null!;

        var expression = TypeFacts.UnwrapConvert(receiver);
        if (expression is not MemberExpression)
            return false;

        Expression? current = expression;
        while (current is MemberExpression { Expression: { } inner })
            current = inner;

        if (current is not ParameterExpression root)
            return false;

        var bound = IsNavigationRoot(root);

        var ok = NavigationExpansion.TryResolvePath(expression, root, _queryProvider!, out var resolved);
        if (!bound || !ok || resolved is null)
            return false;

        path = resolved;
        EnsureRegisteredSource(resolved);
        return true;
    }

    /// <summary>
    /// #148-B D-R3-5-SQL: a resolved navigation is rooted either at a current outer parameter or at a
    /// navigation-join alias injected by <see cref="NavigationExpansion"/> on the command being prepared
    /// (a collection reached through a reference prefix roots at that prefix's joined alias).
    /// </summary>
    private bool IsNavigationRoot(ParameterExpression root)
    {
        if (_outerParams is not null)
        {
            foreach (var parameter in _outerParams)
            {
                if (ReferenceEquals(parameter, root))
                    return true;
            }
        }

        if (_queryProvider is QueryCommand command && command.NavigationPaths is { Count: > 0 } paths)
        {
            foreach (var parameter in paths.Keys)
            {
                if (ReferenceEquals(parameter, root))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// #148-B R2.5 / A10: a declared navigation is resolved from the process-wide metadata even when
    /// the related type was never materialized in this context. The SQL provider diagnoses that as a
    /// missing table before rendering; the in-memory provider must not silently treat the dataset as
    /// empty (a CLR-graph fallback), so it reports the same missing-source diagnostic at preparation.
    /// </summary>
    private void EnsureRegisteredSource(ResolvedNavigationPath path)
    {
        if (!IsInMemory)
            return;

        for (var i = 0; i < path.Hops.Count; i++)
        {
            var hop = path.Hops[i];
            EnsureRegisteredSource(hop.RelatedType, hop.Navigation.Name);
            if (hop.JunctionType is { } junction)
                EnsureRegisteredSource(junction, hop.Navigation.Name);
        }
    }

    private static void EnsureRegisteredSource(Type entityType, string navigationName)
    {
        if (DataContextCache.Metadata.TryGetValue(entityType, out var metadata)
            && !string.IsNullOrEmpty(metadata.TableName))
            return;

        throw new BuildSqlCommandException(
            $"Table name is not registered for type {entityType}. Materialize the entity first " +
            $"(for example with From<{entityType.Name}>()) so its metadata is registered. " +
            $"Required by the implicit navigation '{navigationName}'.");
    }

    /// <summary>
    /// Builds the correlated source type and predicate for a declared collection path: one-to-many
    /// correlates the child's foreign key to the parent's principal key; many-to-many correlates the
    /// junction's parent foreign key and restricts the relation to the junction rows whose mapped child
    /// exists (duplicate junction rows are preserved; no implicit DISTINCT, no child join). A direct
    /// single-hop collection and — for the in-memory provider — a collection reached through a reference
    /// prefix (D-R3-5: an absent prefix yields a NULL key and hence an empty collection) are supported.
    /// </summary>
    private (Type SourceType, LambdaExpression Condition, JunctionChildFilter? ChildFilter) BuildCollectionCorrelation(ResolvedNavigationPath path)
    {
        var hop = path.FinalHop
            ?? throw new NotSupportedException("A collection navigation path without a final hop is not supported.");

        if (!hop.IsCollection)
            throw new NotSupportedException(
                "Only a declared collection navigation is supported by the correlated collection lowering.");

        if (path.RootBinding.Anchor is not ParameterExpression outerParam)
            throw new NotSupportedException("The collection navigation must be rooted at a query source parameter.");

        Type sourceType;
        ResolvedNavigationLeg leg;

        if (hop.IsManyToMany)
        {
            sourceType = hop.JunctionType
                ?? throw new NotSupportedException($"The many-to-many navigation '{hop.Navigation.Name}' has no junction type.");
            leg = hop.SecondaryLeg
                ?? throw new NotSupportedException($"The many-to-many navigation '{hop.Navigation.Name}' has no parent junction leg.");
        }
        else
        {
            sourceType = hop.RelatedType;
            leg = hop.PrimaryLeg;
        }

        Expression outerKey;
        if (path.Hops.Count == 1)
        {
            outerKey = Expression.Property(outerParam, leg.PrincipalKey.PropertyInfo);
        }
        else if (IsInMemory)
        {
            // #148-B D-R3-5: the collection is keyed by the final principal of the reference prefix. The
            // prefix is walked through the registered datasets (never the CLR graph); when any hop is
            // absent the key is NULL, so an absent intermediate reference cannot match a default-valued
            // foreign key.
            outerKey = BuildReferencePrefixKey(path, outerParam, leg);
        }
        else
        {
            throw new NotSupportedException(
                "Only a direct declared collection navigation (a single hop from the query source) is supported.");
        }

        var navigation = Expression.Parameter(sourceType, "navigation");
        var foreignKey = Expression.Property(navigation, leg.ForeignKey.PropertyInfo);

        var index = _queryProvider!.AddOuterReference(outerKey);
        var markerConstructor = ReplaceConstantsExpressionVisitor.GetOuterRefMarkerCI(outerKey.Type);
        var marker = Expression.Property(
            Expression.New(markerConstructor, Expression.Constant(index)),
            nameof(OuterRefMarker<int>.Ref));

        var (left, right) = AlignNavigationKeys(foreignKey, marker);
        var parentEquality = Expression.Equal(left, right);

        JunctionChildFilter? childFilter = null;
        Expression body = parentEquality;

        if (hop.IsManyToMany)
        {
            // #148-B R2.1: a many-to-many occurrence is a junction row whose mapped child exists. The
            // child leg is PrimaryLeg (junction child FK -> child principal key); the parent leg above is
            // SecondaryLeg.
            var childForeignKey = hop.PrimaryLeg.ForeignKey.PropertyInfo;
            var childKey = hop.PrimaryLeg.PrincipalKey.PropertyInfo;

            if (IsInMemory)
            {
                // The in-memory provider cannot nest a child-key subquery in a correlated subcommand's
                // condition, so the existence test is applied as a source filter on the junction
                // (InMemoryQueryBuilder.CreateEnumerator). Junction multiplicity is still preserved.
                childFilter = new JunctionChildFilter(hop.RelatedType, childForeignKey, childKey);
            }
            else
            {
                body = Expression.AndAlso(parentEquality, BuildJunctionChildExistence(navigation, hop.RelatedType, childForeignKey, childKey));
            }
        }

        return (sourceType, Expression.Lambda(body, navigation), childFilter);
    }

    /// <summary>
    /// #148-B D-R3-5: builds a nullable outer-key expression for a collection reached through a reference
    /// prefix. The prefix hops are walked against the registered datasets at execution time (never the
    /// CLR navigation graph); the value is the final principal's principal key, or <see langword="null"/>
    /// when any prefix hop is absent. The nullable type is what keeps an absent reference from matching a
    /// default-valued foreign key.
    /// </summary>
    private Expression BuildReferencePrefixKey(ResolvedNavigationPath path, ParameterExpression outerParam, ResolvedNavigationLeg collectionLeg)
    {
        var hops = path.Hops;
        var prefixCount = hops.Count - 1;
        var chainHops = new InMemoryNavigationChainHop[prefixCount];

        for (var i = 0; i < prefixCount; i++)
        {
            var hop = hops[i];
            if (hop.IsCollection || hop.IsManyToMany)
                throw new NotSupportedException(
                    $"The navigation '{hop.DeclaringType.Name}.{hop.Navigation.Name}' is a collection; " +
                    "only a reference prefix before a collection is supported.");

            chainHops[i] = new InMemoryNavigationChainHop
            {
                EntityType = hop.RelatedType,
                MatchKey = ResolveHopMatchKey(hop).PropertyInfo,
                NextOuterKey = i < prefixCount - 1 ? ResolveHopOuterKey(hops[i + 1]).PropertyInfo : null,
            };
        }

        var firstOuterKey = ResolveHopOuterKey(hops[0]).PropertyInfo;
        var finalKey = collectionLeg.PrincipalKey.PropertyInfo;
        var keyType = finalKey.PropertyType;
        var nullableType = keyType.IsValueType && Nullable.GetUnderlyingType(keyType) is null
            ? typeof(Nullable<>).MakeGenericType(keyType)
            : keyType;

        var resolution = Expression.Call(
            ResolveReferencePrefixKeyMI,
            Expression.Constant((InMemoryDataContext)_dataProvider),
            Expression.Constant(chainHops),
            Expression.Constant(firstOuterKey),
            Expression.Constant(finalKey),
            Expression.Convert(outerParam, typeof(object)));

        return Expression.Convert(resolution, nullableType);
    }

    /// <summary>
    /// #148-B R2.1: builds <c>junction.child_fk in (select child.pk from child)</c>, the mapped-child
    /// existence predicate appended to a many-to-many correlation on the SQL providers. The subquery is
    /// non-correlated and selects only the child key, so it neither joins the child relation into the
    /// count (no fan-out) nor deduplicates the junction rows.
    /// </summary>
    private Expression BuildJunctionChildExistence(Expression navigation, Type childType, PropertyInfo childForeignKey, PropertyInfo childKey)
    {
        var childKeyType = childKey.PropertyType;
        var childParameter = Expression.Parameter(childType, "child");
        var childCommand = ((IDataContext)_dataProvider).CreateCommand(childKeyType, new QueryDefinition
        {
            Exp = Expression.Lambda(Expression.Property(childParameter, childKey), childParameter),
            SrcType = childType,
            Logger = _logger,
        });

        return Expression.Call(
            Expression.Property(null, SQLPI),
            InCommandMI.MakeGenericMethod(childKeyType),
            Expression.Property(navigation, childForeignKey),
            Expression.Constant(childCommand, typeof(QueryCommand<>).MakeGenericType(childKeyType)));
    }

    /// <summary>
    /// Aligns the two key operands to a common type (unifying a nullable key with its non-nullable
    /// counterpart) so the correlation predicate is well-typed.
    /// </summary>
    private static (Expression Left, Expression Right) AlignNavigationKeys(Expression foreignKey, Expression marker)
    {
        if (foreignKey.Type == marker.Type)
            return (foreignKey, marker);

        var foreignUnderlying = Nullable.GetUnderlyingType(foreignKey.Type);
        var markerUnderlying = Nullable.GetUnderlyingType(marker.Type);

        Type common;
        if (foreignUnderlying is not null && (markerUnderlying ?? marker.Type) == foreignUnderlying)
            common = foreignKey.Type;
        else if (markerUnderlying is not null && (foreignUnderlying ?? foreignKey.Type) == markerUnderlying)
            common = marker.Type;
        else if (foreignKey.Type.IsAssignableFrom(marker.Type))
            common = foreignKey.Type;
        else
            common = marker.Type;

        var left = foreignKey.Type == common ? foreignKey : Expression.Convert(foreignKey, common);
        var right = marker.Type == common ? marker : Expression.Convert(marker, common);
        return (left, right);
    }

    /// <summary>
    /// #148-B r3 A3′: builds the correlated 64-bit count command for a collection navigation. The
    /// projection is always the raw <c>count_big(*)</c> scalar and the result type is always
    /// <see cref="long"/> — the command itself carries no Int32 narrowing/cast. The command is tagged
    /// <see cref="QueryCommand.IsWideNavigationCount"/> so the renderer can recognise the outer checked
    /// conversion as navigation-count-derived (provenance) and the preparer can tag the materialized
    /// Int32 column. <c>Count()</c>/property <c>Count</c> apply the checked narrowing on the registered
    /// long scalar; <c>LongCount()</c> uses it directly.
    /// </summary>
    private QueryCommand BuildCollectionCountCommand(Type sourceType, LambdaExpression condition, JunctionChildFilter? childFilter = null)
    {
        var body = Expression.Call(CommonFunctions.SQLExpression, CountBigMI, Expression.NewArrayInit(typeof(object)));
        var scalar = WideCountNarrowing.ForLongCount(body);
        var projection = Expression.Lambda(scalar, condition.Parameters);

        var command = ((IDataContext)_dataProvider).CreateCommand(typeof(long), new QueryDefinition
        {
            Exp = projection,
            SrcType = sourceType,
            Condition = condition,
            Logger = _logger,
        });
        command.JunctionChildFilter = childFilter;
        command.IsWideNavigationCount = true;
        return command;
    }

    /// <summary>
    /// #148-B r3 A3′: true when <paramref name="expression"/> contains the checked Int32 narrowing of a
    /// wide navigation count — a <c>Convert/ConvertChecked</c> to <see cref="int"/> whose operand is a
    /// registry placeholder whose referenced command is tagged <see cref="QueryCommand.IsWideNavigationCount"/>.
    /// This is provenance (the wide-count origin), not the node shape, so an ordinary checked numeric
    /// conversion or an ordinary int column is never matched.
    /// </summary>
    internal static bool ContainsNavigationCountNarrowing(Expression? expression, IQueryRegistry registry)
    {
        if (expression is null)
            return false;

        var probe = new NavigationCountNarrowingProbe(registry);
        probe.Visit(expression);
        return probe.Found;
    }

    /// <summary>
    /// Walks an expression tree looking for the navigation-count checked narrowing. Used by the query
    /// preparer to tag a materialized Int32 column that derives from a wide count.
    /// </summary>
    private sealed class NavigationCountNarrowingProbe(IQueryRegistry registry) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (!Found
                && node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
                && node.Type == typeof(int)
                && TryGetWideCountCommand(node.Operand, registry, out _))
            {
                Found = true;
                return node;
            }

            return base.VisitUnary(node);
        }
    }

    /// <summary>
    /// Unwraps the conversions around a scalar placeholder and returns the referenced command when it is
    /// the tagged wide navigation count. Shared by the SQL renderer's cast suppression and the preparer's
    /// materialization tagging, so both use exactly the same provenance test.
    /// </summary>
    internal static bool TryGetWideCountCommand(Expression expression, IQueryRegistry registry, out QueryCommand? command)
    {
        command = null;

        var current = expression;
        while (current is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
            current = conversion.Operand;

        if (current is IndexExpression { Arguments: [ConstantExpression { Value: int idx }] }
            && idx >= 0 && idx < registry.ReferencedQueries.Count
            && registry.ReferencedQueries[idx] is { IsWideNavigationCount: true } countCommand)
        {
            command = countCommand;
            return true;
        }

        return false;
    }

    /// <summary>
    /// #148-B D6/R2.3: lowers a declared reference value in memory — a scalar member reached through
    /// one or more reference hops, or the whole reference itself. The longest declared navigation prefix
    /// is resolved through the shared metadata resolver; the remaining members are read off the related
    /// principal (or, for a chain, off the final principal of the metadata walk). A value type is read as
    /// nullable so an absent principal is a default/false rather than an exception; the projection A7
    /// guard rejects an unlifted non-nullable scalar with the same path/result-type diagnostic as SQL.
    /// The receiver's populated navigation graph is never read.
    /// </summary>
    private bool TryLowerReferenceScalar(MemberExpression node, out Expression result)
    {
        result = null!;

        if (!IsInMemory)
            return false;

        if (!TryResolveReferenceAccess(node, out var path, out var trailing))
            return false;

        var finalHop = path.FinalHop
            ?? throw new NotSupportedException("A reference navigation path without a final hop is not supported by the in-memory provider.");

        Expression projected = Expression.Parameter(finalHop.RelatedType, "navigation");
        for (var i = 0; i < trailing.Count; i++)
            projected = Expression.Property(projected, trailing[i]);

        var scalarType = projected.Type;

        if (ProjectionMode
            && _compensated == 0
            && scalarType.IsValueType
            && Nullable.GetUnderlyingType(scalarType) is null)
        {
            var member = trailing.Count > 0 ? trailing[^1].Name : finalHop.Navigation.Name;
            throw new QueryPreparationException(
                $"The navigation scalar '{NavigationExpansion.DisplayPath(path.Hops)}.{member}' is projected as the non-nullable type " +
                $"'{scalarType}', but an absent principal yields SQL NULL, which cannot be stored in that type. " +
                $"Project it as a nullable value (for example '(int?)navigation.{member}') or provide an explicit " +
                $"default (for example 'navigation.{member} ?? 0').");
        }

        var resultType = scalarType.IsValueType && Nullable.GetUnderlyingType(scalarType) is null
            ? typeof(Nullable<>).MakeGenericType(scalarType)
            : scalarType;

        // #148-B R2.3: a multi-hop reference chain cannot be expressed as a single nested correlated
        // subquery in the in-memory provider; walk the resolved hops against the registered datasets
        // instead (absence propagates hop by hop, the CLR graph is never read).
        if (path.Hops.Count > 1)
        {
            result = LowerMultiHopReference(path, trailing, resultType);
            return true;
        }

        var (sourceType, condition) = BuildReferenceCorrelation(path);
        var navigation = condition.Parameters[0];

        // A whole reference projects the principal entity itself; a scalar read projects the trailing
        // members off it. In both cases the command's own matcher binds the FK/PK metadata, not the
        // receiver's navigation property.
        Expression body = navigation;
        for (var i = 0; i < trailing.Count; i++)
            body = Expression.Property(body, trailing[i]);

        if (resultType != body.Type)
            body = Expression.Convert(body, resultType);

        var projection = Expression.Lambda(body, navigation);
        var cmd = ((IDataContext)_dataProvider).CreateCommand(resultType, new QueryDefinition
        {
            Exp = projection,
            SrcType = sourceType,
            Condition = condition,
            Logger = _logger,
        });

        result = RegisterScalarTyped(cmd, resultType);
        return true;
    }

    /// <summary>
    /// #148-B R2.3: lowers a resolved multi-hop reference chain to an in-memory navigation-chain plan.
    /// Every hop carries the mapped key pair; the first hop's outer key is registered as an outer
    /// reference, and the evaluator walks the registered datasets at execution time. A chain that is not
    /// a sequence of declared single-column reference hops is rejected (fail closed).
    /// </summary>
    private Expression LowerMultiHopReference(ResolvedNavigationPath path, List<PropertyInfo> trailing, Type resultType)
    {
        var command = BuildNavigationChainCommand(path, trailing, resultType);
        return RegisterScalarTyped(command, resultType);
    }

    /// <summary>
    /// #148-B R2.3/D-R3-4: builds the in-memory navigation-chain command for a resolved reference chain
    /// (at least two hops). The optional <paramref name="trailing"/> members are read off the final
    /// principal by the chain selector; an empty list yields the whole principal (used by the multi-hop
    /// presence check). The chain yields zero rows when any hop is absent.
    /// </summary>
    private QueryCommand BuildNavigationChainCommand(ResolvedNavigationPath path, IReadOnlyList<PropertyInfo> trailing, Type resultType)
    {
        if (path.RootBinding.Anchor is not ParameterExpression outerParam)
            throw new NotSupportedException("The reference navigation must be rooted at a query source parameter.");

        var hops = path.Hops;
        var chainHops = new InMemoryNavigationChainHop[hops.Count];

        for (var i = 0; i < hops.Count; i++)
        {
            var hop = hops[i];
            if (hop.IsCollection
                || hop.IsManyToMany
                || hop.Direction is not (NavigationDirection.DependentToPrincipal or NavigationDirection.PrincipalToDependent))
            {
                throw new NotSupportedException(
                    $"The navigation '{hop.DeclaringType.Name}.{hop.Navigation.Name}' is not a declared single-column reference hop; " +
                    "the in-memory provider supports multi-hop chains of reference navigations only.");
            }

            chainHops[i] = new InMemoryNavigationChainHop
            {
                EntityType = hop.RelatedType,
                MatchKey = ResolveHopMatchKey(hop).PropertyInfo,
                NextOuterKey = i < hops.Count - 1 ? ResolveHopOuterKey(hops[i + 1]).PropertyInfo : null,
            };
        }

        var firstOuterKey = ResolveHopOuterKey(hops[0]).PropertyInfo;
        var outerReferenceIndex = _queryProvider!.AddOuterReference(Expression.Property(outerParam, firstOuterKey));

        var selectorParameter = Expression.Parameter(typeof(object), "navigation");
        Expression selectorBody = Expression.Convert(selectorParameter, hops[^1].RelatedType);
        for (var i = 0; i < trailing.Count; i++)
            selectorBody = Expression.Property(selectorBody, trailing[i]);

        var selector = Expression.Lambda<Func<object, object?>>(
            Expression.Convert(selectorBody, typeof(object)), selectorParameter).Compile();

        var command = ((IDataContext)_dataProvider).CreateCommand(resultType, new QueryDefinition
        {
            SrcType = hops[^1].RelatedType,
            Logger = _logger,
        });
        command.IgnoreColumns = true;
        command.NavigationChain = new InMemoryNavigationChain
        {
            OuterReferenceIndex = outerReferenceIndex,
            Hops = chainHops,
            Selector = selector,
        };

        return command;
    }

    /// <summary>The key on the previous entity that feeds this hop (the dependent FK or principal PK).</summary>
    private static IPropertyMetadata ResolveHopOuterKey(ResolvedNavigationHop hop)
        => hop.Direction == NavigationDirection.DependentToPrincipal
            ? hop.PrimaryLeg.ForeignKey
            : hop.PrimaryLeg.PrincipalKey;

    /// <summary>The key on this hop's entity matched against the previous key.</summary>
    private static IPropertyMetadata ResolveHopMatchKey(ResolvedNavigationHop hop)
        => hop.Direction == NavigationDirection.DependentToPrincipal
            ? hop.PrimaryLeg.PrincipalKey
            : hop.PrimaryLeg.ForeignKey;

    /// <summary>
    /// Builds the correlated existence test for a declared reference navigation presence check. A
    /// single-hop reference uses the FK/PK correlation; a multi-hop chain (D-R3-4) walks the registered
    /// datasets and yields no row when any hop is absent, so <c>== null</c>/<c>!= null</c> stay mutually
    /// consistent and an absent first, middle or final hop all read as absent.
    /// </summary>
    private Expression BuildReferenceExists(ResolvedNavigationPath path)
    {
        QueryCommand cmd;
        if (path.Hops.Count > 1)
        {
            cmd = BuildNavigationChainCommand(path, [], path.FinalHop!.RelatedType);
        }
        else
        {
            var (sourceType, condition) = BuildReferenceCorrelation(path);
            cmd = ((IDataContext)_dataProvider).CreateCommand(sourceType, new QueryDefinition
            {
                SrcType = sourceType,
                Condition = condition,
                Logger = _logger,
            });
            cmd.IgnoreColumns = true;
        }

        PrepareAndRegister(cmd, out var idx);
        _registryParameter ??= Expression.Parameter(typeof(IQueryRegistry));

        return Expression.Call(
            Expression.Property(null, SQLPI),
            CommonFunctions.ExistsMI,
            Expression.Property(
                Expression.Property(_registryParameter, ReferencedQueriesPI),
                ItemPI,
                Expression.Constant(idx)));
    }

    /// <summary>
    /// Builds the correlated source type and predicate for a declared reference path: correlates the
    /// outer dependent foreign key to the inner principal key (many-to-one) or the outer principal key
    /// to the inner dependent foreign key (one-to-one declared from the principal). Only a direct
    /// single-hop reference is supported.
    /// </summary>
    private (Type SourceType, LambdaExpression Condition) BuildReferenceCorrelation(ResolvedNavigationPath path)
    {
        var hop = path.FinalHop
            ?? throw new NotSupportedException("A reference navigation path without a final hop is not supported.");

        if (hop.IsCollection || path.Hops.Count != 1)
            throw new NotSupportedException(
                "Only a direct declared reference navigation (a single hop from the query source) is supported by the in-memory provider.");

        if (path.RootBinding.Anchor is not ParameterExpression outerParam)
            throw new NotSupportedException("The reference navigation must be rooted at a query source parameter.");

        var navigation = Expression.Parameter(hop.RelatedType, "navigation");

        Expression outerKey;
        Expression innerKey;

        if (hop.Direction == NavigationDirection.DependentToPrincipal)
        {
            outerKey = Expression.Property(outerParam, hop.PrimaryLeg.ForeignKey.PropertyInfo);
            innerKey = Expression.Property(navigation, hop.PrimaryLeg.PrincipalKey.PropertyInfo);
        }
        else if (hop.Direction == NavigationDirection.PrincipalToDependent)
        {
            outerKey = Expression.Property(outerParam, hop.PrimaryLeg.PrincipalKey.PropertyInfo);
            innerKey = Expression.Property(navigation, hop.PrimaryLeg.ForeignKey.PropertyInfo);
        }
        else
        {
            throw new NotSupportedException(
                $"The reference navigation direction '{hop.Direction}' is not supported by the in-memory provider.");
        }

        var index = _queryProvider!.AddOuterReference(outerKey);
        var markerConstructor = ReplaceConstantsExpressionVisitor.GetOuterRefMarkerCI(outerKey.Type);
        var marker = Expression.Property(
            Expression.New(markerConstructor, Expression.Constant(index)),
            nameof(OuterRefMarker<int>.Ref));

        var (left, right) = AlignNavigationKeys(innerKey, marker);
        return (hop.RelatedType, Expression.Lambda(Expression.Equal(left, right), navigation));
    }

    /// <summary>
    /// Registers a prepared correlated scalar and returns a typed placeholder the in-memory correlated
    /// rewriter replaces per outer row. Unlike the registry-lambda form the SQL renderer consumes, this
    /// expression carries the scalar CLR type so it can take part in a comparison, <c>??</c> or
    /// arithmetic consumer.
    /// </summary>
    private Expression RegisterScalarTyped(QueryCommand cmd, Type resultType)
    {
        PrepareAndRegister(cmd, out var idx);
        _registryParameter ??= Expression.Parameter(typeof(IQueryRegistry));

        var queryValue = Expression.Property(
            Expression.Property(_registryParameter, ReferencedQueriesPI),
            ItemPI,
            Expression.Constant(idx));

        // Route through object so the placeholder works for both a value type (unboxing) and a
        // reference type (an arbitrary explicit reference conversion the direct Convert would refuse).
        return Expression.Convert(Expression.Convert(queryValue, typeof(object)), resultType);
    }

    /// <summary>True when <paramref name="expression"/> reads a scalar member through a reference navigation.</summary>
    private bool IsReferenceScalarValueAccess(Expression expression)
        => IsInMemory
           && TryResolveReferenceAccess(expression, out _, out var trailing)
           && trailing.Count > 0;

    /// <summary>
    /// Visits a comparison that involves a reference value scalar (already lifted to nullable) and
    /// aligns the other operand to the nullable type so a lifted comparison can be built.
    /// </summary>
    private Expression VisitAlignedReferenceComparison(BinaryExpression node)
    {
        var left = Visit(node.Left)!;
        var right = Visit(node.Right)!;

        if (left.Type != right.Type)
        {
            if (Nullable.GetUnderlyingType(left.Type) == right.Type)
                right = Expression.Convert(right, left.Type);
            else if (Nullable.GetUnderlyingType(right.Type) == left.Type)
                left = Expression.Convert(left, right.Type);
        }

        return Expression.MakeBinary(node.NodeType, left, right);
    }

    /// <summary>
    /// Recognises a null comparison (<c>== null</c> / <c>!= null</c>) whose other operand is a pure
    /// declared reference navigation rooted at a current outer parameter.
    /// </summary>
    private bool TryGetReferenceNullComparison(BinaryExpression node, out ResolvedNavigationPath path)
    {
        path = null!;

        var leftIsNull = node.Left is ConstantExpression { Value: null };
        var rightIsNull = node.Right is ConstantExpression { Value: null };
        if (leftIsNull == rightIsNull)
            return false;

        var operand = leftIsNull ? node.Right : node.Left;

        return TryResolveReferenceAccess(operand, out path, out var trailing)
            && trailing.Count == 0
            && path.FinalHop is { IsCollection: false };
    }

    /// <summary>
    /// Resolves <paramref name="expression"/> as a member chain rooted at one of the currently pushed
    /// outer parameters whose leading members are declared reference navigations. Returns the resolved
    /// navigation prefix and the trailing scalar members applied to the related principal.
    /// </summary>
    private bool TryResolveReferenceAccess(Expression expression, out ResolvedNavigationPath path, out List<PropertyInfo> trailing)
    {
        path = null!;
        trailing = null!;

        if (_outerParams is not { Count: > 0 })
            return false;

        var current = TypeFacts.UnwrapConvert(expression);
        if (current is not MemberExpression)
            return false;

        var members = new List<PropertyInfo>(4);
        while (current is MemberExpression { Expression: { } inner } member)
        {
            if (member.Member is not PropertyInfo property)
                return false;

            members.Add(property);
            current = inner;
        }

        if (current is not ParameterExpression root)
            return false;

        var bound = false;
        foreach (var parameter in _outerParams)
        {
            if (ReferenceEquals(parameter, root))
            {
                bound = true;
                break;
            }
        }

        if (!bound)
            return false;

        members.Reverse();

        for (var k = members.Count; k >= 1; k--)
        {
            Expression prefix = root;
            for (var i = 0; i < k; i++)
                prefix = Expression.Property(prefix, members[i]);

            if (NavigationExpansion.TryResolvePath(prefix, root, _queryProvider!, out var resolved)
                && resolved.Hops.Count == k
                && resolved.FinalHop is { IsCollection: false })
            {
                path = resolved;
                EnsureRegisteredSource(resolved);
                trailing = members.GetRange(k, members.Count - k);
                return true;
            }
        }

        return false;
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

        var scalar = PrepareAndRegister(cmd, out var idx);

        if (!methodName.StartsWith("Any", StringComparison.Ordinal))
            return scalar;

        var p = Expression.Parameter(typeof(IQueryRegistry));
        return Expression.Lambda(
            Expression.Call(Expression.Property(null, SQLPI), CommonFunctions.ExistsMI,
                Expression.Property(Expression.Property(p, ReferencedQueriesPI), ItemPI, Expression.Constant(idx))),
            p);
    }

    /// <summary>
    /// Prepares and registers <paramref name="cmd"/>, returning the scalar-subquery placeholder: a
    /// lambda over the shared <see cref="IQueryRegistry"/> whose body indexes the registered command.
    /// Shared by the method terminals and the navigation <c>Count</c> property terminal.
    /// </summary>
    private LambdaExpression PrepareAndRegister(QueryCommand cmd, out int idx)
    {
        cmd.OuterRegistry ??= _queryProvider;

        if (!cmd.IsPrepared)
            cmd.PrepareCommand(false, _cancellationToken);

        idx = _queryProvider!.AddCommand(cmd);

        var p = Expression.Parameter(typeof(IQueryRegistry));
        return Expression.Lambda(Expression.Property(Expression.Property(p, ReferencedQueriesPI), ItemPI, Expression.Constant(idx)), p);
    }

    /// <summary>Prepares, registers and returns the typed scalar-subquery placeholder for <paramref name="cmd"/>.</summary>
    /// <remarks>
    /// #148-B R2.4: the placeholder is typed as the command's result type (through <c>object</c>) rather
    /// than a <c>Func&lt;IQueryRegistry, QueryCommand&gt;</c> lambda, so a navigation count can compose
    /// with predicate, boolean and arithmetic consumers (a lambda has no numeric operator). The SQL
    /// renderer still recognises the registry-index body and emits the correlated scalar subquery; the
    /// two conversions are walked, not emitted. The projection path stores the declared column type on
    /// the <see cref="SelectExpression"/> separately, so it no longer needs the lambda shape.
    /// </remarks>
    private Expression ReplaceScalarQueryCommand(QueryCommand cmd)
    {
        cmd.OuterRegistry ??= _queryProvider;

        if (!cmd.IsPrepared)
            cmd.PrepareCommand(false, _cancellationToken);

        var idx = _queryProvider!.AddCommand(cmd);

        _registryParameter ??= Expression.Parameter(typeof(IQueryRegistry));
        var queryValue = Expression.Property(
            Expression.Property(_registryParameter, ReferencedQueriesPI),
            ItemPI,
            Expression.Constant(idx));

        // Route through object: a QueryCommand has no direct coercion to the numeric result type, and
        // the reference-then-unbox shape is what the SQL renderer walks to the registry index.
        var resultType = cmd.ResultType ?? typeof(object);
        return Expression.Convert(Expression.Convert(queryValue, typeof(object)), resultType);
    }

    /// <summary>
    /// Converts a boolean subquery placeholder (a lambda over <see cref="IQueryRegistry"/>) into the
    /// equivalent predicate bound to this visitor's shared registry parameter, so the subquery can be
    /// combined with logical operators (<c>&amp;&amp;</c>, <c>||</c>, <c>!</c>) instead of failing when
    /// <c>Expression.MakeBinary</c> sees a <c>Func</c> operand. A
    /// boolean subquery is the only shape that can appear inside a logical operator, so every other
    /// expression passes through unchanged.
    /// </summary>
    private Expression NormalizePredicateOperand(Expression expression)
    {
        if (expression is LambdaExpression { Parameters: [ParameterExpression parameter], ReturnType: var returnType } lambda
            && parameter.Type == typeof(IQueryRegistry)
            && returnType == typeof(bool))
        {
            _registryParameter ??= Expression.Parameter(typeof(IQueryRegistry));
            return new ReplaceParameterExpressionVisitor(_registryParameter).Visit(lambda.Body);
        }

        return expression;
    }

    /// <inheritdoc/>
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
                    // A predicate lambda whose whole body is a correlated subquery (`Where(exists(...))`)
                    // is normalised to a real boolean predicate as well, so the in-memory condition path
                    // binds the subquery instead of ignoring the condition.
                    return Expression.Lambda(NormalizePredicateOperand(Visit(lambdaExpression.Body)!), exp);
                }
                finally
                {
                    _outerParams.Pop();
                }
            }
        }

        return base.VisitLambda(node);
    }
    /// <inheritdoc/>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        Expression? leftNode = null;
        Expression? rightNode = null;

        // #148-B D6/A4: in memory, `reference == null` / `reference != null` is a metadata-based
        // correlated existence check on the principal key, so a dangling foreign key reads as absent
        // exactly like the SQL LEFT JOIN.
        if (IsInMemory
            && node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
            && TryGetReferenceNullComparison(node, out var presencePath))
        {
            var exists = BuildReferenceExists(presencePath);
            return node.NodeType == ExpressionType.Equal ? Expression.Not(exists) : exists;
        }

        // A reference value scalar is read as nullable. Align the other operand to that nullable type
        // before the comparison; otherwise MakeBinary(GreaterThan/Equal, int?, int) is undefined.
        if (IsInMemory
            && node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
                or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
                or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            && (IsReferenceScalarValueAccess(node.Left) || IsReferenceScalarValueAccess(node.Right)))
        {
            return VisitAlignedReferenceComparison(node);
        }

        // A `??` on the left compensates the missing-principal NULL, so an unlifted non-nullable value
        // scalar underneath it is not rejected by the A7 guard.
        if (node.NodeType == ExpressionType.Coalesce && IsInMemory)
        {
            _compensated++;
            var coalesceLeft = Visit(node.Left)!;
            _compensated--;

            var coalesceRight = Visit(node.Right)!;
            if (!ReferenceEquals(coalesceLeft, node.Left) || !ReferenceEquals(coalesceRight, node.Right))
                return Expression.Coalesce(coalesceLeft, coalesceRight, node.Conversion);

            return node;
        }

        if (node.Left.Type == node.Right.Type && node.Left.Type == typeof(string) && node.NodeType == ExpressionType.Add)
        {
            leftNode = Visit(node.Left);
            rightNode = Visit(node.Right);
            return Expression.Call(ConcatMI, leftNode, rightNode);
        }

        if (node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            // A correlated EXISTS/IN is rewritten to a lambda over the registry, which cannot take
            // part in a logical operator until it is bound back to a boolean predicate. Normalise both
            // operands first so `exists(...) || predicate` and `predicate && exists(...)` rebuild.
            var logicalLeft = NormalizePredicateOperand(Visit(node.Left)!);
            var logicalRight = NormalizePredicateOperand(Visit(node.Right)!);
            return Expression.MakeBinary(node.NodeType, logicalLeft, logicalRight);
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
    /// <summary>
    /// Visits a conversion, preserving a lambda whose body is a <c>QueryCommand</c> (a subquery)
    /// instead of wrapping it in a new conversion node.
    /// </summary>
    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            var operand = NormalizePredicateOperand(Visit(node.Operand)!);
            if (operand.Type == typeof(bool))
                return Expression.Not(operand);

            return Expression.MakeUnary(node.NodeType, operand, node.Type, node.Method);
        }

        if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
        {
            // A nullable lift is the explicit compensation for a reference navigation read from a
            // possibly-absent principal; record it while its operand is visited (A7).
            var compensated = IsInMemory && Nullable.GetUnderlyingType(node.Type) is not null;
            if (compensated)
                _compensated++;

            var r = Visit(node.Operand);

            if (compensated)
                _compensated--;

            if (r is LambdaExpression lambda && lambda.Body.Type.IsAssignableTo(typeof(QueryCommand)))
                return r;

            return Expression.MakeUnary(node.NodeType, r!, node.Type, node.Method);
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