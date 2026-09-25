using System.Linq;

namespace NextORM.Core;

public static partial class SqlFunctions
{
    /// <summary>
    /// Surface of the SQLite-only functions (the core scalars, the JSON1 functions/operators/aggregates,
    /// the date helpers and the math-extension functions). Every member is gated by
    /// <see cref="ISqlDialect.SqliteFunctions"/>; other providers reject it with a clear message.
    /// </summary>
    public static SqliteFunctions Sqlite => default!;

    /// <summary>
    /// Row shape produced by <see cref="SqliteFunctions.json_each(string)"/> and
    /// <see cref="SqliteFunctions.json_tree(string)"/>: one row per JSON element. The <c>key</c>,
    /// <c>value</c>, <c>type</c>, <c>fullkey</c> and <c>path</c> columns are projected as text (SQLite
    /// renders an array index or a primitive value as its text form).
    /// </summary>
    public interface IJsonEachRow
    {
        /// <summary>The array index or object label of the element, or <c>null</c>.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("key")]
        string? Key { get; set; }
        /// <summary>The SQL value of the element.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("value")]
        string? Value { get; set; }
        /// <summary>The JSON type of the element (<c>object</c>, <c>array</c>, <c>string</c>, ...).</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("type")]
        string? Type { get; set; }
        /// <summary>The full path that identifies the element.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("fullkey")]
        string? FullKey { get; set; }
        /// <summary>The path to the container of the element.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("path")]
        string? Path { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqliteFunctions.json_tree(string)"/>: one row per JSON element,
    /// recursively. In addition to the <see cref="IJsonEachRow"/> columns it exposes the element
    /// <c>id</c> and the <c>parent</c> id.
    /// </summary>
    public interface IJsonTreeRow
    {
        /// <summary>The array index or object label of the element, or <c>null</c>.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("key")]
        string? Key { get; set; }
        /// <summary>The SQL value of the element.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("value")]
        string? Value { get; set; }
        /// <summary>The JSON type of the element (<c>object</c>, <c>array</c>, <c>string</c>, ...).</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("type")]
        string? Type { get; set; }
        /// <summary>The full path that identifies the element.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("fullkey")]
        string? FullKey { get; set; }
        /// <summary>The path to the container of the element.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("path")]
        string? Path { get; set; }
        /// <summary>The internal id of the element.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        long Id { get; set; }
        /// <summary>The internal id of the parent element, or <c>null</c> for the top-level element.</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("parent")]
        long? Parent { get; set; }
    }
}

/// <summary>
/// SQLite-only SQL surface: the core scalars, the JSON1 functions/operators/aggregates, the date
/// helpers and the math-extension functions. Exposed through <see cref="SqlFunctions.Sqlite"/>; every
/// member is gated by <see cref="ISqlDialect.SqliteFunctions"/> and rejected by providers that do not
/// opt in.
/// </summary>
/// <remarks>
/// The math functions require SQLite to be compiled with <c>SQLITE_ENABLE_MATH_FUNCTIONS</c>, the
/// JSON functions are built in since SQLite 3.38 (opt-in <c>SQLITE_ENABLE_JSON1</c> before that),
/// <c>jsonb</c>/<c>json_pretty</c> need 3.45/3.46, <c>unhex</c> needs 3.41 and <c>timediff</c> needs
/// 3.43. <c>soundex</c> requires the <c>SQLITE_SOUNDEX</c> compile-time option and is not present in
/// every build; the call is still rendered, and the server reports the missing function at run time.
/// </remarks>
public class SqliteFunctions : CommonFunctions
{
    /// <summary>
    /// <c>printf(format, ...)</c>: formats the arguments with a C-style <c>%</c> format string.
    /// </summary>
    public string? printf(string? format, params object?[] args) => default!;

    /// <summary><c>format(format, ...)</c>: the SQLite 3.38+ spelling of <see cref="printf"/>.</summary>
    public string? format(string? format, params object?[] args) => default!;

    /// <summary><c>hex(X)</c>: the upper-case hexadecimal rendering of the value treated as a BLOB.</summary>
    public string? hex(object? value) => default!;

    /// <summary><c>unhex(X)</c>: the BLOB decoded from the hexadecimal string <paramref name="value"/> (SQLite 3.41+).</summary>
    public byte[]? unhex(string? value) => default!;

    /// <summary><c>unhex(X, Y)</c>: like <see cref="unhex(string?)"/>, ignoring every character of <paramref name="ignored"/>.</summary>
    public byte[]? unhex(string? value, string? ignored) => default!;

    /// <summary><c>random()</c>: a pseudo-random 64-bit integer.</summary>
    public long? random() => default!;

