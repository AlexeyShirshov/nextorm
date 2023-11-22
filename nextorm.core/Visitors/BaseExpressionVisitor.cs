using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class BaseExpressionVisitor : ExpressionVisitor, ICloneable, IDisposable
{
    private readonly Type _entityType;
    private readonly SqlDataProvider _dataProvider;
    private readonly ISourceProvider _tableProvider;
    private readonly int _dim;
    protected readonly StringBuilder? _builder;
    private readonly List<Param> _params = new();
    private bool _needAliasForColumn;
    private string? _colName;
    private readonly IAliasProvider? _aliasProvider;
    private readonly bool _dontNeedAlias;
    protected readonly bool _paramMode;
    private bool disposedValue;

    public BaseExpressionVisitor(Type entityType, SqlDataProvider dataProvider, ISourceProvider tableProvider, int dim, IAliasProvider? aliasProvider, bool dontNeedAlias, bool paramMode)
    {
        _entityType = entityType;
        _dataProvider = dataProvider;
        _tableProvider = tableProvider;
        _dim = dim;
        _aliasProvider = aliasProvider;
        _dontNeedAlias = dontNeedAlias;
        _paramMode = paramMode;
        _builder = paramMode ? null : dataProvider._sbPool.Get();
    }
    public bool NeedAliasForColumn => _needAliasForColumn;
    public List<Param> Params => _params;
    public ISourceProvider SourceProvider => _tableProvider;
    public string? ColumnName => _colName;
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
            if (node.Method.Name == nameof(NORM.NORM_SQL.exists)
                && node.Arguments is [Expression exp] && exp.Type.IsAssignableTo(typeof(QueryCommand)))
            {
                if (!_paramMode)
                    _builder!.Append("exists(");

                QueryCommand innerQuery;
                var keyCmd = new ExpressionKey(exp);
                if (!_dataProvider.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                {
                    var d = Expression.Lambda<Func<QueryCommand>>(exp).Compile();
                    _dataProvider.ExpressionsCache[keyCmd] = d;
                    innerQuery = d();
                }
                else
                    innerQuery = ((Func<QueryCommand>)dCmd)();

                var (sql, p) = _dataProvider.MakeSelect(innerQuery, _paramMode);
                _params.AddRange(p);

                if (!_paramMode)
                {
                    _builder!.Append(sql).Append(')');
                }

                return node;
            }
        }

        return base.VisitMethodCall(node);

        string? CompileExp(Expression exp)
        {
            //if (exp.Has<ConstantExpression>(out var ce))
            {
                var key = new ExpressionKey(exp);
                if (!_dataProvider.ExpressionsCache.TryGetValue(key, out var del))
                {
                    if (_dataProvider.Logger?.IsEnabled(LogLevel.Information) ?? false) _dataProvider.Logger.LogInformation("Select expression miss");
                    //var p = Expression.Parameter(ce!.Type);
                    //var rv = new ReplaceConstantExpressionVisitor(p);
                    //var body = rv.Visit(exp);
                    del = Expression.Lambda<Func<string>>(exp).Compile();
                    _dataProvider.ExpressionsCache[key] = del;
                }
                return ((Func<string>)del)();
            }

            throw new NotImplementedException();
        }
    }
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
                _builder!.Append('\'').Append(v?.ToString()).Append('\'');
            }
            else if (t.IsPrimitive)
            {
                if (t == typeof(bool))
                    _builder!.Append(_dataProvider.MakeBool((bool)v));
                else
                    _builder!.Append(v?.ToString());
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

                var innerQuery = _tableProvider.FindSourceFromAlias(null);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name) ?? throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");
                    var col = _dataProvider.MakeColumn(innerCol, innerQuery.EntityType!, innerQuery, false, _params, _paramMode);
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
                else if (!_paramMode)
                {
                    if (_dataProvider.NeedMapping)
                    {
                        var colName = node.Member.GetPropertyColumnName(_dataProvider);
                        if (!string.IsNullOrEmpty(colName))
                        {
                            _builder!.Append(colName);
                            _colName = colName;
                            return node;
                        }
                        // var props = _entityType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.CanWrite).ToArray();
                        // for (int idx = 0; idx < props.Length; idx++)
                        // {
                        //     // if (cancellationToken.IsCancellationRequested)
                        //     //     return;

                        //     var prop = props[idx];
                        //     if (prop is null) continue;
                        //     var colAttr = prop.GetCustomAttribute<ColumnAttribute>(true);
                        //     if (colAttr is not null)
                        //     {
                        //         _builder!.Append(colAttr.Name);
                        //         _colName = colAttr.Name;
                        //         return node;
                        //     }
                        //     else
                        //     {
                        //         foreach (var interf in _entityType.GetInterfaces())
                        //         {
                        //             // if (cancellationToken.IsCancellationRequested)
                        //             //     return;

                        //             var intMap = _entityType.GetInterfaceMap(interf);

                        //             var implIdx = Array.IndexOf(intMap.TargetMethods, prop!.GetMethod);
                        //             if (implIdx >= 0)
                        //             {
                        //                 var intMethod = intMap.InterfaceMethods[implIdx];

                        //                 var intProp = interf.GetProperties().FirstOrDefault(prop => prop.GetMethod == intMethod);
                        //                 colAttr = intProp?.GetCustomAttribute<ColumnAttribute>(true);
                        //                 if (colAttr is not null)
                        //                 {
                        //                     _builder!.Append(colAttr.Name);
                        //                     _colName = colAttr.Name;
                        //                     return node;
                        //                 }
                        //             }
                        //         }
                        //     }
                        // }
                    }
                    else
                        throw new NotImplementedException(node.Member.Name);
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

            var key = new ExpressionKey(node);
            if (!_dataProvider.ExpressionsCache.TryGetValue(key, out var del))
            {
                var body = Expression.Convert(node, typeof(object));
                del = Expression.Lambda<Func<object>>(body).Compile();

                _dataProvider.ExpressionsCache[key] = del;

                if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false)
                {
                    _dataProvider.Logger.LogDebug("Expression cache miss on visit where. hascode: {hash}, value: {value}", key.GetHashCode(), ((Func<object>)del)());
                }
                else if (_dataProvider.Logger?.IsEnabled(LogLevel.Information) ?? false) _dataProvider.Logger.LogInformation("Expression cache miss on visit where");
            }

            _params.Add(new Param(node.Member.Name, ((Func<object>)del)()));

            if (!_paramMode)
                _builder!.Append(_dataProvider.MakeParam(node.Member.Name));

            return node;
        }
        else
        {
            var visitor = new TwoTypeExpressionVisitor<ParameterExpression, ConstantExpression>();
            visitor.Visit(node.Expression);

            //if (node.Expression is ConstantExpression ce)
            if (!visitor.Has1 && visitor.Has2)
            {
                //var ce = visitor.Target2!;
                var key = new ExpressionKey(node);
                if (!_dataProvider.ExpressionsCache.TryGetValue(key, out var del))
                {
                    //value = 1;
                    // var p = Expression.Parameter(ce.Type);
                    // var replace = new ReplaceConstantExpressionVisitor(p);
                    // var body = replace.Visit(node)!;
                    var body = Expression.Convert(node, typeof(object));
                    del = Expression.Lambda<Func<object>>(body).Compile();

                    _dataProvider.ExpressionsCache[key] = del;

                    if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false)
                    {
                        _dataProvider.Logger.LogDebug("Expression cache miss on visit where. hascode: {hash}, value: {value}", key.GetHashCode(), ((Func<object>)del)());
                    }
                    else if (_dataProvider.Logger?.IsEnabled(LogLevel.Information) ?? false) _dataProvider.Logger.LogInformation("Expression cache miss on visit where");
                }
                // var value = 1;
                _params.Add(new Param(node.Member.Name, ((Func<object>)del)()));

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
                    if (lambdaParameter!.Type!.TryGetProjectionDimension(out var _))
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

                var innerQuery = _tableProvider.FindSourceFromAlias(tableAliasForColumn);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name) ?? throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");
                    var col = _dataProvider.MakeColumn(innerCol, innerQuery.EntityType!, innerQuery, hasTableAliasForColumn, _params, _paramMode);

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
    private string? GetAliasFromParam(ParameterExpression param)
    {
        return _aliasProvider?.FindAlias(param);
    }
    // private string GetAliasFromProjection(Type declaringType)
    // {
    //     var idx = 0;
    //     foreach (var prop in _entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
    //     {
    //         if (++idx > _dim && prop.PropertyType == declaringType)
    //             return prop.Name;
    //     }

    //     throw new BuildSqlCommandException($"Cannot find alias of type {declaringType} in {_entityType}");
    // }

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
                    Params.AddRange(leftVisitor.Params);

                    using var rightVisitor = Clone();
                    rightVisitor.Visit(node.Right);
                    Params.AddRange(rightVisitor.Params);

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

        return new BaseExpressionVisitor(_entityType, _dataProvider, _tableProvider, _dim, _aliasProvider, _dontNeedAlias, _paramMode);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (!_paramMode)
                    _dataProvider._sbPool.Return(_builder!);
            }
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~BaseExpressionVisitor()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
