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
        /// <see cref="EntityBuilder{TEntity}.CrossApply{TJoinEntity}(System.Linq.Expressions.Expression{System.Func{TEntity, QueryCommand{TJoinEntity}}})"/>
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
    }
