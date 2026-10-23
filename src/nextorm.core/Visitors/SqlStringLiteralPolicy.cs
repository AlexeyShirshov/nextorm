using System.Globalization;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// The syntactic role a string literal plays in the generated statement. A value can be transported as
/// a plain literal while a grammar token (a keyword-like operand such as a padding character) or a
/// pattern (LIKE/regular expression) has to keep a different spelling on some providers.
/// </summary>
internal enum SqlStringLiteralRole
{
    /// <summary>An ordinary string/char value compared, inserted or returned.</summary>
    Value,
    /// <summary>A string that the grammar requires as a literal token rather than a value expression.</summary>
    GrammarToken,
    /// <summary>A <c>LIKE</c> pattern (wildcards already escaped by the caller).</summary>
    LikePattern,
    /// <summary>A regular-expression pattern.</summary>
    RegexPattern,
    /// <summary>A regular-expression replacement.</summary>
    RegexReplacement,
}

/// <summary>
/// Grammar that constrains the spelling of a string literal on a provider: where a plain token is
/// mandatory, the role alone is not enough. Unused on providers whose policy is role-uniform.
/// </summary>
internal enum SqlStringGrammar
{
    /// <summary>No grammar constraint beyond the role.</summary>
    None,
    /// <summary>A <c>LIKE ... ESCAPE</c> clause argument.</summary>
    LikeEscape,
    /// <summary>A column definition (DDL type/default spelling).</summary>
    ColumnDefinition,
    /// <summary>
    /// A grammar word the provider's parser consumes as a token rather than an expression: a
    /// date-part/unit name, a sequence/object identifier, a collation or an ordering keyword. Only the
    /// historic plain quoted token is legal here; a value spelling (<c>N'…'</c>, <c>E'…'</c>, a
    /// charset-introduced hex literal) would be parsed as a value instead.
    /// </summary>
    Keyword,
    /// <summary>A JSON document.</summary>
    JsonDocument,
    /// <summary>A JSON object key.</summary>
    JsonKey,
    /// <summary>A JSON path element.</summary>
    JsonPathElement,
    /// <summary>A padding character (<c>LPAD</c>/<c>RPAD</c>).</summary>
    PaddingCharacter,
}

/// <summary>
/// Immutable, provider-specific policy for rendering a string as a SQL literal. Each provider instance
/// encodes exactly the spelling measured for that engine; <see cref="Legacy"/> reproduces the historic
/// apostrophe-doubling helper and is the default for custom/user dialects, so their SQL does not change.
/// <para>
/// Hex payloads are UTF-8 produced by <see cref="UTF8Encoding"/>(<c>false</c>, <c>true</c>): an unpaired
/// UTF-16 surrogate fails deterministically instead of being replaced. Unpaired surrogates are outside
/// the supported-content guarantee of this seam.
/// </para>
/// </summary>
internal sealed class SqlStringLiteralPolicy
{
    /// <summary>Strict UTF-8: throws on an unpaired surrogate instead of emitting U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The historic apostrophe-doubling policy; the default for custom/user dialects.</summary>
    internal static readonly SqlStringLiteralPolicy Legacy = new(Family.Legacy);
    /// <summary>SQLite: apostrophe doubling, NUL escaped as a UTF-8 hex blob cast to text.</summary>
    internal static readonly SqlStringLiteralPolicy Sqlite = new(Family.Sqlite);
    /// <summary>SQL Server: always <c>N'…'</c>, NUL expressed through <c>NCHAR(0)</c> concatenation.</summary>
    internal static readonly SqlStringLiteralPolicy SqlServer = new(Family.SqlServer);
    /// <summary>PostgreSQL: <c>E'…'</c> with backslash escapes; NUL is rejected.</summary>
    internal static readonly SqlStringLiteralPolicy Postgres = new(Family.Postgres);
    /// <summary>MySQL (MariaDB reuses it): <c>_utf8mb4 X'…'</c> values, backslash-escaped patterns.</summary>
    internal static readonly SqlStringLiteralPolicy MySql = new(Family.MySql);
    /// <summary>ClickHouse: apostrophe and backslash escapes plus fixed-width C0 hex escapes.</summary>
    internal static readonly SqlStringLiteralPolicy ClickHouse = new(Family.ClickHouse);

