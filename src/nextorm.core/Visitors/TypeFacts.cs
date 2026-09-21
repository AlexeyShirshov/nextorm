using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Static classification helpers for expressions and CLR types used by the visitors: whether an
/// expression already renders as a predicate and whether a numeric conversion needs an explicit
/// <c>cast(...)</c>.
/// </summary>
internal static class TypeFacts
{
    /// <summary>True for a value whose SQL rendering has to be a boolean scalar.</summary>
    internal static bool IsBoolean(Type type) => type == typeof(bool) || type == typeof(bool?);

    /// <summary>
    /// True when the expression already renders as a predicate. Comparison/logical operators,
    /// boolean CASE/COALESCE and the recognised predicate methods produce a condition directly;
    /// anything else (a column, a constant, an arithmetic result) is a boolean value that the
    /// dialect has to convert to a predicate where a condition is required.
    /// </summary>
    internal static bool IsPredicate(Expression expression) => expression.NodeType switch
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

    /// <summary>True for the method calls that the visitor renders as a predicate (LIKE/IN/EXISTS/full-text).</summary>
    internal static bool IsPredicateCall(MethodCallExpression call)
    {
        if (call.Method.DeclaringType == typeof(string))
            return call.Method.Name is nameof(string.Contains) or nameof(string.StartsWith)
                or nameof(string.EndsWith) or nameof(string.IsNullOrEmpty);

        return call.Method.Name is "exists" or "any" or "all" or "Contains"
            or nameof(CommonFunctions.contains) or nameof(CommonFunctions.freetext)
            or nameof(SqlServerFunctions.isjson);
    }

    /// <summary>
    /// True when a numeric CLR conversion actually changes the type and therefore has to be emitted
    /// as <c>cast(...)</c>; dropping it would silently change the SQL semantics.
    /// </summary>
    internal static bool TryGetNumericConversion(Type source, Type target, out Type underlyingTarget)
    {
        static Type Unwrap(Type t) => Nullable.GetUnderlyingType(t) ?? t;
        static bool IsNumeric(Type t) => t == typeof(byte) || t == typeof(short) || t == typeof(int)
            || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(decimal);

        // Unsigned CLR types (ClickHouse's native UInt*) are valid conversion sources; the target must
        // still be a type the dialects know how to name.
        static bool IsConversionSource(Type t) => IsNumeric(t) || t == typeof(sbyte)
            || t == typeof(ushort) || t == typeof(uint) || t == typeof(ulong);

        var unwrappedSource = Unwrap(source);
        underlyingTarget = Unwrap(target);

        return IsConversionSource(unwrappedSource) && IsNumeric(underlyingTarget) && unwrappedSource != underlyingTarget;
    }
}
