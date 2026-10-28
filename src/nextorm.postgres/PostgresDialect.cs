using System.Text;
using NextORM.Core;

namespace NextORM.Postgres;

/// <summary>
/// PostgreSQL dialect: <c>||</c> concatenation, <c>@name</c> parameters, double-quoted identifiers,
/// <c>limit/offset</c> paging and the aggregate name mapping (<c>stdev</c> -> <c>stddev</c>, ...).
/// </summary>
/// <remarks>
/// The type is deliberately not <see langword="sealed"/>. Subclassing it and overriding
/// <see cref="ExtremeRowRenderer"/> is the supported external extension boundary (a public
/// compatibility commitment), so an assembly without <c>InternalsVisibleTo</c> can install its own
/// public <see cref="IExtremeRowRenderer"/>. The built-in renderer is <c>internal</c> and is not part
/// of that contract. The public constructors and the shared <see cref="Instance"/> are preserved;
/// returning <see langword="null"/> from an override keeps the portable window-function lowering.
/// </remarks>
public class PostgresDialect : SqlDialectBase
{
    // The first PostgreSQL release with the ANSI aggregate FILTER clause (SQL:2003 T612, 9.4).
    private static readonly Version AggregatesFilterMinVersion = new(9, 4);

    // PostgreSQL 18 introduced array_sort() and uuidv7(); array_reverse() does not exist in any
    // current PostgreSQL release.
    private static readonly Version ArraySortMinVersion = new(18, 0);

    /// <summary>PostgreSQL supports <c>SAVEPOINT</c>/<c>ROLLBACK TO SAVEPOINT</c>/<c>RELEASE SAVEPOINT</c>.</summary>
    public override bool SupportsSavepoints => true;

    /// <summary>
    /// Gets the shared PostgreSQL dialect instance. Subclassing the dialect does not change this
    /// instance; a derived dialect is created through the public constructors instead.
    /// </summary>
    public static readonly PostgresDialect Instance = new();

    private readonly Version? _version;
    private readonly PostgresUuidGenerators _uuidGenerators;

    /// <summary>
    /// Creates an unversioned PostgreSQL dialect: every supported form (including the ANSI aggregate
    /// <c>FILTER</c> clause) is emitted. The version-gated functions of PostgreSQL 18
    /// (<c>array_sort</c>, <c>uuidv7</c>) are refused, and <c>array_reverse</c> is always refused
    /// because no current PostgreSQL release provides it. Equivalent to the shared <see cref="Instance"/>.
    /// </summary>
    /// <remarks>
    /// The constructor is public so an external assembly can derive a custom dialect from
    /// <see cref="PostgresDialect"/>.
    /// </remarks>
    public PostgresDialect()
    {
        _uuidGenerators = PostgresUuidGenerators.Instance;
    }

    /// <summary>
    /// Creates a PostgreSQL dialect for <paramref name="serverVersion"/>. A version below 9.4 gates off
    /// the ANSI aggregate <c>FILTER</c> clause; <c>array_sort</c> and <c>uuidv7</c> render only from an
    /// explicit 18. A <c>null</c> version is the unversioned form, which refuses those two and
    /// <c>array_reverse</c> (never available).
    /// </summary>
    /// <param name="serverVersion">The PostgreSQL server version, or <see langword="null"/> for unset.</param>
    /// <remarks>
    /// The constructor is public so an external assembly can derive a custom dialect from
    /// <see cref="PostgresDialect"/>; passing <see langword="null"/> equals the parameterless form.
    /// </remarks>
    public PostgresDialect(Version? serverVersion)
    {
        _version = serverVersion;
        _uuidGenerators = serverVersion is null
            ? PostgresUuidGenerators.Instance
            : new PostgresUuidGenerators(serverVersion);
    }

    /// <inheritdoc/>
    public override string ConcatStringOperator => "||";

    /// <summary>PostgreSQL has a native row-value type: <c>ROW(a, b)</c> with <c>(row).fN</c> access.</summary>
    public override ITupleRenderer? Tuple => PostgresTupleRenderer.Instance;

    /// <summary>PostgreSQL supports <c>INSERT ... RETURNING &lt;column&gt;</c>.</summary>
    public override bool SupportsReturning => true;

    /// <summary>PostgreSQL returns the affected rows of a multi-table <c>UPDATE ... FROM ... JOIN</c> through <c>RETURNING</c>.</summary>
    public override bool SupportsUpdateJoinReturning => true;

    /// <summary>PostgreSQL returns the affected rows of a multi-table <c>DELETE ... USING ... JOIN</c> through <c>RETURNING</c>.</summary>
    public override bool SupportsDeleteJoinReturning => true;

    /// <summary>Npgsql exposes streaming <c>GetStream</c>/<c>GetTextReader</c> accessors for LOB columns.</summary>
    public override bool SupportsSequentialAccess => true;

    /// <summary>PostgreSQL renders the native <c>NULLS FIRST</c>/<c>NULLS LAST</c> ordering modifier.</summary>
    public override bool SupportsNullsOrdering => true;

    /// <inheritdoc/>
    public override string RenderOrderBy(string expressionSql, OrderDirection direction, NullsPosition nullsPosition)
        => NullsOrderingRenderer.RenderNative(expressionSql, direction, nullsPosition);

    /// <summary>PostgreSQL allows a data-modifying statement (<c>INSERT ... RETURNING</c>) as a CTE body.</summary>
    public override bool SupportsDataModifyingCtes => true;

    /// <summary>PostgreSQL expresses a key upsert as <c>INSERT ... ON CONFLICT (&lt;keys&gt;) DO UPDATE SET ...</c> (9.5+).</summary>
    public override bool SupportsOnConflict => true;

    /// <summary>PostgreSQL has a native bulk path (<c>COPY ... FROM STDIN (FORMAT BINARY)</c>).</summary>
    public override bool SupportsBulkCopy => true;

    /// <summary>PostgreSQL batches through <c>NpgsqlBatch</c>, which wraps the commands in an implicit transaction so they share one backend.</summary>
    public override bool SupportsBatch => true;

    /// <summary>Npgsql maps <c>CommandType.StoredProcedure</c> to <c>CALL name(...)</c>, so PostgreSQL procedures (not functions) are invoked through <c>ExecuteProcedure</c>.</summary>
    public override bool SupportsStoredProcedures => true;

    /// <summary>PostgreSQL emulates a table-valued parameter with a typed array (<c>unnest</c>/<c>ANY</c>) or a JSON document (<c>jsonb_to_recordset</c>).</summary>
    public override bool SupportsTableValuedParameters => true;

    /// <summary>PostgreSQL skips conflicting rows with a trailing <c>ON CONFLICT DO NOTHING</c>.</summary>
    public override bool SupportsOnConflictDoNothing => true;

    /// <summary>PostgreSQL writes an explicit value to a <c>GENERATED ALWAYS AS IDENTITY</c> column with <c>OVERRIDING SYSTEM VALUE</c>.</summary>
    public override string MakeOverridingSystemValue(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " overriding system value");

    /// <summary>PostgreSQL 15+ renders a general <c>MERGE</c>; the server version is a documented requirement.</summary>
    public override bool SupportsMergeStatement => true;

    /// <summary>PostgreSQL <c>MERGE</c> supports a <c>WHEN MATCHED THEN DELETE</c> branch.</summary>
    public override bool SupportsMergeDelete => true;

    /// <summary>PostgreSQL <c>MERGE</c> supports a <c>THEN DO NOTHING</c> branch.</summary>
    public override bool SupportsMergeDoNothing => true;

    /// <summary>PostgreSQL <c>MERGE</c> supports an explicit <c>ON &lt;condition&gt;</c> and <c>WHEN ... AND &lt;condition&gt;</c>.</summary>
    public override bool SupportsMergeConditionalBranches => true;

    /// <summary>PostgreSQL <c>MERGE</c> forbids qualifying a target column in the <c>UPDATE SET</c> list.</summary>
    public override bool SupportsMergeTargetQualification => false;

    /// <summary>PostgreSQL <c>MERGE ... RETURNING</c> must qualify target columns, otherwise they are ambiguous with the source.</summary>
    public override string MakeMergeReturning(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower)
        => Kw(keywordCase, " returning ") + string.Join(", ", columns.Select(static c => "target." + c));