    private enum Family
    {
        Legacy,
        Sqlite,
        SqlServer,
        Postgres,
        MySql,
        ClickHouse,
    }

    private readonly Family _family;

    private SqlStringLiteralPolicy(Family family) => _family = family;

    /// <summary>
    /// Renders <paramref name="value"/> as a SQL string literal for this policy. The role and grammar
    /// only change the spelling where the provider actually distinguishes them; everything else is
    /// role-uniform.
    /// </summary>
    internal string Render(
        string value,
        SqlStringLiteralRole role = SqlStringLiteralRole.Value,
        SqlStringGrammar grammar = SqlStringGrammar.None)
    {
        ArgumentNullException.ThrowIfNull(value);

        // A grammar position can forbid the ordinary value spelling: the grammar dispatch answers
        // "is this spelling legal here" per provider, everything else is the shared value policy.
        return grammar switch
        {
            // PostgreSQL 17 was measured live to accept an E'…' operand (D1b `like-escape-E-string-hit`
            // returns true); every other provider keeps the historic plain quoted token, because
            // SQL Server's N'…' spelling in ESCAPE is unproven and ClickHouse LIKE lowering is D6.
            SqlStringGrammar.LikeEscape => RenderLikeEscapeToken(value),
            // The column-definition list is a quoted token of the ClickHouse `values` table function,
            // not a value expression; its mode-aware lowering is D6.
            SqlStringGrammar.ColumnDefinition => RenderPlainToken(value),
            // A grammar word (date-part name, sequence identifier, …) is consumed by the parser, so it
            // keeps the historic plain quoted token on every provider.
            SqlStringGrammar.Keyword => RenderPlainToken(value),
            // PostgreSQL's JSON positions are ordinary expression positions (cast(… as jsonb),
            // cast(… as text), array[…]); the JSON translator only runs on PostgreSQL.
            SqlStringGrammar.JsonDocument or SqlStringGrammar.JsonKey or SqlStringGrammar.JsonPathElement =>
                RenderJsonToken(value),
            // The padding character is a function argument (lpad/rpad/repeat/replicate), so an
            // expression is legal on every provider that supports padding.
            _ => RenderValue(value, role),
        };
    }

    private string RenderValue(string value, SqlStringLiteralRole role)
    {
        // The PostgreSQL content check is role-independent and runs before any spelling decision.
        if (_family == Family.Postgres)
            SqlLiteral.ValidatePostgresText(value);

        if (role is SqlStringLiteralRole.RegexPattern or SqlStringLiteralRole.RegexReplacement)
            return RenderRegex(value, role);

        return _family switch
        {
            Family.Sqlite => RenderSqlite(value),
            Family.SqlServer => RenderSqlServer(value),
            Family.Postgres => RenderPostgres(value),
            Family.MySql => RenderMySql(value),
            Family.ClickHouse => RenderClickHouse(value),
            _ => RenderLegacy(value),
        };
    }

    /// <summary>
    /// A regular-expression argument. The pattern/replacement is opaque text the server's regex engine
    /// parses, so the historic quoted spelling of each provider is kept: PostgreSQL, SQLite and SQL
    /// Server never backslash-escaped it (apostrophe doubling only), MySQL/MariaDB and ClickHouse
    /// always did. Only the ClickHouse pattern additionally gains the provider's fixed-width C0 hex
    /// escapes, which RE2 reads as the same byte (<c>\xNN</c> is not a replacement escape, so the
    /// replacement keeps its historic spelling).
    /// </summary>
    private string RenderRegex(string value, SqlStringLiteralRole role)
    {
        return _family switch
        {
            Family.MySql => RenderQuotedBackslashEscaped(value),
            Family.ClickHouse when role == SqlStringLiteralRole.RegexPattern => RenderClickHouse(value),
            Family.ClickHouse => RenderQuotedBackslashEscaped(value),
            _ => RenderLegacy(value),
        };
    }

