using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;
using NpgsqlTypes;

namespace NextORM.Postgres;

/// <summary>
/// Resolves the PostgreSQL range type name and the Npgsql parameter type for a CLR bound type.
/// </summary>
internal static class PostgresRangeTypes
{
    private static readonly FrozenDictionary<Type, (string Name, NpgsqlDbType DbType)> Types =
        new Dictionary<Type, (string Name, NpgsqlDbType DbType)>
        {
            [typeof(int)] = ("int4range", NpgsqlDbType.IntegerRange),
            [typeof(long)] = ("int8range", NpgsqlDbType.BigIntRange),
            [typeof(decimal)] = ("numrange", NpgsqlDbType.NumericRange),
            [typeof(DateTime)] = ("tsrange", NpgsqlDbType.TimestampRange),
            [typeof(DateTimeOffset)] = ("tstzrange", NpgsqlDbType.TimestampTzRange),
            [typeof(DateOnly)] = ("daterange", NpgsqlDbType.DateRange)
        }.ToFrozenDictionary();

    /// <summary>Returns the PostgreSQL range type name for the given bound type.</summary>
    /// <param name="boundType">The CLR bound type of the range.</param>
    /// <returns>The PostgreSQL type name (for example <c>int4range</c>).</returns>
    /// <exception cref="NotSupportedException">The bound type has no PostgreSQL range type.</exception>
    public static string NameFor(Type boundType) => Resolve(boundType).Name;

    /// <summary>Returns the Npgsql parameter type of the range over the given bound type.</summary>
    /// <param name="boundType">The CLR bound type of the range.</param>
    /// <returns>The matching <c>NpgsqlDbType</c>.</returns>
    /// <exception cref="NotSupportedException">The bound type has no PostgreSQL range type.</exception>
    public static NpgsqlDbType DbTypeFor(Type boundType) => Resolve(boundType).DbType;

    private static (string Name, NpgsqlDbType DbType) Resolve(Type boundType) =>
        Types.TryGetValue(boundType, out var entry)
            ? entry
            : throw new NotSupportedException($"The CLR type {boundType} has no PostgreSQL range type.");
}

/// <summary>
/// Converts between the provider-agnostic <see cref="Range{T}"/> and Npgsql's
/// <c>NpgsqlRange&lt;T&gt;</c>.
/// </summary>
internal static class PostgresRange
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> Converters = new();

    /// <summary>Converts a boxed <see cref="Range{T}"/> to the matching boxed <c>NpgsqlRange&lt;T&gt;</c>.</summary>
    /// <param name="range">The boxed provider-agnostic range.</param>
    /// <returns>The boxed driver range.</returns>
    public static object ToDriver(object range)
    {
        var boundType = range.GetType().GetGenericArguments()[0];
        var converter = Converters.GetOrAdd(boundType, static type => BuildConverter(type));
        return converter(range);
    }

    /// <summary>Materializes a driver range as the provider-agnostic <see cref="Range{T}"/>.</summary>
    /// <typeparam name="T">The bound type.</typeparam>
    /// <param name="range">The driver range value.</param>
    /// <returns>The provider-agnostic range.</returns>
    public static Range<T> ToRange<T>(NpgsqlRange<T> range) where T : struct, IComparable<T>
    {
        if (range.IsEmpty)
            return Range<T>.Empty;

        return new Range<T>(
            range.LowerBoundInfinite ? null : range.LowerBound,
            range.UpperBoundInfinite ? null : range.UpperBound,
            range.LowerBoundIsInclusive,
            range.UpperBoundIsInclusive,
            range.LowerBoundInfinite,
            range.UpperBoundInfinite,
            isEmpty: false);
    }

    /// <summary>Converts a provider-agnostic range to the driver range.</summary>
    /// <typeparam name="T">The bound type.</typeparam>
    /// <param name="range">The provider-agnostic range.</param>
    /// <returns>The driver range value.</returns>
    public static NpgsqlRange<T> ToDriver<T>(Range<T> range) where T : struct, IComparable<T>
    {
        if (range.IsEmpty)
            return NpgsqlRange<T>.Empty;

        return new NpgsqlRange<T>(
            range.LowerInfinite ? default : range.Lower!.Value,
            range.LowerInclusive,
            range.LowerInfinite,
            range.UpperInfinite ? default : range.Upper!.Value,
            range.UpperInclusive,
            range.UpperInfinite);
    }

    private static Func<object, object> BuildConverter(Type boundType)
    {
        var generic = typeof(PostgresRange).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(method => method.Name == nameof(ToDriver) && method.IsGenericMethodDefinition)
            .MakeGenericMethod(boundType);
        var rangeType = typeof(Range<>).MakeGenericType(boundType);

        var parameter = Expression.Parameter(typeof(object));
        var call = Expression.Call(generic, Expression.Convert(parameter, rangeType));
        var body = Expression.Convert(call, typeof(object));
        return Expression.Lambda<Func<object, object>>(body, parameter).Compile();
    }
}
