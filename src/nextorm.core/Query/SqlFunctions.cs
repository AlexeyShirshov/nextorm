using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Static entry point for SQL constructs that are written directly inside query expressions.
/// </summary>
/// <remarks>
/// Renamed from <c>NORM</c> (all-caps abbreviation). The nested function surfaces were renamed too:
/// <c>NORM_SQL</c> → <see cref="CommonFunctions"/>, <c>PG</c>/<c>MS</c>/<c>CLK</c> →
/// <see cref="PostgresFunctions"/>/<see cref="SqlServerFunctions"/>/<see cref="ClickHouseFunctions"/>.
/// Method names intentionally mirror SQL tokens (<c>count_big</c>, <c>@in</c>, ...), which the .NET
/// guidelines would not; see <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-10.
/// </remarks>
public static partial class SqlFunctions
{
    /// <summary>
    /// Cross-provider SQL function surface. The reference is only a marker inside a query expression:
    /// the SQL providers translate the call, and the in-memory provider rewrites the few members it
    /// can evaluate (<see cref="CommonFunctions.collate"/>) before compiling the expression.
    /// </summary>
    public static CommonFunctions Sql => default!;

    /// <summary>
    /// Surface of the PostgreSQL-only functions (arrays, native JSON, the extended scalar library,
    /// PG-only aggregates and the <c>generate_series</c>/<c>unnest</c> table functions). Every
    /// member requires <see cref="ISqlDialect.SupportsArrays"/> or the matching capability flag;
    /// other providers reject it with a clear message.
    /// </summary>
    public static PostgresFunctions Postgres { get; } = new();

    /// <summary>
    /// Surface of the text-JSON functions (SQL Server and MySQL/MariaDB), the SQL Server-only
    /// <c>choose</c> conditional function (the portable <c>iif</c> is on <see cref="CommonFunctions"/>)
    /// and the SQL Server <c>string_split</c>/<c>openjson</c> table functions. Every member requires
    /// <see cref="ISqlDialect.SupportsTextJson"/>, <see cref="ISqlDialect.SupportsChoose"/> or
    /// <see cref="ISqlDialect.SupportsTableFunction"/>; other providers reject it with a clear message.
    /// </summary>
    public static SqlServerFunctions SqlServer { get; } = new();

    /// <summary>
    /// Surface of the ClickHouse-only functions (the <c>argMin</c>/<c>argMax</c>, <c>uniq*</c>,
    /// parameterised <c>quantile*</c>/<c>median</c> and the <c>anyLast</c> aggregate, the
    /// string-JSON <c>JSONExtract*</c>/<c>visitParamExtract*</c> family plus the JSONPath
    /// <c>json_value</c>/<c>json_query</c>/<c>json_exists</c> scalars, the dictionary functions, the
    /// <c>-If</c> combinator, the distributed <c>global_in</c> predicate and the <c>numbers</c>/<c>numbers_mt</c>
    /// and <c>zeros</c>/<c>zeros_mt</c> table functions (plus <c>generateRandom</c> and the server/cluster
    /// table functions <c>url</c>/<c>s3</c>/<c>file</c>/<c>remote</c>/<c>remoteSecure</c>/<c>cluster</c>/<c>clusterAllReplicas</c>), the date conversion/part surface
    /// (<c>toDate</c>/<c>toDateTime</c>/<c>toDate32</c>, <c>toYear</c>/..., <c>toStartOf*</c>,
    /// <c>toMonday</c>, <c>toYYYYMM</c>/<c>toYYYYMMDD</c>, <c>toUnixTimestamp</c>), as well as the array functions over array
    /// columns (<c>arrayJoin</c>, <c>length</c>, <c>has</c>, <c>indexOf</c>, <c>hasAny</c>/<c>hasAll</c>,
    /// <c>startsWith</c>/<c>endsWith</c>/<c>hasSubstr</c>,
    /// <c>arrayStringConcat</c>, <c>splitByChar</c>, <c>arraySort</c>, <c>arrayReverse</c>,
    /// <c>arrayDistinct</c>). Every member is
    /// gated by a capability flag
    /// (<see cref="ISqlDialect.SupportsArgMinMax"/>, <see cref="ISqlDialect.AggregateFilterStyle"/>, …);
    /// other providers reject it with a clear message.
    /// </summary>
    public static ClickHouseFunctions ClickHouse { get; } = new();

    /// <summary>
    /// Surface of the MySQL/MariaDB-only functions (the native string/conditional idioms, the
    /// <c>%</c>-templated date conversion and Unix-epoch functions, the hexadecimal hashes, the IPv4
    /// conversion pair, the JSON mutation family and the binary UUID pair). Every member is gated per
    /// name by <see cref="ISqlDialect.MySqlFunctions"/>; other providers reject it with a clear message.
    /// </summary>
    public static MySqlFunctions MySql => default!;

    /// <summary>
    /// References the <paramref name="idx"/>-th positional parameter of the command (for example
    /// <c>@p0</c>). Only valid inside a query expression.
    /// </summary>
    /// <typeparam name="T">The CLR type of the parameter value.</typeparam>
    /// <param name="idx">The zero-based parameter index.</param>
    /// <returns>The parameter value, materialized as <typeparamref name="T"/>.</returns>
    public static T Parameter<T>(int idx) => default!;

