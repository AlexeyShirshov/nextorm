namespace NextORM.Integration.Tests;

/// <summary>
/// One raw-oracle case from the T349 MySQL 8.4 inventory (issue #283). The record is deliberately
/// SQL-first: the diagnostic probe executes <see cref="RawSql"/> on a live server and records what
/// the connector materialises, so the CLR surface decisions are taken from observation rather than
/// from the manual. Aggregate cases carry a temporary-table setup/teardown pair (bounded probe
/// table <c>t349_probe</c>) that the probe always runs inside <c>try/finally</c>.
/// </summary>
/// <param name="Id">Stable inventory case id (<c>L01</c>…<c>L91</c>, overload rows suffixed <c>-b</c>/<c>-c</c>).</param>
/// <param name="Name">Vendor name as spelled in the inventory.</param>
/// <param name="Family">Inventory family: string, numeric, date/time, JSON, aggregate, bit, crypto, information, miscellaneous.</param>
/// <param name="Disposition">Planned disposition: <c>delivered</c>, <c>already-covered</c> or <c>refusal</c>.</param>
/// <param name="RawSql">A complete standalone SELECT; empty for a refusal with no scalar raw form.</param>
/// <param name="ExtraRawSql">Additional setup/NULL/invalid variants recorded alongside the primary statement.</param>
/// <param name="ExpectedVendorType">Vendor-declared return type from the MySQL 8.4 manual (best reading; may be empty).</param>
/// <param name="Notes">Free-text rationale, determinism or prerequisite note.</param>
/// <param name="SetupSql">Optional per-case setup (temporary table) executed on the same connection.</param>
/// <param name="TeardownSql">Optional teardown, always executed in <c>finally</c>.</param>
internal sealed record MySqlCase(
    string Id,
    string Name,
    string Family,
    string Disposition,
    string RawSql,
    string[] ExtraRawSql,
    string ExpectedVendorType,
    string Notes,
    string? SetupSql = null,
    string? TeardownSql = null);

/// <summary>
/// The 91-name T349 inventory as raw-oracle cases. <c>JSON_STORAGE_FREE</c> and <c>CONNECTION_ID</c>
/// are the two already-covered names; <c>UUID</c>, <c>JSON_TABLE</c>, <c>VALUES</c> and
/// <c>DEFAULT</c> are the four planned refusals.
/// </summary>
internal static class T349MySqlFunctionCases
{
    private const string SetupNumeric =
        "CREATE TEMPORARY TABLE t349_probe (x BIGINT UNSIGNED); " +
        "INSERT INTO t349_probe VALUES (1),(3)";

    private const string SetupBinary =
        "CREATE TEMPORARY TABLE t349_probe (x VARBINARY(4)); " +
        "INSERT INTO t349_probe VALUES (UNHEX('01')),(UNHEX('03'))";

    private const string DropProbe = "DROP TEMPORARY TABLE IF EXISTS t349_probe";

