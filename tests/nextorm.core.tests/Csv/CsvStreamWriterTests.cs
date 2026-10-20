using System.Buffers;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Unit coverage for the box-free CSV column plan (<c>CsvStreamWriter</c>), the RFC 4180 escaping and
/// the <c>WriteCsv</c> guards. The compiled plan is driven over a probe <see cref="DbDataReader"/>
/// whose <c>GetValue</c> throws, so any <c>object</c>-based fallback on the write path fails the test.
/// </summary>
public class CsvStreamWriterTests
{
    private readonly IDataContext _ctx;

    public CsvStreamWriterTests(IDataContext ctx) => _ctx = ctx;

    private static Func<SelectExpression, System.Linq.Expressions.Expression, System.Linq.Expressions.Expression> Mapper
        => (column, param) => RowMapperFactory.MapColumn(column, param);

    public static TheoryData<Type, object, string> ScalarCases() => new()
    {
        { typeof(bool), true, "true" },
        { typeof(bool), false, "false" },
        { typeof(byte), (byte)200, "200" },
        { typeof(short), (short)-1234, "-1234" },
        { typeof(int), 42, "42" },
        { typeof(uint), 4_000_000_000u, "4000000000" },
        { typeof(long), -9_000_000_000L, "-9000000000" },
        { typeof(ulong), 18_000_000_000_000_000_000UL, "18000000000000000000" },
        { typeof(float), 1.5f, "1.5" },
        { typeof(double), 1.5d, "1.5" },
        { typeof(decimal), 1.25m, "1.25" },
        { typeof(Guid), Guid.Parse("d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e"), "d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e" },
        { typeof(DateTime), new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) },
        { typeof(DateTimeOffset), new DateTimeOffset(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)), new DateTimeOffset(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)).ToString("O", CultureInfo.InvariantCulture) },
        { typeof(TimeSpan), new TimeSpan(1, 2, 3), new TimeSpan(1, 2, 3).ToString("c", CultureInfo.InvariantCulture) },
        { typeof(string), "hello", "hello" },
    };

    [Theory]
    [MemberData(nameof(ScalarCases))]
    public void Plan_UsesTypedGetter_AndFormatsScalar(Type type, object value, string expected)
    {
        var column = new SelectExpression(type) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be(expected + "\r\n");
        reader.Calls.Should().NotBeEmpty("a typed getter must be used");
        reader.Calls.Should().OnlyContain(call => !call.StartsWith("GetValue", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_FormatsByteArrayAsBase64_ThroughTypedGetter()
    {
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "Data" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, new byte[] { 1, 2, 3 });

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("AQID\r\n");
        reader.Calls.Should().Contain("GetFieldValue<Byte[]>(0)", "byte[] must be read through the typed accessor");
        reader.Calls.Should().OnlyContain(call => !call.StartsWith("GetValue", StringComparison.Ordinal));
    }

    [Fact]
    public void ObjectBridgedConverterColumn_ThrowsBeforeHeader()
    {
        var column = new SelectExpression(typeof(int))
        {
            Index = 0,
            PropertyName = "Bridge",
            Converter = new ObjectBridgeConverter(),
        };
        using var destination = new MemoryStream();

        var act = () =>
        {
            var plan = CsvStreamWriter.Build([column], Mapper);
            using var buffer = new CsvRowBuffer(new CsvDialect(','));
            CsvStreamWriter.WriteHeader(plan, buffer);
            destination.Write(buffer.Written);
        };

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Bridge*")
            .WithMessage("*ConvertFromProvider*");
        destination.Length.Should().Be(0, "the plan must be rejected before the header is written");
    }

    [Fact]
    public void NonNativeDurationColumn_ThrowsBeforeHeader()
    {
        // The SQLite mapping (no native duration) keeps the TimeSpan in an integer column and widens
        // it through IDataRecord.GetValue(object), which boxes on every row.
        var column = new SelectExpression(typeof(TimeSpan)) { Index = 0, PropertyName = "Duration" };

        var act = () => CsvStreamWriter.Build([column], (c, p) => RowMapperFactory.MapColumn(c, p, supportsNativeDuration: false));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Duration*")
            .WithMessage("*GetValue*");
    }

    [Fact]
    public void ValidateProjection_ObjectBridgedConverter_ThrowsBeforeExecution()
    {
        // A direct IPropertyValueConverter can only be invoked through the object-based bridge; the
        // pre-execution guard must reject it from static information, before any reader is opened.
        var column = new SelectExpression(typeof(int))
        {
            Index = 0,
            PropertyName = "Bridge",
            Converter = new ObjectBridgeConverter(),
        };

        var act = () => CsvStreamWriter.ValidateProjection([column], Mapper, _ => false);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Bridge*")
            .WithMessage("*ConvertFromProvider*");
    }

    [Fact]
    public void ValidateProjection_ValueTypeGetValue_RejectedUnlessProviderMapsTyped()
    {
        // The SQLite mapping keeps a TimeSpan in an integer column and widens it through GetValue.
        var column = new SelectExpression(typeof(TimeSpan)) { Index = 0, PropertyName = "Duration" };
        Func<SelectExpression, System.Linq.Expressions.Expression, System.Linq.Expressions.Expression> map =
            (c, p) => RowMapperFactory.MapColumn(c, p, supportsNativeDuration: false);

        var rejected = () => CsvStreamWriter.ValidateProjection([column], map, _ => false);
        rejected.Should().Throw<NotSupportedException>().WithMessage("*GetValue*");

        // A provider that declares the typed hook (SQL Server numeric widening) is not falsely rejected.
        var allowed = () => CsvStreamWriter.ValidateProjection([column], map, _ => true);
        allowed.Should().NotThrow();
    }

    [Fact]
    public void ValidateProjection_UnsupportedType_ThrowsBeforeExecution()
    {
        var column = new SelectExpression(typeof(Uri)) { Index = 0, PropertyName = "Link" };

        var act = () => CsvStreamWriter.ValidateProjection([column], Mapper, _ => true);

        act.Should().Throw<NotSupportedException>().WithMessage("*Link*");
    }

    [Theory]
    [InlineData(typeof(sbyte))]
    [InlineData(typeof(ushort))]
    public void NarrowIntegerTypes_AreRejectedWithoutClaimingSupport(Type type)
    {
        // nextorm's own row mapper cannot read sbyte/ushort, so the terminal must not advertise them.
        var column = new SelectExpression(type) { Index = 0, PropertyName = "N" };

        var act = () => CsvStreamWriter.Build([column], Mapper);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*'N'*")
            .WithMessage("*Supported types are bool, byte, short*");
    }

    [Theory]
    [InlineData(typeof(int?), 7, "7\r\n")]
    [InlineData(typeof(int?), null, "\\N\r\n")]
    [InlineData(typeof(string), null, "\\N\r\n")]
    [InlineData(typeof(string), "", "\r\n")]
    [InlineData(typeof(string), "x", "x\r\n")]
    public void Plan_WritesNullAsMarkerAndEmptyStringAsEmptyField(Type type, object? value, string expected)
    {
        var column = new SelectExpression(type) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be(expected);
    }

    [Theory]
    [InlineData("plain", "plain\r\n")]
    [InlineData("a,b", "\"a,b\"\r\n")]
    [InlineData("a\"b", "\"a\"\"b\"\r\n")]
    [InlineData("a\rb", "\"a\rb\"\r\n")]
    [InlineData("a\nb", "\"a\nb\"\r\n")]
    [InlineData(" leading", " leading\r\n")]
    [InlineData("trailing ", "trailing \r\n")]
    [InlineData("", "\r\n")]
    public void Escaping_QuotesOnlyWhenRequired(string value, string expected)
    {
        var column = new SelectExpression(typeof(string)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be(expected);
    }

    [Theory]
    [InlineData(';', "a;b", "\"a;b\"\r\n")]
    [InlineData(';', "a,b", "a,b\r\n")]
    [InlineData('|', "a|b", "\"a|b\"\r\n")]
    public void Escaping_HonoursCustomDelimiter(char delimiter, string value, string expected)
    {
        var column = new SelectExpression(typeof(string)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = new CsvRowBuffer(new CsvDialect(delimiter));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be(expected);
    }

    [Fact]
    public void Header_UsesPropertyNameWithColumnFallback_AndEscapes()
    {
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = null },
            new SelectExpression(typeof(string)) { Index = 2, PropertyName = "a,b" },
        };
        var plan = CsvStreamWriter.Build(columns, Mapper);

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteHeader(plan, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("Id,Column2,\"a,b\"\r\n");
    }

    [Fact]
    public void Header_ExcelMode_GuardsAFormulaPrefix()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "=x" };
        var plan = CsvStreamWriter.Build([column], Mapper);

        using var buffer = PolicyBuffer(new CsvStreamOptions { ExcelMode = true });
        CsvStreamWriter.WriteHeader(plan, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("'=x\r\n");
    }

    [Fact]
    public void Header_EqualToTheNullMarker_IsQuoted()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "AQID" };
        var plan = CsvStreamWriter.Build([column], Mapper);

        using var buffer = PolicyBuffer(new CsvStreamOptions { NullMarker = "AQID" });
        CsvStreamWriter.WriteHeader(plan, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("\"AQID\"\r\n");
    }

    [Fact]
    public void EmptyResult_WriterLoop_WritesHeaderOnlyWhenIncluded()
    {
        // Drive the real zero-row loop (not just WriteHeader), so the include-header branch and the
        // reader loop are both exercised with a reader that reports no rows.
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false);

        using var destination = new MemoryStream();
        CsvStreamWriter.Write(reader, destination, plan, new CsvStreamOptions(), TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("Id\r\n");
    }

    [Fact]
    public void EmptyResult_WriterLoop_WithoutHeader_WritesNothing()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false);

        using var destination = new MemoryStream();
        CsvStreamWriter.Write(
            reader,
            destination,
            plan,
            new CsvStreamOptions { IncludeHeader = false },
            TestContext.Current.CancellationToken);

        destination.Length.Should().Be(0, "an empty result with IncludeHeader=false must write zero bytes");
    }

    [Theory]
    [InlineData('"')]
    [InlineData('\r')]
    [InlineData('\n')]
    public void InvalidDelimiter_Throws(char delimiter)
    {
        var act = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions { Delimiter = delimiter });

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData('\uD800')] // high surrogate
    [InlineData('\uDC00')] // low surrogate
    public void LoneSurrogateDelimiter_Throws(char delimiter)
    {
        // A lone surrogate cannot be encoded to valid UTF-8: Encoding.UTF8 would silently emit U+FFFD,
        // so the options must reject it rather than write a corrupted delimiter.
        var act = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions { Delimiter = delimiter });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnsupportedColumnType_ThrowsBeforeAnyRow()
    {
        var column = new SelectExpression(typeof(Uri)) { Index = 0, PropertyName = "Link" };

        var act = () => CsvStreamWriter.Build([column], Mapper);

        act.Should().Throw<NotSupportedException>().WithMessage("*Link*");
    }

    [Fact]
    public void WriteCsv_InMemory_ShouldThrowNotSupported()
    {
        using var destination = new MemoryStream();

        var act = () => _ctx.From<LobTestEntity>().WriteCsv(destination);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task WriteCsvAsync_InMemory_ShouldThrowNotSupported()
    {
        using var destination = new MemoryStream();

        var act = async () => await _ctx.From<LobTestEntity>().WriteCsvAsync(destination);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void WriteCsv_AfterContextDispose_ShouldThrowObjectDisposed()
    {
        var ctx = new InMemoryDataContext();
        var command = ctx.From<LobTestEntity>().ToParentCommand();
        ctx.Dispose();
        using var destination = new MemoryStream();

        var act = () => command.WriteCsv(destination);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Build_NoProjectedColumns_Throws()
    {
        var act = () => CsvStreamWriter.Build(null, Mapper);
        act.Should().Throw<InvalidOperationException>().WithMessage("*no projected columns*");

        var empty = () => CsvStreamWriter.Build([], Mapper);
        empty.Should().Throw<InvalidOperationException>().WithMessage("*no projected columns*");
    }

    [Theory]
    [InlineData("Привет мир")]
    [InlineData("café")]
    [InlineData("emoji 😀 ok")]
    public void Plan_WritesNonAsciiContentAsExactUtf8Bytes(string value)
    {
        var column = new SelectExpression(typeof(string)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        buffer.Written.ToArray().Should().Equal(Encoding.UTF8.GetBytes(value + "\r\n"));
    }

    [Theory]
    [InlineData(4 * 1024 + 1)] // one byte over the initial 4 KB row-buffer capacity
    [InlineData(10 * 1024)]    // far beyond it, so the buffer must grow (and re-grow)
    public void Plan_LongField_GrowsTheRowBuffer(int length)
    {
        var value = new string('x', length);
        var column = new SelectExpression(typeof(string)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        buffer.Written.Length.Should().Be(length + 2, "the CRLF row terminator is appended");
        buffer.Written.ToArray().Should().Equal(Encoding.UTF8.GetBytes(value + "\r\n"));
    }

    [Fact]
    public void RowBuffer_FieldExactlyAtTheInitialCapacity_IsWrittenIntact()
    {
        var value = new string('y', 4 * 1024);

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        buffer.WriteUtf8(value);

        buffer.Written.Length.Should().Be(4 * 1024);
        Encoding.UTF8.GetString(buffer.Written).Should().Be(value);
    }

    [Theory]
    [InlineData("ab", "\"ab\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    public void RowBuffer_WriteQuoted_EscapesInnerQuotes(string value, string expected)
    {
        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        buffer.WriteQuoted(Encoding.UTF8.GetBytes(value));

        Encoding.UTF8.GetString(buffer.Written).Should().Be(expected);
    }

    [Fact]
    public void RowBuffer_Dispose_ReturnsTheRentedArrayToThePool()
    {
        var buffer = new CsvRowBuffer(new CsvDialect(','));
        var rented = (byte[])typeof(CsvRowBuffer)
            .GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(buffer)!;

        buffer.Dispose();

        ((byte[])typeof(CsvRowBuffer)
            .GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(buffer)!).Should().BeEmpty("disposing must drop the reference to the rented array");

        // ArrayPool<byte>.Shared keeps a TLS stack per size, so the very next same-size rent on this
        // thread hands the disposed array back — proof it was returned rather than leaked.
        var again = ArrayPool<byte>.Shared.Rent(rented.Length);
        try
        {
            ((object)again).Should().BeSameAs(rented, "the rented array must be returned to ArrayPool on Dispose");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(again);
        }
    }

    [Fact]
    public void Header_DuplicateAndEmptyNames_KeepOrderAndUseColumnFallback()
    {
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "X" },
            new SelectExpression(typeof(int)) { Index = 1, PropertyName = "X" },
            new SelectExpression(typeof(int)) { Index = 2, PropertyName = "" },
        };
        var plan = CsvStreamWriter.Build(columns, Mapper);

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteHeader(plan, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("X,X,Column3\r\n");
    }

    [Fact]
    public void NestedEntityAndCollectionProjections_AreRejectedBeforeHeader()
    {
        using var destination = new MemoryStream();

        var nested = () => WriteHeaderTo(
            new SelectExpression(typeof(NestedProjection)) { Index = 0, PropertyName = "Nested" }, destination);
        var collection = () => WriteHeaderTo(
            new SelectExpression(typeof(List<int>)) { Index = 0, PropertyName = "Items" }, destination);

        nested.Should().Throw<NotSupportedException>().WithMessage("*Nested*");
        collection.Should().Throw<NotSupportedException>().WithMessage("*Items*");
        destination.Length.Should().Be(0, "the projections must be rejected before the header is written");
    }

    [Theory]
    [InlineData("a§b", "\"a§b\"\r\n")]
    [InlineData("abc", "abc\r\n")]
    public void Escaping_MultibyteDelimiter_QuotesOnlyWhenPresent(string value, string expected)
    {
        var column = new SelectExpression(typeof(string)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = new CsvRowBuffer(new CsvDialect('§'));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be(expected);
    }

    [Fact]
    public void Base64Field_IsQuotedWhenTheDelimiterIsABase64Character()
    {
        // 0xFB 0xFF encodes to "+/8=" so a '/' delimiter can occur verbatim in the payload.
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "D" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, new byte[] { 0xFB, 0xFF });

        using var buffer = new CsvRowBuffer(new CsvDialect('/'));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("\"+/8=\"\r\n");
    }

    [Fact]
    public void Base64Field_EqualToTheNullMarker_IsQuoted()
    {
        // 0x01 0x02 0x03 encodes to "AQID"; with that NULL marker the payload must be quoted so a
        // reader cannot mistake a present value for NULL.
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "D" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, new byte[] { 1, 2, 3 });

        using var buffer = PolicyBuffer(new CsvStreamOptions { NullMarker = "AQID" });
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("\"AQID\"\r\n");
    }

    [Fact]
    public void Base64Field_ExcelMode_GuardsALeadingPlus()
    {
        // 0xFB 0xFF encodes to "+/8="; a leading '+' is an Excel formula prefix and must be guarded.
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "D" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, new byte[] { 0xFB, 0xFF });

        using var buffer = PolicyBuffer(new CsvStreamOptions { ExcelMode = true });
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("'+/8=\r\n");
    }

    [Fact]
    public void ValueTransform_AppliesToByteArrayColumns_ThroughTheFieldPolicy()
    {
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "D" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, new byte[] { 1, 2, 3 });

        using var buffer = PolicyBuffer(new CsvStreamOptions { ExcelMode = true, ValueTransform = _ => "=x" });
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("'=x\r\n");
    }

    [Fact]
    public void ProviderWidenedValueType_IsRejected()
    {
        // The SQL Server buffered numeric mapping reads through GetValue(object) and widens with
        // Convert.ChangeType; the CSV terminal now uses the typed provider hook instead, so this shape
        // is rejected (a missing provider hook means the column would box on every row).
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "V" };
        var getValue = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue), [typeof(int)])!;
        var changeType = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;

        var act = () => CsvStreamWriter.Build([column], (_, record) =>
            System.Linq.Expressions.Expression.Convert(
                System.Linq.Expressions.Expression.Call(
                    changeType,
                    System.Linq.Expressions.Expression.Call(record, getValue, System.Linq.Expressions.Expression.Constant(0)),
                    System.Linq.Expressions.Expression.Constant(typeof(int))),
                typeof(int)));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*'V'*")
            .WithMessage("*GetValue*");
    }

    public static TheoryData<Type, Type, object, string> NumericStorageCases() => new()
    {
        { typeof(long), typeof(int), 42L, "42" },
        { typeof(decimal), typeof(int), 42m, "42" },
        { typeof(decimal), typeof(long), 42m, "42" },
        { typeof(int), typeof(int), 42, "42" },
        { typeof(long), typeof(int?), 7L, "7" },
    };

    [Theory]
    [MemberData(nameof(NumericStorageCases))]
    public void Plan_WidenedNumericStorage_UsesStorageTypedGetterAndConversion(
        Type storage, Type projected, object value, string expected)
    {
        // The provider hook receives the reader's storage type and must read it with a typed getter
        // (no GetValue/ChangeType) before converting to the projected type; the CSV bytes must match.
        var column = new SelectExpression(projected) { Index = 0, PropertyName = "V" };
        var reader = new ProbeReader(false, value);
        Type? seenStorage = null;

        var plan = CsvStreamWriter.Build([column], reader, (c, record, st) =>
        {
            seenStorage = st;
            var getter = StorageGetter(st);
            var conversion = typeof(Convert).GetMethod($"To{TargetName(c.PropertyType)}", [st])!;
            return Expression.Call(conversion, Expression.Call(record, getter, Expression.Constant(c.Index)));
        });

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be(expected + "\r\n");
        seenStorage.Should().Be(storage, "the reader's field type must be forwarded to the provider hook");
        reader.Calls.Should().OnlyContain(call => !call.StartsWith("GetValue", StringComparison.Ordinal));
        reader.SchemaCalls.Should().HaveCount(1, "GetFieldType is read once at bind time, not per row");
    }

    [Fact]
    public void Plan_NullableWidenedNumericStorage_WritesNullMarkerForDbNull()
    {
        // The provider hook owns the NULL policy; here the probe mimics it by folding DBNull before the
        // typed getter, which the CSV writer must not re-read.
        var column = new SelectExpression(typeof(int?)) { Index = 0, PropertyName = "V" };
        var reader = new ProbeReader(false, (object?)null) { FieldTypes = [typeof(long)] };

        var plan = CsvStreamWriter.Build([column], reader, (c, record, st) =>
        {
            var isDbNull = Expression.Call(record, typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!, Expression.Constant(c.Index));
            var typed = TypedNumericMapper(c, record, st);
            return Expression.Condition(isDbNull, Expression.Constant(null, c.PropertyType), Expression.Convert(typed, c.PropertyType));
        });

        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("\\N\r\n");
        reader.Calls.Should().NotContain(call => call.StartsWith("GetValue", StringComparison.Ordinal));
    }

    /// <summary>Mimics the SQL Server typed hook: storage-typed getter plus a typed <c>Convert.To</c>.</summary>
    private static Expression TypedNumericMapper(SelectExpression column, Expression record, Type storageType)
    {
        var conversion = typeof(Convert).GetMethod($"To{TargetName(column.PropertyType)}", [storageType])!;
        return Expression.Call(
            conversion,
            Expression.Call(record, StorageGetter(storageType), Expression.Constant(column.Index)));
    }

    private static string TargetName(Type type) => (Nullable.GetUnderlyingType(type) ?? type).Name;

    private static MethodInfo StorageGetter(Type storage) => storage switch
    {
        _ when storage == typeof(byte) => typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetByte))!,
        _ when storage == typeof(short) => typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt16))!,
        _ when storage == typeof(int) => typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt32))!,
        _ when storage == typeof(long) => typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt64))!,
        _ when storage == typeof(float) => typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetFloat))!,
        _ when storage == typeof(double) => typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDouble))!,
        _ when storage == typeof(decimal) => typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDecimal))!,
        _ => throw new NotSupportedException($"No storage getter for {storage.Name}."),
    };

    [Fact]
    public void Formatter_ValueThatDoesNotFitTheScratchBuffer_Throws()
    {
        using var buffer = new CsvRowBuffer(new CsvDialect(','));

        var act = () => CsvValueFormatter.WriteFormatted(buffer, default(OversizedFormattable));

        act.Should().Throw<InvalidOperationException>().WithMessage("*scratch buffer*");
    }

    [Fact]
    public void WriteCsv_PreCancelled_ThrowsBeforeWriting()
    {
        using var destination = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _ctx.From<LobTestEntity>().WriteCsv(destination, null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        destination.Length.Should().Be(0);
    }

    [Fact]
    public async Task WriteCsvAsync_PreCancelled_ThrowsBeforeWriting()
    {
        using var destination = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _ctx.From<LobTestEntity>().WriteCsvAsync(destination, null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        destination.Length.Should().Be(0);
    }

    [Fact]
    public void Dialect_ByteSpan_DetectsMultibyteDelimiter()
    {
        // A non-ASCII delimiter encodes to more than one UTF-8 byte, so the byte-span check must run
        // the multi-byte scan (used by the formatted numeric/date write path).
        var dialect = new CsvDialect('§');

        dialect.NeedsQuoting(Encoding.UTF8.GetBytes("a§b")).Should().BeTrue();
        dialect.NeedsQuoting(Encoding.UTF8.GetBytes("ab")).Should().BeFalse();
        dialect.NeedsQuoting(new byte[] { (byte)'a', 0xC2, (byte)'b' }).Should().BeFalse("only the first delimiter byte matches");
    }

    [Fact]
    public void RowBuffer_EmptyWrites_AreIgnored()
    {
        using var buffer = new CsvRowBuffer(new CsvDialect(','));

        buffer.Write([]);
        buffer.WriteUtf8(ReadOnlySpan<char>.Empty);
        buffer.WriteBase64Field(null);
        buffer.WriteBase64Field([]);

        buffer.Written.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void NullMarker_CustomMarker_DistinguishesNullFromEmptyString()
    {
        var options = new CsvStreamOptions { NullMarker = "(null)" };

        WriteSingleString(null, options).Should().Be("(null)\r\n");
        WriteSingleString("", options).Should().Be("\r\n");
    }

    [Theory]
    [InlineData("\\N", "\\N", "\"\\N\"\r\n")]
    [InlineData("\\N", "NULL", "\\N\r\n")]
    [InlineData("NULL", "NULL", "\"NULL\"\r\n")]
    public void NullMarker_DataLiteralEqualToTheMarker_IsQuoted(string data, string marker, string expected)
    {
        WriteSingleString(data, new CsvStreamOptions { NullMarker = marker }).Should().Be(expected);
    }

    [Theory]
    [InlineData("=SUM(A1)")]
    [InlineData("+1")]
    [InlineData("-x")]
    [InlineData("@x")]
    public void ExcelMode_Off_PreservesFormulaPrefixes(string value)
    {
        WriteSingleString(value, new CsvStreamOptions { ExcelMode = false }).Should().Be(value + "\r\n");
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)\r\n")]
    [InlineData("+1", "'+1\r\n")]
    [InlineData("-x", "'-x\r\n")]
    [InlineData("@x", "'@x\r\n")]
    public void ExcelMode_On_GuardsEveryFormulaPrefix(string value, string expected)
    {
        WriteSingleString(value, new CsvStreamOptions { ExcelMode = true }).Should().Be(expected);
    }

    [Theory]
    [InlineData("=a,b", "\"'=a,b\"\r\n")]
    [InlineData("=a\"b", "\"'=a\"\"b\"\r\n")]
    public void ExcelMode_GuardedField_IsQuotedWhenEscapingIsRequired(string value, string expected)
    {
        // The apostrophe guard lands inside the RFC 4180 quotes, so a guarded field that also needs
        // escaping (delimiter or quote) stays a single field whose first character is the guard.
        WriteSingleString(value, new CsvStreamOptions { ExcelMode = true }).Should().Be(expected);
    }

    [Fact]
    public void ExcelMode_On_NegativeNumber_IsGuarded()
    {
        WriteSingleValue(-1, new CsvStreamOptions { ExcelMode = true }).Should().Be("'-1\r\n");
    }

    [Fact]
    public void ExcelMode_On_Base64StartingWithPlus_IsGuarded()
    {
        // 0xF8 encodes to "+A==", so the formatted byte[] column starts with the '+' formula prefix.
        WriteSingleValue(new byte[] { 0xF8 }, new CsvStreamOptions { ExcelMode = true }).Should().Be("'+A==\r\n");
    }

    [Fact]
    public void NullMarker_CollidingWithRenderedBase64_IsQuoted()
    {
        // byte[] { 1, 2, 3 } renders as Base64 "AQID"; a marker equal to that text must force-quote
        // the rendered value so it cannot be read back as NULL.
        WriteSingleValue(new byte[] { 1, 2, 3 }, new CsvStreamOptions { NullMarker = "AQID" })
            .Should().Be("\"AQID\"\r\n");
    }

    [Fact]
    public void ExcelMode_On_NegativeNumber_CollidingWithGuardedMarker_IsQuoted()
    {
        // The guard turns -1 into '-1; with a marker equal to "'-1" the guarded value would collide with
        // NULL, so the guarded result must itself be quoted (mirrors the string-path collision check).
        WriteSingleValue(-1, new CsvStreamOptions { NullMarker = "'-1", ExcelMode = true })
            .Should().Be("\"'-1\"\r\n");
    }

    [Fact]
    public void ExcelMode_On_Base64_CollidingWithGuardedMarker_IsQuoted()
    {
        // 0xF8 renders as "+A=="; the Excel guard turns it into "'+A==". With that exact text as the NULL
        // marker the guarded field must be quoted so it stays distinguishable from NULL.
        WriteSingleValue(new byte[] { 0xF8 }, new CsvStreamOptions { NullMarker = "'+A==", ExcelMode = true })
            .Should().Be("\"'+A==\"\r\n");
    }

    [Fact]
    public void ValueTransform_RunsBeforeExcelGuard()
    {
        var options = new CsvStreamOptions { ExcelMode = true, ValueTransform = value => "=" + value };

        WriteSingleString("alpha", options).Should().Be("'=alpha\r\n");
    }

    [Fact]
    public void ValueTransform_RunsBeforeEscaping_SoTheGuardedResultIsEscaped()
    {
        var options = new CsvStreamOptions { ExcelMode = true, ValueTransform = _ => "=a,b" };

        WriteSingleString("x", options).Should().Be("\"'=a,b\"\r\n");
    }

    [Fact]
    public void ValueTransform_AppliesToTypedColumns()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, 42);
        using var buffer = PolicyBuffer(new CsvStreamOptions { ValueTransform = _ => "transformed" });

        CsvStreamWriter.WriteRow(plan, reader, buffer);

        Encoding.UTF8.GetString(buffer.Written).Should().Be("transformed\r\n");
    }

    [Fact]
    public void ValueTransform_IsNotInvokedForNull()
    {
        var called = false;
        var options = new CsvStreamOptions
        {
            ValueTransform = _ =>
            {
                called = true;
                return "x";
            },
        };

        WriteSingleString(null, options).Should().Be("\\N\r\n");
        called.Should().BeFalse("NULL is written as the marker and never reaches the transform");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateOptions_EmptyNullMarker_Throws(string? marker)
    {
        var act = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions { NullMarker = marker! });

        act.Should().Throw<ArgumentException>().WithMessage("*NULL marker*");
    }

    [Theory]
    [InlineData("a,b")]
    [InlineData("a\"b")]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    public void ValidateOptions_UnescapableNullMarker_Throws(string marker)
    {
        var act = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions { NullMarker = marker });

        act.Should().Throw<ArgumentException>().WithMessage("*NULL marker*");
    }

    [Fact]
    public void ValidateOptions_LoneSurrogateNullMarker_Throws()
    {
        // A lone surrogate cannot be encoded to valid UTF-8: Encoding.UTF8 would silently emit U+FFFD,
        // so the NULL marker must be rejected instead of written corrupted. The values are built in
        // source (not InlineData) because a test-case serializer cannot round-trip a lone surrogate.
        Action loneHigh = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions { NullMarker = "\uD800" });
        Action loneLow = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions { NullMarker = "\uDC00" });
        Action embedded = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions { NullMarker = "a\uD800b" });

        loneHigh.Should().Throw<ArgumentException>().WithMessage("*NULL marker*");
        loneLow.Should().Throw<ArgumentException>().WithMessage("*NULL marker*");
        embedded.Should().Throw<ArgumentException>().WithMessage("*NULL marker*");
    }

    [Fact]
    public void ValidateOptions_ValidSurrogatePairNullMarker_IsAccepted()
    {
        var act = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions { NullMarker = "x\uD83D\uDE00y" });

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOptions_DefaultMarker_IsValid()
    {
        var act = () => CsvStreamWriter.ValidateOptions(new CsvStreamOptions());

        act.Should().NotThrow();
    }

    private static string WriteSingleString(string? value, CsvStreamOptions options)
    {
        var column = new SelectExpression(typeof(string)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = PolicyBuffer(options);
        CsvStreamWriter.WriteRow(plan, reader, buffer);
        return Encoding.UTF8.GetString(buffer.Written);
    }

    private static string WriteSingleValue<T>(T value, CsvStreamOptions options)
    {
        // Drives the provider-typed formatter (numeric/byte[]) under the call's policy so the Excel
        // guard and marker-collision rules are asserted for non-string columns too.
        var column = new SelectExpression(typeof(T)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], Mapper);
        var reader = new ProbeReader(allowGetValue: false, value);

        using var buffer = PolicyBuffer(options);
        CsvStreamWriter.WriteRow(plan, reader, buffer);
        return Encoding.UTF8.GetString(buffer.Written);
    }

    private static CsvRowBuffer PolicyBuffer(CsvStreamOptions options)
        => new(new CsvDialect(options.Delimiter), new CsvFieldPolicy(options.NullMarker, options.ExcelMode, options.ValueTransform));

    private static void WriteHeaderTo(SelectExpression column, Stream destination)
    {
        var plan = CsvStreamWriter.Build([column], Mapper);
        using var buffer = new CsvRowBuffer(new CsvDialect(','));
        CsvStreamWriter.WriteHeader(plan, buffer);
        destination.Write(buffer.Written);
    }

    private sealed class NestedProjection
    {
        public int Id { get; set; }
    }

    /// <summary>An <see cref="IUtf8SpanFormattable"/> that never fits, so the scratch-buffer guard fires.</summary>
    private struct OversizedFormattable : IUtf8SpanFormattable
    {
        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            bytesWritten = 0;
            return false;
        }
    }

    /// <summary>An <see cref="IPropertyValueConverter"/> implemented directly, so the mapper can only
    /// invoke it through the object-based <c>ConvertFromProvider(object)</c> bridge.</summary>
    private sealed class ObjectBridgeConverter : IPropertyValueConverter
    {
        public Type ProviderType => typeof(int);

        public bool ConvertsNulls => false;

        public object? ConvertToProvider(object? model) => model;

        public object? ConvertFromProvider(object? provider) => (int)(provider ?? 0);
    }

    /// <summary>
    /// A <see cref="DbDataReader"/> whose typed getters record their ordinal and whose
    /// <c>GetValue(int)</c> throws unless explicitly allowed; any object-based fallback on the CSV
    /// write path therefore fails loudly. It is also a <see cref="DbDataReader"/> because the default
    /// column mapper reads <c>uint</c>/<c>ulong</c>/<c>TimeSpan</c>/<c>DateTimeOffset</c> through
    /// <c>GetFieldValue&lt;T&gt;</c>.
    /// </summary>
    private sealed class ProbeReader(bool allowGetValue, params object?[] values) : DbDataReader
    {
        private readonly object?[] _values = values;

        public List<string> Calls { get; } = [];

        /// <summary>Ordinals whose <c>GetFieldType</c> was read, so a test can prove the row loop never reads the schema.</summary>
        public List<int> SchemaCalls { get; } = [];

        /// <summary>Overrides the storage type <c>GetFieldType</c> reports (needed when the probe value is null).</summary>
        public Type[]? FieldTypes { get; init; }

        public override int FieldCount => _values.Length;

        public override int Depth => 0;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => 0;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool IsDBNull(int ordinal) => _values[ordinal] is null or DBNull;

        public override object GetValue(int ordinal)
        {
            if (!allowGetValue)
                throw new InvalidOperationException($"GetValue({ordinal}) must not be called on the CSV write path.");

            return _values[ordinal] ?? DBNull.Value;
        }

        public override T GetFieldValue<T>(int ordinal)
        {
            Calls.Add($"GetFieldValue<{typeof(T).Name}>({ordinal})");
            return (T)_values[ordinal]!;
        }

        public override string GetName(int ordinal) => $"c{ordinal}";

        public override int GetOrdinal(string name) => 0;

        public override string GetDataTypeName(int ordinal) => "probe";

        public override Type GetFieldType(int ordinal)
        {
            SchemaCalls.Add(ordinal);
            return FieldTypes is { } types ? types[ordinal] : _values[ordinal]?.GetType() ?? typeof(object);
        }

        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _values.Length);
            for (var i = 0; i < count; i++)
                values[i] = _values[i] ?? DBNull.Value;

            return count;
        }

        public override IEnumerator GetEnumerator() => _values.GetEnumerator();

        public override bool NextResult() => false;

        public override bool Read() => false;

        public override bool GetBoolean(int ordinal) => Record<bool>(nameof(GetBoolean), ordinal, _values);

        public override byte GetByte(int ordinal) => Record<byte>(nameof(GetByte), ordinal, _values);

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override char GetChar(int ordinal) => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override DateTime GetDateTime(int ordinal) => Record<DateTime>(nameof(GetDateTime), ordinal, _values);

        public override decimal GetDecimal(int ordinal) => Record<decimal>(nameof(GetDecimal), ordinal, _values);

        public override double GetDouble(int ordinal) => Record<double>(nameof(GetDouble), ordinal, _values);

        public override float GetFloat(int ordinal) => Record<float>(nameof(GetFloat), ordinal, _values);

        public override Guid GetGuid(int ordinal) => Record<Guid>(nameof(GetGuid), ordinal, _values);

        public override short GetInt16(int ordinal) => Record<short>(nameof(GetInt16), ordinal, _values);

        public override int GetInt32(int ordinal) => Record<int>(nameof(GetInt32), ordinal, _values);

        public override long GetInt64(int ordinal) => Record<long>(nameof(GetInt64), ordinal, _values);

        public override string GetString(int ordinal) => Record<string>(nameof(GetString), ordinal, _values);

        private T Record<T>(string getter, int ordinal, object?[] values)
        {
            Calls.Add($"{getter}({ordinal})");
            return (T)values[ordinal]!;
        }
    }
}
