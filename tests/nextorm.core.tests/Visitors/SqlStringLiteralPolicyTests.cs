using System.Text;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.SqlServer;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// #327 D7: the provider string-literal policy is an internal seam, and the six shape suites only
/// observe it indirectly. These tests pin the policy itself: the exact-type dialect resolution, the
/// exact spelling of every role and grammar position, and the fact that each spelling decodes back to
/// the original value (so the representation is a transport, not an approximation). No database and no
/// planner wiring is involved.
/// <para>
/// The round-trip oracle decodes the produced literal with a family-specific decoder written here; it
/// never re-encodes with the production code, so a defect in the policy cannot hide behind a mirrored
/// helper. Exact spellings are pinned separately as literals for the hostile cases.
/// </para>
/// <para>
/// <b>Reported, not asserted as rejection:</b> the two parser-consumed grammar positions
/// (<see cref=""keyword""/>, <see cref=""column-definition""/>) deliberately
/// keep the historic plain quoted token on every provider and never run the PostgreSQL content check.
/// On PostgreSQL they are unreachable with user text (the only <c>Keyword</c> emitters are the MySQL
/// and SQL Server translators, and <see cref=""column-definition""/> has no PostgreSQL
/// emitter), so this is recorded as a cell whose intent is "grammar word, not user data" rather than a
/// missing value check.
/// </para>
/// <para>
/// Unpaired UTF-16 surrogates are outside the supported-content guarantee of this seam; only the
/// deterministic encoders that reject them (PostgreSQL validation, the MySQL UTF-8 hex transport and
/// the SQLite NUL blob transport) are asserted.
/// </para>
/// </summary>
public class SqlStringLiteralPolicyTests
{
    private static readonly (string Name, ISqlDialect Dialect)[] Providers =
    [
        ("legacy", CustomDialect.Instance),
        ("sqlite", SqliteDialect.Instance),
        ("sqlserver", SqlServerDialect.Instance),
        ("postgres", PostgresDialect.Instance),
        ("mysql", MySqlDialect.Instance),
        ("mariadb", MariaDbDialect.Instance),
        ("clickhouse", ClickHouseDialect.Instance),
    ];

    private const string HostileValue = "it's";

    // The corpus every provider must transport and decode back, excluding U+0000 (PostgreSQL rejects
    // NUL and the NUL-bearing values are tested separately per provider).
    private static readonly string[] ValueCorpus =
    [
        "plain",
        string.Empty,
        " ",
        "a  ",
        "it's",
        "a''b",
        @"a\b",
        @"\'",
        @"\\'",
        "a\\",
        @"it's; --",
        "x'); SELECT 1; --",
        "%_",
        "\u0001",
        "\u001f",
        "\t\r\n",
        "Привет",
        "😀",
    ];

    private static readonly string[] NulCorpus = ["\0", "a\0b", "\0tail", "head\0"];

    // ------------------------------------------------------------------------------------------------
    // 1. Dialect -> policy resolution (the D2 exact-type rule)
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void For_Should_Map_The_Six_BuiltIn_Dialects_To_Their_Own_Policy()
    {
        SqlStringLiteralPolicies.For(SqliteDialect.Instance).Should().BeSameAs(SqlStringLiteralPolicy.Sqlite);
        SqlStringLiteralPolicies.For(SqlServerDialect.Instance).Should().BeSameAs(SqlStringLiteralPolicy.SqlServer);
        SqlStringLiteralPolicies.For(PostgresDialect.Instance).Should().BeSameAs(SqlStringLiteralPolicy.Postgres);
        SqlStringLiteralPolicies.For(MySqlDialect.Instance).Should().BeSameAs(SqlStringLiteralPolicy.MySql);
        SqlStringLiteralPolicies.For(MariaDbDialect.Instance).Should().BeSameAs(SqlStringLiteralPolicy.MySql,
            "MariaDB reuses the MySQL literal policy");
        SqlStringLiteralPolicies.For(ClickHouseDialect.Instance).Should().BeSameAs(SqlStringLiteralPolicy.ClickHouse);
    }

