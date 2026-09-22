using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Helpers for rewriting and inspecting expression trees.
/// </summary>
public static class ExpressionExtensions
{
    /// <summary>
    /// Determines whether the expression tree contains a node of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The expression node type to look for.</typeparam>
    /// <param name="exp">The expression tree to search.</param>
    /// <returns><see langword="true"/> when a matching node exists; otherwise <see langword="false"/>.</returns>
    public static bool Has<T>(this Expression exp)
        where T : Expression
    {
        var visitor = new TypeExpressionVisitor<T>();
        visitor.Visit(exp);
        return visitor.Has;
    }
    /// <summary>
    /// Determines whether the expression tree contains a node of type <typeparamref name="T"/> and,
    /// when it does, returns the first matching node through <paramref name="param"/>.
    /// </summary>
    /// <typeparam name="T">The expression node type to look for.</typeparam>
    /// <param name="exp">The expression tree to search.</param>
    /// <param name="param">The first matching node, or <c>null</c> when none was found.</param>
    /// <returns><see langword="true"/> when a matching node exists; otherwise <see langword="false"/>.</returns>
    public static bool Has<T>(this Expression exp, out T? param)
        where T : Expression
    {
        var visitor = new TypeExpressionVisitor<T>();
        visitor.Visit(exp);
        param = visitor.Target;
        return visitor.Has;
    }
    /// <summary>
    /// Determines whether the expression contains a special method call that requires an explicit
    /// conversion before it can be compared.
    /// </summary>
    /// <param name="exp">The expression tree to inspect.</param>
    /// <returns><see langword="true"/> when a conversion is required; otherwise <see langword="false"/>.</returns>
    public static bool NeedToConvert(this Expression exp)
    {
        var visitor = new TestSpecialMethodCallVisitor();
        visitor.Visit(exp);
        return visitor.Result;
    }
}
/// <summary>
/// Expression visitor that operates on a single entity type.
/// </summary>
/// <typeparam name="T">The entity type the visitor expects at the leaves.</typeparam>
public class TypeExpressionVisitor<T> : ExpressionVisitor
    where T : Expression
{
    private bool _has;
    private T? _target;
    /// <summary>Whether a node of type <typeparamref name="T"/> was found during the visit.</summary>
    public bool Has => _has;
    /// <summary>The first node of type <typeparamref name="T"/> found during the visit, or <c>null</c>.</summary>
    public T? Target => _target;
    /// <summary>
    /// Records the first node assignable to <typeparamref name="T"/> and stops there; otherwise
    /// continues the base traversal.
    /// </summary>
    /// <param name="node">The current node.</param>
    /// <returns>The node, or the result of the base visit.</returns>
    [return: NotNullIfNotNull("node")]
    public override Expression? Visit(Expression? node)
    {
        if (node is T t)
        {
            _has = true;
            _target = t;
            return node;
        }

        return base.Visit(node);
    }
}
/// <summary>
/// Expression visitor that operates over two entity types (used when translating join conditions).
/// </summary>
/// <typeparam name="T1">The first entity type.</typeparam>
/// <typeparam name="T2">The second entity type.</typeparam>
/// <remarks>
/// Renamed from <c>TwoTypeExpressionVisitor</c> to use the arity style (<c>T1, T2</c>) shared by the
/// rest of the surface. See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-14.
/// </remarks>
public class TypeExpressionVisitor<T1, T2> : ExpressionVisitor
    where T1 : Expression
    where T2 : Expression
{
    private bool _has1;
    private T1? _target1;
    /// <summary>Whether a node of type <typeparamref name="T1"/> was found during the visit.</summary>
    public bool Has1 => _has1;
    /// <summary>The first node of type <typeparamref name="T1"/> found during the visit, or <c>null</c>.</summary>
    public T1? Target1 => _target1;
    private bool _has2;
    private T2? _target2;
    /// <summary>Whether a node of type <typeparamref name="T2"/> was found during the visit.</summary>
    public bool Has2 => _has2;
    /// <summary>The first node of type <typeparamref name="T2"/> found during the visit, or <c>null</c>.</summary>
    public T2? Target2 => _target2;
    /// <summary>
    /// Records the first node assignable to <typeparamref name="T1"/> or <typeparamref name="T2"/> and
    /// stops there; otherwise continues the base traversal.
    /// </summary>
    /// <param name="node">The current node.</param>
    /// <returns>The node, or the result of the base visit.</returns>
    [return: NotNullIfNotNull("node")]
    public override Expression? Visit(Expression? node)
    {
        if (node is T1 t1)
        {
            _has1 = true;
            _target1 = t1;
            return node;
        }
        else if (node is T2 t2)
        {
            _has2 = true;
            _target2 = t2;
            return node;
        }
        return base.Visit(node);
    }
}
/// <summary>
/// Replaces captured constant values with query parameters.
/// </summary>
/// <remarks>
/// The plural <c>Constants</c> distinguishes this full parametrisation pass from the singular
/// <see cref="ReplaceConstantExpressionVisitor"/> in <c>Visitors/ReplaceExpressionVisitor.cs</c>.
/// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-14.
/// </remarks>
public class ReplaceConstantsExpressionVisitor : ExpressionVisitor
{
    private readonly List<(ParameterExpression, object?)> _params = new();
    private readonly object[]? _replaceParams;
    private readonly IEnumerable<ParameterExpression>? _outerParams;
    private readonly IQueryRegistry? _queryProvider;