    /// <summary><c>randomblob(N)</c>: an <paramref name="count"/>-byte BLOB of pseudo-random bytes.</summary>
    public byte[]? randomblob(int count) => default!;

    /// <summary><c>quote(X)</c>: the SQL literal of <paramref name="value"/>.</summary>
    public string? quote(object? value) => default!;

    /// <summary><c>typeof(X)</c>: the storage class of the value (<c>null</c>, <c>integer</c>, <c>real</c>, <c>text</c>, <c>blob</c>).</summary>
    public string? @typeof(object? value) => default!;

    /// <summary><c>glob(pattern, value)</c>: true when <paramref name="value"/> matches the GLOB <paramref name="pattern"/>.</summary>
    public bool? glob(string? pattern, string? value) => default!;

    /// <summary><c>unicode(X)</c>: the code point of the first character.</summary>
    public int? unicode(string? value) => default!;

    /// <summary><c>char(X1, ..., XN)</c>: a string built from the given Unicode code points.</summary>
    public string? @char(params int[] codes) => default!;

    /// <summary>
    /// <c>soundex(X)</c>: the soundex encoding of <paramref name="value"/>. Requires SQLite to be
    /// compiled with <c>SQLITE_SOUNDEX</c>.
    /// </summary>
    public string? soundex(string? value) => default!;

    /// <summary><c>octet_length(X)</c>: the number of bytes in the encoded text representation of <paramref name="value"/>.</summary>
    public int? octet_length(object? value) => default!;

    /// <summary><c>ifnull(X,Y)</c>: the first non-null argument (the two-argument <c>coalesce</c>).</summary>
    public T? ifnull<T>(T? value, T? other) => default!;

    /// <summary><c>if(condition, whenTrue, whenFalse)</c>: the SQLite 3.48+ two-argument-per-pair conditional.</summary>
    public TResult? @if<TResult>(bool condition, TResult? whenTrue, TResult? whenFalse) => default!;

    /// <summary>Arccosine, in radians. Requires the math extension.</summary>
    public double? acos(double? x) => default!;

    /// <summary>Hyperbolic arccosine. Requires the math extension.</summary>
    public double? acosh(double? x) => default!;

    /// <summary>Arcsine, in radians. Requires the math extension.</summary>
    public double? asin(double? x) => default!;

    /// <summary>Hyperbolic arcsine. Requires the math extension.</summary>
    public double? asinh(double? x) => default!;

    /// <summary>Arctangent, in radians. Requires the math extension.</summary>
    public double? atan(double? x) => default!;

    /// <summary>Arctangent of <paramref name="y"/>/<paramref name="x"/> in the correct quadrant. Requires the math extension.</summary>
    public double? atan2(double? y, double? x) => default!;

    /// <summary>Hyperbolic arctangent. Requires the math extension.</summary>
    public double? atanh(double? x) => default!;

    /// <summary>Hyperbolic cosine. Requires the math extension.</summary>
    public double? cosh(double? x) => default!;

    /// <summary>The base-10 logarithm. Requires the math extension.</summary>
    public double? log10(double? x) => default!;

    /// <summary>The base-2 logarithm. Requires the math extension.</summary>
    public double? log2(double? x) => default!;

    /// <summary>The remainder of <paramref name="x"/>/<paramref name="y"/>, also for non-integer operands. Requires the math extension.</summary>
    public double? mod(double? x, double? y) => default!;

    /// <summary>Hyperbolic sine. Requires the math extension.</summary>
    public double? sinh(double? x) => default!;

    /// <summary>Hyperbolic tangent. Requires the math extension.</summary>
    public double? tanh(double? x) => default!;

    /// <summary><c>timediff(a, b)</c>: the signed difference between two timestamps as a SQLite interval string (SQLite 3.43+).</summary>
    public string? timediff(DateTime? a, DateTime? b) => default!;

    /// <summary><c>unixepoch(value)</c>: the number of seconds since 1970-01-01 (SQLite 3.38+).</summary>
    public long? unixepoch(DateTime? value) => default!;

    /// <summary><c>julianday(value)</c>: the Julian day number of the timestamp.</summary>
    public double? julianday(DateTime? value) => default!;

    /// <summary><c>json(X)</c>: validates and minifies the JSON document.</summary>
    public string? json(object? value) => default!;

    /// <summary><c>jsonb(X)</c>: the JSONB binary representation of the document (SQLite 3.45+).</summary>
    public byte[]? jsonb(object? value) => default!;

    /// <summary>
    /// <c>json_extract(json, path)</c>: the value selected by <paramref name="path"/>, read as
    /// <typeparamref name="T"/> (a SQL scalar for a primitive element, or the JSON text for an
    /// array/object). Use <see cref="json_get"/> / <see cref="json_get_text"/> for the operator forms.
    /// </summary>
    public T? json_extract<T>(object? json, string? path) => default!;

