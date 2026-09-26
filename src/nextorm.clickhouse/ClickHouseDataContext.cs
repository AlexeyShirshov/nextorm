using System.Data;
using System.Data.Common;
using System.Globalization;
using ClickHouse.Driver.ADO;
using ClickHouse.Driver.ADO.Parameters;
using NextORM.Core;

namespace NextORM.ClickHouse;

/// <summary>
/// Data context for ClickHouse. Wraps the <c>ClickHouse.Driver</c> ADO.NET driver for connections
/// and parameters and renders SQL through <c>ClickHouseDialect</c>.
/// </summary>
public class ClickHouseDataContext : DataContext
{
    /// <summary>
    /// Creates a ClickHouse context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>ClickHouseConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public ClickHouseDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a ClickHouse context over a caller-supplied connection, which the context does not
    /// dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public ClickHouseDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    /// <summary>Creates a new <c>ClickHouseConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The ClickHouse connection string.</param>
    /// <returns>A new, unopened ClickHouse connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new ClickHouseConnection(connectionString);

    /// <inheritdoc/>
    public override ISqlDialect Dialect => ClickHouseDialect.Instance;

    /// <summary>
    /// Creates a <c>ClickHouseDbParameter</c>. The driver rewrites the ADO-style <c>@name</c>
    /// placeholder to <c>{name:Type}</c> and infers the ClickHouse type from the CLR value, so the
    /// stored name omits the <c>@</c> prefix.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new ClickHouse parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // The driver rewrites the ADO-style @name placeholder to {name:Type} and infers the
        // ClickHouse type from the CLR value; the stored name is the one without the @ prefix.
        return new ClickHouseDbParameter
        {
            ParameterName = name.TrimStart('@'),
            Value = value
        };
    }

    /// <summary>
    /// Creates a <c>ClickHouseDbParameter</c> for a descriptor. A <see cref="TableParameterValue"/> is
    /// emulated with a native array expanded server-side with <c>arrayJoin(@p)</c>: a scalar row type
    /// binds as an <c>Array(T)</c> and an entity row type as an <c>Array(Tuple(...))</c> whose tuple
    /// elements follow the resolved column order. A <see cref="ProcedureParameter.TypeName"/> is SQL
    /// Server only and is rejected.
    /// </summary>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new ClickHouse parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException"><see cref="ProcedureParameter.TypeName"/> is set (SQL Server only), or a table parameter is not <see cref="ParameterDirection.Input"/>.</exception>
    /// <exception cref="NotSupportedException">A table parameter column has a CLR type with no ClickHouse mapping.</exception>
    protected override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
    {
        if (parameter.Value is TableParameterValue tableValue)
            return CreateTableValuedParameter(parameter, tableValue);

        RejectTypeName(parameter);
        return base.CreateProcedureParameter(parameter);
    }

    private ClickHouseDbParameter CreateTableValuedParameter(ProcedureParameter parameter, TableParameterValue value)
    {
        RejectTypeName(parameter);
        RejectNonInputTable(parameter);

        var dbParameter = new ClickHouseDbParameter { ParameterName = parameter.Name.TrimStart('@') };

        if (value.IsScalar)
        {
            // The explicit type is required: the driver infers every element as non-nullable, so an
            // empty array or a null element (for example a null string) would otherwise be rejected
            // when the array is serialized. Core's scalar ToArray widens sbyte/ushort/uint/ulong for
            // PostgreSQL's array element types, so the declared ClickHouse type is resolved from the
            // row type's native representation and the array is narrowed back to it.
            var array = NarrowScalarArray(value.ToArray(this), value.RowType);
            dbParameter.ClickHouseType = $"Array({ColumnTypeName(ScalarStorageType(value.RowType))})";
            dbParameter.Value = FormatTimeOnlyElements(array);
            return dbParameter;
        }

        var columns = value.GetColumns(this);
        dbParameter.Value = BuildRows(value, columns);
        dbParameter.ClickHouseType =
            $"Array(Tuple({string.Join(", ", columns.Select(c => ColumnTypeName(c.ClrType, c.DecimalPrecision, c.DecimalScale)))}))";
        return dbParameter;
    }

    // Core's scalar ToArray widens sbyte -> short, ushort -> int, uint -> long and ulong -> decimal for
    // PostgreSQL's array element types. ClickHouse binds those natively, so the bound array is narrowed
    // back; nullability is preserved and every other element type is left untouched.
    private static Array NarrowScalarArray(Array array, Type rowType)
    {
        var underlying = Nullable.GetUnderlyingType(rowType) ?? rowType;
        var target = underlying switch
        {
            _ when underlying == typeof(sbyte) => typeof(sbyte),
            _ when underlying == typeof(ushort) => typeof(ushort),
            _ when underlying == typeof(uint) => typeof(uint),
            _ when underlying == typeof(ulong) => typeof(ulong),
            _ => null,
        };

        if (target is null || target == array.GetType().GetElementType())
            return array;

        var elementType = Nullable.GetUnderlyingType(array.GetType().GetElementType()!) is not null
            ? typeof(Nullable<>).MakeGenericType(target)
            : target;

        var result = Array.CreateInstance(elementType, array.Length);
        for (var i = 0; i < array.Length; i++)
        {
            var element = array.GetValue(i);
            result.SetValue(element is null ? null : Convert.ChangeType(element, target, CultureInfo.InvariantCulture), i);
        }

        return result;
    }

    // The CLR type a scalar row is represented by for ClickHouse: an enum as its underlying number and a
    // TimeSpan as its stored Int64 duration unit (ticks by default; ClickHouse has no native duration
    // type). The sbyte/ushort/uint/ulong native types and char (mapped to String) are kept as-is;
    // nullability is preserved.
    private static Type ScalarStorageType(Type rowType)
    {
        var underlying = Nullable.GetUnderlyingType(rowType) ?? rowType;

        if (underlying.IsEnum)
            underlying = Enum.GetUnderlyingType(underlying);
        else if (underlying == typeof(TimeSpan))
            underlying = typeof(long);

        return Nullable.GetUnderlyingType(rowType) is not null && underlying.IsValueType
            ? typeof(Nullable<>).MakeGenericType(underlying)
            : underlying;
    }

    // Each row is a boxed object[] whose ordinal values map to the resolved columns. The driver
    // serializes it as a ClickHouse tuple under the explicit Array(Tuple(...)) type. Null is kept as a
    // null element so a nullable column round-trips (the matching column type is Nullable(...)).
    private static object?[][] BuildRows(TableParameterValue value, IReadOnlyList<TableParameterColumn> columns)
    {
        var rows = new List<object?[]>(value.Count ?? 0);

        foreach (var row in value.Rows)
        {
            var values = new object?[columns.Count];
            for (var i = 0; i < columns.Count; i++)
                values[i] = FormatTimeOnly(columns[i].GetValue(row));

            rows.Add(values);
        }

        return rows.ToArray();
    }

    // ClickHouse has no TimeOnly CLR mapping: the binder keeps the property type (TimeOnly) and the
    // column type name is String, so the value is formatted invariantly with full subsecond precision
    // (HH:mm:ss.fffffff) instead of letting the driver bind a type it cannot serialize.
    private static object? FormatTimeOnly(object? value)
        => value is TimeOnly time ? time.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture) : value;

    // Formats a scalar array whose element type is TimeOnly/TimeOnly? to the matching String element
    // array; a null element stays null. Any other element type is returned unchanged.
    private static Array FormatTimeOnlyElements(Array array)
    {
        var elementType = array.GetType().GetElementType()!;
        if ((Nullable.GetUnderlyingType(elementType) ?? elementType) != typeof(TimeOnly))
            return array;

        var result = new string?[array.Length];
        for (var i = 0; i < array.Length; i++)
            result[i] = (string?)FormatTimeOnly(array.GetValue(i));

        return result;
    }

    // Maps a bound column's CLR type to a ClickHouse type name. Reference types and Nullable<T> are
    // wrapped in Nullable(...) because ClickHouse types are not nullable by default. Both paths pass the
    // provider storage type the value was normalized to (a TimeSpan is already its Int64 stored duration
    // unit).
    private static string ColumnTypeName(Type type, int? decimalPrecision = null, int? decimalScale = null)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsEnum)
            underlying = Enum.GetUnderlyingType(underlying);

        var name = ClickHouseTypeName(underlying, decimalPrecision, decimalScale);
        return type.IsValueType && Nullable.GetUnderlyingType(type) is null ? name : $"Nullable({name})";
    }

    private static string ClickHouseTypeName(Type type, int? decimalPrecision = null, int? decimalScale = null) => type switch
    {
        _ when type == typeof(sbyte) => "Int8",
        _ when type == typeof(byte) => "UInt8",
        _ when type == typeof(short) => "Int16",
        _ when type == typeof(ushort) => "UInt16",
        _ when type == typeof(int) => "Int32",
        _ when type == typeof(uint) => "UInt32",
        _ when type == typeof(long) => "Int64",
        _ when type == typeof(ulong) => "UInt64",
        _ when type == typeof(float) => "Float32",
        _ when type == typeof(double) => "Float64",
        // A declared precision/scale is honoured; otherwise ClickHouse's historical TVP default applies.
        _ when type == typeof(decimal) => decimalPrecision is { } precision
            ? $"Decimal({precision}, {decimalScale ?? 0})"
            : "Decimal(38, 10)",
        _ when type == typeof(bool) => "Bool",
        _ when type == typeof(string) => "String",
        _ when type == typeof(char) => "String",
        _ when type == typeof(Guid) => "UUID",
        _ when type == typeof(DateTime) => "DateTime",
        _ when type == typeof(DateTimeOffset) => "DateTime64(6)",
        _ when type == typeof(DateOnly) => "Date",
        _ when type == typeof(TimeOnly) => "String",
        _ when type == typeof(TimeSpan) => "Int64",
        _ when type == typeof(byte[]) => "String",
        _ => throw new NotSupportedException(
            $"ClickHouse table-valued parameter cannot bind a column of type {type.Name}.")
    };

    private static void RejectTypeName(ProcedureParameter parameter)
    {
        if (parameter.TypeName is not null)
            throw new ArgumentException(
                $"TypeName is SQL Server only, but '{parameter.Name}' has TypeName = '{parameter.TypeName}'. "
                + "On ClickHouse pass the rows to ProcedureParameter.Table(name, rows): a scalar set binds as an array and an entity set as an array of tuples.",
                nameof(parameter));
    }

    private static void RejectNonInputTable(ProcedureParameter parameter)
    {
        if (parameter.Direction != ParameterDirection.Input)
            throw new ArgumentException(
                $"A table-valued parameter is input-only, but '{parameter.Name}' has Direction = {parameter.Direction}.",
                nameof(parameter));
    }
}
