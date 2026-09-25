using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MariaDb;

/// <summary>
/// MariaDB dialect: the MySQL family plus the <c>INTERSECT ALL</c>/<c>EXCEPT ALL</c> set-operation
/// variants (MariaDB 10.4+) and the window percentile functions <c>PERCENTILE_CONT</c>/<c>MEDIAN</c>
/// (MariaDB 10.3+), neither of which MySQL implements.
/// </summary>
public sealed class MariaDbDialect : MySqlDialect
{
    /// <summary>Gets the shared MariaDB dialect instance.</summary>
    public static new readonly MariaDbDialect Instance = new();

    /// <summary>MariaDB has no <c>REGEXP_LIKE</c>; it matches with the <c>REGEXP</c> operator and replaces with <c>REGEXP_REPLACE</c>.</summary>
    public override bool SupportsRegex => true;

    /// <inheritdoc/>
    public override string MakeRegexMatch(string value, string pattern, bool ignoreCase) =>
        $"{value} regexp {QuoteStringLiteral((ignoreCase ? "(?i)" : "(?-i)") + pattern, escapeBackslash: true)}";

    /// <inheritdoc/>
    public override string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase) =>
        $"regexp_replace({value}, {QuoteStringLiteral((ignoreCase ? "(?i)" : "(?-i)") + pattern, escapeBackslash: true)}, {QuoteStringLiteral(replacement, escapeBackslash: true)})";

    /// <summary>MariaDB has no native bulk path enabled: <c>MySqlBulkCopy</c> requires a server-side
    /// <c>local_infile</c> setting that is commonly disabled, so the portable
    /// <c>INSERT ... VALUES</c> path is used instead (it also returns keys on request).
    /// </summary>
    public override bool SupportsBulkCopy => false;

    /// <inheritdoc/>
    public override bool SupportsIntersectExceptAll => true;

    /// <summary>MariaDB 10.3+ renders <c>PERCENTILE_CONT</c>/<c>PERCENTILE_DISC</c> as window functions.</summary>
    public override bool SupportsPercentileWindow => true;

    /// <summary>
    /// MariaDB has no <c>ANY_VALUE</c> in the supported range: the SQL-2023 <c>T626</c> feature
    /// (MDEV-10426) is still pending and targeted for 13.2, so <c>any_agg</c> is gated off even though
    /// MySQL implements it.
    /// </summary>
    public override bool SupportsAnyValueAggregate => false;

    /// <summary>MariaDB 11.7+ renders both UUID generators; MySQL has no v4/v7 generator.</summary>
    public override IUuidGenerators UuidGenerators => MariaDbUuidGenerators.Instance;

    /// <summary>MariaDB 10.3+ supports the <c>FOR SYSTEM_TIME</c> temporal-table clause on system-versioned tables.</summary>
    public override bool SupportsTemporalTable => true;

    /// <summary>MariaDB has no <c>CONTAINED IN</c>, so it is gated off; the other kinds are supported.</summary>
    public override bool SupportsTemporalKind(TemporalKind kind) => kind != TemporalKind.ContainedIn;

    /// <summary>
    /// MariaDB renders row locking with <c>LOCK IN SHARE MODE</c>, which — unlike MySQL's — accepts the
    /// <c>NOWAIT</c>/<c>SKIP LOCKED</c> lock options, so the MySQL renderer is not reused.
    /// </summary>
    public override ILockRenderer Lock => MariaDbLockRenderer.Instance;

    /// <summary>
    /// MariaDB inherits the MySQL native-function surface except <c>UUID_TO_BIN</c>/<c>BIN_TO_UUID</c>,
    /// which it does not implement (it converts through <c>CAST(... AS BINARY(16))</c>/<c>CAST(... AS UUID)</c>),
    /// and adds the MariaDB-only names (extended regexp, <c>NVL</c>/<c>NVL2</c>, <c>ADD_MONTHS</c>,
    /// <c>MONTHS_BETWEEN</c>, <c>TO_CHAR</c>/<c>TO_DATE</c>/<c>TO_NUMBER</c>, <c>KDF</c>,
    /// <c>XXH3</c>/<c>XXH32</c>, <c>JSON_DETAILED</c>/<c>JSON_COMPACT</c> and sequence access).
    /// </summary>
    public override IMySqlFunctions MySqlFunctions => MariaDbNativeFunctions.Instance;
}

