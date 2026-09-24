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
