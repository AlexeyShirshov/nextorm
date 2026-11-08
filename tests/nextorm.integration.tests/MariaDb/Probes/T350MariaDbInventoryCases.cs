namespace NextORM.Integration.Tests;

/// <summary>
/// One candidate of the T350 MariaDB 11.4 inventory (issue #284), as a raw-oracle case. The record is
/// deliberately SQL-first: the probe executes <see cref="Sql"/> on a live MariaDB 11.4 server with raw
/// ADO.NET and records what the connector materialises, so the CLR-surface decision is taken from
/// observation rather than from the manual. <see cref="Kind"/> is the operation kind the inventory
/// classifies the name into; a case whose <see cref="Sql"/> is empty has no scalar raw form and only
/// carries a note.
/// </summary>
/// <param name="Id">Stable inventory case id (<c>P</c> = exclusion tuple, <c>S</c> = spec cell, <c>W</c> = wildcard expansion).</param>
/// <param name="Name">Vendor spelling as frozen in the inventory.</param>
/// <param name="Family">Inventory family: string, numeric, date/time, JSON, aggregate, bit, crypto, information, dynamic columns, galera, vector, sequence, grammar, sys.</param>
/// <param name="Provenance">Where the name enters the universe: <c>tuple</c>, <c>spec</c>, <c>both</c>, or <c>wildcard</c>.</param>
/// <param name="Kind">Operation kind: scalar function, operator, grammar form, pseudo-column, stored function, procedure, table construct.</param>
/// <param name="Sql">A complete standalone <c>SELECT</c>; empty when the name has no scalar raw form.</param>
/// <param name="NullSql">The NULL-argument form, or <c>null</c> when NULL is not applicable.</param>
/// <param name="Note">Free-text rationale, determinism or prerequisite note.</param>
/// <param name="SetupSql">Optional per-case setup executed on the same connection before the statement.</param>
/// <param name="TeardownSql">Optional teardown, always executed in <c>finally</c>.</param>
internal sealed record T350Case(
    string Id,
    string Name,
    string Family,
    string Provenance,
    string Kind,
    string Sql,
    string? NullSql,
    string Note,
    string? SetupSql = null,
    string? TeardownSql = null);

/// <summary>
/// The frozen T350 universe as raw-oracle cases: the 89-name MariaDB exclusion tuple
/// (<c>src/nextorm.mariadb/MariaDbDialect.cs:209-230</c>), the concrete names of the MariaDB spec cell
/// (<c>docs/specs/roadmap/sql-function-coverage-gap.md:77</c>) and one version-scoped expansion of its
/// wildcard groups plus the paired encryption forms. Every row carries its provenance so the inventory is
/// a projection of this table rather than a second hand-maintained list.
/// </summary>
internal static class T350Cases
{
    private const string DropSequence = "DROP SEQUENCE IF EXISTS t350_seq";

    private const string CreateSequence = "DROP SEQUENCE IF EXISTS t350_seq; CREATE SEQUENCE t350_seq";

    private const string DropRowIdTable = "DROP TABLE IF EXISTS t350_rowid";

    private const string CreateRowIdTable =
        "DROP TABLE IF EXISTS t350_rowid; " +
        "CREATE TABLE t350_rowid (id INT NOT NULL PRIMARY KEY, v VARCHAR(10)); " +
        "INSERT INTO t350_rowid VALUES (1,'a')";

    private const string DropDefaultTable = "DROP TEMPORARY TABLE IF EXISTS t350_def";

    private const string OracleModeSetup = "SET SESSION sql_mode = 'ORACLE'";

    private const string OracleModeTeardown = "SET SESSION sql_mode = DEFAULT";

    private const string CreateDefaultTable =
        "DROP TEMPORARY TABLE IF EXISTS t350_def; " +
        "CREATE TEMPORARY TABLE t350_def (id INT DEFAULT 7); " +
        "INSERT INTO t350_def VALUES ()";

