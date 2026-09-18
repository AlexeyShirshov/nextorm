using System.Linq;
using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// SQL Server-only SQL surface: the JSON-as-text functions and the SQL Server table functions.
/// Exposed through <see cref="NORM.MS_SQL"/>; every member is gated by a capability flag and
/// rejected by providers that do not opt in.
/// </summary>
public static partial class NORM
{
    /// <summary>SQL Server-only functions; see <see cref="NORM.MS_SQL"/>.</summary>
    public class MS : NORM_SQL
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
        /// <see cref="NORM.IStringSplitRow.Value"/>. Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, Expression{Func{IQueryable{T}}})"/>.
        /// The fragments are not guaranteed to be ordered; add an <c>order by</c> on the caller side if
        /// the input order matters.
        /// </summary>
        [SqlTableFunction("string_split")]
        public IQueryable<IStringSplitRow> string_split(string? value, string? separator) => throw new NotSupportedException();

        /// <summary>
        /// <c>openjson(json)</c> as a FROM source (SQL Server 2016+); select
        /// <see cref="NORM.IOpenJsonRow.Key"/>/<see cref="NORM.IOpenJsonRow.Value"/>/
        /// <see cref="NORM.IOpenJsonRow.Type"/>. Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, Expression{Func{IQueryable{T}}})"/>.
        /// The default schema yields the properties of a JSON object or the elements of a JSON array;
        /// for a typed projection create a <c>[SqlTableFunction("openjson")]</c> wrapper whose row shape
        /// matches the <c>WITH (...)</c> clause instead.
        /// </summary>
        [SqlTableFunction("openjson")]
        public IQueryable<IOpenJsonRow> openjson(string? json) => throw new NotSupportedException();
    }
}