    /// <summary>The <c>LIKE … ESCAPE</c> operand: measured legal as <c>E'…'</c> on PostgreSQL only.</summary>
    private string RenderLikeEscapeToken(string value) =>
        _family == Family.Postgres ? RenderPostgresValidated(value) : RenderPlainToken(value);

    /// <summary>A JSON document/key/path element: an expression position, PostgreSQL-only translator.</summary>
    private string RenderJsonToken(string value) =>
        _family == Family.Postgres ? RenderPostgresValidated(value) : RenderPlainToken(value);

    private string RenderPostgresValidated(string value)
    {
        SqlLiteral.ValidatePostgresText(value);
        return RenderPostgres(value);
    }

    /// <summary>The historic apostrophe-doubled token, with no provider-specific spelling.</summary>
    private static string RenderPlainToken(string value) => RenderLegacy(value);

    /// <summary>Today's behaviour exactly: apostrophes doubled, nothing else.</summary>
    private static string RenderLegacy(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('\'');
        AppendApostropheDoubled(builder, value);
        builder.Append('\'');
        return builder.ToString();
    }

    private static string RenderSqlite(string value)
    {
        // SQLite has no backslash escape and truncates a text literal at an embedded NUL, so a
        // NUL-bearing value is transported as a UTF-8 blob and cast back to text.
        if (value.Contains('\0'))
            return "CAST(X'" + ToUtf8Hex(value, "SQLite") + "' AS TEXT)";

        return RenderLegacy(value);
    }

    private static string RenderSqlServer(string value)
    {
        // SQL Server text literals are always Unicode (N-prefixed) and an embedded NUL truncates the
        // literal, so NUL is spliced in as NCHAR(0) between the surviving text segments.
        if (value.Length == 0)
            return "N''";

        if (!value.Contains('\0'))
        {
            var single = new StringBuilder(value.Length + 3);
            single.Append("N'");
            AppendApostropheDoubled(single, value);
            single.Append('\'');
            return single.ToString();
        }

        var builder = new StringBuilder(value.Length * 2);
        var first = true;
        var start = 0;
        while (start <= value.Length)
        {
            var end = value.IndexOf('\0', start);
            if (end < 0)
                end = value.Length;

            if (end > start)
            {
                if (!first)
                    builder.Append(" + ");

                first = false;
                builder.Append("N'");
                AppendApostropheDoubled(builder, value.AsSpan(start, end - start));
                builder.Append('\'');
            }

            if (end == value.Length)
                break;

            if (!first)
                builder.Append(" + ");

            first = false;
            builder.Append("NCHAR(0)");
            start = end + 1;
        }

        return builder.ToString();
    }

    private static string RenderPostgres(string value)
    {
        // Explicit E'…' so the escape spelling does not depend on standard_conforming_strings: a
        // backslash is always doubled, an apostrophe is backslash-escaped, and every other C0
        // character uses the fixed-width \xNN form.
        var builder = new StringBuilder(value.Length + 3);
        builder.Append("E'");
        foreach (var c in value)
        {
            if (c == '\\')
                builder.Append("\\\\");
            else if (c == '\'')
                builder.Append("\\'");
            else if (c is >= '\u0001' and <= '\u001f')
                builder.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
            else
                builder.Append(c);
        }

        builder.Append('\'');
        return builder.ToString();
    }

    private static string RenderMySql(string value)
    {
        // A value expression: the charset-introduced hex literal is independent of NO_BACKSLASH_ESCAPES
        // and of driver escaping. Regex arguments take the quoted spelling in RenderRegex.
        return "_utf8mb4 X'" + ToUtf8Hex(value, "MySQL") + "'";
    }

