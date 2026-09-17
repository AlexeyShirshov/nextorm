using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace nextorm.core;
public class BaseExpressionVisitor : ExpressionVisitor, ICloneable, IDisposable
{
    private readonly Type _entityType;
    private readonly ISqlDialect _dialect;
    private readonly IColumnsProvider _columnsProvider;
    private readonly int _dim;
    protected readonly StringBuilder? _builder;
    private readonly ObjectPool<StringBuilder> _sbPool;
    private readonly List<Param> _params;
    private readonly ILogger? _logger;
    private bool _needAliasForColumn;
    private string? _colName;
    private readonly IAliasProvider? _aliasProvider;
    private readonly IParamProvider _paramProvider;
    private readonly IQueryProvider _queryProvider;
    private readonly bool _dontNeedAlias;
    protected readonly bool _paramMode;
    private bool _disposedValue;
    //private readonly Stack<(IColumnsProvider, ReadOnlyCollection<ParameterExpression>)> _scope;

    public BaseExpressionVisitor(Type entityType, ISqlDialect dialect, IColumnsProvider columnsProvider, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, bool dontNeedAlias, bool paramMode, List<Param> @params, ILogger? logger, ObjectPool<StringBuilder>? sbPool = null)
    {
        _entityType = entityType;
        _dialect = dialect;
        _columnsProvider = columnsProvider;
        _dim = dim;
        _aliasProvider = aliasProvider;
        _paramProvider = paramProvider;
        _queryProvider = queryProvider;
        _dontNeedAlias = dontNeedAlias;
        _paramMode = paramMode;
        _sbPool = sbPool ?? StringBuilderPool.Shared;
        _builder = paramMode ? null : _sbPool.Get();
        // _scope = paramScope;
        _params = @params;
        _logger = logger;
    }
    public bool NeedAliasForColumn => _needAliasForColumn;
    public IColumnsProvider SourceProvider => _columnsProvider;
    public string? ColumnName => _colName;
    // protected override Expression VisitLambda<T>(Expression<T> node)
    // {
    //     _scope.Push((_tableProvider, node.Parameters));
    //     try
    //     {
    //         return base.VisitLambda(node);
    //     }
    //     finally
    //     {
    //         _scope.Pop();
    //     }
    // }
    /// <summary>
    /// True when the built expression is used as a condition (WHERE/HAVING) instead of a value.
    /// </summary>
    protected virtual bool AsPredicate => false;
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (!_paramMode
            && node.Object is null
            && node.Method.DeclaringType == typeof(Convert)            && node.Arguments is [Expression convertArg]
            && TryGetNumericConversion(convertArg.Type, node.Method.ReturnType, out var convertTarget))
        {
            // Convert.ToXxx is a method call rather than a Convert node, so without this the
            // conversion was silently dropped.
            _needAliasForColumn = true;

            _builder!.Append("cast(");
            Visit(convertArg);
            _builder!.Append(" as ").Append(_dialect.MakeTypeName(convertTarget)).Append(')');

            return node;
        }

        if (TryTranslateFunction(node))
            return node;

        if (TryTranslateSqlFunction(node))
            return node;

        if (TryTranslateWindowFunction(node))
            return node;

        if (TryTranslateCollectionContains(node))
            return node;

