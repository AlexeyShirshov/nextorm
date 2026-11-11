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
    /// <c>JSON_EQUALS</c>/<c>JSON_NORMALIZE</c>, sequence access and <c>ROWNUM</c>). <c>MONTHS_BETWEEN</c>,
    /// <c>TO_DATE</c>, <c>TO_NUMBER</c> and <c>XXH3</c>/<c>XXH32</c> carry their own version minima
    /// (12.2.1 / 12.3 / 12.2.1 / 13.1), so an unversioned dialect — and every server below the minimum —
    /// refuses them before any SQL is emitted.
    /// </summary>
    public override IMySqlFunctions MySqlFunctions => _mySqlFunctions;
}

/// <summary>
/// Decorates the MySQL native-function renderer for MariaDB: every MySQL name is inherited except
/// <c>uuid_to_bin</c>/<c>bin_to_uuid</c>, which MariaDB has no function for (it uses
/// <c>cast(... as binary(16))</c>/<c>cast(... as uuid)</c>), and <c>json_storage_free</c>, whose
/// binary-JSON update accounting MariaDB does not implement, and the MariaDB-only names are added here
/// (extended regexp, <c>nvl</c>/<c>nvl2</c>, the Oracle-compatible date/number conversions, <c>kdf</c>,
/// <c>xxh3</c>/<c>xxh32</c>, <c>json_detailed</c>/<c>json_compact</c>, <c>json_equals</c>/<c>json_normalize</c>,
/// sequence access, <c>rownum</c> and <c>binlog_gtid_pos</c>). MySQL's renderer does not report the
/// MariaDB-only names, so MySQL rejects them.
/// <para>
/// <c>months_between</c> (12.2.1), <c>to_date</c> (12.3), <c>to_number</c> (12.2.1) and
/// <c>xxh3</c>/<c>xxh32</c> (13.1) are absent from MariaDB 11.4, so they are enabled only when the
/// dialect was built with a version at or above the per-name minimum; an unversioned dialect refuses
/// them instead of emitting SQL the server rejects.
/// </para>
/// </summary>
internal sealed class MariaDbNativeFunctions : IMySqlFunctions
{
    public static readonly MariaDbNativeFunctions Instance = new(null);

    private static readonly IMySqlFunctions Inner = MySqlDialect.Instance.MySqlFunctions!;

    private static readonly Version MonthsBetweenMinVersion = new(12, 2, 1);
    private static readonly Version ToDateMinVersion = new(12, 3);
    private static readonly Version ToNumberMinVersion = new(12, 2, 1);

    // #352: XXH3 and XXH32 were added in MariaDB 13.1, so the pinned 11.4 baseline answers both with
    // error 1305. Source: MariaDB documentation, numeric-functions reference — "XXH3 was added in
    // MariaDB 13.1." (https://mariadb.com/docs/server/reference/sql-functions/numeric-functions/xxh3)
    // and "XXH32 was added in MariaDB 13.1."
    // (https://mariadb.com/docs/server/reference/sql-functions/numeric-functions/xxh32), corroborated by
    // the 13.1 "Changes & Improvements" new-feature line "XXH3 and XXH32 hash functions (MDEV-38180)"
    // (https://mariadb.com/docs/release-notes/community-server/13.1/mariadb-13.1-changes-and-improvements);
    // accessed 2026-10-30. They used to be advertised unconditionally, which emitted SQL the server
    // rejected at execution time instead of refusing the call during preparation.
    private static readonly Version XxhMinVersion = new(13, 1);

    private readonly Version? _version;

    /// <summary>Creates the renderer for <paramref name="serverVersion"/>; unset refuses the version-gated names.</summary>
    /// <param name="serverVersion">The MariaDB server version, or <see langword="null"/> for unset.</param>
    public MariaDbNativeFunctions(Version? serverVersion) => _version = serverVersion;

    private static bool IsMariaDbOnly(string name) => name is
        "regexp_substr" or "regexp_instr" or "regexp_replace" or
        "nvl" or "nvl2" or "add_months" or
        "to_char" or "kdf" or
        "json_detailed" or "json_compact" or
        "json_equals" or "json_normalize" or
        "next_value_for" or "nextval" or "setval" or "lastval" or
        "previous_value_for" or "rownum" or "binlog_gtid_pos" or
        // T350 block 2 (issue #284): the residual string and JSON names MariaDB 11.4.13 accepts and
        // MySQL has no spelling for.
        "sformat" or "trim_oracle" or "natural_sort_key" or "lengthb" or "chr" or
        "json_loose" or "json_array_intersect" or "json_key_value" or
        "json_object_filter_keys" or "json_object_to_array" or
        // T350 block 2: the residual crypto names of the same baseline.
        "encode" or "decode" or "encrypt" or "des_encrypt" or "des_decrypt" or "old_password" or
        // T350 block 2: the residual dynamic-columns names (COLUMN_GET stays refused).
        "column_create" or "column_add" or "column_delete" or
        "column_json" or "column_list" or "column_exists" or "column_check" or
        // T350 block 2: the residual galera and single-name functions.
        "wsrep_last_seen_gtid" or "wsrep_last_written_gtid" or "wsrep_sync_wait_upto_gtid" or
        "format_pico_time" or "decode_oracle" or "decode_histogram" or "crc32c" or "sys_guid";

