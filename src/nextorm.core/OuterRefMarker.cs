using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Marker for a reference to an element of an outer (ancestor) query scope, used while building
/// correlated subqueries. It is only meaningful inside a query expression: the SQL translator rewrites
/// <c>new OuterRefMarker(idx).Ref</c> into a reference to the outer alias's column.
/// </summary>
/// <remarks>
/// The marker is deliberately non-generic. A marker closed over the referenced member's type
/// (<c>OuterRefMarker&lt;T&gt;(idx).Ref</c>) needed the closed type's constructor and property metadata
/// while the expression tree was built, and a native AOT image does not keep them for a type closed at
/// run time — the lookup returned <see langword="null"/> and building the tree failed. The element type
/// is carried by the conversion that wraps the marker instead (see <see cref="OuterReference.Create(int, Type)"/>),
/// so the node needs neither a generic instantiation nor a run-time type lookup.
/// </remarks>
/// <param name="i">The zero-based index of the outer element in the correlated query scope.</param>
internal sealed class OuterRefMarker(int i)
{
    /// <summary>
    /// The zero-based index of the referenced outer element, as passed to the constructor.
    /// </summary>
    public int I { get; } = i;

    /// <summary>
    /// Sentinel member the translator looks for to identify an outer reference. It is never assigned,
    /// so at run time its value is always <see langword="null"/>; the element type comes from the
    /// conversion the marker is wrapped in.
    /// </summary>
    public object? Ref { get; }
}

/// <summary>
/// Creates expressions that refer to values in an enclosing query.
/// </summary>
/// <remarks>
/// The returned expression is an opaque query node, not a value to compile or evaluate directly.
/// Its construction doesn't require a runtime generic instantiation or a reflection lookup over a
/// user type.
/// </remarks>
public static class OuterReference
{
    /// <summary>The marker's constructor, resolved once from the statically known marker type.</summary>
    private static readonly ConstructorInfo MarkerCtor = typeof(OuterRefMarker).GetConstructor([typeof(int)])!;

    /// <summary>The marker's sentinel member, resolved once from the statically known marker type.</summary>
    private static readonly PropertyInfo RefMember = typeof(OuterRefMarker).GetProperty(nameof(OuterRefMarker.Ref))!;

    /// <summary>
    /// Registers an enclosing-query expression and creates a reference to it for a correlated subquery.
    /// </summary>
    /// <param name="registry">The query registry for the correlated command.</param>
    /// <param name="outerExpression">The value expression over the enclosing query's existing row parameter.</param>
    /// <returns>A query expression with the same CLR type as <paramref name="outerExpression"/>.</returns>
    /// <remarks>
    /// Use the registry of the same query being built. This method doesn't introduce a new source
    /// or bind a projected slot of an APPLY source.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="registry"/> or <paramref name="outerExpression"/> is <see langword="null"/>.
    /// </exception>
    public static Expression Create(IQueryRegistry registry, Expression outerExpression)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(outerExpression);

        return Create(registry.AddOuterReference(outerExpression), outerExpression.Type);
    }

    /// <summary>
    /// Creates the node that denotes the outer element at <paramref name="index"/> as a value of
    /// <paramref name="valueType"/>.
    /// </summary>
    /// <param name="index">The index of the outer element in the correlated query scope.</param>
    /// <param name="valueType">The CLR type of the referenced element.</param>
    /// <returns>The marker node, typed as <paramref name="valueType"/>.</returns>
    internal static Expression Create(int index, Type valueType)
        => Expression.Convert(
            Expression.Property(
                Expression.New(MarkerCtor, Expression.Constant(index)),
                RefMember),
            valueType);

    /// <summary>
    /// Recognises a marker value node — either the converted
    /// (<c>(T)new OuterRefMarker(idx).Ref</c>) or the bare shape an identity conversion leaves
    /// (<c>new OuterRefMarker(idx).Ref</c>) — and extracts the outer index and the element type.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="index">Receives the index of the outer element.</param>
    /// <param name="valueType">Receives the CLR type the marker is presented as.</param>
    /// <returns><see langword="true"/> when <paramref name="expression"/> is a marker value.</returns>
    internal static bool TryGetValue(Expression? expression, out int index, out Type valueType)
    {
        index = 0;
        valueType = typeof(object);

        if (expression is null)
            return false;

        var operand = expression;
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Method: null } convert)
            operand = convert.Operand;

        if (!TryGetMember(operand, out index))
            return false;

        valueType = expression.Type;
        return true;
    }

    /// <summary>
    /// Recognises the <c>new OuterRefMarker(idx).Ref</c> member access and extracts its index.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="index">Receives the index of the outer element.</param>
    /// <returns><see langword="true"/> when <paramref name="expression"/> is the marker member.</returns>
    internal static bool TryGetMember(Expression? expression, out int index)
    {
        index = 0;

        if (expression is not MemberExpression
            {
                Member.Name: nameof(OuterRefMarker.Ref),
                Expression: NewExpression
                {
                    Type: var markerType,
                    Arguments: [ConstantExpression { Value: int i }]
                }
            })
            return false;

        if (markerType != typeof(OuterRefMarker))
            return false;

        index = i;
        return true;
    }
}