    /// <summary>PostgreSQL reads the last generated identity of the session through the <c>lastval()</c> function.</summary>
    public override bool SupportsIdentityFunction => true;

    /// <summary>PostgreSQL supports <c>INSERT ... DEFAULT VALUES</c> for an all-defaults row.</summary>
    public override bool SupportsDefaultValues => true;

    /// <summary>PostgreSQL accepts <c>DEFAULT</c> as a value in the <c>VALUES</c> list.</summary>
    public override bool SupportsColumnDefault => true;

    /// <summary>PostgreSQL has a native <c>TRUNCATE TABLE</c>.</summary>
    public override bool SupportsTruncate => true;

    /// <summary>PostgreSQL deletes rows based on a join through the <c>USING</c> clause.</summary>
    public override bool SupportsDeleteJoin => true;

    /// <summary>PostgreSQL renders the <c>USING</c> spelling of <see cref="MakeDeleteJoin"/>.</summary>
    public override bool DeleteJoinRequiresUsing => true;

    /// <summary>
    /// Renders the PostgreSQL <c>USING</c> form of a multi-table delete. The joined tables are listed in
    /// <c>USING</c> and the join conditions are folded into the <c>WHERE</c>, because a <c>USING</c> join
    /// condition cannot reference the delete target (the target is aliased in <c>DELETE FROM</c>).
    /// </summary>
    public override string MakeDeleteJoin(
        string target,
        string targetAlias,
        string fromAndJoins,
        string usingSources,
        string joinConditions,
        string? whereSql,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        var where = string.IsNullOrEmpty(joinConditions)
            ? whereSql
            : string.IsNullOrEmpty(whereSql)
                ? joinConditions
                : joinConditions + Kw(keywordCase, " and ") + whereSql;

        var sql = Kw(keywordCase, "delete from ") + target + Kw(keywordCase, " as ") + targetAlias
            + Kw(keywordCase, " using ") + usingSources;

        return string.IsNullOrEmpty(where) ? sql : sql + Kw(keywordCase, " where ") + where;
    }

    /// <summary>PostgreSQL updates rows based on a join through the <c>FROM</c> clause.</summary>
    public override bool SupportsUpdateJoin => true;

    /// <summary>PostgreSQL renders the <c>FROM</c> spelling of the multi-table update (the target stays out of <c>FROM</c>).</summary>
    public override bool UpdateJoinRequiresFrom => true;

    /// <summary>Renders the identity-function query <c>select lastval()</c>.</summary>
    public override string MakeIdentityFunction(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "select lastval()");

    /// <inheritdoc/>
    public override string MakeParam(string name) => $"@{name}";

    /// <inheritdoc/>
    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    /// <inheritdoc/>
    public override string MakeBool(bool v) => v ? "true" : "false";