    private static Version? MinimumVersion(string name) => name switch
    {
        "months_between" => MonthsBetweenMinVersion,
        "to_date" => ToDateMinVersion,
        "to_number" => ToNumberMinVersion,
        "xxh3" or "xxh32" => XxhMinVersion,
        _ => null
    };

    private bool IsVersionGated(string name) =>
        MinimumVersion(name) is { } minimum && _version is not null && _version >= minimum;

    /// <inheritdoc/>
    public bool Supports(string name) =>
        IsVersionGated(name) ||
        (IsMariaDbOnly(name) ||
            // T350 (#284): the names MariaDB 11.4.13 was measured to accept keep flowing through the
            // inherited MySQL renderer (Inner.Render). Only the names the baseline does not have stay
            // excluded: the three permanent MySQL/MariaDB exclusions, the three operator forms the 11.4
            // parser rejects and the eight names the server reports as absent (error 1305).
            (name is not ("uuid_to_bin" or "bin_to_uuid" or "json_storage_free" or
                "json_arrow" or "json_arrow_text" or "json_schema_validation_report" or
                "json_storage_size" or "member_of" or "validate_password_strength" or
                "statement_digest" or "statement_digest_text" or "icu_version" or
                "is_uuid" or "grouping") && Inner.Supports(name)));

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
        "xxh3" => RenderGated(name, $"xxh3({args[0]})"),
        "xxh32" => RenderGated(name, $"xxh32({args[0]})"),
        "json_detailed" => $"json_detailed({args[0]})",
        "json_compact" => $"json_compact({args[0]})",
        "json_equals" => $"json_equals({args[0]}, {args[1]})",
        "json_normalize" => $"json_normalize({args[0]})",
        "next_value_for" => "next value for " + SequenceName(args[0]),
        "nextval" => $"nextval({SequenceName(args[0])})",
        "setval" => $"setval({SequenceName(args[0])}, {args[1]})",
        "lastval" => $"lastval({SequenceName(args[0])})",
        // The last value the connection's own `NEXT VALUE FOR` produced: a grammar form, so the
        // sequence name is the unquoted identifier and never a quoted value.
        "previous_value_for" => "previous value for " + SequenceName(args[0]),
        // MariaDB exposes ROW_NUMBER only as the zero-argument `ROWNUM()` function; the bare pseudo-column
        // is Oracle-mode-only and is not delivered (T350 S1 grammar re-probe G21).
        "rownum" => "ROWNUM()",
        "binlog_gtid_pos" => $"binlog_gtid_pos({args[0]}, {args[1]})",
        // T350 block 2: the residual string and JSON names. The variadic forms join every positional
        // argument; the vendor spelling is unchanged from MySQL's where MySQL has none.
        "sformat" => $"sformat({string.Join(", ", args)})",
        "trim_oracle" => $"trim_oracle({args[0]})",
        "natural_sort_key" => $"natural_sort_key({args[0]})",
        "lengthb" => $"lengthb({args[0]})",
        "chr" => $"chr({args[0]})",
        "json_loose" => $"json_loose({args[0]})",
        "json_array_intersect" => $"json_array_intersect({string.Join(", ", args)})",
        "json_key_value" => $"json_key_value({args[0]}, {args[1]})",
        "json_object_filter_keys" => $"json_object_filter_keys({string.Join(", ", args)})",
        "json_object_to_array" => $"json_object_to_array({args[0]})",
        // T350 block 2: the residual crypto names; encrypt/des_* have one- and two-argument overloads.
        "encode" => $"encode({args[0]}, {args[1]})",
        "decode" => $"decode({args[0]}, {args[1]})",
        "encrypt" => $"encrypt({string.Join(", ", args)})",
        "des_encrypt" => $"des_encrypt({string.Join(", ", args)})",
        "des_decrypt" => $"des_decrypt({string.Join(", ", args)})",
        "old_password" => $"old_password({args[0]})",
        // T350 block 2: the residual dynamic-columns names. column_create/column_add are variadic pairs,
        // column_delete a variadic name list; COLUMN_GET needs the refused AS type clause and stays out.
        "column_create" => $"column_create({string.Join(", ", args)})",
        "column_add" => $"column_add({string.Join(", ", args)})",
        "column_delete" => $"column_delete({string.Join(", ", args)})",
        "column_json" => $"column_json({args[0]})",
        "column_list" => $"column_list({args[0]})",
        "column_exists" => $"column_exists({args[0]}, {args[1]})",
        "column_check" => $"column_check({args[0]})",
        // T350 block 2: the residual galera and single-name functions. DECODE_ORACLE renders the
        // vendor's long spelling, `decode_oracle(...)`: the short `decode(...)` resolves to the
        // two-argument crypto function outside sql_mode=ORACLE (probe t350-decode-oracle-s2, error 1582
        // for the six-argument form), while DECODE_ORACLE(...) is accepted in default mode.
        "wsrep_last_seen_gtid" => "wsrep_last_seen_gtid()",
        "wsrep_last_written_gtid" => "wsrep_last_written_gtid()",
        "wsrep_sync_wait_upto_gtid" => $"wsrep_sync_wait_upto_gtid({args[0]})",
        "format_pico_time" => $"format_pico_time({args[0]})",
        "decode_oracle" => $"decode_oracle({string.Join(", ", args)})",
        "decode_histogram" => $"decode_histogram({args[0]}, {args[1]})",
        "crc32c" => $"crc32c({args[0]})",
        "sys_guid" => "sys_guid()",
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
