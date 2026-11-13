using System.Reflection;

namespace NextORM.Postgres;

/// <summary>
/// Converts the <c>text[]</c> element texts a pgvector array column is projected through back into
/// provider-owned CLR arrays. A PostgreSQL array may contain <c>NULL</c> elements, but a CLR array of
/// a value type cannot represent one, so a <c>NULL</c> element fails with a deterministic error
/// instead of being silently replaced by a default value.
/// </summary>
internal static class PostgresVectorArrays
{
    /// <summary>Converts <c>text[]</c> elements into a <see cref="PgVector"/> array.</summary>
    /// <param name="values">The element texts.</param>
    public static PgVector[] ToVector(string?[] values) => Map(values, PgVector.Parse, "vector");

    /// <summary>Converts <c>text[]</c> elements into a <see cref="PgHalfVector"/> array.</summary>
    /// <param name="values">The element texts.</param>
    public static PgHalfVector[] ToHalfVector(string?[] values) => Map(values, PgHalfVector.Parse, "halfvec");

    /// <summary>Converts <c>text[]</c> elements into a <see cref="PgSparseVector"/> array.</summary>
    /// <param name="values">The element texts.</param>
    public static PgSparseVector[] ToSparseVector(string?[] values) => Map(values, PgSparseVector.Parse, "sparsevec");

    /// <summary>Converts <c>text[]</c> elements into a <see cref="PgBit"/> array.</summary>
    /// <param name="values">The element texts.</param>
    public static PgBit[] ToBit(string?[] values) => Map(values, PgBit.Parse, "bit");

    /// <summary>Gets the converter for an element type, or <see langword="null"/> when it is not pgvector-backed.</summary>
    /// <param name="elementType">The CLR array element type.</param>
    public static MethodInfo? ParseMethodFor(Type elementType) => (Nullable.GetUnderlyingType(elementType) ?? elementType) switch
    {
        var type when type == typeof(PgVector) => typeof(PostgresVectorArrays).GetMethod(nameof(ToVector)),
        var type when type == typeof(PgHalfVector) => typeof(PostgresVectorArrays).GetMethod(nameof(ToHalfVector)),
        var type when type == typeof(PgSparseVector) => typeof(PostgresVectorArrays).GetMethod(nameof(ToSparseVector)),
        var type when type == typeof(PgBit) => typeof(PostgresVectorArrays).GetMethod(nameof(ToBit)),
        _ => null
    };

    private static T[] Map<T>(string?[] values, Func<string, T> parse, string typeName)
    {
        var result = new T[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var text = values[i] ?? throw new InvalidOperationException(
                $"A PostgreSQL '{typeName}' array contains a NULL element at position {i}, which a CLR array of {typeof(T).Name} cannot represent.");
            result[i] = parse(text);
        }
        return result;
    }
}