    /// <summary>PostgreSQL's text type is <c>text</c>, its duration type is <c>interval</c>, a <see cref="Range{T}"/> maps to the native range type selected by its bound type and a <see cref="Range{T}"/> array maps to the matching multirange.</summary>
    /// <param name="type">The CLR type to name.</param>
    /// <returns>The PostgreSQL type name.</returns>
    public override string MakeTypeName(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Range<>))
            return PostgresRangeTypes.NameFor(type.GetGenericArguments()[0]);

        if (type.IsArray && type.GetElementType() is { } element
            && element.IsGenericType && element.GetGenericTypeDefinition() == typeof(Range<>))
            return PostgresRangeTypes.MultirangeNameFor(element.GetGenericArguments()[0]);

        return type switch
        {
            _ when type == typeof(string) => "text",
            _ when type == typeof(TimeSpan) => "interval",
            _ when type == typeof(uint) => "xid",
            _ => base.MakeTypeName(type)
        };
    }

    /// <summary>PostgreSQL has a native duration type (<c>interval</c>), so a <see cref="TimeSpan"/> is stored natively.</summary>
    public override bool SupportsNativeDuration => true;

    /// <summary>PostgreSQL's native duration type is <c>interval</c>, optionally with fractional-second precision.</summary>
    public override string MakeDurationType(DurationUnit? unit, int precision = 0)
        => precision > 0 ? $"interval({precision})" : "interval";

    // PostgreSQL only accepts double-quoted identifiers; single-quoted aliases are a syntax error.
    /// <inheritdoc/>
    public override string Escape(string keyword) => "\"" + keyword + "\"";

    /// <inheritdoc/>
    public override string MakeColumnReference(string name) => Escape(name);

    /// <inheritdoc/>
    public override bool RequireSubqueryAlias => true;

    // PostgreSQL spells the APPLY surface as CROSS JOIN LATERAL / LEFT JOIN LATERAL ... ON true,
    // which is exactly the SqlDialectBase default.
    /// <inheritdoc/>
    public override bool SupportsApply => true;

    /// <summary>PostgreSQL supports a raw SQL derived table (<c>FROM (&lt;sql&gt;) AS alias</c>).</summary>
    public override bool SupportsRawSqlSource => true;

    /// <summary>
    /// PostgreSQL has no statement hints in the core server; the optional <c>pg_hint_plan</c> extension
    /// reads an inline <c>/*+ ... */</c> comment. nextorm renders that comment unconditionally, because
    /// on a server without the extension it is an ordinary comment.
    /// </summary>
    public override bool SupportsQueryHints => true;

    /// <summary>PostgreSQL renders join/subquery/tables-in-scope hints as one <c>/*+ ... */</c> comment.</summary>
    public override bool SupportsInlineHints => true;

    /// <summary>PostgreSQL expresses the subquery hint through the same <c>/*+ ... */</c> comment.</summary>
    public override bool SupportsSubQueryHints => true;

    /// <summary>
    /// Renders the statement-level hints as a <c>/*+ ... */</c> comment immediately after the top-level
    /// <c>select</c> (the position <c>pg_hint_plan</c> reads; a <c>WITH</c> prefix and subqueries are
    /// skipped). PostgreSQL has no <c>maxrecursion</c> option, so <paramref name="maxRecursionOption"/>
    /// is ignored.
    /// </summary>
    public override string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var depth = 0;
        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            if (c is '\'' or '"' or '`')
            {
                var quote = c;
                for (i++; i < sql.Length; i++)
                {
                    if (sql[i] != quote) continue;
                    if (quote == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'') { i++; continue; }
                    break;
                }

                continue;
            }

            if (c == '(') { depth++; continue; }
            if (c == ')') { if (depth > 0) depth--; continue; }
            if (depth != 0 || !MatchesSelect(sql, i)) continue;

            return sql.Insert(i + "select".Length, $" /*+ {string.Join(" ", hints)} */");
        }

        return sql;
    }

    private static bool MatchesSelect(string sql, int index)
    {
        const string keyword = "select";
        if (index + keyword.Length > sql.Length) return false;
        if (string.Compare(sql, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
        if (index > 0 && IsIdentifierChar(sql[index - 1])) return false;
        var end = index + keyword.Length;
        return end >= sql.Length || !IsIdentifierChar(sql[end]);
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // PostgreSQL is the only supported provider that implements INTERSECT ALL / EXCEPT ALL.
    /// <inheritdoc/>
    public override bool SupportsIntersectExceptAll => true;

    // PostgreSQL renders the ANSI GROUP BY ROLLUP (...)/CUBE (...) form.
    /// <inheritdoc/>
    public override bool SupportsRollup => true;
    /// <inheritdoc/>
    public override bool SupportsCube => true;
    /// <inheritdoc/>
    public override bool SupportsGroupingSets => true;

    // PostgreSQL has native array types and the any/all quantifiers over arrays.
    /// <inheritdoc/>
    public override bool SupportsArrays => true;

    /// <summary>
    /// Per-name availability of the PostgreSQL array functions: <c>array_reverse</c> is not available in
    /// any current PostgreSQL release, and <c>array_sort</c> arrived in PostgreSQL 18, so it needs an
    /// explicit 18+ version (an unset version refuses it). Every other name keeps the family default.
    /// </summary>
    /// <param name="name">The SQL name of the array function, without schema qualification.</param>
    public override bool SupportsArrayFunction(string name) => name switch
    {
        "array_reverse" => false,
        "array_sort" => _version is not null && _version >= ArraySortMinVersion,
        _ => base.SupportsArrayFunction(name)
    };

    // PostgreSQL has native range types (int4range/int8range/numrange/tsrange/tstzrange/daterange).
    /// <inheritdoc/>
    public override bool SupportsRanges => true;

    /// <inheritdoc/>
    public override bool SupportsRangeColumns => true;

    /// <inheritdoc/>
    public override bool SupportsTableFunction(string name) =>
        name is "generate_series" or "unnest"
            or "regexp_matches" or "regexp_split_to_table" or "string_to_table"
            or "jsonb_array_elements" or "jsonb_array_elements_text"
            or "jsonb_each" or "jsonb_each_text" or "jsonb_object_keys"
            or "jsonb_path_query" or "ts_stat"
            or "jsonb_to_record" or "jsonb_to_recordset"
            // The #281 JSON readers of PG17: the two array readers, the two object readers, the
            // time-zone-aware JSONPath query and the two <c>record</c> readers with a caller-declared
            // column definition list. The non-<c>b</c> readers take the payload as JSON text and are
            // cast in the call; the populate_record family stays refused (an anyelement base row).
            or "json_array_elements" or "json_array_elements_text"
            or "json_each" or "json_each_text"
            or "jsonb_path_query_tz"
            or "json_to_record" or "json_to_recordset"
            // The #281 system-information readers: 17 record/table-shaped functions plus the three
            // single-column ones. Every name is a PostgreSQL built-in of PG17's system-information
            // chapter; the row shapes are pinned by local row interfaces in SqlFunctions.
            or "pg_available_wal_summaries" or "pg_control_checkpoint" or "pg_control_init"
            or "pg_control_recovery" or "pg_control_system" or "pg_get_catalog_foreign_keys"
            or "pg_get_keywords" or "pg_get_multixact_members" or "pg_get_object_address"
            or "pg_get_wal_summarizer_state" or "pg_identify_object" or "pg_identify_object_as_address"
            or "pg_input_error_info" or "pg_last_committed_xact" or "pg_listening_channels"
            or "pg_options_to_table" or "pg_snapshot_xip" or "pg_tablespace_databases"
            or "pg_wal_summary_contents" or "pg_xact_commit_timestamp_origin";

    /// <summary>PostgreSQL renders the row-derived schema as the alias column-definition list of the record function.</summary>
    public override bool SupportsResultSchema(TableFunctionSchema placement) => placement == TableFunctionSchema.AliasColumnList;

    /// <summary>
    /// PostgreSQL's record set-returning functions take their result schema as an alias
    /// column-definition list: <c>jsonb_to_record(json) as "t1"(a integer, b text)</c>.
    /// </summary>
    public override string MakeTableFunctionAlias(string tableAlias, string? columnDefinitionList, KeywordCase keywordCase = KeywordCase.Lower)
        => columnDefinitionList is null
            ? MakeTableAlias(tableAlias, keywordCase)
            : MakeTableAlias(tableAlias, keywordCase) + "(" + columnDefinitionList + ")";

    /// <summary>
    /// PostgreSQL names the only output column of a scalar set-returning function after the function,
    /// but aliasing the function in <c>FROM</c> renames that column to the alias. nextorm always
    /// aliases a derived source (<see cref="RequireSubqueryAlias"/>), so those functions are wrapped
    /// in a one-column subquery that restores the function-named column
    /// (<c>select generate_series from generate_series(...)</c>). Functions with an explicit output
    /// column (<c>value</c>, <c>key</c>/<c>value</c>, <c>word</c>/<c>ndoc</c>/<c>nentry</c>) keep
    /// their names and are emitted unchanged.
    /// </summary>
    public override string WrapTableFunction(string name, string call) =>
        name is "generate_series" or "unnest" or "regexp_matches" or "regexp_split_to_table" or "string_to_table"
            or "jsonb_object_keys" or "jsonb_path_query"
            or "pg_listening_channels"
            ? $"(select {name}\n from {call})"
            : call;

    /// <summary>
    /// PostgreSQL renders the record/table-valued system-information readers of #281 with an explicit
    /// projection that keeps every documented column name while converting the types the row mapper
    /// cannot read: <c>pg_lsn</c>, <c>oid</c>, <c>regclass</c> and <c>xid8</c> columns become text (the
    /// scalar family casts its <c>oid</c>/<c>pg_lsn</c>/<c>xid8</c> results the same way) and the internal
    /// <c>"char"</c> keyword-category code becomes text. The argument casts are load-bearing for the same
    /// reason as in the scalar family: an <c>oid</c>, <c>xid</c>, <c>pg_lsn</c> or <c>pg_snapshot</c>
    /// argument does not resolve from a text parameter. A function whose columns and arguments are all
    /// mappable is left alone and keeps its bare call.
    /// </summary>
    public override string WrapTableFunction(string name, string call, IReadOnlyList<string> arguments) => name switch
    {
        "json_array_elements" or "json_array_elements_text" or "json_each" or "json_each_text"
            or "json_to_record" or "json_to_recordset" =>
            // The non-b JSON readers take a json value; a text parameter does not resolve the overload
            // (json_array_elements(text) does not exist), so the document argument is cast — harmless for
            // an inline JSON literal, load-bearing for a bound parameter.
            $"{name}(cast({arguments[0]} as json))",
        "jsonb_path_query_tz" =>
            // A scalar set-returning function names its only column after the function, but aliasing the
            // call in FROM renames that column to the alias, so the match column is restored in a
            // one-column subquery. The document/path/vars arguments are cast: a text parameter resolves to
            // none of jsonb/jsonpath.
            "(select jsonb_path_query_tz\n from jsonb_path_query_tz("
            + $"cast({arguments[0]} as jsonb), cast({arguments[1]} as jsonpath),"
            + $" cast({arguments[2]} as jsonb), {arguments[3]}))",
        "pg_control_recovery" =>
            "(select min_recovery_end_lsn::text as min_recovery_end_lsn, min_recovery_end_timeline,"
            + " backup_start_lsn::text as backup_start_lsn, backup_end_lsn::text as backup_end_lsn,"
            + " end_of_backup_record_required"
            + $"\n from {call})",
        "pg_control_checkpoint" =>
            "(select checkpoint_lsn::text as checkpoint_lsn, redo_lsn::text as redo_lsn, redo_wal_file,"
            + " timeline_id, prev_timeline_id, full_page_writes, next_xid, next_oid::text as next_oid,"
            + " next_multixact_id, next_multi_offset, oldest_xid, oldest_xid_dbid::text as oldest_xid_dbid,"
            + " oldest_active_xid, oldest_multi_xid, oldest_multi_dbid::text as oldest_multi_dbid,"
            + " oldest_commit_ts_xid, newest_commit_ts_xid, checkpoint_time"
            + $"\n from {call})",
        "pg_get_keywords" =>
            "(select word, catcode::text as catcode, barelabel, catdesc, baredesc"
            + $"\n from {call})",
        "pg_snapshot_xip" =>
            "(select pg_snapshot_xip::text as pg_snapshot_xip"
            + $"\n from pg_snapshot_xip(cast({arguments[0]} as pg_snapshot)))",
        "pg_tablespace_databases" =>
            "(select pg_tablespace_databases::text as pg_tablespace_databases"
            + $"\n from pg_tablespace_databases(cast({arguments[0]} as oid)))",
        "pg_get_catalog_foreign_keys" =>
            "(select fktable::text as fktable, fkcols, pktable::text as pktable, pkcols, is_array, is_opt"
            + $"\n from {call})",
        "pg_available_wal_summaries" =>
            "(select tli, start_lsn::text as start_lsn, end_lsn::text as end_lsn"
            + $"\n from {call})",
        "pg_get_multixact_members" =>
            // Same text bridge as the transaction-origin reader: a uint literal renders as an integer,
            // and PostgreSQL has no integer-to-xid cast (42846).
            $"(select * from pg_get_multixact_members(cast(cast({arguments[0]} as text) as xid)))",
        "pg_get_object_address" =>
            "(select classid::text as classid, objid::text as objid, objsubid"
            + "\n from pg_get_object_address("
            + string.Join(", ", new[]
            {
                arguments[0],
                $"cast({arguments[1]} as text[])",
                $"cast({arguments[2]} as text[])"
            })
            + "))",
        "pg_get_wal_summarizer_state" =>
            "(select summarized_tli, summarized_lsn::text as summarized_lsn,"
            + " pending_lsn::text as pending_lsn, summarizer_pid"
            + $"\n from {call})",
        "pg_last_committed_xact" =>
            "(select xid, timestamp, roident::text as roident"
            + $"\n from {call})",
        "pg_identify_object" =>
            $"(select * from pg_identify_object(cast({arguments[0]} as oid), cast({arguments[1]} as oid), {arguments[2]}))",
        "pg_identify_object_as_address" =>
            $"(select * from pg_identify_object_as_address(cast({arguments[0]} as oid), cast({arguments[1]} as oid), {arguments[2]}))",
        "pg_wal_summary_contents" =>
            "(select relfilenode::text as relfilenode, reltablespace::text as reltablespace,"
            + " reldatabase::text as reldatabase, relforknumber, relblocknumber, is_limit_block"
            + "\n from pg_wal_summary_contents("
            + $"{arguments[0]}, cast({arguments[1]} as pg_lsn), cast({arguments[2]} as pg_lsn)))",
        "pg_xact_commit_timestamp_origin" =>
            // The xid travels as text first: a uint literal renders as an integer, and PostgreSQL has
            // no integer-to-xid cast (42846), while text-to-xid resolves for both the literal and the
            // xid-bound parameter the driver sends.
            "(select timestamp, roident::text as roident"
            + $"\n from pg_xact_commit_timestamp_origin(cast(cast({arguments[0]} as text) as xid)))",
        _ => WrapTableFunction(name, call)
    };

    // PostgreSQL has native json/jsonb types and the associated functions/operators.
    /// <inheritdoc/>
    public override bool SupportsJson => true;

    // PostgreSQL (and only PostgreSQL) renders the json/jsonb functions and access/containment operators.
    /// <inheritdoc/>
    public override bool SupportsPostgresJsonSql => true;

    // PostgreSQL accepts the FILTER (WHERE ...) aggregate clause from 9.4 (SQL:2003 T612),
    // greatest/least, date_trunc and the string_agg/array_agg aggregate surface. A version below 9.4
    // rejects the filtered-aggregate overloads instead of emitting SQL the server cannot parse.
    /// <inheritdoc/>
    public override AggregateFilterStyle AggregateFilterStyle =>
        _version is null || _version >= AggregatesFilterMinVersion ? AggregateFilterStyle.AnsiFilter : AggregateFilterStyle.None;
    /// <inheritdoc/>
    public override bool SupportsGreatestLeast => true;

    /// <summary>PostgreSQL has no <c>iif</c>/<c>if</c> function; it renders the conditional as a <c>CASE</c> expression.</summary>
    public override IIifRenderer Iif => PostgresIifRenderer.Instance;

    /// <summary>PostgreSQL supports the ANSI <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>PostgreSQL supports <c>nth_value(value, n)</c> as a window function.</summary>
    public override bool SupportsNthValue => true;

    /// <summary>PostgreSQL declares named windows (<c>WINDOW w AS (...)</c>) and references them with <c>OVER w</c>.</summary>
    public override bool SupportsNamedWindows => true;

    /// <summary>PostgreSQL 11+ supports the <c>GROUPS</c> window frame unit.</summary>
    public override bool SupportsWindowFrameGroups => true;

    /// <summary>PostgreSQL renders both null-safe comparison forms natively (<c>IS [NOT] DISTINCT FROM</c>).</summary>
    public override INullSafeComparisonRenderer? NullSafeComparison => PostgresNullSafeComparisonRenderer.Instance;

    /// <summary>PostgreSQL supports the frame <c>EXCLUDE CURRENT ROW</c>/<c>GROUP</c>/<c>TIES</c>/<c>NO OTHERS</c> clause.</summary>
    public override bool SupportsWindowFrameExclusion => true;
    /// <inheritdoc/>
    public override bool SupportsDateTrunc => true;
    /// <inheritdoc/>
    public override bool SupportsDateArithmetic => true;
    /// <inheritdoc/>
    public override bool SupportsStringArrayAggregates => true;

    /// <summary>PostgreSQL renders every date part through <c>extract</c>, including the ISO week and dow forms.</summary>
    public override bool SupportsDatePart(string part) =>
        part is "dow" or "isodow" or "epoch" || base.SupportsDatePart(part);

    /// <summary>PostgreSQL's <c>extract(epoch ...)</c> returns numeric, so it is cast to double precision.</summary>
    public override string MakeDatePart(string part, string value) =>
        part == "epoch" ? $"cast(extract(epoch from {value}) as double precision)" : base.MakeDatePart(part, value);

    /// <summary>PostgreSQL stores a <see cref="System.DateTimeOffset"/> as <c>timestamptz</c>, so DTO operations are supported.</summary>
    public override bool SupportsDateTimeOffsetTranslation => true;

    /// <summary>
    /// PostgreSQL's <c>now()</c> is a session-time-zone-independent instant; materialized as a
    /// <c>timestamptz</c> it canonicalises to an offset-zero <see cref="System.DateTimeOffset"/>.
    /// </summary>
    public override string MakeDateTimeOffsetNow(bool utc) => "now()";

    /// <summary>
    /// DTO date parts are extracted from the UTC wall time (<c>at time zone 'UTC'</c>) so the result
    /// does not depend on the session time zone or DST.
    /// </summary>
    public override string MakeDatePart(string part, string value, bool isDateTimeOffset) =>
        isDateTimeOffset ? MakeDatePart(part, $"({value} at time zone 'UTC')") : MakeDatePart(part, value);

    /// <summary>
    /// DTO date addition runs on the UTC wall time and is re-typed to <c>timestamptz</c> (a naive
    /// <c>timestamp without time zone</c> result cannot be read as a
    /// <see cref="System.DateTimeOffset"/>), so the emitted value is the original instant plus the
    /// interval, independent of the session time zone/DST.
    /// </summary>
    public override string MakeDateAdd(string field, string amount, string value, bool isDateTimeOffset) =>
        isDateTimeOffset
            ? $"(({value} at time zone 'UTC') + ({amount} * interval '1 {field}')) at time zone 'UTC'"
            : MakeDateAdd(field, amount, value);

    /// <summary>
    /// The ANSI <c>date_diff</c> casts a sub-day span to <c>integer</c>; the 64-bit variant widens that
    /// cast to <c>bigint</c> so milliseconds/microseconds over a long range do not overflow.
    /// </summary>
    public override string MakeDateDiffBig(string field, string start, string end) =>
        MakeDateDiffCore(field, start, end, big: true);

    // PostgreSQL full-text search matches a tsvector against a tsquery; contains/freetext differ in
    // how the search string is parsed (plain terms vs. web-search syntax).
    /// <inheritdoc/>
    public override bool SupportsFullText => true;

    /// <inheritdoc/>
    public override string MakeFullText(string functionName, string column, string search) =>
        functionName == "freetext"
            ? $"to_tsvector({column}) @@ websearch_to_tsquery({search})"
            : $"to_tsvector({column}) @@ plainto_tsquery({search})";

    /// <summary>PostgreSQL supports the <c>SELECT DISTINCT ON (expr, ...)</c> modifier.</summary>
    public override IDistinctOnRenderer DistinctOn => PostgresDistinctOnRenderer.Instance;

    /// <summary>
    /// PostgreSQL renders eligible <c>SelectWhereMax</c>/<c>SelectWhereMin</c> with <c>DISTINCT ON</c>/<c>ORDER BY ... LIMIT 1</c>; the rest falls back to the portable window function.
    /// The property is <see langword="virtual"/> so a derived dialect can override it with a custom
    /// <see cref="IExtremeRowRenderer"/>; returning <see langword="null"/> keeps the portable
    /// window-function lowering. The built-in renderer is <c>internal</c> and is not a public contract.
    /// </summary>
    public override IExtremeRowRenderer? ExtremeRowRenderer => PostgresExtremeRowRenderer.Instance;

    /// <summary>PostgreSQL supports the <c>SelectWhereMax</c>/<c>SelectWhereMin</c> feature (native or portable).</summary>
    public override bool SupportsSelectWhereMinMax => true;

    /// <summary>PostgreSQL supports the <c>TABLESAMPLE</c> table modifier (both <c>SYSTEM</c> and <c>BERNOULLI</c>).</summary>
    public override ITableSampleMethods TableSample => PostgresTableSampleMethods.Instance;

    // PostgreSQL is the reference provider for the extended scalar function library and the
    // bool/bit/statistical/ordered-set aggregate surface.
    /// <inheritdoc/>
    public override bool SupportsExtendedScalarFunctions => true;

    /// <summary>PostgreSQL has the standalone session random seed <c>setseed</c>.</summary>
    public override bool SupportsRandomSeed => true;

    /// <summary>PostgreSQL renders the <c>digest</c> (pgcrypto) and <c>sha256</c> (core) hash functions.</summary>
    public override bool SupportsCryptoFunctions => true;

    /// <summary>PostgreSQL is the only provider with the native <c>tsvector</c>/<c>tsquery</c> text-search surface.</summary>
    public override bool SupportsTextSearchFunctions => true;

    /// <summary>PostgreSQL renders the whole session/information family.</summary>
    public override ISessionInfoFunctions SessionInfoFunctions => PostgresSessionInfoFunctions.Instance;

    /// <summary>PostgreSQL renders both UUID generators through the core functions.</summary>
    public override IUuidGenerators UuidGenerators => _uuidGenerators;

    /// <summary>PostgreSQL renders every cross-provider scalar function natively (<c>space</c> as <c>repeat(' ', n)</c>).</summary>
    public override IScalarFunctions ScalarFunctions => PostgresScalarFunctions.Instance;

    /// <summary>PostgreSQL encodes base64 through the core <c>encode</c> function.</summary>
    public override bool SupportsBase64Encode => true;

    /// <inheritdoc/>
    public override string MakeBase64Encode(string value) =>
        $"replace(encode({TypedNull(value, "bytea")}, 'base64'), E'\\n', '')";

    /// <summary>PostgreSQL decodes base64 through the core <c>decode</c> function.</summary>
    public override bool SupportsBase64Decode => true;

    /// <inheritdoc/>
    public override string MakeBase64Decode(string value) =>
        $"decode({TypedNull(value, "text")}, 'base64')";

    /// <summary>PostgreSQL generates random bytes through <c>pgcrypto</c>'s <c>gen_random_bytes</c>.</summary>
    public override bool SupportsCryptGenRandom => true;

    /// <inheritdoc/>
    public override string MakeCryptGenRandom(string length)
    {
        var argument = TypedNull(length, "integer");
        return $"CASE WHEN {argument} IS NULL THEN CAST(NULL AS bytea) ELSE gen_random_bytes({argument}) END";
    }

    /// <summary>
    /// Replaces the bare <c>null</c> token of a literal NULL argument with the provider's typed NULL,
    /// so the function resolves the expected binary/text/integer input type instead of the
    /// polymorphic <c>unknown</c> literal.
    /// </summary>
    private static string TypedNull(string value, string sqlType) =>
        value == "null" ? $"CAST(NULL AS {sqlType})" : value;


    /// <inheritdoc/>
    public override bool SupportsBooleanAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsBitAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsStatisticalAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsRegressionAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsOrderedAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsOrderedPercentiles => true;

    /// <inheritdoc/>
    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddev",
        "stdevp" => "stddev_pop",
        "var" => "variance",
        "varp" => "var_pop",
        _ => name
    };

    /// <summary>
    /// PostgreSQL's <c>log()</c> is base 10; the natural logarithm (<c>Math.Log</c>) is
    /// <c>ln()</c>. The two-argument form only exists as <c>log(numeric, numeric)</c>, so both operands
    /// are cast to <c>numeric</c>.
    /// </summary>
    public override string MakeMathFunction(string name, IReadOnlyList<string> args) =>
        name switch
        {
            "log" when args.Count == 1 => $"ln({args[0]})",
            "log" when args.Count == 2 => $"log(({args[0]})::numeric, ({args[1]})::numeric)",
            _ => base.MakeMathFunction(name, args)
        };

    /// <summary>
    /// PostgreSQL's two-argument <c>round</c> only accepts <c>numeric</c>, so a double precision/real
    /// first argument is cast; decimal/integer already resolve to <c>round(numeric, integer)</c>.
    /// </summary>
    public override string MakeMathFunction(string name, IReadOnlyList<string> args, IReadOnlyList<Type> argTypes) =>
        name == "round" && args.Count == 2 && IsFloatingPoint(argTypes[0])
            ? $"round(({args[0]})::numeric, {args[1]})"
            : base.MakeMathFunction(name, args, argTypes);

    private static bool IsFloatingPoint(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(double) || underlying == typeof(float);
    }

    /// <inheritdoc/>
    protected override string MakeStringPosition(string value, string substring) =>
        $"strpos({value}, {substring})";

    /// <inheritdoc/>
    public override string MakeRepeat(string value, string count) => $"repeat({value}, {count})";

    /// <inheritdoc/>
    protected override string MakeStringReverse(string value) => $"reverse({value})";

    /// <inheritdoc/>
    public override string MakeStuff(string value, string start, string? count, string newValue) =>
        count is null
            ? MakeSubstring(value, "0", start)
            : $"overlay({value} placing {newValue} from {start} + 1 for {count})";

    /// <summary>PostgreSQL renders CLR format specifiers through <c>to_char</c>/<c>to_hex</c>.</summary>
    public override IStringFormatFunctions? StringFormats => PostgresStringFormats.Instance;

    /// <summary>PostgreSQL supports a per-expression <c>COLLATE</c> clause.</summary>
    public override bool SupportsCollation => true;

    /// <inheritdoc/>
    public override string MakeCollate(string value, string collation, KeywordCase keywordCase = KeywordCase.Lower) =>
        value + Kw(keywordCase, " collate ") + QuoteIdentifier(collation);

    /// <summary>PostgreSQL can express ordinal comparison through the <c>"C"</c> collation.</summary>
    public override bool SupportsOrdinalComparison => true;

    /// <inheritdoc/>
    public override string MakeOrdinal(string value, bool ignoreCase) =>
        MakeCollate(ignoreCase ? $"lower({value})" : value, "C");

    /// <summary>PostgreSQL matches with the <c>~</c>/<c>~*</c> operators and replaces with <c>regexp_replace</c>.</summary>
    public override bool SupportsRegex => true;

    /// <inheritdoc/>
    public override string MakeRegexMatch(string value, string pattern, bool ignoreCase) =>
        $"{value} {(ignoreCase ? "~*" : "~")} {SqlStringLiteralPolicy.Postgres.Render(pattern, SqlStringLiteralRole.RegexPattern)}";

    /// <inheritdoc/>
    public override string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase) =>
        $"regexp_replace({value}, {SqlStringLiteralPolicy.Postgres.Render(pattern, SqlStringLiteralRole.RegexPattern)}, {SqlStringLiteralPolicy.Postgres.Render(replacement, SqlStringLiteralRole.RegexReplacement)}, {(ignoreCase ? "'gi'" : "'g'")})";

    /// <inheritdoc/>
    public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
    {
        // WITH TIES is only expressible through the FETCH form (LIMIT has no WITH TIES variant).
        if (paging.HasWithTies)
        {
            if (paging.Offset > 0)
                sqlBuilder.Append(Kw(keywordCase, "offset ")).Append(paging.Offset).Append(' ');

            sqlBuilder.Append(Kw(keywordCase, "fetch first ")).Append(paging.Limit).Append(Kw(keywordCase, " rows with ties"));
            return;
        }

        // PostgreSQL uses "limit N offset M"; OFFSET may appear on its own, but LIMIT must come first.
        if (paging.Limit > 0)
            sqlBuilder.Append(Kw(keywordCase, "limit ")).Append(paging.Limit);

        if (paging.Offset > 0)
        {
            if (paging.Limit > 0)
                sqlBuilder.Append(' ');

            sqlBuilder.Append(Kw(keywordCase, "offset ")).Append(paging.Offset);
        }
    }

    /// <summary>PostgreSQL supports <c>FETCH FIRST ... WITH TIES</c>.</summary>
    public override bool SupportsWithTies => true;

    /// <summary>PostgreSQL supports the trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause.</summary>
    public override ILockRenderer Lock => PostgresLockRenderer.Instance;

    /// <summary>PostgreSQL supports <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelect => true;

    /// <summary>PostgreSQL accepts <c>IF NOT EXISTS</c> on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectIfNotExists => true;

    /// <summary>PostgreSQL accepts a column list on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectColumnList => true;

    /// <summary>PostgreSQL supports <c>ON COMMIT { PRESERVE ROWS | DELETE ROWS | DROP }</c> on a temporary table.</summary>
    public override bool SupportsCreateTableAsSelectOnCommit => true;

    /// <summary>PostgreSQL supports <c>WITH [NO] DATA</c> on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectWithNoData => true;

    /// <summary>Adds the PostgreSQL <c>ON COMMIT { PRESERVE ROWS | DELETE ROWS | DROP }</c> clause of a temporary table.</summary>
    protected override string MakeCreateTableAsHead(CreateTableAsClause clause, KeywordCase keywordCase)
    {
        var head = base.MakeCreateTableAsHead(clause, keywordCase);
        if (clause.OnCommit is TempTableOnCommit.PreserveRows)
            return head;

        return head + Kw(keywordCase, " on commit ") + Kw(keywordCase, clause.OnCommit switch
        {
            TempTableOnCommit.DeleteRows => "delete rows",
            TempTableOnCommit.Drop => "drop",
            _ => "preserve rows",
        });
    }

    /// <summary>Appends the PostgreSQL <c>WITH NO DATA</c> clause.</summary>
    protected override string MakeCreateTableAsTail(CreateTableAsClause clause, KeywordCase keywordCase)
        => clause.WithData ? string.Empty : Kw(keywordCase, " with no data");
}

