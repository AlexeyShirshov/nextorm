using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NextORM.Core;

/// <summary>
/// A single projected column of a query: its ordinal, the expression that produces it, the target
/// property it maps to and the reader accessor used to materialize it.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Static readonly reflection-metadata fields (GetInt32MI, ...) are intentionally PascalCase as immutable lookup tables; IDE1006 is a suggestion and is not enforced by the build.")]
public sealed class SelectExpression //: IEquatable<SelectExpression>
{
    private readonly Type _realType;
    //private readonly bool _nullable;
    /// <summary>Whether the CLR type can hold <c>null</c> (a reference type or a <see cref="Nullable{T}"/>).</summary>
    public readonly bool Nullable;
    /// <summary>The zero-based ordinal of the column in the result set.</summary>
    public int Index { get; set; }
    /// <summary>The target property name, or <c>null</c> for a positional projection.</summary>
    public string? PropertyName { get; set; }
    /// <summary>The expression that produces the value, or <c>null</c> for a plain entity column.</summary>
    public Expression? Expression { get; set; }
    /// <summary>The CLR type of the value produced.</summary>
    public Type PropertyType { get; set; }
    internal PropertyInfo? PropertyInfo { get; set; }

    /// <summary>
    /// The CLR member the row materializer binds this column to when the projection has to be assembled
    /// member by member. It is populated by the projector from the projection expression (an anonymous
    /// type's <c>NewExpression.Members</c>, a DTO's member-init binding), so the materializer never
    /// re-resolves the member from the runtime-typed result type by name.
    /// <para>
    /// Deliberately separate from <see cref="PropertyInfo"/>: that one marks a whole-entity or
    /// expanded-entity column and is consumed by the JSON shape and CSV writers, whereas a projected
    /// anonymous/DTO column is not entity-derived. Folding the two changed JSON enum-attribute and
    /// native-streaming decisions.
    /// </para>
    /// </summary>
    internal PropertyInfo? MaterializerMember { get; set; }

    /// <summary>The member the materializer binds this column to, if the projector resolved one.</summary>
    internal PropertyInfo? BoundMember => PropertyInfo ?? MaterializerMember;

    /// <summary>
    /// The declared storage unit of a <see cref="TimeSpan"/> column on a provider without a native
    /// duration type, or <c>null</c> for a native column. Populated from the entity property metadata;
    /// the provider's column mapper decides whether it applies (see
    /// <see cref="ISqlDialect.SupportsNativeDuration"/>).
    /// </summary>
    public DurationUnit? DurationUnit { get; set; }

    /// <summary>
    /// The fractional-second precision of a native duration type, or zero for the provider default.
    /// </summary>
    public int DurationPrecision { get; set; }

    /// <summary>
    /// The provider-side type <see cref="Converter"/> produces and reads, or <see langword="null"/>
    /// when the property is read as its CLR type. The reader selects its typed accessor from this type
    /// rather than <see cref="PropertyType"/>.
    /// </summary>
    public Type? ProviderType { get; set; }

    /// <summary>
    /// The value converter applied to this column after reading and before materialization, or
    /// <see langword="null"/> when the column is read as-is.
    /// </summary>
    public IPropertyValueConverter? Converter { get; set; }

    /// <summary>
    /// True when a SQL NULL in this column means "no row" (the projection came from a
    /// <c>*OrDefault</c> scalar terminal) and the column is a non-nullable value type, so the reader
    /// must substitute <c>default</c> instead of throwing. Set while the projection is prepared;
    /// read by the provider's column mapper.
    /// </summary>
    public bool DefaultOnNull { get; internal set; }

    /// <summary>
    /// Which column of a <see cref="Range{T}"/> pair this expression reads, or
    /// <see cref="RangeColumnRole.None"/> for an ordinary column. Set by the entity mapping when a
    /// property declared with <see cref="RangeColumnsAttribute"/> expands into two columns.
    /// </summary>
    internal RangeColumnRole RangeColumnRole { get; set; }

    /// <summary>
    /// The pair descriptor of the enclosing <see cref="Range{T}"/> property, or <see langword="null"/>
    /// for an ordinary column. Read by the entity materializer to rebuild the range value.
    /// </summary>
    internal RangeColumnsMetadata? RangeColumns { get; set; }