        if (node.Object?.Type == typeof(TableAlias))
        {
            // switch (node.Method.Name)
            // {
            //     case "Long":
            //         if (node.Arguments[0] is ConstantExpression constExp)
            //             _builder.Append(constExp.Value?.ToString());

            //         break;
            // }
            if (!_paramMode)
            {
                if (_columnsProvider.HasAliases && !_dontNeedAlias)
                {
                    var v = new TypeExpressionVisitor<ParameterExpression>();
                    v.Visit(node.Object);
                    if (v.Has)
                    {
                        var tableAliasForColumn = GetAliasFromParam(v.Target!, false);

                        if (!string.IsNullOrEmpty(tableAliasForColumn))
                        {
                            _builder!.Append(tableAliasForColumn).Append('.');
                        }
                    }
                }

                _builder!.Append(node switch
                {
                    {
                        Method.Name:
                            nameof(TableAlias.Long)
                            or nameof(TableAlias.Int)
                            or nameof(TableAlias.Boolean)
                            or nameof(TableAlias.Byte)
                            or nameof(TableAlias.DateTime)
                            or nameof(TableAlias.Decimal)
                            or nameof(TableAlias.Double)
                            or nameof(TableAlias.Float)
                            or nameof(TableAlias.Guid)
                            or nameof(TableAlias.NullableLong)
                            or nameof(TableAlias.NullableInt)
                            or nameof(TableAlias.NullableBoolean)
                            or nameof(TableAlias.NullableByte)
                            or nameof(TableAlias.NullableDateTime)
                            or nameof(TableAlias.NullableDecimal)
                            or nameof(TableAlias.NullableDouble)
                            or nameof(TableAlias.NullableFloat)
                            or nameof(TableAlias.NullableGuid)
                            or nameof(TableAlias.Column)
                            or "get_Item",
                        Arguments: [ConstantExpression constExp]
                    } => constExp.Value?.ToString(),
                    {
                        Method.Name: nameof(TableAlias.Column),
                        Arguments: [Expression exp]
                    } => CompileExp(exp),
                    _ => throw new NotSupportedException(node.Method.Name)
                });
            }
            return node;
        }
        else if (node.Object?.Type == typeof(TableColumn))
        {
            throw new NotImplementedException();
        }
        else if (node.Method.DeclaringType == typeof(NORM) /*&& _tableProvider is IParamProvider paramProvider*/)
        {
            var paramIdx = node switch
            {
                {
                    Method.Name: nameof(NORM.Param),
                    Arguments: [ConstantExpression constExp]
                } => constExp.Value is int i ? i : -1,
                _ => -1
            };

            if (paramIdx >= 0)
            {
                // Reuse the cached norm_pN table instead of string.Format (same names, no boxing
                // and no composite-formatting pass on the SQL-build path).
                var paramName = NormParam.GetName(paramIdx);
                _params.Add(new Param(paramName, null));
                if (!_paramMode)
                    _builder!.Append(_dialect.MakeParam(paramName));

                return node;
            }
            else
                throw new NotSupportedException(node.Method.Name);
        }
        else if (node.Method.DeclaringType == typeof(NORM.NORM_SQL) /*&& _tableProvider is IParamProvider paramProvider*/)
        {
            // A window function is only valid once it has been given a specification; emitting the bare
            // call would produce invalid SQL, so fail with an actionable message instead.
            if (MapWindowFunctionName(node.Method.Name) is { } windowFunction)
                throw new NotSupportedException($"The window function {windowFunction} must be completed with Over(...).");

            if ((node.Method.Name == nameof(NORM.NORM_SQL.exists)
                || node.Method.Name == nameof(NORM.NORM_SQL.all)
                || node.Method.Name == nameof(NORM.NORM_SQL.any)
                )
                && node.Arguments is [Expression exp] && exp.Type.IsAssignableTo(typeof(QueryCommand)))
            {
                QueryCommand innerQuery;
                var expVisitor = new TwoTypeExpressionVisitor<ParameterExpression, ConstantExpression>();
                expVisitor.Visit(exp);

                var predicateKeyword = node.Method.Name switch
                {
                    nameof(NORM.NORM_SQL.exists) => "exists",
                    nameof(NORM.NORM_SQL.all) => "all",
                    nameof(NORM.NORM_SQL.any) => "any",
                    _ => throw new NotImplementedException()
                };

                var keyCmd = new ExpressionKey(exp, _queryProvider);
                if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                {
                    object? paramValue;
                    Expression body;
                    ParameterExpression pExp;
                    if (expVisitor.Has1)
                    {
                        pExp = Expression.Parameter(typeof(object));
                        var replParam = new ReplaceParameterVisitor(Expression.Convert(pExp, typeof(IQueryProvider)));
                        body = replParam.Visit(exp);
                        paramValue = _queryProvider;
                    }
                    else if (expVisitor.Has2)
                    {
                        pExp = Expression.Parameter(typeof(object));
                        var ce = expVisitor.Target2;
                        var replace = new ReplaceConstantVisitor(Expression.Convert(pExp, ce!.Type));
                        paramValue = ce.Value;
                        body = replace.Visit(exp);
                    }
                    else
                        throw new InvalidOperationException();

                    var d = Expression.Lambda<Func<object?, object>>(body, pExp).Compile();
                    DataContextCache.ExpressionsCache[keyCmd] = d;
                    innerQuery = (QueryCommand)d(paramValue);

                    if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                    {
                        _logger.LogTrace("Expression cache miss on visit exists. hashcode: {hash}, value: {value}", keyCmd.GetHashCode(), d(paramValue));
                    }
                    else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Expression cache miss on visit exists");
                }
                else
                {
                    object? paramValue;

                    if (expVisitor.Has1)
                    {
                        paramValue = _queryProvider;
                    }
                    else if (expVisitor.Has2)
                    {
                        var ce = expVisitor.Target2;
                        paramValue = ce!.Value;
                    }
                    else
                        throw new InvalidOperationException();

                    innerQuery = (QueryCommand)((Func<object?, object>)dCmd)(paramValue);
                }

                var sqlBuilder = new SqlBuilder(_dialect, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
                var sql = sqlBuilder.MakeSelect(innerQuery);

                if (!_paramMode)
                {
                    _builder!.Append(_dialect.MakeSubqueryPredicate(predicateKeyword, sql!, AsPredicate));
                }

                return node;
            }
            else if (node.Method.Name == nameof(NORM.NORM_SQL.@in)
                && node.Arguments is [Expression parExp, Expression cmdExp] && cmdExp.Type.IsAssignableTo(typeof(QueryCommand)))
            {
                if (!_paramMode)
                    Visit(parExp);

                if (!_paramMode) _builder!.Append(" in (");

                var constRepl = new ReplaceConstantsExpressionVisitor(_queryProvider);
                var body = constRepl.Visit(cmdExp);

                QueryCommand innerQuery;

                if (constRepl.Params.Count > 0)
                {
                    var keyCmd = new ExpressionKey(cmdExp, _queryProvider);
                    if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                    {
                        var d = Expression.Lambda(body, constRepl.Params.Select(it => it.Item1)).Compile();

                        DataContextCache.ExpressionsCache[keyCmd] = d;
                        innerQuery = (QueryCommand)d.DynamicInvoke(constRepl.Params.Select(it => it.Item2).ToArray())!;

                        if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                        {
                            _logger.LogTrace("Subquery expression miss: {exp}", cmdExp);
                        }
                        else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Subquery expression miss");
                    }
                    else
                        innerQuery = (QueryCommand)dCmd.DynamicInvoke(constRepl.Params.Select(it => it.Item2).ToArray())!;

                }
                else
                    throw new InvalidOperationException();

                var sqlBuilder = new SqlBuilder(_dialect, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
                var sql = sqlBuilder.MakeSelect(innerQuery);

                if (!_paramMode)
                {
                    _builder!.Append(sql).Append(')');
                }

                return node;
            }
            else if (node.Method.Name == nameof(NORM.NORM_SQL.@in)
                && node.Arguments is [Expression inColumnExp, Expression inValuesExp]
                && !inValuesExp.Type.IsAssignableTo(typeof(QueryCommand)))
            {
                TranslateInValues(inColumnExp, inValuesExp, node.Method.GetGenericArguments()[0]);
                return node;
            }
            else if (node.Method.Name == nameof(NORM.NORM_SQL.count)
                || node.Method.Name == nameof(NORM.NORM_SQL.count_distinct)
                || node.Method.Name == nameof(NORM.NORM_SQL.count_big)
                || node.Method.Name == nameof(NORM.NORM_SQL.count_big_distinct))
            {
                if (!_paramMode) _builder!.Append(_dialect.MakeCount(node.Method.Name.EndsWith("distinct", StringComparison.Ordinal), node.Method.Name.Contains("big", StringComparison.Ordinal)));

                if (node.Arguments is [NewArrayExpression newArray])//ReadOnlyCollection<Expression> args
                {
                    var items = newArray.Expressions;
                    // count() is only valid in SQLite; every other provider (and the SQL standard)
                    // requires count(*).
                    if (items.Count == 0)
                    {
                        if (!_paramMode) _builder!.Append('*');
                    }
                    else
                    {
                        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
                        {
                            var argExp = items[i];
                            using var visitor = new BaseExpressionVisitor(_entityType, _dialect, _columnsProvider, 0, _aliasProvider, _paramProvider, _queryProvider, _dontNeedAlias, _paramMode, _params, _logger, _sbPool);
                            visitor.Visit(argExp);
                            if (!_paramMode) _builder!.Append(visitor.ToString()).Append(", ");
                        }
                        if (!_paramMode) _builder!.Length -= 2;
                    }
                }

                if (!_paramMode) _builder!.Append(')');

                return node;
            }
            else if (node.Method.Name == nameof(NORM.NORM_SQL.min)
                || node.Method.Name == nameof(NORM.NORM_SQL.max))
            {
                if (!_paramMode) _builder!.Append(node.Method.Name).Append('(');

                var args = node.Arguments;
                for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                {
                    var argExp = args[i];
                    using var visitor = new BaseExpressionVisitor(_entityType, _dialect, _columnsProvider, 0, null, _paramProvider, _queryProvider, _dontNeedAlias, _paramMode, _params, _logger, _sbPool);
                    visitor.Visit(argExp);
                    if (!_paramMode) _builder!.Append(visitor.ToString()).Append(", ");
                }

                if (!_paramMode)
                {
                    _builder!.Length -= 2;
                    _builder!.Append(')');
                }

                return node;
            }
            else if (node.Method.Name == nameof(NORM.NORM_SQL.avg)
                || node.Method.Name == nameof(NORM.NORM_SQL.sum)
                || node.Method.Name == nameof(NORM.NORM_SQL.stdev)
                || node.Method.Name == nameof(NORM.NORM_SQL.stdevp)
                || node.Method.Name == nameof(NORM.NORM_SQL.var)
                || node.Method.Name == nameof(NORM.NORM_SQL.varp)
                || node.Method.Name == nameof(NORM.NORM_SQL.avg_distinct)
                || node.Method.Name == nameof(NORM.NORM_SQL.sum_distinct)
                || node.Method.Name == nameof(NORM.NORM_SQL.stdev_distinct)
                || node.Method.Name == nameof(NORM.NORM_SQL.stdevp_distinct)
                || node.Method.Name == nameof(NORM.NORM_SQL.var_distinct)
                || node.Method.Name == nameof(NORM.NORM_SQL.varp_distinct)
                )
            {
                if (!_paramMode)
                {
                    _builder!.Append(_dialect.MakeAggregate(node.Method.Name.Replace("_distinct", string.Empty))).Append('(');
                    if (node.Method.Name.EndsWith("distinct", StringComparison.Ordinal))
                        _builder!.Append("distinct ");
                }

                var args = node.Arguments;
                for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                {
                    var argExp = args[i];
                    using var visitor = new BaseExpressionVisitor(_entityType, _dialect, _columnsProvider, 0, null, _paramProvider, _queryProvider, _dontNeedAlias, _paramMode, _params, _logger, _sbPool);
                    visitor.Visit(argExp);
                    if (!_paramMode) _builder!.Append(visitor.ToString()).Append(", ");
                }

                if (!_paramMode)
                {
                    _builder!.Length -= 2;
                    _builder!.Append(')');
                }

                return node;
            }

            throw new NotImplementedException();
        }
        else if (node.Object?.Type.IsAssignableTo(typeof(QueryCommand)) ?? false)
        {

        }
        else if (node.Type == typeof(string))
        {
            if (node.Object is null)
            {
                if (node.Method.Name == nameof(string.Concat))
                {
                    // A concatenation is a computed column: it must be aliased when selected so that
                    // an outer query can reference it instead of re-expanding (and losing) the inputs.
                    _needAliasForColumn = true;

                    if (!_paramMode)
                        _builder!.Append('(');

                    var args = node.Arguments;
                    for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                    {
                        var arg = args[i];
                        Visit(arg);
                        _builder!.Append(_dialect.ConcatStringOperator);
                    }

                    if (!_paramMode)
                    {
                        _builder!.Length -= _dialect.ConcatStringOperator.Length;
                        _builder!.Append(')');
                    }

                    return node;
                }
            }
            throw new NotImplementedException();
        }
        else if (!node.Has<ParameterExpression>())
        {
            object? value = null;
            var keyCmd = new ExpressionKey(node, _queryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var d))
            {
                var del = Expression.Lambda<Func<object>>(node).Compile();
                DataContextCache.ExpressionsCache[keyCmd] = del;
                value = del();

                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    _logger.LogTrace("Expression cache miss on visit method call. hashcode: {hash}, value: {value}", keyCmd.GetHashCode(), del());
                }
                else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Expression cache miss on visit method call");
            }
            else
                value = ((Func<object>)d)();

            var paramName = _paramProvider.GetParamName();
            var p = new Param(paramName, value);
            _params.Add(p);

            if (!_paramMode)
                _builder!.Append(_dialect.MakeParam(paramName));

            return node;
        }
        else if (node.Object?.Type.IsAssignableFrom(typeof(QueryCommand)) ?? false)
        {
            throw new NotImplementedException();
        }

        // A string/math method that was not recognised above must not silently produce no SQL: when
        // the call cannot be folded to a parameter (it depends on a column) it is a clear error.
        if (node.Has<ParameterExpression>()
            && (node.Method.DeclaringType == typeof(string) || node.Method.DeclaringType == typeof(Math)))
            throw new NotSupportedException($"The method {node.Method.DeclaringType.Name}.{node.Method.Name} is not supported.");

        return base.VisitMethodCall(node);

        string? CompileExp(Expression exp)
        {
            //if (exp.Has<ConstantExpression>(out var ce))
            //{
            var key = new ExpressionKey(exp, _queryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
            {
                //if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Select expression miss");
                //var p = Expression.Parameter(ce!.Type);
                //var rv = new ReplaceConstantExpressionVisitor(p);
                //var body = rv.Visit(exp);
                del = Expression.Lambda<Func<string>>(exp).Compile();
                DataContextCache.ExpressionsCache[key] = del;

                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    _logger.LogTrace("Select expression miss. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<string>)del)());
                }
                else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Select expression miss");
            }
            return ((Func<string>)del)();
            //}

            throw new NotImplementedException();
        }
    }

