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
    // MariaDB 13.0 introduced UPDATE ... RETURNING and the bulk INSERT ... RETURNING arm; the single-row
    // INSERT/DELETE ... RETURNING forms remain unsupported.
    private static readonly Version ReturningMinVersion = new(13, 0);

    /// <summary>Gets the shared MariaDB dialect instance.</summary>
    public static new readonly MariaDbDialect Instance = new();

    private readonly Version? _version;
    private readonly MariaDbNativeFunctions _mySqlFunctions;
    private readonly MariaDbUuidGenerators _uuidGenerators;

    /// <summary>
    /// Creates an unversioned MariaDB dialect: no version-gated form is emitted, so
    /// <c>UPDATE ... RETURNING</c> and every version-gated native function are rejected. Equivalent
    /// to the shared <see cref="Instance"/>.
    /// </summary>
    public MariaDbDialect()
        : this(null)
    {
    }

    /// <summary>
    /// Creates a MariaDB dialect for <paramref name="serverVersion"/>. MariaDB 13.0+ exposes
    /// <c>UPDATE ... RETURNING</c>; the native functions carry their own minima. <see langword="null"/>
    /// is the unversioned form, which rejects every version-gated feature.
    /// </summary>
    /// <param name="serverVersion">The MariaDB server version, or <see langword="null"/> for unset.</param>
    public MariaDbDialect(Version? serverVersion)
    {
        _version = serverVersion;
        _mySqlFunctions = serverVersion is null
            ? MariaDbNativeFunctions.Instance
            : new MariaDbNativeFunctions(serverVersion);
        _uuidGenerators = serverVersion is null
            ? MariaDbUuidGenerators.Instance
            : new MariaDbUuidGenerators(serverVersion);
    }

    /// <summary>
    /// MariaDB 13.0+ appends <c>RETURNING</c> to a single-table <c>UPDATE</c>. INSERT/DELETE
    /// <c>RETURNING</c> stays unsupported outside the bulk path.
    /// </summary>
    public override bool SupportsUpdateReturning => _version is not null && _version >= ReturningMinVersion;

    /// <summary>
    /// MariaDB 13.0+ honours <c>RETURNING</c> on the portable bulk <c>INSERT ... VALUES</c> path, so a
    /// <c>Returning*</c> bulk terminal is enabled from that version on and refused before it. This is the
    /// bulk-only gate: single-row insert/delete <c>RETURNING</c> stays refused at every version, which is why
    /// <see cref="ISqlDialect.SupportsReturning"/> keeps reporting <c>false</c>.
    /// </summary>
    internal override bool SupportsBulkInsertReturning => _version is not null && _version >= ReturningMinVersion;

    /// <summary>MariaDB has no <c>REGEXP_LIKE</c>; it matches with the <c>REGEXP</c> operator and replaces with <c>REGEXP_REPLACE</c>.</summary>
    public override bool SupportsRegex => true;

    /// <inheritdoc/>
    public override string MakeRegexMatch(string value, string pattern, bool ignoreCase) =>
        $"{value} regexp {QuoteStringLiteral((ignoreCase ? "(?i)" : "(?-i)") + pattern, escapeBackslash: true)}";

    /// <inheritdoc/>
    public override string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase) =>
        $"regexp_replace({value}, {QuoteStringLiteral((ignoreCase ? "(?i)" : "(?-i)") + pattern, escapeBackslash: true)}, {QuoteStringLiteral(replacement, escapeBackslash: true)})";

    /// <summary>
    /// MariaDB has <b>no</b> native JSON storage type, so this is overridden to stay <c>false</c> on
    /// purpose. Its <c>JSON</c> type is an alias for <c>LONGTEXT CHARACTER SET utf8mb4 COLLATE
    /// utf8mb4_bin</c> plus a column-level <c>CHECK (json_valid(col))</c>;
    /// <c>information_schema.COLUMNS.DATA_TYPE</c> reports <c>longtext</c> and the server stores the
    /// caller's text verbatim (no normalisation, no binary document type). Exposing that as native
    /// storage would misrepresent the storage and change the binding contract, so <c>Auto</c> stays text,
    /// an explicit <see cref="JsonColumnStorage.Text"/> stays text, and an explicit
    /// <see cref="JsonColumnStorage.Native"/> is refused with the named
    /// <see cref="NotSupportedException"/> before any SQL runs.
    /// </summary>
    public override bool SupportsJson => false;

    /// <summary>
    /// MariaDB has a native bulk path: <c>MySqlBulkCopy</c> loads through <c>LOAD DATA LOCAL INFILE</c>,
    /// which requires <c>AllowLoadLocalInfile=true</c> on the connection and <c>local_infile</c> enabled
    /// on the server. Both are read-only preflight checks: a missing permission is never changed by
    /// nextorm, the portable path is used in <see cref="BulkInsertMode.Auto"/>, and
    /// <see cref="BulkInsertMode.Native"/> refuses by name. MariaDB additionally honours
    /// <c>CheckConstraints=false</c> through scoped session controls that are restored after the write.
    /// </summary>
    public override bool SupportsBulkCopy => true;

    /// <summary>
    /// MariaDB has no lateral/APPLY source: 11.4 rejects the generated <c>cross join lateral (…)</c> and
    /// <c>left join lateral (…) on true</c> derived-table spelling, so APPLY is refused by the capability
    /// gate before any SQL is produced. The one expressible shape is the inherited
    /// <see cref="MySqlDialect.SupportsDirectApplyTableFunction"/> form: an eligible correlated
    /// <c>JSON_TABLE</c> correlates implicitly in <c>FROM</c> and needs no <c>LATERAL</c>. MySQL 8.0.14+
    /// supports the lateral spelling as well.
    /// </summary>
    public override bool SupportsApply => false;

    /// <inheritdoc/>
    public override bool SupportsIntersectExceptAll => true;

    /// <summary>MariaDB 10.3+ renders <c>PERCENTILE_CONT</c>/<c>PERCENTILE_DISC</c> as window functions.</summary>
    public override bool SupportsPercentileWindow => true;

    /// <summary>MariaDB 10.2+ renders <c>SelectWhereMax</c>/<c>SelectWhereMin</c> with a window function.</summary>
    public override bool SupportsSelectWhereMinMax => true;

    /// <summary>
    /// MariaDB has no <c>ANY_VALUE</c> in the supported range: the SQL-2023 <c>T626</c> feature
    /// (MDEV-10426) is still pending and targeted for 13.2, so <c>any_agg</c> is gated off even though
    /// MySQL implements it.
    /// </summary>
    public override bool SupportsAnyValueAggregate => false;

    /// <summary>
    /// MariaDB 11.7+ renders both UUID generators; MySQL has no v4/v7 generator. An unversioned
    /// dialect rejects them, because 11.4 has neither.
    /// </summary>
    public override IUuidGenerators UuidGenerators => _uuidGenerators;

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
    /// <c>XXH3</c>/<c>XXH32</c>, <c>JSON_DETAILED</c>/<c>JSON_COMPACT</c>, the JSON canonicalisation pair
    /// <c>JSON_EQUALS</c>/<c>JSON_NORMALIZE</c> and sequence access). <c>MONTHS_BETWEEN</c>,
    /// <c>TO_DATE</c> and <c>TO_NUMBER</c> carry their own version minima, so an unversioned dialect
    /// rejects them.
    /// </summary>
    public override IMySqlFunctions MySqlFunctions => _mySqlFunctions;
}

