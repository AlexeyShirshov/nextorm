using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Shared coverage for the managed JSON streaming terminals (<c>WriteJson</c> / <c>WriteJsonAsync</c>),
/// issue #39. The managed path is provider independent: every database provider in this suite must emit
/// the same bytes as <see cref="JsonSerializer"/> for the phase-1 projection forms (scalar and flat
/// object) and options (<see cref="JsonStreamMode.Array"/>, <see cref="JsonStreamMode.NdJson"/> and
/// <c>IgnoreNull</c>) over identical rows. The facts run against SQLite, PostgreSQL, SQL Server and
/// MySQL. ClickHouse and MariaDB do not derive <see cref="CommonTestSuite"/> in this project, so the
/// same bodies are exposed as internal static helpers and invoked from their provider-specific test
/// classes (<c>ClickHouseIntegrationTests</c>, <c>MariaDbJsonStreamTests</c>).
/// The in-memory provider has no JSON path and is out of scope.
/// </summary>
public abstract partial class CommonTestSuite
{
    private static readonly JsonSerializerOptions IgnoreNullSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] WriteJsonBytes<TResult>(QueryCommand<TResult> command, JsonStreamOptions? options = null)
    {
        using var buffer = new MemoryStream();
        if (options is null)
            command.WriteJson(buffer);
        else
            command.WriteJson(buffer, options);

        return buffer.ToArray();
    }

    /// <summary>
    /// Scalar projection (<c>Select(x =&gt; x.Id)</c>): the array document must be byte-identical to
    /// <c>JsonSerializer.Serialize(ToList())</c> with default options.
    /// </summary>
    [Fact]
    public void WriteJson_Array_ShouldMatchJsonSerializer_ForScalar() => WriteJsonArrayScalar(_sut);

    /// <summary>
    /// The scalar-projection body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/> (ClickHouse and MariaDB run their own provider-specific suites).
    /// </summary>
    internal static void WriteJsonArrayScalar(TestDataRepository sut)
    {
        var expected = JsonSerializer.Serialize(
            sut.ComplexEntity.OrderBy(it => it.Id).Select(it => it.Id).ToList());

        var actual = WriteJsonBytes(sut.ComplexEntity.OrderBy(it => it.Id).Select(it => it.Id));

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>
    /// Flat object projection: the array of objects must be byte-identical to the default
    /// <see cref="JsonSerializer"/> output, including a SQL NULL member (<c>Int</c> in the first row).
    /// </summary>
    [Fact]
    public void WriteJson_Array_ShouldMatchJsonSerializer_ForFlatDto() => WriteJsonArrayFlatDto(_sut);

    /// <summary>
    /// The flat-DTO body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/>.
    /// </summary>
    internal static void WriteJsonArrayFlatDto(TestDataRepository sut)
    {
        var expected = JsonSerializer.Serialize(
            sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.Int }).ToList());

        var actual = WriteJsonBytes(
            sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.Int }));

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>
    /// NDJSON mode: one standalone JSON value per line, each line equal to
    /// <c>JsonSerializer.Serialize(row)</c>; every record — including the last — is terminated by
    /// <c>\n</c>.
    /// </summary>
    [Fact]
    public void WriteJson_NdJson_ShouldMatchLineByLine() => WriteJsonNdJson(_sut);

    /// <summary>
    /// The NDJSON body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/>.
    /// </summary>
    internal static void WriteJsonNdJson(TestDataRepository sut)
    {
        var rows = sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.Int }).ToList();

        var actual = WriteJsonBytes(
            sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.Int }),
            new JsonStreamOptions { Mode = JsonStreamMode.NdJson });

        var text = Encoding.UTF8.GetString(actual);
        text.Should().EndWith("\n");

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(rows.Count);
        for (var i = 0; i < rows.Count; i++)
            lines[i].Should().Be(JsonSerializer.Serialize(rows[i]));
    }

    /// <summary>
    /// Provider/type parity for the non-string scalar shapes: <c>byte</c> (a <c>tinyint</c> the
    /// provider may report as <c>sbyte</c>), <c>short</c>, <c>float</c>, <c>double</c>,
    /// <c>decimal</c>, <c>DateTime</c> and <c>bool</c> must match <see cref="JsonSerializer"/> over the
    /// same rows, guarding the typed numeric read against the provider's field type.
    /// </summary>
    [Fact]
    public void WriteJson_TypedColumns_ShouldMatchJsonSerializer() => WriteJsonTypedColumns(_sut);

    /// <summary>
    /// The typed-columns body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/>.
    /// </summary>
    internal static void WriteJsonTypedColumns(TestDataRepository sut)
    {
        var projection = sut.ComplexEntity.OrderBy(it => it.Id)
            .Select(it => new { it.Id, it.TinyInt, it.SmallInt, it.Real, it.Double, it.Numeric, it.Datetime, it.Boolean });

        var expected = JsonSerializer.Serialize(
            sut.ComplexEntity.OrderBy(it => it.Id)
                .Select(it => new { it.Id, it.TinyInt, it.SmallInt, it.Real, it.Double, it.Numeric, it.Datetime, it.Boolean })
                .ToList());

        var actual = WriteJsonBytes(projection);

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>
    /// A <c>byte[]</c> projection must be base64-encoded exactly as <see cref="JsonSerializer"/> does,
    /// including the SQL NULL row.
    /// </summary>
    [Fact]
    public void WriteJson_ByteArray_ShouldMatchJsonSerializer() => WriteJsonByteArray(_sut);

    /// <summary>
    /// The <c>byte[]</c> body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/>.
    /// </summary>
    internal static void WriteJsonByteArray(TestDataRepository sut)
    {
        var expected = JsonSerializer.Serialize(
            sut.BinaryEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.Data }).ToList());

        var actual = WriteJsonBytes(
            sut.BinaryEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.Data }));

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>
    /// <c>IgnoreNull</c> over an entity with nullable members: the document must be byte-identical to
    /// <see cref="JsonSerializer"/> with <see cref="JsonIgnoreCondition.WhenWritingNull"/>.
    /// </summary>
    [Fact]
    public void WriteJson_IgnoreNull_ShouldMatchSerializer() => WriteJsonIgnoreNull(_sut);

    /// <summary>
    /// The <c>IgnoreNull</c> body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/>.
    /// </summary>
    internal static void WriteJsonIgnoreNull(TestDataRepository sut)
    {
        var rows = sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.Int, it.String }).ToList();

        var expected = JsonSerializer.Serialize(rows, IgnoreNullSerializerOptions);

        // Guard against a vacuous comparison: the fixture has NULL Int (row 1) and NULL String (row 3),
        // so the ignored-null document must not contain a null literal.
        expected.Should().NotContain("null");

        var actual = WriteJsonBytes(
            sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.Int, it.String }),
            new JsonStreamOptions { IgnoreNull = true });

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>An empty result set must still produce a well-formed empty JSON array.</summary>
    [Fact]
    public void WriteJson_EmptyResult_ShouldProduceEmptyArray() => WriteJsonEmptyResult(_sut);

    /// <summary>
    /// The empty-result body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/>.
    /// </summary>
    internal static void WriteJsonEmptyResult(TestDataRepository sut)
    {
        var actual = WriteJsonBytes(sut.SimpleEntity.Where(it => it.Id < 0).Select(it => it.Id));

        actual.Should().Equal(Utf8(JsonSerializer.Serialize(new List<int>())));
        Encoding.UTF8.GetString(actual).Should().Be("[]");
    }

    // -------------------------------------------------------------------------------------------------
    // D178 (#178) enum storage matrix: the same numeric-backed enum column is persisted by every
    // provider and streamed as a JSON number (plain enum) or as a JSON string (a stock
    // [JsonConverter(typeof(JsonStringEnumConverter))] type attribute). The shared bodies run once per
    // CommonTestSuite-derived provider; ClickHouse/MariaDB re-pin them because they do not derive the
    // suite. The field-type recorder captures each provider's actual IDataRecord.GetFieldType so the
    // provider-matrix evidence is observable facts, not an assumption.

    // NOTE (D178): the bare scalar endpoint Select(x => x.State) is not exercised here. nextorm's
    // query preparer does not classify an enum member as a single-column projection
    // (TypeFacts.IsSingleColumnProjection excludes enums), so that query renders no columns before the
    // JSON layer is reached — a pre-existing preparer limitation outside the JSON streaming footprint.
    // The plan's scalar one-column shape is proven by the direct-writer tests
    // (JsonStreamingTests.WriteEnumDirect with oneColumn:true). Here a one-member object keeps the
    // member-provenance path over a real provider column.

    /// <summary>Plain enum member: JSON output equals the default <see cref="JsonSerializer"/> numeric form.</summary>
    [Fact]
    public void EnumStorage_NumericMember_ShouldMatchJsonSerializer() => EnumStorageNumericMember(_sut);

    /// <summary>
    /// A plain enum over a numeric provider column must stream as the underlying number. The expected
    /// document is built by STJ from the known seed values, so no nextorm read path is involved.
    /// </summary>
    internal static void EnumStorageNumericMember(TestDataRepository sut)
    {
        var expected = JsonSerializer.Serialize(new[]
        {
            new { State = StreamEnumState.Active },
            new { State = StreamEnumState.Disabled },
            new { State = StreamEnumState.Unknown },
        });

        var actual = WriteJsonBytes(
            sut.DataProvider.From<IStreamEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.State }));

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>A stock STJ string-enum type attribute yields the STJ string names on every provider.</summary>
    [Fact]
    public void EnumStorage_StringMember_ShouldMatchJsonSerializer() => EnumStorageStringMember(_sut);

    /// <summary>
    /// The generic stock STJ form (<c>JsonStringEnumConverter&lt;TEnum&gt;</c>) declared on the enum type
    /// must resolve to the same STJ names on every provider (the converter is resolved in the shape plan,
    /// not by the provider, so the byte-for-byte contract is provider-independent).
    /// </summary>
    [Fact]
    public void EnumStorage_GenericStringMember_ShouldMatchJsonSerializer() => EnumStorageGenericStringMember(_sut);

    /// <summary>
    /// A supported generic string-enum converter attribute on the enum type must produce exactly the STJ
    /// output (names) even though the column is stored as the integer value. Mirrors the non-generic body
    /// for providers that do not derive <see cref="CommonTestSuite"/>.
    /// </summary>
    internal static void EnumStorageGenericStringMember(TestDataRepository sut)
    {
        var expected = JsonSerializer.Serialize(new[]
        {
            new { State = GenericStreamEnumState.Active },
            new { State = GenericStreamEnumState.Disabled },
            new { State = GenericStreamEnumState.Unknown },
        });

        var actual = WriteJsonBytes(
            sut.DataProvider.From<IGenericStreamEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.State }));

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>
    /// A supported string-enum converter attribute on the enum type must produce exactly the STJ output
    /// (names) even though the column is stored as the integer value.
    /// </summary>
    internal static void EnumStorageStringMember(TestDataRepository sut)
    {
        var expected = JsonSerializer.Serialize(new[]
        {
            new { State = StringStreamEnumState.Active },
            new { State = StringStreamEnumState.Disabled },
            new { State = StringStreamEnumState.Unknown },
        });

        var actual = WriteJsonBytes(
            sut.DataProvider.From<IStringStreamEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.State }));

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>Flat object member: enum number plus a nullable enum (SQL NULL stays JSON null).</summary>
    [Fact]
    public void EnumStorage_FlatObject_ShouldMatchJsonSerializer() => EnumStorageFlatObject(_sut);

    /// <summary>
    /// The flat object form resolves the enum member provenance and keeps the existing null rules; the
    /// expected document is STJ over the same shape and seed values.
    /// </summary>
    internal static void EnumStorageFlatObject(TestDataRepository sut)
    {
        var expected = JsonSerializer.Serialize(new[]
        {
            new { Id = 1, State = StreamEnumState.Active, NullableState = (StreamEnumState?)null },
            new { Id = 2, State = StreamEnumState.Disabled, NullableState = (StreamEnumState?)StreamEnumState.Active },
            new { Id = 3, State = StreamEnumState.Unknown, NullableState = (StreamEnumState?)StreamEnumState.Disabled },
        });

        var actual = WriteJsonBytes(
            sut.DataProvider.From<IStreamEnumEntity>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.State, x.NullableState }));

        actual.Should().Equal(Utf8(expected));
    }

    /// <summary>Records the provider's actual <see cref="IDataRecord.GetFieldType"/> for the enum column.</summary>
    [Fact]
    public void EnumStorage_FieldType_ShouldBeNumeric() => RecordEnumStorageFieldType(_sut, Provider.Name);

    private static readonly object FieldTypeDumpGate = new();

    /// <summary>
    /// Reads the raw provider field type of the enum-backed column and asserts it is a native numeric
    /// type (the contract the numeric JSON writer relies on). With <c>NEXTORM_ENUM_FIELDTYPE_DUMP</c>
    /// set to a file path it appends <c>provider\tfullTypeName\tvalue</c>, which is how the D178 provider
    /// matrix is captured from a real provider run.
    /// </summary>
    internal static void RecordEnumStorageFieldType(TestDataRepository sut, string provider)
    {
        var context = (DataContext)sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand("select state from enum_entity where id = 1");
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();

        var fieldType = reader.GetFieldType(0);
        fieldType.Name.Should().BeOneOf("SByte", "Byte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64");

        var dump = Environment.GetEnvironmentVariable("NEXTORM_ENUM_FIELDTYPE_DUMP");
        if (!string.IsNullOrEmpty(dump))
        {
            lock (FieldTypeDumpGate)
                File.AppendAllText(dump, $"{provider}\t{fieldType.FullName}\t{reader.GetValue(0)}\n");
        }
    }

    /// <summary>Plain numeric enum stored by every provider; the underlying value is an <c>int</c>.</summary>
    public enum StreamEnumState
    {
        Unknown = 0,
        Active = 7,
        Disabled = -3,
    }

    /// <summary>The supported stock STJ string-enum converter form declared on the enum type.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StringStreamEnumState
    {
        Unknown = 0,
        Active = 7,
        Disabled = -3,
    }

    /// <summary>The generic stock STJ string-enum converter form declared on the enum type.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GenericStreamEnumState>))]
    public enum GenericStreamEnumState
    {
        Unknown = 0,
        Active = 7,
        Disabled = -3,
    }

    [SqlTable("enum_entity")]
    public interface IStreamEnumEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("state")]
        StreamEnumState State { get; set; }

        [Column("nullable_state")]
        StreamEnumState? NullableState { get; set; }
    }

    [SqlTable("enum_entity")]
    public interface IStringStreamEnumEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("state")]
        StringStreamEnumState State { get; set; }
    }

    [SqlTable("enum_entity")]
    public interface IGenericStreamEnumEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("state")]
        GenericStreamEnumState State { get; set; }
    }
}