internal sealed class PostgresIifRenderer : IIifRenderer
{
    public static readonly PostgresIifRenderer Instance = new();

    public string Render(string condition, string whenTrue, string whenFalse) =>
        $"case when {condition} then {whenTrue} else {whenFalse} end";
}

internal sealed class PostgresSessionInfoFunctions : ISessionInfoFunctions
{
    public static readonly PostgresSessionInfoFunctions Instance = new();

    public bool Supports(string name) =>
        name is "current_user" or "session_user" or "current_schema" or "current_database" or "version"
            or "pg_backend_pid" or "inet_server_addr" or "inet_server_port" or "inet_client_addr"
            or "inet_client_port";

    public string Render(string name) => name switch
    {
        "current_user" or "session_user" or "current_schema" => name,
        "current_database" => "current_database()",
        "version" => "version()",
        "pg_backend_pid" => "pg_backend_pid()",
        "inet_server_addr" => "CAST(inet_server_addr() AS text)",
        "inet_server_port" => "inet_server_port()",
        "inet_client_addr" => "CAST(inet_client_addr() AS text)",
        "inet_client_port" => "inet_client_port()",
        _ => throw new NotSupportedException($"The {name} session information function is not supported by PostgreSQL.")
    };
}

/// <summary>
/// Renders <c>gen_random_uuid</c> (available since PostgreSQL 13) and <c>uuidv7</c> (PostgreSQL 18+).
/// <c>uuidv7</c> is enabled only for an explicit version at or above 18; an unversioned dialect refuses
/// it in both <see cref="Supports"/> and <see cref="Render"/> instead of emitting SQL the server rejects.
/// </summary>
internal sealed class PostgresUuidGenerators : IUuidGenerators
{
    public static readonly PostgresUuidGenerators Instance = new(null);