/// <summary>
/// Decorates the MySQL native-function renderer for MariaDB: every MySQL name is inherited except
/// <c>uuid_to_bin</c>/<c>bin_to_uuid</c>, which MariaDB has no function for (it uses
/// <c>cast(... as binary(16))</c>/<c>cast(... as uuid)</c>), and the MariaDB-only names are added here
/// (extended regexp, <c>nvl</c>/<c>nvl2</c>, the Oracle-compatible date/number conversions, <c>kdf</c>,
/// <c>xxh3</c>/<c>xxh32</c>, <c>json_detailed</c>/<c>json_compact</c> and sequence access). MySQL's
/// renderer does not report the MariaDB-only names, so MySQL rejects them.
/// </summary>
internal sealed class MariaDbNativeFunctions : IMySqlFunctions
{
    public static readonly MariaDbNativeFunctions Instance = new();

    private static readonly IMySqlFunctions Inner = MySqlDialect.Instance.MySqlFunctions!;

    private static bool IsMariaDbOnly(string name) => name is
        "regexp_substr" or "regexp_instr" or "regexp_replace" or
        "nvl" or "nvl2" or "add_months" or "months_between" or
        "to_char" or "to_date" or "to_number" or "kdf" or
        "xxh3" or "xxh32" or "json_detailed" or "json_compact" or
        "next_value_for" or "nextval" or "setval" or "lastval";

    /// <inheritdoc/>
    public bool Supports(string name) =>
        IsMariaDbOnly(name) || (name is not ("uuid_to_bin" or "bin_to_uuid") && Inner.Supports(name));

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> args) => name switch
    {
        "regexp_substr" => $"regexp_substr({args[0]}, {args[1]})",
        "regexp_instr" => $"regexp_instr({args[0]}, {args[1]})",
        "regexp_replace" => $"regexp_replace({args[0]}, {args[1]}, {args[2]})",
        "nvl" => $"nvl({args[0]}, {args[1]})",
        "nvl2" => $"nvl2({args[0]}, {args[1]}, {args[2]})",
        "add_months" => $"add_months({args[0]}, {args[1]})",
        "months_between" => $"months_between({args[0]}, {args[1]})",
        "to_char" => $"to_char({args[0]}, {args[1]})",
        "to_date" => $"to_date({args[0]}, {args[1]})",
        "to_number" => $"to_number({args[0]}, {args[1]})",
        "kdf" => $"kdf({args[0]}, {args[1]}, {args[2]}, {args[3]})",
        "xxh3" => $"xxh3({args[0]})",
        "xxh32" => $"xxh32({args[0]})",
        "json_detailed" => $"json_detailed({args[0]})",
        "json_compact" => $"json_compact({args[0]})",
        "next_value_for" => "next value for " + SequenceName(args[0]),
        "nextval" => $"nextval({SequenceName(args[0])})",
        "setval" => $"setval({SequenceName(args[0])}, {args[1]})",
        "lastval" => $"lastval({SequenceName(args[0])})",
        _ => Supports(name)
            ? Inner.Render(name, args)
            : throw new NotSupportedException($"The {name} function is not supported by MariaDB.")
    };

    private static string SequenceName(string argument) =>
        argument.Length >= 2 && argument[0] == '\'' && argument[^1] == '\''
            ? argument[1..^1].Replace("''", "'")
            : argument;
}

internal sealed class MariaDbLockRenderer : ILockRenderer
{
    public static readonly MariaDbLockRenderer Instance = new();

    public bool UsesTableHints => false;

    public string Render(LockMode mode, KeywordCase keywordCase = KeywordCase.Lower) =>
        Render(mode, LockWaitMode.Wait, keywordCase);

    public string Render(LockMode mode, LockWaitMode wait, KeywordCase keywordCase = KeywordCase.Lower) =>
        SqlKeywords.Of(keywordCase, (mode == LockMode.Share ? " lock in share mode" : " for update") + LockWaitSuffix(wait));

    private static string LockWaitSuffix(LockWaitMode wait) => wait switch
    {
        LockWaitMode.Wait => "",
        LockWaitMode.NoWait => " nowait",
        LockWaitMode.SkipLocked => " skip locked",
        _ => throw new ArgumentOutOfRangeException(nameof(wait), wait, "Unknown locking wait mode.")
    };
}

internal sealed class MariaDbUuidGenerators : IUuidGenerators
{
    public static readonly MariaDbUuidGenerators Instance = new();

    public bool Supports(string name) => name is "gen_random_uuid" or "uuidv7";

    public string Render(string name) => name switch
    {
        "gen_random_uuid" => "uuid_v4()",
        "uuidv7" => "uuid_v7()",
        _ => throw new NotSupportedException($"The {name} UUID generator function is not supported by MariaDB.")
    };
}
