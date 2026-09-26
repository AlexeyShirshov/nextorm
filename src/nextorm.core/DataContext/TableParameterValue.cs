using System.Text.Json;

namespace NextORM.Core;

/// <summary>
/// The value of a table-valued parameter: a sequence of rows plus the CLR type of each row. It is held
/// by <see cref="ProcedureParameter.Value"/> and never bound by the provider directly — each provider's
/// <c>CreateProcedureParameter</c> hook translates it (SQL Server streams <c>SqlDataRecord</c>s, other
/// providers pass a JSON document or a typed array).
/// <para>
/// A row type is either a <b>scalar</b> (a primitive, <see cref="string"/>, <see cref="decimal"/>,
/// <see cref="Guid"/>, <see cref="DateTime"/>/<see cref="DateTimeOffset"/>/<see cref="DateOnly"/>/
/// <see cref="TimeOnly"/>, <see cref="TimeSpan"/>, <c>byte[]</c>, an enum or a nullable of these)
/// mapping to one column, or a
/// <b>mapped entity</b> whose columns come from the entity metadata (every non-computed mapped
/// property, in metadata order, including identity columns; use the same <c>[SqlTable]</c>/fluent
/// mapping as a bulk insert). A <see cref="Range{T}"/> property (two columns) is not supported.
/// </para>
/// <para>
/// The type is <b>not user-extensible</b>: the constructor is <c>private protected</c> and instances are
/// created only through the <c>ProcedureParameter.Table&lt;T&gt;</c> factories. The rows are enumerated
/// once per provider enumeration; do not mutate or dispose the source between building the descriptor and
/// executing the command.
/// </para>
/// </summary>
public abstract class TableParameterValue
{
    /// <summary>Creates a table parameter value for the given row type and optional known count.</summary>
    /// <param name="rowType">The CLR type of each row.</param>
    /// <param name="count">The known row count, or <see langword="null"/> for a lazy source.</param>
    private protected TableParameterValue(Type rowType, int? count)
    {
        ArgumentNullException.ThrowIfNull(rowType);
        RowType = rowType;
        Count = count;
    }

    /// <summary>The CLR type of each row (the entity type or the scalar element type).</summary>
    public Type RowType { get; }

    /// <summary>
    /// The number of rows when the source sequence is a known-size collection
    /// (<see cref="ICollection{T}"/>/<see cref="IReadOnlyCollection{T}"/>), or <see langword="null"/> for
    /// a lazy/one-shot source. Providers use it to bind an empty table-valued parameter without consuming
    /// the sequence.
    /// </summary>
    public int? Count { get; }

    /// <summary>
    /// Whether the row type maps to a single column (a scalar) rather than to a mapped entity. A scalar
    /// set has one synthetic column named <c>Value</c>.
    /// </summary>
    public bool IsScalar => TableParameterBinder.IsScalarRowType(RowType);

    /// <summary>
    /// The rows, boxed. Providers enumerate this; a fresh enumeration is created per
    /// <see cref="IEnumerable{T}.GetEnumerator"/> call when the underlying source supports it.
    /// </summary>
    public abstract IEnumerable<object> Rows { get; }

    /// <summary>
    /// Resolves the columns bound by the provider: one synthetic <c>Value</c> column for a scalar row
    /// type, or the entity's non-computed mapped columns (in metadata order, including identity columns)
    /// for an entity. The column's CLR type is the converter's provider type when a value converter is
    /// mapped, an enum is exposed as its underlying numeric type and, on a provider without a native
    /// duration type, a <see cref="TimeSpan"/> as the integer type it is stored as.
    /// </summary>
    /// <param name="context">The context supplying the naming convention and dialect.</param>
    /// <returns>The columns, in binding order.</returns>
    /// <exception cref="NotSupportedException">A property maps a <see cref="Range{T}"/> to two columns, the entity has no mapped column, or a property maps through a value converter to <see cref="TimeSpan"/> on a provider without a native duration type.</exception>
    /// <exception cref="InvalidOperationException">A column declares a decimal precision/scale whose bound provider type is not decimal, or as a partial pair.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A column declares a decimal precision/scale outside the allowed range (precision 1..38, scale 0..precision).</exception>
    public IReadOnlyList<TableParameterColumn> GetColumns(DataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TableParameterBinder.BuildColumns(this, context);
    }