    // uuidv7() arrived in PostgreSQL 18.
    private static readonly Version UuidV7MinVersion = new(18, 0);

    private readonly Version? _version;

    /// <summary>Creates the generator surface for <paramref name="serverVersion"/>; unset refuses <c>uuidv7</c>.</summary>
    /// <param name="serverVersion">The PostgreSQL server version, or <see langword="null"/> for unset.</param>
    public PostgresUuidGenerators(Version? serverVersion) => _version = serverVersion;

    private bool IsUuidV7Enabled => _version is not null && _version >= UuidV7MinVersion;

    public bool Supports(string name) => name switch
    {
        "gen_random_uuid" => true,
        "uuidv7" => IsUuidV7Enabled,
        _ => false
    };

    public string Render(string name) => name switch
    {
        "gen_random_uuid" => "gen_random_uuid()",
        "uuidv7" when IsUuidV7Enabled => "uuidv7()",
        "uuidv7" => throw new NotSupportedException(
            "The uuidv7 function is not supported by this provider."),
        _ => throw new NotSupportedException($"The {name} UUID generator function is not supported by PostgreSQL.")
    };
}

internal sealed class PostgresDistinctOnRenderer : IDistinctOnRenderer
{
    public static readonly PostgresDistinctOnRenderer Instance = new();

