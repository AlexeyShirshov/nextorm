using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// One T350 residual name as a SQL-generation case: the canonical inventory identity, the MariaDB fragment
/// the production path must emit, and the behaviour the row must produce — a rendered fragment for a
/// delivered name, or the exact pre-SQL refusal text for a name the shared surface cannot express.
/// </summary>
/// <param name="Id">Stable inventory id (the row id of <c>.pdca/T350/rv1/inventory.md</c>).</param>
/// <param name="Name">Canonical vendor name.</param>
/// <param name="Family">Inventory family.</param>
/// <param name="ExpectedSqlFragment">The exact fragment the rendered SQL must contain.</param>
/// <param name="Behaviour"><c>delivered</c>, or <c>refusal: &lt;exact reason&gt;</c>.</param>
internal sealed record T350SqlCase(
    string Id,
    string Name,
    string Family,
    string ExpectedSqlFragment,
    string Behaviour);

/// <summary>
/// One T350 refused name: the capability gate (or, for a name with no surface member, the dialect
/// renderer) must throw the pinned message before any SQL, and a delivered sibling must still render on
/// the same provider so the refusal cannot pass by breaking the surrounding surface.
/// </summary>
/// <param name="Id">Stable inventory id (the row id of <c>.pdca/T350/rv1/inventory.md</c>).</param>
/// <param name="Name">Canonical vendor name.</param>
/// <param name="Family">Inventory family.</param>
/// <param name="ExpectedMessage">The exact refusal text, asserted verbatim.</param>
/// <param name="Call">Prepares the refused call; it must throw instead of returning a command.</param>
/// <param name="Control">Renders a delivered sibling on the same provider.</param>
/// <param name="ControlFragment">The exact fragment that sibling must emit.</param>
internal sealed record T350RefusalCase(
    string Id,
    string Name,
    string Family,
    string ExpectedMessage,
    Func<IDataContext, object?> Call,
    Func<IDataContext, string> Control,
    string ControlFragment);

/// <summary>
/// T350 (issue #284) residual MariaDB names, SQL-generation side. The tables are the single source of the
/// delivered/refused contract: one row per name and three facts that iterate them, so a name cannot be
/// delivered without a pinned fragment, claimed without its dialect capability, or refused without its
/// exact reason text.
/// <para>
/// Block 1 covers the 75 names the S1 probe proved supported on MariaDB 11.4.13 and that only the
/// exclusion tuple was still hiding, plus the three members this block adds (<c>rownum</c>,
/// <c>binlog_gtid_pos</c>, <c>previous_value_for</c>) — 78 delivered rows. Block 2 adds the 31 residual
/// names (10 string/JSON, 6 crypto, 7 dynamic columns and the 8 galera/singles), 109 in total. The 23
/// refusals and the five <c>VEC_*</c> version gates are pinned with their exact text. These tests never
/// open a connection; the live counterpart is
/// <c>T350MariaDbFunctionLiveMatrixTests</c> in the integration project.
/// </para>
/// </summary>
public class T350MariaDbResidualFunctionSqlGenerationTests
{
    private const string ColumnGetRefusal =
        "COLUMN_GET requires a compile-time AS type clause; the supported function surface cannot " +
        "represent that clause safely.";

    private const string TruncRefusal =
        "TRUNC is unavailable on the verified MariaDB 11.4 baseline, including ORACLE mode; " +
        "use TRUNCATE for numeric truncation.";

    private const string RowIdRefusal =
        "_rowid requires a source table with one integer primary-key column; this function surface " +
        "cannot validate that table-dependent requirement.";

    private const string VectorRefusal =
        "Requires MariaDB 11.7 or later; the verified baseline is MariaDB 11.4.";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static string Select<T>(
        IDataContext ctx, Func<EntityBuilder<IComplexEntity>, QueryCommand<T>> build)
        => SqlOf(ctx, build(ctx.From<IComplexEntity>()));

    private static object? Prepare<T>(
        IDataContext ctx, Func<EntityBuilder<IComplexEntity>, QueryCommand<T>> build)
        => ctx.GetPreparedQueryCommand(build(ctx.From<IComplexEntity>()), false, false, CancellationToken.None);

    private static string Plain<T>(IDataContext ctx, Func<EntityBuilder<IComplexEntity>, QueryCommand<T>> build)
        => Select(ctx, build);