    /// <summary>
    /// Serializes the parameter as a JSON array: an array of scalar values for a scalar row type, or an
    /// array of objects keyed by the mapped column name for an entity. Nulls become JSON <c>null</c>,
    /// dates and times are ISO-8601 strings, <c>byte[]</c> is base64, enums are their underlying number,
    /// and a duration on a provider without a native duration type is its stored integer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="context">The context supplying the naming convention and dialect.</param>
    /// <exception cref="NotSupportedException">A row holds a value with no JSON representation (<c>NaN</c>/<c>±∞</c>) or an unsupported CLR type.</exception>
    public void WriteJson(Utf8JsonWriter writer, DataContext context)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(context);
        TableParameterBinder.WriteJson(this, writer, context);
    }

    /// <summary>Serializes the parameter as a JSON array and returns the UTF-8 document as a string.</summary>
    /// <param name="context">The context supplying the naming convention and dialect.</param>
    /// <returns>The UTF-8 JSON document as a string.</returns>
    /// <exception cref="NotSupportedException">A row holds a value with no JSON representation (<c>NaN</c>/<c>±∞</c>) or an unsupported CLR type.</exception>
    public string WriteJson(DataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TableParameterBinder.WriteJson(this, context);
    }

    /// <summary>
    /// Builds a typed one-dimensional array from a scalar table-valued parameter, for a provider that
    /// binds a scalar set as a native array (PostgreSQL, ClickHouse). The element type is the row type's
    /// provider storage type: a <see cref="sbyte"/> widens to <see cref="short"/>, <see cref="ushort"/> to
    /// <see cref="int"/>, <see cref="uint"/> to <see cref="long"/>, <see cref="ulong"/> to
    /// <see cref="decimal"/> and <see cref="char"/> to <see cref="string"/>; an enum is its underlying
    /// number and, on a provider without a native duration type, a <see cref="TimeSpan"/> is its integer
    /// storage value (the same representation the entity path binds). Nullability mirrors the row type: a
    /// nullable row type produces a nullable element array so NULL rows survive, a non-nullable one a
    /// non-nullable array. Intended for a scalar row type only.
    /// </summary>
    /// <param name="context">The context supplying the dialect.</param>
    /// <returns>The typed array.</returns>
    public Array ToArray(DataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TableParameterBinder.ToTypedArray(this, context);
    }
}

/// <summary>
/// One column of a <see cref="TableParameterValue"/> as resolved for binding: its provider column name,
/// the CLR type bound for it (converter/duration/enum aware) and a getter that reads and normalizes the
/// value from a row.
/// </summary>
public sealed class TableParameterColumn
{
    private readonly Func<object, object?> _getValue;

    internal TableParameterColumn(string name, Type clrType, Func<object, object?> getValue, int? decimalPrecision = null, int? decimalScale = null)
    {
        Name = name;
        ClrType = clrType;
        DecimalPrecision = decimalPrecision;
        DecimalScale = decimalScale;
        _getValue = getValue;
    }

    /// <summary>The provider column name (naming convention applied to auto names).</summary>
    public string Name { get; }

    /// <summary>
    /// The CLR type bound for the column: the value converter's provider type when one is mapped, the
    /// underlying numeric type for an enum and, on a provider without a native duration type, the
    /// integer type a <see cref="TimeSpan"/> is stored as; otherwise the property type.
    /// </summary>
    public Type ClrType { get; }

    /// <summary>
    /// The decimal precision declared for the mapped property (the total number of digits), or
    /// <see langword="null"/> when the column is not a decimal or the property declares none. A provider
    /// that needs a column precision (for example the table-valued parameter column metadata of SQL
    /// Server and ClickHouse) uses it; otherwise the provider default applies.
    /// </summary>
    public int? DecimalPrecision { get; }

    /// <summary>
    /// The decimal scale declared for the mapped property (the number of digits to the right of the
    /// decimal point), or <see langword="null"/> when the column is not a decimal or the property
    /// declares none.
    /// </summary>
    public int? DecimalScale { get; }

    /// <summary>Reads and normalizes the column's value from <paramref name="row"/>.</summary>
    /// <param name="row">The row (an entity instance or a boxed scalar value; a <see langword="null"/> row yields <see langword="null"/>).</param>
    /// <returns>The provider value, or <see langword="null"/>.</returns>
    public object? GetValue(object row) => _getValue(row);
}

/// <summary>
/// The concrete carrier produced by <c>ProcedureParameter.Table&lt;T&gt;(name, rows)</c> and
/// <c>ProcedureParameter.Table&lt;T&gt;(name, typeName, rows)</c>. It is internal so the only entry point
/// is the factory; providers see the non-generic <see cref="TableParameterValue"/>.
/// </summary>
/// <typeparam name="T">The CLR type of each row.</typeparam>
internal sealed class TableValuedParameter<T> : TableParameterValue
{
    private readonly IEnumerable<T> _rows;

    internal TableValuedParameter(IEnumerable<T> rows)
        : base(typeof(T), KnownCount(rows))
    {
        _rows = rows;
    }

    public override IEnumerable<object> Rows => _rows.Cast<object>();

    private static int? KnownCount(IEnumerable<T> rows)
        => rows switch
        {
            ICollection<T> collection => collection.Count,
            IReadOnlyCollection<T> collection => collection.Count,
            _ => null,
        };
}