    public string Render(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower) =>
        SqlKeywords.Of(keywordCase, "distinct on (") + string.Join(", ", columns) + ") ";
}

internal sealed class PostgresTableSampleMethods : ITableSampleMethods
{
    public static readonly PostgresTableSampleMethods Instance = new();

    public bool Supports(TableSampleMethod method) => true;

    public string Render(TableSampleMethod method, double percent, double? seed, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var text = SqlKeywords.Of(keywordCase, " tablesample ") + method.ToString().ToLowerInvariant()
            + " (" + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        return seed is { } value
            ? text + SqlKeywords.Of(keywordCase, " repeatable (") + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"
            : text;
    }
}

internal sealed class PostgresLockRenderer : ILockRenderer
{
    public static readonly PostgresLockRenderer Instance = new();

    public bool UsesTableHints => false;

    /// <summary>PostgreSQL supports the targeted <c>FOR UPDATE OF &lt;alias&gt;</c>/<c>FOR SHARE OF &lt;alias&gt;</c> form.</summary>
    public bool SupportsSourceLocks => true;

    public string Render(LockMode mode, KeywordCase keywordCase = KeywordCase.Lower) =>
        Render(mode, LockWaitMode.Wait, keywordCase);

    public string Render(LockMode mode, LockWaitMode wait, KeywordCase keywordCase = KeywordCase.Lower) =>
        SqlKeywords.Of(keywordCase, (mode == LockMode.Share ? " for share" : " for update") + LockWaitSuffix(wait));

    public string Render(LockMode mode, LockWaitMode wait, KeywordCase keywordCase, string? targetAlias)
    {
        if (targetAlias is null)
            return Render(mode, wait, keywordCase);

        ArgumentException.ThrowIfNullOrWhiteSpace(targetAlias);

        // Case-fold only the keyword prefix and wait suffix: the target is an already-resolved,
        // already-quoted alias and must stay verbatim (a case-folded quoted alias is a different,
        // non-existent identifier under KeywordCase.Upper).
        var prefix = SqlKeywords.Of(keywordCase, mode == LockMode.Share ? " for share of " : " for update of ");
        return prefix + targetAlias + SqlKeywords.Of(keywordCase, LockWaitSuffix(wait));
    }

    private static string LockWaitSuffix(LockWaitMode wait) => wait switch
    {
        LockWaitMode.Wait => "",
        LockWaitMode.NoWait => " nowait",
        LockWaitMode.SkipLocked => " skip locked",
        _ => throw new ArgumentOutOfRangeException(nameof(wait), wait, "Unknown locking wait mode.")
    };
}

internal sealed class PostgresTupleRenderer : ITupleRenderer
{
    public static readonly PostgresTupleRenderer Instance = new();

    public string RenderConstructor(IReadOnlyList<string> fields) => "ROW(" + string.Join(", ", fields) + ")";

    public string? RenderElement(string row, int oneBasedIndex) => "(" + row + ").f" + oneBasedIndex;
}