    /// <summary>
    /// Whether this entry is the entity's dynamic-columns store rather than a projected column. Such
    /// an entry renders the source's <c>*</c> after the mapped columns; it carries no expression and is
    /// read into the store by the materializer.
    /// </summary>
    internal bool IsDynamicColumnsStore { get; set; }

    /// <summary>
    /// True when the column was projected through a streaming named-column accessor
    /// (<see cref="TableAlias.GetStream"/>/<see cref="TableAlias.GetTextReader"/> or
    /// <see cref="TableColumn.AsStream"/>/<see cref="TableColumn.AsTextReader"/>). Such a column can
    /// only be materialized by a streaming terminal; the buffered row mapper rejects it and the mapper
    /// cache key carries the flag so a buffered mapper is never reused for a streaming shape.
    /// </summary>
    internal bool IsLobStreaming { get; set; }

    /// <summary>
    /// #148-B r3 A3′: true when this <see cref="int"/> column is the checked narrowing of a wide
    /// (64-bit) navigation collection count. The reader must read the underlying <c>bigint</c> and
    /// apply a checked <c>long-&gt;int</c> conversion instead of a provider <c>GetInt32</c>, so an
    /// out-of-range count throws <see cref="OverflowException"/> uniformly on every provider. Set by
    /// the query preparer from the wide-count provenance (the referenced count command); carried into
    /// the plan hash and the mapper signature so a nav-count column never shares a cached mapper with
    /// an ordinary int column.
    /// </summary>
    internal bool IsWideCountNarrowed { get; set; }

    /// <summary>
    /// The physical column name the mapped property reads, used by the dynamic-columns store to skip
    /// the columns already read into declared members. <see langword="null"/> for a computed column.
    /// </summary>
    internal string? PhysicalColumnName { get; set; }

    /// <summary>
    /// The entity-typed projection item this column was expanded from, or <see langword="null"/> for an
    /// ordinary column. Columns sharing the same instance form one item, which the row materializer
    /// rebuilds into an entity instance (and into <see langword="null"/> when every column of the item
    /// reads as SQL <c>NULL</c>, i.e. the row is on the missing side of an outer join).
    /// </summary>
    internal ProjectionEntityItem? ProjectionItem { get; set; }

    /// <summary>
    /// The stable output identifier of a projection-source column that has no CLR property name (an
    /// unaliased computed scalar such as <c>Select(x =&gt; x.Total * 2)</c>). A typed CTE declaration
    /// aliases such a column to this name so a consumer can reference it, because the defining
    /// expression references the CTE's own input columns and cannot be re-rendered against the CTE.
    /// <see langword="null"/> for an ordinary named column.
    /// </summary>
    internal string? OutputName { get; set; }

    /// <summary>
    /// True when this column is a consumer-side reference to a projection source's output identifier
    /// (see <see cref="OutputName"/>) rather than a re-rendered defining expression. Such a column
    /// renders as the source's output column name.
    /// </summary>
    internal bool IsProjectionOutputReference { get; set; }
    // public List<QueryCommand>? ReferencedQueries { get; set; }
    //private readonly IDictionary<ExpressionKey, Delegate> _expCache;
    // private readonly IQueryRegistry _queryProvider;

