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
}