    internal static readonly MySqlCase[] All =
    [
        // ---- string (13) ----
        new("L01", "BIN", "string", "delivered", "SELECT BIN(5)", [], "VARCHAR", "unsigned input; negative input is an error"),
        new("L01-b", "BIN", "string", "delivered", "SELECT BIN(18446744073709551615)", [], "VARCHAR", "unsigned BIGINT overload"),
        new("L02", "EXPORT_SET", "string", "delivered", "SELECT EXPORT_SET(5,'Y','N')", [], "VARCHAR", "bits, on, off"),
        new("L02-b", "EXPORT_SET", "string", "delivered", "SELECT EXPORT_SET(5,'Y','N',',',4)", [], "VARCHAR", "separator and count overload"),
        new("L03", "HEX", "string", "delivered", "SELECT HEX(UNHEX('6162'))", [], "VARCHAR", "binary overload"),
        new("L03-b", "HEX", "string", "delivered", "SELECT HEX(18446744073709551615)", [], "VARCHAR", "unsigned overload"),
        new("L03-c", "HEX", "string", "delivered", "SELECT HEX('abc')", [], "VARCHAR", "string overload"),
        new("L04", "MAKE_SET", "string", "delivered", "SELECT MAKE_SET(1,'a','b')", [], "VARCHAR", "variadic value list"),
        new("L05", "OCT", "string", "delivered", "SELECT OCT(8)", [], "VARCHAR", "unsigned input"),
        new("L06", "ORD", "string", "delivered", "SELECT ORD('A')", [], "BIGINT", "leading-byte code point"),
        new("L07", "QUOTE", "string", "delivered", "SELECT QUOTE('a''b')", [], "VARCHAR", "escaped apostrophe input"),
        new("L08", "SOUNDEX", "string", "delivered", "SELECT SOUNDEX('hello')", [], "VARCHAR", "deterministic"),
        new("L09", "STRCMP", "string", "delivered", "SELECT STRCMP('a','b')", [], "INTEGER", "signed comparison result"),
        new("L10", "TO_BASE64", "string", "delivered", "SELECT TO_BASE64('abc')", [], "VARCHAR", "string input"),
        new("L11", "FROM_BASE64", "string", "delivered", "SELECT FROM_BASE64(TO_BASE64('abc'))", [], "BLOB", "binary round trip"),
        new("L12", "UNHEX", "string", "delivered", "SELECT UNHEX('6162')", [], "VARBINARY", "binary result"),
        new("L13", "WEIGHT_STRING", "string", "delivered", "SELECT WEIGHT_STRING('abc')", [], "VARBINARY", "no modifier clause"),
        new("L13-b", "WEIGHT_STRING", "string", "delivered", "SELECT WEIGHT_STRING('abc' AS CHAR(4))", [], "VARBINARY", "AS CHAR(N) modifier pads with spaces"),
        new("L13-c", "WEIGHT_STRING", "string", "delivered", "SELECT WEIGHT_STRING('abc' AS BINARY(4))", [], "VARBINARY", "AS BINARY(N) modifier pads with 0x00"),

        // ---- numeric (5) ----
        new("L14", "CONV", "numeric", "delivered", "SELECT CONV('ff',16,10)", [], "VARCHAR", "string result"),
        new("L15", "CRC32", "numeric", "delivered", "SELECT CRC32('abc')", [], "BIGINT UNSIGNED", "string overload"),
        new("L15-b", "CRC32", "numeric", "delivered", "SELECT CRC32(UNHEX('6162'))", [], "BIGINT UNSIGNED", "binary overload"),
        new("L16", "LOG2", "numeric", "delivered", "SELECT LOG2(8)", [], "DOUBLE", "double result"),
        new("L17", "RAND", "numeric", "delivered", "SELECT RAND(42)", [], "DOUBLE", "seeded; reproducible"),
        new("L17-b", "RAND", "numeric", "delivered", "SELECT RAND()", [], "DOUBLE", "unseeded overload; type/range only"),
        new("L18", "TRUNCATE", "numeric", "delivered", "SELECT TRUNCATE(1.2345,2)", [], "DECIMAL", "literal is DECIMAL"),
        new("L18-b", "TRUNCATE", "numeric", "delivered", "SELECT TRUNCATE(1.2345e0,2)", [], "DOUBLE", "double overload keeps DOUBLE"),

        // ---- date/time (23) ----
        new("L19", "ADDDATE", "date/time", "delivered", "SELECT ADDDATE('2020-01-01', INTERVAL 1 DAY)", [], "DATE", "INTERVAL form"),
        new("L19-b", "ADDDATE", "date/time", "delivered", "SELECT ADDDATE('2020-01-01', 1)", [], "DATE", "days-integer form"),
        new("L20", "ADDTIME", "date/time", "delivered", "SELECT ADDTIME('2020-01-01 00:00:00','01:00:00')", [], "DATETIME", "datetime plus time"),
        new("L20-b", "ADDTIME", "date/time", "delivered", "SELECT ADDTIME('12:00:00','01:00:00')", [], "TIME", "time plus time overload"),
        new("L21", "CONVERT_TZ", "date/time", "delivered", "SELECT CONVERT_TZ('2020-01-01 00:00:00','+00:00','+02:00')", [], "DATETIME", "numeric offsets need no time-zone tables"),
        new("L21-b", "CONVERT_TZ", "date/time", "delivered", "SELECT CONVERT_TZ('2020-01-01 00:00:00','UTC','Europe/Berlin')", [], "DATETIME", "named zones require mysql.time_zone_name (U5)"),
        new("L22", "DAYNAME", "date/time", "delivered", "SELECT DAYNAME('2020-01-01')", [], "VARCHAR", "deterministic"),
        new("L23", "FROM_DAYS", "date/time", "delivered", "SELECT FROM_DAYS(738000)", [], "DATE", "day number to date"),
        new("L24", "GET_FORMAT", "date/time", "delivered", "SELECT GET_FORMAT(DATE,'EUR')", [], "VARCHAR", "keyword enum argument"),
        new("L25", "MAKEDATE", "date/time", "delivered", "SELECT MAKEDATE(2020,32)", [], "DATE", "year plus day-of-year"),
        new("L26", "MAKETIME", "date/time", "delivered", "SELECT MAKETIME(12,34,56)", [], "TIME", "integer seconds"),
        new("L26-b", "MAKETIME", "date/time", "delivered", "SELECT MAKETIME(12,34,56.123456)", [], "TIME", "fractional seconds overload"),
        new("L27", "MICROSECOND", "date/time", "delivered", "SELECT MICROSECOND('2020-01-01 00:00:00.123456')", [], "INTEGER", "datetime overload"),
        new("L27-b", "MICROSECOND", "date/time", "delivered", "SELECT MICROSECOND('12:34:56.123456')", [], "INTEGER", "time overload"),
        new("L28", "MONTHNAME", "date/time", "delivered", "SELECT MONTHNAME('2020-01-01')", [], "VARCHAR", "deterministic"),
        new("L29", "PERIOD_ADD", "date/time", "delivered", "SELECT PERIOD_ADD(202401,2)", [], "INTEGER", "period is YYYYMM"),
        new("L30", "PERIOD_DIFF", "date/time", "delivered", "SELECT PERIOD_DIFF(202402,202401)", [], "INTEGER", "period difference"),
        new("L31", "SEC_TO_TIME", "date/time", "delivered", "SELECT SEC_TO_TIME(3661)", [], "TIME", "seconds to TIME"),
        new("L32", "SUBDATE", "date/time", "delivered", "SELECT SUBDATE('2020-01-01', INTERVAL 1 DAY)", [], "DATE", "INTERVAL form"),
        new("L32-b", "SUBDATE", "date/time", "delivered", "SELECT SUBDATE('2020-01-01', 1)", [], "DATE", "days-integer form"),
        new("L33", "SUBTIME", "date/time", "delivered", "SELECT SUBTIME('2020-01-01 00:00:00','01:00:00')", [], "DATETIME", "datetime minus time"),
        new("L33-b", "SUBTIME", "date/time", "delivered", "SELECT SUBTIME('12:00:00','01:00:00')", [], "TIME", "time minus time overload"),
        new("L34", "TIME", "date/time", "delivered", "SELECT TIME('2020-01-01 12:34:56')", [], "TIME", "datetime overload"),
        new("L34-b", "TIME", "date/time", "delivered", "SELECT TIME('12:34:56')", [], "TIME", "time overload"),
        new("L35", "TIME_FORMAT", "date/time", "delivered", "SELECT TIME_FORMAT('12:34:56','%H:%i')", [], "VARCHAR", "format string result"),
        new("L36", "TIME_TO_SEC", "date/time", "delivered", "SELECT TIME_TO_SEC('12:34:56')", [], "INTEGER/DECIMAL", "integer seconds; manual does not pin the fractional form"),
        new("L37", "TIMEDIFF", "date/time", "delivered", "SELECT TIMEDIFF('2020-01-02 00:00:00','2020-01-01 00:00:00')", [], "TIME", "datetime overload"),
        new("L37-b", "TIMEDIFF", "date/time", "delivered", "SELECT TIMEDIFF('12:00:00','01:00:00')", [], "TIME", "time overload"),
        new("L38", "TIMESTAMP", "date/time", "delivered", "SELECT TIMESTAMP('2020-01-01 00:00:00')", [], "DATETIME", "single-argument overload"),
        new("L38-b", "TIMESTAMP", "date/time", "delivered", "SELECT TIMESTAMP('2020-01-01','12:00:00')", [], "DATETIME", "date plus time overload"),
        new("L39", "TO_DAYS", "date/time", "delivered", "SELECT TO_DAYS('2020-01-01')", [], "BIGINT", "integer result"),
        new("L40", "TO_SECONDS", "date/time", "delivered", "SELECT TO_SECONDS('2020-01-01 00:00:00')", [], "BIGINT", "integer result"),
        new("L41", "YEARWEEK", "date/time", "delivered", "SELECT YEARWEEK('2020-01-01')", [], "INTEGER", "default mode"),
        new("L41-b", "YEARWEEK", "date/time", "delivered", "SELECT YEARWEEK('2020-01-01',1)", [], "INTEGER", "mode overload"),

        // ---- JSON (19, incl. the operators and the shape refusal) ----
        new("L42", "JSON_ARRAY", "JSON", "delivered", "SELECT JSON_ARRAY(1,'a',NULL)", [], "JSON", "variadic; mixed argument types"),
        new("L43", "JSON_CONTAINS", "JSON", "delivered", "SELECT JSON_CONTAINS('{\"a\":1}','1','$.a')", [], "INTEGER", "with path"),
        new("L43-b", "JSON_CONTAINS", "JSON", "delivered", "SELECT JSON_CONTAINS('{\"a\":1}','{\"a\":1}')", [], "INTEGER", "without path"),
        new("L44", "JSON_CONTAINS_PATH", "JSON", "delivered", "SELECT JSON_CONTAINS_PATH('{\"a\":1}','one','$.a')", [], "INTEGER", "single path"),
        new("L44-b", "JSON_CONTAINS_PATH", "JSON", "delivered", "SELECT JSON_CONTAINS_PATH('{\"a\":1,\"b\":2}','all','$.a','$.b')", [], "INTEGER", "variadic paths"),
        new("L45", "JSON_MERGE", "JSON", "delivered", "SELECT JSON_MERGE('{\"a\":1}','{\"b\":2}')", [], "JSON", "variadic merge preserve"),
        new("L46", "JSON_OBJECT", "JSON", "delivered", "SELECT JSON_OBJECT('k',1)", [], "JSON", "key/value pairs"),
        new("L47", "JSON_OVERLAPS", "JSON", "delivered", "SELECT JSON_OVERLAPS('[1,2]','[2,3]')", [], "INTEGER", "boolean result"),
        new("L48", "JSON_PRETTY", "JSON", "delivered", "SELECT JSON_PRETTY('{\"a\":1}')", [], "LONGTEXT", "textual JSON output"),
        new("L49", "JSON_QUOTE", "JSON", "delivered", "SELECT JSON_QUOTE('a\"b')", [], "VARCHAR", "quoted string"),
        new("L50", "JSON_SCHEMA_VALID", "JSON", "delivered", "SELECT JSON_SCHEMA_VALID('{\"type\":\"object\"}','{}')", [], "INTEGER", "boolean result"),
        new("L51", "JSON_SCHEMA_VALIDATION_REPORT", "JSON", "delivered", "SELECT JSON_SCHEMA_VALIDATION_REPORT('{\"type\":\"object\"}','{}')", [], "JSON", "vendor spelling confirmed in b0"),
        new("L52", "JSON_SEARCH", "JSON", "delivered", "SELECT JSON_SEARCH('[\"abc\"]','one','abc')", [], "JSON", "single-value search"),
        new("L52-b", "JSON_SEARCH", "JSON", "delivered", "SELECT JSON_SEARCH('[\"a%c\"]','one','a%c',NULL,'$[0]')", [], "JSON", "escape and path arguments"),
        new("L53", "JSON_STORAGE_FREE", "JSON", "already-covered", "SELECT JSON_STORAGE_FREE('{\"a\":1}')", [], "BIGINT UNSIGNED", "existing surface unchanged (regression control)"),
        new("L54", "JSON_STORAGE_SIZE", "JSON", "delivered", "SELECT JSON_STORAGE_SIZE('{\"a\":1}')", [], "BIGINT UNSIGNED", "byte size"),
        new("L55", "JSON_TABLE", "JSON", "refusal", "SELECT * FROM JSON_TABLE('[{\"a\":1}]','$[*]' COLUMNS(a INT PATH '$.a')) jt", [], "TABLE", "shape row: table-valued, no scalar contract"),
        new("L56", "JSON_ARRAYAGG", "JSON", "delivered", "SELECT JSON_ARRAYAGG(x) FROM t349_probe", ["SELECT JSON_ARRAYAGG(x) FROM (SELECT 1 AS x) t"], "JSON", "aggregate over the probe table", SetupNumeric, DropProbe),
        new("L57", "JSON_OBJECTAGG", "JSON", "delivered", "SELECT JSON_OBJECTAGG(CAST(x AS CHAR),x) FROM t349_probe", ["SELECT JSON_OBJECTAGG('k',x) FROM (SELECT 1 AS x) t"], "JSON", "aggregate over the probe table", SetupNumeric, DropProbe),
        new("L58", "->", "JSON", "delivered", "SELECT doc -> '$.a' FROM t349_probe", ["SELECT '{\"a\":1}' -> '$.a'"], "JSON", "column plus literal path; a string literal on the left is rejected (1064)", "CREATE TEMPORARY TABLE t349_probe (doc JSON); INSERT INTO t349_probe VALUES ('{\"a\":1}')", DropProbe),
        new("L59", "->>", "JSON", "delivered", "SELECT doc ->> '$.a' FROM t349_probe", ["SELECT '{\"a\":1}' ->> '$.a'"], "VARCHAR", "column plus literal path; the unquoted-text operator", "CREATE TEMPORARY TABLE t349_probe (doc JSON); INSERT INTO t349_probe VALUES ('{\"a\":1}')", DropProbe),
        new("L60", "MEMBER OF", "JSON", "delivered", "SELECT 'a' MEMBER OF ('[\"a\",\"b\"]')", [], "INTEGER", "infix operator with parenthesised JSON array"),

        // ---- aggregate (3) + bit aggregates over the binary overload ----
        new("L61", "BIT_AND", "aggregate", "delivered", "SELECT BIT_AND(x) FROM t349_probe", ["SELECT BIT_AND(x) FROM (SELECT 1 AS x UNION ALL SELECT 3) t"], "BIGINT UNSIGNED", "unsigned aggregate", SetupNumeric, DropProbe),
        new("L61-b", "BIT_AND", "aggregate", "delivered", "SELECT BIT_AND(x) FROM t349_probe", [], "VARBINARY", "binary overload", SetupBinary, DropProbe),
        new("L62", "BIT_OR", "aggregate", "delivered", "SELECT BIT_OR(x) FROM t349_probe", ["SELECT BIT_OR(x) FROM (SELECT 1 AS x UNION ALL SELECT 3) t"], "BIGINT UNSIGNED", "unsigned aggregate", SetupNumeric, DropProbe),
        new("L62-b", "BIT_OR", "aggregate", "delivered", "SELECT BIT_OR(x) FROM t349_probe", [], "VARBINARY", "binary overload", SetupBinary, DropProbe),
        new("L63", "BIT_XOR", "aggregate", "delivered", "SELECT BIT_XOR(x) FROM t349_probe", ["SELECT BIT_XOR(x) FROM (SELECT 1 AS x UNION ALL SELECT 3) t"], "BIGINT UNSIGNED", "unsigned aggregate", SetupNumeric, DropProbe),
        new("L63-b", "BIT_XOR", "aggregate", "delivered", "SELECT BIT_XOR(x) FROM t349_probe", [], "VARBINARY", "binary overload", SetupBinary, DropProbe),

        // ---- bit (1) ----
        new("L64", "BIT_COUNT", "bit", "delivered", "SELECT BIT_COUNT(7)", [], "BIGINT UNSIGNED", "unsigned overload"),
        new("L64-b", "BIT_COUNT", "bit", "delivered", "SELECT BIT_COUNT(UNHEX('07'))", [], "BIGINT UNSIGNED", "binary overload"),

        // ---- encryption / compression / hash (9) ----
        new("L65", "AES_ENCRYPT", "crypto", "delivered", "SELECT AES_ENCRYPT('text','key')", [], "VARBINARY", "no IV"),
        new("L65-b", "AES_ENCRYPT", "crypto", "delivered", "SELECT AES_ENCRYPT('text','key',UNHEX('00000000000000000000000000000000'))", [], "VARBINARY", "explicit IV overload"),
        new("L66", "AES_DECRYPT", "crypto", "delivered", "SELECT AES_DECRYPT(AES_ENCRYPT('text','key'),'key')", [], "VARBINARY", "round trip in one statement"),
        new("L67", "COMPRESS", "crypto", "delivered", "SELECT COMPRESS('abc')", [], "VARBINARY", "compressed payload"),
        new("L68", "UNCOMPRESS", "crypto", "delivered", "SELECT UNCOMPRESS(COMPRESS('abc'))", [], "VARBINARY", "round trip"),
        new("L69", "UNCOMPRESSED_LENGTH", "crypto", "delivered", "SELECT UNCOMPRESSED_LENGTH(COMPRESS('abc'))", [], "BIGINT UNSIGNED", "length of the uncompressed form"),
        new("L70", "RANDOM_BYTES", "crypto", "delivered", "SELECT RANDOM_BYTES(4)", [], "VARBINARY", "non-deterministic; length only"),
        new("L71", "VALIDATE_PASSWORD_STRENGTH", "crypto", "delivered", "SELECT VALIDATE_PASSWORD_STRENGTH('abc')", [], "INTEGER", "requires component_validate_password (U5)"),
        new("L72", "STATEMENT_DIGEST", "crypto", "delivered", "SELECT STATEMENT_DIGEST('SELECT 1')", [], "VARBINARY", "digest bytes"),
        new("L73", "STATEMENT_DIGEST_TEXT", "crypto", "delivered", "SELECT STATEMENT_DIGEST_TEXT('SELECT 1')", [], "LONGTEXT", "normalised statement text"),

        // ---- information (10) ----
        new("L74", "CONNECTION_ID", "information", "already-covered", "SELECT CONNECTION_ID()", [], "BIGINT UNSIGNED", "existing portable surface (regression control)"),
        new("L75", "FOUND_ROWS", "information", "delivered", "SELECT FOUND_ROWS()", [], "BIGINT UNSIGNED", "session-dependent; SQL_CALC_FOUND_ROWS setup", "SELECT SQL_CALC_FOUND_ROWS 1"),
        new("L76", "LAST_INSERT_ID", "information", "delivered", "SELECT LAST_INSERT_ID()", [], "BIGINT UNSIGNED", "session-dependent; value seeded by setup", "SELECT LAST_INSERT_ID(7)"),
        new("L76-b", "LAST_INSERT_ID", "information", "delivered", "SELECT LAST_INSERT_ID(7)", [], "BIGINT UNSIGNED", "write form overload"),
        new("L77", "ROW_COUNT", "information", "delivered", "SELECT ROW_COUNT()", [], "BIGINT", "session-dependent; no-op DO setup", "DO 1"),
        new("L78", "CURRENT_ROLE", "information", "delivered", "SELECT CURRENT_ROLE()", [], "VARCHAR", "no role granted in a fresh session"),
        new("L79", "CHARSET", "information", "delivered", "SELECT CHARSET('a')", [], "VARCHAR", "literal argument"),
        new("L80", "COLLATION", "information", "delivered", "SELECT COLLATION('a')", [], "VARCHAR", "literal argument"),
        new("L81", "COERCIBILITY", "information", "delivered", "SELECT COERCIBILITY('a')", [], "INTEGER", "literal argument"),
        new("L82", "BENCHMARK", "information", "delivered", "SELECT BENCHMARK(1,1+1)", [], "INTEGER", "tiny fixed count; result/type only"),
        new("L83", "ICU_VERSION", "information", "delivered", "SELECT ICU_VERSION()", [], "VARCHAR", "server ICU build"),

        // ---- miscellaneous (8, incl. 3 refusals) ----
        new("L84", "UUID", "miscellaneous", "refusal", "SELECT UUID()", [], "VARCHAR(36)", "refused for the surface: UUID() generates a version-1 UUID"),
        new("L85", "UUID_SHORT", "miscellaneous", "delivered", "SELECT UUID_SHORT()", [], "BIGINT UNSIGNED", "unsigned 64-bit value"),
        new("L86", "IS_UUID", "miscellaneous", "delivered", "SELECT IS_UUID('00000000-0000-0000-0000-000000000000')", [], "INTEGER", "nil UUID is a valid shape"),
        new("L87", "SLEEP", "miscellaneous", "delivered", "SELECT SLEEP(0)", [], "INTEGER", "zero duration; result/type only"),
        new("L88", "GROUPING", "miscellaneous", "delivered", "SELECT GROUPING(x) FROM (SELECT 1 AS x) t GROUP BY x WITH ROLLUP", [], "BIGINT UNSIGNED", "WITH ROLLUP position; first row is the grouped row"),
        new("L89", "VALUES", "miscellaneous", "refusal", "", ["SELECT x FROM (SELECT 1 AS x) t ON DUPLICATE KEY UPDATE x = VALUES(x)"], "", "refused: deprecated in 8.4 and column-identifier only inside the mutation builder; no scalar contract"),
        new("L90", "NAME_CONST", "miscellaneous", "delivered", "SELECT NAME_CONST('n', 1)", [], "same as value", "debugging helper; result type follows the value"),
        new("L91", "DEFAULT", "miscellaneous", "refusal", "", ["SELECT DEFAULT(x) FROM (SELECT 1 AS x) t"], "", "refused: no renderer exists and the vendor restricts DEFAULT(col) to literal column defaults")
    ];
}