    /// <summary>
    /// The delivered rows: every name whose capability is now claimed on MariaDB and the exact select-item
    /// fragment the production renderer must produce for it.
    /// </summary>
    private static readonly T350SqlCase[] DeliveredCases =
    [
        new("4", "JSON_ARRAY", "JSON", "json_array(1)", "delivered"),
        new("5", "JSON_ARRAYAGG", "JSON", "json_arrayagg(id)", "delivered"),
        new("8", "JSON_CONTAINS", "JSON", "json_contains(_utf8mb4 X'7b2261223a317d', _utf8mb4 X'31')", "delivered"),
        new("9", "JSON_CONTAINS_PATH", "JSON", "json_contains_path(_utf8mb4 X'7b7d', 'one', '$.a')", "delivered"),
        new("10", "JSON_MERGE", "JSON", "json_merge(_utf8mb4 X'7b2261223a317d', _utf8mb4 X'7b2262223a327d')", "delivered"),
        new("11", "JSON_OBJECT", "JSON", "json_object(_utf8mb4 X'61', 1)", "delivered"),
        new("12", "JSON_OBJECTAGG", "JSON", "json_objectagg(_utf8mb4 X'61', id)", "delivered"),
        new("13", "JSON_OVERLAPS", "JSON", "json_overlaps(_utf8mb4 X'7b7d', _utf8mb4 X'7b7d')", "delivered"),
        new("14", "JSON_PRETTY", "JSON", "json_pretty(_utf8mb4 X'7b7d')", "delivered"),
        new("15", "JSON_QUOTE", "JSON", "json_quote(_utf8mb4 X'61')", "delivered"),
        new("16", "JSON_SCHEMA_VALID", "JSON", "json_schema_valid(_utf8mb4 X'7b7d', _utf8mb4 X'7b2261223a317d')", "delivered"),
        new("18", "JSON_SEARCH", "JSON", "json_search(_utf8mb4 X'7b2261223a2262227d', 'one', _utf8mb4 X'62')", "delivered"),
        new("21", "BIN", "string", "bin(10)", "delivered"),
        new("22", "EXPORT_SET", "string", "export_set(5, _utf8mb4 X'59', _utf8mb4 X'4e')", "delivered"),
        new("23", "HEX", "string", "hex(somestring)", "delivered"),
        new("24", "MAKE_SET", "string", "make_set(3, _utf8mb4 X'61', _utf8mb4 X'62')", "delivered"),
        new("25", "OCT", "string", "oct(10)", "delivered"),
        new("26", "ORD", "string", "ord(somestring)", "delivered"),
        new("27", "QUOTE", "string", "quote(somestring)", "delivered"),
        new("28", "SOUNDEX", "string", "soundex(somestring)", "delivered"),
        new("29", "STRCMP", "string", "strcmp(somestring, _utf8mb4 X'61')", "delivered"),
        new("30", "TO_BASE64", "string", "to_base64(@norm_p0)", "delivered"),
        new("31", "FROM_BASE64", "string", "from_base64(somestring)", "delivered"),
        new("32", "UNHEX", "string", "unhex(somestring)", "delivered"),
        new("33", "WEIGHT_STRING", "string", "weight_string(somestring)", "delivered"),
        new("34", "CONV", "numeric", "conv(_utf8mb4 X'323535', 10, 16)", "delivered"),
        new("35", "CRC32", "numeric", "crc32(somestring)", "delivered"),
        new("36", "LOG2", "numeric", "log2(cast(id as double))", "delivered"),
        new("37", "RAND", "numeric", "rand()", "delivered"),
        new("38", "TRUNCATE", "numeric", "truncate(1.234, 2)", "delivered"),
        new("39", "BIT_COUNT", "bit", "bit_count(10)", "delivered"),
        new("40", "BIT_AND", "bit", "bit_and(10)", "delivered"),
        new("41", "BIT_OR", "bit", "bit_or(10)", "delivered"),
        new("42", "BIT_XOR", "bit", "bit_xor(10)", "delivered"),
        new("43", "ADDDATE", "date/time", "adddate(dt, 1)", "delivered"),
        new("44", "ADDTIME", "date/time", "addtime(dt, time(dt))", "delivered"),
        new("45", "CONVERT_TZ", "date/time", "convert_tz(dt, _utf8mb4 X'2b30303a3030', _utf8mb4 X'2b30313a3030')", "delivered"),
        new("46", "DAYNAME", "date/time", "dayname(dt)", "delivered"),
        new("47", "FROM_DAYS", "date/time", "from_days(id)", "delivered"),
        new("48", "GET_FORMAT", "date/time", "get_format(DATE, 'EUR')", "delivered"),
        new("49", "MAKEDATE", "date/time", "makedate(2020, 32)", "delivered"),
        new("50", "MAKETIME", "date/time", "maketime(12, 34, 56)", "delivered"),
        new("51", "MICROSECOND", "date/time", "microsecond(dt)", "delivered"),
        new("52", "MONTHNAME", "date/time", "monthname(dt)", "delivered"),
        new("53", "PERIOD_ADD", "date/time", "period_add(202401, 1)", "delivered"),
        new("54", "PERIOD_DIFF", "date/time", "period_diff(202402, 202401)", "delivered"),
        new("55", "SEC_TO_TIME", "date/time", "sec_to_time(3661)", "delivered"),
        new("56", "SUBDATE", "date/time", "subdate(dt, 1)", "delivered"),
        new("57", "SUBTIME", "date/time", "subtime(dt, time(dt))", "delivered"),
        new("58", "TIME", "date/time", "time(dt)", "delivered"),
        new("59", "TIME_FORMAT", "date/time", "time_format(time(dt), _utf8mb4 X'25483a2569')", "delivered"),
        new("60", "TIME_TO_SEC", "date/time", "time_to_sec(time(dt))", "delivered"),
        new("61", "TIMEDIFF", "date/time", "timediff(dt, dt)", "delivered"),
        new("62", "TIMESTAMP", "date/time", "timestamp(dt)", "delivered"),
        new("63", "TO_DAYS", "date/time", "to_days(dt)", "delivered"),
        new("64", "TO_SECONDS", "date/time", "to_seconds(dt)", "delivered"),
        new("65", "YEARWEEK", "date/time", "yearweek(dt)", "delivered"),
        new("66", "AES_ENCRYPT", "crypto", "aes_encrypt(@norm_p0, @norm_p1)", "delivered"),
        new("67", "AES_DECRYPT", "crypto", "aes_decrypt(@norm_p0, @norm_p1)", "delivered"),
        new("68", "COMPRESS", "crypto", "compress(@norm_p0)", "delivered"),
        new("69", "UNCOMPRESS", "crypto", "uncompress(@norm_p0)", "delivered"),
        new("70", "UNCOMPRESSED_LENGTH", "crypto", "uncompressed_length(@norm_p0)", "delivered"),
        new("71", "RANDOM_BYTES", "crypto", "random_bytes(16)", "delivered"),
        new("75", "FOUND_ROWS", "information", "found_rows()", "delivered"),
        new("76", "LAST_INSERT_ID", "information", "last_insert_id()", "delivered"),
        new("77", "ROW_COUNT", "information", "row_count()", "delivered"),
        new("78", "CURRENT_ROLE", "information", "current_role()", "delivered"),
        new("79", "CHARSET", "information", "charset(somestring)", "delivered"),
        new("80", "COLLATION", "information", "collation(somestring)", "delivered"),
        new("81", "COERCIBILITY", "information", "coercibility(somestring)", "delivered"),
        new("82", "BENCHMARK", "information", "benchmark(1, somestring)", "delivered"),
        new("84", "UUID_SHORT", "information", "uuid_short()", "delivered"),
        new("86", "SLEEP", "miscellaneous", "sleep(cast(id as double))", "delivered"),
        new("88", "NAME_CONST", "miscellaneous", "name_const('label', id)", "delivered"),
        new("89", "COLUMN_DEFAULT", "miscellaneous", "default(id)", "delivered"),
        new("98", "ROWNUM", "information", "ROWNUM()", "delivered"),
        new("100", "BINLOG_GTID_POS", "information", "binlog_gtid_pos(_utf8mb4 X'6d7973716c642d62696e2e303030303032', 740)", "delivered"),
        new("121", "PREVIOUS VALUE FOR", "sequence", "previous value for t350_seq", "delivered"),
        // T350 block 2: the residual string names MariaDB 11.4.13 accepts and MySQL has no spelling for.
        new("90", "SFORMAT", "string", "sformat(somestring, id)", "delivered"),
        new("91", "TRIM_ORACLE", "string", "trim_oracle(somestring)", "delivered"),
        new("92", "NATURAL_SORT_KEY", "string", "natural_sort_key(somestring)", "delivered"),
        new("93", "LENGTHB", "string", "lengthb(somestring)", "delivered"),
        new("94", "CHR", "string", "chr(65)", "delivered"),
        // T350 block 2: the residual JSON names of the same group.
        new("110", "JSON_LOOSE", "JSON", "json_loose(somestring)", "delivered"),
        new("113", "JSON_ARRAY_INTERSECT", "JSON", "json_array_intersect(somestring, somestring)", "delivered"),
        new("114", "JSON_KEY_VALUE", "JSON", "json_key_value(somestring, '$.a')", "delivered"),
        new("115", "JSON_OBJECT_FILTER_KEYS", "JSON", "json_object_filter_keys(somestring, somestring)", "delivered"),
        new("116", "JSON_OBJECT_TO_ARRAY", "JSON", "json_object_to_array(somestring)", "delivered"),
        // T350 block 2: the residual crypto names (DES_* are still present on the 11.4 baseline).
        new("101", "ENCODE", "crypto", "encode(somestring, somestring)", "delivered"),
        new("102", "DECODE", "crypto", "decode(@norm_p0, somestring)", "delivered"),
        new("103", "ENCRYPT", "crypto", "encrypt(somestring, somestring)", "delivered"),
        new("104", "DES_ENCRYPT", "crypto", "des_encrypt(somestring, somestring)", "delivered"),
        new("105", "DES_DECRYPT", "crypto", "des_decrypt(des_encrypt(somestring, somestring), somestring)", "delivered"),
        new("106", "OLD_PASSWORD", "crypto", "old_password(somestring)", "delivered"),
        // T350 block 2: the residual dynamic-columns names (COLUMN_GET remains a declared refusal).
        new("123", "COLUMN_CREATE", "dynamic columns", "column_create(somestring, id)", "delivered"),
        new("124", "COLUMN_ADD", "dynamic columns",
            "column_add(column_create(somestring, id), somestring, id)", "delivered"),
        new("125", "COLUMN_DELETE", "dynamic columns",
            "column_delete(column_create(somestring, id), somestring)", "delivered"),
        new("127", "COLUMN_JSON", "dynamic columns", "column_json(column_create(somestring, id))", "delivered"),
        new("128", "COLUMN_LIST", "dynamic columns", "column_list(column_create(somestring, id))", "delivered"),
        new("129", "COLUMN_EXISTS", "dynamic columns",
            "column_exists(column_create(somestring, id), somestring)", "delivered"),
        new("130", "COLUMN_CHECK", "dynamic columns", "column_check(column_create(somestring, id))", "delivered"),
        // T350 block 2: the residual galera names (readable without a Galera cluster).
        new("131", "WSREP_LAST_SEEN_GTID", "galera", "wsrep_last_seen_gtid()", "delivered"),
        new("132", "WSREP_LAST_WRITTEN_GTID", "galera", "wsrep_last_written_gtid()", "delivered"),
        new("133", "WSREP_SYNC_WAIT_UPTO_GTID", "galera", "wsrep_sync_wait_upto_gtid(somestring)", "delivered"),
        // T350 block 2: the residual single-name functions.
        new("96", "FORMAT_PICO_TIME", "date/time", "format_pico_time(id)", "delivered"),
        new("97", "DECODE_ORACLE", "control flow", "decode(id, id, somestring, somestring)", "delivered"),
        new("99", "DECODE_HISTOGRAM", "information", "decode_histogram(somestring, somestring)", "delivered"),
        new("107", "CRC32C", "numeric", "crc32c(somestring)", "delivered"),
        new("108", "SYS_GUID", "miscellaneous", "sys_guid()", "delivered")
    ];