/// <summary>
/// Decorates the MySQL native-function renderer for MariaDB: every MySQL name is inherited except
/// <c>uuid_to_bin</c>/<c>bin_to_uuid</c>, which MariaDB has no function for (it uses
/// <c>cast(... as binary(16))</c>/<c>cast(... as uuid)</c>), and <c>json_storage_free</c>, whose
/// binary-JSON update accounting MariaDB does not implement, and the MariaDB-only names are added here
/// (extended regexp, <c>nvl</c>/<c>nvl2</c>, the Oracle-compatible date/number conversions, <c>kdf</c>,
/// <c>xxh3</c>/<c>xxh32</c>, <c>json_detailed</c>/<c>json_compact</c>, <c>json_equals</c>/<c>json_normalize</c>
/// and sequence access). MySQL's renderer does not report the MariaDB-only names, so MySQL rejects them.
/// <para>
/// <c>months_between</c> (12.2.1), <c>to_date</c> (12.3) and <c>to_number</c> (12.2.1) are absent from
/// MariaDB 11.4, so they are enabled only when the dialect was built with a version at or above the
/// per-name minimum; an unversioned dialect refuses them instead of emitting SQL the server rejects.
/// </para>
/// </summary>
internal sealed class MariaDbNativeFunctions : IMySqlFunctions
{
    public static readonly MariaDbNativeFunctions Instance = new(null);

    private static readonly IMySqlFunctions Inner = MySqlDialect.Instance.MySqlFunctions!;

    private static readonly Version MonthsBetweenMinVersion = new(12, 2, 1);
    private static readonly Version ToDateMinVersion = new(12, 3);
    private static readonly Version ToNumberMinVersion = new(12, 2, 1);

    private readonly Version? _version;

    /// <summary>Creates the renderer for <paramref name="serverVersion"/>; unset refuses the version-gated names.</summary>
    /// <param name="serverVersion">The MariaDB server version, or <see langword="null"/> for unset.</param>
    public MariaDbNativeFunctions(Version? serverVersion) => _version = serverVersion;

    private static bool IsMariaDbOnly(string name) => name is
        "regexp_substr" or "regexp_instr" or "regexp_replace" or
        "nvl" or "nvl2" or "add_months" or
        "to_char" or "kdf" or
        "xxh3" or "xxh32" or "json_detailed" or "json_compact" or
        "json_equals" or "json_normalize" or
        "next_value_for" or "nextval" or "setval" or "lastval";

    private static Version? MinimumVersion(string name) => name switch
    {
        "months_between" => MonthsBetweenMinVersion,
        "to_date" => ToDateMinVersion,
        "to_number" => ToNumberMinVersion,
        _ => null
    };

