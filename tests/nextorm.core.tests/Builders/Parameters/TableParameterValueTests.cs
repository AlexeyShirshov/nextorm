using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Public-surface tests of the <see cref="TableParameterValue"/> carrier: JSON serialization, typed
/// arrays and column resolution. A minimal in-test <see cref="DataContext"/> supplies the dialect and
/// naming convention the carrier reads.
/// </summary>
public class TableParameterValueTests
{
    private enum Level : short
    {
        Low = 1,
        High = 2,
    }

    [SqlTable("binder_entity")]
    private sealed class BinderEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level Kind { get; set; }
    }

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    private static TableParameterValue Carrier<T>(IEnumerable<T> rows)
    {
        var parameter = ProcedureParameter.Table("p", rows);
        return (TableParameterValue)parameter.Value!;
    }

    private static string Json<T>(IEnumerable<T> rows)
    {
        using var context = new TestContext();
        return Carrier(rows).WriteJson(context);
    }

    private static object? ToParameterValue(object? value, IPropertyMetadata property, ISqlDialect dialect)
    {
        var method = typeof(DataContext).Assembly.GetType("NextORM.Core.DurationStorage")!
            .GetMethod("ToParameterValue", BindingFlags.NonPublic | BindingFlags.Static)!;
        return method.Invoke(null, [value, property, dialect]);
    }

    [Fact]
    public void Json_Scalar_NullableIntsWithNulls()
    {
        Json(new int?[] { 1, null, 3 }).Should().Be("[1,null,3]");
    }

    [Fact]
    public void Json_Scalar_StringEscapesUnicodeAndQuotes()
    {
        // The default Utf8JsonWriter encoder escapes quotes and non-ASCII; both are valid JSON.
        Json(new string?[] { "a\"b\nc\\d", null, "юникод" })
            .Should().Be("[\"a\\u0022b\\nc\\\\d\",null,\"\\u044E\\u043D\\u0438\\u043A\\u043E\\u0434\"]");
    }

    [Fact]
    public void Json_Scalar_BoolGuidByteArrayDecimal()
    {
        var guid = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        Json(new[] { true, false }).Should().Be("[true,false]");
        Json(new[] { guid }).Should().Be("[\"0f8fad5b-d9cb-469f-a165-70867728950e\"]");
        Json(new[] { new byte[] { 1, 2, 3 } }).Should().Be("[\"AQID\"]");
        Json(new[] { 12.5m }).Should().Be("[12.5]");
    }

    [Fact]
    public void Json_Scalar_DateTimeDateOnlyTimeOnly()
    {
        Json(new[] { new DateTime(2024, 1, 2, 3, 4, 5) }).Should().Be("[\"2024-01-02T03:04:05\"]");
        Json(new[] { new DateOnly(2024, 1, 2) }).Should().Be("[\"2024-01-02\"]");
        Json(new[] { new TimeOnly(3, 4, 5) }).Should().Be("[\"03:04:05.0000000\"]");
    }

    [Fact]
    public void Json_Scalar_Enum_ShouldWriteUnderlyingNumber()
    {
        Json(new[] { Level.Low, Level.High }).Should().Be("[1,2]");
    }

    [Fact]
    public void Json_NonFiniteDoubleAndFloat_ShouldThrow()
    {
        Action nanDouble = () => Json(new[] { double.NaN });
        Action infinityFloat = () => Json(new[] { float.PositiveInfinity });

        nanDouble.Should().Throw<NotSupportedException>();
        infinityFloat.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Json_Entity_ShouldUseMappedColumnNamesAndWrapInArray()
    {
        var json = Json(new[]
        {
            new BinderEntity { Id = 1, Name = "alpha", Kind = Level.High },
            new BinderEntity { Id = 2, Name = null, Kind = Level.Low },
        });

        json.Should().Be("[{\"Id\":1,\"Name\":\"alpha\",\"Kind\":2},{\"Id\":2,\"Name\":null,\"Kind\":1}]");
    }

    [Fact]
    public void ToArray_NullableInts_ShouldKeepNulls()
    {
        using var context = new TestContext();
        var array = Carrier(new int?[] { 1, null, 3 }).ToArray(context);

        array.Should().BeOfType<int?[]>();
        ((int?[])array).Should().Equal(1, null, 3);
    }

    [Fact]
    public void ToArray_Enum_ShouldUseUnderlyingType()
    {
        using var context = new TestContext();
        var array = Carrier(new[] { Level.Low, Level.High }).ToArray(context);

        array.Should().BeOfType<short[]>();
        ((short[])array).Should().Equal((short)1, (short)2);
    }

    [Fact]
    public void ToArray_Strings_ShouldBeStringArray()
    {
        using var context = new TestContext();
        var array = Carrier(new[] { "a", "b" }).ToArray(context);

        array.Should().BeOfType<string[]>();
        ((string[])array).Should().Equal("a", "b");
    }

    [Fact]
    public void GetColumns_Scalar_ShouldBeSingleValueColumn()
    {
        using var context = new TestContext();
        var columns = Carrier(new[] { 1, 2 }).GetColumns(context);

        columns.Should().ContainSingle();
        columns[0].Name.Should().Be("Value");
        columns[0].ClrType.Should().Be(typeof(int));
        columns[0].GetValue(7).Should().Be(7);
    }

    [Fact]
    public void GetColumns_Entity_ShouldResolveNamesTypesAndGetters()
    {
        using var context = new TestContext();
        var carrier = Carrier(new[] { new BinderEntity { Id = 1, Name = "a", Kind = Level.High } });
        var columns = carrier.GetColumns(context);

        columns.Select(c => c.Name).Should().Equal("Id", "Name", "Kind");
        columns.Select(c => c.ClrType).Should().Equal(typeof(int), typeof(string), typeof(short));

        var row = new BinderEntity { Id = 9, Name = "z", Kind = Level.Low };
        columns[0].GetValue(row).Should().Be(9);
        columns[1].GetValue(row).Should().Be("z");
        columns[2].GetValue(row).Should().Be((short)1);
    }

    [Fact]
    public void Count_ShouldBeKnownForCollectionAndNullForLazy()
    {
        Carrier(new[] { 1, 2, 3 }).Count.Should().Be(3);
        Carrier(Yield()).Count.Should().BeNull();

        static IEnumerable<int> Yield()
        {
            yield return 1;
        }
    }

    [Fact]
    public void IsScalar_ShouldDistinguishScalarFromEntity()
    {
        Carrier(new[] { 1 }).IsScalar.Should().BeTrue();
        Carrier(new[] { new BinderEntity() }).IsScalar.Should().BeFalse();
    }

    private enum ByteEnum : byte { A = 1, B = 2 }
    private enum SByteEnum : sbyte { A = 1 }
    private enum UShortEnum : ushort { A = 1 }
    private enum UIntEnum : uint { A = 1 }
    private enum LongEnum : long { A = 1 }
    private enum ULongEnum : ulong { A = 1 }

    [SqlTable("binder_json")]
    private sealed class JsonEntity
    {
        public int Id { get; set; }

        [JsonColumn]
        public int[]? Numbers { get; set; }
    }

    [SqlTable("binder_range")]
    private sealed class RangeEntity
    {
        public int Id { get; set; }

        [RangeColumns("lower", "upper")]
        public Range<int> During { get; set; }
    }

    [SqlTable("binder_empty")]
    private sealed class EmptyEntity
    {
        public int Value => 1;
    }

    [SqlTable("binder_duration")]
    private sealed class DurationEntity
    {
        public int Id { get; set; }
        public TimeSpan Elapsed { get; set; }
    }

    [SqlTable("binder_computed")]
    private sealed class ComputedEntity
    {
        public int Id { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int Doubled { get; set; }
    }

    [SqlTable("binder_converter")]
    private sealed class ConvertedEntity
    {
        public int Id { get; set; }

        [ValueConverter(typeof(EnumToStringConverter<ProbeStatus>))]
        public ProbeStatus Status { get; set; }
    }

    [SqlTable("binder_duration_unit")]
    private sealed class DurationUnitEntity
    {
        public int Id { get; set; }

        [Duration(DurationUnit.Seconds)]
        public TimeSpan Elapsed { get; set; }
    }

    [SqlTable("binder_unsupported")]
    private sealed class UnsupportedPropertyEntity
    {
        public int Id { get; set; }

        public Uri? Link { get; set; }
    }

    private sealed class IntSecondsToDurationConverter : ValueConverter<int, TimeSpan>
    {
        public override TimeSpan ConvertToProvider(int model) => TimeSpan.FromSeconds(model);

        public override int ConvertFromProvider(TimeSpan provider) => (int)provider.TotalSeconds;
    }

    private sealed class NullableIntSecondsToDurationConverter : ValueConverter<int?, TimeSpan?>
    {
        public override TimeSpan? ConvertToProvider(int? model)
            => model is null ? null : TimeSpan.FromSeconds(model.Value);

        public override int? ConvertFromProvider(TimeSpan? provider)
            => provider is null ? null : (int)provider.Value.TotalSeconds;
    }

    [SqlTable("binder_duration_converter")]
    private sealed class DurationConverterEntity
    {
        public int Id { get; set; }

        [ValueConverter(typeof(IntSecondsToDurationConverter))]
        public int Elapsed { get; set; }
    }

    [SqlTable("binder_nullable_duration_converter")]
    private sealed class NullableDurationConverterEntity
    {
        public int Id { get; set; }

        [ValueConverter(typeof(NullableIntSecondsToDurationConverter))]
        public int? Elapsed { get; set; }
    }

    private sealed class NativeDurationDialect : SqlDialectBase
    {
        internal static readonly NativeDurationDialect Instance = new();

        public override bool SupportsNativeDuration => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class NativeDurationContext : DataContext
    {
        public NativeDurationContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => NativeDurationDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    [Fact]
    public void Json_Scalar_CharSignedUnsignedFloatTimeSpanDateTimeOffset()
    {
        Json(new[] { 'x' }).Should().Be("[\"x\"]");
        Json(new[] { (sbyte)-3 }).Should().Be("[-3]");
        Json(new[] { (ushort)5 }).Should().Be("[5]");
        Json(new[] { (uint)6L }).Should().Be("[6]");
        Json(new[] { (ulong)7 }).Should().Be("[7]");
        Json(new[] { 1.5f }).Should().Be("[1.5]");
        Json(new[] { 2.5 }).Should().Be("[2.5]");
        // The test dialect has no native duration type, so a TimeSpan is its stored tick count.
        Json(new[] { TimeSpan.FromMinutes(1) }).Should().Be("[600000000]");
        Json(new[] { new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero) })
            .Should().Be("[\"2024-01-02T03:04:05+00:00\"]");
    }

    [Fact]
    public void Json_EnumUnderlyingTypes_ShouldWriteNumbers()
    {
        Json(new[] { ByteEnum.A, ByteEnum.B }).Should().Be("[1,2]");
        Json(new[] { SByteEnum.A }).Should().Be("[1]");
        Json(new[] { UShortEnum.A }).Should().Be("[1]");
        Json(new[] { UIntEnum.A }).Should().Be("[1]");
        Json(new[] { LongEnum.A }).Should().Be("[1]");
        Json(new[] { ULongEnum.A }).Should().Be("[1]");
    }

    [Fact]
    public void ToArray_SignednessAndChar_ShouldUseStorageType()
    {
        using var context = new TestContext();

        ((short[])Carrier(new[] { (sbyte)-1 }).ToArray(context)).Should().Equal((short)-1);
        ((int[])Carrier(new[] { (ushort)2 }).ToArray(context)).Should().Equal(2);
        ((long[])Carrier(new[] { (uint)3 }).ToArray(context)).Should().Equal(3L);
        ((decimal[])Carrier(new[] { (ulong)4 }).ToArray(context)).Should().Equal(4m);
        ((string[])Carrier(new[] { 'x' }).ToArray(context)).Should().Equal("x");
    }

    [Fact]
    public void GetColumns_JsonColumn_ShouldUseConverterProviderTypeAndValue()
    {
        using var context = new TestContext();
        var carrier = Carrier(new[] { new JsonEntity { Id = 1, Numbers = new[] { 1, 2 } } });
        var columns = carrier.GetColumns(context);

        columns.Select(c => c.Name).Should().Equal("Id", "Numbers");
        columns[1].ClrType.Should().Be(typeof(string));

        var row = new JsonEntity { Id = 2, Numbers = new[] { 3 } };
        columns[1].GetValue(row).Should().Be("[3]");
    }

    [Fact]
    public void Json_EntityWithJsonColumn_ShouldWriteConverterOutput()
    {
        Json(new[] { new JsonEntity { Id = 1, Numbers = new[] { 1, 2 } } })
            .Should().Be("[{\"Id\":1,\"Numbers\":\"[1,2]\"}]");
    }

    [Fact]
    public void GetColumns_RangeProperty_ShouldThrow()
    {
        using var context = new TestContext();

        Action act = () => Carrier(new[] { new RangeEntity() }).GetColumns(context);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void GetColumns_EntityWithoutMappedColumns_ShouldThrow()
    {
        using var context = new TestContext();

        Action act = () => Carrier(new[] { new EmptyEntity() }).GetColumns(context);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void GetColumns_ScalarEnum_ShouldUseUnderlyingTypeAndValue()
    {
        using var context = new TestContext();
        var columns = Carrier(new[] { Level.Low, Level.High }).GetColumns(context);

        columns.Should().ContainSingle();
        columns[0].Name.Should().Be("Value");
        columns[0].ClrType.Should().Be(typeof(short));
        columns[0].GetValue(Level.High).Should().Be((short)2);
    }

    [Fact]
    public void GetColumns_ScalarNullableEnum_ShouldUseNullableUnderlyingType()
    {
        using var context = new TestContext();
        var columns = Carrier(new Level?[] { Level.Low, null }).GetColumns(context);

        columns[0].ClrType.Should().Be(typeof(short?));
    }

    [Fact]
    public void GetColumns_ScalarTimeSpan_ShouldUseStoredTicks()
    {
        using var context = new TestContext();
        var columns = Carrier(new[] { TimeSpan.FromMinutes(1) }).GetColumns(context);

        columns[0].ClrType.Should().Be(typeof(long));
        columns[0].GetValue(TimeSpan.FromMinutes(1)).Should().Be(600000000L);
    }

    [Fact]
    public void GetColumns_EntityTimeSpan_ShouldUseStoredTicksConsistently()
    {
        using var context = new TestContext();
        var row = new DurationEntity { Id = 1, Elapsed = TimeSpan.FromMinutes(1) };
        var columns = Carrier(new[] { row }).GetColumns(context);

        columns[1].ClrType.Should().Be(typeof(long));
        columns[1].GetValue(row).Should().Be(600000000L);
    }

    [Fact]
    public void ToArray_TimeSpan_ShouldUseStoredTicks()
    {
        using var context = new TestContext();
        var array = Carrier(new[] { TimeSpan.FromMinutes(1) }).ToArray(context);

        array.Should().BeOfType<long[]>();
        ((long[])array).Should().Equal(600000000L);
    }

    [Fact]
    public void ToArray_NullableTimeSpan_ShouldUseNullableTicks()
    {
        using var context = new TestContext();
        var array = Carrier(new TimeSpan?[] { TimeSpan.FromMinutes(1), null }).ToArray(context);

        array.Should().BeOfType<long?[]>();
        ((long?[])array).Should().Equal(600000000L, null);
    }

    [Fact]
    public void ToArray_TimeSpan_WithNativeDurationDialect_ShouldKeepTimeSpan()
    {
        using var context = new NativeDurationContext();
        var array = Carrier(new[] { TimeSpan.FromMinutes(1) }).ToArray(context);

        array.Should().BeOfType<TimeSpan[]>();
        ((TimeSpan[])array).Should().Equal(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GetColumns_EntityComputedProperty_ShouldSkipIt()
    {
        using var context = new TestContext();
        var carrier = Carrier(new[] { new ComputedEntity { Id = 1, Doubled = 2 } });
        var columns = carrier.GetColumns(context);

        columns.Should().ContainSingle();
        columns[0].Name.Should().Be("Id");
    }

    [Fact]
    public void GetColumns_EntityValueConverter_ShouldUseProviderTypeAndValue()
    {
        using var context = new TestContext();
        var row = new ConvertedEntity { Id = 1, Status = ProbeStatus.Active };
        var columns = Carrier(new[] { row }).GetColumns(context);

        columns.Select(c => c.Name).Should().Equal("Id", "Status");
        columns[1].ClrType.Should().Be(typeof(string));
        columns[1].GetValue(row).Should().Be("Active");
    }

    [Fact]
    public void Json_EntityValueConverter_ShouldWriteConvertedValue()
    {
        Json(new[] { new ConvertedEntity { Id = 1, Status = ProbeStatus.Closed } })
            .Should().Be("[{\"Id\":1,\"Status\":\"Closed\"}]");
    }

    [Fact]
    public void GetColumns_EntityTimeSpanWithUnit_ShouldUseDeclaredStorageUnit()
    {
        using var context = new TestContext();
        var row = new DurationUnitEntity { Id = 1, Elapsed = TimeSpan.FromMinutes(1) };
        var columns = Carrier(new[] { row }).GetColumns(context);

        columns[1].ClrType.Should().Be(typeof(long));
        columns[1].GetValue(row).Should().Be(60L);
    }

    [Fact]
    public void Json_EntityTimeSpanWithUnit_ShouldWriteStoredUnits()
    {
        Json(new[] { new DurationUnitEntity { Id = 1, Elapsed = TimeSpan.FromMinutes(1) } })
            .Should().Be("[{\"Id\":1,\"Elapsed\":60}]");
    }

    [Fact]
    public void GetColumns_EntityTimeSpan_WithNativeDurationDialect_ShouldKeepTimeSpan()
    {
        using var context = new NativeDurationContext();
        var row = new DurationEntity { Id = 1, Elapsed = TimeSpan.FromMinutes(1) };
        var columns = Carrier(new[] { row }).GetColumns(context);

        columns[1].ClrType.Should().Be(typeof(TimeSpan));
        columns[1].GetValue(row).Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Json_EntityTimeSpan_WithNativeDurationDialect_ShouldWriteIsoDuration()
    {
        using var context = new NativeDurationContext();
        var json = Carrier(new[] { new DurationEntity { Id = 1, Elapsed = TimeSpan.FromMinutes(1) } })
            .WriteJson(context);

        json.Should().Be("[{\"Id\":1,\"Elapsed\":\"00:01:00\"}]");
    }

    // A converter owns its provider representation: the general write seam returns the converted
    // TimeSpan unchanged (it does not reduce it to a stored integer), on a native and on a non-native
    // provider alike. A table-valued parameter cannot bind a converter-to-TimeSpan column on a provider
    // without a native duration type, so it rejects it instead of reducing the value.
    [Fact]
    public void ToParameterValue_ConverterToTimeSpan_OnNonNative_ShouldReturnTimeSpan()
    {
        using var context = new TestContext();
        var property = MetadataProperty(nameof(DurationConverterEntity.Elapsed));

        var result = ToParameterValue(90, property, context.Dialect);

        result.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void ToParameterValue_ConverterToTimeSpan_OnNative_ShouldReturnTimeSpan()
    {
        using var context = new NativeDurationContext();
        var property = MetadataProperty(nameof(DurationConverterEntity.Elapsed));

        var result = ToParameterValue(90, property, context.Dialect);

        result.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void GetColumns_EntityConverterToTimeSpan_OnNonNative_ShouldThrow()
    {
        using var context = new TestContext();
        var carrier = Carrier(new[] { new DurationConverterEntity { Id = 1, Elapsed = 90 } });

        Action act = () => carrier.GetColumns(context);

        act.Should().Throw<NotSupportedException>().WithMessage("*Elapsed*");
    }

    [Fact]
    public void GetColumns_EntityNullableConverterToTimeSpan_OnNonNative_ShouldThrow()
    {
        using var context = new TestContext();
        var carrier = Carrier(new[] { new NullableDurationConverterEntity { Id = 1, Elapsed = 90 } });

        Action act = () => carrier.GetColumns(context);

        act.Should().Throw<NotSupportedException>().WithMessage("*Elapsed*");
    }

    [Fact]
    public void GetColumns_EntityConverterToTimeSpan_WithNativeDurationDialect_ShouldKeepTimeSpan()
    {
        using var context = new NativeDurationContext();
        var row = new DurationConverterEntity { Id = 1, Elapsed = 90 };
        var columns = Carrier(new[] { row }).GetColumns(context);

        columns[1].ClrType.Should().Be(typeof(TimeSpan));
        columns[1].GetValue(row).Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void Json_EntityConverterToTimeSpan_OnNonNative_ShouldThrow()
    {
        Action act = () => Json(new[] { new DurationConverterEntity { Id = 1, Elapsed = 90 } });

        act.Should().Throw<NotSupportedException>().WithMessage("*Elapsed*");
    }

    private static IPropertyMetadata MetadataProperty(string name)
        => new EntityMetadataBuilder<DurationConverterEntity>().Build().Properties
            .Single(p => p.PropertyInfo.Name == name);

    [Fact]
    public void ToArray_NullableEnum_ShouldUseNullableUnderlyingType()
    {
        using var context = new TestContext();
        var array = Carrier(new Level?[] { Level.Low, null }).ToArray(context);

        array.Should().BeOfType<short?[]>();
        ((short?[])array).Should().Equal((short)1, null);
    }

    [Fact]
    public void ToArray_StringsWithNulls_ShouldKeepNullElements()
    {
        using var context = new TestContext();
        var array = Carrier(new string?[] { "a", null }).ToArray(context);

        array.Should().BeOfType<string[]>();
        ((string?[])array).Should().Equal("a", null);
    }

    [Fact]
    public void Json_EntityUnsupportedPropertyType_ShouldThrow()
    {
        Action act = () => Json(new[]
        {
            new UnsupportedPropertyEntity { Id = 1, Link = new Uri("https://example.com") },
        });

        act.Should().Throw<NotSupportedException>();
    }
}
