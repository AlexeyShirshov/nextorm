using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// SQL literal helpers shared by the expression visitors: string literal quoting and LIKE-wildcard
/// escaping. Free of visitor state so they are trivially reusable and testable.
/// </summary>
internal static class SqlLiteral
{
    /// <summary>Quotes a value as a SQL string literal, doubling embedded single quotes.</summary>
    internal static string ToSqlStringLiteral(string value) => "'" + value.Replace("'", "''") + "'";

    /// <summary>
    /// Validates a text value the PostgreSQL provider cannot represent: <c>U+0000</c> is rejected (the
    /// server terminates text at NUL) and so is an unpaired UTF-16 surrogate, which is outside the
    /// supported-content range. Shared by inline literal rendering and by the pre-execution parameter
    /// check; the message never echoes the supplied value.
    /// </summary>
    internal static void ValidatePostgresText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Contains('\u0000'))
            throw new NotSupportedException(
                "PostgreSQL text values cannot contain U+0000 (NUL); the value was rejected before it could reach the server.");

        if (!IsWellFormedUtf16(value))
            throw UnsupportedSurrogate("PostgreSQL");
    }

    /// <summary>
    /// Creates the deterministic failure for a value that cannot be encoded as UTF-8 because of an
    /// unpaired UTF-16 surrogate. The message names the provider and never echoes the value.
    /// </summary>
    internal static NotSupportedException UnsupportedSurrogate(string provider) => new(
        $"{provider} cannot represent a string that contains an unpaired UTF-16 surrogate outside the supported range.");

    private static bool IsWellFormedUtf16(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                    return false;

                i++;
            }
            else if (char.IsLowSurrogate(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Escapes the LIKE metacharacters (<c>%</c>, <c>_</c>, <c>\</c>) with a backslash.</summary>
    internal static string EscapeLikeWildcards(string value)
    {
        if (value.IndexOfAny(['%', '_', '\\']) < 0)
            return value;

        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    /// <summary>Reads a compile-time <c>string</c>/<c>char</c> constant; any other expression returns <c>false</c>.</summary>
    internal static bool TryGetConstantString(Expression expression, out string value)
    {
        switch (expression)
        {
            case ConstantExpression { Value: string s }:
                value = s;
                return true;
            case ConstantExpression { Value: char c }:
                value = c.ToString();
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }
}
