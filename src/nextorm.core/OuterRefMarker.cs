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
/// is carried by the conversion that wraps the marker instead (see <see cref="OuterReference.Create"/>),
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
/// Builds and recognises the outer-reference node the correlated translator emits:
/// <c>(T)new OuterRefMarker(idx).Ref</c>.
/// </summary>
/// <remarks>
/// Everything the node needs is closed over a type the image already knows
/// (<see cref="OuterRefMarker"/>), so it can be built at run time without a generic instantiation and
/// without a reflection lookup over a user type. The conversion to <c>T</c> is an unbox/cast the
/// expression compiler performs on the target type handle, not a metadata lookup.
/// </remarks>
internal static class OuterReference
{
    /// <summary>The marker's constructor, resolved once from the statically known marker type.</summary>
    private static readonly ConstructorInfo MarkerCtor = typeof(OuterRefMarker).GetConstructor([typeof(int)])!;

    /// <summary>The marker's sentinel member, resolved once from the statically known marker type.</summary>
    private static readonly PropertyInfo RefMember = typeof(OuterRefMarker).GetProperty(nameof(OuterRefMarker.Ref))!;

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