    /// <summary>Initializes a projected column for the given CLR type, deriving its nullability from the type.</summary>
    /// <param name="propertyType">The CLR type of the value produced.</param>
    public SelectExpression(Type propertyType)
    {
        PropertyType = propertyType;
        var nullable = PropertyType.IsGenericType && PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        Nullable = PropertyType.IsClass || nullable;

        if (nullable)
        {
            _realType = System.Nullable.GetUnderlyingType(PropertyType)!;
        }
        else
        {
            _realType = PropertyType;
        }
        //_expCache = expCache;
        // _queryProvider = queryProvider;
    }
    //public readonly bool IsEmpty => _realType is null;
    private readonly static MethodInfo GetInt32MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt32))!;
    private readonly static MethodInfo GetInt64MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt64))!;
    private readonly static MethodInfo GetDateTimeMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDateTime))!;
    private readonly static MethodInfo GetStringMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetString))!;
    private readonly static MethodInfo GetBooleanMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetBoolean))!;
    private readonly static MethodInfo GetDoubleMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDouble))!;
    private readonly static MethodInfo GetDecimalMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDecimal))!;
    private readonly static MethodInfo GetFloatMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetFloat))!;
    private readonly static MethodInfo GetInt16MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt16))!;
    private readonly static MethodInfo GetByteMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetByte))!;
    private readonly static MethodInfo GetGuidMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetGuid))!;
    // Two accessor tables share the simple (non-generic) getters below. The internal one that the shared
    // row mapper consumes gives the eight GetFieldValue<T> types a non-generic static reader that closes
    // the instantiation at compile time (NativeAOT has no JIT for a runtime instantiation). The public
    // accessor keeps returning the closed generic DbDataReader.GetFieldValue<T> member it always returned,
    // so no public contract changes; replacing that member is a separate, user-approved design proposal.
    private readonly static MethodInfo GetFieldValueMI = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!;
    private readonly static MethodInfo GetFieldValueTimeSpanMI = ((Func<DbDataReader, int, TimeSpan>)ReadFieldValueTimeSpan).Method;
    private readonly static MethodInfo GetFieldValueDateTimeOffsetMI = ((Func<DbDataReader, int, DateTimeOffset>)ReadFieldValueDateTimeOffset).Method;
    private readonly static MethodInfo GetFieldValueUInt64MI = ((Func<DbDataReader, int, ulong>)ReadFieldValueUInt64).Method;
    private readonly static MethodInfo GetFieldValueUInt32MI = ((Func<DbDataReader, int, uint>)ReadFieldValueUInt32).Method;
    private readonly static MethodInfo GetFieldValueJsonDocumentMI = ((Func<DbDataReader, int, JsonDocument>)ReadFieldValueJsonDocument).Method;
    private readonly static MethodInfo GetFieldValueJsonElementMI = ((Func<DbDataReader, int, JsonElement>)ReadFieldValueJsonElement).Method;
    private readonly static MethodInfo GetFieldValueJsonObjectMI = ((Func<DbDataReader, int, JsonObject>)ReadFieldValueJsonObject).Method;
    private readonly static MethodInfo GetFieldValueByteArrayMI = ((Func<DbDataReader, int, byte[]>)ReadFieldValueByteArray).Method;
    private readonly static MethodInfo GetValueMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    // internal int XxHash32;
    internal int PlanHashCode;

    /// <summary>Gets the reader accessor that reads a value of this column's type from a data record.</summary>
    /// <returns>The <see cref="MethodInfo"/> of the matching <see cref="IDataRecord"/> getter.</returns>
    public MethodInfo GetDataRecordMethod() => GetLegacyDataRecordMethod(ProviderType ?? _realType);

    /// <summary>
    /// The accessor the shared row mapper emits for a column. It matches
    /// <see cref="GetDataRecordMethod()"/> except for the eight types whose typed accessor is a
    /// <c>DbDataReader.GetFieldValue&lt;T&gt;</c> instantiation: the mapper receives a non-generic static
    /// reader that pins the instantiation at compile time, so the AOT compiler never has to materialise a
    /// generic method from a runtime type.
    /// </summary>
    internal MethodInfo GetReaderAccessorMethod(Type readType)
    {
        readType = System.Nullable.GetUnderlyingType(readType) ?? readType;

        if (ResolveSimpleAccessor(readType) is { } simple)
            return simple;

        if (readType == typeof(TimeSpan))
            return GetFieldValueTimeSpanMI;
        if (readType == typeof(DateTimeOffset))
            return GetFieldValueDateTimeOffsetMI;
        if (readType == typeof(ulong))
            return GetFieldValueUInt64MI;
        if (readType == typeof(uint))
            return GetFieldValueUInt32MI;
        if (readType == typeof(JsonDocument))
            return GetFieldValueJsonDocumentMI;
        if (readType == typeof(JsonElement))
            return GetFieldValueJsonElementMI;
        if (readType == typeof(JsonObject))
            return GetFieldValueJsonObjectMI;
        if (readType == typeof(byte[]))
            return GetFieldValueByteArrayMI;

        throw UnsupportedAccessor(readType);
    }

    /// <summary>The public accessor table: unchanged pre-S3 shape, including the closed generic members.</summary>
    private MethodInfo GetLegacyDataRecordMethod(Type readType)
    {
        readType = System.Nullable.GetUnderlyingType(readType) ?? readType;

        if (ResolveSimpleAccessor(readType) is { } simple)
            return simple;

        if (readType == typeof(TimeSpan))
        {
            // Native durations/intervals are exposed by the provider driver as System.TimeSpan.
            // Providers without a native type store the value in an integer column and are read by a
            // provider-specific override instead (RowMapperFactory.MapColumn with
            // supportsNativeDuration == false).
            return GetFieldValueMI.MakeGenericMethod(typeof(TimeSpan));
        }
        if (readType == typeof(DateTimeOffset))
            return GetFieldValueMI.MakeGenericMethod(typeof(DateTimeOffset));
        if (readType == typeof(ulong))
            return GetFieldValueMI.MakeGenericMethod(typeof(ulong));
        if (readType == typeof(uint))
        {
            // PostgreSQL xid/xmin are 32-bit unsigned integers exposed as System.UInt32.
            return GetFieldValueMI.MakeGenericMethod(typeof(uint));
        }
        if (readType == typeof(JsonDocument))
        {
            // A provider-native JSON column (PostgreSQL jsonb) is read as System.Text.Json.JsonDocument.
            return GetFieldValueMI.MakeGenericMethod(typeof(JsonDocument));
        }
        if (readType == typeof(JsonElement))
            return GetFieldValueMI.MakeGenericMethod(typeof(JsonElement));
        if (readType == typeof(JsonObject))
        {
            // ClickHouse's native JSON is materialized by the driver as a JsonObject; the typed accessor
            // reads it without boxing. The accessor selector only reaches this branch for a dialect whose
            // NativeJsonProviderType is JsonObject (see RowMapperFactory.GetReaderAccessor); PostgreSQL and
            // every other dialect keep their previous not-supported rejection.
            return GetFieldValueMI.MakeGenericMethod(typeof(JsonObject));
        }
        if (readType == typeof(byte[]))
        {
            // A binary column is read through the generic typed accessor so the value is not boxed
            // through GetValue; the caller receives the array directly.
            return GetFieldValueMI.MakeGenericMethod(typeof(byte[]));
        }

        throw UnsupportedAccessor(readType);
    }

    /// <summary>The getters both accessor tables share, or <see langword="null"/> for a special or unsupported type.</summary>
    private static MethodInfo? ResolveSimpleAccessor(Type readType)
    {
        if (readType == typeof(int))
            return GetInt32MI;
        if (readType == typeof(long))
            return GetInt64MI;
        if (readType == typeof(DateTime))
            return GetDateTimeMI;
        if (readType == typeof(string))
            return GetStringMI;
        if (readType == typeof(bool))
            return GetBooleanMI;
        if (readType == typeof(double))
            return GetDoubleMI;
        if (readType == typeof(decimal))
            return GetDecimalMI;
        if (readType == typeof(float))
            return GetFloatMI;
        if (readType == typeof(short))
            return GetInt16MI;
        if (readType == typeof(byte))
            return GetByteMI;
        if (readType == typeof(Guid))
            return GetGuidMI;

        // Other array columns and array-returning expressions (PostgreSQL text[]/int[],
        // ClickHouse Array(T) including nested arrays), ClickHouse Tuple(...) columns and a native
        // ClickHouse Map(String, String) have no typed reader getter; read the value through GetValue
        // and let the caller cast it to the declared shape. A byte[] column is not one of them: it has a
        // typed accessor and is handled by the special branches.
        if ((readType.IsArray && readType != typeof(byte[]))
            || TypeFacts.IsTupleType(readType)
            || readType == typeof(Dictionary<string, string>))
            return GetValueMI;

        return null;
    }

    private NotSupportedException UnsupportedAccessor(Type readType)
        => new($"Property '{PropertyName}' with index ({Index}) has type {readType} which is not supported");

    // Non-generic accessor bodies behind the GetFieldValue*MI fields above. Each pins one
    // DbDataReader.GetFieldValue<T> instantiation at compile time so the accessor table never has to
    // instantiate a generic method from a runtime type (NativeAOT has no JIT for that path).
    private static TimeSpan ReadFieldValueTimeSpan(DbDataReader reader, int ordinal) => reader.GetFieldValue<TimeSpan>(ordinal);

    private static DateTimeOffset ReadFieldValueDateTimeOffset(DbDataReader reader, int ordinal) => reader.GetFieldValue<DateTimeOffset>(ordinal);

    private static ulong ReadFieldValueUInt64(DbDataReader reader, int ordinal) => reader.GetFieldValue<ulong>(ordinal);

    private static uint ReadFieldValueUInt32(DbDataReader reader, int ordinal) => reader.GetFieldValue<uint>(ordinal);

    private static JsonDocument ReadFieldValueJsonDocument(DbDataReader reader, int ordinal) => reader.GetFieldValue<JsonDocument>(ordinal);

    private static JsonElement ReadFieldValueJsonElement(DbDataReader reader, int ordinal) => reader.GetFieldValue<JsonElement>(ordinal);

    private static JsonObject ReadFieldValueJsonObject(DbDataReader reader, int ordinal) => reader.GetFieldValue<JsonObject>(ordinal);

    private static byte[] ReadFieldValueByteArray(DbDataReader reader, int ordinal) => reader.GetFieldValue<byte[]>(ordinal);

    /// <summary>
    /// The static accessor that reads a <c>byte[]</c> column through
    /// <see cref="DbDataReader.GetFieldValue{T}(int)"/>. Consumption sites that need to recognise a
    /// direct binary read compare against this member instead of pattern-matching the accessor shape.
    /// </summary>
    internal static MethodInfo ByteArrayFieldValueReader => GetFieldValueByteArrayMI;

    // public override int GetHashCode()
    // {
    //     unchecked
    //     {
    //         var hash = new XxHash32();

    //         hash.Add(Index);

    //         hash.Add(PropertyType);

    //         hash.Add(PropertyName);

    //         hash.Add(Expression, _queryProvider.GetPreciseExpressionEqualityComparer());

    //         // if (ReferencedQueries is not null)
    //         //     foreach (var cmd in ReferencedQueries)
    //         //         hash.Add(cmd);

    //         return hash.ToHashCode();
    //     }
    // }
    // public override bool Equals(object? obj)
    // {
    //     // if (obj is null) return false;
    //     // return Equals((SelectExpression)obj);
    //     return Equals(obj as SelectExpression);
    // }
    // public bool Equals(SelectExpression? exp)
    // {
    //     if (exp is null) return false;

    //     if (Index != exp.Index) return false;

    //     if (PropertyType != exp.PropertyType) return false;

    //     if (PropertyName != exp.PropertyName) return false;

    //     if (!_queryProvider.GetPreciseExpressionEqualityComparer().Equals(Expression, exp.Expression))
    //         return false;

    //     // if (ReferencedQueries is null && exp.ReferencedQueries is not null) return false;
    //     // if (ReferencedQueries is not null && exp.ReferencedQueries is null) return false;

    //     // if (ReferencedQueries is not null && exp.ReferencedQueries is not null)
    //     // {
    //     //     if (ReferencedQueries.Count != exp.ReferencedQueries.Count) return false;

    //     //     for (int i = 0; i < ReferencedQueries.Count; i++)
    //     //     {
    //     //         if (!ReferencedQueries[i].Equals(exp.ReferencedQueries[i])) return false;
    //     //     }
    //     // }

    //     return true;
    // }
}

/// <summary>
/// Groups the scalar <see cref="SelectExpression"/>s an entity-typed projection item was expanded
/// into. The preparer tags every expanded column with the shared instance; the row materializer groups
/// by reference, rebuilds the entity from the group and assigns it to <see cref="Member"/>.
/// </summary>
internal sealed class ProjectionEntityItem
{
    /// <summary>Initializes a projection item group.</summary>
    /// <param name="slot">The item's zero-based position in the projection (its argument/member order).</param>
    /// <param name="entityType">The entity type the group materializes into.</param>
    /// <param name="member">The projection member the entity is assigned to, or <see langword="null"/> when the position addresses a constructor parameter.</param>
    public ProjectionEntityItem(int slot, Type entityType, PropertyInfo? member)
    {
        Slot = slot;
        EntityType = entityType;
        Member = member;
    }

    /// <summary>The item's zero-based position in the projection.</summary>
    public int Slot { get; }

    /// <summary>The entity type the group materializes into.</summary>
    public Type EntityType { get; }

    /// <summary>The projection member the entity is assigned to, or <see langword="null"/> for a constructor position.</summary>
    public PropertyInfo? Member { get; }
}