    /// <summary>Which part of a string a LIKE-style method (<c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c>) matches.</summary>
    private enum LikePosition
    {
        Contains,
        StartsWith,
        EndsWith
    }

    /// <summary>
    /// Translates the supported scalar functions (string, <see cref="Math"/> and <c>NORM.SQL.like</c>).
    /// Everything provider-specific is delegated to <see cref="ISqlDialect"/>; this dispatch only maps
    /// the CLR method to a function name and walks the arguments.
    /// </summary>
    private bool TryTranslateFunction(MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;

        if (declaringType == typeof(string))
            return TryTranslateStringMethod(node);

        if (declaringType == typeof(Math))
            return TryTranslateMathMethod(node);

        if (declaringType == typeof(NORM.NORM_SQL) && node.Method.Name == nameof(NORM.NORM_SQL.like))
            return TryTranslateLikeFunction(node);

        return false;
    }

    /// <summary>
    /// Translates a call to a method annotated with <see cref="SqlFunctionAttribute"/> into
    /// <c>[schema.]name(arg1, arg2, ...)</c>. An instance method's target is emitted as the first
    /// argument. This is attempted only after the built-in <c>string</c>/<see cref="Math"/>/<c>NORM.SQL</c>
    /// translations, so an attribute cannot change their behaviour.
    /// </summary>
    private bool TryTranslateSqlFunction(MethodCallExpression node)
    {
        var attribute = node.Method.GetCustomAttribute<SqlFunctionAttribute>(inherit: false)
            ?? node.Method.DeclaringType?.GetCustomAttribute<SqlFunctionAttribute>(inherit: false);

        if (attribute is null)
            return false;

        if (_paramMode)
        {
            // Nothing is emitted, but every embedded expression still has to be walked so captured
            // constants become parameters in the same order as the SQL pass.
            if (node.Object is not null)
                Visit(node.Object);

            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                Visit(node.Arguments[i]);

            return true;
        }

        var name = string.IsNullOrEmpty(attribute.Name) ? node.Method.Name : attribute.Name;

        _needAliasForColumn = true;
        _builder!.Append(_dialect.MakeFunction(name, attribute.Schema)).Append('(');

        var first = true;

        if (node.Object is not null)
        {
            _builder!.Append(VisitToString(node.Object));
            first = false;
        }

        for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
        {
            if (!first) _builder!.Append(", ");
            _builder!.Append(VisitToString(node.Arguments[i]));
            first = false;
        }

        _builder!.Append(')');
        return true;
    }

    /// <summary>
    /// Translates a window function into <c>func(...) over (partition by ... order by ... frame)</c>.
    /// The function is modelled as a call to the <c>Over</c> method on <see cref="NORM.WindowFunction{T}"/> whose
    /// target is the <see cref="NORM.NORM_SQL"/> function call (<c>row_number</c>, <c>lag</c>, <c>sum_over</c>,
    /// ...); the partition/order keys are nested lambdas that close over the outer query parameter, so
    /// they are rendered with this visitor's own context (the same <see cref="Clone"/>-free path the
    /// rest of the select list uses).
    /// <para>
    /// Limitations: <c>SELECT DISTINCT</c>, <c>GROUP BY</c> and correlated-references contexts are not
    /// adjusted for window functions. They are emitted as written (which the provider may reject)
    /// rather than silently rewritten into different semantics.
    /// </para>
    /// </summary>
    private bool TryTranslateWindowFunction(MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;
        if (declaringType is null
            || !declaringType.IsGenericType
            || declaringType.GetGenericTypeDefinition() != typeof(NORM.WindowFunction<>))
            return false;

        if (node.Object is not MethodCallExpression functionCall
            || functionCall.Method.DeclaringType != typeof(NORM.NORM_SQL)
            || MapWindowFunctionName(functionCall.Method.Name) is not { } functionName)
            return false;

        // The Over overloads differ in which parameters are present (partition-only, order-only, arrays,
        // frame), so the arguments are mapped by parameter name/type rather than by position.
        SplitWindowArguments(node, out var partitionArgument, out var orderArgument, out var frameArgument);

        var partitions = ParseWindowPartitions(partitionArgument);
        var orders = ParseWindowOrders(orderArgument);

        if (_paramMode)
        {
            // The parameter-extraction pass emits no SQL, but it still has to walk every embedded
            // expression so captured constants become parameters in the same order as the SQL pass.
            for (var (i, cnt) = (0, functionCall.Arguments.Count); i < cnt; i++)
                Visit(functionCall.Arguments[i]);

            for (var (i, cnt) = (0, partitions.Count); i < cnt; i++)
                Visit(partitions[i]);

            for (var (i, cnt) = (0, orders.Count); i < cnt; i++)
                Visit(orders[i].Body);

            return true;
        }

        var frame = EvaluateWindowFrame(frameArgument);

        _needAliasForColumn = true;

        // The aggregate functions keep their dialect mapping (e.g. stdev -> stddev) and count() keeps
        // its dialect spelling (the opening parenthesis is part of MakeCount); the ranking/value
        // functions are ANSI and shared by every provider.
        var functionArgs = functionCall.Arguments;
        if (functionName == "count")
        {
            _builder!.Append(_dialect.MakeCount(false, false));
        }
        else
        {
            _builder!.Append(functionName is "sum" or "avg" or "min" or "max"
                ? _dialect.MakeAggregate(functionName)
                : functionName);
            _builder!.Append('(');
        }

        if (functionName == "count" && functionArgs.Count == 0)
        {
            _builder!.Append('*');
        }
        else
        {
            for (var (i, cnt) = (0, functionArgs.Count); i < cnt; i++)
            {
                if (i > 0) _builder!.Append(", ");
                Visit(functionArgs[i]);
            }
        }

        _builder!.Append(") over (");

        if (partitions.Count > 0)
        {
            _builder!.Append("partition by ");
            for (var (i, cnt) = (0, partitions.Count); i < cnt; i++)
            {
                if (i > 0) _builder!.Append(", ");
                Visit(partitions[i]);
            }
        }

        if (orders.Count > 0)
        {
            if (partitions.Count > 0) _builder!.Append(' ');

            _builder!.Append("order by ");
            for (var (i, cnt) = (0, orders.Count); i < cnt; i++)
            {
                if (i > 0) _builder!.Append(", ");
                Visit(orders[i].Body);
                if (orders[i].Direction == OrderDirection.Desc)
                    _builder!.Append(" desc");
            }
        }

        if (frame is not null)
        {
            if (partitions.Count > 0 || orders.Count > 0) _builder!.Append(' ');
            _builder!.Append(RenderWindowFrame(frame));
        }

        _builder!.Append(')');
        return true;
    }

    /// <summary>Maps the <see cref="NORM.NORM_SQL"/> method name to the SQL window function name.</summary>
    private static string? MapWindowFunctionName(string methodName) => methodName switch
    {
        nameof(NORM.NORM_SQL.row_number) => "row_number",
        nameof(NORM.NORM_SQL.rank) => "rank",
        nameof(NORM.NORM_SQL.dense_rank) => "dense_rank",
        nameof(NORM.NORM_SQL.ntile) => "ntile",
        nameof(NORM.NORM_SQL.lag) => "lag",
        nameof(NORM.NORM_SQL.lead) => "lead",
        nameof(NORM.NORM_SQL.first_value) => "first_value",
        nameof(NORM.NORM_SQL.last_value) => "last_value",
        nameof(NORM.NORM_SQL.sum_over) => "sum",
        nameof(NORM.NORM_SQL.avg_over) => "avg",
        nameof(NORM.NORM_SQL.min_over) => "min",
        nameof(NORM.NORM_SQL.max_over) => "max",
        nameof(NORM.NORM_SQL.count_over) => "count",
        _ => null
    };

    private static List<Expression> ParseWindowPartitions(Expression? expression)
    {
        var result = new List<Expression>();
        if (expression is null or ConstantExpression { Value: null })
            return result;

        if (expression is NewArrayExpression array)
        {
            for (var (i, cnt) = (0, array.Expressions.Count); i < cnt; i++)
                AddWindowLambda(array.Expressions[i], result);
        }
        else
            AddWindowLambda(expression, result);

        return result;
    }

    private static void AddWindowLambda(Expression expression, List<Expression> result)
    {
        if (UnwrapWindowLambda(expression) is { } body)
            result.Add(body);
    }

    private static List<(Expression Body, OrderDirection Direction)> ParseWindowOrders(Expression? expression)
    {
        var result = new List<(Expression, OrderDirection)>();
        if (expression is null or ConstantExpression { Value: null })
            return result;

        if (expression is NewArrayExpression array)
        {
            for (var (i, cnt) = (0, array.Expressions.Count); i < cnt; i++)
                AddWindowOrder(array.Expressions[i], result);
        }
        else
            AddWindowOrder(expression, result);

        return result;
    }