    private bool IsVersionGated(string name) =>
        MinimumVersion(name) is { } minimum && _version is not null && _version >= minimum;

    /// <inheritdoc/>
    public bool Supports(string name) =>
        IsVersionGated(name) ||
        (IsMariaDbOnly(name) ||
            (name is not ("uuid_to_bin" or "bin_to_uuid" or "json_storage_free") && Inner.Supports(name)));

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> args) => name switch
    {
        "regexp_substr" => $"regexp_substr({args[0]}, {args[1]})",
        "regexp_instr" => $"regexp_instr({args[0]}, {args[1]})",
        "regexp_replace" => $"regexp_replace({args[0]}, {args[1]}, {args[2]})",
        "nvl" => $"nvl({args[0]}, {args[1]})",
        "nvl2" => $"nvl2({args[0]}, {args[1]}, {args[2]})",
        "add_months" => $"add_months({args[0]}, {args[1]})",
        "months_between" => RenderGated(name, $"months_between({args[0]}, {args[1]})"),
        "to_char" => $"to_char({args[0]}, {args[1]})",
        "to_date" => RenderGated(name, $"to_date({args[0]}, {args[1]})"),
        "to_number" => RenderGated(name, $"to_number({args[0]}, {args[1]})"),
        "kdf" => $"kdf({args[0]}, {args[1]}, {args[2]}, {args[3]})",
        "xxh3" => $"xxh3({args[0]})",
        "xxh32" => $"xxh32({args[0]})",
        "json_detailed" => $"json_detailed({args[0]})",
        "json_compact" => $"json_compact({args[0]})",
        "json_equals" => $"json_equals({args[0]}, {args[1]})",
        "json_normalize" => $"json_normalize({args[0]})",
        "next_value_for" => "next value for " + SequenceName(args[0]),
        "nextval" => $"nextval({SequenceName(args[0])})",
        "setval" => $"setval({SequenceName(args[0])}, {args[1]})",
        "lastval" => $"lastval({SequenceName(args[0])})",
        _ => Supports(name)
            ? Inner.Render(name, args)
            : throw new NotSupportedException($"The {name} function is not supported by MariaDB.")
    };

    private string RenderGated(string name, string sql) =>
        IsVersionGated(name)
            ? sql
            : throw new NotSupportedException(
                $"The {name} function requires MariaDB {MinimumVersion(name)} or later; " +
                $"the configured server version is {(_version is null ? "unset" : _version.ToString())}.");

    private static string SequenceName(string argument) =>
        argument.Length >= 2 && argument[0] == '\'' && argument[^1] == '\''
            ? argument[1..^1].Replace("''", "'")
            : argument;
}

internal sealed class MariaDbLockRenderer : ILockRenderer
{
    public static readonly MariaDbLockRenderer Instance = new();

    public bool UsesTableHints => false;

    /// <summary>
    /// MariaDB's grammar has no <c>FOR UPDATE OF</c>/<c>FOR SHARE OF</c> form, so source-level locking is
    /// rejected fail-closed. The inherited MySQL <c>OF</c> form must never leak here; whole-query locking
    /// keeps working through <see cref="Render(LockMode, LockWaitMode, KeywordCase)"/>.
    /// </summary>
    public bool SupportsSourceLocks => false;

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

/// <summary>
/// Renders <c>gen_random_uuid</c> and <c>uuidv7</c> with MariaDB's <c>uuid_v4()</c>/<c>uuid_v7()</c>.
/// Both functions arrived in MariaDB 11.7, so the generators are enabled only for an explicit version
/// at or above 11.7; an unversioned dialect refuses them instead of rendering SQL 11.4 rejects.
/// </summary>
internal sealed class MariaDbUuidGenerators : IUuidGenerators
{
    public static readonly MariaDbUuidGenerators Instance = new(null);

    private static readonly Version MinVersion = new(11, 7);

    private readonly Version? _version;

    /// <summary>Creates the generator surface for <paramref name="serverVersion"/>; unset refuses both names.</summary>
    /// <param name="serverVersion">The MariaDB server version, or <see langword="null"/> for unset.</param>
    public MariaDbUuidGenerators(Version? serverVersion) => _version = serverVersion;

    private bool IsEnabled => _version is not null && _version >= MinVersion;

    /// <inheritdoc/>
    public bool Supports(string name) => IsEnabled && name is ("gen_random_uuid" or "uuidv7");

    /// <inheritdoc/>
    public string Render(string name)
    {
        if (!IsEnabled)
            throw new NotSupportedException(
                $"The {name} UUID generator function requires MariaDB {MinVersion} or later; " +
                $"the configured server version is {(_version is null ? "unset" : _version.ToString())}.");

        return name switch
        {
            "gen_random_uuid" => "uuid_v4()",
            "uuidv7" => "uuid_v7()",
            _ => throw new NotSupportedException($"The {name} UUID generator function is not supported by MariaDB.")
        };
    }
}