    /// <summary>
    /// The production call for each delivered row. The map is keyed by the canonical inventory name so a
    /// row cannot carry a fragment the renderer never produced for that name.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<IDataContext, string>> DeliveredSql =
        new Dictionary<string, Func<IDataContext, string>>(StringComparer.Ordinal)
        {
            ["JSON_ARRAY"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_array(1))),
            ["JSON_ARRAYAGG"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.json_arrayagg(x.Id))),
            ["JSON_CONTAINS"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_contains("{\"a\":1}", "1"))),
            ["JSON_CONTAINS_PATH"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_contains_path("{}", "one", "$.a"))),
            ["JSON_MERGE"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_merge("{\"a\":1}", "{\"b\":2}"))),
            ["JSON_OBJECT"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_object("a", 1))),
            ["JSON_OBJECTAGG"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.json_objectagg("a", x.Id))),
            ["JSON_OVERLAPS"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_overlaps("{}", "{}"))),
            ["JSON_PRETTY"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_pretty("{}"))),
            ["JSON_QUOTE"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_quote("a"))),
            ["JSON_SCHEMA_VALID"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_schema_valid("{}", "{\"a\":1}"))),
            ["JSON_SEARCH"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_search("{\"a\":\"b\"}", "one", "b"))),
            ["BIN"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.bin(10UL))),
            ["EXPORT_SET"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.export_set(5UL, "Y", "N"))),
            ["HEX"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.hex(x.String))),
            ["MAKE_SET"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.make_set(3UL, "a", "b"))),
            ["OCT"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.oct(10UL))),
            ["ORD"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.ord(x.String))),
            ["QUOTE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.quote(x.String))),
            ["SOUNDEX"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.soundex(x.String))),
            ["STRCMP"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.strcmp(x.String, "a"))),
            ["TO_BASE64"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.to_base64(SqlFunctions.Parameter<byte[]>(0)))),
            ["FROM_BASE64"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.from_base64(x.String))),
            ["UNHEX"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.unhex(x.String))),
            ["WEIGHT_STRING"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.weight_string(x.String))),
            ["CONV"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.conv("255", 10, 16))),
            ["CRC32"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.crc32(x.String))),
            ["LOG2"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.log2(x.Id))),
            ["RAND"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.rand())),
            ["TRUNCATE"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.truncate(1.234m, 2))),
            ["BIT_COUNT"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.bit_count(10UL))),
            ["BIT_AND"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.bit_and(10UL))),
            ["BIT_OR"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.bit_or(10UL))),
            ["BIT_XOR"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.bit_xor(10UL))),
            ["ADDDATE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.adddate(x.Datetime, 1))),
            ["ADDTIME"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.addtime(x.Datetime, SqlFunctions.MySql.time(x.Datetime)))),
            ["CONVERT_TZ"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.convert_tz(x.Datetime, "+00:00", "+01:00"))),
            ["DAYNAME"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.dayname(x.Datetime))),
            ["FROM_DAYS"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.from_days(x.Id))),
            ["GET_FORMAT"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.get_format(MySqlFormatKind.Date, "EUR"))),
            ["MAKEDATE"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.makedate(2020, 32))),
            ["MAKETIME"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.maketime(12, 34, 56m))),
            ["MICROSECOND"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.microsecond(x.Datetime))),
            ["MONTHNAME"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.monthname(x.Datetime))),
            ["PERIOD_ADD"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.period_add(202401L, 1L))),
            ["PERIOD_DIFF"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.period_diff(202402L, 202401L))),
            ["SEC_TO_TIME"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.sec_to_time(3661m))),
            ["SUBDATE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.subdate(x.Datetime, 1))),
            ["SUBTIME"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.subtime(x.Datetime, SqlFunctions.MySql.time(x.Datetime)))),
            ["TIME"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.time(x.Datetime))),
            ["TIME_FORMAT"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.time_format(SqlFunctions.MySql.time(x.Datetime), "%H:%i"))),
            ["TIME_TO_SEC"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.time_to_sec(SqlFunctions.MySql.time(x.Datetime)))),
            ["TIMEDIFF"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.timediff(x.Datetime, x.Datetime))),
            ["TIMESTAMP"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.timestamp(x.Datetime))),
            ["TO_DAYS"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.to_days(x.Datetime))),
            ["TO_SECONDS"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.to_seconds(x.Datetime))),
            ["YEARWEEK"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.yearweek(x.Datetime))),
            ["AES_ENCRYPT"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.aes_encrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1)))),
            ["AES_DECRYPT"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.aes_decrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1)))),
            ["COMPRESS"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.compress(SqlFunctions.Parameter<byte[]>(0)))),
            ["UNCOMPRESS"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.uncompress(SqlFunctions.Parameter<byte[]>(0)))),
            ["UNCOMPRESSED_LENGTH"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.uncompressed_length(SqlFunctions.Parameter<byte[]>(0)))),
            ["RANDOM_BYTES"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.random_bytes(16))),
            ["FOUND_ROWS"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.found_rows())),
            ["LAST_INSERT_ID"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.last_insert_id())),
            ["ROW_COUNT"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.row_count())),
            ["CURRENT_ROLE"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.current_role())),
            ["CHARSET"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.charset(x.String))),
            ["COLLATION"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.collation(x.String))),
            ["COERCIBILITY"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.coercibility(x.String))),
            ["BENCHMARK"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.benchmark(1L, x.String))),
            ["UUID_SHORT"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.uuid_short())),
            ["SLEEP"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.sleep(x.Id))),
            ["NAME_CONST"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.name_const("label", x.Id))),
            ["COLUMN_DEFAULT"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.column_default(x.Id))),
            ["ROWNUM"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.rownum())),
            ["BINLOG_GTID_POS"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.binlog_gtid_pos("mysqld-bin.000002", 740UL))),
            ["PREVIOUS VALUE FOR"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.previous_value_for("t350_seq"))),
            ["SFORMAT"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.sformat(x.String, x.Id))),
            ["TRIM_ORACLE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.trim_oracle(x.String))),
            ["NATURAL_SORT_KEY"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.natural_sort_key(x.String))),
            ["LENGTHB"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.lengthb(x.String))),
            ["CHR"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.chr(65))),
            ["JSON_LOOSE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.json_loose(x.String))),
            ["JSON_ARRAY_INTERSECT"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.json_array_intersect(x.String, x.String))),
            ["JSON_KEY_VALUE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.json_key_value(x.String, "$.a"))),
            ["JSON_OBJECT_FILTER_KEYS"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.json_object_filter_keys(x.String, x.String))),
            ["JSON_OBJECT_TO_ARRAY"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.json_object_to_array(x.String))),
            ["ENCODE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.encode(x.String, x.String))),
            ["DECODE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.decode(SqlFunctions.Parameter<byte[]>(0), x.String))),
            ["ENCRYPT"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.encrypt(x.String, x.String))),
            ["DES_ENCRYPT"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.des_encrypt(x.String, x.String))),
            ["DES_DECRYPT"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.des_decrypt(
                SqlFunctions.MySql.des_encrypt(x.String, x.String), x.String))),
            ["OLD_PASSWORD"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.old_password(x.String))),
            ["COLUMN_CREATE"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.column_create(x.String, x.Id))),
            ["COLUMN_ADD"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.column_add(SqlFunctions.MySql.column_create(x.String, x.Id), x.String, x.Id))),
            ["COLUMN_DELETE"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.column_delete(SqlFunctions.MySql.column_create(x.String, x.Id), x.String))),
            ["COLUMN_JSON"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.column_json(SqlFunctions.MySql.column_create(x.String, x.Id)))),
            ["COLUMN_LIST"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.column_list(SqlFunctions.MySql.column_create(x.String, x.Id)))),
            ["COLUMN_EXISTS"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.column_exists(SqlFunctions.MySql.column_create(x.String, x.Id), x.String))),
            ["COLUMN_CHECK"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.column_check(SqlFunctions.MySql.column_create(x.String, x.Id)))),
            // T350 block 2: the residual galera and single-name functions.
            ["WSREP_LAST_SEEN_GTID"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.wsrep_last_seen_gtid())),
            ["WSREP_LAST_WRITTEN_GTID"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.wsrep_last_written_gtid())),
            ["WSREP_SYNC_WAIT_UPTO_GTID"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.wsrep_sync_wait_upto_gtid(x.String))),
            ["FORMAT_PICO_TIME"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.format_pico_time(x.Id))),
            ["DECODE_ORACLE"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.decode_oracle(x.Id, x.Id, x.String, x.String))),
            ["DECODE_HISTOGRAM"] = ctx => Plain(ctx, e => e.Select(x =>
                SqlFunctions.MySql.decode_histogram(x.String, x.String))),
            ["CRC32C"] = ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.crc32c(x.String))),
            ["SYS_GUID"] = ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.sys_guid()))
        };

    /// <summary>
    /// The 18 refused rows and the five <c>VEC_*</c> version gates. A name with no surface member
    /// (<c>FORMAT_BYTES</c>) is refused by the dialect renderer, which is the only production path that
    /// exists for it.
    /// </summary>
    private static readonly T350RefusalCase[] RefusedCases =
    [
        new("1", "UUID_TO_BIN", "JSON", "The uuid_to_bin function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.uuid_to_bin(x.String))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.uuid_short())), "uuid_short()"),
        new("2", "BIN_TO_UUID", "JSON", "The bin_to_uuid function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(_ => SqlFunctions.MySql.bin_to_uuid(SqlFunctions.Parameter<byte[]>(0)))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.uuid_short())), "uuid_short()"),
        new("3", "JSON_STORAGE_FREE", "JSON", "The json_storage_free function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.json_storage_free(x.String))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_pretty("{}"))), "json_pretty("),
        new("6", "JSON_ARROW", "JSON", "The json_arrow function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.json_arrow(x.String, "$.a"))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_quote("a"))), "json_quote("),
        new("7", "JSON_ARROW_TEXT", "JSON", "The json_arrow_text function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.json_arrow_text(x.String, "$.a"))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_quote("a"))), "json_quote("),
        new("17", "JSON_SCHEMA_VALIDATION_REPORT", "JSON",
            "The json_schema_validation_report function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(_ => SqlFunctions.MySql.json_schema_validation_report("{}", "{}"))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_pretty("{}"))), "json_pretty("),
        new("19", "JSON_STORAGE_SIZE", "JSON", "The json_storage_size function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.json_storage_size(x.String))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_pretty("{}"))), "json_pretty("),
        new("20", "MEMBER_OF", "JSON", "The member_of function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(_ => SqlFunctions.MySql.member_of(1L, "[1,2]"))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_contains("{\"a\":1}", "1"))), "json_contains("),
        new("72", "VALIDATE_PASSWORD_STRENGTH", "crypto",
            "The validate_password_strength function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.validate_password_strength(x.String))),
            ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.hex(x.String))), "hex(somestring)"),
        new("73", "STATEMENT_DIGEST", "information",
            "The statement_digest function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.statement_digest(x.String))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.row_count())), "row_count()"),
        new("74", "STATEMENT_DIGEST_TEXT", "information",
            "The statement_digest_text function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.statement_digest_text(x.String))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.row_count())), "row_count()"),
        new("83", "ICU_VERSION", "information", "The icu_version function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(_ => SqlFunctions.MySql.icu_version())),
            ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.charset(x.String))), "charset(somestring)"),
        new("85", "IS_UUID", "information", "The is_uuid function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.is_uuid(x.String))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.uuid_short())), "uuid_short()"),
        new("87", "GROUPING", "aggregate", "The grouping function is not supported by this provider.",
            ctx => Prepare(ctx, e => e.Select(x => SqlFunctions.MySql.grouping(x.Id))),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.bit_and(10UL))), "bit_and(10)"),
        new("95", "TRUNC", "date/time", TruncRefusal,
            ctx => Prepare(ctx, e => e.Select(x => new { x.Id, V = SqlFunctions.MySql.trunc(x.Datetime) })),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.truncate(1.234m, 2))), "truncate(1.234, 2)"),
        new("109", "FORMAT_BYTES", "string", "The format_bytes function is not supported by MariaDB.",
            _ => MariaDbDialect.Instance.MySqlFunctions!.Render("format_bytes", ["1024"]),
            ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.hex(x.String))), "hex(somestring)"),
        new("122", "_rowid", "pseudo-column", RowIdRefusal,
            ctx => Prepare(ctx, e => e.Select(_ => SqlFunctions.MySql._rowid())),
            ctx => Plain(ctx, e => e.Select(x => SqlFunctions.MySql.column_default(x.Id))), "default(id)"),
        new("126", "COLUMN_GET", "dynamic columns", ColumnGetRefusal,
            ctx => Prepare(ctx, e => e.Select(x => new { x.Id, V = SqlFunctions.MySql.column_get(x.String, "a") })),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_pretty("{}"))), "json_pretty("),
        new("134", "VEC_FromText", "vector", VectorRefusal,
            ctx => Prepare(ctx, e => e.Select(x => new { x.Id, V = SqlFunctions.MySql.vec_from_text(x.String) })),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_array(1))), "json_array(1)"),
        new("135", "VEC_ToText", "vector", VectorRefusal,
            ctx => Prepare(ctx, e => e.Select(x => new { x.Id, V = SqlFunctions.MySql.vec_to_text(x.String) })),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_array(1))), "json_array(1)"),
        new("136", "VEC_DISTANCE_EUCLIDEAN", "vector", VectorRefusal,
            ctx => Prepare(ctx, e => e.Select(x => new { x.Id, V = SqlFunctions.MySql.vec_distance_euclidean(x.String, x.String) })),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_array(1))), "json_array(1)"),
        new("137", "VEC_DISTANCE_COSINE", "vector", VectorRefusal,
            ctx => Prepare(ctx, e => e.Select(x => new { x.Id, V = SqlFunctions.MySql.vec_distance_cosine(x.String, x.String) })),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_array(1))), "json_array(1)"),
        new("138", "VEC_DISTANCE", "vector", VectorRefusal,
            ctx => Prepare(ctx, e => e.Select(x => new { x.Id, V = SqlFunctions.MySql.vec_distance(x.String, x.String) })),
            ctx => Plain(ctx, e => e.Select(_ => SqlFunctions.MySql.json_array(1))), "json_array(1)")
    ];

    /// <summary>The three names this block delivered on MariaDB only; MySQL has none of them.</summary>
    private static readonly HashSet<string> MariaDbOnlyNames =
        new(StringComparer.Ordinal)
        {
            "ROWNUM", "BINLOG_GTID_POS", "PREVIOUS VALUE FOR",
            // T350 block 2: the residual string and JSON names.
            "SFORMAT", "TRIM_ORACLE", "NATURAL_SORT_KEY", "LENGTHB", "CHR",
            "JSON_LOOSE", "JSON_ARRAY_INTERSECT", "JSON_KEY_VALUE",
            "JSON_OBJECT_FILTER_KEYS", "JSON_OBJECT_TO_ARRAY",
            // T350 block 2: the residual crypto names.
            "ENCODE", "DECODE", "ENCRYPT", "DES_ENCRYPT", "DES_DECRYPT", "OLD_PASSWORD",
            // T350 block 2: the residual dynamic-columns names.
            "COLUMN_CREATE", "COLUMN_ADD", "COLUMN_DELETE", "COLUMN_JSON",
            "COLUMN_LIST", "COLUMN_EXISTS", "COLUMN_CHECK",
            // T350 block 2: the residual galera and single-name functions.
            "WSREP_LAST_SEEN_GTID", "WSREP_LAST_WRITTEN_GTID", "WSREP_SYNC_WAIT_UPTO_GTID",
            "FORMAT_PICO_TIME", "DECODE_ORACLE", "DECODE_HISTOGRAM", "CRC32C", "SYS_GUID"
        };

    /// <summary>
    /// The 14 refused rows that <b>are</b> MySQL 8.4 functions: the inventory's three permanent exclusions,
    /// the three operator forms and the eight names the MariaDB server reports absent. MariaDB excludes
    /// them precisely because it has no spelling, so MySQL keeps claiming them — the exclusion tuple is the
    /// MariaDB-only half of the contract. The other nine refused rows have no MySQL spelling either.
    /// </summary>
    private static readonly HashSet<string> MySqlSurfaceRefusals = new(StringComparer.Ordinal)
    {
        "UUID_TO_BIN", "BIN_TO_UUID", "JSON_STORAGE_FREE",
        "JSON_ARROW", "JSON_ARROW_TEXT", "MEMBER_OF",
        "JSON_SCHEMA_VALIDATION_REPORT", "JSON_STORAGE_SIZE", "VALIDATE_PASSWORD_STRENGTH",
        "STATEMENT_DIGEST", "STATEMENT_DIGEST_TEXT", "ICU_VERSION", "IS_UUID", "GROUPING"
    };

    private static string CapabilityName(string canonicalName) => canonicalName switch
    {
        "PREVIOUS VALUE FOR" => "previous_value_for",
        _ => canonicalName.ToLowerInvariant()
    };

    [Fact]
    public void EveryResidualRow_ShouldCarryEitherItsFragmentOrItsRefusal()
    {
        DeliveredCases.Should().HaveCount(
            109, "78 block-1 names plus the 31 block-2 names: 10 string/JSON, 6 crypto, " +
                 "7 dynamic columns and the 8 galera/singles");
        DeliveredCases.Should().OnlyContain(@case => @case.Behaviour == "delivered");
        DeliveredCases.Select(@case => @case.Id).Should().OnlyHaveUniqueItems("one row per inventory id");
        DeliveredCases.Select(@case => @case.Name).Should().OnlyHaveUniqueItems("one row per canonical name");
        DeliveredCases.Select(@case => @case.Name).Should().BeEquivalentTo(
            DeliveredSql.Keys, "every delivered row must carry the production call that renders it");
        DeliveredCases.Select(@case => @case.Name).Should().Contain(
            ["ROWNUM", "BINLOG_GTID_POS", "PREVIOUS VALUE FOR"], "the three new members are covered too");

        RefusedCases.Should().HaveCount(23, "18 refusals plus the five 11.7-gated vector names");
        RefusedCases.Select(@case => @case.Id).Should().OnlyHaveUniqueItems("one row per inventory id");
        RefusedCases.Select(@case => @case.Name).Should().OnlyHaveUniqueItems("one row per canonical name");

        foreach (var @case in DeliveredCases)
        {
            @case.Family.Should().NotBeNullOrWhiteSpace($"{@case.Id} must name its inventory family");
            @case.ExpectedSqlFragment.Should().NotBeNullOrWhiteSpace(
                $"{@case.Id}/{@case.Name} is delivered, so its rendered fragment must be pinned verbatim");
        }

        foreach (var @case in RefusedCases)
        {
            @case.Family.Should().NotBeNullOrWhiteSpace($"{@case.Id} must name its inventory family");
            @case.ExpectedMessage.Should().NotBeNullOrWhiteSpace(
                $"{@case.Id}/{@case.Name} must carry the exact refusal text the capability gate throws");
            @case.ControlFragment.Should().NotBeNullOrWhiteSpace(
                $"{@case.Id}/{@case.Name} needs a positive sibling control");
        }
    }

    [Fact]
    public void EveryDeliveredName_ShouldRenderItsPinnedFragment()
    {
        using var ctx = MariaDbTestContext.Create();

        foreach (var @case in DeliveredCases)
        {
            var sql = DeliveredSql[@case.Name](ctx);

            sql.Should().Contain(@case.ExpectedSqlFragment, $"{@case.Id}/{@case.Name} must render its native form");
            sql.Should().StartWith("select ", $"{@case.Id}/{@case.Name} must render as a select item");
        }
    }

    [Fact]
    public void EveryDeliveredName_ShouldBeClaimedByItsDialect()
    {
        var mariadb = MariaDbDialect.Instance.MySqlFunctions!;
        var mysql = MySqlDialect.Instance.MySqlFunctions!;

        foreach (var @case in DeliveredCases)
        {
            var name = CapabilityName(@case.Name);
            mariadb.Supports(name).Should().BeTrue($"#{284} delivered '{name}' on MariaDB 11.4");

            if (MariaDbOnlyNames.Contains(@case.Name))
                mysql.Supports(name).Should().BeFalse($"'{name}' is MariaDB-only and MySQL has no spelling");
            else
                mysql.Supports(name).Should().BeTrue($"'{name}' is the inherited MySQL 8.4 surface");
        }
    }

    [Fact]
    public void EveryRefusedName_ShouldBeRefusedOnBothDialects()
    {
        var mariadb = MariaDbDialect.Instance.MySqlFunctions!;
        var mysql = MySqlDialect.Instance.MySqlFunctions!;

        foreach (var @case in RefusedCases)
        {
            var name = CapabilityName(@case.Name);
            mariadb.Supports(name).Should().BeFalse($"'{name}' is refused on the verified MariaDB 11.4 baseline");

            if (MySqlSurfaceRefusals.Contains(@case.Name))
                mysql.Supports(name).Should().BeTrue(
                    $"'{name}' is the MySQL 8.4 name the MariaDB exclusion tuple keeps out");
            else
                mysql.Supports(name).Should().BeFalse($"'{name}' has no MySQL spelling either");

            using var ctx = MariaDbTestContext.Create();
            var refused = () => @case.Call(ctx);
            refused.Should().Throw<NotSupportedException>(
                $"{@case.Id}/{@case.Name} must be refused before any SQL is emitted").WithMessage(@case.ExpectedMessage);

            @case.Control(ctx).Should().Contain(
                @case.ControlFragment, $"{@case.Id}/{@case.Name}: the sibling surface must keep rendering");
        }
    }

    /// <summary>
    /// The three §3 refusals and the five <c>VEC_*</c> version gates are also enforced in the member body,
    /// exactly like <c>json_table</c>/<c>uuid</c>/<c>values</c>: the planner can fold a projection whose
    /// whole body is a bare call client-side, so a client-side evaluation must hit the same message instead
    /// of silently producing a value.
    /// </summary>
    [Fact]
    public void DeclaredRefusalMembers_ShouldThrowFromTheirOwnBody()
    {
        var functions = new MySqlFunctions();
        (string Name, Func<object?> Call, string Message)[] bodies =
        [
            ("COLUMN_GET", () => functions.column_get("blob", "a"), ColumnGetRefusal),
            ("TRUNC", () => functions.trunc(1.5m), TruncRefusal),
            ("_rowid", () => functions._rowid(), RowIdRefusal),
            ("VEC_FromText", () => functions.vec_from_text("x"), VectorRefusal),
            ("VEC_ToText", () => functions.vec_to_text("x"), VectorRefusal),
            ("VEC_DISTANCE_EUCLIDEAN", () => functions.vec_distance_euclidean("x", "y"), VectorRefusal),
            ("VEC_DISTANCE_COSINE", () => functions.vec_distance_cosine("x", "y"), VectorRefusal),
            ("VEC_DISTANCE", () => functions.vec_distance("x", "y"), VectorRefusal)
        ];

        foreach (var (name, call, message) in bodies)
            call.Should().Throw<NotSupportedException>($"{name} must refuse in its own body").WithMessage(message);
    }
}