    private static void AddWindowOrder(Expression expression, List<(Expression, OrderDirection)> result)
    {
        // A WindowOrder (NORM.SQL.asc/desc) carries an explicit direction; a bare lambda is ascending.
        if (expression is MethodCallExpression call
            && call.Method.DeclaringType == typeof(NORM.NORM_SQL)
            && call.Method.Name is nameof(NORM.NORM_SQL.asc) or nameof(NORM.NORM_SQL.desc))
        {
            if (UnwrapWindowLambda(call.Arguments[0]) is { } body)
            {
                var direction = call.Method.Name == nameof(NORM.NORM_SQL.desc) ? OrderDirection.Desc : OrderDirection.Asc;
                result.Add((body, direction));
            }

            return;
        }

        if (UnwrapWindowLambda(expression) is { } plainBody)
            result.Add((plainBody, OrderDirection.Asc));
    }

    /// <summary>
    /// Extracts the body of a partition/order lambda. An <c>Expression&lt;TDelegate&gt;</c> argument is
    /// stored in the tree as a <c>Quote</c>-wrapped lambda, so the quote has to be unwrapped first.
    /// </summary>
    private static Expression? UnwrapWindowLambda(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            expression = quote.Operand;

        return expression is LambdaExpression lambda ? lambda.Body : null;
    }

    /// <summary>
    /// Evaluates the <see cref="NORM.WindowFrame"/> argument. A frame is constant with respect to the
    /// query (it carries only boundaries), so its expression is compiled and cached rather than
    /// rendered as SQL.
    /// </summary>
    /// <summary>
    /// Maps the <c>Over</c> overload arguments onto partition/order/frame slots. The overloads differ
    /// in which slots exist (partition-only, order-only, arrays), so this is driven by the parameter
    /// names of the resolved method rather than by position.
    /// </summary>
    private static void SplitWindowArguments(
        MethodCallExpression node,
        out Expression? partitionArgument,
        out Expression? orderArgument,
        out Expression? frameArgument)
    {
        partitionArgument = null;
        orderArgument = null;
        frameArgument = null;

        var parameters = node.Method.GetParameters();
        var args = node.Arguments;
        var index = 0;

        if (parameters.Length > 0 && parameters[0].Name == "partitionBy")
            partitionArgument = args[index++];

        if (index < parameters.Length && parameters[index].Name == "orderBy")
            orderArgument = args[index++];

        if (index < parameters.Length && parameters[index].ParameterType == typeof(NORM.WindowFrame))
            frameArgument = args[index];
    }

    private NORM.WindowFrame? EvaluateWindowFrame(Expression? expression)
    {
        if (expression is null or ConstantExpression { Value: null })
            return null;

        if (expression is ConstantExpression { Value: NORM.WindowFrame constant })
            return constant;

        var key = new ExpressionKey(expression, _queryProvider);
        if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
        {
            del = Expression.Lambda<Func<NORM.WindowFrame>>(expression).Compile();
            DataContextCache.ExpressionsCache[key] = del;
        }

        return ((Func<NORM.WindowFrame>)del)();
    }

    private static string RenderWindowFrame(NORM.WindowFrame frame)
    {
        var unit = frame.Type == NORM.WindowFrameType.Rows ? "rows" : "range";
        return $"{unit} between {RenderWindowFrameBound(frame.Start)} and {RenderWindowFrameBound(frame.End)}";
    }

    private static string RenderWindowFrameBound(NORM.WindowFrameBound bound) => bound.Kind switch
    {
        NORM.WindowFrameBoundKind.UnboundedPreceding => "unbounded preceding",
        NORM.WindowFrameBoundKind.Preceding => $"{bound.Offset} preceding",
        NORM.WindowFrameBoundKind.CurrentRow => "current row",
        NORM.WindowFrameBoundKind.Following => $"{bound.Offset} following",
        NORM.WindowFrameBoundKind.UnboundedFollowing => "unbounded following",
        _ => throw new NotSupportedException(bound.Kind.ToString())
    };

    /// <summary>
    /// Translates a captured-collection membership test (<c>Enumerable.Contains</c> or an instance
    /// <c>Contains</c>) into the same <c>in (...)</c> predicate as the value-list
    /// <see cref="NORM.NORM_SQL.@in{T}(T, IEnumerable{T})"/> overload. Only collections that do not
    /// depend on the query parameter (captured locals/fields/constants) are translated.
    /// </summary>
    private bool TryTranslateCollectionContains(MethodCallExpression node)
    {
        if (!InValues.TryGetArguments(node, out var columnExp, out var valuesExp, out var elementType))
            return false;

        TranslateInValues(columnExp, valuesExp, elementType);
        return true;
    }

    /// <summary>
    /// Renders a value-list membership test (<c>column in (v1, ...)</c>). Values become parameters so
    /// the query stays injection safe; an empty list becomes an always-false condition and a list that
    /// can contain null keeps the C# <c>Contains</c> semantics by adding an <c>is null</c> branch.
    /// </summary>
    private void TranslateInValues(Expression columnExp, Expression valuesExp, Type elementType)
    {
        // A captured collection is re-read on every execution and the number of parameters (and so
        // the SQL text) depends on its length. When the shape was folded into the plan key while
        // preparing the condition, the evaluated partition is reused here and the plan stays cacheable.
        // In every other context (join/having/select, or a query that is not plan-cached) the shape is
        // not part of any key, so caching must stay disabled.
        var command = _queryProvider as QueryCommand;
        InValuesPartition partition;
        if (command?.InValuesPartitions is { } partitions && partitions.TryGetValue(valuesExp, out var cachedPartition))
        {
            partition = cachedPartition;
        }
        else
        {
            if (valuesExp is not NewArrayExpression && command is not null)
                command.Cache = false;

            partition = InValues.EvaluatePartition(valuesExp, _queryProvider);
        }

        var nonNull = partition.NonNull;
        var hasNull = partition.HasNull;
        var nullableAware = !elementType.IsValueType || Nullable.GetUnderlyingType(elementType) is not null;

        if (_paramMode)
        {
            Visit(columnExp);

            for (var i = 0; i < nonNull.Count; i++)
                _params.Add(new Param(_paramProvider.GetParamName(), nonNull[i]));

            return;
        }

        var column = VisitToString(columnExp);

        if (nonNull.Count == 0)
        {
            var emptyPredicate = nullableAware && hasNull ? $"{column} is null" : "1 = 0";
            _builder!.Append(_dialect.MakeBooleanPredicate(emptyPredicate, AsPredicate));
            return;
        }

        var inBuilder = _sbPool.Get();
        try
        {
            if (nullableAware && hasNull)
                inBuilder.Append('(');

            inBuilder.Append(column).Append(" in (");
            for (var i = 0; i < nonNull.Count; i++)
            {
                var paramName = _paramProvider.GetParamName();
                _params.Add(new Param(paramName, nonNull[i]));

                if (i > 0)
                    inBuilder.Append(", ");
                inBuilder.Append(_dialect.MakeParam(paramName));
            }
            inBuilder.Append(')');

            if (nullableAware && hasNull)
                inBuilder.Append(" or ").Append(column).Append(" is null)");

            _builder!.Append(_dialect.MakeBooleanPredicate(inBuilder.ToString(), AsPredicate));
        }
        finally
        {
            _sbPool.Return(inBuilder);
        }
    }

    private bool TryTranslateStringMethod(MethodCallExpression node)
    {
        var methodName = node.Method.Name;

        // Instance string methods.
        if (node.Object is not null && node.Object.Type == typeof(string))
        {
            switch (methodName)
            {
                // Only the culture-less overloads are portable; the CultureInfo overloads are not.
                case nameof(string.ToUpper) when node.Arguments.Count == 0:
                    return EmitStringFunction(node.Object, v => _dialect.MakeUpper(v));
                case nameof(string.ToLower) when node.Arguments.Count == 0:
                    return EmitStringFunction(node.Object, v => _dialect.MakeLower(v));
                // Trim/TrimStart/TrimEnd also have a (char)/params char[] overload that is not a
                // whitespace trim, so only the parameterless form is translated.
                case nameof(string.Trim) when node.Arguments.Count == 0:
                    return EmitStringFunction(node.Object, v => _dialect.MakeTrim(v, StringTrimKind.Both));
                case nameof(string.TrimStart) when node.Arguments.Count == 0:
                    return EmitStringFunction(node.Object, v => _dialect.MakeTrim(v, StringTrimKind.Start));
                case nameof(string.TrimEnd) when node.Arguments.Count == 0:
                    return EmitStringFunction(node.Object, v => _dialect.MakeTrim(v, StringTrimKind.End));
                case nameof(string.Substring):
                    return TryTranslateSubstring(node);
                case nameof(string.Replace):
                    return TryTranslateReplace(node);
                case nameof(string.Contains):
                    return TryTranslateStringLike(node, LikePosition.Contains);
                case nameof(string.StartsWith):
                    return TryTranslateStringLike(node, LikePosition.StartsWith);
                case nameof(string.EndsWith):
                    return TryTranslateStringLike(node, LikePosition.EndsWith);
            }

            return false;
        }

        // Static string methods.
        if (node.Object is null && methodName == nameof(string.IsNullOrEmpty))
            return TryTranslateIsNullOrEmpty(node);

        return false;
    }

    private bool EmitStringFunction(Expression operand, Func<string, string> render)
    {
        if (_paramMode)
        {
            Visit(operand);
            return true;
        }

        // A function call is a computed column and has to be aliased when selected.
        _needAliasForColumn = true;
        _builder!.Append(render(VisitToString(operand)));
        return true;
    }

