using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace NextORM.Core;
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
    private readonly List<Parameter> _params;
    private readonly ILogger? _logger;
    private bool _needAliasForColumn;
    private string? _colName;
    private readonly IAliasProvider? _aliasProvider;
    private readonly IParameterProvider _parameterProvider;
    private readonly IQueryRegistry _queryProvider;
    private readonly bool _dontNeedAlias;
    protected readonly bool _paramMode;
    private bool _disposedValue;
    private readonly VisitorOptions _options;

    /// <summary>
    /// Creates a visitor from a single <see cref="VisitorOptions"/> value.
    /// </summary>
    public BaseExpressionVisitor(VisitorOptions options)
    {
        _options = options;
        _entityType = options.EntityType;
        _dialect = options.Dialect;
        _columnsProvider = options.ColumnsProvider;
        _dim = options.Dim;
        _aliasProvider = options.AliasProvider;
        _parameterProvider = options.ParameterProvider;
        _queryProvider = options.QueryProvider;
        _dontNeedAlias = options.DontNeedAlias;
        _paramMode = options.ParamMode;
        _sbPool = options.SbPool ?? StringBuilderPool.Shared;
        _builder = options.ParamMode ? null : _sbPool.Get();
        _params = options.Params;
        _logger = options.Logger;
    }

    public bool NeedAliasForColumn { get => _needAliasForColumn; internal set => _needAliasForColumn = value; }
    public IColumnsProvider SourceProvider => _columnsProvider;
    public string? ColumnName { get => _colName; internal set => _colName = value; }
    /// <summary>
    /// True when the built expression is used as a condition (WHERE/HAVING) instead of a value.
    /// </summary>
    protected virtual bool AsPredicate => false;

    // Internal collaboration surface for the visitor's translator classes (Visitors/*.cs).
    // Deliberately internal (and not protected) so the public/protected API of this type is
    // unchanged; there is no InternalsVisibleTo, so it stays inside NextORM.Core.
    internal ISqlDialect Dialect => _dialect;
    internal IColumnsProvider ColumnsProvider => _columnsProvider;
    internal IAliasProvider? AliasProvider => _aliasProvider;
    internal IParameterProvider ParameterProvider => _parameterProvider;
    internal IQueryRegistry QueryProvider => _queryProvider;
    internal ObjectPool<StringBuilder> BuilderPool => _sbPool;
    internal List<Parameter> Params => _params;
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
            if (!_paramMode)
                EmitTableAliasColumn(node);

            return node;
        }
        else if (node.Object?.Type == typeof(TableColumn))
        {
            throw new NotSupportedException($"Calls on {nameof(TableColumn)} are not supported.");
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

            throw new NotSupportedException($"The method {node.Method.Name} is not supported.");
        }
        else if (!node.Has<ParameterExpression>())
        {
            EmitFoldedParameter(node);
            return node;
        }
        else if (node.Object?.Type.IsAssignableFrom(typeof(QueryCommand)) ?? false)
        {
            throw new NotSupportedException($"Instance calls on {node.Object.Type.Name} are not supported.");
        }

        // A string/math/date method that was not recognised above must not silently produce no SQL:
        // when the call cannot be folded to a parameter (it depends on a column) it is a clear error.
        if (node.Has<ParameterExpression>()
            && (node.Method.DeclaringType == typeof(string)
                || node.Method.DeclaringType == typeof(Math)
                || node.Method.DeclaringType == typeof(DateTime)))
            throw new NotSupportedException($"The method {node.Method.DeclaringType.Name}.{node.Method.Name} is not supported.");

        return base.VisitMethodCall(node);
    }

    /// <summary>
    /// Renders the column selected by a <see cref="TableAlias"/> accessor call, prefixing it with the
    /// table alias when the query has aliased sources.
    /// </summary>
    private void EmitTableAliasColumn(MethodCallExpression node)
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

        if (!TableAliasAccessors.IsAccessor(node.Method.Name))
            throw new NotSupportedException(node.Method.Name);

        if (node.Arguments is [ConstantExpression constExp])
            _builder!.Append(constExp.Value?.ToString());
        else if (TableAliasAccessors.AllowsExpression(node.Method.Name) && node.Arguments is [Expression exp])
            _builder!.Append(CompileExpression(exp));
        else
            throw new NotSupportedException(node.Method.Name);
    }

    /// <summary>
    /// Folds a method call with no lambda parameters into a constant and emits it as a query
    /// parameter, caching the compiled delegate by expression key.
    /// </summary>
    private void EmitFoldedParameter(MethodCallExpression node)
    {
        object? value;
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

        var paramName = _parameterProvider.GetParamName();
        var p = new Parameter(paramName, value);
        _params.Add(p);

        if (!_paramMode)
            _builder!.Append(_dialect.MakeParam(paramName));
    }

    /// <summary>Compiles and evaluates a computed column expression to its string form (cached by expression key).</summary>
    private string CompileExpression(Expression exp)
    {
        var key = new ExpressionKey(exp, _queryProvider);
        if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
        {
            del = Expression.Lambda<Func<string>>(exp).Compile();
            DataContextCache.ExpressionsCache[key] = del;

            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
            {
                _logger.LogTrace("Select expression miss. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<string>)del)());
            }
            else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Select expression miss");
        }
        return ((Func<string>)del)();
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

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (!_paramMode)
            TryEmitValue(node.Type, node.Value);

        return node;
    }

    /// <summary>
    /// Appends a constant literal (null, string/GUID, or primitive) to the builder. Returns
    /// <see langword="false"/> for values the caller must handle itself (anything non-primitive).
    /// </summary>
    private bool TryEmitValue(Type t, object? v)
    {
        if (v is null)
            _builder!.Append("null");
        else if (t == typeof(string) || t == typeof(Guid))
        {
            _builder!.Append('\'').Append(v.ToString()).Append('\'');
        }
        else if (t.IsPrimitive || t == typeof(decimal))
        {
            if (t == typeof(bool))
                _builder!.Append(_dialect.MakeBool((bool)v));
            else
                // SQL literals must not depend on the current culture (ru-RU would render 0.2
                // as 0,2, which changes the statement). decimal is not an IsPrimitive type, so it
                // has to be listed explicitly or its literal would be dropped from the SQL.
                _builder!.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
        }
        else
            return false;

        return true;
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
