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
