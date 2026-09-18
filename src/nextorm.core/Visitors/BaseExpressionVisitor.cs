using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace nextorm.core;
/// <summary>
/// Base class for the expression visitors: holds the dialect, column/alias/parameter providers,
/// parameter list and logging state shared by the derived visitors.
/// </summary>
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
    private readonly VisitorOptions _options;
    //private readonly Stack<(IColumnsProvider, ReadOnlyCollection<ParameterExpression>)> _scope;

    /// <summary>
    /// Creates a visitor from a single <see cref="VisitorOptions"/> value. This is the preferred
    /// overload; the long-signature one below is kept for binary compatibility.
    /// </summary>
    public BaseExpressionVisitor(VisitorOptions options)
    {
        _options = options;
        _entityType = options.EntityType;
        _dialect = options.Dialect;
        _columnsProvider = options.ColumnsProvider;
        _dim = options.Dim;
        _aliasProvider = options.AliasProvider;
        _paramProvider = options.ParamProvider;
        _queryProvider = options.QueryProvider;
        _dontNeedAlias = options.DontNeedAlias;
        _paramMode = options.ParamMode;
        _sbPool = options.SbPool ?? StringBuilderPool.Shared;
        _builder = options.ParamMode ? null : _sbPool.Get();
        // _scope = paramScope;
        _params = options.Params;
        _logger = options.Logger;
    }

    /// <summary>
    /// Compatibility overload kept for binary compatibility; prefer <see cref="VisitorOptions"/>.
    /// </summary>
    public BaseExpressionVisitor(Type entityType, ISqlDialect dialect, IColumnsProvider columnsProvider, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, bool dontNeedAlias, bool paramMode, List<Param> @params, ILogger? logger, ObjectPool<StringBuilder>? sbPool = null)
        : this(new VisitorOptions(entityType, dialect, columnsProvider, dim, aliasProvider, paramProvider, queryProvider, dontNeedAlias, paramMode, @params, logger, sbPool))
    {
    }
    public bool NeedAliasForColumn { get => _needAliasForColumn; internal set => _needAliasForColumn = value; }
    public IColumnsProvider SourceProvider => _columnsProvider;
    public string? ColumnName { get => _colName; internal set => _colName = value; }
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

    // Internal collaboration surface for the visitor's translator classes (Visitors/*.cs).
    // Deliberately internal (and not protected) so the public/protected API of this type is
    // unchanged; there is no InternalsVisibleTo, so it stays inside nextorm.core.
    internal ISqlDialect Dialect => _dialect;
    internal IColumnsProvider ColumnsProvider => _columnsProvider;
    internal IAliasProvider? AliasProvider => _aliasProvider;
    internal IParamProvider ParamProvider => _paramProvider;
    internal IQueryProvider QueryProvider => _queryProvider;
    internal ObjectPool<StringBuilder> BuilderPool => _sbPool;
    internal List<Param> Params => _params;
    internal ILogger? Logger => _logger;
    internal Type EntityType => _entityType;
    internal int Dim => _dim;
    internal bool DontNeedAlias => _dontNeedAlias;
    internal bool IsParamMode => _paramMode;
    internal StringBuilder? Builder => _builder;
    internal bool IsPredicateContext => AsPredicate;
    internal VisitorOptions Options => _options;
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (!_paramMode
            && node.Object is null
            && node.Method.DeclaringType == typeof(Convert)            && node.Arguments is [Expression convertArg]
            && TypeFacts.TryGetNumericConversion(convertArg.Type, node.Method.ReturnType, out var convertTarget))
        {
            // Convert.ToXxx is a method call rather than a Convert node, so without this the
            // conversion was silently dropped.
            _needAliasForColumn = true;

            _builder!.Append("cast(");
            Visit(convertArg);
            _builder!.Append(" as ").Append(_dialect.MakeTypeName(convertTarget)).Append(')');

            return node;
        }

        if (ScalarFunctionTranslator.TryTranslateBuiltIn(this, node))
            return node;

        if (ScalarFunctionTranslator.TryTranslateSqlFunction(this, node))
            return node;

        if (WindowFunctionTranslator.TryTranslate(this, node))
            return node;

        if (InValuesTranslator.TryTranslateCollectionContains(this, node))
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
                        var tableAliasForColumn = AliasResolver.GetAliasFromParam(this, v.Target!, false);

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
                            or nameof(TableAlias.Bytes)
                            or nameof(TableAlias.NullableBytes)
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
        else if (NormSqlTranslator.TryTranslate(this, node))
            return node;
        else if (node.Object?.Type.IsAssignableTo(typeof(QueryCommand)) ?? false)
        {

        }
        else if (node.Type == typeof(string))
        {
            if (node.Object is null && node.Method.Name == nameof(string.Concat))
            {
                ScalarFunctionTranslator.EmitStringConcat(this, node);
                return node;
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

        // A string/math/date method that was not recognised above must not silently produce no SQL:
        // when the call cannot be folded to a parameter (it depends on a column) it is a clear error.
        if (node.Has<ParameterExpression>()
            && (node.Method.DeclaringType == typeof(string)
                || node.Method.DeclaringType == typeof(Math)
                || node.Method.DeclaringType == typeof(DateTime)))
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

    /// <summary>Renders <paramref name="expression"/> into a fresh pooled builder (never in parameter mode).</summary>
    internal string VisitToString(Expression expression)
    {
        using var visitor = Clone();
        visitor.Visit(expression);
        return visitor.ToString();
    }

    protected override Expression VisitNew(NewExpression node)
    {
        // new string(char, int) repeats a single character; SQL has no char literal, so the character
        // must be a compile-time constant and is rendered as a one-character string.
        if (node.Type == typeof(string)
            && node.Constructor is { } constructor
            && constructor.GetParameters() is [var charParam, var countParam]
            && charParam.ParameterType == typeof(char)
            && countParam.ParameterType == typeof(int)
            && node.Arguments is [var charArg, var countArg])
        {
            if (!SqlLiteral.TryGetConstantString(charArg, out var character))
                throw new NotSupportedException("The new string(char, n) character must be a constant.");

            if (_paramMode)
            {
                Visit(charArg);
                Visit(countArg);
                return node;
            }

            _needAliasForColumn = true;
            _builder!.Append(_dialect.MakeRepeat(
                SqlLiteral.ToSqlStringLiteral(character),
                VisitToString(countArg)));
            return node;
        }

        return base.VisitNew(node);
    }

    protected override Expression VisitIndex(IndexExpression node)
        => MemberTranslator.VisitIndex(this, node) ?? base.VisitIndex(node);
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
        => MemberTranslator.VisitMember(this, node) ?? base.VisitMember(node);

    protected override Expression VisitUnary(UnaryExpression node)
        => PredicateTranslator.VisitUnary(this, node) ?? base.VisitUnary(node);

    /// <summary>
    /// Renders a whole condition (WHERE/HAVING/JOIN ON) into this visitor's builder. A condition
    /// that is a bare boolean value is turned into a predicate by the dialect.
    /// </summary>
    internal void VisitCondition(Expression condition) => PredicateTranslator.VisitCondition(this, condition);
    protected override Expression VisitConditional(ConditionalExpression node) => PredicateTranslator.VisitConditional(this, node);
    protected override Expression VisitSwitch(SwitchExpression node) => PredicateTranslator.VisitSwitch(this, node);
    protected override Expression VisitBinary(BinaryExpression node) => PredicateTranslator.VisitBinary(this, node);
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

        return new BaseExpressionVisitor(_options);
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