/// <summary>
/// PostgreSQL rendering of the culture-invariant CLR format specifiers: numbers through
/// <c>to_char</c> masks (and <c>to_hex</c> for <c>X</c>), date/time through a <c>to_char</c> mask.
/// The group separator (<c>N</c>) is not offered because PostgreSQL's <c>to_char</c> grouping is
/// locale-dependent.
/// </summary>
internal sealed class PostgresStringFormats : IStringFormatFunctions
{
    internal static readonly PostgresStringFormats Instance = new();

    public bool SupportsNumber(char specifier) => specifier is 'F' or 'D' or 'X';

    public string RenderNumber(string value, char specifier, int precision) => specifier switch
    {
        'F' => precision <= 0
            ? $"to_char({value}, 'FM9999999999999990')"
            : $"to_char({value}, 'FM9999999999999990.' || repeat('0', {precision}))",
        'D' => precision <= 0
            ? $"to_char({value}, 'FM9999999999999990')"
            : $"to_char({value}, 'FM' || repeat('0', {precision}))",
        'X' => precision <= 0
            ? $"upper(to_hex({value}))"
            : $"lpad(upper(to_hex({value})), {precision}, '0')",
        _ => throw new NotSupportedException($"The numeric format specifier '{specifier}' is not supported by PostgreSQL.")
    };

    public bool SupportsDateFormat(string clrFormat) => TryMapDate(clrFormat, out _);

    public string RenderDate(string value, string clrFormat) =>
        TryMapDate(clrFormat, out var mask)
            ? $"to_char({value}, '{mask}')"
            : throw new NotSupportedException($"The date/time format string '{clrFormat}' is not supported by PostgreSQL.");

    private static bool TryMapDate(string format, out string mask)
    {
        var sb = new StringBuilder(format.Length + 4);
        var i = 0;

        while (i < format.Length)
        {
            if (Match(format, i, "yyyy")) { sb.Append("YYYY"); i += 4; }
            else if (Match(format, i, "yy")) { sb.Append("YY"); i += 2; }
            else if (Match(format, i, "MM")) { sb.Append("MM"); i += 2; }
            else if (Match(format, i, "dd")) { sb.Append("DD"); i += 2; }
            else if (Match(format, i, "HH")) { sb.Append("HH24"); i += 2; }
            else if (Match(format, i, "mm")) { sb.Append("MI"); i += 2; }
            else if (Match(format, i, "ss")) { sb.Append("SS"); i += 2; }
            else if (format[i] is '-' or '/' or '.' or ':' or ' ') { sb.Append(format[i]); i++; }
            else { mask = string.Empty; return false; }
        }

        mask = sb.ToString();
        return true;
    }

    private static bool Match(string value, int index, string token) =>
        index + token.Length <= value.Length && string.CompareOrdinal(value, index, token, 0, token.Length) == 0;
}

/// <summary>
/// Renders the cross-provider scalar functions of <see cref="CommonFunctions"/> on PostgreSQL, where
/// every name has a native spelling (<c>left</c>/<c>right</c>, <c>lpad</c>/<c>rpad</c>, <c>repeat</c>/
/// <c>reverse</c>, <c>concat_ws</c>, <c>translate</c>, <c>ascii</c>, <c>chr</c>). <c>space</c> has no
/// native function and is rendered as <c>repeat(' ', n)</c>. Also renders the PostgreSQL numeric-scale
/// members <c>scale</c>/<c>min_scale</c>/<c>trim_scale</c>, the residual string family
/// (<c>btrim</c>/<c>ltrim</c>/<c>rtrim</c> with a character set, <c>starts_with</c>, <c>parse_ident</c>,
/// <c>quote_ident</c>/<c>quote_literal</c>/<c>quote_nullable</c>, <c>to_ascii</c>, <c>to_hex</c>/
/// <c>to_oct</c>/<c>to_bin</c>, <c>convert_from</c>/<c>convert_to</c>, <c>encode</c>/<c>decode</c>,
/// <c>normalize</c>, <c>unistr</c>, <c>regexp_match</c>), the residual numeric family (<c>div</c>,
/// <c>erf</c>/<c>erfc</c>, <c>random_normal</c> and the degree-based trigonometry), the residual
/// date/time family (<c>date_subtract</c>, <c>justify_interval</c>, <c>make_timestamptz</c>, the
/// transaction clock functions, <c>timeofday</c>, <c>isfinite</c>), the UUID extraction functions and
/// the binary-string family (<c>get_bit</c>/<c>set_bit</c>, <c>get_byte</c>/<c>set_byte</c>,
/// <c>bit_count</c>), each advertised by its own name.
/// </summary>
internal sealed class PostgresScalarFunctions : IScalarFunctions
{
    internal static readonly PostgresScalarFunctions Instance = new();

