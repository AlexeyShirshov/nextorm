using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using ClickHouse.Driver.ADO.Parameters;
using NextORM.ClickHouse.Bulk;
using NextORM.Core;

namespace NextORM.ClickHouse;

/// <summary>
/// Data context for ClickHouse. Wraps the <c>ClickHouse.Driver</c> ADO.NET driver for connections
/// and parameters and renders SQL through <c>ClickHouseDialect</c>.
/// </summary>
/// <remarks>
/// The bulk-insert executor is re-implemented here (the base hook cannot carry the whole-invocation
/// budget or the explicit option presence) so <see cref="BulkInsertMode"/> resolves against the native
/// <c>InsertBinaryAsync</c> prerequisites, the session/parallelism request is validated before a row is
/// read, and the portable path is entered only for the equivalent single-stream request.
/// </remarks>
public class ClickHouseDataContext : DataContext, IBulkInsertExecutor, IBulkInsertNativeOptions, IBulkInsertPortableScope
{
    private static readonly MethodInfo GetFieldValueMI = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!;
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo ToJsonDocumentMI = typeof(ClickHouseDataContext).GetMethod(nameof(ToJsonDocument), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ToJsonElementMI = typeof(ClickHouseDataContext).GetMethod(nameof(ToJsonElement), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ToDateTimeOffsetMI = typeof(ClickHouseDataContext).GetMethod(nameof(ToDateTimeOffset), BindingFlags.NonPublic | BindingFlags.Static)!;

    // The settings of a client/data-source-built context: the context creates its own connection from a
    // copy so the ADO connection and the client share one actual ClickHouse session id. Null for the
    // connection-string and caller-connection constructors, where the connection itself is the source.
    private readonly ClickHouseClientSettings? _settings;

    // A caller-supplied client (directly or through its data source) is owned by the caller and never
    // disposed by the context; when none was supplied the context owns the client it derives from the
    // connection settings and disposes it exactly once.
    private readonly IClickHouseClient? _providedClient;
    private ClickHouseClient? _ownedClient;
    private int _ownedClientDisposalCount;

    // The ALIAS/MATERIALIZED columns of a destination, cached per (database, table): the column kind lives
    // only in the catalog, and a bulk insert must not pay a catalog round trip per call. Metadata is read
    // once per target for the lifetime of the context.
    private readonly Dictionary<string, IReadOnlyList<string>> _nonWritableColumns = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a ClickHouse context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>ClickHouseConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public ClickHouseDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : this(connectionString, null, optionsBuilder, null)
    {
    }

    /// <summary>
    /// Creates a ClickHouse context bound to an explicitly declared server version that owns a
    /// connection built lazily from <paramref name="connectionString"/>. The version is a caller
    /// declaration (there is no server probing): it enables the capabilities introduced after the
    /// pinned baseline (currently the native <c>arrayExcept</c>, ClickHouse 25.9).
    /// </summary>
    /// <param name="serverVersion">The ClickHouse server version this context declares.</param>
    /// <param name="connectionString">The connection string used to create the <c>ClickHouseConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serverVersion"/> is <see langword="null"/>; use the unversioned constructor instead.</exception>
    /// <exception cref="InvalidOperationException">A different capability declaration was already registered for this context type.</exception>
    public ClickHouseDataContext(Version serverVersion, string connectionString, DataContextBuilder optionsBuilder)
        : this(connectionString, null, optionsBuilder, ValidateServerVersion(serverVersion))
    {
    }

    /// <summary>
    /// Creates a ClickHouse context over a caller-supplied connection, which the context does not
    /// dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public ClickHouseDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : this(null, connection, optionsBuilder, null)
    {
    }

    /// <summary>
    /// Creates a ClickHouse context bound to an explicitly declared server version over a
    /// caller-supplied connection, which the context does not dispose.
    /// </summary>
    /// <param name="serverVersion">The ClickHouse server version this context declares.</param>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serverVersion"/> is <see langword="null"/>; use the unversioned constructor instead.</exception>
    /// <exception cref="InvalidOperationException">A different capability declaration was already registered for this context type.</exception>
    public ClickHouseDataContext(Version serverVersion, DbConnection connection, DataContextBuilder optionsBuilder)
        : this(null, connection, optionsBuilder, ValidateServerVersion(serverVersion))
    {
    }

    /// <summary>
    /// Creates a ClickHouse context over a caller-supplied client, which the context does not dispose.
    /// The context's own connection is built from a copy of the client's settings, so both share the same
    /// actual session id and every other setting; the caller's settings object is never mutated. This
    /// (like the data-source overload) is an unversioned construction path: a versioned capability must
    /// be declared through the versioned connection-string or connection constructor.
    /// </summary>
    /// <param name="client">An already-created <c>ClickHouseClient</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public ClickHouseDataContext(ClickHouseClient client, DataContextBuilder optionsBuilder)
        : this(null, null, optionsBuilder, null)
    {
        ArgumentNullException.ThrowIfNull(client);

        _providedClient = client;
        _settings = new ClickHouseClientSettings(client.Settings);
    }

    /// <summary>
    /// Creates a ClickHouse context over a caller-supplied data source, which the context does not
    /// dispose: the pooled client is obtained from the data source and its settings are copied for the
    /// context's own connection, never mutated. This is an unversioned construction path.
    /// </summary>
    /// <param name="dataSource">An already-created <c>ClickHouseDataSource</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public ClickHouseDataContext(ClickHouseDataSource dataSource, DataContextBuilder optionsBuilder)
        : this(null, null, optionsBuilder, null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        _providedClient = dataSource.GetClient();
        _settings = new ClickHouseClientSettings(dataSource.Settings);
    }

    // The single construction path: declares the capability, then selects the shared dialect for an
    // undeclared context and a cached versioned dialect otherwise. The declared version reaches the base
    // through ServerVersion, exactly like the SQL Server provider.
    private ClickHouseDataContext(
        string? connectionString,
        DbConnection? connection,
        DataContextBuilder optionsBuilder,
        Version? serverVersion)
        : base(connectionString, connection, optionsBuilder, serverVersion)
    {
        RegisterCapabilityDeclaration(GetType(), ServerVersion);
        _dialect = ServerVersion is null
            ? ClickHouseDialect.Instance
            : VersionedDialects.GetOrAdd(ServerVersion, static version => new ClickHouseDialect(version));
    }

    private static Version ValidateServerVersion(Version serverVersion)
    {
        ArgumentNullException.ThrowIfNull(serverVersion);
        return serverVersion;
    }

    // A context type owns one immutable capability declaration: the plan cache is keyed by the context
    // type, so two declarations would silently share cached SQL. A second, conflicting declaration is a
    // configuration error that names the remedy: derive a distinct context subclass. "Unspecified" and any
    // concrete version are different declarations. Validation runs before the dialect is selected and
    // before any query is prepared.
    private static void RegisterCapabilityDeclaration(Type contextType, Version? serverVersion)
    {
        var candidate = new CapabilityDeclaration(serverVersion);
        var registered = CapabilitiesByContextType.GetOrAdd(contextType, candidate);

        if (registered.Equals(candidate))
            return;

        throw new InvalidOperationException(
            $"The ClickHouse context type '{contextType.Name}' is already bound to server version " +
            $"{DescribeServerVersion(registered.ServerVersion)}, so it cannot also be used with " +
            $"{DescribeServerVersion(candidate.ServerVersion)}. A context type has one immutable server " +
            "version because the plan cache is keyed by the context type; derive a distinct DataContext " +
            "subclass for each version.");
    }

    private static string DescribeServerVersion(Version? serverVersion)
        => serverVersion is null ? "unset (null)" : serverVersion.ToString();

    /// <summary>An immutable capability declaration: the caller's server version, or unset.</summary>
    private readonly record struct CapabilityDeclaration(Version? ServerVersion);

    /// <summary>One versioned dialect per declared server version, shared by every context type that declares it.</summary>
    private static readonly ConcurrentDictionary<Version, ClickHouseDialect> VersionedDialects = new();

    /// <summary>One capability declaration per concrete context type (the plan-cache key).</summary>
    private static readonly ConcurrentDictionary<Type, CapabilityDeclaration> CapabilitiesByContextType = new();

    private readonly ISqlDialect _dialect;

    /// <summary>
    /// Creates the context's own <c>ClickHouseConnection</c>: from the connection string when the context
    /// was built with one, otherwise from a copy of the supplied client/data-source settings so the
    /// connection and the client address the same database and session.
    /// </summary>
    /// <param name="connectionString">The connection string, or <see langword="null"/> when settings were supplied.</param>
    /// <returns>A new, unopened ClickHouse connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => _settings is null
            ? new ClickHouseConnection(connectionString)
            : new ClickHouseConnection(new ClickHouseClientSettings(_settings));

    /// <inheritdoc/>
    public override ISqlDialect Dialect => _dialect;

    /// <summary>
    /// ClickHouse has no time-of-day type: the binder stores a <see cref="TimeOnly"/> as <c>String</c>, so a
    /// mapped projection would have to parse the text back. The projection is refused and a value
    /// converter must opt in explicitly.
    /// </summary>
    protected override string? TimeOnlyProjectionRefusal =>
        "TimeOnly projection refused for ClickHouse: there is no native time-of-day type and TimeOnly is bound as String; use an explicit value converter.";

    /// <summary>
    /// Materializes a projected native ClickHouse <c>JSON</c> column. A bare <see cref="JsonObject"/> is
    /// read as-is; a bare <see cref="JsonDocument"/>/<see cref="JsonElement"/> is adapted from the driver's
    /// object-root <see cref="JsonObject"/> transport. A SQL <c>NULL</c> becomes a null reference (or
    /// <see langword="default"/> for the value-type <see cref="JsonElement"/>), which stays distinct from an
    /// empty object. A <see cref="DateTimeOffset"/> projection is adapted from the driver's
    /// <see cref="DateTime"/> transport (see <see cref="MapDateTimeOffsetColumn"/>). Every other column is
    /// mapped by the base implementation, which also resolves a <c>[JsonColumn]</c> converter (its provider
    /// type is <see cref="JsonObject"/> on ClickHouse).
    /// </summary>
    /// <param name="column">The projected column being read.</param>
    /// <param name="param">The data-reader expression the accessor is built from.</param>
    /// <returns>An expression that reads the column value.</returns>
    public override Expression MapColumnExpression(SelectExpression column, Expression param)
    {
        var realType = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;
        // A converter (for example [JsonColumn]) keeps its own storage policy; the native-mapping
        // shortcut applies to a bare JSON projection only.
        if (column.Converter is null
            && (realType == typeof(JsonObject) || realType == typeof(JsonDocument) || realType == typeof(JsonElement)))
            return MapJsonColumn(column, param, realType);

        if (column.Converter is null && realType == typeof(DateTimeOffset))
            return MapDateTimeOffsetColumn(column, param);

        return base.MapColumnExpression(column, param);
    }

    // The ClickHouse.Driver surfaces DateTime64 as System.DateTime, so the generic typed reader's
    // GetFieldValue<DateTimeOffset> throws InvalidCastException for a DateTimeOffset projection. The
    // driver's value is the stored UTC instant (Kind Utc/Unspecified), so it is read as DateTime and
    // reinterpreted as an offset-zero DateTimeOffset. No locale/session shift is applied and the stored
    // microsecond precision is preserved.
    private static Expression MapDateTimeOffsetColumn(SelectExpression column, Expression param)
    {
        var index = Expression.Constant(column.Index);
        var getter = Expression.Call(
            Expression.Convert(param, typeof(DbDataReader)),
            GetFieldValueMI.MakeGenericMethod(typeof(DateTime)),
            index);
        Expression value = Expression.Call(ToDateTimeOffsetMI, getter);

        if (value.Type != column.PropertyType)
            value = Expression.Convert(value, column.PropertyType);

        if (column.Nullable)
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Constant(null, column.PropertyType),
                value);

        if (column.DefaultOnNull)
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Default(column.PropertyType),
                value);

        return value;
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(value, TimeSpan.Zero),
        DateTimeKind.Local => new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero),
        _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero),
    };

    private static Expression MapJsonColumn(SelectExpression column, Expression param, Type realType)
    {
        var index = Expression.Constant(column.Index);
        var getter = Expression.Call(
            Expression.Convert(param, typeof(DbDataReader)),
            GetFieldValueMI.MakeGenericMethod(typeof(JsonObject)),
            index);

        Expression value = realType == typeof(JsonObject) ? getter : AdaptJsonObject(getter, realType);

        // Defensive DBNull handling: a SQL NULL materializes as a null reference (JsonObject/JsonDocument),
        // or as default(JsonElement) (ValueKind Undefined) for the non-nullable value type, distinct from
        // {}. For a declared Nullable<T> (for example JsonElement?) the null must stay a true null: the
        // default(JsonElement) would otherwise surface as an Undefined value that has HasValue == true.
        Expression nullValue;
        if (column.Nullable && column.PropertyType != realType)
        {
            value = Expression.Convert(value, column.PropertyType);
            nullValue = Expression.Constant(null, column.PropertyType);
        }
        else
        {
            nullValue = realType.IsValueType
                ? Expression.Default(realType)
                : Expression.Constant(null, realType);
        }

        return Expression.Condition(
            Expression.Call(param, IsDBNullMI, index),
            nullValue,
            value);
    }

    private static Expression AdaptJsonObject(Expression jsonObject, Type realType)
    {
        var method = realType == typeof(JsonDocument) ? ToJsonDocumentMI : ToJsonElementMI;
        return Expression.Call(method, jsonObject);
    }

    private static JsonDocument ToJsonDocument(JsonObject value) => JsonDocument.Parse(value.ToJsonString());

    private static JsonElement ToJsonElement(JsonObject value) => JsonSerializer.SerializeToElement(value);

    /// <summary>
    /// Creates a <c>ClickHouseDbParameter</c>. The driver rewrites the ADO-style <c>@name</c>
    /// placeholder to <c>{name:Type}</c> and infers the ClickHouse type from the CLR value, so the
    /// stored name omits the <c>@</c> prefix. A <see cref="JsonObject"/>, and an object-root
    /// <see cref="JsonElement"/>/<see cref="JsonDocument"/>, are bound as the native ClickHouse
    /// <c>JSON</c> type; a rank-1 CLR array whose declared element type is nullable
    /// (<see cref="Nullable{T}"/> or <see cref="string"/>) is bound as <c>Array(Nullable(T))</c> so its
    /// null elements survive the driver's non-nullable inference. Any other value keeps its existing
    /// binding.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new ClickHouse parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // The driver rewrites the ADO-style @name placeholder to {name:Type} and infers the
        // ClickHouse type from the CLR value; the stored name is the one without the @ prefix.
        var parameter = new ClickHouseValueParameter
        {
            ParameterName = name.TrimStart('@'),
            Value = value
        };

        // The driver infers JSON from a JsonObject value; the explicit type makes the native JSON
        // binding independent of that inference. Native ClickHouse JSON is object-rooted, so a bare
        // object-root element/document is normalized to a self-contained JsonObject first; any other
        // root (array, primitive, string, JSON null) keeps its existing binding rather than silently
        // rewriting. A JsonObject created directly from a JsonElement still references the caller's
        // JsonDocument, so the value is reparsed from its raw text: the parameter then survives the
        // caller disposing the document before execution.
        if (value is JsonObject)
        {
            parameter.ClickHouseType = "JSON";
        }
        else if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            parameter.Value = JsonNode.Parse(element.GetRawText())!;
            parameter.ClickHouseType = "JSON";
        }
        else if (value is JsonDocument { RootElement.ValueKind: JsonValueKind.Object } document)
        {
            parameter.Value = JsonNode.Parse(document.RootElement.GetRawText())!;
            parameter.ClickHouseType = "JSON";
        }
        else if (value is not null && NullableArrayTypeName(value.GetType()) is { } nullableArrayType)
        {
            // The driver infers a non-nullable element type from the CLR array, so a null element is
            // silently replaced by the element default. The declared nullable element type is the only
            // reliable source: an explicit Array(Nullable(T)) keeps the nulls, and the value itself is
            // bound unchanged (never converted or copied).
            parameter.ClickHouseType = nullableArrayType;
        }

        return parameter;
    }

    // The declared element type of a rank-1 CLR array that can hold a null: Nullable<T> or string.
    // Returns the matching Array(Nullable(T)) ClickHouse type name, or null when the shape stays on
    // the driver's inference (non-nullable element type, jagged/nested array, an element type the
    // provider cannot name). The name comes from the declared element type only, so empty and
    // all-null arrays keep it too.
    private static string? NullableArrayTypeName(Type arrayType)
    {
        if (!arrayType.IsArray || arrayType.GetArrayRank() != 1)
            return null;

        var elementType = arrayType.GetElementType()!;
        if (Nullable.GetUnderlyingType(elementType) is null && elementType != typeof(string))
            return null;

        // ClickHouse's canonical Decimal spelling omits the space the table-valued-parameter mapping
        // carries; the name is only metadata, so keep the element mapping's default precision/scale.
        if ((Nullable.GetUnderlyingType(elementType) ?? elementType) == typeof(decimal))
            return "Array(Nullable(Decimal(38,10)))";

        try
        {
            return $"Array({ColumnTypeName(elementType)})";
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// A <c>ClickHouseDbParameter</c> that normalizes a CLR <see cref="char"/> to the equivalent
    /// one-character string at every driver assignment. ClickHouse has no char type (the driver's type
    /// map rejects <see cref="char"/>), and SQL has no char value either; the mapping matches the
    /// table-parameter storage rule (<c>TableParameterBinder</c>) and covers both the cold bind and the
    /// cached-command value refresh, which writes <c>Value</c> on the existing driver parameter.
    /// Every other value passes through byte-for-byte.
    /// </summary>
    private sealed class ClickHouseValueParameter : ClickHouseDbParameter
    {
        public override object? Value
        {
            get => base.Value;
            set => base.Value = value is char character ? character.ToString() : value;
        }
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
    protected internal override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
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

    // ---------------------------------------------------------------------------------------------
    // Bulk insert (IBulkInsertExecutor). The provider owns the path decision because it owns the native
    // prerequisites: Mode.Portable never enters here, Mode.Native refuses by name when a prerequisite is
    // missing, and Mode.Auto uses the portable INSERT ... VALUES path only for the equivalent
    // single-stream request instead of replaying a failed insert.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The native <c>InsertBinaryAsync</c> path writes the explicit values into the ordinary writable
    /// columns (ClickHouse has no identity concept, so a mapped identity column is simply written), and
    /// cannot skip conflicting rows: ClickHouse has no unique constraint and no ignore form.
    /// </summary>
    bool IBulkInsertNativeOptions.SupportsNativeKeepIdentity => true;

    /// <inheritdoc cref="IBulkInsertNativeOptions.SupportsNativeIgnoreDuplicates"/>
    bool IBulkInsertNativeOptions.SupportsNativeIgnoreDuplicates => false;

    /// <summary>
    /// Applies the provider-scoped contract on the portable path, so <see cref="BulkInsertMode.Portable"/>
    /// runs the same option and shape contract as the native path: the session/parallelism request is
    /// resolved against the connection's actual session (the only place that knows it), the
    /// <c>KeepNulls=false</c> equivalence guard runs, and ALIAS/MATERIALIZED columns are refused. ClickHouse
    /// has no session control to snapshot or restore, so the scope itself owns no state.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The scope to dispose after the write.</returns>
    IDisposable IBulkInsertPortableScope.BeginPortableBulkInsertScope(BulkInsertCommand command)
    {
        ValidatePortableFallback(command);
        EnsureKeepNullsIsDefaultEquivalent(command);
        EnsureColumnsAreWritable(command);
        return NoBulkInsertScope.Instance;
    }

    int IBulkInsertExecutor.BulkInsert(BulkInsertCommand command)
    {
        var rows = command.SyncRows
            ?? throw new InvalidOperationException("BulkInsert is synchronous but the source is async; use BulkInsertAsync instead.");

        if (!TryUseNative(command, out var client))
        {
            ValidatePortableFallback(command);
            return new PortableBulkInsertExecutor(this).BulkInsert(command);
        }

        // The shape guard runs before the first row is pulled, so a refused shape never writes a batch.
        EnsureKeepNullsIsDefaultEquivalent(command);

        using var deadline = BulkInsertDeadline.Start(command.Timeout, CancellationToken.None);
        return WriteNativeRows(command, rows, client!, deadline);
    }

    async Task<int> IBulkInsertExecutor.BulkInsertAsync(BulkInsertCommand command, CancellationToken cancellationToken)
    {
        var rows = command.AsyncRows ?? new SyncToAsyncEnumerable<object?[]>(command.SyncRows!);

        if (!TryUseNative(command, out var client))
        {
            ValidatePortableFallback(command);
            return await new PortableBulkInsertExecutor(this).BulkInsertAsync(command, cancellationToken).ConfigureAwait(false);
        }

        EnsureKeepNullsIsDefaultEquivalent(command);

        using var deadline = BulkInsertDeadline.Start(command.Timeout, cancellationToken);
        try
        {
            return await WriteNativeRowsAsync(command, rows, client!, deadline).ConfigureAwait(false);
        }
        catch (OperationCanceledException cancellation)
        {
            throw deadline.Translate(cancellation);
        }
    }

    /// <summary>
    /// Resolves whether the write takes the native path. <see cref="BulkInsertMode.Portable"/> never does;
    /// a missing native prerequisite refuses an explicit <see cref="BulkInsertMode.Native"/> by name and
    /// lets <see cref="BulkInsertMode.Auto"/> fall back to the portable path, which validates equivalence
    /// itself before a row is read.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <param name="client">The resolved client, or <see langword="null"/> when the native path is not taken.</param>
    /// <returns>Whether the native path is used.</returns>
    /// <exception cref="NotSupportedException">An explicit native request has no usable ClickHouse client.</exception>
    private bool TryUseNative(BulkInsertCommand command, out IClickHouseClient? client)
    {
        client = null;

        if (command.Mode == BulkInsertMode.Portable)
            return false;

        try
        {
            client = ResolveClient();
            return true;
        }
        catch (NotSupportedException) when (command.Mode != BulkInsertMode.Native)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the client that runs the native insert: the caller-supplied client or data-source client
    /// as-is, otherwise a context-owned client built from a full copy of the connection's settings
    /// (<c>new ClickHouseClientSettings(connection.Settings)</c>, never a connection-string round trip and
    /// never a mutation of the connection's settings object).
    /// </summary>
    /// <returns>The client to use.</returns>
    /// <exception cref="NotSupportedException">The context connection is not a ClickHouse connection.</exception>
    private IClickHouseClient ResolveClient()
    {
        if (_providedClient is not null)
            return _providedClient;

        var connection = GetConnection();
        if (connection is not ClickHouseConnection clickHouseConnection)
        {
            throw new NotSupportedException(
                $"The ClickHouse native InsertBinaryAsync path requires a ClickHouse.Driver {nameof(ClickHouseConnection)}; the current connection is "
                + $"{connection.GetType().Name}. Use Mode.Auto to write through the portable INSERT ... VALUES path, or supply a ClickHouse connection.");
        }

        _ownedClient ??= new ClickHouseClient(new ClickHouseClientSettings(clickHouseConnection.Settings));
        return _ownedClient;
    }

    /// <summary>
    /// Validates a requested ClickHouse group before the portable fallback reads a row: the portable path
    /// is a single stream over the context connection, so only the equivalent request is accepted there.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <exception cref="NotSupportedException">The portable path cannot express the request.</exception>
    private void ValidatePortableFallback(BulkInsertCommand command)
    {
        if (command.ClickHouse is null)
            return;

        var usesSession = GetConnection() is ClickHouseConnection clickHouseConnection
            && clickHouseConnection.Settings.UseSession
            && !string.IsNullOrEmpty(clickHouseConnection.Settings.SessionId);

        ClickHouseInsertRequest.ValidatePortableFallback(command.Options, usesSession);
    }

    private int WriteNativeRows(BulkInsertCommand command, IEnumerable<object?[]> rows, IClickHouseClient client, BulkInsertDeadline deadline)
    {
        var adapter = CreateAdapter(command, client);
        return adapter.Write(rows, command.Progress, command.NotifyEvery, deadline);
    }

    private Task<int> WriteNativeRowsAsync(BulkInsertCommand command, IAsyncEnumerable<object?[]> rows, IClickHouseClient client, BulkInsertDeadline deadline)
    {
        var adapter = CreateAdapter(command, client);
        return adapter.WriteAsync(rows, command.Progress, command.NotifyEvery, deadline);
    }

    private ClickHouseBulkInsertAdapter CreateAdapter(BulkInsertCommand command, IClickHouseClient client)
    {
        // Resolve and validate the session/parallelism request before the source is enumerated: a
        // contradictory request fails by name and writes nothing.
        var request = ClickHouseInsertRequest.Resolve(command, client.Settings);
        EnsureLocalTemporarySession(command, request);

        var tableName = SqlMutationBuilder.RenderBulkTarget(Dialect, QuoteIdentifiers, command, NamingConvention);

        // Row 3 is checked after the destination is validated (so a qualification the dialect cannot express
        // is refused without touching the server) and before the first row is read.
        EnsureColumnsAreWritable(command, tableName);

        var columnNames = new List<string>(command.Columns.Count);
        foreach (var column in command.Columns)
            columnNames.Add(SqlMutationBuilder.ResolveColumnName(column, NamingConvention));

        var chunkSize = command.Batch?.MaxBatchSize is int rows && rows > 0
            ? rows
            : ClickHouseBulkInsertAdapter.DefaultChunkRows;

        return ClickHouseBulkInsertAdapter.ForClient(client, tableName, columnNames, request, chunkSize);
    }

    /// <summary>
    /// Row 17: a local temporary target is an external, pre-existing table in the caller's session, so the
    /// insert must run in the same actual session id as the context connection; nextorm never creates or
    /// drops the table and opens no transaction.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <param name="request">The resolved session/parallelism request.</param>
    /// <exception cref="NotSupportedException">The insert and the connection address different sessions.</exception>
    private void EnsureLocalTemporarySession(BulkInsertCommand command, ClickHouseInsertRequest request)
    {
        if (command.TargetKind != BulkInsertTargetKind.LocalTemporary || !request.UsesSession)
            return;

        var connectionSessionId = (GetConnection() as ClickHouseConnection)?.Settings.SessionId;
        if (string.IsNullOrEmpty(connectionSessionId)
            || string.Equals(connectionSessionId, request.SessionId, StringComparison.Ordinal))
        {
            return;
        }

        throw new NotSupportedException(
            $"BulkInsertOptions.TargetKind.LocalTemporary requires the insert to run in the same actual ClickHouse session as the context connection ('{connectionSessionId}'), "
            + $"but the resolved client uses '{request.SessionId}'. Supply a client or data source built from the connection's settings, or write to a regular table.");
    }

    // A binary insert always writes the supplied value, so KeepNulls=false can only be honoured when
    // writing NULL is indistinguishable from letting the destination DEFAULT apply: every written column
    // must be nullable. The SQL-default half is certified against the live schema by the integration
    // suite; a non-nullable input column is refused here, before any row is read. The whole row is still
    // written, so no partial-column trick fakes the DEFAULT.
    private void EnsureKeepNullsIsDefaultEquivalent(BulkInsertCommand command)
    {
        if (command.Options.KeepNulls is not false)
            return;

        var nullability = new NullabilityInfoContext();
        var nonNullable = new List<string>(command.Columns.Count);
        foreach (var column in command.Columns)
        {
            if (nullability.Create(column.PropertyInfo).WriteState != NullabilityState.Nullable)
                nonNullable.Add(column.ColumnName);
        }

        if (nonNullable.Count > 0)
        {
            throw new NotSupportedException(
                "BulkInsertOptions.KeepNulls=false cannot be honoured by the ClickHouse bulk paths: the write always sends the supplied value, and only an omitted column "
                + "lets the destination DEFAULT apply. The request is accepted only for a default-equivalent shape, where every written column is nullable; "
                + $"the non-nullable input column(s) {string.Join(", ", nonNullable.Select(name => $"'{name}'"))} break that shape. "
                + "Use KeepNulls=true, or write the DEFAULT explicitly.");
        }

        // A nullable input alone is not enough: a non-NULL SQL default would be silently skipped, so the
        // option would be accepted and inert. Only the catalog answers that half of the equivalence.
        var destination = BulkInsertDestination.From(command.Options);
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in command.Columns)
            written.Add(SqlMutationBuilder.ResolveColumnName(column, NamingConvention));

        var defaulted = ReadColumnsWithSqlDefault(
                SqlMutationBuilder.ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, NamingConvention),
                destination.Schema ?? destination.Database)
            .Where(written.Contains)
            .ToList();

        if (defaulted.Count == 0)
            return;

        throw new NotSupportedException(
            "BulkInsertOptions.KeepNulls=false cannot be honoured by the ClickHouse bulk paths: the write always sends the supplied value, and only an omitted column "
            + $"lets the destination DEFAULT apply. The written column(s) {string.Join(", ", defaulted.Select(name => $"'{name}'"))} declare a SQL default, "
            + "so a NULL input would be written as NULL instead of the default. Use KeepNulls=true, or write the DEFAULT explicitly.");
    }

    /// <summary>
    /// Reads the columns of <paramref name="table"/> whose live SQL default is non-empty. This is the half of
    /// the <c>KeepNulls=false</c> equivalence that exists only in the schema, so it is read from
    /// <c>system.columns</c>. Internal and virtual so the SQL-generation tests, which talk to a recording
    /// transport instead of a server, can supply the schema facts.
    /// </summary>
    /// <param name="table">The physical table name.</param>
    /// <param name="database">The destination database, or <see langword="null"/> for the current one.</param>
    /// <returns>The column names that declare a SQL default.</returns>
    internal virtual IReadOnlyList<string> ReadColumnsWithSqlDefault(string table, string? database)
    {
        EnsureConnectionOpen();

        var databasePredicate = database is null
            ? "database = currentDatabase()"
            : $"database = '{EscapeSqlLiteral(database)}'";

        var defaulted = new List<string>();
        using var select = CreateCommand(
            "select name from system.columns where table = '" + EscapeSqlLiteral(table) + "' and "
            + databasePredicate
            + " and startsWith(type, 'Nullable') and default_expression <> ''");

        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                defaulted.Add(reader.GetString(0));
        }

        return defaulted;
    }

    /// <summary>
    /// Row 3: ClickHouse computes ALIAS and MATERIALIZED columns from other columns, so they accept no
    /// inserted value. A mapping that writes one is refused by name before the source is read instead of
    /// failing inside the insert with a server error. The catalog facts are cached per target, so a context
    /// pays one metadata read per destination, not one per insert.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <param name="renderedTable">The already-validated table reference, or <see langword="null"/> when it was not rendered yet.</param>
    /// <exception cref="NotSupportedException">A written column is ALIAS or MATERIALIZED.</exception>
    private void EnsureColumnsAreWritable(BulkInsertCommand command, string? renderedTable = null)
    {
        var destination = BulkInsertDestination.From(command.Options);
        var table = SqlMutationBuilder.ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, NamingConvention);
        var database = destination.Schema ?? destination.Database;

        var nonWritable = ReadNonWritableColumns(table, database);
        if (nonWritable.Count == 0)
            return;

        var refused = new List<string>(nonWritable.Count);
        foreach (var column in command.Columns)
        {
            var name = SqlMutationBuilder.ResolveColumnName(column, NamingConvention);
            if (nonWritable.Contains(name, StringComparer.OrdinalIgnoreCase))
                refused.Add(name);
        }

        if (refused.Count == 0)
            return;

        throw new NotSupportedException(
            "ClickHouse writes only ordinary/DEFAULT columns: an ALIAS or MATERIALIZED column is computed by the server and accepts no inserted value. "
            + $"The written column(s) {string.Join(", ", refused.Select(name => $"'{name}'"))} are ALIAS or MATERIALIZED on '{renderedTable ?? table}'. "
            + "Remove them from the mapping, or write to a regular column.");
    }

    /// <summary>
    /// Reads the ALIAS/MATERIALIZED columns of <paramref name="table"/>, caching the answer per
    /// (database, table). The column kind exists only in the schema, so it is read from <c>system.columns</c>
    /// once per target. Internal and virtual so the SQL-generation tests, which talk to a recording
    /// transport instead of a server, can supply the schema facts.
    /// </summary>
    /// <param name="table">The physical table name.</param>
    /// <param name="database">The destination database, or <see langword="null"/> for the current one.</param>
    /// <returns>The column names that are ALIAS or MATERIALIZED.</returns>
    internal virtual IReadOnlyList<string> ReadNonWritableColumns(string table, string? database)
    {
        var key = (database ?? string.Empty) + "\u0000" + table;
        if (_nonWritableColumns.TryGetValue(key, out var cached))
            return cached;

        var connection = GetConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        EnsureConnectionOpen();

        var databasePredicate = database is null
            ? "database = currentDatabase()"
            : $"database = '{EscapeSqlLiteral(database)}'";

        var nonWritable = new List<string>();
        try
        {
            using var select = CreateCommand(
                "select name from system.columns where table = '" + EscapeSqlLiteral(table) + "' and "
                + databasePredicate
                + " and default_kind in ('ALIAS', 'MATERIALIZED')");

            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    nonWritable.Add(reader.GetString(0));
            }
        }
        finally
        {
            // The metadata probe is not the caller's work: a connection that was closed before it stays closed.
            if (!wasOpen && connection.State == ConnectionState.Open)
                connection.Close();
        }

        _nonWritableColumns[key] = nonWritable;
        return nonWritable;
    }

    // ClickHouse string literals escape the quote and the backslash; the table/database names come from the
    // mapped metadata and the destination options, never from SQL text.
    private static string EscapeSqlLiteral(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Exactly once: the exchange makes a repeated Dispose a no-op and the client is disposed only
            // when the context created it. A caller-supplied client/data-source client is never touched.
            var owned = Interlocked.Exchange(ref _ownedClient, null);
            if (owned is not null)
            {
                Interlocked.Increment(ref _ownedClientDisposalCount);
                owned.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>The context-owned client, or <see langword="null"/> when a client/data source was supplied or no native write ran.</summary>
    internal IClickHouseClient? OwnedClient => _ownedClient;

    /// <summary>The number of times the context disposed its own client (0 or 1).</summary>
    internal int OwnedClientDisposalCount => Volatile.Read(ref _ownedClientDisposalCount);

    /// <summary>The scope returned when the portable path needs no provider session state.</summary>
    private sealed class NoBulkInsertScope : IDisposable
    {
        internal static readonly NoBulkInsertScope Instance = new();

        private NoBulkInsertScope()
        {
        }

        public void Dispose()
        {
        }
    }
}
