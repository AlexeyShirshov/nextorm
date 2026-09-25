using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Recognizes the provider-agnostic range shapes: <see cref="Range{T}"/> and a <see cref="Range{T}"/>
/// array (the CLR representation of a PostgreSQL multirange). Centralizes the shape test used by the
/// in-memory scalar rewriter, the operand translator and the in-memory aggregates so a change to the
/// representation has a single core-side source.
/// </summary>
internal static class RangeTypeFacts
{
    /// <summary>True when <paramref name="type"/> is a <see cref="Range{T}"/>, unwrapping a nullable wrapper.</summary>
    /// <param name="type">The CLR type to test.</param>
    /// <returns><see langword="true"/> when the type is <see cref="Range{T}"/> or a nullable <see cref="Range{T}"/>.</returns>
    internal static bool IsRange(Type type)
    {
        var real = Nullable.GetUnderlyingType(type) ?? type;
        return real.IsGenericType && real.GetGenericTypeDefinition() == typeof(Range<>);
    }

    /// <summary>True when <paramref name="type"/> is an array whose element is <see cref="Range{T}"/>.</summary>
    /// <param name="type">The CLR type to test.</param>
    /// <returns><see langword="true"/> when the type is a <see cref="Range{T}"/> array.</returns>
    internal static bool IsRangeArray(Type type)
        => type.IsArray && type.GetElementType() is { } element && IsRange(element);

    /// <summary>True when the expression is a <see cref="Range{T}"/>, ignoring an enclosing conversion.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> when the expression type is <see cref="Range{T}"/>.</returns>
    internal static bool IsRange(Expression expression) => IsRange(TypeFacts.UnwrapConvert(expression).Type);

    /// <summary>True when the expression is a <see cref="Range{T}"/> array, ignoring an enclosing conversion.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> when the expression type is a <see cref="Range{T}"/> array.</returns>
    internal static bool IsRangeArray(Expression expression) => IsRangeArray(TypeFacts.UnwrapConvert(expression).Type);

    /// <summary>Returns the bound type of a <see cref="Range{T}"/>, unwrapping a nullable wrapper.</summary>
    /// <param name="type">The CLR type to inspect.</param>
    /// <param name="boundType">The bound type <c>T</c> when the test succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is <see cref="Range{T}"/>.</returns>
    internal static bool TryGetRangeBoundType(Type type, out Type boundType)
    {
        var real = Nullable.GetUnderlyingType(type) ?? type;
        if (IsRange(real))
        {
            boundType = real.GetGenericArguments()[0];
            return true;
        }

        boundType = null!;
        return false;
    }

}
