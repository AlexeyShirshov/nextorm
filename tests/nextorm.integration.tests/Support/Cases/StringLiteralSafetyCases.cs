using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace NextORM.Integration.Tests;

/// <summary>
/// #327 D8: one string value of the live string-literal safety corpus. <see cref="Id"/> is stable
/// and is used in the assertion messages/evidence, so a failure names the exact case instead of an
/// index. <see cref="ContainsNul"/> lets a provider branch treat U+0000 specially (PostgreSQL
/// rejects it deterministically; the other providers transport it).
/// </summary>
internal sealed record StringLiteralSafetyCase(string Id, string Value)
{
    /// <summary>True when the value carries U+0000, which PostgreSQL cannot represent.</summary>
    public bool ContainsNul => Value.Contains('\0');

    /// <summary>The exact UTF-8 bytes the provider must return.</summary>
    public byte[] Utf8Bytes => Encoding.UTF8.GetBytes(Value);
}

/// <summary>#327 D8: one single-character value of the corpus (char arm of the §8 matrix).</summary>
internal sealed record CharLiteralSafetyCase(string Id, char Value)
{
    public bool ContainsNul => Value == '\0';
}

/// <summary>
/// #327 D8: the frozen live corpus. The first 33 entries are the D1 probe strings (one per D1
/// identity), followed by every individual non-NUL C0 code point U+0001..U+001F (AC-R8 requires each
/// code point, not one representative), so the matrix cells are asserted case by case. The values are
/// data only and are never used as SQL text.
/// </summary>
internal static class StringLiteralSafetyCases
{
    /// <summary>The ordered string corpus: 33 D1 strings + 31 individual non-NUL C0 characters.</summary>
    public static IReadOnlyList<StringLiteralSafetyCase> Strings { get; } = BuildStrings();

    /// <summary>The single-character corpus (inline string and char arms).</summary>
    public static IReadOnlyList<CharLiteralSafetyCase> Chars { get; } = BuildChars();

    /// <summary>4096 Unicode characters: the SQL Server long-value acceptance value.</summary>
    public static string LongUnicode { get; } = new('ж', 4096);

    /// <summary>A 4096-character control-containing value for intermediate truncation/sizing checks.</summary>
    public static string LongControl { get; } =
        new string('x', 2048) + new string('\u0001', 2048);

    /// <summary>Every non-NUL C0 code point, U+0001..U+001F, as its own case.</summary>
    public static IReadOnlyList<StringLiteralSafetyCase> NonNulC0 { get; } =
        Strings.Where(c => c.Id.Length == 5 && c.Id[0] == 'u' && !c.ContainsNul).ToArray();

    private static IReadOnlyList<StringLiteralSafetyCase> BuildStrings()
    {
        var cases = new List<StringLiteralSafetyCase>
        {
            new("empty", ""),
            new("plain", "plain"),
            new("apostrophe", "'"),
            new("apostrophe-pair", "''"),
            new("backslash", "\\"),
            new("backslash-pair", "\\\\"),
            new("backslash-embedded", "a\\b"),
            new("backslash-apostrophe", "\\'"),
            new("backslash-pair-apostrophe", "\\\\'"),
            new("apostrophe-backslash", "'\\"),
            new("trailing-backslash", "a\\"),
            new("trailing-backslash-pair", "a\\\\"),
            new("apostrophe-word", "it's"),
            new("apostrophe-statement", "it's; --"),
            new("injection", "x'); SELECT 1; --"),
            new("nul", "\0"),
            new("nul-a-b", "a\0b"),
            new("c0-all", AllC0()),
            new("lf", "\n"),
            new("cr", "\r"),
            new("tab", "\t"),
            new("u001a", "\u001A"),
            new("percent", "%"),
            new("underscore", "_"),
            new("bang", "!"),
            new("backslash-metachars", "\\%\\_"),
            new("spaced", "a b"),
            new("trailing-space", "a "),
            new("trailing-spaces", "a  "),
            new("cyrillic", "Привет"),
            new("cjk", "漢字"),
            new("emoji", "😀"),
            new("mixed-hostile", "a\0'\\😀 "),
        };

        for (var code = 1; code <= 0x1F; code++)
            cases.Add(new($"u{code:x4}", ((char)code).ToString()));

        return cases.AsReadOnly();
    }

    private static IReadOnlyList<CharLiteralSafetyCase> BuildChars() =>
    [
        new("char-apostrophe", '\''),
        new("char-backslash", '\\'),
        new("char-percent", '%'),
        new("char-underscore", '_'),
        new("char-bang", '!'),
        new("char-nul", '\0'),
        new("char-u0001", '\u0001'),
        new("char-u001f", '\u001F'),
        new("char-a", 'a'),
        new("char-space", ' '),
        new("char-cyrillic", 'П'),
    ];

    private static string AllC0() => string.Concat(Enumerable.Range(1, 0x1F).Select(code => (char)code));
}

/// <summary>#327 D8: one LIKE pattern and the row values it must match (native semantics).</summary>
internal sealed record LikePatternCase(string Id, string Pattern, string[] Matches, bool ExplicitEscape = false);

