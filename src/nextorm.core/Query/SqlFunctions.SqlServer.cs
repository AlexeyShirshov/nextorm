using System.Linq;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Text-JSON surface (SQL Server and MySQL/MariaDB), the SQL Server-only <c>choose</c> conditional
/// function (the portable <c>iif</c> lives on <see cref="CommonFunctions"/>) and the SQL Server table
/// functions. Exposed through <see cref="SqlFunctions.SqlServer"/>; every member is gated by a
/// capability flag and rejected by providers that do not opt in.
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
        /// for a typed projection create a <c>[SqlTableFunction("openjson")]</c> wrapper whose row shape
        /// matches the <c>WITH (...)</c> clause instead.
        /// </summary>
        [SqlTableFunction("openjson")]
        public IQueryable<SqlFunctions.IOpenJsonRow> openjson(string? json) => throw new NotSupportedException();

        /// <summary>
        /// <c>choose(index, value, ...)</c>: the 1-based <paramref name="index"/>-th value (NULL when out
        /// of range). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsChoose"/>; SQL Server).
        /// </summary>
        public TResult? choose<TResult>(int index, params TResult?[] values) => default!;
    }