    /// <summary><c>json -&gt; path</c>: the subcomponent selected by <paramref name="path"/> as JSON text.</summary>
    public string? json_get(object? json, string? path) => default!;

    /// <summary><c>json -&gt;&gt; path</c>: the subcomponent selected by <paramref name="path"/> as an SQL value.</summary>
    public string? json_get_text(object? json, string? path) => default!;

    /// <summary><c>json_array(value1, ...)</c>: a JSON array built from the arguments.</summary>
    public string? json_array(params object?[] values) => default!;

    /// <summary><c>json_array_insert(json, path, value, ...)</c>: inserts path/value pairs into an array.</summary>
    public string? json_array_insert(string? json, params object?[] pathValuePairs) => default!;

    /// <summary><c>json_insert(json, path, value, ...)</c>: adds the path/value pairs that do not already exist.</summary>
    public string? json_insert(string? json, params object?[] pathValuePairs) => default!;

    /// <summary><c>json_replace(json, path, value, ...)</c>: replaces the path/value pairs that already exist.</summary>
    public string? json_replace(string? json, params object?[] pathValuePairs) => default!;

    /// <summary><c>json_set(json, path, value, ...)</c>: inserts or replaces the path/value pairs.</summary>
    public string? json_set(string? json, params object?[] pathValuePairs) => default!;

    /// <summary><c>json_object(label1, value1, ...)</c>: a JSON object built from the label/value pairs.</summary>
    public string? json_object(params object?[] keyValuePairs) => default!;

    /// <summary><c>json_patch(target, patch)</c>: applies the RFC-7396 merge patch to <paramref name="target"/>.</summary>
    public string? json_patch(object? target, object? patch) => default!;

    /// <summary><c>json_pretty(json)</c>: the document with indentation (SQLite 3.46+).</summary>
    public string? json_pretty(object? json) => default!;

    /// <summary><c>json_quote(value)</c>: the JSON representation of an SQL value.</summary>
    public string? json_quote(object? value) => default!;

    /// <summary><c>json_remove(json, path, ...)</c>: the document with the selected elements removed.</summary>
    public string? json_remove(string? json, params string?[] paths) => default!;

    /// <summary><c>json_type(json)</c>: the JSON type of the outermost element.</summary>
    public string? json_type(object? json) => default!;

    /// <summary><c>json_type(json, path)</c>: the JSON type of the element selected by <paramref name="path"/>.</summary>
    public string? json_type(object? json, string? path) => default!;

    /// <summary><c>json_valid(json)</c>: true when the value is well-formed JSON.</summary>
    public bool? json_valid(object? json) => default!;

    /// <summary><c>json_valid(json, flags)</c>: true when the value is well-formed JSON under the bitmask <paramref name="flags"/>.</summary>
    public bool? json_valid(object? json, int flags) => default!;

    /// <summary><c>json_group_array(value)</c> aggregate: a JSON array of the group's values.</summary>
    public string? json_group_array<T>(T? value) => default!;

    /// <summary><c>json_group_object(label, value)</c> aggregate: a JSON object of the group's label/value pairs.</summary>
    public string? json_group_object<TKey, TValue>(TKey? key, TValue? value) => default!;

    /// <summary>
    /// <c>json_each(json)</c> as a FROM source: one row per immediate child (see
    /// <see cref="SqlFunctions.IJsonEachRow"/>).
    /// </summary>
    [SqlTableFunction("json_each")]
    public IQueryable<SqlFunctions.IJsonEachRow> json_each(string? json) => throw new NotSupportedException();

    /// <summary><c>json_each(json, path)</c> as a FROM source, walking the element selected by <paramref name="path"/>.</summary>
    [SqlTableFunction("json_each")]
    public IQueryable<SqlFunctions.IJsonEachRow> json_each(string? json, string? path) => throw new NotSupportedException();

    /// <summary>
    /// <c>json_tree(json)</c> as a FROM source: one row per element of the recursive walk (see
    /// <see cref="SqlFunctions.IJsonTreeRow"/>).
    /// </summary>
    [SqlTableFunction("json_tree")]
    public IQueryable<SqlFunctions.IJsonTreeRow> json_tree(string? json) => throw new NotSupportedException();

    /// <summary><c>json_tree(json, path)</c> as a FROM source, walking the element selected by <paramref name="path"/>.</summary>
    [SqlTableFunction("json_tree")]
    public IQueryable<SqlFunctions.IJsonTreeRow> json_tree(string? json, string? path) => throw new NotSupportedException();
}
