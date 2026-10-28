using System.Linq;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Text-JSON surface (SQL Server and MySQL/MariaDB), the SQL Server-only <c>choose</c> conditional
/// function (the portable <c>iif</c> lives on <see cref="CommonFunctions"/>), the SQL Server
/// T-SQL-only scalar library (the string, trigonometric, date, binary/system and SQL/JSON functions
/// gated per name by <see cref="ISqlServerFunctions"/>), the SQL Server postfix XML data-type methods
/// (<c>xml_value</c>/<c>xml_query</c>/<c>xml_exist</c> and the <c>xml_nodes</c> rowset) and the SQL
/// Server table functions. Exposed through <see cref="SqlFunctions.SqlServer"/>; every member is gated
/// by a capability flag and rejected by providers that do not opt in.
/// </summary>
    public class SqlServerFunctions : CommonFunctions
    {
        /// <summary>
        /// The name of the server instance the client is connected to (<c>@@SERVERNAME</c>); SQL NULL when
        /// the instance has no configured name. Requires a provider that exposes the SQL Server identity
        /// surface (see <see cref="ISqlServerFunctions.SupportsSessionIdentity"/>; SQL Server). This is a
        /// server identity, not the client host (<see cref="host_name"/>).
        /// </summary>
        public string? server_name() => default!;

        /// <summary>
        /// The identifier of the client host (<c>HOST_ID()</c>) as the server recorded it; SQL NULL when the
        /// client did not report one. Requires a provider that exposes the SQL Server identity surface (see
        /// <see cref="ISqlServerFunctions.SupportsSessionIdentity"/>; SQL Server).
        /// </summary>
        public string? host_id() => default!;

        /// <summary>
        /// The name of the client host (<c>HOST_NAME()</c>) as the client reported it; SQL NULL when it did
        /// not. The value is client-supplied and therefore not a trusted identity — it is the client, not the
        /// server (<see cref="server_name"/>; on ClickHouse the server host is
        /// <see cref="ClickHouseFunctions.host_name"/>). Requires a provider that exposes the SQL Server
        /// identity surface (see <see cref="ISqlServerFunctions.SupportsSessionIdentity"/>; SQL Server).
        /// </summary>
        public string? host_name() => default!;

        /// <summary>
        /// The application name the client reported on connect (<c>APP_NAME()</c>); SQL NULL when it did not.
        /// The value is client-supplied and untrusted. Requires a provider that exposes the SQL Server
        /// identity surface (see <see cref="ISqlServerFunctions.SupportsSessionIdentity"/>; SQL Server).
        /// </summary>
        public string? app_name() => default!;

        /// <summary>
        /// The login name of the current security context (<c>SUSER_NAME()</c>), resolved from the security
        /// token rather than from the session string. Requires a provider that exposes the SQL Server
        /// identity surface (see <see cref="ISqlServerFunctions.SupportsSessionIdentity"/>; SQL Server).
        /// </summary>
        public string? suser_name() => default!;

        /// <summary>
        /// The login that originally connected the session (<c>ORIGINAL_LOGIN()</c>), which survives an
        /// <c>EXECUTE AS</c> context switch. Requires a provider that exposes the SQL Server identity surface
        /// (see <see cref="ISqlServerFunctions.SupportsSessionIdentity"/>; SQL Server).
        /// </summary>
        public string? original_login() => default!;

        /// <summary>
        /// <c>json_value(json, path)</c>: the scalar value at <paramref name="path"/>. Here JSON is
        /// stored in a plain text column rather than a native JSON type. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SupportsTextJson"/>).
        /// </summary>
        public string? json_value(string? json, string? path) => default!;

        /// <summary>
        /// <c>json_query(json, path)</c>: the JSON fragment (object/array) at <paramref name="path"/>.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SupportsTextJson"/>).
        /// </summary>
        public string? json_query(string? json, string? path) => default!;

        /// <summary>
        /// <c>json_modify(json, path, value)</c>: a copy of <paramref name="json"/> with
        /// <paramref name="path"/> updated. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsTextJson"/>).
        /// </summary>
        public string? json_modify(string? json, string? path, string? value) => default!;

        /// <summary>
        /// <c>isjson(value)</c>: true when the text is valid JSON. Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SupportsTextJson"/>).
        /// </summary>
        public bool isjson(string? value) => default!;

        /// <summary>
        /// <c>xml.value(xquery, sqltype)</c>: the scalar value selected by the XQuery, cast to the
        /// T-SQL type named by <paramref name="sqlType"/> (both arguments must be string literals).
        /// Rendered in the postfix form <c>xmlcol.value('(path)[1]', 'int')</c>; <typeparamref name="T"/>
        /// must match the requested SQL type. Requires a provider that supports the XML data-type
        /// methods (see <see cref="IXmlFunctions.Supports"/>; SQL Server).
        /// </summary>
        public T? xml_value<T>(string? xml, string? xpath, string? sqlType) => default!;

        /// <summary>
        /// <c>xml.query(xquery)</c>: the XML fragment selected by the XQuery (the XQuery must be a
        /// string literal). Rendered in the postfix form <c>xmlcol.query('/path')</c>. Requires a
        /// provider that supports the XML data-type methods (see
        /// <see cref="IXmlFunctions.Supports"/>; SQL Server).
        /// </summary>
        public string? xml_query(string? xml, string? xpath) => default!;

        /// <summary>
        /// <c>xml.exist(xquery)</c>: true when the XQuery selects at least one node (the XQuery must be
        /// a string literal). Rendered in the postfix form <c>xmlcol.exist('/path')</c>, which yields
        /// <c>bit</c>; in a predicate context the dialect compares it with 1. Requires a provider that
        /// supports the XML data-type methods (see
        /// <see cref="IXmlFunctions.Supports"/>; SQL Server).
        /// </summary>
        public bool xml_exist(string? xml, string? xpath) => default!;

        /// <summary>
        /// <c>xml.nodes(xquery)</c> as a composable rowset source (SQL Server): unfolds the XML value
        /// into one row per node selected by <paramref name="xpath"/> (the XQuery must be a string
        /// literal). Use only as the source of
        ///         <see cref="EntityBuilder{TEntity}.CrossApply{TJoinEntity}(System.Linq.Expressions.Expression{System.Func{TEntity, QueryCommand{TJoinEntity}}}, System.Action{NextORM.Core.JoinOptions})"/>
        /// (or <c>OuterApply</c>); rendered as <c>&lt;xml&gt;.nodes('xpath') as \[alias\](\[value\])</c>,
        /// and the unfolded <see cref="SqlFunctions.IXmlNodesRow.Value"/> is projected further with
        /// <see cref="xml_value{T}(string?, string?, string?)"/>/<see cref="xml_query(string?, string?)"/>/
        /// <see cref="xml_exist(string?, string?)"/>. Requires a provider that supports the XML
        /// data-type methods (see <see cref="IXmlFunctions.Supports"/>; SQL Server).
        /// </summary>
        /// <remarks>
        /// Unlike the runtime table-function factories (<c>string_split</c>/<c>openjson</c>) this is a
        /// correlated source over the left-hand row's column, not a standalone table function, so it is
        /// expressed through <c>CrossApply</c>/<c>OuterApply</c> rather than <c>ctx.From(...)</c>.
        /// </remarks>
        public QueryCommand<SqlFunctions.IXmlNodesRow> xml_nodes(string? xml, string? xpath) =>
            throw new NotSupportedException("xml_nodes can only be used as a CROSS/OUTER APPLY source.");

        /// <summary>
        /// <c>string_split(value, separator)</c> as a FROM source (SQL Server 2016+); select
        /// <see cref="SqlFunctions.IStringSplitRow.Value"/>. Use through
        /// <c>ctx.From(() =&gt; ...)</c>.
        /// The fragments are not guaranteed to be ordered; add an <c>order by</c> on the caller side if
        /// the input order matters.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IStringSplitRow> string_split(string? value, string? separator) => new("string_split");

        /// <summary>
        /// <c>openjson(json)</c> as a FROM source (SQL Server 2016+); select
        /// <see cref="SqlFunctions.IOpenJsonRow.Key"/>/<see cref="SqlFunctions.IOpenJsonRow.Value"/>/
        /// <see cref="SqlFunctions.IOpenJsonRow.Type"/>. Use through
        /// <c>ctx.From(() =&gt; ...)</c>.
        /// The default schema yields the properties of a JSON object or the elements of a JSON array;
        /// for a typed projection declare a runtime factory whose row shape matches the
        /// <see cref="TvfEntityBuilder{T}.WithClause"/> schema (for example
        /// <c>WithClause = "name nvarchar(50) '$.name'"</c>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IOpenJsonRow> openjson(string? json) => new("openjson");

        /// <summary>
        /// <c>CONTAINSTABLE(table, column, search)</c> as a FROM source (SQL Server full-text search with
        /// ranking). Join it back to the full-text-indexed table on
        /// <see cref="SqlFunctions.IKeyRankRow{TKey}.Key"/> and project
        /// <see cref="SqlFunctions.IKeyRankRow{TKey}.Rank"/>:
        /// <c>ctx.From&lt;IDocument&gt;().Join(ctx.From(() =&gt; SqlFunctions.SqlServer.containstable&lt;int&gt;("documents", "title", search)), (d, k) =&gt; d.Id == k.Key).OrderByDescending(p =&gt; p.Item2.Rank)</c>.
        /// <paramref name="table"/> and <paramref name="column"/> are emitted verbatim as identifiers
        /// (the table name or its alias exactly as it appears in the query); only pass trusted values.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IKeyRankRow<TKey>> containstable<TKey>(string table, string column, string search) => new("containstable", null, null, null, [0, 1], TableFunctionSchema.None);

        /// <summary>
        /// <c>FREETEXTTABLE(table, column, search)</c> as a FROM source; the natural-language counterpart
        /// of <see cref="containstable{TKey}(string, string, string)"/> (same <c>KEY</c>/<c>RANK</c> columns).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IKeyRankRow<TKey>> freetexttable<TKey>(string table, string column, string search) => new("freetexttable", null, null, null, [0, 1], TableFunctionSchema.None);

        /// <summary>
        /// <c>choose(index, value, ...)</c>: the 1-based <paramref name="index"/>-th value (NULL when out
        /// of range). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsChoose"/>; SQL Server).
        /// </summary>
        public TResult? choose<TResult>(int index, params TResult?[] values) => default!;

        /// <summary>
        /// <c>patindex('%pattern%', expression)</c>: the 1-based position of the first occurrence of
        /// <paramref name="pattern"/> in <paramref name="expression"/> (0 when absent), where <c>%</c> and
        /// <c>_</c> are wildcards. Not a regular-expression match. Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? patindex(string? pattern, string? expression) => default!;

        /// <summary>
        /// <c>quotename(value)</c>: brackets <paramref name="value"/> as a delimited identifier
        /// (NULL when <paramref name="value"/> is longer than 128 characters). Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? quotename(string? value) => default!;

        /// <summary>
        /// <c>quotename(value, quote)</c>: delimits <paramref name="value"/> with the single
        /// <paramref name="quote"/> character (for example <c>"["</c>, <c>"'"</c>, <c>"`"</c>).
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server).
        /// </summary>
        public string? quotename(string? value, string? quote) => default!;

        /// <summary>
        /// <c>soundex(value)</c>: the four-character Soundex code of <paramref name="value"/>.
        /// The result is collation-sensitive. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? soundex(string? value) => default!;

        /// <summary>
        /// <c>difference(first, second)</c>: how similar the Soundex codes of the two strings are, as a
        /// value from 0 (not similar) to 4 (very similar). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? difference(string? first, string? second) => default!;

        /// <summary>
        /// <c>string_escape(value, type)</c>: escapes the characters of <paramref name="value"/> that are
        /// special in the target format named by <paramref name="type"/> (for example <c>"json"</c> or
        /// <c>"xml"</c>; both must be literals). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? string_escape(string? value, string? type) => default!;

        /// <summary>
        /// <c>unicode(value)</c>: the UTF-16 code unit of the first character of <paramref name="value"/>.
        /// The Unicode counterpart of <see cref="CommonFunctions.ascii"/>. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? unicode(string? value) => default!;

        /// <summary>
        /// <c>nchar(code)</c>: the Unicode character with the given UTF-16 code unit. The Unicode
        /// counterpart of <see cref="CommonFunctions.@char"/>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? nchar(int code) => default!;

        /// <summary>
        /// <c>format(value, format)</c>: formats <paramref name="value"/> with a CLR standard or custom
        /// format string (the .NET formatting rules). Distinct from the CLR
        /// <see cref="string.Format(string, object?)"/> translation; this is the native T-SQL
        /// <c>FORMAT</c>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? format(object? value, string? format) => default!;

        /// <summary>
        /// <c>format(value, format, culture)</c>: as <see cref="format(object?, string?)"/> with an
        /// explicit <paramref name="culture"/> (for example <c>"en-US"</c>). Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? format(object? value, string? format, string? culture) => default!;

        /// <summary>
        /// <c>acos(value)</c>: the arc cosine, in radians. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public double? acos(double? value) => default!;

        /// <summary>
        /// <c>asin(value)</c>: the arc sine, in radians. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public double? asin(double? value) => default!;

        /// <summary>
        /// <c>atan(value)</c>: the arc tangent, in radians. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public double? atan(double? value) => default!;

        /// <summary>
        /// <c>atn2(y, x)</c>: the arc tangent of <paramref name="y"/> / <paramref name="x"/> (the T-SQL
        /// spelling of two-argument arc tangent). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public double? atn2(double? y, double? x) => default!;

        /// <summary>
        /// <c>square(value)</c>: the square of <paramref name="value"/>. The portable alternative is
        /// <c>value * value</c> or <see cref="Math.Pow(double, double)"/>. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public double? square(double? value) => default!;

        /// <summary>
        /// <c>datename(datepart, date)</c>: the name of the requested <paramref name="datepart"/> (for
        /// example the month or weekday name). <paramref name="datepart"/> must be a constant string.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server).
        /// </summary>
        public string? datename(string? datepart, DateTime? date) => default!;

        /// <summary>
        /// <c>date_bucket(datepart, width, date)</c>: the start of the <paramref name="width"/>-wide
        /// bucket of <paramref name="datepart"/> units that contains <paramref name="date"/>, using the
        /// default origin (1900-01-01). <paramref name="datepart"/> must be a constant string. Requires
        /// a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server
        /// 2022+).
        /// </summary>
        public DateTime? date_bucket(string? datepart, int width, DateTime? date) => default!;

        /// <summary>
        /// <c>date_bucket(datepart, width, date, origin)</c>: as
        /// <see cref="date_bucket(string?, int, DateTime?)"/> measured from <paramref name="origin"/>.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server 2022+).
        /// </summary>
        public DateTime? date_bucket(string? datepart, int width, DateTime? date, DateTime? origin) => default!;

        /// <summary>
        /// <c>hashbytes(algorithm, data)</c>: the hash of <paramref name="data"/> using the named
        /// algorithm (<c>MD5</c>, <c>SHA1</c>, <c>SHA2_256</c>, <c>SHA2_512</c>, <c>SHA3_256</c>, …;
        /// the algorithm must be a constant string). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public byte[]? hashbytes(string? algorithm, byte[]? data) => default!;

        /// <summary>
        /// <c>newsequentialid()</c>: a sequentially increasing GUID. T-SQL accepts it only as the
        /// <c>DEFAULT</c> of a <c>uniqueidentifier</c> column, not as a value in an ordinary
        /// <c>SELECT</c> list (the server raises an error there). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public Guid? newsequentialid() => default!;

        /// <summary>
        /// <c>NEXT VALUE FOR [sequence]</c>: the next value of the named sequence object (SQL Server
        /// 2012+). <paramref name="sequence"/> must be a constant string naming one object, written as
        /// either one component (<c>[name]</c>) or two (<c>[schema].[name]</c>); each component is
        /// bracket-quoted with an embedded <c>]</c> doubled, and the name is never bound as a SQL
        /// parameter. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public long next_value_for(string sequence) => default!;

        /// <summary>
        /// <c>nextval(sequence)</c>: an alias of <see cref="next_value_for(string)"/> that renders the
        /// same <c>NEXT VALUE FOR [sequence]</c> expression (SQL Server 2012+). Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public long nextval(string sequence) => default!;

        /// <summary>
        /// <c>json_array(value, ...)</c>: constructs a JSON array text from the arguments. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server
        /// 2022+).
        /// </summary>
        public string? json_array(params object?[] values) => default!;

        /// <summary>
        /// <c>json_object(key, value, ...)</c>: constructs a JSON object text from the alternating key
        /// and value arguments (keys must be string literals). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server 2022+).
        /// </summary>
        public string? json_object(params object?[] keyValuePairs) => default!;

        /// <summary>
        /// <c>json_arrayagg(value)</c>: aggregates the values of a group into a JSON array. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server
        /// 2025+).
        /// </summary>
        public string? json_arrayagg<T>(T? value) => default!;

        /// <summary>
        /// <c>json_objectagg(key, value)</c>: aggregates the key/value pairs of a group into a JSON
        /// object. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server 2025+).
        /// </summary>
        public string? json_objectagg<TKey, TValue>(TKey? key, TValue? value) => default!;

        /// <summary>
        /// <c>json_contains(json, searchValue, path)</c>: true when <paramref name="json"/> contains
        /// <paramref name="searchValue"/> at <paramref name="path"/>. Requires a provider that supports
        /// it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server 2025+). Distinct from the
        /// PostgreSQL/MySQL containment operator, which is expressed as a JSON predicate.
        /// </summary>
        public bool json_contains(string? json, string? searchValue, string? path) => default!;

        /// <summary>
        /// <c>json_path_exists(json, path)</c>: true when <paramref name="path"/> selects a value in
        /// <paramref name="json"/>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server 2022+).
        /// </summary>
        public bool json_path_exists(string? json, string? path) => default!;

        // --- Date/time clock, offset and FROMPARTS family (SQL Server 2008+; gated per name) ---

        /// <summary>
        /// <c>sysdatetime()</c>: the current system date/time as <c>datetime2</c> (server-local).
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server).
        /// </summary>
        public DateTime? sysdatetime() => default!;

        /// <summary>
        /// <c>sysdatetimeoffset()</c>: the current system date/time with the server's UTC offset as
        /// <c>datetimeoffset</c>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTimeOffset? sysdatetimeoffset() => default!;

        /// <summary>
        /// <c>sysutcdatetime()</c>: the current UTC date/time as <c>datetime2</c>. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTime? sysutcdatetime() => default!;

        /// <summary>
        /// <c>switchoffset(value, time_zone)</c>: changes the time-zone offset of a
        /// <c>datetimeoffset</c> to <paramref name="timeZone"/>, expressed as a string (for example
        /// <c>"-08:00"</c>). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTimeOffset? switchoffset(DateTimeOffset? value, string? timeZone) => default!;

        /// <summary>
        /// <c>switchoffset(value, time_zone_minutes)</c>: the signed-integer-minutes form of
        /// <see cref="switchoffset(DateTimeOffset?, string?)"/>. Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTimeOffset? switchoffset(DateTimeOffset? value, int timeZoneMinutes) => default!;

        /// <summary>
        /// <c>todatetimeoffset(value, time_zone)</c>: attaches <paramref name="timeZone"/> (a string
        /// such as <c>"+02:00"</c>) to a <c>datetime</c>, yielding a <c>datetimeoffset</c>. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTimeOffset? todatetimeoffset(DateTime? value, string? timeZone) => default!;

        /// <summary>
        /// <c>todatetimeoffset(value, time_zone_minutes)</c>: the signed-integer-minutes form of
        /// <see cref="todatetimeoffset(DateTime?, string?)"/>. Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTimeOffset? todatetimeoffset(DateTime? value, int timeZoneMinutes) => default!;

        /// <summary>
        /// <c>timefromparts(hour, minute, seconds, fractions, precision)</c>: builds a <c>time</c> from
        /// its parts. <paramref name="precision"/> must be a constant between 0 and 7. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public TimeSpan? timefromparts(int? hour, int? minute, int? seconds, int? fractions, int? precision) => default!;

        /// <summary>
        /// <c>smalldatetimefromparts(year, month, day, hour, minute)</c>: builds a <c>smalldatetime</c>.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server).
        /// </summary>
        public DateTime? smalldatetimefromparts(int? year, int? month, int? day, int? hour, int? minute) => default!;

        /// <summary>
        /// <c>datetimefromparts(year, month, day, hour, minute, seconds, milliseconds)</c>: builds a
        /// <c>datetime</c>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTime? datetimefromparts(int? year, int? month, int? day, int? hour, int? minute, int? seconds, int? milliseconds) => default!;

        /// <summary>
        /// <c>datetime2fromparts(year, month, day, hour, minute, seconds, fractions, precision)</c>:
        /// builds a <c>datetime2</c>. <paramref name="precision"/> must be a constant between 0 and 7.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server).
        /// </summary>
        public DateTime? datetime2fromparts(int? year, int? month, int? day, int? hour, int? minute, int? seconds, int? fractions, int? precision) => default!;

        /// <summary>
        /// <c>datetimeoffsetfromparts(year, month, day, hour, minute, seconds, fractions,
        /// hour_offset, minute_offset, precision)</c>: builds a <c>datetimeoffset</c>.
        /// <paramref name="precision"/> must be a constant between 0 and 7. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTimeOffset? datetimeoffsetfromparts(int? year, int? month, int? day, int? hour, int? minute, int? seconds, int? fractions, int? hourOffset, int? minuteOffset, int? precision) => default!;

        // --- Binary / checksum family ---

        /// <summary>
        /// <c>checksum(value, ...)</c>: a checksum computed over the listed values. At least one value
        /// is required; the wildcard <c>checksum(*)</c> form is not exposed. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? checksum(params object?[] values) => default!;

        /// <summary>
        /// <c>binary_checksum(value, ...)</c>: a checksum computed over the binary representations of
        /// the listed values. At least one value is required. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? binary_checksum(params object?[] values) => default!;

        /// <summary>
        /// <c>compress(value)</c>: GZIP-compresses a string into <c>varbinary(max)</c>. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public byte[]? compress(string? value) => default!;

        /// <summary>
        /// <c>compress(value)</c>: GZIP-compresses a binary value. Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public byte[]? compress(byte[]? value) => default!;

        /// <summary>
        /// <c>decompress(value)</c>: decompresses a value produced by <c>compress</c> (SQL Server
        /// returns <c>null</c> for an invalid or truncated input). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public byte[]? decompress(byte[]? value) => default!;

        // --- Other T-SQL scalars ---

        /// <summary>
        /// <c>rand()</c>: a pseudo-random <c>float</c> in [0, 1). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public double? rand() => default!;

        /// <summary>
        /// <c>rand(seed)</c>: a pseudo-random <c>float</c> in [0, 1) from the given integer seed.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server).
        /// </summary>
        public double? rand(int seed) => default!;

        /// <summary>
        /// <c>stuff(value, start, length, new_value)</c>: replaces <paramref name="length"/>
        /// characters of <paramref name="value"/> from 1-based <paramref name="start"/> with
        /// <paramref name="newValue"/>. <b>Character data only:</b> SQL Server's T-SQL <c>STUFF</c>
        /// implicitly converts binary arguments to <c>varchar</c>, so there is deliberately no
        /// <c>byte[]</c> overload (it could never return binary). Requires a provider that supports
        /// it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? stuff(string? value, int start, int length, string? newValue) => default!;

        // --- Metadata A: object/column/index metadata (missing objects yield NULL; gated per name) ---

        /// <summary>
        /// <c>col_length(table, column)</c>: the defined length, in bytes, of <paramref name="column"/>
        /// (NULL when the object or column does not exist). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? col_length(string? table, string? column) => default!;

        /// <summary>
        /// <c>col_name(table_id, column_id)</c>: the name of the column with the given ids (NULL when
        /// they do not identify a column). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? col_name(int? tableId, int? columnId) => default!;

        /// <summary>
        /// <c>ident_incr(table_or_view)</c>: the increment of the identity column of the table or view
        /// (NULL on error). T-SQL returns <c>numeric(38,0)</c>, whose ADO.NET mapping is
        /// <see cref="decimal"/> (not <see cref="int"/>), so an increment above the 32-bit range does
        /// not overflow on materialisation. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public decimal? ident_incr(string? table) => default!;

        /// <summary>
        /// <c>ident_seed(table_or_view)</c>: the seed of the identity column of the table or view (NULL
        /// on error). T-SQL returns <c>numeric(38,0)</c>, whose ADO.NET mapping is
        /// <see cref="decimal"/> (not <see cref="int"/>), so a seed above the 32-bit range does not
        /// overflow on materialisation. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public decimal? ident_seed(string? table) => default!;

        /// <summary>
        /// <c>index_col(table, index_id, key_id)</c>: the name of the indexed column (NULL when the
        /// index or key does not exist). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? index_col(string? table, int? indexId, int? keyId) => default!;

        /// <summary>
        /// <c>object_definition(object_id)</c>: the T-SQL source text of the object (NULL when it does
        /// not exist or the caller lacks permission). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? object_definition(int? objectId) => default!;

        /// <summary>
        /// <c>object_id(object_name)</c>: the id of the schema-scoped object (NULL when it does not
        /// exist). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? object_id(string? objectName) => default!;

        /// <summary>
        /// <c>object_id(object_name, object_type)</c>: as <see cref="object_id(string?)"/> restricted to
        /// <paramref name="objectType"/> (for example <c>"U"</c> for a user table). Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? object_id(string? objectName, string? objectType) => default!;

        /// <summary>
        /// <c>object_name(object_id)</c>: the name of the schema-scoped object with the given id (NULL
        /// when it does not exist). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? object_name(int? objectId) => default!;

        /// <summary>
        /// <c>object_name(object_id, database_id)</c>: as <see cref="object_name(int?)"/> resolved in
        /// <paramref name="databaseId"/> rather than the current database. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? object_name(int? objectId, int? databaseId) => default!;

        /// <summary>
        /// <c>object_schema_name(object_id)</c>: the name of the schema that owns the object with the
        /// given id (NULL when it does not exist). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? object_schema_name(int? objectId) => default!;

        /// <summary>
        /// <c>object_schema_name(object_id, database_id)</c>: as <see cref="object_schema_name(int?)"/>
        /// resolved in <paramref name="databaseId"/>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? object_schema_name(int? objectId, int? databaseId) => default!;

        /// <summary>
        /// <c>stats_date(table_id, stats_id)</c>: the last-updated date of the statistics (NULL when the
        /// statistics do not exist). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public DateTime? stats_date(int? tableId, int? statsId) => default!;

        // --- Metadata B: database/schema/type metadata (gated per name) ---

        /// <summary>
        /// <c>db_id()</c>: the id of the current database. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? db_id() => default!;

        /// <summary>
        /// <c>db_id(database_name)</c>: the id of the named database. Requires a provider that supports
        /// it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? db_id(string? database) => default!;

        /// <summary>
        /// <c>db_name()</c>: the name of the current database. Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? db_name() => default!;

        /// <summary>
        /// <c>db_name(database_id)</c>: the name of the database with the given id. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? db_name(int? databaseId) => default!;

        /// <summary>
        /// <c>original_db_name()</c>: the database name the client connected to (the original name in a
        /// contained-database redirect). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? original_db_name() => default!;

        /// <summary>
        /// <c>schema_id()</c>: the id of the caller's default schema. Requires a provider that supports
        /// it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? schema_id() => default!;

        /// <summary>
        /// <c>schema_id(schema_name)</c>: the id of the named schema. Requires a provider that supports
        /// it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? schema_id(string? schema) => default!;

        /// <summary>
        /// <c>schema_name()</c>: the name of the caller's default schema. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? schema_name() => default!;

        /// <summary>
        /// <c>schema_name(schema_id)</c>: the name of the schema with the given id. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? schema_name(int? schemaId) => default!;

        /// <summary>
        /// <c>type_id(type_name)</c>: the id of the named data type. Requires a provider that supports
        /// it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? type_id(string? typeName) => default!;

        /// <summary>
        /// <c>type_name(type_id)</c>: the name of the data type with the given id. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? type_name(int? typeId) => default!;

        // --- Metadata C: file/filegroup metadata (gated per name) ---

        /// <summary>
        /// <c>filegroup_id(filegroup_name)</c>: the id of the named filegroup. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? filegroup_id(string? filegroupName) => default!;

        /// <summary>
        /// <c>filegroup_name(filegroup_id)</c>: the name of the filegroup with the given id. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? filegroup_name(int? filegroupId) => default!;

        /// <summary>
        /// <c>file_id(file_name)</c>: the id of the database file with the given logical name. Requires
        /// a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? file_id(string? fileName) => default!;

        /// <summary>
        /// <c>file_idex(file_name)</c>: as <see cref="file_id(string?)"/>, but without the bounds of the
        /// current database. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? file_idex(string? fileName) => default!;

        /// <summary>
        /// <c>file_name(file_id)</c>: the logical name of the database file with the given id. Requires
        /// a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? file_name(int? fileId) => default!;

        // --- Metadata D: server/system metadata scalars (gated per name) ---

        /// <summary>
        /// <c>current_request_id()</c>: the id of the current request on the current connection (NULL
        /// outside a request scope). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server 2016+).
        /// </summary>
        public short? current_request_id() => default!;

        /// <summary>
        /// <c>current_timezone()</c>: the name of the current time zone as configured on the server.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server 2016+).
        /// </summary>
        public string? current_timezone() => default!;

        /// <summary>
        /// <c>current_timezone_id()</c>: the id of the current time zone. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server 2016+).
        /// </summary>
        public int? current_timezone_id() => default!;

        /// <summary>
        /// <c>current_transaction_id()</c>: the id of the current transaction (NULL outside a
        /// transaction scope). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server 2016+).
        /// </summary>
        public long? current_transaction_id() => default!;

        /// <summary>
        /// <c>formatmessage(msg_string, param, ...)</c>: builds a message from a format string and up to
        /// 20 arguments. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? formatmessage(string? message, params object?[] values) => default!;

        /// <summary>
        /// <c>formatmessage(msg_number, param, ...)</c>: builds the message stored under
        /// <paramref name="messageId"/> in <c>sys.messages</c> with up to 20 arguments. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? formatmessage(int? messageId, params object?[] values) => default!;

        /// <summary>
        /// <c>getansinull()</c>: whether the current database allows nulls by default. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? getansinull() => default!;

        /// <summary>
        /// <c>getansinull(database)</c>: whether the named database allows nulls by default. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? getansinull(string? database) => default!;

        /// <summary>
        /// <c>ident_current(table_or_view)</c>: the last identity value generated by any session for the
        /// named table or view. It is not a last-insert API for the current session and is racy under
        /// concurrent inserts. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public decimal? ident_current(string? tableOrView) => default!;

        /// <summary>
        /// <c>isdate(value)</c>: 1 when <paramref name="value"/> is a valid <c>datetime</c>, otherwise 0
        /// (the T-SQL form returns <c>int</c>, not <c>bit</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? isdate(object? value) => default!;

        /// <summary>
        /// <c>isnumeric(value)</c>: 1 when <paramref name="value"/> is a valid numeric type, otherwise 0
        /// (the T-SQL form returns <c>int</c>, not <c>bit</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public int? isnumeric(object? value) => default!;

        /// <summary>
        /// <c>min_active_rowversion()</c>: the oldest active rowversion, as <c>binary(8)</c> rather
        /// than a numeric counter. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public byte[] min_active_rowversion() => default!;

        /// <summary>
        /// <c>parsename(object_name, piece)</c>: the requested 1-based part of a four-part object name
        /// (NULL when the part is not a valid identifier). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? parsename(string? objectName, int? piece) => default!;

        /// <summary>
        /// <c>PARSE(value AS decimal(38,18) USING N'en-US')</c>: parses <paramref name="value"/> as a
        /// decimal with 18 fractional digits. The culture is fixed to <c>en-US</c> and is not a
        /// parameter, so the result does not depend on the session language; the portable alternative
        /// for other target types is not provided. SQL NULL (including a NULL argument) yields SQL
        /// NULL. Invalid text raises the server error (a <c>SqlException</c> with the CLR provider) —
        /// use <see cref="try_parse"/> for the NULL-returning form. Requires a provider that supports
        /// it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public decimal? parse(string? value) =>
            throw new NotSupportedException("The parse function can only be used inside a query expression.");

        /// <summary>
        /// <c>TRY_PARSE(value AS decimal(38,18) USING N'en-US')</c>: as <see cref="parse"/> with the
        /// fixed <c>en-US</c> culture, but invalid text returns SQL NULL instead of raising. SQL NULL
        /// (including a NULL argument) yields SQL NULL. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public decimal? try_parse(string? value) =>
            throw new NotSupportedException("The try_parse function can only be used inside a query expression.");

        /// <summary>
        /// <c>publishingservername()</c>: the name of the publishing server (the server name when a
        /// replicated database is published). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? publishingservername() => default!;

        /// <summary>
        /// <c>rowcount_big()</c>: the number of rows affected by the previous command on the same
        /// connection. nextorm executes every query as its own command, so the previous command is
        /// nextorm's own statement: use the affected-row count of the executed command instead.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>;
        /// SQL Server).
        /// </summary>
        public long rowcount_big() => default!;

        /// <summary>
        /// <c>scope_identity()</c>: the last identity value generated in the current scope, and only
        /// inside the same batch as the <c>INSERT</c> — a separate nextorm command sees NULL. Use
        /// <c>ReturningIdentity&lt;TKey&gt;()</c> for a last-insert key. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public decimal? scope_identity() => default!;

        /// <summary>
        /// <c>str(float_expression)</c>: the string form of a floating-point number (length 10,
        /// 0 decimals). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? str(double? value) => default!;

        /// <summary>
        /// <c>str(float_expression, length)</c>: the string form of a floating-point number with the
        /// given total <paramref name="length"/>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? str(double? value, int? length) => default!;

        /// <summary>
        /// <c>str(float_expression, length, decimal)</c>: as <see cref="str(double?, int?)"/> with an
        /// explicit number of <paramref name="decimalPlaces"/>. Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? str(double? value, int? length, int? decimalPlaces) => default!;

        /// <summary>
        /// <c>xact_state()</c>: the state of the current transaction in the native T-SQL encoding
        /// 0 (no active transaction), 1 (active and committable) or -1 (active but uncommittable, so the
        /// batch must be rolled back). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public short xact_state() => default!;

        /// <summary>
        /// <c>percentile_cont(0.5) within group (order by value) over (...)</c>: the median as the native
        /// analytic composition (T-SQL has no <c>median</c> function). Complete the call with
        /// <c>Over(...)</c>: a partition-only window (or an empty <c>over ()</c>) is
        /// supported, while a window <c>ORDER BY</c> or a frame is refused. The result keeps one row per
        /// input row, unlike an aggregate. Requires the window percentile capability
        /// (see <see cref="ISqlDialect.SupportsPercentileWindow"/>; SQL Server and MariaDB).
        /// </summary>
        /// <typeparam name="T">The value expression's type.</typeparam>
        /// <param name="value">The value to take the median of.</param>
        /// <returns>The interpolated median of the window, or SQL NULL when the window has no non-null values.</returns>
        public WindowFunction<double?> median<T>(T? value) => default!;

        /// <summary>
        /// <c>percentile</c>: not supported by SQL Server. The native spellings are
        /// <c>CommonFunctions.percentile_cont</c> and
        /// <c>CommonFunctions.percentile_disc</c> completed with
        /// <c>Over(...)</c>, which differ in whether the result is interpolated or
        /// an input value; a generic alias would hide that difference. Referencing this member throws
        /// <see cref="NotSupportedException"/> at translation time.
        /// </summary>
        /// <typeparam name="T">The value expression's type.</typeparam>
        /// <param name="fraction">The percentile fraction, in <c>[0, 1]</c>.</param>
        /// <param name="value">The value to take the percentile of.</param>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public WindowFunction<double?> percentile<T>(double fraction, T? value) => default!;

        /// <summary>
        /// <c>regexp_count(input, pattern)</c>: the number of non-overlapping matches of the regular
        /// expression <paramref name="pattern"/> in <paramref name="input"/> (<c>0</c> when there is no
        /// match, SQL NULL when the input is NULL). Rendered as <c>regexp_count(input, pattern)</c>.
        /// Requires SQL Server 2025 (major version 17) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2025Functions"/> gate; a lower or unversioned
        /// declaration is refused before SQL.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <returns>The match count as <c>int</c>, or SQL NULL when the input is NULL.</returns>
        public int? regexp_count(string? input, string? pattern) => default!;

        /// <summary>
        /// <c>regexp_substr(input, pattern)</c>: the first substring of <paramref name="input"/> matching
        /// the regular expression <paramref name="pattern"/>, or SQL NULL when there is no match or the
        /// input or pattern is NULL. Rendered as <c>regexp_substr(input, pattern)</c>. Requires SQL Server
        /// 2025 (major version 17) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2025Functions"/> gate; a lower or unversioned
        /// declaration is refused before SQL. The function is <b>not</b> compatibility-level gated: it
        /// executes at compatibility 160 on a major-17 engine.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression; invalid patterns are the engine's error.</param>
        /// <returns>The first match, or SQL NULL when there is no match or an operand is NULL.</returns>
        public string? regexp_substr(string? input, string? pattern) => default!;

        /// <summary>
        /// <c>regexp_substr(input, pattern, start)</c>: the first match at or after the one-based
        /// <paramref name="start"/> position. Omitting <paramref name="start"/> and passing a NULL start
        /// are separate forms with distinct native semantics: the omitted form starts at position 1, while
        /// a supplied NULL start yields SQL NULL (probe <c>substr_null_start</c>).
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <param name="start">The one-based search start; a value below 1 is the engine's error.</param>
        /// <returns>The match, or SQL NULL when there is no match or an operand is NULL.</returns>
        public string? regexp_substr(string? input, string? pattern, int? start) => default!;

        /// <summary>
        /// <c>regexp_substr(input, pattern, start, occurrence)</c>: the <paramref name="occurrence"/>-th
        /// (one-based) match at or after <paramref name="start"/>. A supplied NULL occurrence yields SQL
        /// NULL; an occurrence below 1 is the engine's error.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <param name="start">The one-based search start.</param>
        /// <param name="occurrence">The one-based match number.</param>
        /// <returns>The match, or SQL NULL when there is no match or an operand is NULL.</returns>
        public string? regexp_substr(string? input, string? pattern, int? start, int? occurrence) => default!;

        /// <summary>
        /// <c>regexp_substr(input, pattern, start, occurrence, match_type)</c>: the full native form with
        /// the <paramref name="match_type"/> flag string (<c>c</c>, <c>i</c>, <c>s</c> and <c>m</c>; any
        /// other flag is the engine's Msg 19303). A supplied NULL match type yields SQL NULL; the value is
        /// passed through, never validated client-side.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <param name="start">The one-based search start.</param>
        /// <param name="occurrence">The one-based match number.</param>
        /// <param name="match_type">The native match-type flags, or SQL NULL.</param>
        /// <returns>The match, or SQL NULL when there is no match or an operand is NULL.</returns>
        public string? regexp_substr(
            string? input, string? pattern, int? start, int? occurrence, string? match_type) => default!;

        /// <summary>
        /// <c>regexp_instr(input, pattern)</c>: the one-based position of the first match of
        /// <paramref name="pattern"/> in <paramref name="input"/>, <c>0</c> when there is no match and SQL
        /// NULL when an operand is NULL. Requires SQL Server 2025 (major version 17) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2025Functions"/> gate; it is not compatibility-level
        /// gated.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <returns>The match position, <c>0</c> for no match, or SQL NULL for a NULL operand.</returns>
        public int? regexp_instr(string? input, string? pattern) => default!;

        /// <summary><c>regexp_instr(input, pattern, start)</c>: searches from the one-based <paramref name="start"/>.</summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <param name="start">The one-based search start; a value below 1 is the engine's error.</param>
        /// <returns>The match position, <c>0</c> for no match, or SQL NULL for a NULL operand.</returns>
        public int? regexp_instr(string? input, string? pattern, int? start) => default!;

        /// <summary><c>regexp_instr(input, pattern, start, occurrence)</c>: the <paramref name="occurrence"/>-th match.</summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <param name="start">The one-based search start.</param>
        /// <param name="occurrence">The one-based match number.</param>
        /// <returns>The match position, <c>0</c> for no match, or SQL NULL for a NULL operand.</returns>
        public int? regexp_instr(string? input, string? pattern, int? start, int? occurrence) => default!;

        /// <summary>
        /// <c>regexp_instr(input, pattern, start, occurrence, return_option)</c>: <paramref name="return_option"/>
        /// <c>0</c> returns the match position (the default) and <c>1</c> the position immediately after
        /// the match. A supplied NULL return option behaves like the omitted form (<c>0</c>) in the
        /// observed engine; any other value is the engine's Msg 19301.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <param name="start">The one-based search start.</param>
        /// <param name="occurrence">The one-based match number.</param>
        /// <param name="return_option">The native return option (<c>0</c> or <c>1</c>).</param>
        /// <returns>The requested position, <c>0</c> for no match, or SQL NULL for a NULL operand.</returns>
        public int? regexp_instr(
            string? input, string? pattern, int? start, int? occurrence, int? return_option) => default!;

        /// <summary>
        /// <c>regexp_instr(input, pattern, start, occurrence, return_option, match_type)</c>: the full
        /// native form with the <paramref name="match_type"/> flag string. A supplied NULL match type
        /// yields SQL NULL; invalid flags are the engine's Msg 19303.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <param name="start">The one-based search start.</param>
        /// <param name="occurrence">The one-based match number.</param>
        /// <param name="return_option">The native return option (<c>0</c> or <c>1</c>).</param>
        /// <param name="match_type">The native match-type flags, or SQL NULL.</param>
        /// <returns>The requested position, <c>0</c> for no match, or SQL NULL for a NULL operand.</returns>
        public int? regexp_instr(
            string? input, string? pattern, int? start, int? occurrence, int? return_option,
            string? match_type) => default!;

        /// <summary>
        /// <c>left_shift(value, shift)</c>: the bitwise left shift of <paramref name="value"/> by
        /// <paramref name="shift"/> bits. Rendered with typed arguments and a typed-NULL guard
        /// (<c>case when value is null or shift is null then cast(null as int) else
        /// left_shift(cast(value as int), cast(shift as bigint)) end</c>) because a bare
        /// <c>left_shift(null, …)</c> raises Msg 8116. Requires SQL Server 2022 (major version 16) and
        /// the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate; a lower or
        /// unversioned declaration is refused before SQL.
        /// </summary>
        /// <param name="value">The <c>int</c> operand.</param>
        /// <param name="shift">The number of bits to shift; SQL NULL when either operand is NULL.</param>
        /// <returns>The shifted <c>int</c>, or SQL NULL when either operand is NULL.</returns>
        /// <remarks>
        /// <b>Overload resolution.</b> The bit family is one overload set per operand type
        /// (<c>byte?</c>/<c>short?</c>/<c>int?</c>/<c>long?</c>), so an untyped constant binds to the
        /// <i>smallest</i> operand overload that can hold it: <c>left_shift(1, 2)</c> selects
        /// <c>byte?</c> and renders <c>tinyint</c>, and <c>left_shift(-8, 1)</c> selects <c>short?</c>.
        /// The rendered SQL type therefore follows the compiler's pick, not the literal's intent. Cast
        /// the operand explicitly (<c>left_shift((int?)1, 2)</c>) whenever a specific SQL Server type is
        /// required. The overload set is deliberately not collapsed in this release.
        /// </remarks>
        public int? left_shift(int? value, int? shift) => default!;

        /// <summary>
        /// <c>left_shift(value, shift)</c> for a <see cref="byte"/> operand. Rendered with the operand
        /// typed as <c>tinyint</c> and the shift amount as <c>bigint</c>, behind a typed-NULL guard
        /// (<c>case when … then cast(null as tinyint) else … end</c>) because a bare
        /// <c>left_shift(null, …)</c> raises Msg 8116. Requires SQL Server 2022 (major version 16) and
        /// the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>tinyint</c> operand.</param>
        /// <param name="shift">The number of bits to shift.</param>
        /// <returns>The shifted <c>byte</c>, or SQL NULL when either argument is NULL.</returns>
        public byte? left_shift(byte? value, int? shift) => default!;

        /// <summary>
        /// <c>left_shift(value, shift)</c> for a <see cref="short"/> operand. Rendered with the operand
        /// typed as <c>smallint</c> and the shift amount as <c>bigint</c>, behind the same typed-NULL
        /// guard (a NULL yields <c>cast(null as smallint)</c>). Requires SQL Server 2022 (major version
        /// 16) and the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>smallint</c> operand.</param>
        /// <param name="shift">The number of bits to shift.</param>
        /// <returns>The shifted <c>short</c>, or SQL NULL when either argument is NULL.</returns>
        public short? left_shift(short? value, int? shift) => default!;

        /// <summary>
        /// <c>left_shift(value, shift)</c> for a <see cref="long"/> operand. Rendered with the operand
        /// typed as <c>bigint</c> and the shift amount as <c>bigint</c>, behind the same typed-NULL
        /// guard (a NULL yields <c>cast(null as bigint)</c>). Requires SQL Server 2022 (major version
        /// 16) and the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>bigint</c> operand.</param>
        /// <param name="shift">The number of bits to shift.</param>
        /// <returns>The shifted <c>long</c>, or SQL NULL when either argument is NULL.</returns>
        public long? left_shift(long? value, int? shift) => default!;

        /// <summary>
        /// <c>right_shift(value, shift)</c> for a <see cref="byte"/> operand: the logical (zero-fill)
        /// right shift of <paramref name="value"/>. Rendered as
        /// <c>right_shift(cast(value as tinyint), cast(shift as bigint))</c> behind a typed-NULL guard
        /// (a NULL yields <c>cast(null as tinyint)</c>). Requires SQL Server 2022 (major version 16) and
        /// the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>tinyint</c> operand.</param>
        /// <param name="shift">The number of bits to shift.</param>
        /// <returns>The shifted <c>byte</c>, or SQL NULL when either argument is NULL.</returns>
        public byte? right_shift(byte? value, int? shift) => default!;

        /// <summary>
        /// <c>right_shift(value, shift)</c> for a <see cref="short"/> operand. Rendered as
        /// <c>right_shift(cast(value as smallint), cast(shift as bigint))</c> behind a typed-NULL guard
        /// (a NULL yields <c>cast(null as smallint)</c>). Requires SQL Server 2022 (major version 16)
        /// and the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>smallint</c> operand.</param>
        /// <param name="shift">The number of bits to shift.</param>
        /// <returns>The shifted <c>short</c>, or SQL NULL when either argument is NULL.</returns>
        public short? right_shift(short? value, int? shift) => default!;

        /// <summary>
        /// <c>right_shift(value, shift)</c> for an <see cref="int"/> operand. Rendered as
        /// <c>right_shift(cast(value as int), cast(shift as bigint))</c> behind a typed-NULL guard
        /// (a NULL yields <c>cast(null as int)</c>). Requires SQL Server 2022 (major version 16) and the
        /// version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>int</c> operand.</param>
        /// <param name="shift">The number of bits to shift.</param>
        /// <returns>The shifted <c>int</c>, or SQL NULL when either argument is NULL.</returns>
        public int? right_shift(int? value, int? shift) => default!;

        /// <summary>
        /// <c>right_shift(value, shift)</c> for a <see cref="long"/> operand. Rendered as
        /// <c>right_shift(cast(value as bigint), cast(shift as bigint))</c> behind a typed-NULL guard
        /// (a NULL yields <c>cast(null as bigint)</c>). Requires SQL Server 2022 (major version 16) and
        /// the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>bigint</c> operand.</param>
        /// <param name="shift">The number of bits to shift.</param>
        /// <returns>The shifted <c>long</c>, or SQL NULL when either argument is NULL.</returns>
        public long? right_shift(long? value, int? shift) => default!;

        /// <summary>
        /// <c>bit_count(value)</c> for a <see cref="byte"/> operand: the number of one bits in the value.
        /// The T-SQL result is <c>bigint</c>, so the CLR result is <c>long?</c>; the rendered call types
        /// the operand as <c>tinyint</c> and a NULL operand yields <c>cast(null as bigint)</c>. Requires
        /// SQL Server 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>tinyint</c> operand.</param>
        /// <returns>The number of set bits, or SQL NULL when the operand is NULL.</returns>
        public long? bit_count(byte? value) => default!;

        /// <summary>
        /// <c>bit_count(value)</c> for a <see cref="short"/> operand. The T-SQL result is <c>bigint</c>,
        /// so the CLR result is <c>long?</c>; the rendered call types the operand as <c>smallint</c> and
        /// a NULL operand yields <c>cast(null as bigint)</c>. Requires SQL Server 2022 (major version 16)
        /// and the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>smallint</c> operand.</param>
        /// <returns>The number of set bits, or SQL NULL when the operand is NULL.</returns>
        public long? bit_count(short? value) => default!;

        /// <summary>
        /// <c>bit_count(value)</c> for an <see cref="int"/> operand. The T-SQL result is <c>bigint</c>,
        /// so the CLR result is <c>long?</c>; the rendered call types the operand as <c>int</c> and a
        /// NULL operand yields <c>cast(null as bigint)</c>. Requires SQL Server 2022 (major version 16)
        /// and the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>int</c> operand.</param>
        /// <returns>The number of set bits, or SQL NULL when the operand is NULL.</returns>
        public long? bit_count(int? value) => default!;

        /// <summary>
        /// <c>bit_count(value)</c> for a <see cref="long"/> operand. The T-SQL result is <c>bigint</c>,
        /// so the CLR result is <c>long?</c>; the rendered call types the operand as <c>bigint</c> and a
        /// NULL operand yields <c>cast(null as bigint)</c>. Requires SQL Server 2022 (major version 16)
        /// and the version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>bigint</c> operand.</param>
        /// <returns>The number of set bits, or SQL NULL when the operand is NULL.</returns>
        public long? bit_count(long? value) => default!;

        /// <summary>
        /// <c>get_bit(value, offset)</c> for a <see cref="byte"/> operand: the bit at the zero-based
        /// <paramref name="offset"/>. The T-SQL result is <c>bit</c>, read as <c>bool?</c>; the rendered
        /// call types the operand as <c>tinyint</c>, the offset as <c>int</c>, and a NULL argument yields
        /// <c>cast(null as bit)</c>. Requires SQL Server 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>tinyint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <returns>The bit as a boolean, or SQL NULL when either argument is NULL.</returns>
        public bool? get_bit(byte? value, int? offset) => default!;

        /// <summary>
        /// <c>get_bit(value, offset)</c> for a <see cref="short"/> operand. The T-SQL result is
        /// <c>bit</c>, read as <c>bool?</c>; the rendered call types the operand as <c>smallint</c>, the
        /// offset as <c>int</c>, and a NULL argument yields <c>cast(null as bit)</c>. Requires SQL Server
        /// 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>smallint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <returns>The bit as a boolean, or SQL NULL when either argument is NULL.</returns>
        public bool? get_bit(short? value, int? offset) => default!;

        /// <summary>
        /// <c>get_bit(value, offset)</c> for an <see cref="int"/> operand. The T-SQL result is
        /// <c>bit</c>, read as <c>bool?</c>; the rendered call types the operand as <c>int</c>, the
        /// offset as <c>int</c>, and a NULL argument yields <c>cast(null as bit)</c>. Requires SQL Server
        /// 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>int</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <returns>The bit as a boolean, or SQL NULL when either argument is NULL.</returns>
        public bool? get_bit(int? value, int? offset) => default!;

        /// <summary>
        /// <c>get_bit(value, offset)</c> for a <see cref="long"/> operand. The T-SQL result is
        /// <c>bit</c>, read as <c>bool?</c>; the rendered call types the operand as <c>bigint</c>, the
        /// offset as <c>int</c>, and a NULL argument yields <c>cast(null as bit)</c>. Requires SQL Server
        /// 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>bigint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <returns>The bit as a boolean, or SQL NULL when either argument is NULL.</returns>
        public bool? get_bit(long? value, int? offset) => default!;

        /// <summary>
        /// <c>set_bit(value, offset)</c> for a <see cref="byte"/> operand: sets the bit at the zero-based
        /// <paramref name="offset"/> to the native default value 1. Rendered as
        /// <c>set_bit(cast(value as tinyint), cast(offset as int))</c> behind a typed-NULL guard (a NULL
        /// yields <c>cast(null as tinyint)</c>). Requires SQL Server 2022 (major version 16) and the
        /// version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>tinyint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <returns>The value with the bit set, or SQL NULL when either argument is NULL.</returns>
        public byte? set_bit(byte? value, int? offset) => default!;

        /// <summary>
        /// <c>set_bit(value, offset)</c> for a <see cref="short"/> operand: sets the bit at the zero-based
        /// <paramref name="offset"/> to the native default value 1. Rendered as
        /// <c>set_bit(cast(value as smallint), cast(offset as int))</c> behind a typed-NULL guard (a NULL
        /// yields <c>cast(null as smallint)</c>). Requires SQL Server 2022 (major version 16) and the
        /// version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>smallint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <returns>The value with the bit set, or SQL NULL when either argument is NULL.</returns>
        public short? set_bit(short? value, int? offset) => default!;

        /// <summary>
        /// <c>set_bit(value, offset)</c> for an <see cref="int"/> operand: sets the bit at the zero-based
        /// <paramref name="offset"/> to the native default value 1. Rendered as
        /// <c>set_bit(cast(value as int), cast(offset as int))</c> behind a typed-NULL guard (a NULL
        /// yields <c>cast(null as int)</c>). Requires SQL Server 2022 (major version 16) and the
        /// version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>int</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <returns>The value with the bit set, or SQL NULL when either argument is NULL.</returns>
        public int? set_bit(int? value, int? offset) => default!;

        /// <summary>
        /// <c>set_bit(value, offset)</c> for a <see cref="long"/> operand: sets the bit at the zero-based
        /// <paramref name="offset"/> to the native default value 1. Rendered as
        /// <c>set_bit(cast(value as bigint), cast(offset as int))</c> behind a typed-NULL guard (a NULL
        /// yields <c>cast(null as bigint)</c>). Requires SQL Server 2022 (major version 16) and the
        /// version-bound <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>bigint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <returns>The value with the bit set, or SQL NULL when either argument is NULL.</returns>
        public long? set_bit(long? value, int? offset) => default!;

        /// <summary>
        /// <c>set_bit(value, offset, value_to_set)</c> for a <see cref="byte"/> operand: sets the bit at
        /// the zero-based <paramref name="offset"/> to <paramref name="bitValue"/>. Rendered as
        /// <c>set_bit(cast(value as tinyint), cast(offset as int), cast(bitValue as bit))</c> behind a
        /// typed-NULL guard over all three arguments (a NULL yields <c>cast(null as tinyint)</c>; an
        /// explicitly NULL <paramref name="bitValue"/> yields SQL NULL rather than the native default).
        /// Requires SQL Server 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>tinyint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <param name="bitValue">The bit value to set; SQL NULL when either argument is NULL.</param>
        /// <returns>The value with the bit set, or SQL NULL when any argument is NULL.</returns>
        public byte? set_bit(byte? value, int? offset, bool? bitValue) => default!;

        /// <summary>
        /// <c>set_bit(value, offset, value_to_set)</c> for a <see cref="short"/> operand. Rendered as
        /// <c>set_bit(cast(value as smallint), cast(offset as int), cast(bitValue as bit))</c> behind a
        /// typed-NULL guard over all three arguments (a NULL yields <c>cast(null as smallint)</c>).
        /// Requires SQL Server 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>smallint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <param name="bitValue">The bit value to set; SQL NULL when either argument is NULL.</param>
        /// <returns>The value with the bit set, or SQL NULL when any argument is NULL.</returns>
        public short? set_bit(short? value, int? offset, bool? bitValue) => default!;

        /// <summary>
        /// <c>set_bit(value, offset, value_to_set)</c> for an <see cref="int"/> operand. Rendered as
        /// <c>set_bit(cast(value as int), cast(offset as int), cast(bitValue as bit))</c> behind a
        /// typed-NULL guard over all three arguments (a NULL yields <c>cast(null as int)</c>). Requires
        /// SQL Server 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>int</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <param name="bitValue">The bit value to set; SQL NULL when either argument is NULL.</param>
        /// <returns>The value with the bit set, or SQL NULL when any argument is NULL.</returns>
        public int? set_bit(int? value, int? offset, bool? bitValue) => default!;

        /// <summary>
        /// <c>set_bit(value, offset, value_to_set)</c> for a <see cref="long"/> operand. Rendered as
        /// <c>set_bit(cast(value as bigint), cast(offset as int), cast(bitValue as bit))</c> behind a
        /// typed-NULL guard over all three arguments (a NULL yields <c>cast(null as bigint)</c>).
        /// Requires SQL Server 2022 (major version 16) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2022Functions"/> gate.
        /// </summary>
        /// <param name="value">The <c>bigint</c> operand.</param>
        /// <param name="offset">The zero-based bit position.</param>
        /// <param name="bitValue">The bit value to set; SQL NULL when either argument is NULL.</param>
        /// <returns>The value with the bit set, or SQL NULL when any argument is NULL.</returns>
        public long? set_bit(long? value, int? offset, bool? bitValue) => default!;

        /// <summary>
        /// <c>TRY_CAST(value AS &lt;type&gt;)</c>: converts <paramref name="value"/> to the SQL Server type
        /// closed-mapped from <typeparamref name="T"/> and returns SQL NULL when the conversion is invalid
        /// (malformed text, overflow, an out-of-range value) instead of raising an error. The target map is
        /// fixed: <c>byte</c>→<c>tinyint</c>, <c>short</c>→<c>smallint</c>, <c>int</c>→<c>int</c>,
        /// <c>long</c>→<c>bigint</c>, <c>float</c>→<c>real</c>, <c>double</c>→<c>float(53)</c>,
        /// <c>decimal</c>→<c>decimal(38,10)</c>, <c>bool</c>→<c>bit</c>, <c>Guid</c>→<c>uniqueidentifier</c>,
        /// <c>DateTime</c>→<c>datetime2(7)</c>, <c>DateTimeOffset</c>→<c>datetimeoffset(7)</c>,
        /// <c>TimeSpan</c>→<c>time(7)</c>; any other target is refused before the command is prepared. A
        /// target conversion SQL Server itself prohibits is <b>not</b> suppressed and surfaces as the
        /// engine error. SQL Server only (declared 16, 17 and unversioned contexts); other providers
        /// reject the call.
        /// </summary>
        /// <typeparam name="T">The SQL Server conversion target.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted value, or SQL NULL when the conversion is invalid.</returns>
        public T? try_cast<T>(object? value) where T : struct => default!;

        /// <summary>
        /// <c>TRY_CAST(value AS nvarchar(4000))</c>: the string-target overload of
        /// <see cref="try_cast{T}(object?)"/>. A value longer than 4000 characters is truncated by the
        /// server, exactly like the native cast; an invalid conversion is SQL NULL.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted string, or SQL NULL when the conversion is invalid.</returns>
        public string? try_cast(object? value) => default!;

        /// <summary>
        /// <c>TRY_CONVERT(&lt;type&gt;, value)</c>: the no-style form of
        /// <see cref="try_convert{T}(object?, int?)"/>. Uses the same closed target map as
        /// <see cref="try_cast{T}(object?)"/>; an unmapped target is refused before the command is
        /// prepared, and a target conversion SQL Server prohibits surfaces as the engine error.
        /// </summary>
        /// <typeparam name="T">The SQL Server conversion target.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted value, or SQL NULL when the conversion is invalid.</returns>
        public T? try_convert<T>(object? value) where T : struct => default!;

        /// <summary>
        /// <c>TRY_CONVERT(&lt;type&gt;, value, style)</c>: the style-taking form. Omitting the argument and
        /// passing a NULL <paramref name="style"/> are separate overloads: the NULL style is emitted as a
        /// typed NULL and keeps SQL Server's native NULL-style semantics, which the engine applies as a
        /// NULL <i>result</i> even when the value itself is convertible (probe C4:
        /// <c>try_convert(int, '42', cast(null as int))</c> is NULL). The style is a
        /// <c>datetime</c>/<c>float</c>/money style, not a target-type selector.
        /// </summary>
        /// <typeparam name="T">The SQL Server conversion target.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <param name="style">
        /// The native conversion style, or SQL NULL for the engine's NULL-style result (which is SQL NULL).
        /// </param>
        /// <returns>The converted value, or SQL NULL when the conversion is invalid.</returns>
        public T? try_convert<T>(object? value, int? style) where T : struct => default!;

        /// <summary>
        /// <c>TRY_CONVERT(nvarchar(4000), value)</c>: the string-target overload without a style.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted string, or SQL NULL when the conversion is invalid.</returns>
        public string? try_convert(object? value) => default!;

        /// <summary>
        /// <c>TRY_CONVERT(nvarchar(4000), value, style)</c>: the string-target overload with a style. A
        /// NULL <paramref name="style"/> keeps SQL Server's native NULL-style semantics, which the engine
        /// applies as a NULL result even for a convertible value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="style">
        /// The native conversion style, or SQL NULL for the engine's NULL-style result (which is SQL NULL).
        /// </param>
        /// <returns>The converted string, or SQL NULL when the conversion is invalid.</returns>
        public string? try_convert(object? value, int? style) => default!;

        /// <summary>
        /// <c>product(value)</c>: the product of the non-null <paramref name="value"/> rows in the
        /// aggregate group (NULL rows are ignored). Rendered as <c>product(value)</c> in an aggregate
        /// context; no window/<c>OVER</c> expansion is offered. Requires SQL Server 2025 (major
        /// version 17) and the version-bound <see cref="ISqlDialect.SupportsProductAggregate"/> gate; a
        /// lower or unversioned declaration is refused before SQL.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to multiply across the group.</param>
        /// <returns>The product, or SQL NULL when the group has no non-null value.</returns>
        public T? product<T>(T? value) => default!;

        /// <summary>
        /// <c>approx_count_distinct(value)</c>: the approximate number of distinct non-null
        /// <paramref name="value"/> rows in the aggregate group, returned as <c>bigint</c>. An empty group
        /// and an all-NULL group both yield <c>0</c> (not SQL NULL). Rendered in an aggregate context
        /// only; the engine is not a windowing function here, so an <c>OVER</c> clause is rejected by the
        /// server (Msg 4113, observed on 17.0.5005.3). A NULL operand is rendered as a typed NULL
        /// (<c>approx_count_distinct(cast(null as int))</c>), because a bare <c>NULL</c> operand is
        /// rejected with Msg 8117. Requires SQL Server 2019 (major version 15) and the version-bound
        /// <see cref="ISqlDialect.SupportsApproxCountDistinctAggregate"/> gate; a lower or unversioned
        /// declaration is refused before SQL.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value whose distinct non-null occurrences are counted.</param>
        /// <returns>The approximate distinct count as <c>bigint</c>; <c>0</c> for an empty or all-NULL group.</returns>
        public long approx_count_distinct<T>(T? value) => default!;

        /// <summary>
        /// <c>checksum_agg(value)</c>: the checksum of the non-null <paramref name="value"/> rows in the
        /// aggregate group, returned as <c>int</c>. SQL NULL rows are skipped and the result is
        /// order-insensitive but not a stable identity (identical values cancel: two <c>5</c> rows and one
        /// <c>5</c> row checksum identically); an empty group and an all-NULL group yield SQL NULL.
        /// Rendered as <c>checksum_agg(value)</c>; unlike the approximate percentiles the engine does allow
        /// an <c>OVER</c> clause (observed on 17.0.5005.3), but this surface offers the aggregate form
        /// only. A NULL operand is rendered as a typed NULL (<c>checksum_agg(cast(null as int))</c>),
        /// because a bare <c>NULL</c> operand is rejected with Msg 8117. SQL Server has the function
        /// without a version floor, so the gate is a plain provider opt-in
        /// (<see cref="ISqlDialect.SupportsChecksumAggregate"/>).
        /// </summary>
        /// <param name="value">The value to checksum across the group.</param>
        /// <returns>The checksum as <c>int</c>, or SQL NULL when the group has no non-null value.</returns>
        public int? checksum_agg(int? value) => default!;

        /// <summary>
        /// <c>grouping_id(expressions)</c>: the bit mask identifying which of the listed expressions are
        /// aggregated away by the current grouping set, returned as <c>int</c> (<c>0</c> under a plain
        /// <c>GROUP BY</c>; the ROLLUP/GROUPING SETS subtotal bits otherwise). Every argument must be a
        /// <c>GROUP BY</c> expression: the server reports Msg 8161 without a <c>GROUP BY</c> and for an
        /// argument that is not grouped, and this library enforces the same obligation <b>before</b> SQL is
        /// executed (a missing grouping context or a mismatched argument is refused with a
        /// <see cref="BuildSqlCommandException"/> and zero statements run). Allowed only in the SELECT
        /// list, <c>HAVING</c> and <c>ORDER BY</c> of a grouped command. SQL Server has the function
        /// without a version floor, so the gate is a plain provider opt-in
        /// (<see cref="ISqlDialect.SupportsGroupingIdAggregate"/>).
        /// </summary>
        /// <param name="expressions">The grouping expressions whose subtotal bits are reported.</param>
        /// <returns>The grouping bit mask as <c>int</c>.</returns>
        public int grouping_id(params object?[] expressions) => default!;

        /// <summary>
        /// <c>approx_percentile_cont(fraction) within group (order by value)</c>: the approximate
        /// continuous (interpolated) percentile of the non-null <paramref name="value"/> rows, returned as
        /// <c>float(53)</c>. The engine accepts an integer sort expression even though the result is
        /// <c>float</c>; an empty group and an all-NULL group yield SQL NULL, and the endpoints <c>0</c>
        /// and <c>1</c> are exact. The fraction must be constant or captured and finite in <c>[0, 1]</c>;
        /// a row-dependent, NaN/infinite or out-of-range fraction is refused before SQL. The sort
        /// expression must be a column, a parameter or a computed expression: SQL Server rejects a NULL
        /// or literal constant <c>ORDER BY</c> inside <c>WITHIN GROUP</c> (Msg 5309), so such a call is
        /// refused before SQL too. The aggregate is ordered-set only: completing it with <c>OVER</c> is
        /// refused, the engine's
        /// own form being <c>WITHIN GROUP</c> without <c>OVER</c>. Requires SQL Server 2022 (major
        /// version 16) and the name-specific <see cref="ISqlDialect.SupportsApproxPercentileCont"/> gate.
        /// </summary>
        /// <typeparam name="T">The sort-input type.</typeparam>
        /// <param name="fraction">The percentile fraction, in <c>[0, 1]</c>.</param>
        /// <param name="value">The value to take the percentile of.</param>
        /// <returns>The approximate interpolated percentile, or SQL NULL when the group has no non-null value.</returns>
        public double? approx_percentile_cont<T>(double fraction, T? value) => default!;

        /// <summary>
        /// <c>approx_percentile_disc(fraction) within group (order by value)</c>: the approximate discrete
        /// percentile of the non-null <paramref name="value"/> rows, returned as the sort input type (the
        /// engine's <c>APPROX_PERCENTILE_DISC</c> result type tracks the input, e.g. <c>int</c> for an
        /// integer sort expression; a <c>decimal</c> sort expression is rejected by the engine, Msg 402).
        /// An empty group and an all-NULL group yield SQL NULL, and the endpoints <c>0</c> and <c>1</c>
        /// are exact. The fraction must be constant or captured and finite in <c>[0, 1]</c>; a
        /// row-dependent, NaN/infinite or out-of-range fraction is refused before SQL. The sort
        /// expression must be a column, a parameter or a computed expression (a NULL or literal constant
        /// <c>ORDER BY</c> is Msg 5309 and is refused before SQL). Ordered-set only: completing it with
        /// <c>OVER</c> is refused. Requires SQL Server 2022 (major version 16) and the name-specific
        /// <see cref="ISqlDialect.SupportsApproxPercentileDisc"/> gate.
        /// </summary>
        /// <typeparam name="T">The sort-input type; a value type, because the discrete result is an input value.</typeparam>
        /// <param name="fraction">The percentile fraction, in <c>[0, 1]</c>.</param>
        /// <param name="value">The value to take the percentile of.</param>
        /// <returns>The approximate discrete percentile, or SQL NULL when the group has no non-null value.</returns>
        public T? approx_percentile_disc<T>(double fraction, T? value) where T : struct => default!;

        /// <summary>
        /// <c>regexp_split_to_table(input, pattern)</c> as a FROM source: splits
        /// <paramref name="input"/> on the regular expression <paramref name="pattern"/> and returns one
        /// row per fragment with the row shape
        /// <see cref="SqlFunctions.ISqlServerRegexpSplitToTableRow"/> (<c>value varchar(n)</c>,
        /// <c>ordinal bigint</c>). Use through <c>ctx.From(() =&gt; ...)</c>. Requires SQL Server 2025
        /// (major version 17, see <see cref="ISqlDialect.SupportsSqlServer2025Functions"/>) <b>and</b>
        /// database compatibility level 170; at 160 the engine raises Msg 208 for the missing function.
        /// </summary>
        /// <param name="input">The string to split.</param>
        /// <param name="pattern">The regular expression delimiter.</param>
        public TvfEntityBuilder<SqlFunctions.ISqlServerRegexpSplitToTableRow> regexp_split_to_table(string? input, string? pattern)
            => new("regexp_split_to_table");

        /// <summary>
        /// <c>regexp_matches(input, pattern)</c> as a FROM source: returns one row per non-overlapping
        /// match with the row shape <see cref="SqlFunctions.ISqlServerRegexpMatchRow"/>
        /// (<c>match_id bigint</c>, <c>start_position int</c>, <c>end_position int</c>,
        /// <c>match_value varchar(n)</c>, <c>substring_matches varchar(max)</c>); a string without a match
        /// produces zero rows. Use through <c>ctx.From(() =&gt; ...)</c>. Requires SQL Server 2025 (major
        /// version 17, see <see cref="ISqlDialect.SupportsSqlServer2025Functions"/>) <b>and</b> database
        /// compatibility level 170: at 160 the engine raises Msg 208 for the missing function, a runtime
        /// prerequisite the dialect cannot inspect.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        public TvfEntityBuilder<SqlFunctions.ISqlServerRegexpMatchRow> regexp_matches(string? input, string? pattern)
            => new("regexp_matches");

        /// <summary>
        /// <c>regexp_matches(input, pattern, match_type)</c>: the flag-taking form (<c>c</c>, <c>i</c>,
        /// <c>s</c> and <c>m</c>). A supplied NULL match type returns zero rows; invalid flags are the
        /// engine's Msg 19303. The row shape and the compatibility-170 prerequisite are those of
        /// <see cref="regexp_matches(string?, string?)"/>.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="pattern">The regular expression.</param>
        /// <param name="match_type">The native match-type flags, or SQL NULL.</param>
        public TvfEntityBuilder<SqlFunctions.ISqlServerRegexpMatchRow> regexp_matches(
            string? input, string? pattern, string? match_type)
            => new("regexp_matches");

        /// <summary>
        /// <c>edit_distance(input, pattern)</c>: the Levenshtein edit distance between the two strings,
        /// or SQL NULL when either operand is NULL. Rendered as <c>edit_distance(input, pattern)</c>.
        /// Requires SQL Server 2025 (major version 17) and the version-bound
        /// <see cref="ISqlDialect.SupportsSqlServer2025Functions"/> gate. The major-17 gate does not
        /// promise the runtime prerequisite: the function additionally needs database compatibility level
        /// 170, <c>PREVIEW_FEATURES = ON</c> and a Windows collation (a <c>SQL_*</c> collation raises
        /// Msg 9847); those are observed environment facts, documented and asserted live, not modelled by
        /// the version flag.
        /// </summary>
        /// <param name="input">The first string.</param>
        /// <param name="pattern">The second string.</param>
        /// <returns>The edit distance as <c>int</c>, or SQL NULL when either operand is NULL.</returns>
        public int? edit_distance(string? input, string? pattern) => default!;

        /// <summary>
        /// <c>edit_distance(input, pattern, maximum_distance)</c>: the bounded form. The native result is
        /// the true distance when it is at most <paramref name="maximum_distance"/>, and
        /// <c>maximum_distance + 1</c> otherwise (observed: <c>0</c> for equal strings, <c>1</c> for a
        /// positive distance capped at <c>0</c>, <c>2</c> capped at <c>1</c>); a negative maximum is
        /// treated as uncapped by the engine. Omitting the argument and passing a NULL maximum are
        /// distinct: a supplied NULL yields SQL NULL. The argument is passed through as an <c>int</c>;
        /// a wider CLR operand is refused before SQL rather than truncated.
        /// </summary>
        /// <param name="input">The first string.</param>
        /// <param name="pattern">The second string.</param>
        /// <param name="maximum_distance">The native distance cap, or SQL NULL.</param>
        /// <returns>The bounded distance as <c>int</c>, or SQL NULL when an operand is NULL.</returns>
        public int? edit_distance(string? input, string? pattern, int? maximum_distance) => default!;

        /// <summary>
        /// <c>edit_distance_similarity(input, pattern)</c>: the edit-distance similarity as an
        /// <c>int</c> in <c>0..100</c> (<c>57</c> for <c>kitten</c>/<c>sitting</c> on the tested build),
        /// or SQL NULL when either operand is NULL. Requires SQL Server 2025 (major version 17) and the
        /// same runtime prerequisites as <see cref="edit_distance(string?, string?)"/> (compatibility 170,
        /// <c>PREVIEW_FEATURES = ON</c>, Windows collation).
        /// </summary>
        /// <param name="input">The first string.</param>
        /// <param name="pattern">The second string.</param>
        /// <returns>The similarity as <c>int</c>, or SQL NULL when either operand is NULL.</returns>
        public int? edit_distance_similarity(string? input, string? pattern) => default!;

        /// <summary>
        /// <c>jaro_winkler_distance(input, pattern)</c>: the Jaro-Winkler distance as <c>float(53)</c>
        /// (<c>0.0388888888888862</c> for <c>martha</c>/<c>marhta</c> on the tested build), or SQL NULL
        /// when either operand is NULL. Requires SQL Server 2025 (major version 17) and the same runtime
        /// prerequisites as <see cref="edit_distance(string?, string?)"/>.
        /// </summary>
        /// <param name="input">The first string.</param>
        /// <param name="pattern">The second string.</param>
        /// <returns>The distance as <c>double</c>, or SQL NULL when either operand is NULL.</returns>
        public double? jaro_winkler_distance(string? input, string? pattern) => default!;

        /// <summary>
        /// <c>jaro_winkler_similarity(input, pattern)</c>: the Jaro-Winkler similarity as an <c>int</c> in
        /// <c>0..100</c> (<c>96</c> for <c>martha</c>/<c>marhta</c> on the tested build), or SQL NULL when
        /// either operand is NULL. Requires SQL Server 2025 (major version 17) and the same runtime
        /// prerequisites as <see cref="edit_distance(string?, string?)"/>.
        /// </summary>
        /// <param name="input">The first string.</param>
        /// <param name="pattern">The second string.</param>
        /// <returns>The similarity as <c>int</c>, or SQL NULL when either operand is NULL.</returns>
        public int? jaro_winkler_similarity(string? input, string? pattern) => default!;
    }