    /// <summary>
    /// Creates a visitor that replaces runtime values with query parameters, matching an argument to a
    /// parameter by type.
    /// </summary>
    /// <param name="params">The values to register as parameters; may be <c>null</c>.</param>
    public ReplaceConstantsExpressionVisitor(params object[]? @params)
    {
        _replaceParams = @params;
    }
    /// <summary>
    /// Creates a visitor that replaces members of <paramref name="params"/> with outer-reference
    /// markers and all other constants with parameters.
    /// </summary>
    /// <param name="params">The outer parameters whose members become outer references; may be <c>null</c>.</param>
    /// <param name="queryProvider">The registry the outer references are registered on.</param>
    public ReplaceConstantsExpressionVisitor(IEnumerable<ParameterExpression>? @params, IQueryRegistry queryProvider)
    {
        _outerParams = @params;
        _queryProvider = queryProvider;
    }
    /// <summary>The parameters produced while replacing constants, as parameter/value pairs.</summary>
    public List<(ParameterExpression, object?)> Params => _params;

    // public bool HasOuterParams { get; internal set; }

    /// <inheritdoc/>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        // if (node.Type.IsScalar())
        // {
        return GetConstantParameter(node);
        //}
        //return node;
    }
    /// <inheritdoc/>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_replaceParams is not null)
        {
            for (var (i, cnt) = (0, _replaceParams.Length); i < cnt; i++)
            {
                var param = _replaceParams[i];
                if (param.GetType() == node.Type || param.GetType().IsAssignableFrom(node.Type) || param.GetType().IsAssignableTo(node.Type))
                {
                    _params.Add((node, param));
                    return node;
                }
            }
        }

        return base.VisitParameter(node);
    }
    private ParameterExpression GetConstantParameter(ConstantExpression node)
    {
        var p = Expression.Parameter(node.Type);
        _params.Add((p, node.Value));
        return p;
    }
    /// <inheritdoc/>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.NodeType == ExpressionType.MemberAccess && node.Expression is ParameterExpression param
            && _outerParams is not null && _outerParams.Contains(param))
        {
            var idx = _queryProvider!.AddOuterReference(node);
            return Expression.Property(Expression.New(GetOuterRefMarkerCI(node.Type), Expression.Constant(idx)), "Ref");
        }

        return base.VisitMember(node);
    }
    /// <summary>
    /// Returns the constructor of <c>OuterRefMarker&lt;T&gt;</c> closed over
    /// <paramref name="type"/>, used to build an outer-reference marker node.
    /// </summary>
    /// <param name="type">The type of the outer reference.</param>
    /// <returns>The constructor taking the reference index.</returns>
    public static ConstructorInfo GetOuterRefMarkerCI(Type type)
    {
        return typeof(OuterRefMarker<>).MakeGenericType(type).GetConstructor([typeof(int)])!;
    }
}
/// <summary>
/// Expression visitor whose traversal is driven by a caller-supplied predicate.
/// </summary>
/// <typeparam name="T">The entity type the predicate is evaluated against.</typeparam>
/// <param name="predicate">Receives the current node and a compiled predicate for <typeparamref name="T"/>.</param>
public class PredicateExpressionVisitor<T>(Func<Expression?, Func<T, bool>, bool> predicate) : ExpressionVisitor
{
    private readonly Func<Expression?, Func<T, bool>, bool> _predicate = predicate;
    private T? _value;
    private bool _result;
    /// <summary>The value captured by the predicate through its store callback, if any.</summary>
    public T? Value => _value;
    /// <summary>Whether the caller-supplied predicate matched a node during the visit.</summary>
    public bool Result => _result;

    /// <summary>
    /// Invokes the caller-supplied predicate for each node; on a match it records the result and stops,
    /// otherwise it continues the base traversal.
    /// </summary>
    /// <param name="node">The current node.</param>
    /// <returns>The node, or the result of the base visit.</returns>
    [return: NotNullIfNotNull("node")]
    public override Expression? Visit(Expression? node)
    {
        if (_predicate(node, StoreValue))
        {
            _result = true;
            return node;
        }

        return base.Visit(node);
    }
    private bool StoreValue(T value)
    {
        _value = value;
        return true;
    }
}