    /// <summary>
    /// References a column of an entity source by its database name with an explicit CLR type, for
    /// entities that do not declare a property for the column (for example a wide ClickHouse table).
    /// The value is materialized as <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type of the column value.</typeparam>
    /// <param name="entity">The entity source the column belongs to (a query lambda parameter).</param>
    /// <param name="columnName">
    /// The <b>raw</b> database column name. It is not SQL text: it is always safely quoted by the
    /// provider, regardless of the command's <c>QuoteIdentifiers</c> option. A dot is a literal name
    /// character (not a schema/alias qualifier), SQL-looking punctuation stays inside the one
    /// identifier, and the name is never stripped of brackets. Manual pre-quoting is not supported, so
    /// <c>Column(t, "[1]")</c> is the literal three-character name <c>[1]</c> (rendered <c>[[1]]]</c> on
    /// SQL Server); to reference the column named <c>1</c> write <c>Column(t, "1")</c>.
    /// </param>
    /// <remarks>
    /// Only valid inside a query expression. The in-memory provider has no column-name concept and
    /// rejects the query.
    /// </remarks>
    public static T Column<T>(object entity, string columnName) => throw new NotSupportedException(
        "SqlFunctions.Column can only be used inside a query expression; the in-memory provider does not support column-by-name access.");

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.generate_series(long, long)"/>: a single column
    /// named <c>generate_series</c> holding the generated number.
    /// </summary>
    public interface IGenerateSeriesRow
    {
        /// <summary>The generated number.</summary>
        [Column("generate_series")]
        long Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.unnest{T}(T[])"/>: a single column named
    /// <c>unnest</c> holding the array element.
    /// </summary>
    public interface IUnnestRow<T>
    {
        /// <summary>The array element.</summary>
        [Column("unnest")]
        T Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.unnest{T1,T2}(T1[], T2[])"/>: one column per
    /// array, named <c>value1</c>/<c>value2</c> after the position of the source array. Unequal array
    /// lengths are NULL-padded by the server to the longest array.
    /// </summary>
    /// <typeparam name="T1">The element type of the first array.</typeparam>
    /// <typeparam name="T2">The element type of the second array.</typeparam>
    public interface IUnnestMultiRow<T1, T2>
    {
        /// <summary>The element from the first array (NULL when that array is shorter).</summary>
        [Column("value1")]
        T1 Value1 { get; set; }
        /// <summary>The element from the second array (NULL when that array is shorter).</summary>
        [Column("value2")]
        T2 Value2 { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.unnest{T1,T2,T3}(T1[], T2[], T3[])"/>: the
    /// three admitted array positions, NULL-padded to the longest array.
    /// </summary>
    /// <typeparam name="T1">The element type of the first array.</typeparam>
    /// <typeparam name="T2">The element type of the second array.</typeparam>
    /// <typeparam name="T3">The element type of the third array.</typeparam>
    public interface IUnnestMultiRow<T1, T2, T3>
    {
        /// <summary>The element from the first array (NULL when that array is shorter).</summary>
        [Column("value1")]
        T1 Value1 { get; set; }
        /// <summary>The element from the second array (NULL when that array is shorter).</summary>
        [Column("value2")]
        T2 Value2 { get; set; }
        /// <summary>The element from the third array (NULL when that array is shorter).</summary>
        [Column("value3")]
        T3 Value3 { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.unnest_with_ordinality{T}(T[])"/>: the element
    /// and its one-based position. PostgreSQL types the ordinality column as <c>bigint</c>, so it is
    /// <see cref="long"/> here.
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    public interface IUnnestOrdinalityRow<T>
    {
        /// <summary>The array element.</summary>
        [Column("value")]
        T Value { get; set; }
        /// <summary>The one-based position of the element (<c>bigint</c>).</summary>
        [Column("ordinality")]
        long Ordinality { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.unnest_with_ordinality{T1,T2}(T1[], T2[])"/>:
    /// one column per admitted array plus the shared one-based position (<c>bigint</c>, so
    /// <see cref="long"/>). Unequal arrays are NULL-padded to the longest.
    /// </summary>
    /// <typeparam name="T1">The element type of the first array.</typeparam>
    /// <typeparam name="T2">The element type of the second array.</typeparam>
    public interface IUnnestOrdinalityMultiRow<T1, T2>
    {
        /// <summary>The element from the first array (NULL when that array is shorter).</summary>
        [Column("value1")]
        T1 Value1 { get; set; }
        /// <summary>The element from the second array (NULL when that array is shorter).</summary>
        [Column("value2")]
        T2 Value2 { get; set; }
        /// <summary>The one-based position of the row (<c>bigint</c>).</summary>
        [Column("ordinality")]
        long Ordinality { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.generate_subscripts{T}(T[], int?)"/>: the single
    /// <c>generate_subscripts</c> column holds one subscript of the requested dimension.
    /// </summary>
    public interface IGenerateSubscriptsRow
    {
        /// <summary>One valid subscript of the requested array dimension.</summary>
        [Column("generate_subscripts")]
        int Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.regexp_matches(string?, string?)"/>: the single
    /// <c>regexp_matches</c> column is a <c>text[]</c> holding the captured groups (the whole match when
    /// there are no groups). PostgreSQL returns <c>setof text[]</c>.
    /// </summary>
    public interface IRegexpMatchesRow
    {
        /// <summary>The captured groups of one match, or the whole match when there are no groups.</summary>
        [Column("regexp_matches")]
        string[] Matches { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.regexp_split_to_table(string?, string?)"/>: the
    /// single <c>regexp_split_to_table</c> column holds one fragment.
    /// </summary>
    public interface IRegexpSplitToTableRow
    {
        /// <summary>One fragment produced by the split.</summary>
        [Column("regexp_split_to_table")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.string_to_table(string?, string?)"/>: the
    /// single <c>string_to_table</c> column holds one fragment.
    /// </summary>
    public interface IStringToTableRow
    {
        /// <summary>One fragment produced by the split; NULL when it matched the <c>null_string</c> argument.</summary>
        [Column("string_to_table")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.jsonb_array_elements(object?)"/>,
    /// <see cref="PostgresFunctions.jsonb_array_elements_text(object?)"/>,
    /// <see cref="PostgresFunctions.json_array_elements(string?)"/> and
    /// <see cref="PostgresFunctions.json_array_elements_text(string?)"/>: the single <c>value</c> column
    /// holds one array element (a <c>json</c>/<c>jsonb</c> document rendered as text, or plain text).
    /// </summary>
    public interface IJsonArrayElementsRow
    {
        /// <summary>One array element (a <c>json</c>/<c>jsonb</c> document rendered as text, or plain text).</summary>
        [Column("value")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.jsonb_each(object?)"/>,
    /// <see cref="PostgresFunctions.jsonb_each_text(object?)"/>,
    /// <see cref="PostgresFunctions.json_each(string?)"/> and
    /// <see cref="PostgresFunctions.json_each_text(string?)"/>: the <c>key</c>/<c>value</c> columns of
    /// one object property (the value is a <c>json</c>/<c>jsonb</c> document rendered as text, or plain
    /// text). All four functions return exactly two columns on PostgreSQL 17, so they share this shape.
    /// </summary>
    public interface IJsonbEachRow
    {
        /// <summary>The property name.</summary>
        [Column("key")]
        string? Key { get; set; }
        /// <summary>The property value (a jsonb document rendered as text, or plain text).</summary>
        [Column("value")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.jsonb_object_keys(object?)"/>: the single
    /// <c>jsonb_object_keys</c> column holds one top-level object key.
    /// </summary>
    public interface IJsonObjectKeysRow
    {
        /// <summary>One top-level object key.</summary>
        [Column("jsonb_object_keys")]
        string? Key { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.jsonb_path_query(object?, string?)"/>: the single
    /// <c>jsonb_path_query</c> column holds one JSONPath match (a jsonb document rendered as text).
    /// </summary>
    public interface IJsonPathQueryRow
    {
        /// <summary>One JSONPath match (a jsonb document rendered as text).</summary>
        [Column("jsonb_path_query")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.jsonb_path_query_tz(string?, string?, string?, bool?)"/>:
    /// the single <c>jsonb_path_query_tz</c> column holds one time-zone-aware JSONPath match (a jsonb
    /// document rendered as text). The column is named after the function, unlike the <c>value</c> column of
    /// the array readers, so the row shape is separate from <see cref="IJsonPathQueryRow"/>.
    /// </summary>
    public interface IJsonPathQueryTzRow
    {
        /// <summary>One time-zone-aware JSONPath match (a jsonb document rendered as text).</summary>
        [Column("jsonb_path_query_tz")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.ts_stat(string?)"/>: one statistic for a lexeme
    /// (<c>word</c>, document count <c>ndoc</c>, total occurrences <c>nentry</c>).
    /// </summary>
    public interface ITsStatRow
    {
        /// <summary>The lexeme.</summary>
        [Column("word")]
        string? Word { get; set; }
        /// <summary>The number of documents containing the lexeme.</summary>
        [Column("ndoc")]
        int? Ndoc { get; set; }
        /// <summary>The total number of occurrences of the lexeme.</summary>
        [Column("nentry")]
        int? Nentry { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.ts_debug(string?)"/>: one tokenizer step of
    /// the default text-search configuration for the document. The six columns are the documented
    /// <c>ts_debug</c> result on PostgreSQL 17 — <c>alias</c>, <c>description</c>, <c>token</c>,
    /// <c>dictionaries</c> (<c>regdictionary[]</c>), <c>dictionary</c> (<c>regdictionary</c>) and
    /// <c>lexemes</c> (<c>text[]</c>). The two dictionary columns have no CLR mapping and are read as
    /// text (the emitted projection casts them: <c>dictionaries::text[]</c> and
    /// <c>dictionary::text</c>), exactly like the <c>regclass</c>/<c>oid</c> columns of the
    /// system-information readers.
    /// </summary>
    public interface ITsDebugRow
    {
        /// <summary>The token type alias (<c>asciiword</c>, <c>blank</c>, <c>word</c>, …).</summary>
        [Column("alias")]
        string? Alias { get; set; }
        /// <summary>The human-readable description of the token type.</summary>
        [Column("description")]
        string? Description { get; set; }
        /// <summary>The token text extracted from the document.</summary>
        [Column("token")]
        string? Token { get; set; }
        /// <summary>The dictionaries consulted for the token, as their textual names.</summary>
        [Column("dictionaries")]
        string[]? Dictionaries { get; set; }
        /// <summary>The dictionary that produced the lexemes, as its textual name; NULL when none matched.</summary>
        [Column("dictionary")]
        string? Dictionary { get; set; }
        /// <summary>The lexemes the token normalised to; an empty array when the token is not a word.</summary>
        [Column("lexemes")]
        string[]? Lexemes { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.ts_parse(string?, string?)"/>: one token of
    /// the document as the named text-search parser split it (<c>tokid</c> is the token type id,
    /// <c>token</c> the token text).
    /// </summary>
    public interface ITsParseRow
    {
        /// <summary>The token type id of the token.</summary>
        [Column("tokid")]
        int? Tokid { get; set; }
        /// <summary>The token text.</summary>
        [Column("token")]
        string? Token { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.ts_token_type(string?)"/>: the token types
    /// the named text-search parser recognises (<c>tokid</c>, <c>alias</c>, <c>description</c>).
    /// </summary>
    public interface ITsTokenTypeRow
    {
        /// <summary>The token type id.</summary>
        [Column("tokid")]
        int? Tokid { get; set; }
        /// <summary>The token type alias (<c>asciiword</c>, <c>blank</c>, <c>word</c>, …).</summary>
        [Column("alias")]
        string? Alias { get; set; }
        /// <summary>The human-readable description of the token type.</summary>
        [Column("description")]
        string? Description { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_control_system()"/>: the one
    /// <c>pg_control_system</c> record (control-file version, catalog version, the cluster's system
    /// identifier and the control file's modification time).
    /// </summary>
    public interface IPgControlSystemRow
    {
        /// <summary>The <c>pg_control</c> format version (1700 on PostgreSQL 17).</summary>
        [Column("pg_control_version")]
        int? PgControlVersion { get; set; }
        /// <summary>The catalog version number of the running server.</summary>
        [Column("catalog_version_no")]
        int? CatalogVersionNo { get; set; }
        /// <summary>The cluster-wide system identifier from <c>pg_control</c>.</summary>
        [Column("system_identifier")]
        long? SystemIdentifier { get; set; }
        /// <summary>The time the control file was last written.</summary>
        [Column("pg_control_last_modified")]
        DateTime? PgControlLastModified { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_control_init()"/>: the compile-time and
    /// <c>pg_control</c> constants of the running server (alignment, block sizes, identifier limits).
    /// </summary>
    public interface IPgControlInitRow
    {
        /// <summary>The maximum alignment the server guarantees.</summary>
        [Column("max_data_alignment")]
        int? MaxDataAlignment { get; set; }
        /// <summary>The database block size (<c>BLCKSZ</c>).</summary>
        [Column("database_block_size")]
        int? DatabaseBlockSize { get; set; }
        /// <summary>The number of blocks per segment file.</summary>
        [Column("blocks_per_segment")]
        int? BlocksPerSegment { get; set; }
        /// <summary>The WAL block size (<c>XLOG_BLCKSZ</c>).</summary>
        [Column("wal_block_size")]
        int? WalBlockSize { get; set; }
        /// <summary>The size of one WAL segment file, in bytes.</summary>
        [Column("bytes_per_wal_segment")]
        int? BytesPerWalSegment { get; set; }
        /// <summary>The maximum length of an identifier, in bytes.</summary>
        [Column("max_identifier_length")]
        int? MaxIdentifierLength { get; set; }
        /// <summary>The maximum number of columns in an index.</summary>
        [Column("max_index_columns")]
        int? MaxIndexColumns { get; set; }
        /// <summary>The maximum size of a <c>TOAST</c>ed chunk, in bytes.</summary>
        [Column("max_toast_chunk_size")]
        int? MaxToastChunkSize { get; set; }
        /// <summary>The chunk size used for large objects, in bytes.</summary>
        [Column("large_object_chunk_size")]
        int? LargeObjectChunkSize { get; set; }
        /// <summary>Whether <c>float8</c> is passed by value on this build.</summary>
        [Column("float8_pass_by_value")]
        bool? Float8PassByValue { get; set; }
        /// <summary>The data page checksum version the cluster was initialised with.</summary>
        [Column("data_page_checksum_version")]
        int? DataPageChecksumVersion { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_control_recovery()"/>: the recovery
    /// state recorded in the control file. The <c>pg_lsn</c> columns are read as text
    /// (<c>0/0</c> when unset), because the driver's LSN type has no CLR mapping in the row mapper.
    /// </summary>
    public interface IPgControlRecoveryRow
    {
        /// <summary>The minimum recovery end LSN, as text.</summary>
        [Column("min_recovery_end_lsn")]
        string? MinRecoveryEndLsn { get; set; }
        /// <summary>The timeline of the minimum recovery end point.</summary>
        [Column("min_recovery_end_timeline")]
        int? MinRecoveryEndTimeline { get; set; }
        /// <summary>The start LSN of the backup, as text.</summary>
        [Column("backup_start_lsn")]
        string? BackupStartLsn { get; set; }
        /// <summary>The end LSN of the backup, as text.</summary>
        [Column("backup_end_lsn")]
        string? BackupEndLsn { get; set; }
        /// <summary>Whether the end-of-backup record is still required.</summary>
        [Column("end_of_backup_record_required")]
        bool? EndOfBackupRecordRequired { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_control_checkpoint()"/>: the checkpoint
    /// state recorded in the control file. <c>pg_lsn</c> and <c>oid</c> columns are read as text and
    /// <c>xid</c> columns as <see cref="uint"/>, the CLR type the driver exposes for <c>xid</c>.
    /// </summary>
    public interface IPgControlCheckpointRow
    {
        /// <summary>The checkpoint's redo location, as text.</summary>
        [Column("checkpoint_lsn")]
        string? CheckpointLsn { get; set; }
        /// <summary>The redo location the next WAL record must start at, as text.</summary>
        [Column("redo_lsn")]
        string? RedoLsn { get; set; }
        /// <summary>The WAL file that contains <see cref="RedoLsn"/>.</summary>
        [Column("redo_wal_file")]
        string? RedoWalFile { get; set; }
        /// <summary>The timeline the checkpoint belongs to.</summary>
        [Column("timeline_id")]
        int? TimelineId { get; set; }
        /// <summary>The previous timeline identifier.</summary>
        [Column("prev_timeline_id")]
        int? PrevTimelineId { get; set; }
        /// <summary>Whether full-page writes are enabled.</summary>
        [Column("full_page_writes")]
        bool? FullPageWrites { get; set; }
        /// <summary>The next transaction id, in its textual <c>epoch:counter</c> form.</summary>
        [Column("next_xid")]
        string? NextXid { get; set; }
        /// <summary>The next OID, as text.</summary>
        [Column("next_oid")]
        string? NextOid { get; set; }
        /// <summary>The next multixact id.</summary>
        [Column("next_multixact_id")]
        uint? NextMultixactId { get; set; }
        /// <summary>The next multixact offset.</summary>
        [Column("next_multi_offset")]
        uint? NextMultiOffset { get; set; }
        /// <summary>The oldest transaction id that has not been vacuumed.</summary>
        [Column("oldest_xid")]
        uint? OldestXid { get; set; }
        /// <summary>The database of <see cref="OldestXid"/>, as text.</summary>
        [Column("oldest_xid_dbid")]
        string? OldestXidDbId { get; set; }
        /// <summary>The oldest transaction id that is still running.</summary>
        [Column("oldest_active_xid")]
        uint? OldestActiveXid { get; set; }
        /// <summary>The oldest multixact id.</summary>
        [Column("oldest_multi_xid")]
        uint? OldestMultiXid { get; set; }
        /// <summary>The database of <see cref="OldestMultiXid"/>, as text.</summary>
        [Column("oldest_multi_dbid")]
        string? OldestMultiDbId { get; set; }
        /// <summary>The oldest transaction id with a commit timestamp.</summary>
        [Column("oldest_commit_ts_xid")]
        uint? OldestCommitTsXid { get; set; }
        /// <summary>The newest transaction id with a commit timestamp.</summary>
        [Column("newest_commit_ts_xid")]
        uint? NewestCommitTsXid { get; set; }
        /// <summary>The time of the checkpoint.</summary>
        [Column("checkpoint_time")]
        DateTime? CheckpointTime { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_get_keywords()"/>: one SQL keyword or
    /// keyword-like token per row. <c>catcode</c> is PostgreSQL's internal <c>"char"</c> one-byte
    /// category code, read as text because the row mapper has no <see cref="char"/> accessor.
    /// </summary>
    public interface IPgKeywordRow
    {
        /// <summary>The keyword or token text.</summary>
        [Column("word")]
        string? Word { get; set; }
        /// <summary>The one-letter category code (<c>U</c>, <c>C</c>, <c>T</c> or <c>R</c>), as text.</summary>
        [Column("catcode")]
        string? CatCode { get; set; }
        /// <summary>Whether the keyword can be used as a bare column label.</summary>
        [Column("barelabel")]
        bool? BareLabel { get; set; }
        /// <summary>The human-readable description of the category.</summary>
        [Column("catdesc")]
        string? CatDesc { get; set; }
        /// <summary>The bare-label description.</summary>
        [Column("baredesc")]
        string? BareDesc { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_listening_channels()"/>: a single column
    /// named <c>pg_listening_channels</c> holding one channel name this session is listening on.
    /// </summary>
    public interface IListeningChannelRow
    {
        /// <summary>The name of a channel the current session has <c>LISTEN</c>ed on.</summary>
        [Column("pg_listening_channels")]
        string? Channel { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_snapshot_xip(string?)"/>: a single column
    /// named <c>pg_snapshot_xip</c> holding one in-progress transaction id of the snapshot. The id is
    /// read as text (<c>xid8</c> has no CLR mapping in the row mapper), exactly like the scalar
    /// <c>pg_snapshot_xmin</c>/<c>pg_snapshot_xmax</c> members.
    /// </summary>
    public interface ISnapshotXipRow
    {
        /// <summary>An in-progress transaction id of the snapshot, in its textual form.</summary>
        [Column("pg_snapshot_xip")]
        string? Xid { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_tablespace_databases(string?)"/>: a single
    /// column named <c>pg_tablespace_databases</c> holding one database OID, read as text like the
    /// other <c>oid</c>-returning members of the family.
    /// </summary>
    public interface ITablespaceDatabaseRow
    {
        /// <summary>The OID of a database that has files in the tablespace, as text.</summary>
        [Column("pg_tablespace_databases")]
        string? DatabaseOid { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_get_catalog_foreign_keys()"/>: one
    /// catalog foreign-key relationship per row. Both table columns are <c>regclass</c> and both column
    /// lists are <c>text[]</c>; the tables are read as text (<c>::text</c> in the projection) and the
    /// arrays keep their element order, including NULL elements the server may return.
    /// </summary>
    public interface IPgCatalogForeignKeyRow
    {
        /// <summary>The referencing catalog, as its textual <c>regclass</c> form.</summary>
        [Column("fktable")]
        string? FkTable { get; set; }
        /// <summary>The referencing column names.</summary>
        [Column("fkcols")]
        string[]? FkColumns { get; set; }
        /// <summary>The referenced catalog, as its textual <c>regclass</c> form.</summary>
        [Column("pktable")]
        string? PkTable { get; set; }
        /// <summary>The referenced column names.</summary>
        [Column("pkcols")]
        string[]? PkColumns { get; set; }
        /// <summary>Whether the reference points at an array column.</summary>
        [Column("is_array")]
        bool? IsArray { get; set; }
        /// <summary>Whether the reference is optional (the column may be NULL).</summary>
        [Column("is_opt")]
        bool? IsOptional { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_options_to_table(string?[])"/>: one
    /// storage-option entry per row (the same shape <c>unnest(reloptions)</c> would split by hand).
    /// </summary>
    public interface IPgOptionRow
    {
        /// <summary>The option name (the part before <c>=</c>).</summary>
        [Column("option_name")]
        string? OptionName { get; set; }
        /// <summary>The option value, or NULL for a flag-style option.</summary>
        [Column("option_value")]
        string? OptionValue { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_available_wal_summaries()"/>: one WAL
    /// summary file per row. The <c>pg_lsn</c> bounds are read as text (the row mapper has no LSN
    /// accessor); the set is empty until the server has written summaries (it needs
    /// <c>summarize_wal = on</c>).
    /// </summary>
    public interface IPgAvailableWalSummaryRow
    {
        /// <summary>The timeline the summary belongs to.</summary>
        [Column("tli")]
        long? TimelineId { get; set; }
        /// <summary>The first LSN covered by the summary, as text.</summary>
        [Column("start_lsn")]
        string? StartLsn { get; set; }
        /// <summary>The last LSN covered by the summary, as text.</summary>
        [Column("end_lsn")]
        string? EndLsn { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_get_multixact_members(uint?)"/>: one row
    /// per transaction that is a member of the multixact. Nothing is returned for a multixact id the
    /// server has not created yet.
    /// </summary>
    public interface IPgMultixactMemberRow
    {
        /// <summary>The member transaction id.</summary>
        [Column("xid")]
        uint? Xid { get; set; }
        /// <summary>The lock mode the member holds (for example <c>sh</c> or <c>nokeyex</c>).</summary>
        [Column("mode")]
        string? Mode { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_get_object_address(string?, string?[], string?[])"/>:
    /// the catalog coordinates of one object. Both OIDs are read as text, like the other
    /// <c>oid</c>-returning members of the family.
    /// </summary>
    public interface IPgObjectAddressRow
    {
        /// <summary>The OID of the catalog that holds the object, as text.</summary>
        [Column("classid")]
        string? ClassId { get; set; }
        /// <summary>The OID of the object, as text.</summary>
        [Column("objid")]
        string? ObjId { get; set; }
        /// <summary>The sub-object id (0 for the object itself, the column number for a column).</summary>
        [Column("objsubid")]
        int? ObjSubId { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_get_wal_summarizer_state()"/>: the single
    /// state row of the WAL summarizer. The <c>pg_lsn</c> columns are text and
    /// <c>summarizer_pid</c> is NULL while the summarizer is not running.
    /// </summary>
    public interface IPgWalSummarizerStateRow
    {
        /// <summary>The timeline the summarizer is working on.</summary>
        [Column("summarized_tli")]
        long? SummarizedTli { get; set; }
        /// <summary>The LSN up to which WAL has been summarized, as text.</summary>
        [Column("summarized_lsn")]
        string? SummarizedLsn { get; set; }
        /// <summary>The LSN of the last WAL record the summarizer has seen, as text.</summary>
        [Column("pending_lsn")]
        string? PendingLsn { get; set; }
        /// <summary>The summarizer process id, or NULL when it is not running.</summary>
        [Column("summarizer_pid")]
        int? SummarizerPid { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_last_committed_xact()"/>: the most recent
    /// transaction with a commit timestamp (zero or one row). <c>roident</c> is read as text.
    /// </summary>
    public interface IPgCommittedXactRow
    {
        /// <summary>The transaction id of the last committed transaction.</summary>
        [Column("xid")]
        uint? Xid { get; set; }
        /// <summary>The commit time of that transaction.</summary>
        [Column("timestamp")]
        DateTime? Timestamp { get; set; }
        /// <summary>The replication origin of that transaction, as text.</summary>
        [Column("roident")]
        string? RoIdent { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_identify_object(string?, string?, int?)"/>: the
    /// human-readable identification of one catalog object — its type, schema, name and the identity
    /// string that the server accepts back as an object reference. An object that does not exist yields
    /// one all-NULL row.
    /// </summary>
    public interface IPgIdentifyObjectRow
    {
        /// <summary>The object type, for example <c>table</c> or <c>index</c>.</summary>
        [Column("type")]
        string? Type { get; set; }
        /// <summary>The schema the object lives in, or NULL for objects without a schema.</summary>
        [Column("schema")]
        string? Schema { get; set; }
        /// <summary>The object name, or NULL for objects without a name.</summary>
        [Column("name")]
        string? Name { get; set; }
        /// <summary>The exact textual identity the server accepts as an object reference.</summary>
        [Column("identity")]
        string? Identity { get; set; }
    }

    /// <summary>
    /// Row shape produced by
    /// <see cref="PostgresFunctions.pg_identify_object_as_address(string?, string?, int?)"/>: the
    /// machine-readable address of one catalog object. Both address components are <c>text[]</c> and keep
    /// their element order; <c>object_args</c> is empty for objects whose address needs no arguments.
    /// </summary>
    public interface IPgIdentifyObjectAddressRow
    {
        /// <summary>The object type, for example <c>table</c> or <c>column</c>.</summary>
        [Column("type")]
        string? Type { get; set; }
        /// <summary>The name components of the address (schema, relation, column, …).</summary>
        [Column("object_names")]
        string[]? ObjectNames { get; set; }
        /// <summary>The positional arguments of the address (for example the argument types of a function).</summary>
        [Column("object_args")]
        string[]? ObjectArgs { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_input_error_info(string?, string?)"/>: the
    /// soft error report of parsing a value as a type, without raising the error. A valid value yields one
    /// all-NULL row; an invalid value yields the message, the SQLSTATE code and the optional detail/hint.
    /// </summary>
    public interface IPgInputErrorInfoRow
    {
        /// <summary>The primary error message, or NULL when the value parses.</summary>
        [Column("message")]
        string? Message { get; set; }
        /// <summary>The optional error detail line.</summary>
        [Column("detail")]
        string? Detail { get; set; }
        /// <summary>The optional error hint line.</summary>
        [Column("hint")]
        string? Hint { get; set; }
        /// <summary>The SQLSTATE code of the error (for example <c>22P02</c>), or NULL when the value parses.</summary>
        [Column("sql_error_code")]
        string? SqlErrorCode { get; set; }
    }

    /// <summary>
    /// Row shape produced by
    /// <see cref="PostgresFunctions.pg_wal_summary_contents(long?, string?, string?)"/>: one block range of
    /// a WAL summary file. The <c>oid</c> columns are read as text like the other <c>oid</c>-returning
    /// members of the family and <c>relforknumber</c> is a <see cref="short"/> (<c>int2</c>).
    /// </summary>
    public interface IPgWalSummaryContentRow
    {
        /// <summary>The OID of the relation the block belongs to, as text (0 for non-relation blocks).</summary>
        [Column("relfilenode")]
        string? RelFileNode { get; set; }
        /// <summary>The OID of the relation's tablespace, as text.</summary>
        [Column("reltablespace")]
        string? RelTablespace { get; set; }
        /// <summary>The OID of the relation's database, as text.</summary>
        [Column("reldatabase")]
        string? RelDatabase { get; set; }
        /// <summary>The fork number of the relation (0 for the main fork).</summary>
        [Column("relforknumber")]
        short? RelForkNumber { get; set; }
        /// <summary>The first block number of the range.</summary>
        [Column("relblocknumber")]
        long? RelBlockNumber { get; set; }
        /// <summary>Whether this row marks the end of the block range rather than a modified block.</summary>
        [Column("is_limit_block")]
        bool? IsLimitBlock { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.pg_xact_commit_timestamp_origin(uint?)"/>: the
    /// commit timestamp of one transaction and the replication origin it belongs to (<c>roident</c> read as
    /// text). Requires <c>track_commit_timestamp</c>; a transaction without a commit timestamp yields NULL
    /// columns.
    /// </summary>
    public interface IPgXactCommitOriginRow
    {
        /// <summary>The commit time of the transaction.</summary>
        [Column("timestamp")]
        DateTime? Timestamp { get; set; }
        /// <summary>The replication origin of the transaction, as text.</summary>
        [Column("roident")]
        string? RoIdent { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqlServerFunctions.string_split(string?, string?)"/>: a single column
    /// named <c>value</c> holding one fragment (SQL Server <c>string_split</c>).
    /// </summary>
    public interface IStringSplitRow
    {
        /// <summary>One fragment produced by the split.</summary>
        [Column("value")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqlServerFunctions.regexp_split_to_table(string?, string?)"/>:
    /// SQL Server 2025 returns the fragment as <c>value varchar(n)</c> and its one-based position as
    /// <c>ordinal bigint</c>.
    /// </summary>
    public interface ISqlServerRegexpSplitToTableRow
    {
        /// <summary>One fragment produced by the split.</summary>
        [Column("value")]
        string? Value { get; set; }

        /// <summary>The one-based position of the fragment in the input.</summary>
        [Column("ordinal")]
        long Ordinal { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqlServerFunctions.regexp_matches(string?, string?)"/>: SQL Server
    /// 2025 returns one row per match with <c>match_id bigint</c>, the one-based <c>start_position int</c>
    /// and <c>end_position int</c>, the matched text as <c>match_value varchar(n)</c> and the capture
    /// groups as the engine's JSON text in <c>substring_matches varchar(max)</c>. The
    /// <c>substring_matches</c> column is carried as an opaque string: its length is not fixed by the
    /// input, so it is never cast or truncated by the provider.
    /// </summary>
    public interface ISqlServerRegexpMatchRow
    {
        /// <summary>The one-based match number.</summary>
        [Column("match_id")]
        long MatchId { get; set; }

        /// <summary>The one-based position of the first character of the match.</summary>
        [Column("start_position")]
        int StartPosition { get; set; }

        /// <summary>The one-based position of the last character of the match.</summary>
        [Column("end_position")]
        int EndPosition { get; set; }

        /// <summary>The matched text.</summary>
        [Column("match_value")]
        string? MatchValue { get; set; }

        /// <summary>The capture groups as the engine's JSON text (<c>varchar(max)</c>), never truncated.</summary>
        [Column("substring_matches")]
        string? SubstringMatches { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqlServerFunctions.xml_nodes(string?, string?)"/>: the single
    /// <c>value</c> column holds one XML node selected by the XQuery (an <c>xml</c> value that is
    /// projected further with <c>xml_value</c>/<c>xml_query</c>/<c>xml_exist</c>).
    /// </summary>
    public interface IXmlNodesRow
    {
        /// <summary>One XML node selected by the XQuery, projected further with <c>xml_value</c>/<c>xml_query</c>/<c>xml_exist</c>.</summary>
        [Column("value")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqlServerFunctions.openjson(string?)"/>: the <c>key</c>/<c>value</c>/<c>type</c>
    /// columns of SQL Server's <c>openjson</c> (the default schema, i.e. object properties or array elements).
    /// </summary>
    public interface IOpenJsonRow
    {
        // KEY is a reserved word in T-SQL, so the physical name is quoted here. The openjson helper
        // is gated to SQL Server (see ISqlDialect.SupportsTableFunction), so the quoting is safe.
        /// <summary>The property name or array index.</summary>
        [Column("[key]")]
        string? Key { get; set; }
        /// <summary>The property or element value.</summary>
        [Column("value")]
        string? Value { get; set; }
        /// <summary>The JSON value type reported by <c>openjson</c>.</summary>
        [Column("type")]
        int Type { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqlServerFunctions.containstable{TKey}(string?, string?, string?)"/>
    /// and <see cref="SqlServerFunctions.freetexttable{TKey}(string?, string?, string?)"/>: the SQL Server
    /// full-text <c>KEY</c> (the full-text key of the matched row, used to join back to the base table)
    /// and <c>RANK</c> (the relevance score) columns.
    /// </summary>
    public interface IKeyRankRow<TKey>
    {
        /// <summary>The full-text key of the matched row, used to join back to the base table.</summary>
        [Column("[key]")]
        TKey Key { get; set; }
        /// <summary>The relevance score of the match.</summary>
        [Column("[rank]")]
        int Rank { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="ClickHouseFunctions.numbers(long)"/> and
    /// <see cref="ClickHouseFunctions.numbers_mt(long)"/>: a single column named <c>number</c> holding the
    /// generated value.
    /// </summary>
    public interface INumbersRow
    {
        /// <summary>The generated number.</summary>
        [Column("number")]
        long Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="ClickHouseFunctions.zeros(long)"/> and
    /// <see cref="ClickHouseFunctions.zeros_mt(long)"/>: a single column named <c>zero</c> holding the
    /// byte <c>0</c> (ClickHouse <c>UInt8</c>).
    /// </summary>
    public interface IZerosRow
    {
        /// <summary>The constant byte <c>0</c> (ClickHouse <c>UInt8</c>).</summary>
        [Column("zero")]
        byte Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="ClickHouseFunctions.generate_random()"/> and
    /// <see cref="ClickHouseFunctions.generate_random(long)"/>: the fixed structure
    /// <c>id UInt64, value Float64, name String</c> of ClickHouse's <c>generateRandom</c>.
    /// </summary>
    public interface IGenerateRandomRow
    {
        /// <summary>The generated <c>UInt64</c> identifier.</summary>
        [Column("id")]
        long Id { get; set; }
        /// <summary>The generated <c>Float64</c> value.</summary>
        [Column("value")]
        double Value { get; set; }
        /// <summary>The generated string.</summary>
        [Column("name")]
        string? Name { get; set; }
    }

}

/// <summary>
    /// Surface of SQL functions and predicates that can be used inside query expressions. The SQL
    /// providers evaluate the members through the expression translator; the in-memory provider
    /// rewrites the small subset that has a native CLR equivalent.
    /// </summary>
    /// <remarks>
    /// Renamed from <c>NORM_SQL</c>. The <see cref="System.Reflection.MethodInfo"/> fields and
    /// <c>SQLExpression</c> are implementation details and are now <c>internal</c>.
    /// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-10.
    /// </remarks>
    public class CommonFunctions
        // Common (cross-provider) surface.
    {
        [Browsable(false)]
        internal static readonly MethodInfo ExistsMI = typeof(CommonFunctions).GetMethod(nameof(exists), BindingFlags.Public | BindingFlags.Instance)!;

        [Browsable(false)]
        internal static readonly MethodInfo MinMI = AggregateMethod(nameof(min));

        [Browsable(false)]
        internal static readonly MethodInfo MaxMI = AggregateMethod(nameof(max));

        /// <summary>
        /// Canonical <c>avg</c> translation marker. The AVG family is declared per numeric input
        /// type (see <see cref="AverageMethodFor"/>), so the marker is a concrete declaration instead
        /// of a generic definition; call sites select the real overload from the selector type.
        /// </summary>
        [Browsable(false)]
        internal static readonly MethodInfo AvgMI = typeof(CommonFunctions).GetMethod(nameof(avg), [typeof(double?)])!;

        [Browsable(false)]
        internal static readonly MethodInfo SumMI = AggregateMethod(nameof(sum));

        [Browsable(false)]
        internal static readonly MethodInfo StdevMI = AggregateMethod(nameof(stdev));

        [Browsable(false)]
        internal static readonly MethodInfo StdevpMI = AggregateMethod(nameof(stdevp));

        [Browsable(false)]
        internal static readonly MethodInfo VarMI = AggregateMethod(nameof(var));

        [Browsable(false)]
        internal static readonly MethodInfo VarpMI = AggregateMethod(nameof(varp));

        [Browsable(false)]
        internal static readonly ConstantExpression SQLExpression = Expression.Constant(new CommonFunctions());

        /// <summary>
        /// Resolves the single-value overload of an aggregate method. Several aggregates
        /// (<c>min</c>, <c>max</c>, <c>avg</c>, <c>sum</c>) also have an <c>(value, filter)</c>
        /// overload, so a name-only <c>GetMethod</c> lookup is ambiguous; binding by parameter count
        /// selects the plain aggregate.
        /// </summary>
        private static MethodInfo AggregateMethod(string name)
        {
            var candidates = typeof(CommonFunctions).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == name && m.GetParameters().Length == 1)
                .ToList();

            // The variability aggregates declare two single-value overloads: the value type and its
            // Nullable<T> form. The translation marker keeps the nullable-input declaration, which
            // accepts every selector shape; Min/Max/Avg/Sum still have exactly one.
            return candidates.Count == 1
                ? candidates[0]
                : candidates.Single(m => Nullable.GetUnderlyingType(m.GetParameters()[0].ParameterType) is not null);
        }

        /// <summary>
        /// Resolves the concrete numeric <c>avg</c> declaration for the selector's input type. AVG is
        /// declared per numeric input type (the result type is fixed by the input type), so the
        /// terminal and the terminal rewriter select the overload by parameter type instead of
        /// closing a generic definition.
        /// </summary>
        /// <param name="inputType">The selector's value type, its <see cref="Nullable{T}"/> form included.</param>
        /// <returns>The matching <c>avg</c> declaration.</returns>
        internal static MethodInfo AverageMethodFor(Type inputType)
            => typeof(CommonFunctions).GetMethod(nameof(avg), [inputType])
                ?? throw new NotSupportedException($"The AVG aggregate has no numeric overload for '{inputType}'.");

        /// <summary>
        /// <c>exists(subquery)</c>: true when the correlated subquery returns at least one row. ANSI
        /// and portable across every provider.
        /// </summary>
        /// <param name="cmd">The correlated subquery.</param>
        public bool exists(QueryCommand cmd) => default!;

        /// <summary>
        /// <c>column like pattern</c>: SQL pattern matching where <c>%</c> and <c>_</c> are wildcards.
        /// ANSI and portable, but case-sensitivity follows the provider's collation.
        /// </summary>
        /// <param name="column">The value to test.</param>
        /// <param name="pattern">The pattern to match against.</param>
        public bool like(string? column, string? pattern) => default!;

        /// <summary>
        /// <c>column like pattern escape escapeChar</c>: SQL pattern matching with an explicit escape
        /// character for the wildcards. ANSI and portable.
        /// </summary>
        /// <param name="column">The value to test.</param>
        /// <param name="pattern">The pattern to match against.</param>
        /// <param name="escapeChar">The character that escapes a literal <c>%</c> or <c>_</c>.</param>
        public bool like(string? column, string? pattern, string? escapeChar) => default!;

        /// <summary>
        /// <c>value collate collation</c>: applies a provider-native collation to a string operand,
        /// which determines how it compares and orders. <paramref name="collation"/> must be a constant
        /// string naming a collation the provider understands (for example <c>"C"</c> on PostgreSQL,
        /// <c>"Latin1_General_100_BIN2"</c> on SQL Server). Requires a provider that supports
        /// per-expression collation (see <see cref="ISqlDialect.SupportsCollation"/>); ClickHouse has no
        /// <c>COLLATE</c> clause.
        /// </summary>
        /// <param name="value">The string operand to collate.</param>
        /// <param name="collation">The provider-native collation name (a constant).</param>
        /// <remarks>
        /// The in-memory provider executes the call natively and returns the value unchanged, because
        /// its comparisons are already ordinal (the <c>collation</c> argument is ignored).
        /// </remarks>
        public string? collate(string? value, string? collation) => value;

        /// <summary>
        /// <c>contains(column, search)</c>: true when the full-text-indexed <paramref name="column"/>
        /// contains <paramref name="search"/>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsFullText"/>).
        /// </summary>
        public bool contains<T>(T? column, string? search) => default!;

        /// <summary>
        /// <c>freetext(column, search)</c>: true when the full-text-indexed <paramref name="column"/>
        /// matches the meaning (not just the words) of <paramref name="search"/>. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SupportsFullText"/>).
        /// </summary>
        public bool freetext<T>(T? column, string? search) => default!;

        /// <summary>
        /// The name of the current user (<c>current_user</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SessionInfoFunctions"/> and
        /// <see cref="ISessionInfoFunctions.Supports"/>).
        /// </summary>
        public string? current_user() => default!;

        /// <summary>
        /// The name of the session user (<c>session_user</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SessionInfoFunctions"/> and
        /// <see cref="ISessionInfoFunctions.Supports"/>).
        /// </summary>
        public string? session_user() => default!;

        /// <summary>
        /// The current schema (<c>current_schema</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SessionInfoFunctions"/> and
        /// <see cref="ISessionInfoFunctions.Supports"/>).
        /// </summary>
        public string? current_schema() => default!;

        /// <summary>
        /// The current database name (<c>current_database()</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SessionInfoFunctions"/> and
        /// <see cref="ISessionInfoFunctions.Supports"/>).
        /// </summary>
        public string? current_database() => default!;

        /// <summary>
        /// The provider version string (<c>version()</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SessionInfoFunctions"/> and
        /// <see cref="ISessionInfoFunctions.Supports"/>).
        /// </summary>
        public string? version() => default!;

        /// <summary>
        /// The server-side backend process id (<c>pg_backend_pid()</c>). Requires a provider that supports
        /// it (see <see cref="ISqlDialect.SessionInfoFunctions"/> and
        /// <see cref="ISessionInfoFunctions.Supports"/>; PostgreSQL).
        /// </summary>
        public int pg_backend_pid() => default!;

        /// <summary>
        /// The IP address of the server the client is connected to, rendered as text
        /// (<c>CAST(inet_server_addr() AS text)</c>). The value is SQL NULL on a unix-socket connection,
        /// so the result is nullable. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SessionInfoFunctions"/>; PostgreSQL).
        /// </summary>
        public string? inet_server_addr() => default!;

        /// <summary>
        /// The TCP port of the server the client is connected to (<c>inet_server_port()</c>). The value is
        /// SQL NULL on a unix-socket connection. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SessionInfoFunctions"/>; PostgreSQL).
        /// </summary>
        public int? inet_server_port() => default!;

        /// <summary>
        /// The IP address of the client, rendered as text (<c>CAST(inet_client_addr() AS text)</c>). The
        /// value is SQL NULL on a unix-socket connection, so the result is nullable. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SessionInfoFunctions"/>; PostgreSQL).
        /// </summary>
        public string? inet_client_addr() => default!;

        /// <summary>
        /// The TCP port the client is connected from (<c>inet_client_port()</c>). The value is SQL NULL on a
        /// unix-socket connection. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SessionInfoFunctions"/>; PostgreSQL).
        /// </summary>
        public int? inet_client_port() => default!;

        /// <summary>
        /// The server-side connection id as an unsigned 64-bit value (MySQL/MariaDB
        /// <c>CAST(CONNECTION_ID() AS UNSIGNED)</c>, ClickHouse <c>CAST(connectionId() AS UInt64)</c>).
        /// It identifies the connection, not the user. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SessionInfoFunctions"/>).
        /// </summary>
        public ulong connection_id() => default!;

        /// <summary>
        /// The connecting account as the server reports it (<c>USER()</c>), surfaced by MySQL/MariaDB: the
        /// <c>user@host</c> pair of the client, which is not the effective authenticated account
        /// (<see cref="current_user"/>) and is client-influenced. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SessionInfoFunctions"/>; MySQL/MariaDB).
        /// </summary>
        public string user() => default!;

        /// <summary>
        /// Generates a random UUID v4 (<c>gen_random_uuid()</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.UuidGenerators"/> and
        /// <see cref="IUuidGenerators.Supports"/>): PostgreSQL 13+, SQL Server
        /// (<c>newid()</c>), MariaDB 11.7+ (<c>uuid_v4()</c>) and ClickHouse
        /// (<c>generateUUIDv4()</c>). An unversioned MariaDB dialect rejects it: MariaDB 11.4 has no
        /// <c>UUID_V4()</c> and <c>UUID()</c> (v1) is not substituted.
        /// </summary>
        public Guid? gen_random_uuid() => default!;

        /// <summary>
        /// Generates a UUID v7 (<c>uuidv7()</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.UuidGenerators"/> and
        /// <see cref="IUuidGenerators.Supports"/>): PostgreSQL 18+, MariaDB 11.7+
        /// (<c>uuid_v7()</c>) and ClickHouse (<c>generateUUIDv7()</c>). PostgreSQL renders it only
        /// when the dialect is configured with an explicit server version 18 or later; a version
        /// below 18 and an unset version refuse at preparation with <see cref="NotSupportedException"/>,
        /// and no v4 generator or <see langword="null"/> is substituted. An unversioned MariaDB
        /// dialect rejects it.
        /// </summary>
        public Guid? uuidv7() => default!;

        /// <summary>
        /// <c>column in (subquery)</c>: true when <paramref name="column"/> equals a value returned by
        /// the correlated subquery.
        /// </summary>
        /// <typeparam name="T">The compared value type.</typeparam>
        /// <param name="column">The value to test.</param>
        /// <param name="cmd">The subquery producing candidate values.</param>
        public bool @in<T>(T column, QueryCommand<T> cmd) => default!;

        /// <summary><c>column in (values)</c>: true when <paramref name="column"/> equals one of the in-memory values.</summary>
        /// <typeparam name="T">The compared value type.</typeparam>
        /// <param name="column">The value to test.</param>
        /// <param name="values">The candidate values.</param>
        public bool @in<T>(T column, IEnumerable<T> values) => default!;

        /// <summary><c>column in (v0, v1, ...)</c>: true when <paramref name="column"/> equals one of the listed values.</summary>
        /// <typeparam name="T">The compared value type.</typeparam>
        /// <param name="column">The value to test.</param>
        /// <param name="values">The candidate values.</param>
        public bool @in<T>(T column, params T[] values) => default!;

        /// <summary>
        /// <c>any(subquery)</c>: the <c>ANY</c> subquery quantifier, true when any row of the subquery
        /// satisfies the comparison. Distinct from <c>any_agg</c>, the arbitrary-value aggregate.
        /// </summary>
        /// <typeparam name="T">The subquery result type.</typeparam>
        /// <param name="cmd">The subquery.</param>
        public T any<T>(QueryCommand<T> cmd) => default!;

        /// <summary>
        /// <c>all(subquery)</c>: the <c>ALL</c> subquery quantifier, true when every row of the
        /// subquery satisfies the comparison.
        /// </summary>
        /// <typeparam name="T">The subquery result type.</typeparam>
        /// <param name="cmd">The subquery.</param>
        public T all<T>(QueryCommand<T> cmd) => default!;

        /// <summary><c>nullif(value, other)</c>: null when the two arguments are equal. ANSI and portable.</summary>
        public T? nullif<T>(T? value, T? other) => default!;

        /// <summary>
        /// <c>iif(condition, whenTrue, whenFalse)</c>: the conditional expression, rendered as <c>iif</c>
        /// (SQL Server, SQLite), <c>if</c> (MySQL/MariaDB, ClickHouse) or <c>case when ... then ... else ... end</c>
        /// (PostgreSQL). Requires a provider that supports it (see <see cref="ISqlDialect.Iif"/>).
        /// </summary>
        public TResult? iif<TResult>(bool condition, TResult? whenTrue, TResult? whenFalse) => default!;

        /// <summary>
        /// <c>greatest(...)</c>: the largest of the arguments. NULL handling is provider-specific:
        /// PostgreSQL, SQL Server 2022+ and ClickHouse 24.12+ ignore NULL arguments, while MySQL/MariaDB
        /// and SQLite return NULL when any argument is NULL.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SupportsGreatestLeast"/>).
        /// </summary>
        public T? greatest<T>(params T?[] values) => default!;

        /// <summary>
        /// <c>least(...)</c>: the smallest of the arguments. NULL handling is provider-specific:
        /// PostgreSQL, SQL Server 2022+ and ClickHouse 24.12+ ignore NULL arguments, while MySQL/MariaDB
        /// and SQLite return NULL when any argument is NULL.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SupportsGreatestLeast"/>).
        /// </summary>
        public T? least<T>(params T?[] values) => default!;

        /// <summary>
        /// <c>left(value, n)</c>: the first <paramref name="n"/> characters of <paramref name="value"/>.
        /// PostgreSQL treats a negative <paramref name="n"/> as "all but the last |n| characters"; the
        /// other providers do not, so use a non-negative <paramref name="n"/> for portable queries.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? left(string? value, int n) => default!;

        /// <summary>
        /// <c>right(value, n)</c>: the last <paramref name="n"/> characters of <paramref name="value"/>.
        /// PostgreSQL treats a negative <paramref name="n"/> as "all but the first |n| characters"; the
        /// other providers do not, so use a non-negative <paramref name="n"/> for portable queries.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? right(string? value, int n) => default!;

        /// <summary>
        /// <c>lpad(value, length, pad)</c>: left-pads <paramref name="value"/> with
        /// <paramref name="pad"/> (default a single space) to <paramref name="length"/> characters,
        /// truncating a longer value. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.ScalarFunctions"/> and <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? lpad(string? value, int length, string? pad = " ") => default!;

        /// <summary>
        /// <c>rpad(value, length, pad)</c>: right-pads <paramref name="value"/> with
        /// <paramref name="pad"/> (default a single space) to <paramref name="length"/> characters,
        /// truncating a longer value. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.ScalarFunctions"/> and <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? rpad(string? value, int length, string? pad = " ") => default!;

        /// <summary>
        /// <c>repeat(value, count)</c>: <paramref name="value"/> repeated <paramref name="count"/>
        /// times. Requires a provider that supports it (see <see cref="ISqlDialect.ScalarFunctions"/>
        /// and <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? repeat(string? value, int count) => default!;

        /// <summary>
        /// <c>reverse(value)</c>: the characters of <paramref name="value"/> in reverse order. Requires
        /// a provider that supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? reverse(string? value) => default!;

        /// <summary>
        /// <c>space(count)</c>: a string of <paramref name="count"/> spaces. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? space(int count) => default!;

        /// <summary>
        /// <c>concat_ws(separator, ...)</c>: joins the values with <paramref name="separator"/>. Null
        /// arguments are skipped on PostgreSQL, SQL Server, MySQL/MariaDB and SQLite; ClickHouse's
        /// <c>concatWithSeparator</c> returns NULL when any argument is NULL.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? concat_ws(string? separator, params object?[] values) => default!;

        /// <summary>
        /// <c>translate(value, from, to)</c>: replaces every character of <paramref name="from"/> in
        /// <paramref name="value"/> with the character at the same position of <paramref name="to"/>;
        /// a character of <paramref name="from"/> beyond the length of <paramref name="to"/> is
        /// deleted. Requires a provider that supports it (see <see cref="ISqlDialect.ScalarFunctions"/>
        /// and <see cref="IScalarFunctions.Supports"/>); MySQL and MariaDB have no
        /// <c>translate</c> and reject it.
        /// </summary>
        public string? translate(string? value, string? from, string? to) => default!;

        /// <summary>
        /// <c>ascii(value)</c>: the code point of the first character of <paramref name="value"/> (0 for
        /// an empty string). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.ScalarFunctions"/> and <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public int? ascii(string? value) => default!;

        /// <summary>
        /// <c>char(code)</c>: the character with the given code point (the SQL <c>char</c>/<c>chr</c>,
        /// declared as <c>@char</c> because <c>char</c> is a C# keyword). Requires a provider that
        /// supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public string? @char(int code) => default!;

        /// <summary>
        /// <c>bit_length(value)</c>: the number of bits in the string's octets (8 ×
        /// <see cref="octet_length"/>). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.ScalarFunctions"/> and <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public int? bit_length(string? value) => default!;

        /// <summary>
        /// <c>octet_length(value)</c>: the number of octets (bytes) in the encoded value. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>); SQLite requires 3.43+ for its native
        /// <c>octet_length()</c>.
        /// </summary>
        public int? octet_length(string? value) => default!;

        /// <summary>
        /// <c>base64_encode(value)</c>: the standard base64 (RFC 4648) text of
        /// <paramref name="value"/>, with <c>+</c>/<c>/</c> and <c>=</c> padding and no line
        /// wrapping. SQL NULL (including a NULL argument) yields SQL NULL. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SupportsBase64Encode"/> and
        /// <see cref="ISqlDialect.MakeBase64Encode(string)"/>); SQL Server, PostgreSQL, MySQL and
        /// MariaDB render it natively, and every other provider rejects the call.
        /// </summary>
        public string? base64_encode(byte[]? value) =>
            throw new NotSupportedException("The base64_encode function can only be used inside a query expression.");

        /// <summary>
        /// <c>base64_decode(value)</c>: the binary value of the standard base64 (RFC 4648) text
        /// <paramref name="value"/>. SQL NULL (including a NULL argument) yields SQL NULL. The
        /// input domain is canonical base64; invalid or URL-safe input is not normalized and the
        /// provider error is not masked. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsBase64Decode"/> and
        /// <see cref="ISqlDialect.MakeBase64Decode(string)"/>).
        /// </summary>
        public byte[]? base64_decode(string? value) =>
            throw new NotSupportedException("The base64_decode function can only be used inside a query expression.");

        /// <summary>
        /// <c>crypt_gen_random(length)</c>: a cryptographically strong random byte string of
        /// <paramref name="length"/> bytes. The portable domain is 1–1024 bytes; a constant length
        /// outside it is rejected before any SQL is emitted
        /// (<see cref="ArgumentOutOfRangeException"/>). A NULL argument yields SQL NULL. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsCryptGenRandom"/> and
        /// <see cref="ISqlDialect.MakeCryptGenRandom(string)"/>); PostgreSQL additionally requires
        /// the <c>pgcrypto</c> extension to be installed on the server.
        /// </summary>
        public byte[]? crypt_gen_random(int? length) =>
            throw new NotSupportedException("The crypt_gen_random function can only be used inside a query expression.");

        /// <summary>
        /// <c>cot(x)</c>: the cotangent of <paramref name="value"/> (in radians). PostgreSQL, SQL Server
        /// and MySQL/MariaDB have a native <c>cot</c>; ClickHouse and SQLite do not, and reject the call.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public double? cot(double? value) => default!;

        /// <summary>
        /// <c>degrees(x)</c>: converts <paramref name="value"/> from radians to degrees. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public double? degrees(double? value) => default!;

        /// <summary>
        /// <c>radians(x)</c>: converts <paramref name="value"/> from degrees to radians. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.ScalarFunctions"/> and
        /// <see cref="IScalarFunctions.Supports"/>).
        /// </summary>
        public double? radians(double? value) => default!;

        /// <summary>
        /// <c>pi()</c>: the constant π. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.ScalarFunctions"/> and <see cref="IScalarFunctions.Supports"/>); the
        /// trigonometric <c>Math.Acos</c>/<c>Asin</c>/<c>Atan</c>/<c>Atan2</c> are reachable directly.
        /// </summary>
        public double? pi() => default!;

        /// <summary>
        /// <c>date_trunc(field, value)</c>: truncates a timestamp to <paramref name="field"/>
        /// (for example <c>"month"</c>). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsDateTrunc"/>).
        /// </summary>
        public DateTime? date_trunc(string field, DateTime? value) => default!;

        /// <summary>
        /// <c>dateadd(field, amount, value)</c>: adds <paramref name="amount"/> units of
        /// <paramref name="field"/> (for example <c>"day"</c>) to a date/time. The field must be a
        /// constant string. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public DateTime? date_add(string field, int amount, DateTime? value) => default!;

        /// <summary>
        /// <c>datediff(field, start, end)</c>: the number of <paramref name="field"/> boundaries
        /// between two timestamps (SQL Server semantics; a provider without a native <c>datediff</c>
        /// may count whole units instead, which can differ). The field must be a constant string and
        /// cannot be <c>decade</c>/<c>century</c>/<c>millennium</c>, which T-SQL <c>datediff</c> has no
        /// part for. The result is 32-bit, so the span must fit <see cref="int"/>; use
        /// <see cref="date_diff_big"/> for wider spans. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public int? date_diff(string field, DateTime? start, DateTime? end) => default!;

        /// <summary>
        /// <c>datediff_big(field, start, end)</c>: like <see cref="date_diff"/>, but the difference is
        /// returned as a 64-bit value so a <c>millisecond</c>/<c>microsecond</c> span over a long range
        /// does not overflow the 32-bit <c>date_diff</c>. The field must be a constant string. Requires
        /// a provider that supports it (see <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public long? date_diff_big(string field, DateTime? start, DateTime? end) => default!;

        /// <summary>
        /// <c>eomonth(value)</c>: the last day of the month of <paramref name="value"/>. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public DateTime? end_of_month(DateTime? value) => default!;

        /// <summary>
        /// <c>datefromparts(year, month, day)</c>: builds a date from its numeric parts. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public DateTime? date_from_parts(int year, int month, int day) => default!;

        /// <summary>
        /// <c>extract(part, value)</c>: the integer <paramref name="part"/> of a date/time. Supports
        /// <c>year</c>, <c>quarter</c>, <c>month</c>, <c>week</c> (ISO 8601), <c>day</c>, <c>doy</c>,
        /// <c>dow</c> (0=Sunday..6=Saturday), <c>isodow</c> (1=Monday..7=Sunday), <c>hour</c>,
        /// <c>minute</c> and <c>second</c>. The part must be a constant string. Use
        /// <see cref="date_part"/> for <c>epoch</c>. Requires a provider that supports the part (see
        /// <see cref="ISqlDialect.SupportsDatePart(string)"/>).
        /// </summary>
        public int? extract(string part, DateTime? value) => default!;

        /// <summary>
        /// <c>date_part(part, value)</c>: the numeric <paramref name="part"/> of a date/time.
        /// Currently supports <c>epoch</c> (the number of seconds since 1970-01-01 00:00:00). Providers
        /// whose native timestamp type carries sub-second precision include the fraction; ClickHouse's
        /// <c>DateTime</c> is second-precision, so there the value is a whole number of seconds. The
        /// part must be a constant string. Use <see cref="extract"/> for the
        /// integer parts. Requires a provider that supports the part (see
        /// <see cref="ISqlDialect.SupportsDatePart(string)"/>).
        /// </summary>
        /// <remarks>
        /// This is not PostgreSQL's general <c>date_part</c> synonym: only the numeric-valued part
        /// <c>epoch</c> is provided here, while the integer parts (including PostgreSQL's
        /// <c>date_part('year', x)</c>) go through <see cref="extract"/>.
        /// </remarks>
        public double? date_part(string part, DateTime? value) => default!;

        /// <summary>
        /// <c>string_agg(value, delimiter)</c>: concatenates the values of a group. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SupportsStringArrayAggregates"/>).
        /// </summary>
        public string? string_agg<T>(T? value, string delimiter) => default!;

        /// <summary>Filtered <c>string_agg(value, delimiter) filter (where ...)</c>.</summary>
        public string? string_agg<T>(T? value, string delimiter, Expression<Func<bool>> filter) => default!;

        /// <summary>
        /// <c>count</c> aggregate. With no arguments it is <c>count(*)</c>; with arguments it counts
        /// the rows whose listed expression(s) are non-null. Only SQLite renders a bare <c>count()</c>;
        /// other providers require an argument or the filtered overload.
        /// </summary>
        /// <param name="properties">The expressions to count, or an empty array for <c>count(*)</c>.</param>
        public int count(params object?[] properties) => default!;

        /// <summary>64-bit <c>count</c> (<c>count_big</c> on SQL Server).</summary>
        /// <param name="properties">The expressions to count, or an empty array for <c>count(*)</c>.</param>
        public long count_big(params object?[] properties) => default!;

        /// <summary><c>count(distinct ...)</c>: the number of distinct non-null values of the listed expression(s).</summary>
        /// <param name="properties">The expressions whose distinct values are counted.</param>
        public int count_distinct(params object?[] properties) => default!;

        /// <summary>64-bit <c>count(distinct ...)</c> (<c>count_big(distinct ...)</c> on SQL Server).</summary>
        /// <param name="properties">The expressions whose distinct values are counted.</param>
        public long count_big_distinct(params object?[] properties) => default!;

        /// <summary><c>min</c> aggregate: the smallest non-null value of <paramref name="property"/>.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? min<T>(T? property) => default!;

        /// <summary><c>max</c> aggregate: the largest non-null value of <paramref name="property"/>.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? max<T>(T? property) => default!;

        /// <summary><c>avg</c> aggregate over <c>byte</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(byte property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>byte</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(byte? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>sbyte</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(sbyte property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>sbyte</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(sbyte? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>short</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(short property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>short</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(short? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>ushort</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(ushort property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>ushort</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(ushort? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>int</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(int property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>int</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(int? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>uint</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(uint property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>uint</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(uint? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>long</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(long property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>long</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(long? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>ulong</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(ulong property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>ulong</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(ulong? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>float</c> values; the mean is the fractional <see cref="float"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public float? avg(float property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>float</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="float"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public float? avg(float? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>double</c> values; the mean is the fractional <see cref="double"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(double property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>double</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="double"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg(double? property) => default!;
        /// <summary><c>avg</c> aggregate over <c>decimal</c> values; the mean is the fractional <see cref="decimal"/> value of the non-null rows.</summary>
        /// <param name="property">The value to aggregate.</param>
        public decimal? avg(decimal property) => default!;

        /// <summary><c>avg</c> aggregate over nullable <c>decimal</c> values; <see langword="null"/> rows are ignored and the mean is the fractional <see cref="decimal"/> value.</summary>
        /// <param name="property">The value to aggregate.</param>
        public decimal? avg(decimal? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>byte</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(byte property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>byte</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(byte? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>sbyte</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(sbyte property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>sbyte</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(sbyte? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>short</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(short property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>short</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(short? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>ushort</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(ushort property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>ushort</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(ushort? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>int</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(int property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>int</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(int? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>uint</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(uint property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>uint</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(uint? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>long</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(long property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>long</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(long? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>ulong</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(ulong property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>ulong</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(ulong? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>float</c> values: the mean of the distinct non-null rows as a fractional <see cref="float"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public float? avg_distinct(float property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>float</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="float"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public float? avg_distinct(float? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>double</c> values: the mean of the distinct non-null rows as a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(double property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>double</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public double? avg_distinct(double? property) => default!;

        /// <summary><c>avg(distinct ...)</c> over <c>decimal</c> values: the mean of the distinct non-null rows as a fractional <see cref="decimal"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public decimal? avg_distinct(decimal property) => default!;

        /// <summary><c>avg(distinct ...)</c> over nullable <c>decimal</c> values: <see langword="null"/> rows are ignored and the mean is a fractional <see cref="decimal"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public decimal? avg_distinct(decimal? property) => default!;

        /// <summary><c>sum</c> aggregate: the sum of the non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? sum<T>(T? property) => default!;

        /// <summary><c>sum(distinct ...)</c>: the sum of the distinct non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? sum_distinct<T>(T? property) => default!;

        /// <summary><c>stdev</c> aggregate: the sample standard deviation of the non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? stdev<T>(T property) where T : struct => default!;

        /// <summary><c>stdev</c> aggregate: the sample standard deviation of the non-null values; a nullable input is accepted and <see langword="null"/> values are ignored.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? stdev<T>(T? property) where T : struct => default!;

        /// <summary><c>stdev(distinct ...)</c> aggregate: the sample standard deviation of the distinct non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? stdev_distinct<T>(T property) where T : struct => default!;

        /// <summary><c>stdev(distinct ...)</c> aggregate: the sample standard deviation of the distinct non-null values; a nullable input is accepted and <see langword="null"/> values are ignored.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? stdev_distinct<T>(T? property) where T : struct => default!;

        /// <summary><c>stdevp</c> aggregate: the population standard deviation of the non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? stdevp<T>(T property) where T : struct => default!;

        /// <summary><c>stdevp</c> aggregate: the population standard deviation of the non-null values; a nullable input is accepted and <see langword="null"/> values are ignored.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? stdevp<T>(T? property) where T : struct => default!;

        /// <summary><c>stdevp(distinct ...)</c> aggregate: the population standard deviation of the distinct non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? stdevp_distinct<T>(T property) where T : struct => default!;

        /// <summary><c>stdevp(distinct ...)</c> aggregate: the population standard deviation of the distinct non-null values; a nullable input is accepted and <see langword="null"/> values are ignored.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? stdevp_distinct<T>(T? property) where T : struct => default!;

        /// <summary><c>var</c> aggregate: the sample variance of the non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? var<T>(T property) where T : struct => default!;

        /// <summary><c>var</c> aggregate: the sample variance of the non-null values; a nullable input is accepted and <see langword="null"/> values are ignored.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? var<T>(T? property) where T : struct => default!;

        /// <summary><c>var(distinct ...)</c> aggregate: the sample variance of the distinct non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? var_distinct<T>(T property) where T : struct => default!;

        /// <summary><c>var(distinct ...)</c> aggregate: the sample variance of the distinct non-null values; a nullable input is accepted and <see langword="null"/> values are ignored.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? var_distinct<T>(T? property) where T : struct => default!;

        /// <summary><c>varp</c> aggregate: the population variance of the non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? varp<T>(T property) where T : struct => default!;

        /// <summary><c>varp</c> aggregate: the population variance of the non-null values; a nullable input is accepted and <see langword="null"/> values are ignored.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? varp<T>(T? property) where T : struct => default!;

        /// <summary><c>varp(distinct ...)</c> aggregate: the population variance of the distinct non-null values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? varp_distinct<T>(T property) where T : struct => default!;

        /// <summary><c>varp(distinct ...)</c> aggregate: the population variance of the distinct non-null values; a nullable input is accepted and <see langword="null"/> values are ignored.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="property">The value to aggregate.</param>
        public T? varp_distinct<T>(T? property) where T : struct => default!;

        /// <summary>
        /// <c>any_value(property)</c>: an arbitrary value from the group. Distinct from the
        /// <see cref="any{T}(QueryCommand{T})"/> subquery quantifier, hence the name. Rendered as
        /// <c>ANY_VALUE(x)</c> by MySQL and as <c>any(x)</c> by ClickHouse; MariaDB has no
        /// <c>ANY_VALUE</c> yet, so it is gated off. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsAnyValueAggregate"/>).
        /// </summary>
        public T? any_agg<T>(T? property) => default!;

        /// <summary>
        /// Filtered <c>count(*)</c>. Requires a provider that can filter an aggregate (see
        /// <see cref="ISqlDialect.AggregateFilterStyle"/>): the ANSI <c>filter (where ...)</c> clause or,
        /// on ClickHouse, the <c>-If</c> combinator (<c>countIf(...)</c>). PostgreSQL, SQLite and
        /// ClickHouse opt in.
        /// </summary>
        public int count(Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered 64-bit <c>count(*)</c> (<c>count_big ... filter (where ...)</c>, or the ClickHouse <c>countIf</c>).</summary>
        public long count_big(Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>min(property)</c> (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>minIf</c>).</summary>
        public T? min<T>(T? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>max(property)</c> (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>maxIf</c>).</summary>
        public T? max<T>(T? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>byte</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(byte property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>byte</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(byte? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>sbyte</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(sbyte property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>sbyte</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(sbyte? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>short</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(short property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>short</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(short? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>ushort</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(ushort property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>ushort</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(ushort? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>int</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(int property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>int</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(int? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>uint</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(uint property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>uint</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(uint? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>long</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(long property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>long</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(long? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>ulong</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(ulong property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>ulong</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(ulong? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>float</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="float"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public float? avg(float property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>float</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="float"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public float? avg(float? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>double</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(double property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>double</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public double? avg(double? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over <c>decimal</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); the mean is a fractional <see cref="decimal"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public decimal? avg(decimal property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property)</c> over nullable <c>decimal</c> values (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>avgIf</c>); <see langword="null"/> rows are ignored and the mean is a fractional <see cref="decimal"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        /// <param name="filter">The predicate the aggregated rows must satisfy.</param>
        public decimal? avg(decimal? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>sum(property)</c> (the ANSI <c>filter (where ...)</c> clause or the ClickHouse <c>sumIf</c>).</summary>
        public T? sum<T>(T? property, Expression<Func<bool>> filter) => default!;

        /// <summary><c>corr(Y, X)</c>: the correlation coefficient of a set of (Y, X) pairs.</summary>
        public double? corr<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>covar_pop(Y, X)</c>: the population covariance.</summary>
        public double? covar_pop<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>covar_samp(Y, X)</c>: the sample covariance.</summary>
        public double? covar_samp<TY, TX>(TY? y, TX? x) => default!;

        /// <summary>Order key of a window specification rendered as <c>asc</c>.</summary>
        public WindowOrder asc(Expression<Func<object?>> expression) => default!;

        /// <summary>Order key of a window specification rendered as <c>desc</c>.</summary>
        public WindowOrder desc(Expression<Func<object?>> expression) => default!;

        /// <summary>Sequential row number within the window partition.</summary>
        public WindowFunction<int> row_number() => default!;

        /// <summary>Rank with gaps within the window partition.</summary>
        public WindowFunction<int> rank() => default!;

        /// <summary>Rank without gaps within the window partition.</summary>
        public WindowFunction<int> dense_rank() => default!;

        /// <summary>Splits the partition into <paramref name="buckets"/> roughly equal groups.</summary>
        public WindowFunction<int> ntile(int buckets) => default!;

        /// <summary>Value of <paramref name="property"/> from the previous row (offset 1).</summary>
        public WindowFunction<T?> lag<T>(T? property) => default!;

        /// <summary>Value of <paramref name="property"/> from the row <paramref name="offset"/> rows back.</summary>
        public WindowFunction<T?> lag<T>(T? property, int offset) => default!;

        /// <summary>Value of <paramref name="property"/> from the row <paramref name="offset"/> rows back, or <paramref name="defaultValue"/>.</summary>
        public WindowFunction<T?> lag<T>(T? property, int offset, T? defaultValue) => default!;

        /// <summary>Value of <paramref name="property"/> from the next row (offset 1).</summary>
        public WindowFunction<T?> lead<T>(T? property) => default!;

        /// <summary>Value of <paramref name="property"/> from the row <paramref name="offset"/> rows ahead.</summary>
        public WindowFunction<T?> lead<T>(T? property, int offset) => default!;

        /// <summary>Value of <paramref name="property"/> from the row <paramref name="offset"/> rows ahead, or <paramref name="defaultValue"/>.</summary>
        public WindowFunction<T?> lead<T>(T? property, int offset, T? defaultValue) => default!;

        /// <summary>First value of <paramref name="property"/> in the window frame.</summary>
        public WindowFunction<T?> first_value<T>(T? property) => default!;

        /// <summary>Last value of <paramref name="property"/> in the window frame.</summary>
        public WindowFunction<T?> last_value<T>(T? property) => default!;

        /// <summary>
        /// <c>nth_value(property, n)</c>: the value of <paramref name="property"/> at the 1-based
        /// <paramref name="n"/>-th row of the window frame. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsNthValue"/>); SQL Server has no <c>NTH_VALUE</c>.
        /// </summary>
        public WindowFunction<T?> nth_value<T>(T? property, int n) => default!;

        /// <summary>Relative rank of the current row: <c>(rank - 1) / (rows - 1)</c>, in <c>[0, 1]</c>.</summary>
        public WindowFunction<double> percent_rank() => default!;

        /// <summary>Cumulative distribution of the current row within the partition, in <c>(0, 1]</c>.</summary>
        public WindowFunction<double> cume_dist() => default!;

        /// <summary>
        /// Windowed <c>percentile_cont(fraction)</c>: the interpolated value at
        /// <paramref name="fraction"/> in the window. Rendered as
        /// <c>percentile_cont(fraction) within group (order by value) over (...)</c>; requires a provider
        /// that supports the window percentile form (see
        /// <see cref="ISqlDialect.SupportsPercentileWindow"/>; SQL Server and MariaDB). PostgreSQL
        /// expresses percentiles as an ordered-set aggregate instead
        /// (<see cref="PostgresFunctions.percentile_cont"/>), which is not a window function.
        /// </summary>
        public WindowFunction<T?> percentile_cont<T>(double fraction, T? value) => default!;

        /// <summary>Windowed <c>percentile_disc(fraction)</c>: the first value whose position reaches the fraction.</summary>
        public WindowFunction<T?> percentile_disc<T>(double fraction, T? value) => default!;

        /// <summary>Windowed <c>sum</c>. The <c>_over</c> suffix avoids clashing with the scalar aggregate.</summary>
        public WindowFunction<T?> sum_over<T>(T? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>byte</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(byte property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>byte</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(byte? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>sbyte</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(sbyte property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>sbyte</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(sbyte? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>short</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(short property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>short</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(short? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>ushort</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(ushort property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>ushort</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(ushort? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>int</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(int property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>int</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(int? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>uint</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(uint property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>uint</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(uint? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>long</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(long property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>long</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(long? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>ulong</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(ulong property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>ulong</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(ulong? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>float</c> values; the window result is a fractional <see cref="float"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<float?> avg_over(float property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>float</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="float"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<float?> avg_over(float? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>double</c> values; the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(double property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>double</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="double"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<double?> avg_over(double? property) => default!;

        /// <summary>Windowed <c>avg</c> over <c>decimal</c> values; the window result is a fractional <see cref="decimal"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<decimal?> avg_over(decimal property) => default!;

        /// <summary>Windowed <c>avg</c> over nullable <c>decimal</c> values; <see langword="null"/> rows are ignored and the window result is a fractional <see cref="decimal"/>.</summary>
        /// <param name="property">The value to aggregate.</param>
        public WindowFunction<decimal?> avg_over(decimal? property) => default!;

        /// <summary>Windowed <c>min</c>.</summary>
        public WindowFunction<T?> min_over<T>(T? property) => default!;

        /// <summary>Windowed <c>max</c>.</summary>
        public WindowFunction<T?> max_over<T>(T? property) => default!;

        /// <summary>Windowed <c>count(*)</c>.</summary>
        public WindowFunction<int> count_over() => default!;

        /// <summary>Windowed <c>count(property)</c>.</summary>
        public WindowFunction<int> count_over<T>(T? property) => default!;

        /// <summary>
        /// <c>left is not distinct from right</c>: two-valued null-safe equality. Both NULL operands
        /// compare equal, exactly one NULL operand compares unequal, and two non-NULL operands compare
        /// with ordinary equality. Unlike <c>==</c>, the result is never SQL <c>NULL</c>/UNKNOWN.
        /// Requires a provider that supports null-safe comparison (see
        /// <see cref="ISqlDialect.NullSafeComparison"/>); the provider may render it natively or
        /// emulate it.
        /// </summary>
        /// <typeparam name="T">The common operand type; a nullable column uses the nullable type.</typeparam>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The two-valued comparison result.</returns>
        public bool is_not_distinct_from<T>(T left, T right) => default!;

        /// <summary>
        /// <c>left is distinct from right</c>: the complement of
        /// <see cref="is_not_distinct_from{T}(T, T)"/>. Two non-NULL operands compare with ordinary
        /// inequality, and a NULL on exactly one side compares unequal instead of yielding SQL
        /// <c>NULL</c>/UNKNOWN. Requires a provider that supports null-safe comparison (see
        /// <see cref="ISqlDialect.NullSafeComparison"/>).
        /// </summary>
        /// <typeparam name="T">The common operand type; a nullable column uses the nullable type.</typeparam>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The two-valued comparison result.</returns>
        public bool is_distinct_from<T>(T left, T right) => default!;
    }
