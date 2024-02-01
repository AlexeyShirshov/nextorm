using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class BaseExpressionVisitor : ExpressionVisitor, ICloneable, IDisposable
{
    private readonly Type _entityType;
    private readonly DbContext _dataProvider;
    private readonly IColumnsProvider _columnsProvider;
    private readonly int _dim;
    protected readonly StringBuilder? _builder;
    private readonly List<Param> _params;
    private readonly ILogger _logger;
    private bool _needAliasForColumn;
    private string? _colName;
    private readonly IAliasProvider? _aliasProvider;
    private readonly IParamProvider _paramProvider;
    private readonly IQueryProvider _queryProvider;
    private readonly bool _dontNeedAlias;
    protected readonly bool _paramMode;
    private bool _disposedValue;
    //private readonly Stack<(IColumnsProvider, ReadOnlyCollection<ParameterExpression>)> _scope;

    public BaseExpressionVisitor(Type entityType, DbContext dataProvider, IColumnsProvider columnsProvider, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, bool dontNeedAlias, bool paramMode, List<Param> @params, ILogger logger)
    {
        _entityType = entityType;
        _dataProvider = dataProvider;
        _columnsProvider = columnsProvider;
        _dim = dim;
        _aliasProvider = aliasProvider;
        _paramProvider = paramProvider;
        _queryProvider = queryProvider;
        _dontNeedAlias = dontNeedAlias;
        _paramMode = paramMode;
        _builder = paramMode ? null : DbContext._sbPool.Get();
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
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
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
                            or nameof(TableAlias.Column),
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
                var paramName = string.Format("norm_p{0}", paramIdx);
                _params.Add(new Param(paramName, null));
                if (!_paramMode)
                    _builder!.Append(_dataProvider.MakeParam(paramName));

                return node;
            }
            else
                throw new NotSupportedException(node.Method.Name);
        }
        else if (node.Method.DeclaringType == typeof(NORM.NORM_SQL) /*&& _tableProvider is IParamProvider paramProvider*/)
        {
            if ((node.Method.Name == nameof(NORM.NORM_SQL.exists)
                || node.Method.Name == nameof(NORM.NORM_SQL.all)
                || node.Method.Name == nameof(NORM.NORM_SQL.any)
                )
                && node.Arguments is [Expression exp] && exp.Type.IsAssignableTo(typeof(QueryCommand)))
            {
                QueryCommand innerQuery;
                var expVisitor = new TwoTypeExpressionVisitor<ParameterExpression, ConstantExpression>();
                expVisitor.Visit(exp);

                if (!_paramMode)
                    _builder!.Append(node.Method.Name switch
                    {
                        nameof(NORM.NORM_SQL.exists) => "exists(",
                        nameof(NORM.NORM_SQL.all) => "all(",
                        nameof(NORM.NORM_SQL.any) => "any(",
                        _ => throw new NotImplementedException()
                    });

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

                    if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                    {
                        _dataProvider.Logger.LogTrace("Expression cache miss on visit exists. hashcode: {hash}, value: {value}", keyCmd.GetHashCode(), d(paramValue));
                    }
                    else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Expression cache miss on visit exists");
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

                var sqlBuilder = new SqlBuilder(_dataProvider, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
                var sql = sqlBuilder.MakeSelect(innerQuery);

                if (!_paramMode)
                {
                    _builder!.Append(sql).Append(')');
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

                        if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                        {
                            _dataProvider.Logger.LogTrace("Subquery expression miss: {exp}", cmdExp);
                        }
                        else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Subquery expression miss");
                    }
                    else
                        innerQuery = (QueryCommand)dCmd.DynamicInvoke(constRepl.Params.Select(it => it.Item2).ToArray())!;

                }
                else
                    throw new InvalidOperationException();

                var sqlBuilder = new SqlBuilder(_dataProvider, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
                var sql = sqlBuilder.MakeSelect(innerQuery);

                if (!_paramMode)
                {
                    _builder!.Append(sql).Append(')');
                }

                return node;
            }
            else if (node.Method.Name == nameof(NORM.NORM_SQL.count)
                || node.Method.Name == nameof(NORM.NORM_SQL.count_distinct)
                || node.Method.Name == nameof(NORM.NORM_SQL.count_big)
                || node.Method.Name == nameof(NORM.NORM_SQL.count_big_distinct))
            {
                if (!_paramMode) _builder!.Append(_dataProvider.MakeCount(node.Method.Name.EndsWith("distinct"), node.Method.Name.Contains("big")));

                if (node.Arguments is [NewArrayExpression newArray] && newArray.Expressions is not [])//ReadOnlyCollection<Expression> args
                {
                    var items = newArray.Expressions;
                    for (var (i, cnt) = (0, items.Count); i < cnt; i++)
                    {
                        var argExp = items[i];
                        using var visitor = new BaseExpressionVisitor(_entityType, _dataProvider, _columnsProvider, 0, _aliasProvider, _paramProvider, _queryProvider, _dontNeedAlias, _paramMode, _params, _logger);
                        visitor.Visit(argExp);
                        if (!_paramMode) _builder!.Append(visitor.ToString()).Append(", ");
                    }
                    if (!_paramMode) _builder!.Length -= 2;
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
                    using var visitor = new BaseExpressionVisitor(_entityType, _dataProvider, _columnsProvider, 0, null, _paramProvider, _queryProvider, _dontNeedAlias, _paramMode, _params, _logger);
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
                    _builder!.Append(node.Method.Name.Replace("_distinct", string.Empty)).Append('(');
                    if (node.Method.Name.EndsWith("distinct"))
                        _builder!.Append("distinct ");
                }

                var args = node.Arguments;
                for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                {
                    var argExp = args[i];
                    using var visitor = new BaseExpressionVisitor(_entityType, _dataProvider, _columnsProvider, 0, null, _paramProvider, _queryProvider, _dontNeedAlias, _paramMode, _params, _logger);
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
                    if (!_paramMode)
                        _builder!.Append('(');

                    var args = node.Arguments;
                    for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                    {
                        var arg = args[i];
                        Visit(arg);
                        _builder!.Append(_dataProvider.ConcatStringOperator);
                    }

                    if (!_paramMode)
                    {
                        _builder!.Length -= _dataProvider.ConcatStringOperator.Length;
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

                if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    _dataProvider.Logger.LogTrace("Expression cache miss on visit method call. hashcode: {hash}, value: {value}", keyCmd.GetHashCode(), del());
                }
                else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Expression cache miss on visit method call");
            }
            else
                value = ((Func<object>)d)();

            var paramName = _paramProvider.GetParamName();
            var p = new Param(paramName, value);
            _params.Add(p);

            if (!_paramMode)
                _builder!.Append(_dataProvider.MakeParam(paramName));

            return node;
        }
        else if (node.Object?.Type.IsAssignableFrom(typeof(QueryCommand)) ?? false)
        {
            throw new NotImplementedException();
        }


        return base.VisitMethodCall(node);

        string? CompileExp(Expression exp)
        {
            //if (exp.Has<ConstantExpression>(out var ce))
            //{
            var key = new ExpressionKey(exp, _queryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
            {
                //if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Select expression miss");
                //var p = Expression.Parameter(ce!.Type);
                //var rv = new ReplaceConstantExpressionVisitor(p);
                //var body = rv.Visit(exp);
                del = Expression.Lambda<Func<string>>(exp).Compile();
                DataContextCache.ExpressionsCache[key] = del;

                if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    _dataProvider.Logger.LogTrace("Select expression miss. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<string>)del)());
                }
                else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Select expression miss");
            }
            return ((Func<string>)del)();
            //}

            throw new NotImplementedException();
        }
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
            var sqlBuilder = new SqlBuilder(_dataProvider, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
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
                    _builder!.Append(_dataProvider.MakeBool((bool)v));
                else
                    _builder!.Append(v.ToString());
            }
            else
                return false;

            return true;
        }
    }
    protected override Expression VisitMember(MemberExpression node)
    {
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
                    var colName = node.Member.GetPropertyColumnName(_dataProvider);
                    if (!string.IsNullOrEmpty(colName))
                    {
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

                var innerQuery = _columnsProvider.FindQueryCommand(_entityType);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name);
                    if (innerCol is null)
                        throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

                    var sqlBuilder = new SqlBuilder(_dataProvider, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
                    var col = sqlBuilder.MakeColumn(innerCol.Expression!, innerQuery.EntityType!, false);
                    if (!_paramMode)
                    {
                        if (col.NeedAliasForColumn)
                            _builder!.Append(innerCol.PropertyName);
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
                    _builder!.Append(_dataProvider.EmptyString);

                return node;
            }

            var key = new ExpressionKey(node, _queryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
            {
                var body = Expression.Convert(node, typeof(object));
                del = Expression.Lambda<Func<object>>(body).Compile();

                DataContextCache.ExpressionsCache[key] = del;

                if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    _dataProvider.Logger.LogTrace("Expression cache miss on visit where. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<object>)del)());
                }
                else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Expression cache miss on visit where");
            }

            _params.Add(new Param(node.Member.Name, ((Func<object>)del)()));

            if (!_paramMode)
                _builder!.Append(_dataProvider.MakeParam(node.Member.Name));

            return node;
        }
        else if (node.Expression is NewExpression n && n.Type.IsGenericType && n.Type.GetGenericTypeDefinition() == typeof(OuterRefMarker<>) && n.Arguments is [ConstantExpression cexp] && cexp.Value is int idx)
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
                tableAliasForColumn = GetAliasFromParam((ParameterExpression)memberAccessExp.Expression);

            if (!_paramMode)
                _builder!.Append(tableAliasForColumn).Append('.');

            if (!_paramMode)
            {
                var colName = memberAccessExp.Member.GetPropertyColumnName(_dataProvider);
                if (!string.IsNullOrEmpty(colName))
                {
                    _builder!.Append(colName);
                    _colName = colName;
                    return node;
                }
            }
        }
        else
        {
            var visitor = new TwoTypeExpressionVisitor<ParameterExpression, ConstantExpression>();
            visitor.Visit(node.Expression);

            //if (node.Expression is ConstantExpression ce)
            if (!visitor.Has1 && visitor.Has2)
            {
                //var ce = visitor.Target2!;
                ExpressionKey? key = null;
                Delegate? del = null;
                // if (_dataProvider.CacheExpressions)
                // {
                key = new ExpressionKey(node, _queryProvider);
                DataContextCache.ExpressionsCache.TryGetValue(key, out del);
                // }

                if (del is null)
                {
                    var p = Expression.Parameter(typeof(object));
                    var replace = new ReplaceConstantVisitor(Expression.Convert(p, visitor.Target2!.Type));
                    var body = Expression.Convert(replace.Visit(node), typeof(object));
                    del = Expression.Lambda<Func<object?, object>>(body, p).Compile();

                    if (key is not null)
                        DataContextCache.ExpressionsCache[key] = del;

                    if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                    {
                        _dataProvider.Logger.LogTrace("Expression cache miss on visit where. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<object?, object>)del)(visitor.Target2.Value));
                    }
                    else if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Expression cache miss on visit where");
                }
                // var value = 1;
                _params.Add(new Param(node.Member.Name, ((Func<object?, object>)del)(visitor.Target2!.Value)));

                if (!_paramMode)
                    _builder!.Append(_dataProvider.MakeParam(node.Member.Name));

                return node;
            }
            else if (visitor.Has1)
            {
                var lambdaParameter = visitor.Target1;

                var hasTableAliasForColumn = false;
                string? tableAliasForColumn = null;

                if (!_dontNeedAlias)
                {
                    if (lambdaParameter!.Type!.TryGetProjectionDimension(out _))
                    {
                        var aliasVisitor = new AliasFromProjectionVisitor();
                        aliasVisitor.Visit(node.Expression);
                        tableAliasForColumn = aliasVisitor.Alias;
                    }
                    else
                        tableAliasForColumn = GetAliasFromParam(lambdaParameter);

                    if (!_paramMode)
                        _builder!.Append(tableAliasForColumn).Append('.');

                    hasTableAliasForColumn = true;
                }

                if (!_paramMode)
                {
                    var colName = node.Member.GetPropertyColumnName(_dataProvider);
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

                var innerQuery = _columnsProvider.FindQueryCommand(node.Expression.Type);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name);
                    if (innerCol is null)
                        throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

                    var sqlBuilder = new SqlBuilder(_dataProvider, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, _aliasProvider, _logger);
                    var col = sqlBuilder.MakeColumn(innerCol.Expression!, innerQuery.EntityType!, hasTableAliasForColumn);

                    if (!_paramMode)
                    {
                        if (col.NeedAliasForColumn)
                            _builder!.Append(innerCol.PropertyName);
                        else
                            _builder!.Append(col.Column);
                    }
                }
                else
                {
                    if (!_paramMode)
                    {
                        var c = _dataProvider.GetColumnName(node.Member);

                        _builder!.Append(c);

                        _colName = c;
                    }
                }
                //}
                return node;
            }
        }

        return base.VisitMember(node);
    }

    private string? GetAliasFromParam(ParameterExpression lambdaParameter)
    {
        var idx = _columnsProvider!.FindAlias(lambdaParameter.Type) ?? throw new InvalidOperationException();

        return _aliasProvider!.FindAlias(idx);
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

                    _builder!.Append(_dataProvider.MakeCoalesce(
                        leftVisitor.ToString(),
                        rightVisitor.ToString()
                    ));
                    return node;
                }
                break;
            case ExpressionType.Conditional:
                throw new NotImplementedException();
            case ExpressionType.Switch:
                throw new NotImplementedException();
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
                        _builder!.Append(_dataProvider.ConcatStringOperator);
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

    object ICloneable.Clone()
    {
        return Clone();
    }

    public virtual BaseExpressionVisitor Clone()
    {
        if (_paramMode) throw new NotSupportedException("Cannot clone in param mode");

        return new BaseExpressionVisitor(_entityType, _dataProvider, _columnsProvider, _dim, _aliasProvider, _paramProvider, _queryProvider, _dontNeedAlias, _paramMode, _params, _logger);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (!_paramMode)
                    DbContext._sbPool.Return(_builder!);
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

public readonly ref struct AutoCleanup
{
    private readonly Action _onComplete;

    public AutoCleanup(Action onStart, Action onComplete)
    {
        onStart?.Invoke();
        _onComplete = onComplete;
    }
    public void Dispose()
    {
        _onComplete?.Invoke();
    }
}