    private static string RenderClickHouse(string value)
    {
        // ClickHouse honours backslash escapes in single-quoted literals; the regex renderers use the
        // same spelling, so the role does not change anything here.
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('\'');
        foreach (var c in value)
        {
            if (c == '\\')
                builder.Append("\\\\");
            else if (c == '\'')
                builder.Append("\\'");
            else if (c <= '\u001f')
                builder.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
            else
                builder.Append(c);
        }

        builder.Append('\'');
        return builder.ToString();
    }

    /// <summary>Quotes with apostrophe doubling and backslashes doubled (the historic pattern helper).</summary>
    private static string RenderQuotedBackslashEscaped(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('\'');
        foreach (var c in value)
        {
            if (c == '\\')
                builder.Append("\\\\");
            else if (c == '\'')
                builder.Append("''");
            else
                builder.Append(c);
        }

        builder.Append('\'');
        return builder.ToString();
    }

    private static void AppendApostropheDoubled(StringBuilder builder, ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (c == '\'')
                builder.Append('\'');

            builder.Append(c);
        }
    }

    /// <summary>Lowercase UTF-8 hex of <paramref name="value"/>; an unpaired surrogate is rejected.</summary>
    private static string ToUtf8Hex(string value, string provider)
    {
        byte[] bytes;
        try
        {
            bytes = StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException)
        {
            throw SqlLiteral.UnsupportedSurrogate(provider);
        }

        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));

        return builder.ToString();
    }
}

/// <summary>
/// Resolves a dialect's string-literal policy from its verified concrete type identity, so Core never
/// references a provider assembly (that reference would be circular). Only the six built-in dialects are
/// mapped; every other dialect — a custom dialect or a user subclass of a built-in one — keeps
/// <see cref="SqlStringLiteralPolicy.Legacy"/> unchanged.
/// <para>
/// The unsealed built-ins (<c>PostgresDialect</c>, <c>MySqlDialect</c>, <c>ClickHouseDialect</c>) are
/// extended by external consumers through their public constructors (see
/// <c>tests/nextorm.publicextensibility.tests</c>), and that subclass is not covered by the measured
/// built-in policy, so resolution is deliberately exact and never walks the base-type chain. Identity is
/// the assembly name plus the full type name, so a same-named type from another assembly is not mistaken
/// for a built-in either.
/// </para>
/// </summary>
internal static class SqlStringLiteralPolicies
{
    /// <summary>
    /// Returns the policy for <paramref name="dialect"/>, or <see cref="SqlStringLiteralPolicy.Legacy"/>
    /// when the concrete type is not one of the built-in dialects.
    /// </summary>
    internal static SqlStringLiteralPolicy For(ISqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(dialect);

        var type = dialect.GetType();
        return (type.Assembly.GetName().Name, type.FullName) switch
        {
            ("nextorm.sqlite", "NextORM.Sqlite.SqliteDialect") => SqlStringLiteralPolicy.Sqlite,
            ("nextorm.sqlserver", "NextORM.SqlServer.SqlServerDialect") => SqlStringLiteralPolicy.SqlServer,
            ("nextorm.postgres", "NextORM.Postgres.PostgresDialect") => SqlStringLiteralPolicy.Postgres,
            ("nextorm.mysql", "NextORM.MySql.MySqlDialect") => SqlStringLiteralPolicy.MySql,
            // MariaDB reuses the MySQL literal policy (see the plan's provider policy table) and its
            // dialect is sealed, so the exact-type mapping is stable.
            ("nextorm.mariadb", "NextORM.MariaDb.MariaDbDialect") => SqlStringLiteralPolicy.MySql,
            ("nextorm.clickhouse", "NextORM.ClickHouse.ClickHouseDialect") => SqlStringLiteralPolicy.ClickHouse,
            _ => SqlStringLiteralPolicy.Legacy,
        };
    }
}