    internal static readonly T350Case[] All =
    [
        // ---- the 89-name exclusion tuple ----
        new("P001", "UUID_TO_BIN", "JSON", "tuple", "scalar function", "SELECT UUID_TO_BIN('6ccd780c-baba-1026-9564-5b8c656024db')", "SELECT UUID_TO_BIN(NULL)", "permanent MySQL/MariaDB exclusion: MariaDB casts instead"),
        new("P002", "BIN_TO_UUID", "JSON", "tuple", "scalar function", "SELECT BIN_TO_UUID(UNHEX('6ccd780cbaba102695645b8c656024db'))", "SELECT BIN_TO_UUID(NULL)", "permanent exclusion: MariaDB casts instead"),
        new("P003", "JSON_STORAGE_FREE", "JSON", "tuple", "scalar function", "SELECT JSON_STORAGE_FREE('{\"a\":1}')", "SELECT JSON_STORAGE_FREE(NULL)", "permanent exclusion: binary-JSON update accounting does not exist on MariaDB"),
        new("P004", "JSON_ARRAY", "JSON", "tuple", "scalar function", "SELECT JSON_ARRAY(1,2)", "SELECT JSON_ARRAY(NULL)", "T349 name; MariaDB has JSON_ARRAY"),
        new("P005", "JSON_ARRAYAGG", "JSON", "tuple", "scalar function", "SELECT JSON_ARRAYAGG(x) FROM (SELECT 1 AS x UNION ALL SELECT 2) t", null, "aggregate; row order not guaranteed"),
        new("P006", "JSON_ARROW", "JSON", "tuple", "operator", "SELECT '{\"a\":1}'->'$.a'", "SELECT NULL->'$.a'", "the -> operator; refused at the token, and refused again with the spaced spelling in the grammar re-probe"),
        new("P007", "JSON_ARROW_TEXT", "JSON", "tuple", "operator", "SELECT '{\"a\":1}'->>'$.a'", "SELECT NULL->>'$.a'", "the ->> operator; refused at the token, and refused again with the spaced spelling in the grammar re-probe"),
        new("P008", "JSON_CONTAINS", "JSON", "tuple", "scalar function", "SELECT JSON_CONTAINS('{\"a\":1}','1','$.a')", "SELECT JSON_CONTAINS(NULL,'1','$.a')", "with path argument"),
        new("P009", "JSON_CONTAINS_PATH", "JSON", "tuple", "scalar function", "SELECT JSON_CONTAINS_PATH('{\"a\":1}','one','$.a')", null, "keyword enum argument"),
        new("P010", "JSON_MERGE", "JSON", "tuple", "scalar function", "SELECT JSON_MERGE('{\"a\":1}','{\"b\":2}')", null, "deprecated merge alias"),
        new("P011", "JSON_OBJECT", "JSON", "tuple", "scalar function", "SELECT JSON_OBJECT('a',1)", null, "key/value pairs"),
        new("P012", "JSON_OBJECTAGG", "JSON", "tuple", "scalar function", "SELECT JSON_OBJECTAGG(k,v) FROM (SELECT 1 AS k, 2 AS v) t", null, "aggregate; row order not guaranteed"),
        new("P013", "JSON_OVERLAPS", "JSON", "tuple", "scalar function", "SELECT JSON_OVERLAPS('{\"a\":1}','{\"a\":1}')", "SELECT JSON_OVERLAPS(NULL,'{\"a\":1}')", "also in the spec cell (both provances)"),
        new("P014", "JSON_PRETTY", "JSON", "tuple", "scalar function", "SELECT JSON_PRETTY('{\"a\":1}')", null, "MySQL-only pretty printer"),
        new("P015", "JSON_QUOTE", "JSON", "tuple", "scalar function", "SELECT JSON_QUOTE('a')", "SELECT JSON_QUOTE(NULL)", "string quoting"),
        new("P016", "JSON_SCHEMA_VALID", "JSON", "tuple", "scalar function", "SELECT JSON_SCHEMA_VALID('{\"type\":\"object\"}','{\"a\":1}')", null, "also in the spec cell (both provances)"),
        new("P017", "JSON_SCHEMA_VALIDATION_REPORT", "JSON", "tuple", "scalar function", "SELECT JSON_SCHEMA_VALIDATION_REPORT('{\"type\":\"object\"}','{\"a\":1}')", null, "MySQL-only report form"),
        new("P018", "JSON_SEARCH", "JSON", "tuple", "scalar function", "SELECT JSON_SEARCH('{\"a\":\"b\"}','one','b')", null, "path search"),
        new("P019", "JSON_STORAGE_SIZE", "JSON", "tuple", "scalar function", "SELECT JSON_STORAGE_SIZE('{\"a\":1}')", null, "binary-JSON storage accounting"),
        new("P020", "MEMBER_OF", "JSON", "tuple", "operator", "SELECT 1 MEMBER OF ('[1,2]')", "SELECT NULL MEMBER OF ('[1,2]')", "the MEMBER OF operator; refused both parenthesised and against JSON_ARRAY in the grammar re-probe"),
        new("P021", "BIN", "string", "tuple", "scalar function", "SELECT BIN(5)", "SELECT BIN(NULL)", "unsigned input"),
        new("P022", "EXPORT_SET", "string", "tuple", "scalar function", "SELECT EXPORT_SET(5,'Y','N')", "SELECT EXPORT_SET(NULL,'Y','N')", "bits, on, off"),
        new("P023", "HEX", "string", "tuple", "scalar function", "SELECT HEX('abc')", "SELECT HEX(NULL)", "string overload"),
        new("P024", "MAKE_SET", "string", "tuple", "scalar function", "SELECT MAKE_SET(1,'a','b')", "SELECT MAKE_SET(NULL,'a','b')", "variadic value list"),
        new("P025", "OCT", "string", "tuple", "scalar function", "SELECT OCT(8)", "SELECT OCT(NULL)", "unsigned input"),
        new("P026", "ORD", "string", "tuple", "scalar function", "SELECT ORD('A')", "SELECT ORD(NULL)", "leading-byte code point"),
        new("P027", "QUOTE", "string", "tuple", "scalar function", "SELECT QUOTE('a''b')", "SELECT QUOTE(NULL)", "escaped apostrophe input"),
        new("P028", "SOUNDEX", "string", "tuple", "scalar function", "SELECT SOUNDEX('hello')", "SELECT SOUNDEX(NULL)", "deterministic"),
        new("P029", "STRCMP", "string", "tuple", "scalar function", "SELECT STRCMP('a','b')", "SELECT STRCMP(NULL,'b')", "signed comparison result"),
        new("P030", "TO_BASE64", "string", "tuple", "scalar function", "SELECT TO_BASE64('abc')", "SELECT TO_BASE64(NULL)", "string input"),
        new("P031", "FROM_BASE64", "string", "tuple", "scalar function", "SELECT FROM_BASE64(TO_BASE64('abc'))", "SELECT FROM_BASE64(NULL)", "binary round trip"),
        new("P032", "UNHEX", "string", "tuple", "scalar function", "SELECT UNHEX('6162')", "SELECT UNHEX(NULL)", "binary result"),
        new("P033", "WEIGHT_STRING", "string", "tuple", "scalar function", "SELECT WEIGHT_STRING('abc')", "SELECT WEIGHT_STRING(NULL)", "no modifier clause"),
        new("P034", "CONV", "numeric", "tuple", "scalar function", "SELECT CONV('ff',16,10)", "SELECT CONV(NULL,16,10)", "string result"),
        new("P035", "CRC32", "numeric", "tuple", "scalar function", "SELECT CRC32('abc')", "SELECT CRC32(NULL)", "ISO 3309 polynomial"),
        new("P036", "LOG2", "numeric", "tuple", "scalar function", "SELECT LOG2(8)", "SELECT LOG2(NULL)", "double result"),
        new("P037", "RAND", "numeric", "tuple", "scalar function", "SELECT RAND(42)", "SELECT RAND(NULL)", "seeded; reproducible"),
        new("P038", "TRUNCATE", "numeric", "tuple", "scalar function", "SELECT TRUNCATE(1.2345,2)", "SELECT TRUNCATE(NULL,2)", "decimal literal"),
        new("P039", "BIT_COUNT", "bit", "tuple", "scalar function", "SELECT BIT_COUNT(5)", "SELECT BIT_COUNT(NULL)", "integer bit count"),
        new("P040", "BIT_AND", "bit", "tuple", "scalar function", "SELECT BIT_AND(x) FROM (SELECT 1 AS x UNION ALL SELECT 3) t", null, "aggregate"),
        new("P041", "BIT_OR", "bit", "tuple", "scalar function", "SELECT BIT_OR(x) FROM (SELECT 1 AS x UNION ALL SELECT 3) t", null, "aggregate"),
        new("P042", "BIT_XOR", "bit", "tuple", "scalar function", "SELECT BIT_XOR(x) FROM (SELECT 1 AS x UNION ALL SELECT 3) t", null, "aggregate"),
        new("P043", "ADDDATE", "date/time", "tuple", "scalar function", "SELECT ADDDATE('2020-01-01', INTERVAL 1 DAY)", "SELECT ADDDATE(NULL, INTERVAL 1 DAY)", "INTERVAL form"),
        new("P044", "ADDTIME", "date/time", "tuple", "scalar function", "SELECT ADDTIME('2020-01-01 00:00:00','01:00:00')", "SELECT ADDTIME(NULL,'01:00:00')", "datetime plus time"),
        new("P045", "CONVERT_TZ", "date/time", "tuple", "scalar function", "SELECT CONVERT_TZ('2020-01-01 00:00:00','+00:00','+02:00')", "SELECT CONVERT_TZ(NULL,'+00:00','+02:00')", "numeric offsets need no time-zone tables"),
        new("P046", "DAYNAME", "date/time", "tuple", "scalar function", "SELECT DAYNAME('2020-01-01')", "SELECT DAYNAME(NULL)", "deterministic"),
        new("P047", "FROM_DAYS", "date/time", "tuple", "scalar function", "SELECT FROM_DAYS(738000)", "SELECT FROM_DAYS(NULL)", "day number to date"),
        new("P048", "GET_FORMAT", "date/time", "tuple", "scalar function", "SELECT GET_FORMAT(DATE,'EUR')", null, "keyword enum argument"),
        new("P049", "MAKEDATE", "date/time", "tuple", "scalar function", "SELECT MAKEDATE(2020,32)", "SELECT MAKEDATE(NULL,32)", "year plus day-of-year"),
        new("P050", "MAKETIME", "date/time", "tuple", "scalar function", "SELECT MAKETIME(12,34,56)", "SELECT MAKETIME(NULL,34,56)", "integer seconds"),
        new("P051", "MICROSECOND", "date/time", "tuple", "scalar function", "SELECT MICROSECOND('2020-01-01 00:00:00.123456')", "SELECT MICROSECOND(NULL)", "datetime overload"),
        new("P052", "MONTHNAME", "date/time", "tuple", "scalar function", "SELECT MONTHNAME('2020-01-01')", "SELECT MONTHNAME(NULL)", "deterministic"),
        new("P053", "PERIOD_ADD", "date/time", "tuple", "scalar function", "SELECT PERIOD_ADD(202401,2)", "SELECT PERIOD_ADD(NULL,2)", "period is YYYYMM"),
        new("P054", "PERIOD_DIFF", "date/time", "tuple", "scalar function", "SELECT PERIOD_DIFF(202402,202401)", "SELECT PERIOD_DIFF(NULL,202401)", "period difference"),
        new("P055", "SEC_TO_TIME", "date/time", "tuple", "scalar function", "SELECT SEC_TO_TIME(3661)", "SELECT SEC_TO_TIME(NULL)", "seconds to TIME"),
        new("P056", "SUBDATE", "date/time", "tuple", "scalar function", "SELECT SUBDATE('2020-01-01', INTERVAL 1 DAY)", "SELECT SUBDATE(NULL, INTERVAL 1 DAY)", "INTERVAL form"),
        new("P057", "SUBTIME", "date/time", "tuple", "scalar function", "SELECT SUBTIME('2020-01-01 00:00:00','01:00:00')", "SELECT SUBTIME(NULL,'01:00:00')", "datetime minus time"),
        new("P058", "TIME", "date/time", "tuple", "scalar function", "SELECT TIME('2020-01-01 12:34:56')", "SELECT TIME(NULL)", "datetime overload"),
        new("P059", "TIME_FORMAT", "date/time", "tuple", "scalar function", "SELECT TIME_FORMAT('12:34:56','%H:%i')", "SELECT TIME_FORMAT(NULL,'%H:%i')", "format string result"),
        new("P060", "TIME_TO_SEC", "date/time", "tuple", "scalar function", "SELECT TIME_TO_SEC('12:34:56')", "SELECT TIME_TO_SEC(NULL)", "integer seconds"),
        new("P061", "TIMEDIFF", "date/time", "tuple", "scalar function", "SELECT TIMEDIFF('2020-01-02 00:00:00','2020-01-01 00:00:00')", "SELECT TIMEDIFF(NULL,'2020-01-01 00:00:00')", "datetime overload"),
        new("P062", "TIMESTAMP", "date/time", "tuple", "scalar function", "SELECT TIMESTAMP('2020-01-01 00:00:00')", "SELECT TIMESTAMP(NULL)", "single-argument overload"),
        new("P063", "TO_DAYS", "date/time", "tuple", "scalar function", "SELECT TO_DAYS('2020-01-01')", "SELECT TO_DAYS(NULL)", "integer result"),
        new("P064", "TO_SECONDS", "date/time", "tuple", "scalar function", "SELECT TO_SECONDS('2020-01-01 00:00:00')", "SELECT TO_SECONDS(NULL)", "integer result"),
        new("P065", "YEARWEEK", "date/time", "tuple", "scalar function", "SELECT YEARWEEK('2020-01-01')", "SELECT YEARWEEK(NULL)", "default mode"),
        new("P066", "AES_ENCRYPT", "crypto", "tuple", "scalar function", "SELECT AES_ENCRYPT('text','key')", "SELECT AES_ENCRYPT(NULL,'key')", "random IV; value non-deterministic, type/binary only"),
        new("P067", "AES_DECRYPT", "crypto", "tuple", "scalar function", "SELECT AES_DECRYPT(AES_ENCRYPT('text','key'),'key')", "SELECT AES_DECRYPT(NULL,'key')", "round trip of the random-IV form"),
        new("P068", "COMPRESS", "crypto", "tuple", "scalar function", "SELECT COMPRESS('abc')", "SELECT COMPRESS(NULL)", "zlib bytes"),
        new("P069", "UNCOMPRESS", "crypto", "tuple", "scalar function", "SELECT UNCOMPRESS(COMPRESS('abc'))", "SELECT UNCOMPRESS(NULL)", "round trip"),
        new("P070", "UNCOMPRESSED_LENGTH", "crypto", "tuple", "scalar function", "SELECT UNCOMPRESSED_LENGTH(COMPRESS('abc'))", "SELECT UNCOMPRESSED_LENGTH(NULL)", "payload length"),
        new("P071", "RANDOM_BYTES", "crypto", "tuple", "scalar function", "SELECT RANDOM_BYTES(4)", "SELECT RANDOM_BYTES(NULL)", "random; value non-deterministic"),
        new("P072", "VALIDATE_PASSWORD_STRENGTH", "crypto", "tuple", "scalar function", "SELECT VALIDATE_PASSWORD_STRENGTH('abc')", "SELECT VALIDATE_PASSWORD_STRENGTH(NULL)", "password policy helper"),
        new("P073", "STATEMENT_DIGEST", "information", "tuple", "scalar function", "SELECT STATEMENT_DIGEST('SELECT 1')", "SELECT STATEMENT_DIGEST(NULL)", "digest of a statement text"),
        new("P074", "STATEMENT_DIGEST_TEXT", "information", "tuple", "scalar function", "SELECT STATEMENT_DIGEST_TEXT('SELECT 1')", "SELECT STATEMENT_DIGEST_TEXT(NULL)", "normalised statement text"),
        new("P075", "FOUND_ROWS", "information", "tuple", "scalar function", "SELECT FOUND_ROWS()", null, "session state; value depends on the previous statement"),
        new("P076", "LAST_INSERT_ID", "information", "tuple", "scalar function", "SELECT LAST_INSERT_ID()", null, "session state"),
        new("P077", "ROW_COUNT", "information", "tuple", "scalar function", "SELECT ROW_COUNT()", null, "session state"),
        new("P078", "CURRENT_ROLE", "information", "tuple", "scalar function", "SELECT CURRENT_ROLE()", null, "also in the spec cell (both provances)"),
        new("P079", "CHARSET", "information", "tuple", "scalar function", "SELECT CHARSET('abc')", "SELECT CHARSET(NULL)", "connection charset of the argument"),
        new("P080", "COLLATION", "information", "tuple", "scalar function", "SELECT COLLATION('abc')", "SELECT COLLATION(NULL)", "collation of the argument"),
        new("P081", "COERCIBILITY", "information", "tuple", "scalar function", "SELECT COERCIBILITY('abc')", "SELECT COERCIBILITY(NULL)", "coercibility rank"),
        new("P082", "BENCHMARK", "information", "tuple", "scalar function", "SELECT BENCHMARK(1,1)", null, "count 1 keeps the probe bounded"),
        new("P083", "ICU_VERSION", "information", "tuple", "scalar function", "SELECT ICU_VERSION()", null, "MySQL-only ICU build identifier"),
        new("P084", "UUID_SHORT", "information", "tuple", "scalar function", "SELECT UUID_SHORT()", null, "random; value non-deterministic"),
        new("P085", "IS_UUID", "information", "tuple", "scalar function", "SELECT IS_UUID('6ccd780c-baba-1026-9564-5b8c656024db')", "SELECT IS_UUID(NULL)", "UUID text validation"),
        new("P086", "SLEEP", "miscellaneous", "tuple", "scalar function", "SELECT SLEEP(0)", "SELECT SLEEP(NULL)", "0 seconds keeps the probe bounded"),
        new("P087", "GROUPING", "aggregate", "tuple", "scalar function", "SELECT GROUPING(x) FROM (SELECT 1 AS x) t GROUP BY x WITH ROLLUP", null, "requires WITH ROLLUP"),
        new("P088", "NAME_CONST", "miscellaneous", "tuple", "scalar function", "SELECT NAME_CONST('x',1)", null, "named constant"),
        new("P089", "COLUMN_DEFAULT", "miscellaneous", "tuple", "scalar function", "SELECT DEFAULT(id) FROM t350_def", null, "DEFAULT(col) form on a table with a declared default", CreateDefaultTable, DropDefaultTable),

        // ---- the 35 concrete names of the MariaDB spec cell ----
        new("S001", "SFORMAT", "string", "spec", "scalar function", "SELECT SFORMAT('{}', 42)", "SELECT SFORMAT('{}', NULL)", "fmtlib-style formatter"),
        new("S002", "TRIM_ORACLE", "string", "spec", "scalar function", "SELECT TRIM_ORACLE('  a  ')", "SELECT TRIM_ORACLE(NULL)", "Oracle-compatible TRIM"),
        new("S003", "NATURAL_SORT_KEY", "string", "spec", "scalar function", "SELECT NATURAL_SORT_KEY('a10')", "SELECT NATURAL_SORT_KEY(NULL)", "natural ordering key"),
        new("S004", "LENGTHB", "string", "spec", "scalar function", "SELECT LENGTHB('abc')", "SELECT LENGTHB(NULL)", "byte length"),
        new("S005", "CHR", "string", "spec", "scalar function", "SELECT CHR(65)", "SELECT CHR(NULL)", "single code point"),
        new("S006", "TRUNC", "date/time", "spec", "scalar function", "SELECT TRUNC('2025-09-24 12:43','DD')", "SELECT TRUNC(NULL,'DD')", "date truncation; documented as Oracle-compatible"),
        new("S006-b", "TRUNC", "date/time", "spec", "scalar function", "SELECT TRUNC('2025-09-24 12:43','DD')", null, "same form under sql_mode=ORACLE", OracleModeSetup, OracleModeTeardown),
        new("S007", "FORMAT_PICO_TIME", "date/time", "spec", "scalar function", "SELECT FORMAT_PICO_TIME(1)", "SELECT FORMAT_PICO_TIME(NULL)", "picoseconds to text"),
        new("S008", "DECODE_ORACLE", "control flow", "spec", "scalar function", "SELECT DECODE_ORACLE(1, 1, 'a', 'b')", "SELECT DECODE_ORACLE(NULL, 1, 'a', 'b')", "Oracle DECODE"),
        new("S009", "ROWNUM", "information", "spec", "grammar form", "SELECT ROWNUM()", null, "function form; the bare ROWNUM pseudo-column needs Oracle mode"),
        new("S009-b", "ROWNUM", "information", "spec", "pseudo-column", "SELECT ROWNUM", null, "bare pseudo-column form under sql_mode=ORACLE", OracleModeSetup, OracleModeTeardown),
        new("S010", "DECODE_HISTOGRAM", "information", "spec", "scalar function", "SELECT DECODE_HISTOGRAM('SINGLE_PREC_HB', 'abc')", null, "existence is decided by the error class: a histogram-argument error still proves the function exists"),
        new("S011", "CURRENT_ROLE", "information", "both", "scalar function", "SELECT CURRENT_ROLE()", null, "already in the exclusion tuple"),
        new("S012", "BINLOG_GTID_POS", "information", "spec", "scalar function", "SELECT BINLOG_GTID_POS('', 0)", null, "requires binlog context"),
        new("S013", "ENCODE", "crypto", "spec", "scalar function", "SELECT ENCODE('abc','key')", "SELECT ENCODE(NULL,'key')", "symmetric pair with DECODE"),
        new("S014", "DECODE", "crypto", "spec", "scalar function", "SELECT DECODE(ENCODE('abc','key'),'key')", "SELECT DECODE(NULL,'key')", "symmetric pair with ENCODE"),
        new("S015", "ENCRYPT", "crypto", "spec", "scalar function", "SELECT ENCRYPT('abc','ab')", "SELECT ENCRYPT(NULL,'ab')", "unix crypt(); two-character salt keeps it deterministic"),
        new("S016", "DES_ENCRYPT", "crypto", "spec", "scalar function", "SELECT DES_ENCRYPT('abc','key')", "SELECT DES_ENCRYPT(NULL,'key')", "removed in 13.0; still present in the 11.4 range"),
        new("S017", "DES_DECRYPT", "crypto", "spec", "scalar function", "SELECT DES_DECRYPT(DES_ENCRYPT('abc','key'),'key')", "SELECT DES_DECRYPT(NULL,'key')", "pair of DES_ENCRYPT"),
        new("S018", "OLD_PASSWORD", "crypto", "spec", "scalar function", "SELECT OLD_PASSWORD('abc')", "SELECT OLD_PASSWORD(NULL)", "pre-4.1 hash"),
        new("S019", "CRC32C", "numeric", "spec", "scalar function", "SELECT CRC32C('abc')", "SELECT CRC32C(NULL)", "Castagnoli polynomial"),
        new("S020", "SYS_GUID", "miscellaneous", "spec", "scalar function", "SELECT SYS_GUID()", null, "random; value non-deterministic"),
        new("S021", "FORMAT_BYTES", "string", "spec", "scalar function", "SELECT FORMAT_BYTES(1024)", "SELECT FORMAT_BYTES(NULL)", "human-readable byte count"),
        new("S022", "JSON_LOOSE", "JSON", "spec", "scalar function", "SELECT JSON_LOOSE('{\"a\":1,\"b\":[2,3]}')", "SELECT JSON_LOOSE(NULL)", "readable JSON text"),
        new("S023", "JSON_NORMALIZE", "JSON", "both", "scalar function", "SELECT JSON_NORMALIZE('{\"b\":2,\"a\":1}')", "SELECT JSON_NORMALIZE(NULL)", "delivered by T284; already covered"),
        new("S024", "JSON_EQUALS", "JSON", "both", "scalar function", "SELECT JSON_EQUALS('{\"a\":1}','{\"a\":1}')", "SELECT JSON_EQUALS(NULL,'{\"a\":1}')", "delivered by T284; already covered"),
        new("S025", "JSON_OVERLAPS", "JSON", "both", "scalar function", "SELECT JSON_OVERLAPS('{\"a\":1}','{\"a\":1}')", "SELECT JSON_OVERLAPS(NULL,'{\"a\":1}')", "already in the exclusion tuple"),
        new("S026", "JSON_ARRAY_INTERSECT", "JSON", "spec", "scalar function", "SELECT JSON_ARRAY_INTERSECT('[1,2,3]','[1,2,4]')", "SELECT JSON_ARRAY_INTERSECT(NULL,'[1,2,4]')", "11.2+"),
        new("S027", "JSON_KEY_VALUE", "JSON", "spec", "scalar function", "SELECT JSON_KEY_VALUE('{\"a\":1}','$')", "SELECT JSON_KEY_VALUE(NULL,'$')", "11.2+"),
        new("S028", "JSON_OBJECT_FILTER_KEYS", "JSON", "spec", "scalar function", "SELECT JSON_OBJECT_FILTER_KEYS('{\"a\":1,\"b\":2}','[\"a\"]')", null, "11.2+"),
        new("S029", "JSON_OBJECT_TO_ARRAY", "JSON", "spec", "scalar function", "SELECT JSON_OBJECT_TO_ARRAY('{\"a\":1}')", "SELECT JSON_OBJECT_TO_ARRAY(NULL)", "11.2+"),
        new("S030", "JSON_SCHEMA_VALID", "JSON", "both", "scalar function", "SELECT JSON_SCHEMA_VALID('{\"type\":\"object\"}','{\"a\":1}')", null, "already in the exclusion tuple"),
        new("S031", "MEDIAN", "aggregate", "spec", "scalar function", "SELECT MEDIAN(x) OVER () FROM (SELECT 1 AS x UNION ALL SELECT 3) t", null, "window form; already covered by SupportsPercentileWindow"),
        new("S032", "PERCENTILE_CONT", "aggregate", "spec", "scalar function", "SELECT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY x) OVER () FROM (SELECT 1 AS x UNION ALL SELECT 3) t", null, "window form; already covered by SupportsPercentileWindow"),
        new("S033", "PERCENTILE_DISC", "aggregate", "spec", "scalar function", "SELECT PERCENTILE_DISC(0.5) WITHIN GROUP (ORDER BY x) OVER () FROM (SELECT 1 AS x UNION ALL SELECT 3) t", null, "window form; already covered by SupportsPercentileWindow"),
        new("S034", "NEXT VALUE FOR", "sequence", "spec", "grammar form", "SELECT NEXT VALUE FOR t350_seq", null, "sequence control for PREVIOUS VALUE FOR", CreateSequence, null),
        new("S035", "PREVIOUS VALUE FOR", "sequence", "spec", "grammar form", "SELECT PREVIOUS VALUE FOR t350_seq", null, "requires a NEXT VALUE FOR in the same session; S034 precedes it", null, DropSequence),
        new("S036", "_rowid", "pseudo-column", "spec", "pseudo-column", "SELECT _rowid FROM t350_rowid", null, "pseudo-column on a table with a single integer primary key", CreateRowIdTable, DropRowIdTable),

        // ---- one version-scoped expansion of the wildcard groups ----
        new("W001", "COLUMN_CREATE", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_CREATE('a',1)", null, "COLUMN_* expansion"),
        new("W002", "COLUMN_ADD", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_ADD(COLUMN_CREATE('a',1),'b',2)", null, "COLUMN_* expansion"),
        new("W003", "COLUMN_DELETE", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_DELETE(COLUMN_CREATE('a',1,'b',2),'b')", null, "COLUMN_* expansion"),
        new("W004", "COLUMN_GET", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_GET(COLUMN_CREATE('a',1),'a' AS unsigned)", null, "COLUMN_* expansion; the AS type clause is part of the signature"),
        new("W004-b", "COLUMN_GET", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_GET(COLUMN_CREATE('a',1),'a')", null, "COLUMN_* negative control: the AS type token is mandatory, so the two-argument form must be refused"),
        new("W004-c", "COLUMN_GET", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_GET(COLUMN_CREATE('a',1),'a' AS char)", null, "COLUMN_* expansion; the AS type token selects the CLR type (char -> string; the typed form exists only on COLUMN_GET)"),
        new("W005", "COLUMN_JSON", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_JSON(COLUMN_CREATE('a',1))", null, "COLUMN_* expansion"),
        new("W006", "COLUMN_LIST", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_LIST(COLUMN_CREATE('a',1))", null, "COLUMN_* expansion"),
        new("W007", "COLUMN_EXISTS", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_EXISTS(COLUMN_CREATE('a',1),'a')", null, "COLUMN_* expansion"),
        new("W008", "COLUMN_CHECK", "dynamic columns", "wildcard", "scalar function", "SELECT COLUMN_CHECK(COLUMN_CREATE('a',1))", null, "COLUMN_* expansion"),
        new("W009", "WSREP_LAST_SEEN_GTID", "galera", "wildcard", "scalar function", "SELECT WSREP_LAST_SEEN_GTID()", null, "WSREP_* expansion; recorded on the default non-Galera server"),
        new("W010", "WSREP_LAST_WRITTEN_GTID", "galera", "wildcard", "scalar function", "SELECT WSREP_LAST_WRITTEN_GTID()", null, "WSREP_* expansion; recorded on the default non-Galera server"),
        new("W011", "WSREP_SYNC_WAIT_UPTO_GTID", "galera", "wildcard", "scalar function", "SELECT WSREP_SYNC_WAIT_UPTO_GTID('0-0-0')", null, "WSREP_* expansion; blocking/waiter semantics on a non-Galera server"),
        new("W012", "VEC_FromText", "vector", "wildcard", "scalar function", "SELECT VEC_FromText('[1,2]')", null, "VEC_* expansion; documented as 11.7+"),
        new("W013", "VEC_ToText", "vector", "wildcard", "scalar function", "SELECT VEC_ToText(VEC_FromText('[1,2]'))", null, "VEC_* expansion; documented as 11.7+"),
        new("W014", "VEC_DISTANCE_EUCLIDEAN", "vector", "wildcard", "scalar function", "SELECT VEC_DISTANCE_EUCLIDEAN(VEC_FromText('[1,2]'),VEC_FromText('[2,3]'))", null, "VEC_* expansion; documented as 11.7+"),
        new("W015", "VEC_DISTANCE_COSINE", "vector", "wildcard", "scalar function", "SELECT VEC_DISTANCE_COSINE(VEC_FromText('[1,2]'),VEC_FromText('[2,3]'))", null, "VEC_* expansion; documented as 11.7+"),
        new("W016", "VEC_DISTANCE", "vector", "wildcard", "scalar function", "SELECT VEC_DISTANCE(VEC_FromText('[1,2]'),VEC_FromText('[2,3]'))", null, "VEC_* expansion; documented as 11.7+")
    ];
}
