using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Converts a provider-native ClickHouse decimal array result (<c>ClickHouseDecimal[]</c> or
/// <c>ClickHouseDecimal?[]</c>) into the CLR decimal array the projection declared.
/// </summary>
/// <remarks>
/// <para>
/// With the driver's default <c>UseCustomDecimals=true</c> a <c>Decimal(38,10)</c> element is
/// reported as <c>ClickHouse.Driver.Numerics.ClickHouseDecimal</c>, so an array column read through
/// <see cref="System.Data.IDataRecord.GetValue(int)"/> arrives as a
/// <c>ClickHouseDecimal[]</c>/<c>ClickHouseDecimal?[]</c>. A CLR array cast does not apply the
/// element's conversion operator, so the materializer's cast to <c>decimal[]</c>/<c>decimal?[]</c>
/// failed before this converter existed. NextORM.Core cannot reference the provider assembly, so
/// the element type is matched by full name — the same seam
/// <see cref="JsonRowWriterFactory"/> uses for the scalar ClickHouse decimal field type.
/// </para>
/// <para>
/// The conversion is one linear pass over the source array into one exactly-typed destination. The
/// driver's explicit <c>operator decimal</c> is bound once per source element type into a compiled
/// delegate, so its <see cref="OverflowException"/> surfaces unchanged (no per-element reflection
/// metadata lookup, no reflection invocation, no catch, no default or partial result). Null
/// elements stay null. An array whose runtime type is already assignable to the target is returned
/// unchanged by the caller's cast; this converter never copies such an array.
/// </para>
/// </remarks>
internal static class ArrayResultConverter
{
    /// <summary>The driver's decimal element type, matched by name instead of a package reference.</summary>
    private const string ClickHouseDecimalFullName = "ClickHouse.Driver.Numerics.ClickHouseDecimal";

    private static readonly MethodInfo ConvertMI = typeof(ArrayResultConverter)
        .GetMethod(nameof(Convert), BindingFlags.NonPublic | BindingFlags.Static)!;

    // Built once per source element type: (decimal)(ClickHouseDecimal)value. The compiled delegate
    // invokes the driver's conversion operator directly and never wraps its exception.
    private static readonly ConcurrentDictionary<Type, Func<object, decimal>> ElementConverters = new();

    /// <summary>Whether <paramref name="type"/> is a CLR <c>decimal[]</c> or <c>decimal?[]</c>.</summary>
    /// <param name="type">The candidate array type.</param>
    /// <returns><see langword="true"/> for the two decimal array shapes.</returns>
    internal static bool IsDecimalArray(Type type)
        => type.IsArray
           && (type.GetElementType() == typeof(decimal) || type.GetElementType() == typeof(decimal?));

    /// <summary>
    /// Converts a provider-native ClickHouse decimal array into <paramref name="targetType"/>.
    /// Returns <see langword="false"/> (and leaves <paramref name="result"/> null) for every other
    /// source/target pairing, so the caller keeps its previous cast behaviour.
    /// </summary>
    /// <param name="source">The provider array value.</param>
    /// <param name="targetType">The declared materialized array type.</param>
    /// <param name="result">The converted array when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a conversion was applied.</returns>
    internal static bool TryConvert(Array source, Type targetType, out object? result)
    {
        result = null;

        // Rank-one decimal arrays only: the destination is allocated with one element per source
        // element, and a multi-dimensional source has no equivalent linear layout.
        if (!IsDecimalArray(targetType) || source.Rank != 1)
            return false;

        if (source.GetType().GetElementType() is not { } sourceElementType)
            return false;

        // Nullable<ClickHouseDecimal> boxes each element as ClickHouseDecimal, so the wrapper is
        // stripped for both the identity check and the compiled conversion.
        var elementType = Nullable.GetUnderlyingType(sourceElementType) ?? sourceElementType;
        if (!string.Equals(elementType.FullName, ClickHouseDecimalFullName, StringComparison.Ordinal))
            return false;

        var converter = ElementConverters.GetOrAdd(elementType, static type => CompileConverter(type));
        var length = source.Length;

        if (Nullable.GetUnderlyingType(targetType.GetElementType()!) is not null)
        {
            var nullableDestination = new decimal?[length];
            for (var i = 0; i < length; i++)
            {
                var element = source.GetValue(i);
                // A null element stays null; it is never substituted with zero or default(decimal).
                nullableDestination[i] = element is null ? null : converter(element);
            }

            result = nullableDestination;
            return true;
        }

        var destination = new decimal[length];
        for (var i = 0; i < length; i++)
        {
            var element = source.GetValue(i);
            if (element is null)
            {
                // A non-nullable destination cannot represent SQL NULL: fail closed rather than
                // silently substituting the element default.
                throw new InvalidCastException(
                    $"Cannot materialize a null {elementType.Name} array element as decimal: the projected array element type is non-nullable.");
            }

            destination[i] = converter(element);
        }

        result = destination;
        return true;
    }

    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="targetType"/> exactly like
    /// <see cref="Expression.Convert(Expression, Type)"/>, except that a decimal-array target is
    /// routed through <see cref="Convert{T}"/> so the provider element type is converted element by
    /// element. Emitted once per compiled mapper, never per row.
    /// </summary>
    /// <param name="value">The expression to convert.</param>
    /// <param name="targetType">The declared member/parameter/result type.</param>
    /// <returns>The conversion expression.</returns>
    internal static Expression EmitConvert(Expression value, Type targetType)
    {
        if (IsDecimalArray(targetType))
        {
            return Expression.Call(
                ConvertMI.MakeGenericMethod(targetType),
                Expression.Convert(value, typeof(object)));
        }

        return value.Type == targetType ? value : Expression.Convert(value, targetType);
    }

    /// <summary>
    /// Materializer entry point: an already-typed value is returned unchanged (the assignable fast
    /// path, no copy), a provider ClickHouse decimal array is converted with
    /// <see cref="TryConvert"/>, and every other value keeps the previous failing cast.
    /// </summary>
    /// <typeparam name="T">The declared materialized type.</typeparam>
    /// <param name="value">The boxed reader/result value.</param>
    /// <returns>The value as <typeparamref name="T"/>.</returns>
    internal static T Convert<T>(object? value)
    {
        if (value is T typed)
            return typed;

        if (value is Array array && TryConvert(array, typeof(T), out var converted))
            return (T)converted!;

        return (T)value!;
    }

    private static Func<object, decimal> CompileConverter(Type elementType)
    {
        var value = Expression.Parameter(typeof(object), "value");

        // (decimal)(ClickHouseDecimal)value binds the driver's explicit conversion operator once;
        // the compiled delegate calls it directly, so an OverflowException is not wrapped in
        // TargetInvocationException (as Delegate.DynamicInvoke would).
        var body = Expression.Convert(Expression.Convert(value, elementType), typeof(decimal));
        return Expression.Lambda<Func<object, decimal>>(body, value).Compile();
    }
}