/// <summary>
/// #327 D8: the frozen LIKE corpus shared by the four shared-suite providers and the dedicated
/// MySQL/MariaDB/ClickHouse classes. The pattern text uses the documented SQL pattern language:
/// <c>%</c>/<c>_</c> are wildcards and a backslash escapes the next wildcard or backslash. The
/// expected sets are computed from <see cref="LikeRows"/>; the provider test additionally compares
/// the ORM result with a native typed-parameter reference.
/// </summary>
internal static class StringLiteralSafetyLike
{
    /// <summary>The deterministic rows every LIKE fixture inserts (no NUL: NUL LIKE is provider-native).</summary>
    public static IReadOnlyList<(string Id, string Value)> LikeRows { get; } =
    [
        ("like-plain", "dadfasd"),
        ("like-other", "xxx"),
        ("like-apostrophe", "it's; --"),
        ("like-backslash", "a\\b"),
        ("like-percent", "100%"),
        ("like-underscore", "a_b"),
        ("like-cyrillic", "Привет"),
    ];

    public static IReadOnlyList<LikePatternCase> Patterns { get; } =
    [
        new("like-prefix", "dad%", ["dadfasd"]),
        new("like-suffix", "%d", ["dadfasd"]),
        new("like-single-wildcard", "d_d%", ["dadfasd"]),
        new("like-all", "%", ["dadfasd", "xxx", "it's; --", "a\\b", "100%", "a_b", "Привет"]),
        new("like-one-char", "_", []),
        new("like-literal-percent", "%\\%", ["100%"], ExplicitEscape: true),
        new("like-literal-underscore", "%\\_%", ["a_b"], ExplicitEscape: true),
        new("like-literal-backslash", "a\\\\b", ["a\\b"], ExplicitEscape: true),
        new("like-apostrophe", "it's%", ["it's; --"]),
        new("like-apostrophe-mixed", "%'s%--", ["it's; --"]),
        new("like-cyrillic", "Привет", ["Привет"]),
    ];

    /// <summary>
    /// Builds the inline-pattern predicate: the pattern is an <see cref="Expression.Constant"/>, so the
    /// provider renders it as a SQL literal (the captured arm binds it as a parameter instead).
    /// </summary>
    public static Expression<Func<TEntity, bool>> InlinePatternPredicate<TEntity>(
        string markerProperty,
        string valueProperty,
        int marker,
        string pattern,
        bool explicitEscape = false)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var surface = Expression.Property(null, typeof(SqlFunctions), nameof(SqlFunctions.Sql));
        var argumentTypes = explicitEscape
            ? new[] { typeof(string), typeof(string), typeof(string) }
            : new[] { typeof(string), typeof(string) };
        var like = typeof(CommonFunctions).GetMethod(
            nameof(CommonFunctions.like),
            BindingFlags.Public | BindingFlags.Instance,
            argumentTypes)!;

        var call = explicitEscape
            ? Expression.Call(
                surface,
                like,
                Expression.Property(parameter, valueProperty),
                Expression.Constant(pattern, typeof(string)),
                Expression.Constant("\\", typeof(string)))
            : Expression.Call(
                surface,
                like,
                Expression.Property(parameter, valueProperty),
                Expression.Constant(pattern, typeof(string)));

        var body = Expression.AndAlso(
            Expression.Equal(
                Expression.Property(parameter, markerProperty),
                Expression.Constant(marker, typeof(int))),
            call);

        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }
}

/// <summary>
/// #327 D8: the task-owned mutation/readback fixture. The shared seed tables are read-only for the
/// suite (other classes assert their exact contents/count in the same parallel run), so the live
/// literal corpus owns <c>sls_probe</c> and creates it with a provider-appropriate Unicode text type:
/// the ORM mutation path is exercised without mutating a shared fixture.
/// </summary>
[SqlTable("sls_probe")]
internal interface IStringLiteralProbe
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("marker")]
    int Marker { get; set; }

    [Column("svalue")]
    string? Svalue { get; set; }
}

/// <summary>#327 D8: the ClickHouse-owned equivalent of <see cref="IStringLiteralProbe"/>.</summary>
[SqlTable("sls_ch_probe")]
internal interface IClickHouseStringLiteralProbe
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("marker")]
    int Marker { get; set; }

    [Column("svalue")]
    string? Svalue { get; set; }
}

/// <summary>#327 D8: MySQL-owned probe table (unique per provider so parallel classes never collide).</summary>
[SqlTable("sls_mysql_probe")]
internal interface IMySqlStringLiteralProbe
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("marker")]
    int Marker { get; set; }

    [Column("svalue")]
    string? Svalue { get; set; }
}

/// <summary>#327 D8: MariaDB-owned probe table (unique per provider so parallel classes never collide).</summary>
[SqlTable("sls_maria_probe")]
internal interface IMariaDbStringLiteralProbe
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("marker")]
    int Marker { get; set; }

    [Column("svalue")]
    string? Svalue { get; set; }
}
