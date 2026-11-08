using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Pre-execution guard for PostgreSQL parameter values (D5). The server terminates text at
/// <c>U+0000</c>, so a final value that became NUL-bearing only at execution time (a captured value, a
/// converter result or a cache refresh) must be rejected with the same deterministic
/// <see cref="NotSupportedException"/> the translation-time literal check raises, instead of reaching
/// the driver and failing there.
/// <para>
/// The guard is a plain scan of the final values: <c>null</c>/<see cref="DBNull"/> are skipped and only
/// <see cref="string"/> and <see cref="char"/> values can fail. It allocates nothing per value; the
/// exception is created only on the failing path and never echoes the supplied value.
/// </para>
/// <para>
/// Every seam passes the dialect's resolved <see cref="SqlStringLiteralPolicy"/>; the guard is inert for
/// every non-PostgreSQL policy, so SQLite, SQL Server, MySQL, MariaDB and ClickHouse are unaffected.
/// </para>
/// </summary>
internal static class PostgresParameterGuard
{
    /// <summary>Whether <paramref name="policy"/> is the PostgreSQL literal policy (the guard applies).</summary>
    internal static bool IsPostgres(SqlStringLiteralPolicy? policy) =>
        ReferenceEquals(policy, SqlStringLiteralPolicy.Postgres);

    /// <summary>Validates the final values of a rendered statement's parameter list.</summary>
    /// <param name="policy">The dialect's string-literal policy.</param>
    /// <param name="parameters">The parameters that would be bound to the driver.</param>
    internal static void Validate(SqlStringLiteralPolicy? policy, IReadOnlyList<Parameter> parameters)
    {
        if (!IsPostgres(policy))
            return;

        for (var i = 0; i < parameters.Count; i++)
            ValidateValue(parameters[i].Value);
    }

    /// <summary>Validates the final values already bound to a command's parameter collection.</summary>
    /// <param name="policy">The dialect's string-literal policy.</param>
    /// <param name="parameters">The driver-visible parameter collection.</param>
    internal static void Validate(SqlStringLiteralPolicy? policy, DbParameterCollection parameters)
    {
        if (!IsPostgres(policy))
            return;

        for (var i = 0; i < parameters.Count; i++)
            ValidateValue(parameters[i].Value);
    }

    /// <summary>Validates one final value: only a NUL inside text is rejected; everything else passes.</summary>
    /// <param name="value">The converted, final provider-bound value.</param>
    internal static void ValidateValue(object? value)
    {
        switch (value)
        {
            case string text:
                if (text.Contains('\0'))
                    throw SqlLiteral.PostgresNul();
                break;
            case char character:
                if (character == '\0')
                    throw SqlLiteral.PostgresNul();
                break;
        }
    }
}