    private bool TryTranslateSubstring(MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count is < 1 or > 2)
            return false;

        // Only the (int) and (int, int) overloads have a SQL equivalent; Substring(Range) does not.
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].Type != typeof(int))
                return false;
        }

        if (_paramMode)
        {
            Visit(node.Object);
            for (var i = 0; i < args.Count; i++) Visit(args[i]);
            return true;
        }

        _needAliasForColumn = true;
        var value = VisitToString(node.Object);
        var start = VisitToString(args[0]);
        var length = args.Count == 2 ? VisitToString(args[1]) : null;
        _builder!.Append(_dialect.MakeSubstring(value, start, length));
        return true;
    }

    private bool TryTranslateReplace(MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count != 2)
            return false;

        // Only the (string, string) overload maps to SQL replace; (char, char) has no char-typed form.
        if (args[0].Type != typeof(string) || args[1].Type != typeof(string))
            return false;

        if (_paramMode)
        {
            Visit(node.Object);
            Visit(args[0]);
            Visit(args[1]);
            return true;
        }

        _needAliasForColumn = true;
        _builder!.Append(_dialect.MakeReplace(VisitToString(node.Object), VisitToString(args[0]), VisitToString(args[1])));
        return true;
    }

    private bool TryTranslateStringLike(MethodCallExpression node, LikePosition position)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count != 1 || args[0].Type != typeof(string))
            return false;

        if (_paramMode)
        {
            Visit(node.Object);
            Visit(args[0]);
            return true;
        }

        _needAliasForColumn = true;
        var value = VisitToString(node.Object);
        var pattern = BuildLikePattern(args[0], position, out var escaped);
        var predicate = escaped
            ? $"{value} like {pattern} escape '\\'"
            : $"{value} like {pattern}";
        _builder!.Append(_dialect.MakeBooleanPredicate(predicate, AsPredicate));
        return true;
    }

    private bool TryTranslateLikeFunction(MethodCallExpression node)
    {
        var args = node.Arguments;
        if (args.Count is < 2 or > 3)
            return false;

        if (_paramMode)
        {
            for (var i = 0; i < args.Count; i++) Visit(args[i]);
            return true;
        }

        _needAliasForColumn = true;
        var value = VisitToString(args[0]);
        var pattern = VisitToString(args[1]);

        var escapeClause = string.Empty;
        if (args.Count == 3)
        {
            if (!TryGetConstantString(args[2], out var escapeChar))
                throw new NotSupportedException("The NORM.SQL.like escape character must be a constant string or char.");

            escapeClause = $" escape {ToSqlStringLiteral(escapeChar)}";
        }

        _builder!.Append(_dialect.MakeBooleanPredicate($"{value} like {pattern}{escapeClause}", AsPredicate));
        return true;
    }

    private bool TryTranslateIsNullOrEmpty(MethodCallExpression node)
    {
        var args = node.Arguments;
        if (args.Count != 1)
            return false;

        if (_paramMode)
        {
            Visit(args[0]);
            return true;
        }

        _needAliasForColumn = true;
        var value = VisitToString(args[0]);
        _builder!.Append(_dialect.MakeBooleanPredicate($"({value} is null or {value} = {_dialect.EmptyString})", AsPredicate));
        return true;
    }

    private bool TryTranslateMathMethod(MethodCallExpression node)
    {
        var name = node.Method.Name switch
        {
            nameof(Math.Abs) => "abs",
            nameof(Math.Ceiling) => "ceiling",
            nameof(Math.Floor) => "floor",
            nameof(Math.Round) => "round",
            nameof(Math.Sqrt) => "sqrt",
            nameof(Math.Pow) => "pow",
            nameof(Math.Exp) => "exp",
            nameof(Math.Log) => "log",
            nameof(Math.Sin) => "sin",
            nameof(Math.Cos) => "cos",
            nameof(Math.Tan) => "tan",
            nameof(Math.Sign) => "sign",
            nameof(Math.Truncate) => "trunc",
            _ => null
        };

        if (name is null)
            return false;

        var args = node.Arguments;

        // Math.Round's MidpointRounding overloads and the two-argument Math.Log are not portable and
        // are deliberately left unsupported rather than emitting SQL with different semantics.
        if (node.Method.Name == nameof(Math.Round) && args.Count > 2)
            return false;
        if (node.Method.Name == nameof(Math.Log) && args.Count != 1)
            return false;

        if (_paramMode)
        {
            for (var i = 0; i < args.Count; i++) Visit(args[i]);
            return true;
        }

        _needAliasForColumn = true;
        var sqlArgs = new string[args.Count];
        for (var i = 0; i < args.Count; i++)
            sqlArgs[i] = VisitToString(args[i]);

        _builder!.Append(_dialect.MakeMathFunction(name, sqlArgs));
        return true;
    }

    private bool TryTranslateMember(MemberExpression node)
    {
        var declaringType = node.Member.DeclaringType;

        // Static DateTime.Now / DateTime.UtcNow as a SQL expression instead of an evaluated parameter.
        if (node.Expression is null && declaringType == typeof(DateTime))
        {
            if (node.Member.Name == nameof(DateTime.Now) || node.Member.Name == nameof(DateTime.UtcNow))
            {
                _needAliasForColumn = true;
                if (!_paramMode)
                    _builder!.Append(_dialect.MakeNow(node.Member.Name == nameof(DateTime.UtcNow)));

                return true;
            }

            return false;
        }

        if (node.Expression is null)
            return false;

        // Nullable<T>.Value is a CLR-only wrapper: the underlying expression is rendered as is.
        if (declaringType is not null
            && node.Member.Name == nameof(Nullable<int>.Value)
            && Nullable.GetUnderlyingType(declaringType) is not null)
        {
            Visit(node.Expression);
            return true;
        }

        if (declaringType == typeof(string) && node.Member.Name == nameof(string.Length))
        {
            if (_paramMode)
            {
                Visit(node.Expression);
                return true;
            }

            _needAliasForColumn = true;
            _builder!.Append(_dialect.MakeStringLength(VisitToString(node.Expression)));
            return true;
        }

        if (declaringType == typeof(DateTime))
        {
            var part = node.Member.Name switch
            {
                nameof(DateTime.Year) => "year",
                nameof(DateTime.Month) => "month",
                nameof(DateTime.Day) => "day",
                nameof(DateTime.Hour) => "hour",
                nameof(DateTime.Minute) => "minute",
                nameof(DateTime.Second) => "second",
                _ => null
            };

            if (part is null)
                return false;

            if (_paramMode)
            {
                Visit(node.Expression);
                return true;
            }

            _needAliasForColumn = true;
            _builder!.Append(_dialect.MakeDatePart(part, VisitToString(node.Expression)));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds the right-hand side of a LIKE predicate for a <c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c>
    /// call. A constant is escaped and inlined; a runtime value is wrapped in <c>%</c> using the dialect
    /// concatenation operator and left unescaped (the wildcards cannot be escaped at translation time).
    /// </summary>
    private string BuildLikePattern(Expression argument, LikePosition position, out bool escaped)
    {
        var prefix = position is LikePosition.Contains or LikePosition.EndsWith ? "%" : string.Empty;
        var suffix = position is LikePosition.Contains or LikePosition.StartsWith ? "%" : string.Empty;

        if (argument is ConstantExpression { Value: string constant })
        {
            var escapedValue = EscapeLikeWildcards(constant);
            escaped = escapedValue != constant;
            return ToSqlStringLiteral(prefix + escapedValue + suffix);
        }

        escaped = false;
        var builder = _sbPool.Get();
        try
        {
            if (prefix.Length > 0)
                builder.Append(ToSqlStringLiteral(prefix)).Append(_dialect.ConcatStringOperator);

            builder.Append(VisitToString(argument));

            if (suffix.Length > 0)
                builder.Append(_dialect.ConcatStringOperator).Append(ToSqlStringLiteral(suffix));

            return builder.ToString();
        }
        finally
        {
            _sbPool.Return(builder);
        }
    }

    private static string EscapeLikeWildcards(string value)
    {
        if (value.IndexOfAny(['%', '_', '\\']) < 0)
            return value;

        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    private static bool TryGetConstantString(Expression expression, out string value)
    {
        switch (expression)
        {
            case ConstantExpression { Value: string s }:
                value = s;
                return true;
            case ConstantExpression { Value: char c }:
                value = c.ToString();
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private static string ToSqlStringLiteral(string value) => "'" + value.Replace("'", "''") + "'";

    /// <summary>Renders <paramref name="expression"/> into a fresh pooled builder (never in parameter mode).</summary>
    private string VisitToString(Expression expression)
    {
        using var visitor = Clone();
        visitor.Visit(expression);
        return visitor.ToString();
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
        if (node.Type.IsAssignableTo(typeof(QueryCommand)) && node.Arguments is [ConstantExpression ce] && ce.Value is int idx)
        {
            if (!_paramMode)
            {
                _builder!.Append('(');
            }
            _needAliasForColumn = true;
            var innerQuery = _queryProvider.ReferencedQueries[idx];
            var sqlBuilder = new SqlBuilder(_dialect, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
            var sql = sqlBuilder.MakeSelect(innerQuery);

                if (!_paramMode)
                {
                    _builder!.Append(sql).Append(')');
                }

            return node;
        }

        return base.VisitIndex(node);
    }
    // protected override Expression VisitLambda<T>(Expression<T> node)
    // {
    //     if (typeof(T).IsAssignableTo(typeof(QueryCommand)))
    //     {

    //     }
    //     return base.VisitLambda(node);
    // }
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (!_paramMode)
        {
            if (!EmitValue(node.Type, node.Value))
            {
                // if (node.Type.IsClosure())
                // {
                //     var o = GetFirstProp(node.Value);

                //     EmitValue(o.Item1, o.Item2);
                // }
            }
        }

        return node;

        // return base.VisitConstant(node);
        // static (Type, object?) GetFirstProp(object value)
        // {
        //     var field = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).First();
        //     return (field.FieldType, field.GetValue(value));
        // }
        bool EmitValue(Type t, object? v)
        {
            if (v is null)
                _builder!.Append("null");
            else if (t == typeof(string) || t == typeof(Guid))
            {
                _builder!.Append('\'').Append(v.ToString()).Append('\'');
            }
            else if (t.IsPrimitive)
            {
                if (t == typeof(bool))
                    _builder!.Append(_dialect.MakeBool((bool)v));
                else
                    // SQL literals must not depend on the current culture (ru-RU would render 0.2
                    // as 0,2, which changes the statement).
                    _builder!.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
            }
            else
                return false;

            return true;
        }
    }
    protected override Expression VisitMember(MemberExpression node)
    {
        if (TryTranslateMember(node))
            return node;

        if (node.Expression?.Type == _entityType)
        {
            if (node.Expression!.Type!.IsAssignableTo(typeof(IProjection)))
            {
                if (!_paramMode)
                    _builder!.Append(node.Member.Name).Append('.');

                return node;
            }
            else
            {
                if (!_paramMode)
                {
                    var colName = node.Member.GetPropertyColumnName();
                    if (!string.IsNullOrEmpty(colName))
                    {
                        if (!_dontNeedAlias && _columnsProvider.HasAliases)
                        {
                            string? tableAliasForColumn = null;
                            var v = new TypeExpressionVisitor<ParameterExpression>();
                            v.Visit(node.Expression);
                            if (v.Has)
                            {
                                // var aliasVisitor = new AliasFromProjectionVisitor();
                                // aliasVisitor.Visit(node.Expression);
                                // tableAliasForColumn = aliasVisitor.Alias;

                                // if (string.IsNullOrEmpty(tableAliasForColumn))
                                tableAliasForColumn = GetAliasFromParam(v.Target!, false);
                            }

                            if (!string.IsNullOrEmpty(tableAliasForColumn))
                                _builder!.Append(tableAliasForColumn).Append('.');
                        }

                        _builder!.Append(colName);
                        _colName = colName;
                        return node;
                    }
                    //var colAttr = node.Member.GetCustomAttribute<ColumnAttribute>();
                    // if (colAttr is not null)
                    // {
                    //     _builder!.Append(colAttr.Name);
                    //     _colName = colAttr.Name;
                    //     return node;
                    // }
                }

                var (n, innerQuery) = _columnsProvider.FindQueryCommand(_entityType);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name);
                    if (innerCol is null)
                        throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

                    var sqlBuilder = new SqlBuilder(_dialect, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
                    var col = sqlBuilder.MakeColumn(innerCol, innerQuery.EntityType!, true, renameAware: true);
                    if (!_paramMode)
                    {
                        if (!_dontNeedAlias)
                            _builder!.Append(_aliasProvider!.FindAlias(n)).Append('.');

                        if (col.NeedAliasForColumn)
                            _builder!.Append(_dialect.MakeColumnReference(innerCol.PropertyName!));
                        else
                        {
                            _builder!.Append(col.Column);
                            //_colName = col.Name;
                        }
                    }
                }
            }
        }
        else if (node.Expression is null)
        {
            if (!_paramMode && node.Member.DeclaringType == typeof(string))
            {
                if (node.Member.Name == nameof(string.Empty))
                    _builder!.Append(_dialect.EmptyString);

                return node;
            }

            var key = new ExpressionKey(node, _queryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
            {
                var body = Expression.Convert(node, typeof(object));
                del = Expression.Lambda<Func<object>>(body).Compile();

                DataContextCache.ExpressionsCache[key] = del;

                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    _logger.LogTrace("Expression cache miss on visit where. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<object>)del)());
                }
                else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Expression cache miss on visit where");
            }

            _params.Add(new Param(node.Member.Name, ((Func<object>)del)()));

            if (!_paramMode)
                _builder!.Append(_dialect.MakeParam(node.Member.Name));

            return node;
        }
        else if (node.Expression is NewExpression n)
        {
            if (!_paramMode && n.Type.IsGenericType && n.Type.GetGenericTypeDefinition() == typeof(OuterRefMarker<>) && n.Arguments is [ConstantExpression cexp] && cexp.Value is int idx)
            {
                var memberAccessExp = (MemberExpression)_queryProvider.OuterReferences![idx];
                string? tableAliasForColumn = null;

                if (memberAccessExp!.Type!.TryGetProjectionDimension(out _))
                {
                    var aliasVisitor = new AliasFromProjectionVisitor();
                    aliasVisitor.Visit(node.Expression);
                    tableAliasForColumn = aliasVisitor.Alias;
                }
                else
                    tableAliasForColumn = GetAliasFromParam((ParameterExpression)memberAccessExp.Expression!, false);

                _builder!.Append(tableAliasForColumn).Append('.');

                var colName = memberAccessExp.Member.GetPropertyColumnName();
                if (!string.IsNullOrEmpty(colName))
                {
                    _builder!.Append(colName);
                    _colName = colName;
                    return node;
                }
            }
        }
        else if (node.Expression.Type == typeof(TableColumn))
        {
            if (!_paramMode && node.Expression is MethodCallExpression mce && mce.Arguments is [ConstantExpression arg] && arg.Value is string column)
            {
                string? tableAliasForColumn = null;
                var v = new TypeExpressionVisitor<ParameterExpression>();
                v.Visit(mce.Object);
                if (v.Has)
                {
                    var aliasVisitor = new AliasFromProjectionVisitor();
                    aliasVisitor.Visit(node.Expression);
                    tableAliasForColumn = aliasVisitor.Alias;

                    if (string.IsNullOrEmpty(tableAliasForColumn))
                        tableAliasForColumn = GetAliasFromParam(v.Target!, false);
                }

                // Only a resolved table alias is prefixed: without a join there is a single source and
                // no alias, so emitting "<empty>." would produce invalid SQL (".column").
                if (!string.IsNullOrEmpty(tableAliasForColumn))
                    _builder!.Append(tableAliasForColumn).Append('.');

                _builder!.Append(column);
            }

            return node;
        }
        else
        {
            // The common member access is <param>.Member (join condition) or <param>.tN.Member
            // (projection), so the lambda parameter can be read structurally. Only the remaining
            // shapes (closure constants, deeper chains) need the allocating visitor.
            ParameterExpression? lambdaParameter = node.Expression switch
            {
                ParameterExpression p => p,
                MemberExpression { Expression: ParameterExpression p } => p,
                _ => null
            };

            if (lambdaParameter is null)
            {
                var visitor = new TwoTypeExpressionVisitor<ParameterExpression, ConstantExpression>();
                visitor.Visit(node.Expression);

                //if (node.Expression is ConstantExpression ce)
                if (!visitor.Has1 && visitor.Has2)
                {
                    //var ce = visitor.Target2!;
                    // Note: expression caching is currently unconditional (the CacheExpressions flag is not wired).
                    var key = new ExpressionKey(node, _queryProvider);
                    Delegate? del = null;
                    DataContextCache.ExpressionsCache.TryGetValue(key, out del);

                    if (del is null)
                    {
                        var p = Expression.Parameter(typeof(object));
                        var replace = new ReplaceConstantVisitor(Expression.Convert(p, visitor.Target2!.Type));
                        var body = Expression.Convert(replace.Visit(node), typeof(object));
                        del = Expression.Lambda<Func<object?, object>>(body, p).Compile();

                        DataContextCache.ExpressionsCache[key] = del;

                        if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                        {
                            _logger.LogTrace("Expression cache miss on visit where. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<object?, object>)del)(visitor.Target2.Value));
                        }
                        else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Expression cache miss on visit where");
                    }
                    // var value = 1;
                    _params.Add(new Param(node.Member.Name, ((Func<object?, object>)del)(visitor.Target2!.Value)));

                    if (!_paramMode)
                        _builder!.Append(_dialect.MakeParam(node.Member.Name));

                    return node;
                }

                lambdaParameter = visitor.Has1 ? visitor.Target1 : null;
            }

            if (lambdaParameter is not null)
            {
                var hasTableAliasForColumn = false;
                string? tableAliasForColumn = null;

                // Aliases are only needed to emit SQL text. In parameter-extraction mode (_paramMode)
                // nothing is appended, and _columnsProvider is not populated by MakeFrom/MakeJoin in
                // that mode, so resolving the alias would fail for join queries.
                if (!_paramMode && !_dontNeedAlias && lambdaParameter is not null)
                {
                    if (lambdaParameter.Type!.IsAssignableTo(typeof(IProjection)))
                    {
                        // When a type repeats inside the projection, the columns provider has to be
                        // told which occurrence is meant. The member name (tN) is the 1-based position
                        // among all projection items; the occurrence for every position of a given
                        // projection shape is cached (it is a pure function of the generic arguments).
                        var propExp = (MemberExpression)node.Expression!;
                        var name = propExp.Member.Name;
                        var position = 0;
                        for (var i = 1; i < name.Length; i++) position = position * 10 + (name[i] - '0');
                        position--;
                        var paramIdx = ProjectionAliasCache.GetOccurrence(lambdaParameter.Type, position);
                        tableAliasForColumn = GetAliasFromParam(node.Expression!.Type, paramIdx, false);
                    }
                    else if (_dim >= 2)
                    {
                        // In a chained join (dim >= 2) the condition is
                        // (accumulated projection, joinedEntity). Only the joined entity is a
                        // non-projection parameter, and its table alias is the (dim + 1)-th one.
                        // Resolving by type alone would pick the first table of that type, which is
                        // wrong when the same entity type is joined again.
                        tableAliasForColumn = _aliasProvider!.FindAlias(_dim);
                    }
                    else
                        tableAliasForColumn = GetAliasFromParam(lambdaParameter, false);

                    _builder!.Append(tableAliasForColumn).Append('.');

                    hasTableAliasForColumn = true;
                }

                if (!_paramMode)
                {
                    var colName = node.Member.GetPropertyColumnName();
                    if (!string.IsNullOrEmpty(colName))
                    {
                        _builder!.Append(colName);
                        _colName = colName;
                        return node;
                    }
                    // var colAttr = node.Member.GetCustomAttribute<ColumnAttribute>();
                    // if (colAttr is not null)
                    // {
                    //     _builder!.Append(colAttr.Name);
                    //     _colName = colAttr.Name;
                    //     return node;
                    // }
                }

                var (idx, innerQuery) = _columnsProvider.FindQueryCommand(node.Expression!.Type);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name);
                    if (innerCol is null)
                        throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

                    var sqlBuilder = new SqlBuilder(_dialect, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
                    var col = sqlBuilder.MakeColumn(innerCol, innerQuery.EntityType!, true, renameAware: true);

                    if (!_paramMode)
                    {
                        if (!hasTableAliasForColumn)
                            _builder!.Append(_aliasProvider!.FindAlias(idx)).Append('.');

                        if (col.NeedAliasForColumn)
                            _builder!.Append(_dialect.MakeColumnReference(innerCol.PropertyName!));
                        else
                            _builder!.Append(col.Column);
                    }
                }
                else if (!_paramMode)
                {
                    // The member is neither mapped in the entity metadata nor resolvable through an
                    // inner query, so no column name can be produced for it.
                    throw new BuildSqlCommandException($"Cannot resolve column for member {node.Member.Name}");
                }
                //}
                return node;
            }
        }

        return base.VisitMember(node);
    }

    private string? GetAliasFromParam(ParameterExpression lambdaParameter, bool fromProjection)
    {
        var idx = _columnsProvider!.FindAlias(lambdaParameter, fromProjection);

        if (!idx.HasValue) return null;

        return _aliasProvider!.FindAlias(idx.Value);
    }
    private string? GetAliasFromParam(Type entityType, int? paramIdx, bool fromProjection)
    {
        var idx = _columnsProvider!.FindAlias(entityType, paramIdx, fromProjection) ?? throw new InvalidOperationException();

        return _aliasProvider!.FindAlias(idx);
    }
    protected override Expression VisitUnary(UnaryExpression node)
    {
        // Numeric conversions are otherwise dropped, which silently changes the SQL semantics
        // (integer division, SQL Server integer avg, a projection wider than the column type).
        if (!_paramMode
            && node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
            && TryGetNumericConversion(node.Operand.Type, node.Type, out var target))
        {
            _needAliasForColumn = true;

            _builder!.Append("cast(");
            Visit(node.Operand);
            _builder!.Append(" as ").Append(_dialect.MakeTypeName(target)).Append(')');

            return node;
        }

        // The C# bool negation and the integral ones-complement both use ExpressionType.Not, so the
        // result type decides between the logical and the bitwise SQL operator.
        if (node.NodeType == ExpressionType.Not && !IsBoolean(node.Type))
            return VisitOnesComplement(node);

        switch (node.NodeType)
        {
            case ExpressionType.Not:
                return VisitNot(node);
            case ExpressionType.OnesComplement:
                return VisitOnesComplement(node);
            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                return VisitUnaryOperator(node, "-");
            case ExpressionType.UnaryPlus:
                return VisitUnaryOperator(node, "+");
        }

        return base.VisitUnary(node);
    }

    /// <summary>
    /// Translates a logical NOT. The operand is a boolean expression, so it is rendered with
    /// predicate semantics and the whole negation is handed to the dialect: a provider without a
    /// boolean type (SQL Server) has to turn a projected predicate into a bit scalar.
    /// </summary>
    private Expression VisitNot(UnaryExpression node)
    {
        if (_paramMode)
        {
            // Nothing is emitted, but the operand is still walked so captured constants are collected.
            Visit(node.Operand);
            return node;
        }

        _needAliasForColumn = true;
        _builder!.Append(_dialect.MakeBooleanPredicate($"not ({RenderPredicate(node.Operand)})", AsPredicate));

        return node;
    }

    /// <summary>Translates the integer ones-complement operator (<c>~</c>).</summary>
    private Expression VisitOnesComplement(UnaryExpression node) => VisitUnaryOperator(node, "~");

    /// <summary>
    /// Translates a unary arithmetic/bitwise operator. The operand is parenthesised so that the
    /// grouping of the source expression is preserved (e.g. <c>-(a + b)</c> stays a single operand).
    /// </summary>
    private Expression VisitUnaryOperator(UnaryExpression node, string sqlOperator)
    {
        if (_paramMode)
        {
            Visit(node.Operand);
            return node;
        }

        _needAliasForColumn = true;
        _builder!.Append(sqlOperator).Append('(');
        Visit(node.Operand);
        _builder!.Append(')');

        return node;
    }

    private static bool TryGetNumericConversion(Type source, Type target, out Type underlyingTarget)
    {
        static Type Unwrap(Type t) => Nullable.GetUnderlyingType(t) ?? t;
        static bool IsNumeric(Type t) => t == typeof(byte) || t == typeof(short) || t == typeof(int)
            || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(decimal);

        var unwrappedSource = Unwrap(source);
        underlyingTarget = Unwrap(target);

        return IsNumeric(unwrappedSource) && IsNumeric(underlyingTarget) && unwrappedSource != underlyingTarget;
    }
    /// <summary>
    /// Renders a boolean expression with predicate semantics (the context of a WHERE clause) into a
    /// fresh builder, so that the test of a conditional/switch uses the dialect's predicate form
    /// (e.g. EXISTS/ANY/ALL) instead of a scalar one.
    /// </summary>
    private string RenderPredicate(Expression expression)
    {
        // A bare boolean value (a bit column, a CASE, a method call) is not a predicate on a dialect
        // without a boolean type, so it is rendered as a value and the dialect turns it into one.
        if (!_paramMode && IsBoolean(expression.Type) && !IsPredicate(expression))
        {
            using var valueVisitor = Clone();
            valueVisitor.Visit(expression);

            return _dialect.MakeBooleanValuePredicate(valueVisitor.ToString());
        }

        using var visitor = new WhereExpressionVisitor(_entityType, _dialect, _columnsProvider, _dim, _aliasProvider, _paramProvider, _queryProvider, _paramMode, _params, _logger);
        visitor.Visit(expression);

        return visitor.ToString();
    }
    /// <summary>
    /// Renders a boolean expression that is used as a condition (a logical operand such as the left
    /// and right side of <c>&amp;&amp;</c>/<c>||</c>). A value that is not itself a predicate is
    /// turned into one by the dialect, so a bare boolean value stays valid on providers without a
    /// boolean type.
    /// </summary>
    private void AppendCondition(Expression expression)
    {
        if (!_paramMode && IsBoolean(expression.Type) && !IsPredicate(expression))
        {
            using var valueVisitor = Clone();
            valueVisitor.Visit(expression);
            _builder!.Append(_dialect.MakeBooleanValuePredicate(valueVisitor.ToString()));
            return;
        }

        Visit(expression);
    }
    /// <summary>
    /// Renders a whole condition (WHERE/HAVING/JOIN ON) into this visitor's builder. A condition
    /// that is a bare boolean value is turned into a predicate by the dialect.
    /// </summary>
    internal void VisitCondition(Expression condition)
    {
        // The root condition arrives as a lambda; the parameter is resolved by the member visitor,
        // so the body can be rendered directly (with the value-to-predicate conversion applied).
        if (condition is LambdaExpression lambda)
            condition = lambda.Body;

        AppendCondition(condition);
    }
    /// <summary>True for a value whose SQL rendering has to be a boolean scalar.</summary>
    private static bool IsBoolean(Type type) => type == typeof(bool) || type == typeof(bool?);

    /// <summary>
    /// True when the expression already renders as a predicate. Comparison/logical operators,
    /// boolean CASE/COALESCE and the recognised predicate methods produce a condition directly;
    /// anything else (a column, a constant, an arithmetic result) is a boolean value that the
    /// dialect has to convert to a predicate where a condition is required.
    /// </summary>
    private static bool IsPredicate(Expression expression) => expression.NodeType switch
    {
        ExpressionType.Equal or ExpressionType.NotEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
            or ExpressionType.AndAlso or ExpressionType.OrElse
            or ExpressionType.Not or ExpressionType.Coalesce
            or ExpressionType.Conditional or ExpressionType.Switch => true,
        ExpressionType.Call => IsPredicateCall((MethodCallExpression)expression),
        _ => false
    };

    /// <summary>True for the method calls that the visitor renders as a predicate (LIKE/IN/EXISTS).</summary>
    private static bool IsPredicateCall(MethodCallExpression call)
    {
        if (call.Method.DeclaringType == typeof(string))
            return call.Method.Name is nameof(string.Contains) or nameof(string.StartsWith)
                or nameof(string.EndsWith) or nameof(string.IsNullOrEmpty);

        return call.Method.Name is "exists" or "any" or "all" or "Contains";
    }
    protected override Expression VisitConditional(ConditionalExpression node)
    {
        // A CASE is a computed column and has to be aliased when it appears in a select list.
        _needAliasForColumn = true;

        if (_paramMode)
        {
            // The parameter-extraction pass emits no SQL, but the whole tree still has to be walked
            // so that captured constants/parameters inside the test and the branches are collected.
            Visit(node.Test);
            Visit(node.IfTrue);
            Visit(node.IfFalse);
            return node;
        }

        // The test is a condition, so it is rendered as a predicate (the branches are scalars).
        var test = RenderPredicate(node.Test);

        using var trueVisitor = Clone();
        trueVisitor.Visit(node.IfTrue);

        using var falseVisitor = Clone();
        falseVisitor.Visit(node.IfFalse);

        var caseBuilder = _sbPool.Get();
        try
        {
            caseBuilder.Append("case when ").Append(test)
                .Append(" then ").Append(trueVisitor.ToString())
                .Append(" else ").Append(falseVisitor.ToString())
                .Append(" end");

            _builder!.Append(_dialect.MakeCase(caseBuilder.ToString(), IsBoolean(node.Type), AsPredicate));
        }
        finally
        {
            _sbPool.Return(caseBuilder);
        }

        return node;
    }
    protected override Expression VisitSwitch(SwitchExpression node)
    {
        // A custom comparison method is not equality and cannot be translated without translating
        // the method itself; the C# compiler never produces one for a switch over constants.
        if (node.Comparison is not null)
            throw new NotSupportedException("A switch expression with a custom comparison method is not supported");

        _needAliasForColumn = true;

        if (_paramMode)
        {
            Visit(node.SwitchValue);

            for (var (i, cnt) = (0, node.Cases.Count); i < cnt; i++)
            {
                var @case = node.Cases[i];
                for (var (j, tvCnt) = (0, @case.TestValues.Count); j < tvCnt; j++)
                    Visit(@case.TestValues[j]);

                Visit(@case.Body);
            }

            if (node.DefaultBody is not null)
                Visit(node.DefaultBody);

            return node;
        }

        if (node.Cases.Count == 0)
        {
            // A switch without cases is just its default arm.
            if (node.DefaultBody is null)
                _builder!.Append("null");
            else
                Visit(node.DefaultBody);

            return node;
        }

        var caseBuilder = _sbPool.Get();
        var switchValueVisitor = Clone();
        try
        {
            switchValueVisitor.Visit(node.SwitchValue);
            var switchValue = switchValueVisitor.ToString();

            caseBuilder.Append("case");

            for (var (i, cnt) = (0, node.Cases.Count); i < cnt; i++)
            {
                var @case = node.Cases[i];

                using var bodyVisitor = Clone();
                bodyVisitor.Visit(@case.Body);
                var body = bodyVisitor.ToString();

                // Several test values share one body (case 1: case 2:), so the body is rendered once.
                for (var (j, tvCnt) = (0, @case.TestValues.Count); j < tvCnt; j++)
                {
                    using var testVisitor = Clone();
                    testVisitor.Visit(@case.TestValues[j]);

                    caseBuilder.Append(" when ").Append(switchValue)
                        .Append(" = ").Append(testVisitor.ToString())
                        .Append(" then ").Append(body);
                }
            }

            caseBuilder.Append(" else ");

            if (node.DefaultBody is null)
                caseBuilder.Append("null");
            else
            {
                using var defaultVisitor = Clone();
                defaultVisitor.Visit(node.DefaultBody);
                caseBuilder.Append(defaultVisitor.ToString());
            }

            caseBuilder.Append(" end");

            _builder!.Append(_dialect.MakeCase(caseBuilder.ToString(), IsBoolean(node.Type), AsPredicate));
        }
        finally
        {
            switchValueVisitor.Dispose();
            _sbPool.Return(caseBuilder);
        }

        return node;
    }
    protected override Expression VisitBinary(BinaryExpression node)
    {
        _needAliasForColumn = true;

        switch (node.NodeType)
        {
            case ExpressionType.Coalesce:
                if (!_paramMode)
                {
                    using var leftVisitor = Clone();
                    leftVisitor.Visit(node.Left);

                    using var rightVisitor = Clone();
                    rightVisitor.Visit(node.Right);

                    var coalesceLeft = leftVisitor.ToString();
                    var coalesceRight = rightVisitor.ToString();

                    _builder!.Append(node.Type == typeof(bool)
                        ? _dialect.MakeBoolCoalesce(coalesceLeft, coalesceRight)
                        : _dialect.MakeCoalesce(coalesceLeft, coalesceRight));
                    return node;
                }
                break;
        }

        // A logical AND/OR is a condition, so each operand is rendered as a predicate. On a dialect
        // without a boolean type a bare boolean value (e.g. a bit column) has to be turned into a
        // predicate first, otherwise the emitted <c>and</c>/<c>or</c> is rejected (SQL Server).
        if (!_paramMode && node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            _builder!.Append('(');
            AppendCondition(node.Left);
            _builder!.Append(node.NodeType == ExpressionType.AndAlso ? " and " : " or ");
            AppendCondition(node.Right);
            _builder!.Append(')');
            return node;
        }

        if (!_paramMode)
            _builder!.Append('(');

        Visit(node.Left);

        if (!_paramMode)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Add:
                    if (node.Type == typeof(string))
                        _builder!.Append(_dialect.ConcatStringOperator);
                    else
                        _builder!.Append(" + ");

                    break;
                case ExpressionType.And:
                    _builder!.Append(" & "); break;
                case ExpressionType.AndAlso:
                    _builder!.Append(" and "); break;
                case ExpressionType.Decrement:
                    _builder!.Append(" -1 "); break;
                case ExpressionType.Divide:
                    _builder!.Append(" / "); break;
                case ExpressionType.GreaterThan:
                    _builder!.Append(" > "); break;
                case ExpressionType.GreaterThanOrEqual:
                    _builder!.Append(" >= "); break;
                case ExpressionType.Increment:
                    _builder!.Append(" + 1"); break;
                case ExpressionType.LeftShift:
                    _builder!.Append(" << "); break;
                case ExpressionType.LessThan:
                    _builder!.Append(" < "); break;
                case ExpressionType.LessThanOrEqual:
                    _builder!.Append(" <= "); break;
                case ExpressionType.Modulo:
                    _builder!.Append(" % "); break;
                case ExpressionType.Multiply:
                    _builder!.Append(" * "); break;
                case ExpressionType.Negate:
                    _builder!.Append(" - "); break;
                case ExpressionType.Not:
                    _builder!.Append(" ~ "); break;
                case ExpressionType.NotEqual:
                    _builder!.Append(" != "); break;
                case ExpressionType.Equal:
                    _builder!.Append(" = "); break;
                case ExpressionType.Or:
                    _builder!.Append(" | "); break;
                case ExpressionType.OrElse:
                    _builder!.Append(" or "); break;
                case ExpressionType.Power:
                    _builder!.Append(" ^ "); break;
                case ExpressionType.RightShift:
                    _builder!.Append(" >> "); break;
                case ExpressionType.Subtract:
                    _builder!.Append(" - "); break;
                default:
                    throw new NotSupportedException(node.NodeType.ToString());
            }
        }

        Visit(node.Right);

        if (!_paramMode)
            _builder!.Append(')');

        return node;
    }
    public override string ToString()
    {
        return _builder!.ToString();
    }

    /// <summary>
    /// Appends the rendered SQL directly to <paramref name="target"/> instead of materialising an
    /// intermediate string. Used where the caller immediately appends the result to a larger builder.
    /// </summary>
    internal void WriteTo(StringBuilder target) => target.Append(_builder);


    object ICloneable.Clone()
    {
        return Clone();
    }

    public virtual BaseExpressionVisitor Clone()
    {
        if (_paramMode) throw new NotSupportedException("Cannot clone in param mode");

        return new BaseExpressionVisitor(_entityType, _dialect, _columnsProvider, _dim, _aliasProvider, _paramProvider, _queryProvider, _dontNeedAlias, _paramMode, _params, _logger, _sbPool);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (!_paramMode)
                    _sbPool.Return(_builder!);
            }
            _disposedValue = true;
        }
    }
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
