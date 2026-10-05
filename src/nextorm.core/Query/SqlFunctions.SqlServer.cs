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
        /// Unlike the <c>[SqlTableFunction]</c> sources (<c>string_split</c>/<c>openjson</c>) this is a
        /// correlated source over the left-hand row's column, not a standalone table function, so it is
        /// expressed through <c>CrossApply</c>/<c>OuterApply</c> rather than
        /// <c>FromTableFunction</c>.
        /// </remarks>
        public QueryCommand<SqlFunctions.IXmlNodesRow> xml_nodes(string? xml, string? xpath) =>
            throw new NotSupportedException("xml_nodes can only be used as a CROSS/OUTER APPLY source.");

        /// <summary>
        /// <c>string_split(value, separator)</c> as a FROM source (SQL Server 2016+); select
        /// <see cref="SqlFunctions.IStringSplitRow.Value"/>. Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, Expression{Func{IQueryable{T}}})"/>.
        /// The fragments are not guaranteed to be ordered; add an <c>order by</c> on the caller side if
        /// the input order matters.
        /// </summary>
        [SqlTableFunction("string_split")]
        public IQueryable<SqlFunctions.IStringSplitRow> string_split(string? value, string? separator) => throw new NotSupportedException();

        /// <summary>
        /// <c>openjson(json)</c> as a FROM source (SQL Server 2016+); select
        /// <see cref="SqlFunctions.IOpenJsonRow.Key"/>/<see cref="SqlFunctions.IOpenJsonRow.Value"/>/
        /// <see cref="SqlFunctions.IOpenJsonRow.Type"/>. Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, Expression{Func{IQueryable{T}}})"/>.
        /// The default schema yields the properties of a JSON object or the elements of a JSON array;
        /// for a typed projection declare a <c>[SqlTableFunction("openjson")]</c> wrapper whose row
        /// shape matches the <see cref="SqlTableFunctionAttribute.WithClause"/> schema (for example
        /// <c>WithClause = "name nvarchar(50) '$.name'"</c>).
        /// </summary>
        [SqlTableFunction("openjson")]
        public IQueryable<SqlFunctions.IOpenJsonRow> openjson(string? json) => throw new NotSupportedException();

        /// <summary>
        /// <c>CONTAINSTABLE(table, column, search)</c> as a FROM source (SQL Server full-text search with
        /// ranking). Join it back to the full-text-indexed table on
        /// <see cref="SqlFunctions.IKeyRankRow{TKey}.Key"/> and project
        /// <see cref="SqlFunctions.IKeyRankRow{TKey}.Rank"/>:
        /// <c>ctx.From&lt;IDocument&gt;().Join(ctx.FromTableFunction(() =&gt; SqlFunctions.SqlServer.containstable&lt;int&gt;("documents", "title", search)), (d, k) =&gt; d.Id == k.Key).OrderByDescending(p =&gt; p.Item2.Rank)</c>.
        /// <paramref name="table"/> and <paramref name="column"/> are emitted verbatim as identifiers
        /// (the table name or its alias exactly as it appears in the query); only pass trusted values.
        /// </summary>
        [SqlTableFunction("containstable", VerbatimArguments = new[] { 0, 1 })]
        public IQueryable<SqlFunctions.IKeyRankRow<TKey>> containstable<TKey>(string table, string column, string search) => throw new NotSupportedException();

        /// <summary>
        /// <c>FREETEXTTABLE(table, column, search)</c> as a FROM source; the natural-language counterpart
        /// of <see cref="containstable{TKey}(string, string, string)"/> (same <c>KEY</c>/<c>RANK</c> columns).
        /// </summary>
        [SqlTableFunction("freetexttable", VerbatimArguments = new[] { 0, 1 })]
        public IQueryable<SqlFunctions.IKeyRankRow<TKey>> freetexttable<TKey>(string table, string column, string search) => throw new NotSupportedException();

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
        /// <c>parsename(object_name, piece)</c>: the requested 1-based part of a four-part object name
        /// (NULL when the part is not a valid identifier). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? parsename(string? objectName, int? piece) => default!;

        /// <summary>
        /// <c>publishingservername()</c>: the name of the publishing server (the server name when a
        /// replicated database is published). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SqlServerFunctions"/>; SQL Server).
        /// </summary>
        public string? publishingservername() => default!;

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
    }