    /// <inheritdoc/>
    public bool Supports(string name) => name is
        "left" or "right" or "lpad" or "rpad" or "repeat" or "reverse" or "space" or
        "concat_ws" or "translate" or "ascii" or "char" or
        "bit_length" or "octet_length" or "cot" or "degrees" or "radians" or "pi" or
        "scale" or "min_scale" or "trim_scale" or
        "btrim" or "ltrim" or "rtrim" or "starts_with" or "parse_ident" or "quote_ident" or
        "quote_literal" or "quote_nullable" or "to_ascii" or "to_hex" or "to_oct" or "to_bin" or
        "convert_from" or "convert_to" or "encode" or "decode" or "normalize" or "unistr" or
        "regexp_match" or
        "div" or "erf" or "erfc" or "random_normal" or
        "acosd" or "asind" or "atand" or "atan2d" or "cosd" or "cotd" or "sind" or "tand" or
        "date_subtract" or "justify_interval" or "make_timestamptz" or
        "clock_timestamp" or "statement_timestamp" or "transaction_timestamp" or "timeofday" or
        "isfinite" or
        "uuid_extract_timestamp" or "uuid_extract_version" or
        "get_bit" or "set_bit" or "get_byte" or "set_byte" or "bit_count" or
        "current_schemas" or "pg_postmaster_start_time" or "pg_current_logfile" or
        "pg_conf_load_time" or "pg_jit_available" or "pg_my_temp_schema" or
        "pg_is_other_temp_schema" or "pg_notification_queue_usage" or
        "pg_blocking_pids" or "pg_safe_snapshot_blocking_pids" or "pg_trigger_depth" or
        "pg_collation_is_visible" or "pg_conversion_is_visible" or "pg_function_is_visible" or
        "pg_opclass_is_visible" or "pg_operator_is_visible" or "pg_opfamily_is_visible" or
        "pg_statistics_obj_is_visible" or "pg_table_is_visible" or "pg_ts_config_is_visible" or
        "pg_ts_dict_is_visible" or "pg_ts_parser_is_visible" or "pg_ts_template_is_visible" or
        "pg_type_is_visible" or "pg_char_to_encoding" or "pg_encoding_to_char" or "pg_basetype" or
        "pg_describe_object" or "pg_tablespace_location" or "pg_settings_get_flags" or
        "pg_index_has_property" or "pg_indexam_has_property" or "pg_index_column_has_property" or
        "pg_input_is_valid" or "pg_get_serial_sequence" or
        "pg_get_function_arguments" or "pg_get_function_identity_arguments" or
        "pg_get_function_result" or "pg_get_functiondef" or
        "pg_get_indexdef" or "pg_get_partition_constraintdef" or "pg_get_partkeydef" or
        "pg_get_ruledef" or "pg_get_statisticsobjdef" or "pg_get_triggerdef" or
        "pg_get_userbyid" or "pg_get_viewdef" or "pg_has_role" or
        "pg_current_snapshot" or "pg_current_xact_id" or "pg_current_xact_id_if_assigned" or
        "pg_snapshot_xmax" or "pg_snapshot_xmin" or "pg_visible_in_snapshot" or
        "pg_xact_commit_timestamp" or "pg_xact_status";

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> args) => name switch
    {
        "left" => $"left({args[0]}, {args[1]})",
        "right" => $"right({args[0]}, {args[1]})",
        "lpad" => $"lpad({args[0]}, {args[1]}, {Pad(args)})",
        "rpad" => $"rpad({args[0]}, {args[1]}, {Pad(args)})",
        "repeat" => $"repeat({args[0]}, {args[1]})",
        "reverse" => $"reverse({args[0]})",
        "space" => $"repeat(' ', {args[0]})",
        "concat_ws" => $"concat_ws({string.Join(", ", args)})",
        "translate" => $"translate({args[0]}, {args[1]}, {args[2]})",
        "ascii" => $"ascii({args[0]})",
        "char" => $"chr({args[0]})",
        "bit_length" => $"bit_length({args[0]})",
        "octet_length" => $"octet_length({args[0]})",
        "cot" => $"cot({args[0]})",
        "degrees" => $"degrees({args[0]})",
        "radians" => $"radians({args[0]})",
        "pi" => "pi()",
        "scale" => $"scale({args[0]})",
        "min_scale" => $"min_scale({args[0]})",
        "trim_scale" => $"trim_scale({args[0]})",
        // pg_my_temp_schema returns oid, which the driver cannot read as a string; cast to text as
        // pg_typeof does (see ExtendedScalarFunctionTranslator.EmitPgTypeOf).
        "pg_my_temp_schema" => "cast(pg_my_temp_schema() as text)",
        "btrim" or "ltrim" or "rtrim" or "starts_with" or "parse_ident" or "quote_ident" or
            "quote_literal" or "quote_nullable" or "to_ascii" or "to_hex" or "to_oct" or "to_bin" or
            "convert_from" or "convert_to" or "encode" or "decode" or "normalize" or "unistr" or
            "regexp_match" or "div" or "erf" or "erfc" or "random_normal" or
            "acosd" or "asind" or "atand" or "atan2d" or "cosd" or "cotd" or "sind" or "tand" or
            "date_subtract" or "justify_interval" or "make_timestamptz" or
            "clock_timestamp" or "statement_timestamp" or "transaction_timestamp" or "timeofday" or
            "isfinite" or "uuid_extract_timestamp" or "uuid_extract_version" or
            "get_bit" or "set_bit" or "get_byte" or "set_byte" or "bit_count" or
            "current_schemas" or "pg_postmaster_start_time" or "pg_current_logfile" or
            "pg_conf_load_time" or "pg_jit_available" or
            "pg_notification_queue_usage" or "pg_blocking_pids" or
            "pg_safe_snapshot_blocking_pids" or "pg_trigger_depth" or
            "pg_char_to_encoding" or "pg_encoding_to_char" or "pg_settings_get_flags" or
            "pg_index_has_property" or "pg_index_column_has_property" or "pg_input_is_valid" or
            "pg_get_serial_sequence"
            => $"{name}({string.Join(", ", args)})",
        // oid-typed arguments: a text parameter does not resolve to the oid overload (measured:
        // "function pg_table_is_visible(text) does not exist"), so the argument is cast, like the
        // regclass argument of nextval/currval.
        "pg_is_other_temp_schema" or "pg_collation_is_visible" or "pg_conversion_is_visible" or
            "pg_function_is_visible" or "pg_opclass_is_visible" or "pg_operator_is_visible" or
            "pg_opfamily_is_visible" or "pg_statistics_obj_is_visible" or "pg_table_is_visible" or
            "pg_ts_config_is_visible" or "pg_ts_dict_is_visible" or "pg_ts_parser_is_visible" or
            "pg_ts_template_is_visible" or "pg_type_is_visible"
            => $"{name}(cast({args[0]} as oid))",
        "pg_tablespace_location" => $"pg_tablespace_location(cast({args[0]} as oid))",
        "pg_indexam_has_property" => $"pg_indexam_has_property(cast({args[0]} as oid), {args[1]})",
        "pg_describe_object" =>
            $"pg_describe_object(cast({args[0]} as oid), cast({args[1]} as oid), {args[2]})",
        // regtype argument and regtype result: the driver cannot read a regtype, so the result is cast
        // to text (as pg_typeof does) and the text argument is cast to regtype to resolve the overload.
        "pg_basetype" => $"cast(pg_basetype(cast({args[0]} as regtype)) as text)",
        // pg_get_*_def/arguments take an oid; a text parameter does not resolve to the oid overload.
        "pg_get_function_arguments" or "pg_get_function_identity_arguments" or
            "pg_get_function_result" or "pg_get_functiondef" or
            "pg_get_partition_constraintdef" or "pg_get_partkeydef" or "pg_get_statisticsobjdef"
            => $"{name}(cast({args[0]} as oid))",
        // The same name has both a one-argument and a boolean-column form, so the arity picks the shape.
        "pg_get_indexdef" => args.Count == 1
            ? $"pg_get_indexdef(cast({args[0]} as oid))"
            : $"pg_get_indexdef(cast({args[0]} as oid), {args[1]}, {args[2]})",
        "pg_get_ruledef" => args.Count == 1
            ? $"pg_get_ruledef(cast({args[0]} as oid))"
            : $"pg_get_ruledef(cast({args[0]} as oid), {args[1]})",
        "pg_get_triggerdef" => args.Count == 1
            ? $"pg_get_triggerdef(cast({args[0]} as oid))"
            : $"pg_get_triggerdef(cast({args[0]} as oid), {args[1]})",
        "pg_get_userbyid" => $"pg_get_userbyid(cast({args[0]} as oid))",
        // to_regclass accepts a view name and a numeric OID in text form; the text -> regclass cast
        // resolves a name only (a numeric argument raises 42P01), so it cannot serve the (oid) rows.
        // The second argument's type (boolean or integer) picks the pretty overload; an unknown name
        // yields NULL.
        "pg_get_viewdef" => args.Count == 1
            ? $"pg_get_viewdef(to_regclass({args[0]}))"
            : $"pg_get_viewdef(to_regclass({args[0]}), {args[1]})",
        // regrole accepts a role name and a numeric OID; the two-argument form's second argument is the
        // privilege text, the three-argument form's is the membership role.
        "pg_has_role" => args.Count == 2
            ? $"pg_has_role(cast({args[0]} as regrole), {args[1]})"
            : $"pg_has_role(cast({args[0]} as regrole), cast({args[1]} as regrole), {args[2]})",
        // pg_snapshot and xid8 have no CLR mapping; the textual form is the only one the driver can read.
        "pg_current_snapshot" => "cast(pg_current_snapshot() as text)",
        "pg_current_xact_id" => "cast(pg_current_xact_id() as text)",
        "pg_current_xact_id_if_assigned" => "cast(pg_current_xact_id_if_assigned() as text)",
        // xid8 and pg_snapshot arguments are given in the textual form these members return.
        "pg_snapshot_xmax" => $"cast(pg_snapshot_xmax(cast({args[0]} as pg_snapshot)) as text)",
        "pg_snapshot_xmin" => $"cast(pg_snapshot_xmin(cast({args[0]} as pg_snapshot)) as text)",
        "pg_visible_in_snapshot" =>
            $"pg_visible_in_snapshot(cast({args[0]} as xid8), cast({args[1]} as pg_snapshot))",
        "pg_xact_commit_timestamp" => $"pg_xact_commit_timestamp(cast({args[0]} as xid))",
        "pg_xact_status" => $"pg_xact_status(cast({args[0]} as xid8))",
        _ => throw new NotSupportedException($"The {name} function is not supported by PostgreSQL.")
    };

    private static string Pad(IReadOnlyList<string> args) => args.Count == 3 ? args[2] : "' '";
}

/// <summary>
/// Renders the explicit null-safe comparison helpers on PostgreSQL, which has the ANSI spellings as
/// native operators: <c>a IS NOT DISTINCT FROM b</c> and <c>a IS DISTINCT FROM b</c>. Both are
/// two-valued, so the result is a plain boolean and needs no scalar legalisation.
/// </summary>
internal sealed class PostgresNullSafeComparisonRenderer : INullSafeComparisonRenderer
{
    internal static readonly PostgresNullSafeComparisonRenderer Instance = new();

    /// <inheritdoc/>
    public NullSafeComparisonDisposition DistinctDisposition => NullSafeComparisonDisposition.Native;

    /// <inheritdoc/>
    public NullSafeComparisonDisposition NotDistinctDisposition => NullSafeComparisonDisposition.Native;

    /// <inheritdoc/>
    public string RenderDistinct(string left, string right) => $"({left} is distinct from {right})";

    /// <inheritdoc/>
    public string RenderNotDistinct(string left, string right) => $"({left} is not distinct from {right})";
}
