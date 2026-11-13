using System.Text;

namespace NextORM.Postgres;

/// <summary>
/// Maps the provider-owned pgvector CLR types to their PostgreSQL type names and text parameter
/// representation. The types are deliberately not mapped through a data-source plugin: values travel
/// as PostgreSQL text, so a plain <c>NpgsqlConnection</c> works without <c>UseVector()</c>.
/// </summary>
internal static class PostgresVectorTypes
{
    /// <summary>True for a pgvector-backed CLR scalar, with nullability stripped.</summary>
    /// <param name="type">The CLR type to inspect.</param>
    public static bool IsVectorType(Type type) => NameOf(type) is not null;

    /// <summary>True for a one-dimensional array whose element type is a pgvector-backed scalar.</summary>
    /// <param name="type">The CLR type to inspect.</param>
    public static bool IsVectorArray(Type type)
        => type.IsArray && type.GetElementType() is { } element && IsVectorType(element);

    /// <summary>Gets the PostgreSQL type name for a pgvector-backed type, its array, or <see langword="null"/>.</summary>
    /// <param name="type">The CLR type to name.</param>
    public static string? NameFor(Type type)
    {
        if (type.IsArray && type.GetElementType() is { } element && NameOf(element) is { } elementName)
            return elementName + "[]";
        return NameOf(type);
    }

    /// <summary>
    /// Renders a pgvector-backed value as PostgreSQL text: the scalar text form, or an array literal
    /// with quoted elements and <c>NULL</c> for a null element.
    /// </summary>
    /// <param name="value">A pgvector-backed scalar or array value.</param>
    public static string ToParameterText(object value)
    {
        if (value is not Array array)
            return value.ToString() ?? string.Empty;

        var result = new StringBuilder("{");
        for (var i = 0; i < array.Length; i++)
        {
            if (i != 0) result.Append(',');
            if (array.GetValue(i) is not { } element)
            {
                result.Append("NULL");
                continue;
            }
            result.Append('"')
                .Append((element.ToString() ?? string.Empty)
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal))
                .Append('"');
        }
        return result.Append('}').ToString();
    }

    private static string? NameOf(Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var candidate when candidate == typeof(PgVector) => "vector",
        var candidate when candidate == typeof(PgHalfVector) => "halfvec",
        var candidate when candidate == typeof(PgSparseVector) => "sparsevec",
        var candidate when candidate == typeof(PgBit) => "bit",
        _ => null
    };
}