    [Fact]
    public void For_Should_Fall_Back_To_Legacy_For_An_Unrelated_Custom_Dialect()
    {
        SqlStringLiteralPolicies.For(CustomDialect.Instance).Should().BeSameAs(SqlStringLiteralPolicy.Legacy,
            "only the six built-in concrete dialects are mapped");
        SqlStringLiteralPolicy.Legacy.Render(HostileValue).Should().Be("'it''s'");
    }

    [Fact]
    public void For_Should_Not_Walk_The_Base_Type_Chain_Of_An_Unsealed_BuiltIn()
    {
        var derived = new PostgresDerivedDialect();

        SqlStringLiteralPolicies.For(derived).Should().BeSameAs(SqlStringLiteralPolicy.Legacy,
            "a user subclass of a built-in dialect is not covered by the measured built-in policy");
        SqlStringLiteralPolicy.Legacy.Render(HostileValue).Should().Be("'it''s'",
            "the subclass keeps the historic apostrophe-doubling spelling, not the PostgreSQL E-string");
    }

    [Fact]
    public void For_Should_Reject_A_Null_Dialect()
    {
        FluentActions.Invoking(() => SqlStringLiteralPolicies.For(null!))
            .Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------------------------------------
    // 2. Value role: exact spellings and a family-specific round trip
    // ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("legacy", "it's", "'it''s'")]
    [InlineData("legacy", @"a\b", @"'a\b'")]
    [InlineData("legacy", "", "''")]
    [InlineData("sqlite", "it's", "'it''s'")]
    [InlineData("sqlite", @"a\b", @"'a\b'")]
    [InlineData("sqlite", "a\0b", "CAST(X'610062' AS TEXT)")]
    [InlineData("sqlite", "\0", "CAST(X'00' AS TEXT)")]
    [InlineData("sqlserver", "it's", "N'it''s'")]
    [InlineData("sqlserver", @"a\b", @"N'a\b'")]
    [InlineData("sqlserver", "", "N''")]
    [InlineData("sqlserver", "a\0b", "N'a' + NCHAR(0) + N'b'")]
    [InlineData("sqlserver", "\0", "NCHAR(0)")]
    [InlineData("sqlserver", "a\0", "N'a' + NCHAR(0)")]
    [InlineData("sqlserver", "\0a", "NCHAR(0) + N'a'")]
    [InlineData("postgres", "it's", @"E'it\'s'")]
    [InlineData("postgres", @"a\b", @"E'a\\b'")]
    [InlineData("postgres", @"\'", @"E'\\\''")]
    [InlineData("postgres", "a\\", @"E'a\\'")]
    [InlineData("postgres", "\u0001", @"E'\x01'")]
    [InlineData("postgres", "\u001f", @"E'\x1f'")]
    [InlineData("postgres", "", "E''")]
    [InlineData("postgres", "😀", "E'😀'")]
    [InlineData("mysql", "it's", "_utf8mb4 X'69742773'")]
    [InlineData("mysql", @"\'", "_utf8mb4 X'5c27'")]
    [InlineData("mysql", "a\\", "_utf8mb4 X'615c'")]
    [InlineData("mysql", "", "_utf8mb4 X''")]
    [InlineData("mysql", "\0", "_utf8mb4 X'00'")]
    [InlineData("mariaDb", "it's", "_utf8mb4 X'69742773'")]
    [InlineData("mariaDb", "😀", "_utf8mb4 X'f09f9880'")]
    [InlineData("clickhouse", "it's", @"'it\'s'")]
    [InlineData("clickhouse", @"a\b", @"'a\\b'")]
    [InlineData("clickhouse", @"\'", @"'\\\''")]
    [InlineData("clickhouse", "", "''")]
    [InlineData("clickhouse", "\0", @"'\x00'")]
    [InlineData("clickhouse", "\u0001", @"'\x01'")]
    [InlineData("clickhouse", "\u001f", @"'\x1f'")]
    [InlineData("clickhouse", "\t", @"'\x09'")]
    public void ValueRole_Should_Render_The_Exact_Intended_Spelling(string provider, string value, string expected)
    {
        Policy(provider).Render(value).Should().Be(expected);
    }

    [Fact]
    public void ValueRole_Should_Round_Trip_The_Whole_Corpus_On_Every_Provider()
    {
        foreach (var (name, _) in Providers)
        {
            foreach (var value in ValueCorpus)
            {
                var literal = Policy(name).Render(value);

                ShapeOf(name, literal);
                Decode(name, literal).Should().Be(value,
                    $"{name} must transport the value as data that decodes back exactly (literal: {literal})");
            }
        }
    }

    [Fact]
    public void ValueRole_Should_Round_Trip_Nul_Bearing_Values_Where_They_Are_Supported()
    {
        foreach (var name in new[] { "legacy", "sqlite", "sqlserver", "mysql", "mariaDb", "clickhouse" })
        {
            foreach (var value in NulCorpus)
            {
                var literal = Policy(name).Render(value);

                Decode(name, literal).Should().Be(value,
                    $"{name} must transport an embedded NUL without truncating (literal: {literal})");
            }
        }
    }

    [Fact]
    public void ValueRole_Should_Reject_Nul_On_PostgreSQL()
    {
        var exception = FluentActions.Invoking(() => SqlStringLiteralPolicy.Postgres.Render("a\0b"))
            .Should().Throw<NotSupportedException>().Which;

        exception.Message.Should().Contain("PostgreSQL").And.Contain("U+0000");
        exception.Message.Should().NotContain("a\0b");
    }

    // ------------------------------------------------------------------------------------------------
    // 3. Grammar tokens: which position gets which spelling
    // ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("legacy", "like-escape", "'it''s'")]
    [InlineData("sqlite", "like-escape", "'it''s'")]
    [InlineData("sqlserver", "like-escape", "'it''s'")]
    [InlineData("postgres", "like-escape", @"E'it\'s'")]
    [InlineData("mysql", "like-escape", "_utf8mb4 X'69742773'")]
    [InlineData("mariaDb", "like-escape", "_utf8mb4 X'69742773'")]
    [InlineData("clickhouse", "like-escape", "'it''s'")]
    [InlineData("legacy", "column-definition", "'it''s'")]
    [InlineData("sqlite", "column-definition", "'it''s'")]
    [InlineData("sqlserver", "column-definition", "'it''s'")]
    [InlineData("postgres", "column-definition", "'it''s'")]
    [InlineData("mysql", "column-definition", "'it''s'")]
    [InlineData("mariaDb", "column-definition", "'it''s'")]
    [InlineData("clickhouse", "column-definition", "'it''s'")]
    [InlineData("legacy", "keyword", "'it''s'")]
    [InlineData("sqlite", "keyword", "'it''s'")]
    [InlineData("sqlserver", "keyword", "'it''s'")]
    [InlineData("postgres", "keyword", "'it''s'")]
    [InlineData("mysql", "keyword", "'it''s'")]
    [InlineData("mariaDb", "keyword", "'it''s'")]
    [InlineData("clickhouse", "keyword", "'it''s'")]
    [InlineData("legacy", "json-document", "'it''s'")]
    [InlineData("sqlite", "json-document", "'it''s'")]
    [InlineData("sqlserver", "json-document", "'it''s'")]
    [InlineData("postgres", "json-document", @"E'it\'s'")]
    [InlineData("mysql", "json-document", "'it''s'")]
    [InlineData("mariaDb", "json-document", "'it''s'")]
    [InlineData("clickhouse", "json-document", "'it''s'")]
    [InlineData("legacy", "json-key", "'it''s'")]
    [InlineData("sqlite", "json-key", "'it''s'")]
    [InlineData("sqlserver", "json-key", "'it''s'")]
    [InlineData("postgres", "json-key", @"E'it\'s'")]
    [InlineData("mysql", "json-key", "'it''s'")]
    [InlineData("mariaDb", "json-key", "'it''s'")]
    [InlineData("clickhouse", "json-key", "'it''s'")]
    [InlineData("legacy", "json-path-element", "'it''s'")]
    [InlineData("sqlite", "json-path-element", "'it''s'")]
    [InlineData("sqlserver", "json-path-element", "'it''s'")]
    [InlineData("postgres", "json-path-element", @"E'it\'s'")]
    [InlineData("mysql", "json-path-element", "'it''s'")]
    [InlineData("mariaDb", "json-path-element", "'it''s'")]
    [InlineData("clickhouse", "json-path-element", "'it''s'")]
    public void GrammarRole_Should_Pin_The_Position_Spelling(string provider, string grammar, string expected)
    {
        Policy(provider)
            .Render(HostileValue, SqlStringLiteralRole.GrammarToken, Grammar(grammar))
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("legacy", "' '")]
    [InlineData("sqlite", "' '")]
    [InlineData("sqlserver", "N' '")]
    [InlineData("postgres", "E' '")]
    [InlineData("mysql", "_utf8mb4 X'20'")]
    [InlineData("mariaDb", "_utf8mb4 X'20'")]
    [InlineData("clickhouse", "' '")]
    public void PaddingCharacter_Should_Use_The_Provider_Value_Spelling(string provider, string expected)
    {
        Policy(provider)
            .Render(" ", SqlStringLiteralRole.GrammarToken, Grammar("padding-character"))
            .Should().Be(expected,
                "the padding character is a function argument, so the provider value expression is legal");
    }

    [Fact]
    public void GrammarRole_With_Grammar_None_Should_Use_The_Value_Spelling()
    {
        foreach (var (name, _) in Providers)
        {
            Policy(name).Render(HostileValue, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.None)
                .Should().Be(Policy(name).Render(HostileValue),
                    $"{name}: the role alone does not change the spelling");
        }
    }

    // ------------------------------------------------------------------------------------------------
    // 4. Regex roles
    // ------------------------------------------------------------------------------------------------

    private const string RegexInput = @"a\b'";

    [Theory]
    [InlineData("legacy", "regex-pattern", @"'a\b'''")]
    [InlineData("sqlite", "regex-pattern", @"'a\b'''")]
    [InlineData("sqlserver", "regex-pattern", @"'a\b'''")]
    [InlineData("postgres", "regex-pattern", @"'a\b'''")]
    [InlineData("mysql", "regex-pattern", @"'a\\b'''")]
    [InlineData("mariaDb", "regex-pattern", @"'a\\b'''")]
    [InlineData("clickhouse", "regex-pattern", @"'a\\b\''")]
    [InlineData("legacy", "regex-replacement", @"'a\b'''")]
    [InlineData("sqlite", "regex-replacement", @"'a\b'''")]
    [InlineData("sqlserver", "regex-replacement", @"'a\b'''")]
    [InlineData("postgres", "regex-replacement", @"'a\b'''")]
    [InlineData("mysql", "regex-replacement", @"'a\\b'''")]
    [InlineData("mariaDb", "regex-replacement", @"'a\\b'''")]
    [InlineData("clickhouse", "regex-replacement", @"'a\\b'''")]
    public void RegexRoles_Should_Keep_The_Historic_Per_Provider_Spelling(
        string provider,
        string role,
        string expected)
    {
        Policy(provider).Render(RegexInput, Role(role), SqlStringGrammar.None).Should().Be(expected);
    }

    [Fact]
    public void RegexPattern_On_ClickHouse_Should_Gain_The_Fixed_Width_C0_Escape()
    {
        SqlStringLiteralPolicy.ClickHouse
            .Render("\u0001a\tb", SqlStringLiteralRole.RegexPattern, SqlStringGrammar.None)
            .Should().Be(@"'\x01a\x09b'");
    }

    [Fact]
    public void RegexRoles_Should_Reject_Nul_On_PostgreSQL()
    {
        foreach (var role in new[] { "regex-pattern", "regex-replacement" })
        {
            FluentActions.Invoking(() => SqlStringLiteralPolicy.Postgres.Render("a\0b", Role(role)))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*PostgreSQL*U+0000*");
        }
    }

    // ------------------------------------------------------------------------------------------------
    // 5. PostgreSQL content validation in every value-bearing cell
    // ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("value", "none")]
    [InlineData("like-pattern", "none")]
    [InlineData("regex-pattern", "none")]
    [InlineData("regex-replacement", "none")]
    [InlineData("grammar-token", "none")]
    [InlineData("grammar-token", "like-escape")]
    [InlineData("grammar-token", "json-document")]
    [InlineData("grammar-token", "json-key")]
    [InlineData("grammar-token", "json-path-element")]
    [InlineData("grammar-token", "padding-character")]
    public void Postgres_ValueBearing_Cell_Should_Reject_Nul_Without_Echoing_The_Value(
        string role,
        string grammar)
    {
        const string value = "marker\0secret";

        var exception = FluentActions.Invoking(() =>
                SqlStringLiteralPolicy.Postgres.Render(value, Role(role), Grammar(grammar)))
            .Should().Throw<NotSupportedException>().Which;

        exception.Message.Should().Contain("PostgreSQL").And.Contain("U+0000");
        exception.Message.Should().NotContain("marker").And.NotContain("secret",
            "the rejection must not echo the supplied value");
    }

    [Theory]
    [InlineData("grammar-token", "keyword")]
    [InlineData("grammar-token", "column-definition")]
    public void Postgres_Parser_Consumed_Grammar_Token_Should_Keep_The_Plain_Token(
        string role,
        string grammar)
    {
        // Reported cell: these positions are grammar words consumed by the parser (date-part/unit names,
        // sequence identifiers, the ClickHouse DDL column list), never PostgreSQL user values, so the
        // content check does not run. The historic spelling is preserved unchanged.
        SqlStringLiteralPolicy.Postgres.Render("word", Role(role), Grammar(grammar)).Should().Be("'word'");
    }

    [Fact]
    public void Postgres_Every_Safe_C0_Character_Should_Render()
    {
        for (var code = 1; code <= 0x1f; code++)
        {
            var value = ((char)code).ToString();

            SqlStringLiteralPolicy.Postgres.Render(value).Should().Be(
                "E'\\x" + code.ToString("x2", System.Globalization.CultureInfo.InvariantCulture) + "'",
                $"U+{code:X4} is a legal PostgreSQL text character and must use the fixed-width escape");
        }
    }

    [Theory]
    [InlineData("value", "none")]
    [InlineData("like-pattern", "none")]
    [InlineData("grammar-token", "like-escape")]
    [InlineData("grammar-token", "json-document")]
    [InlineData("grammar-token", "padding-character")]
    public void Postgres_Unpaired_Surrogate_Should_Fail_Deterministically(
        string role,
        string grammar)
    {
        var exception = FluentActions.Invoking(() =>
                SqlStringLiteralPolicy.Postgres.Render("\uD800", Role(role), Grammar(grammar)))
            .Should().Throw<NotSupportedException>().Which;

        exception.Message.Should().Contain("PostgreSQL").And.Contain("unpaired UTF-16 surrogate");
        exception.Message.Should().NotContain("\uD800");
    }

    [Fact]
    public void MySQL_Unpaired_Surrogate_Should_Fail_In_The_Hex_Encoder()
    {
        var exception = FluentActions.Invoking(() => SqlStringLiteralPolicy.MySql.Render("\uD800"))
            .Should().Throw<NotSupportedException>().Which;

        exception.Message.Should().Contain("MySQL").And.Contain("unpaired UTF-16 surrogate");
        exception.Message.Should().NotContain("\uD800");
    }

    [Fact]
    public void SQLite_Unpaired_Surrogate_Should_Fail_In_The_Blob_Encoder()
    {
        // The SQLite encoder only runs for NUL-bearing values; an unpaired surrogate on the plain path is
        // outside the supported-content guarantee and is not asserted.
        var exception = FluentActions.Invoking(() => SqlStringLiteralPolicy.Sqlite.Render("\0\uD800"))
            .Should().Throw<NotSupportedException>().Which;

        exception.Message.Should().Contain("SQLite").And.Contain("unpaired UTF-16 surrogate");
        exception.Message.Should().NotContain("\uD800");
    }

    // ------------------------------------------------------------------------------------------------
    // Helpers: dialect lookup, family-specific decoders and shape assertions
    // ------------------------------------------------------------------------------------------------

    private static SqlStringLiteralRole Role(string role) => role switch
    {
        "value" => SqlStringLiteralRole.Value,
        "grammar-token" => SqlStringLiteralRole.GrammarToken,
        "like-pattern" => SqlStringLiteralRole.LikePattern,
        "regex-pattern" => SqlStringLiteralRole.RegexPattern,
        "regex-replacement" => SqlStringLiteralRole.RegexReplacement,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role key."),
    };

    private static SqlStringGrammar Grammar(string grammar) => grammar switch
    {
        "none" => SqlStringGrammar.None,
        "like-escape" => SqlStringGrammar.LikeEscape,
        "column-definition" => SqlStringGrammar.ColumnDefinition,
        "keyword" => SqlStringGrammar.Keyword,
        "json-document" => SqlStringGrammar.JsonDocument,
        "json-key" => SqlStringGrammar.JsonKey,
        "json-path-element" => SqlStringGrammar.JsonPathElement,
        "padding-character" => SqlStringGrammar.PaddingCharacter,
        _ => throw new ArgumentOutOfRangeException(nameof(grammar), grammar, "Unknown grammar key."),
    };

    private static SqlStringLiteralPolicy Policy(string provider) => provider switch
    {
        "legacy" => SqlStringLiteralPolicy.Legacy,
        "sqlite" => SqlStringLiteralPolicy.Sqlite,
        "sqlserver" => SqlStringLiteralPolicy.SqlServer,
        "postgres" => SqlStringLiteralPolicy.Postgres,
        "mysql" or "mariadb" or "mariaDb" => SqlStringLiteralPolicy.MySql,
        "clickhouse" => SqlStringLiteralPolicy.ClickHouse,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider key."),
    };

    private static void ShapeOf(string provider, string literal)
    {
        switch (provider)
        {
            case "sqlserver":
                (literal.StartsWith("N'", StringComparison.Ordinal) || literal.StartsWith("NCHAR(0)", StringComparison.Ordinal))
                    .Should().BeTrue($"SQL Server always uses a Unicode literal: {literal}");
                break;
            case "postgres":
                literal.Should().StartWith("E'", "PostgreSQL uses the explicit E-string form");
                break;
            case "mysql":
            case "mariadb":
            case "mariaDb":
                literal.Should().StartWith("_utf8mb4 X'", "the MySQL family uses the charset-introduced hex literal");
                literal.Should().EndWith("'");
                break;
            case "sqlite":
                if (literal.StartsWith("CAST(X'", StringComparison.Ordinal))
                {
                    literal.Should().EndWith("' AS TEXT)", "a NUL-bearing SQLite value is a UTF-8 blob cast to text");
                }
                else
                {
                    literal.Should().StartWith("'");
                }

                break;
            case "legacy":
                literal.Should().StartWith("'").And.NotStartWith("N'").And.NotStartWith("E'");
                break;
            case "clickhouse":
                literal.Should().StartWith("'");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider key.");
        }
    }

    private static string Decode(string provider, string literal) => provider switch
    {
        "sqlserver" => DecodeSqlServer(literal),
        "postgres" => DecodePostgres(literal),
        "mysql" or "mariadb" or "mariaDb" => DecodeMySql(literal),
        "clickhouse" => DecodeClickHouse(literal),
        "sqlite" => literal.StartsWith("CAST(X'", StringComparison.Ordinal)
            ? DecodeSqliteBlob(literal)
            : DecodeLegacy(literal),
        _ => DecodeLegacy(literal),
    };

    private static string DecodeLegacy(string literal)
    {
        if (literal.Length < 2 || literal[0] != '\'' || literal[^1] != '\'')
            throw new FormatException($"Not a plain quoted literal: {literal}");

        return literal[1..^1].Replace("''", "'", StringComparison.Ordinal);
    }

    private static string DecodeSqliteBlob(string literal)
    {
        const string prefix = "CAST(X'";
        const string suffix = "' AS TEXT)";
        if (!literal.StartsWith(prefix, StringComparison.Ordinal) || !literal.EndsWith(suffix, StringComparison.Ordinal))
            throw new FormatException($"Not a SQLite blob cast: {literal}");

        var hex = literal[prefix.Length..^suffix.Length];
        return Encoding.UTF8.GetString(Convert.FromHexString(hex));
    }

    private static string DecodeSqlServer(string literal)
    {
        if (literal == "NCHAR(0)")
            return "\0";

        var builder = new StringBuilder(literal.Length);
        var index = 0;
        var sawSegment = false;
        while (index < literal.Length)
        {
            if (literal.AsSpan(index).StartsWith("NCHAR(0)", StringComparison.Ordinal))
            {
                builder.Append('\0');
                index += "NCHAR(0)".Length;
                sawSegment = true;
            }
            else if (literal.AsSpan(index).StartsWith("N'", StringComparison.Ordinal))
            {
                index += 2;
                while (true)
                {
                    if (literal[index] == '\'')
                    {
                        if (index + 1 < literal.Length && literal[index + 1] == '\'')
                        {
                            builder.Append('\'');
                            index += 2;
                            continue;
                        }

                        index++;
                        break;
                    }

                    builder.Append(literal[index]);
                    index++;
                }

                sawSegment = true;
            }
            else if (literal.AsSpan(index).StartsWith(" + ", StringComparison.Ordinal))
            {
                index += 3;
            }
            else
            {
                throw new FormatException($"Not a SQL Server Unicode literal: {literal}");
            }
        }

        if (!sawSegment)
            throw new FormatException($"Empty SQL Server literal: {literal}");

        return builder.ToString();
    }

    private static string DecodePostgres(string literal)
    {
        if (!literal.StartsWith("E'", StringComparison.Ordinal) || !literal.EndsWith("'", StringComparison.Ordinal))
            throw new FormatException($"Not a PostgreSQL E-string literal: {literal}");

        return DecodeBackslashEscaped(literal[2..^1], fixedWidthC0: true);
    }

    private static string DecodeClickHouse(string literal)
    {
        if (literal.Length < 2 || literal[0] != '\'' || literal[^1] != '\'')
            throw new FormatException($"Not a ClickHouse quoted literal: {literal}");

        return DecodeBackslashEscaped(literal[1..^1], fixedWidthC0: true);
    }

    private static string DecodeMySql(string literal)
    {
        const string prefix = "_utf8mb4 X'";
        if (!literal.StartsWith(prefix, StringComparison.Ordinal) || !literal.EndsWith("'", StringComparison.Ordinal))
            throw new FormatException($"Not a MySQL charset-introduced hex literal: {literal}");

        return Encoding.UTF8.GetString(Convert.FromHexString(literal[prefix.Length..^1]));
    }

    private static string DecodeBackslashEscaped(string inner, bool fixedWidthC0)
    {
        var builder = new StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            i++;
            var escape = inner[i];
            switch (escape)
            {
                case '\\':
                    builder.Append('\\');
                    break;
                case '\'':
                    builder.Append('\'');
                    break;
                case 'x' when fixedWidthC0:
                    builder.Append((char)Convert.ToInt32(inner.Substring(i + 1, 2), 16));
                    i += 2;
                    break;
                default:
                    throw new FormatException($"Unsupported escape \\{escape} in {inner}");
            }
        }

        return builder.ToString();
    }

    private sealed class CustomDialect : SqlDialectBase
    {
        internal static readonly CustomDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class PostgresDerivedDialect : PostgresDialect
    {
    }
}
