namespace NextORM.Core;

/// <summary>
/// Options controlling the dialect of the CSV written by
/// <c>QueryCommandExtensions.WriteCsv</c>/<c>WriteCsvAsync</c> (and the
/// <see cref="EntityBuilder{TEntity}"/> overloads).
/// </summary>
/// <remarks>
/// The output is always UTF-8 without a byte-order mark, uses RFC 4180 escaping and CRLF row
/// terminators, and formats values with the invariant culture.
/// </remarks>
public sealed class CsvStreamOptions
{
    /// <summary>The NULL marker written by default; two characters, backslash and <c>N</c>.</summary>
    public const string DefaultNullMarker = "\\N";

    /// <summary>Gets or sets a value indicating whether a header row is written before the data rows. Defaults to <c>true</c>.</summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>Gets or sets the field delimiter. Defaults to <c>','</c>.</summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>
    /// Gets or sets the literal text written for a SQL NULL. Defaults to <see cref="DefaultNullMarker"/> (<c>\N</c>).
    /// </summary>
    /// <remarks>
    /// The contract distinguishes NULL from an empty field: NULL is always written as this marker, while a
    /// non-NULL empty string (or an empty <c>byte[]</c>) is written as an empty field. Because the marker is a
    /// literal value too, a non-NULL value whose formatted text equals the marker exactly is quoted
    /// (<c>"\N"</c>) so a reader never mistakes it for NULL. The marker must be non-empty and must not contain
    /// the delimiter, a double quote, CR or LF; these combinations are rejected before any output.
    /// </remarks>
    public string NullMarker { get; set; } = DefaultNullMarker;

    /// <summary>
    /// Gets or sets a value indicating whether the Excel formula-injection guard is enabled. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// When enabled, a field whose text begins with <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> is prefixed with an
    /// apostrophe (<c>'</c>) so spreadsheet applications treat it as text rather than a formula. The guard runs
    /// after <see cref="ValueTransform"/> and before RFC 4180 escaping, so the apostrophe is part of the escaped
    /// value. It applies to the formatted text of every column, including negative numeric values.
    /// </remarks>
    public bool ExcelMode { get; set; }

    /// <summary>
    /// Gets or sets an optional per-value transform, or <see langword="null"/> (the default) to keep the box-free
    /// typed formatting path.
    /// </summary>
    /// <remarks>
    /// The transform receives every non-NULL value (boxed) and returns the text that replaces the default
    /// formatting, or <see langword="null"/> to write the NULL marker. It runs before the Excel guard and before
    /// CSV escaping, so a transform may itself introduce a formula prefix that the guard then neutralises. A SQL
    /// NULL never invokes the transform; it is always written as <see cref="NullMarker"/>.
    /// </remarks>
    public Func<object?, string?>? ValueTransform { get; set; }
}
