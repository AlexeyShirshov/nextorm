using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Unit coverage for the raw PostgreSQL row/composite materialization seam (issue #194, D194.3).
///
/// The seam is chosen in <c>RawMapperFactory.GetOrBuild&lt;T&gt;</c>: a single reader column whose
/// data type name is <c>record</c> is materialized into the declared <see cref="Tuple"/> family from
/// the driver's null-preserving <c>System.Object[]</c>; a single non-record column is a named
/// composite. These tests drive the seam with a fake one-record-column reader (the shape the D194.1
/// spike observed for Npgsql 10.0.3), so no database is required. The container-backed PostgreSQL
/// execution matrix lives in <c>nextorm.integration.tests.PostgresRawRowTests</c>.
///
/// Contracts pinned here:
/// S1 anonymous <see cref="Tuple"/> arity 1..7, R194-NULL whole-record / all-fields-null / non-nullable
/// item, R194-ERROR guards S3/S4/S6..S13, and R194-R2-CACHE map-key separation.
/// </summary>
public class RawRowMaterializerTests
{
    // ---------------------------------------------------------------- S1 positive

    [Fact]
    public void AnonymousTuple_Arity1_ShouldMaterialize()
        => AssertAnonymousTuple<Tuple<int>>([41], Tuple.Create(41));

    [Fact]
    public void AnonymousTuple_Arity2_ShouldMaterialize()
        => AssertAnonymousTuple<Tuple<int, string>>([7, "a"], Tuple.Create(7, "a"));

    [Fact]
    public void AnonymousTuple_Arity3_ShouldMaterialize()
        => AssertAnonymousTuple<Tuple<int, string, int?>>([1, "b", 3], Tuple.Create(1, "b", (int?)3));

    [Fact]
    public void AnonymousTuple_Arity4_ShouldMaterialize()
        => AssertAnonymousTuple<Tuple<int, string, bool, double>>(
            [1, "c", true, 2.5], Tuple.Create(1, "c", true, 2.5));

    [Fact]
    public void AnonymousTuple_Arity5_ShouldMaterialize()
        => AssertAnonymousTuple<Tuple<int, string, long, decimal, DateTime>>(
            [1, "d", 2L, 3.5m, new DateTime(2024, 1, 2, 3, 4, 5)],
            Tuple.Create(1, "d", 2L, 3.5m, new DateTime(2024, 1, 2, 3, 4, 5)));

    [Fact]
    public void AnonymousTuple_Arity6_ShouldMaterialize()
        => AssertAnonymousTuple<Tuple<int, string, long, decimal, float, Guid>>(
            [1, "e", 2L, 3.5m, 4.5f, Guid.Parse("11111111-2222-3333-4444-555555555555")],
            Tuple.Create(1, "e", 2L, 3.5m, 4.5f, Guid.Parse("11111111-2222-3333-4444-555555555555")));

    [Fact]
    public void AnonymousTuple_Arity7_ShouldMaterialize_WithMixedReferenceAndValueTypeItems()
        => AssertAnonymousTuple<Tuple<int?, string, bool?, short, string?, int, double?>>(
            [null, "f", true, (short)9, null, 10, 11.5],
            Tuple.Create((int?)null, "f", (bool?)true, (short)9, (string?)null, 10, (double?)11.5));

    [Fact]
    public void AnonymousTuple_FieldWidening_ShouldConvertToDeclaredItemType()
        => AssertAnonymousTuple<Tuple<long, string>>([5, "g"], Tuple.Create(5L, "g"));

    // ---------------------------------------------------------------- scalar allow-list

    [Fact]
    public void AnonymousTuple_AllowListedItemTypes_ShouldMaterialize()
    {
        // enum, byte[], DateOnly, TimeOnly, TimeSpan, DateTimeOffset and float are all in the record
        // scalar allow-list and must round-trip through the null-preserving object[].
        var bytes = new byte[] { 1, 2, 3 };
        var dateOnly = new DateOnly(2024, 2, 3);
        var timeOnly = new TimeOnly(4, 5, 6);
        var timeSpan = TimeSpan.FromMinutes(90);
        var offset = new DateTimeOffset(2024, 2, 3, 4, 5, 6, TimeSpan.FromHours(3));

        AssertAnonymousTuple<Tuple<SampleEnum, byte[], DateOnly, TimeOnly, TimeSpan, DateTimeOffset, float>>(
            [SampleEnum.Two, bytes, dateOnly, timeOnly, timeSpan, offset, 1.5f],
            Tuple.Create(SampleEnum.Two, bytes, dateOnly, timeOnly, timeSpan, offset, 1.5f));
    }

    [Fact]
    public void AnonymousTuple_ShortAndNullableDecimal_ShouldMaterialize()
    {
        AssertAnonymousTuple<Tuple<short, decimal?>>([(short)7, 2.5m], Tuple.Create((short)7, (decimal?)2.5m));
        AssertAnonymousTuple<Tuple<decimal?, short>>([null, (short)8], Tuple.Create((decimal?)null, (short)8));
    }

    // ---------------------------------------------------------------- R194-NULL

    [Fact]
    public void WholeRecordNull_ShouldMaterializeAsClrNull()
    {
        var reader = RecordReader.Record(values: [DBNull.Value], nulls: [true]);

        var result = Map<Tuple<int?, string?>>(reader);

        result.Should().BeNull();
    }

    [Fact]
    public void AllFieldsNull_ShouldMaterializeNonNullTupleWithNullItems()
    {
        var reader = RecordReader.Record(values: [new object?[] { null, null }]);

        var result = Map<Tuple<int?, string?>>(reader);

        result.Should().NotBeNull();
        result!.Item1.Should().BeNull();
        result.Item2.Should().BeNull();
    }

    [Fact]
    public void NullableItemWithSqlNull_ShouldMaterializeNullItem()
    {
        var reader = RecordReader.Record(values: [new object?[] { 1, null }]);

        var result = Map<Tuple<int, string?>>(reader);

        result.Item1.Should().Be(1);
        result.Item2.Should().BeNull();
    }

    [Fact]
    public void NonNullableValueTypeItemWithSqlNull_ShouldThrowNeverDefault()
    {
        var reader = RecordReader.Record(values: [new object?[] { null, "a" }]);

        var act = () => Map<Tuple<int, string>>(reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("PostgreSQL raw-row materialization failed:*");
    }

    [Fact]
    public void NonConvertibleField_ShouldThrowAndPreserveInnerException()
    {
        var reader = RecordReader.Record(values: [new object?[] { "not-an-int" }]);

        var act = () => Map<Tuple<int>>(reader);

        var ex = act.Should().Throw<InvalidOperationException>()
            .WithMessage("PostgreSQL raw-row materialization failed:*").Which;
        ex.InnerException.Should().NotBeNull(
            "the driver/conversion exception must not be discarded");
    }

    [Fact]
    public void NonFormatConversionFailure_ShouldWrapAndPreserveInnerException()
    {
        // A Guid -> int conversion fails with a non-Format exception (InvalidCastException); it must
        // still be wrapped in the contracted exception with the inner exception preserved.
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var reader = RecordReader.Record(values: [new object?[] { guid }]);

        var act = () => Map<Tuple<int>>(reader);

        var ex = act.Should().Throw<InvalidOperationException>()
            .WithMessage("PostgreSQL raw-row materialization failed:*").Which;
        ex.InnerException.Should().NotBeNull("the conversion exception must not be discarded");
        ex.InnerException.Should().NotBeOfType<FormatException>(
            "a Guid -> int conversion is not a parse failure, so the wrapper cannot depend on FormatException");
    }

    // ---------------------------------------------------------------- R194-ERROR arity / record type

    [Fact]
    public void RecordWiderThanDeclaredTuple_ShouldThrowContractedInvalidOperation()
    {
        // A ROW with more fields than the declared tuple must not silently drop the extra fields.
        var reader = RecordReader.Record(values: [new object?[] { 1, "a", 5 }]);

        var act = () => Map<Tuple<int, string>>(reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("PostgreSQL raw-row materialization failed:*");
    }

    [Fact]
    public void RecordNarrowerThanDeclaredTuple_ShouldThrowContractedInvalidOperation()
    {
        // A ROW with fewer fields must surface the contract, not a bare IndexOutOfRangeException.
        var reader = RecordReader.Record(values: [new object?[] { 1 }]);

        var act = () => Map<Tuple<int, string>>(reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("PostgreSQL raw-row materialization failed:*");
    }

    [Fact]
    public void NonArrayRecordValue_ShouldThrowContractedInvalidOperation()
    {
        // A driver that returns a tuple instead of object[] must be rejected through the contract, not a
        // bare InvalidCastException.
        var reader = RecordReader.Record(values: [Tuple.Create(1, "a")]);

        var act = () => Map<Tuple<int, string>>(reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("PostgreSQL raw-row materialization failed:*");
    }

    [Fact]
    public void MetadataProbeFailure_ShouldSurfaceTheContractAndNotSwallowDriverErrors()
    {
        using var ctx = new TestContext();

        // A rejected metadata probe combined with the explicit composite fact yields a detection verdict:
        // the PostgreSQL path reports the unregistered-composite guard rather than silently mapping a
        // wrong shape. The fact — not the probe exception shape — is what classifies the column.
        var probeFailure = RecordReader.MetadataFailure();
        var actProbe = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, probeFailure);
        actProbe.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");

        // A real driver/connection error from the same probe is not swallowed.
        var driverError = RecordReader.MetadataDriverError();
        var actDriver = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, driverError);
        actDriver.Should().Throw<TestDbException>();
    }

    [Fact]
    public void MetadataProbeInvalidOperation_ShouldPropagateNotBeSwallowed()
    {
        // The metadata-rejection set is narrowed to InvalidCastException/NotSupportedException. A plain
        // InvalidOperationException (non-disposed) is a genuine fault, not a "no verdict on this column"
        // rejection, so it must propagate rather than being swallowed as a missing-composite verdict.
        var reader = RecordReader.MetadataInvalidOperation();
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<InvalidOperationException>()
            .Which.Should().NotBeOfType<ObjectDisposedException>();
    }

    // ---------------------------------------------------------------- R194-ERROR guards

    [Fact]
    public void ValueTupleResult_ShouldGuard()
        => AssertGuard<ValueTuple<int, string>>(RecordReader.Record(values: [new object?[] { 1, "a" }]));

    [Fact]
    public void TupleBeyondSeven_ShouldGuard()
    {
        var tuple8 = typeof(Tuple<,,,,,,,>).MakeGenericType(
            typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
            typeof(Tuple<int>));
        var reader = RecordReader.Record(values: [new object?[] { 1, 2, 3, 4, 5, 6, 7, new object?[] { 8 } }]);

        var ex = InvokeGenericGetOrBuild(tuple8, reader);

        ex.Should().BeOfType<NotSupportedException>();
        ex!.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void UnregisteredNamedComposite_ShouldGuardWithRegistrationHint()
    {
        var reader = RecordReader.Composite(throwOnFieldType: true);

        var act = () => Map<NamedComposite>(reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ")
            .And.Contain("MapComposite");
    }

    [Fact]
    public void NamedCompositeDeclaredAsTuple_ShouldGuard()
    {
        var reader = RecordReader.Composite(throwOnFieldType: false);

        var act = () => Map<Tuple<int, string>>(reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void AnonymousRowDeclaredAsDto_ShouldGuard()
    {
        var reader = RecordReader.Record(values: [new object?[] { 1, "a" }]);

        var act = () => Map<NamedComposite>(reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void NestedRow_ShouldGuard()
    {
        // The driver returns the nested ROW as a nested System.Object[]; the declared item is not.
        var reader = RecordReader.Record(values: [new object?[] { new object?[] { 1, "a" }, 2 }]);

        var act = () => Map<Tuple<int, int>>(reader);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("PostgreSQL raw-row materialization is not supported:*nested ROW*");
    }

    [Fact]
    public void EmptyRow_ShouldGuard()
    {
        var reader = RecordReader.Record(values: [Array.Empty<object>()]);

        var act = () => Map<object>(reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void MultipleRecordColumns_ShouldGuard()
    {
        var reader = RecordReader.Multi(
            ["record", "record"],
            [typeof(object[]), typeof(object[])],
            [new object?[] { 1 }, new object?[] { 2 }]);

        var act = () => Map<Tuple<int, string>>(reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void MixedAnonymousRecordAndScalar_ShouldGuard()
    {
        var reader = RecordReader.Multi(
            ["record", "int4"],
            [typeof(object[]), typeof(int)],
            [new object?[] { 1, "a" }, 2]);

        var act = () => Map<Tuple<int, string>>(reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void MixedNamedCompositeAndScalar_ShouldGuard()
    {
        // A named composite column is not reported under the anonymous "record" data type; the guard
        // must still fire when it is mixed with a scalar column instead of falling through to the
        // entity path with a misleading "no mapped property" error.
        var reader = RecordReader.Multi(
            ["app.named_composite", "int4"],
            [typeof(NamedComposite), typeof(int)],
            [new NamedComposite { A = 1, B = "a" }, 2],
            composites: [true, false]);

        var act = () => Map<NamedComposite>(reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void NonAllowListedRecordField_ShouldGuard()
    {
        var reader = RecordReader.Record(values: [new object?[] { new NamedComposite(), 1 }]);

        var act = () => Map<Tuple<NamedComposite, int>>(reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void NonPostgresProvider_SingleNonScalarColumn_ShouldKeepTheOrdinaryPath()
    {
        // A provider that does not surface PostgreSQL record columns must never receive the
        // PostgreSQL-specific named-composite diagnostic.
        var reader = RecordReader.Composite(throwOnFieldType: true);
        using var ctx = new NonPostgresContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("None of the result-set columns*",
                "a non-PostgreSQL provider keeps the pre-D194 entity path; the PostgreSQL raw-row diagnostic must not leak");
    }

    // ---------------------------------------------------------------- n=3 classification / error hardening

    [Fact]
    public void SingleNonScalarColumn_IntArray_ShouldNotHijackAsNamedComposite()
    {
        // The driver reports GetFieldType == int[] and a non-schema-qualified array data type name
        // (_int4). Array covariance must not route this to the typed named-composite accessor (which
        // would silently read it through GetFieldValue<int[]>); the ordinary entity path is kept and
        // fails while building the array's metadata, so the read never silently succeeds. An explicit
        // composite fact must not bypass the array exclusion.
        var reader = RecordReader.Multi(["_int4"], [typeof(int[])], [new[] { 1, 2, 3 }], composites: [true]);

        var act = () => Map<int[]>(reader);

        act.Should().Throw<ArgumentException>("the ordinary metadata path is kept; no silent GetFieldValue success");
    }

    [Fact]
    public void SingleNonScalarColumn_Dictionary_ShouldNotHijackAsNamedComposite()
    {
        // Same for a Dictionary<string,int>: it is IEnumerable and its data type name is not a qualified
        // composite, so it must never reach the typed named-composite accessor and silently succeed.
        var reader = RecordReader.Multi(
            ["_int4"], [typeof(Dictionary<string, int>)], [new Dictionary<string, int> { ["a"] = 1 }], composites: [true]);

        var act = () => Map<Dictionary<string, int>>(reader);

        act.Should().Throw<ArgumentException>("the ordinary metadata path is kept; no silent GetFieldValue success");
    }

    [Fact]
    public void SingleNonScalarColumn_CustomEnumerableComposite_ShouldNotBeBlanketRejected()
    {
        // The collection exclusion is narrowed to arrays + IDictionary: a caller-registered composite that
        // merely implements IEnumerable is a legitimate named composite and must still reach the typed
        // composite accessor instead of the ordinary entity path.
        var composite = new CustomEnumerableComposite();
        var reader = RecordReader.Multi(
            ["app.custom_enumerable"], [typeof(CustomEnumerableComposite)], [composite], composites: [true]);
        using var ctx = new TestContext();

        var mapper = RawMapperFactory.GetOrBuild<CustomEnumerableComposite>(ctx, reader);

        mapper(reader).Should().BeSameAs(composite, "an IEnumerable composite is a genuine named composite");
    }

    [Fact]
    public void UnresolvableNonCompositeColumn_OrdinaryNoMatchRead_ShouldKeepTheEntityError()
    {
        // A dotted-but-not-composite column whose GetFieldType throws NotSupportedException (unknown
        // type, e.g. ltree without EnableLTree) must not be classified as a named composite: classification
        // is delegated to the provider's authoritative predicate, and no explicit composite fact exists
        // here, so the established "None of the result-set columns ..." error is preserved instead of a
        // misleading composite diagnostic. The metadata-probe exception shape is not a discriminator.
        var reader = RecordReader.UnresolvableNonCompositeAndScalar();
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("None of the result-set columns*")
            .Which.Message.Should().NotContain("named composite");
    }

    [Fact]
    public void SingleUnresolvableColumn_DottedNameAndCastExceptionOnly_ShouldNotClassifyAsComposite()
    {
        // The metadata probe rejects the single column with InvalidCastException and its data type name
        // is dotted, but no explicit composite fact is present: neither a name nor an exception shape is
        // a classifier, so the ordinary entity path (and its no-match error) is kept.
        var reader = RecordReader.Multi(["app.named_composite"], [null], [42]);
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("None of the result-set columns*")
            .Which.Message.Should().NotContain("named composite");
    }

    [Fact]
    public void MetadataProbeRejection_WithoutCompositeFact_ShouldNotClassifyAsComposite()
    {
        // Both metadata probes reject (NotSupportedException on the type name, InvalidCastException on the
        // field type) yet the predicate is the only classifier: with no explicit composite fact the read
        // takes the ordinary path instead of the removed exception-shape composite diagnosis.
        var reader = new RecordReader(["app.named_composite"], [null], [42]) { ThrowOnDataTypeName = true };
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("None of the result-set columns*")
            .Which.Message.Should().NotContain("named composite");
    }

    [Fact]
    public void UnresolvableDottedNonCompositeColumn_WithCastException_ShouldNotClassifyAsComposite()
    {
        // A dotted, unresolvable non-composite mixed with a scalar: the explicit fact says "not composite",
        // so the mixed-column composite guard must not fire.
        var reader = RecordReader.Multi(
            ["app.dotted_non_composite", "int4"],
            [null, typeof(int)],
            [new object(), 5],
            composites: [false, false]);
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("None of the result-set columns*")
            .Which.Message.Should().NotContain("named composite");
    }

    [Fact]
    public void UnknownPlaceholderColumn_WithCastException_ShouldNotClassifyAsComposite()
    {
        // The "-.-" placeholder an unresolved driver column reports is not a composite signal either.
        var reader = RecordReader.Multi(
            ["-.-", "int4"], [null, typeof(int)], [new object(), 5], composites: [false, false]);
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("None of the result-set columns*")
            .Which.Message.Should().NotContain("named composite");
    }

    [Fact]
    public void UnresolvableExplicitCompositeColumn_MixedWithScalar_ShouldGuard()
    {
        // The explicit composite fact (the sole classifier) identifies the unresolvable column, so the
        // mixed-composite guard fires and names the column for diagnostics.
        var reader = RecordReader.Multi(
            ["app.unregistered_composite", "int4"],
            [null, typeof(int)],
            [new NamedComposite { A = 1, B = "a" }, 5],
            composites: [true, false]);
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ")
            .And.Contain("app.unregistered_composite");
    }

    [Fact]
    public void UnresolvableExplicitCompositeColumn_LaterOrdinal_ShouldGuard()
    {
        // Same fact at a later ordinal: the scan is per column, not fixed to ordinal 0.
        var reader = RecordReader.Multi(
            ["int4", "app.unregistered_composite"],
            [typeof(int), null],
            [5, new NamedComposite { A = 1, B = "a" }],
            composites: [false, true]);
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ")
            .And.Contain("app.unregistered_composite");
    }

    [Fact]
    public void CompositeClassification_DoesNotScaleWithRows()
    {
        // Classification is a one-time mapper-preparation fact: the provider predicate is consulted while
        // building the mapper, never again while rows are read.
        var reader = new RecordReader(
            ["app.pt_composite"], [typeof(Tuple<int, string>)], [Tuple.Create(1, "a")],
            composites: [true]) { ReadCount = 3 };
        using var ctx = new TestContext();

        var mapper = RawMapperFactory.GetOrBuild<Tuple<int, string>>(ctx, reader);
        var classificationsAfterBuild = ctx.CompositeProbeCount;
        classificationsAfterBuild.Should().BeGreaterThan(0, "mapper preparation must consult the predicate");

        var rows = 0;
        while (reader.Read())
        {
            mapper(reader);
            rows++;
        }

        rows.Should().Be(3);
        ctx.CompositeProbeCount.Should().Be(
            classificationsAfterBuild,
            "classification must not grow with the row count");
    }

    // ---------------------------------------------------------------- T11 empty result set

    [Fact]
    public void EmptyRead_ShouldBuildTheMapperAndYieldNoRows()
    {
        // T11 empty-result-set row: the raw-row mapper is prepared from the reader's column metadata
        // before any row is read, so preparing it must not consult row values. A reader that reports no
        // rows (ReadCount = 0) therefore builds a valid mapper and yields an empty sequence, not a throw.
        var reader = new RecordReader(["record"], [typeof(object[])], [new object?[] { 1, "a" }]) { ReadCount = 0 };
        using var ctx = new TestContext();

        var mapper = RawMapperFactory.GetOrBuild<Tuple<int, string>>(ctx, reader);

        var rows = new List<Tuple<int, string>>();
        while (reader.Read())
            rows.Add(mapper(reader));

        rows.Should().BeEmpty("an empty raw-row result set must materialize no rows without throwing");
    }

    [Fact]
    public void NullableStructComposite_Registered_ShouldRouteToNamedComposite()
    {
        // The driver reports a registered struct composite's CLR type (StructComposite), not
        // Nullable<StructComposite>, so eligibility must unwrap the declared Nullable<> or the
        // named-composite branch is unreachable and the read falls to the generic entity error.
        var composite = new StructComposite { A = 1, B = "a" };
        var reader = RecordReader.Multi(["app.struct_composite"], [typeof(StructComposite)], [composite], composites: [true]);
        using var ctx = new TestContext();

        var mapper = RawMapperFactory.GetOrBuild<StructComposite?>(ctx, reader);

        mapper(reader).Should().Be(composite);
    }

    [Fact]
    public void CovariantArrayRecordField_WithMismatchedItemType_ShouldBeDataMismatchNotNestedRow()
    {
        // A string[] runtime field is covariant with object[], but the nested-ROW check must use exact
        // SZArray/object element identity. With a mismatched declared item type the field is a data
        // mismatch (InvalidOperationException), not the nested-ROW NotSupportedException.
        var reader = RecordReader.Record(values: [new object?[] { new[] { "x", "y" } }]);

        var act = () => Map<Tuple<int>>(reader);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("PostgreSQL raw-row materialization failed:*");
    }

    [Fact]
    public void DisposedReaderMetadataProbe_ShouldPropagate()
    {
        // ObjectDisposedException is also an InvalidOperationException, but it is a real fault, not a
        // metadata rejection verdict: a disposed reader must propagate instead of being swallowed as
        // "this column is not a record".
        var reader = RecordReader.MetadataDisposed();
        using var ctx = new TestContext();

        var act = () => RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DisposedNamedCompositeRead_ShouldPropagateUnwrapped()
    {
        // ObjectDisposedException is a real fault, not a materialization mismatch: the named-composite
        // read must let it propagate unwrapped, consistent with the metadata probes.
        var reader = RecordReader.NamedCompositeDisposed();
        using var ctx = new TestContext();
        var mapper = RawMapperFactory.GetOrBuild<NamedComposite>(ctx, reader);

        var act = () => mapper(reader);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void CancelledConversion_ShouldPropagateUnwrapped()
    {
        // OperationCanceledException is a cancellation signal, not a data mismatch: the field conversion
        // must let it propagate instead of wrapping it in the R194-ERROR InvalidOperationException.
        var reader = RecordReader.Record(values: [new object?[] { new CancelOnConvert() }]);

        var act = () => Map<Tuple<int>>(reader);

        act.Should().Throw<OperationCanceledException>();
    }

    // ---------------------------------------------------------------- R194-R2-CACHE

    [Fact]
    public void RawMapperCacheKey_RecordKindDiscriminatesOrdinaryFromRecordMapping()
    {
        // Key-identity guard (not cache behaviour): the raw-row discriminator is part of the key so a
        // record mapping can never alias an ordinary mapping that shares provider/result/columns.
        var ordinary = new RawMapperCacheKey(
            typeof(TestContext), typeof(Tuple<int, string>), true, "k", null,
            RawRowKind.None, null);
        var record = new RawMapperCacheKey(
            typeof(TestContext), typeof(Tuple<int, string>), true, "k", null,
            RawRowKind.AnonymousTuple, "sig");

        // Structural/key inequality only: the record struct's generated equality, not a hash-code
        // coincidence, must separate the two keys.
        ordinary.Should().NotBe(record);
        ordinary.RecordKind.Should().Be(RawRowKind.None);
        record.RecordKind.Should().Be(RawRowKind.AnonymousTuple);

        // Actual cache separation through the real key construction: the same result type read as an
        // anonymous record and as a caller-registered named composite must not alias.
        using var ctx = new TestContext();
        var anonymous = RawMapperFactory.GetOrBuild<Tuple<int, string>>(
            ctx, RecordReader.Record(values: [new object?[] { 1, "a" }]));
        var composite = RawMapperFactory.GetOrBuild<Tuple<int, string>>(
            ctx, RecordReader.Multi(["app.pt_composite"], [typeof(Tuple<int, string>)], [Tuple.Create(1, "a")], composites: [true]));

        composite.Should().NotBeSameAs(
            anonymous, "RecordKind must keep the two raw-row shapes in distinct cache entries");
    }

    [Fact]
    public void AnonymousTuple_RepeatedReads_ShouldReuseTheCachedMapper()
    {
        var reader = RecordReader.Record(values: [new object?[] { 1, "a" }]);
        using var ctx = new TestContext();

        var first = RawMapperFactory.GetOrBuild<Tuple<int, string>>(ctx, reader);
        var second = RawMapperFactory.GetOrBuild<Tuple<int, string>>(ctx, reader);

        second.Should().BeSameAs(first, "the raw record mapper is cached by record shape, not rebuilt");
    }

    [Fact]
    public void AnonymousTuple_DistinctShapes_ShouldNotAlias()
    {
        // Exercise the real cache key construction (<c>RawMapperFactory.GetOrBuild</c>), not hand-built key
        // structs: a distinct item type and a distinct arity must each be a distinct entry, while the same
        // shape must reuse one.
        using var ctx = new TestContext();
        var readerInt = RecordReader.Record(values: [new object?[] { 1, "a" }]);
        var readerIntAgain = RecordReader.Record(values: [new object?[] { 9, "z" }]);
        var readerLong = RecordReader.Record(values: [new object?[] { 1L, "a" }]);
        var readerArity3 = RecordReader.Record(values: [new object?[] { 1, "a", 2 }]);

        var mapperInt = RawMapperFactory.GetOrBuild<Tuple<int, string>>(ctx, readerInt);
        var mapperIntAgain = RawMapperFactory.GetOrBuild<Tuple<int, string>>(ctx, readerIntAgain);
        var mapperLong = RawMapperFactory.GetOrBuild<Tuple<long, string>>(ctx, readerLong);
        var mapperArity3 = RawMapperFactory.GetOrBuild<Tuple<int, string, int>>(ctx, readerArity3);

        mapperIntAgain.Should().BeSameAs(mapperInt, "the same record shape reuses one cache entry");
        mapperLong.Should().NotBeSameAs(mapperInt, "a distinct item type is a distinct cache entry");
        mapperArity3.Should().NotBeSameAs(mapperInt, "a distinct arity is a distinct cache entry");
    }

    // ---------------------------------------------------------------- helpers

    private static T Map<T>(RecordReader reader)
    {
        using var ctx = new TestContext();
        return RawMapperFactory.GetOrBuild<T>(ctx, reader)(reader);
    }

    private static void AssertAnonymousTuple<T>(object?[] values, T expected)
    {
        var result = Map<T>(RecordReader.Record(values: [values]));
        result.Should().Be(expected);
    }

    private static void AssertGuard<T>(RecordReader reader)
    {
        var act = () => Map<T>(reader);
        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    private static Exception? InvokeGenericGetOrBuild(Type resultType, RecordReader reader)
    {
        using var ctx = new TestContext();
        var method = typeof(RawMapperFactory)
            .GetMethod(nameof(RawMapperFactory.GetOrBuild), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(resultType);
        try
        {
            method.Invoke(null, [ctx, reader]);
            return null;
        }
        catch (TargetInvocationException ex)
        {
            return ex.InnerException;
        }
    }

    /// <summary>An enum in the record scalar allow-list.</summary>
    private enum SampleEnum
    {
        One = 1,
        Two = 2,
    }

    /// <summary>A class shape used as an (un)registered named composite / DTO in the guard tests.</summary>
    private sealed class NamedComposite
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    /// <summary>
    /// A caller-registered composite shape that implements <see cref="IEnumerable"/> but is neither an
    /// array nor an <see cref="IDictionary"/>; it must not be excluded by the collection filter.
    /// </summary>
    private sealed class CustomEnumerableComposite : IEnumerable
    {
        public IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }

    /// <summary>A caller-registered struct composite, to pin the declared <c>Nullable&lt;T&gt;</c> eligibility.</summary>
    private struct StructComposite
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    /// <summary>
    /// A value whose <see cref="IConvertible"/> conversion to the declared record item type throws
    /// <see cref="OperationCanceledException"/>, to pin that cancellation is not wrapped as a data
    /// mismatch.
    /// </summary>
    private sealed class CancelOnConvert : IConvertible
    {
        public TypeCode GetTypeCode() => TypeCode.Int32;

        public bool ToBoolean(IFormatProvider? provider) => throw new OperationCanceledException();
        public byte ToByte(IFormatProvider? provider) => throw new OperationCanceledException();
        public char ToChar(IFormatProvider? provider) => throw new OperationCanceledException();
        public DateTime ToDateTime(IFormatProvider? provider) => throw new OperationCanceledException();
        public decimal ToDecimal(IFormatProvider? provider) => throw new OperationCanceledException();
        public double ToDouble(IFormatProvider? provider) => throw new OperationCanceledException();
        public short ToInt16(IFormatProvider? provider) => throw new OperationCanceledException();
        public int ToInt32(IFormatProvider? provider) => throw new OperationCanceledException();
        public long ToInt64(IFormatProvider? provider) => throw new OperationCanceledException();
        public sbyte ToSByte(IFormatProvider? provider) => throw new OperationCanceledException();
        public float ToSingle(IFormatProvider? provider) => throw new OperationCanceledException();
        public string ToString(IFormatProvider? provider) => throw new OperationCanceledException();
        public object ToType(Type conversionType, IFormatProvider? provider) => throw new OperationCanceledException();
        public ushort ToUInt16(IFormatProvider? provider) => throw new OperationCanceledException();
        public uint ToUInt32(IFormatProvider? provider) => throw new OperationCanceledException();
        public ulong ToUInt64(IFormatProvider? provider) => throw new OperationCanceledException();
    }

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>
    /// The raw mapper takes a concrete <see cref="DataContext"/>; the record branch never touches the
    /// provider, so a no-op context is enough. It opts into the PostgreSQL raw-row column surface the
    /// real provider exposes.
    /// </summary>
    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => TestDialect.Instance;

        protected override bool SupportsRawRowColumns => true;

        /// <summary>Counts provider-predicate consultations to pin the one-time (not per-row) cadence.</summary>
        public int CompositeProbeCount { get; private set; }

        protected override bool IsGenuineCompositeColumn(DbDataReader reader, int ordinal)
        {
            CompositeProbeCount++;
            return reader is RecordReader fake && fake.IsComposite(ordinal);
        }

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    /// <summary>
    /// A provider that does not surface PostgreSQL raw-row/composite columns: the PostgreSQL-specific
    /// detection and diagnostics must never apply to its raw path.
    /// </summary>
    private sealed class NonPostgresContext : DataContext
    {
        public NonPostgresContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    private sealed class TestDbException(string message) : DbException(message);

    /// <summary>
    /// A fake single-result-set reader exposing an arbitrary column shape. A record column mimics the
    /// Npgsql 10.0.3 shape observed by the D194.1 spike: <c>GetDataTypeName == "record"</c>,
    /// <c>GetFieldType == System.Object[]</c> and a null-preserving <c>System.Object[]</c> value.
    /// </summary>
    private sealed class RecordReader(
        string[] dataTypeNames,
        Type?[] fieldTypes,
        object?[] values,
        bool[]? nulls = null,
        bool[]? composites = null) : DbDataReader
    {
        public static RecordReader Record(object?[] values, bool[]? nulls = null)
            => new(["record"], [typeof(object[])], values, nulls);

        public static RecordReader Composite(bool throwOnFieldType)
            => new(["app.named_composite"], [throwOnFieldType ? null : typeof(NamedComposite)], [new NamedComposite { A = 1, B = "a" }], composites: [true]);

        public static RecordReader Multi(string[] dataTypeNames, Type?[] fieldTypes, object?[] values, bool[]? composites = null)
            => new(dataTypeNames, fieldTypes, values, composites: composites);

        /// <summary>A reader whose data-type-name metadata probe fails (not a driver/connection error).</summary>
        public static RecordReader MetadataFailure()
            => new(["app.named_composite"], [null], [42], composites: [true]) { ThrowOnDataTypeName = true };

        /// <summary>A reader whose data-type-name metadata probe throws a real driver error.</summary>
        public static RecordReader MetadataDriverError()
            => new(["app.named_composite"], [typeof(object)], [42]) { DataTypeNameException = new TestDbException("connection lost") };

        /// <summary>A reader whose field-type metadata probe fails with a disposed-reader fault.</summary>
        public static RecordReader MetadataDisposed()
            => new(["_int4"], [null], [42]) { FieldTypeException = new ObjectDisposedException("reader") };

        /// <summary>A reader whose data-type-name metadata probe throws a plain (non-disposed) InvalidOperationException.</summary>
        public static RecordReader MetadataInvalidOperation()
            => new(["app.named_composite"], [null], [42])
            {
                DataTypeNameException = new InvalidOperationException("metadata probe fault"),
            };

        /// <summary>A reader with a resolvable named composite whose typed read fails with a disposed-reader fault.</summary>
        public static RecordReader NamedCompositeDisposed()
            => new(["app.named_composite"], [typeof(NamedComposite)], [new NamedComposite { A = 1, B = "a" }], composites: [true])
            {
                FieldValueException = new ObjectDisposedException("reader"),
            };

        /// <summary>
        /// A dotted-but-not-composite column (unknown type, e.g. ltree) whose field-type probe throws
        /// <see cref="NotSupportedException"/>, plus an ordinary scalar column.
        /// </summary>
        public static RecordReader UnresolvableNonCompositeAndScalar()
            => new(["public.ltree", "int4"], [typeof(object), typeof(int)], [new object(), 5], composites: [false, false])
            {
                FieldTypeExceptions = [new NotSupportedException("unknown type public.ltree"), null],
            };

        /// <summary>
        /// The explicit per-ordinal composite fact the fake context consumes. Classification is driven by
        /// this fact alone: the data type name and the metadata-probe exception shape are never consulted.
        /// </summary>
        public bool IsComposite(int ordinal) => composites is not null && composites[ordinal];

        public bool ThrowOnDataTypeName { get; init; }

        public Exception? DataTypeNameException { get; init; }

        public Exception? FieldTypeException { get; init; }

        public Exception? FieldValueException { get; init; }

        public Exception?[]? FieldTypeExceptions { get; init; }

        /// <summary>Number of rows <see cref="Read"/> reports before returning <see langword="false"/>.</summary>
        public int ReadCount { get; init; }

        private int _reads;

        public override int FieldCount => dataTypeNames.Length;
        public override int Depth => 0;
        public override bool HasRows => true;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;

        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool IsDBNull(int ordinal)
            => nulls?[ordinal] ?? values[ordinal] is null or DBNull;

        public override object GetValue(int ordinal) => values[ordinal] ?? DBNull.Value;

        public override string GetDataTypeName(int ordinal)
            => DataTypeNameException is { } error
                ? throw error
                : ThrowOnDataTypeName
                    ? throw new NotSupportedException("metadata probe is not supported")
                    : dataTypeNames[ordinal];

        public override Type GetFieldType(int ordinal)
            => FieldTypeExceptions?[ordinal] is { } perField
                ? throw perField
                : FieldTypeException is { } error
                    ? throw error
                    : fieldTypes[ordinal] ?? throw new InvalidCastException(
                        $"Reading as 'System.Object' is not supported for fields having DataTypeName '{dataTypeNames[ordinal]}'");

        public override T GetFieldValue<T>(int ordinal)
            => FieldValueException is { } error ? throw error : base.GetFieldValue<T>(ordinal);

        public override string GetName(int ordinal) => "c" + ordinal;

        public override int GetOrdinal(string name)
        {
            for (var i = 0; i < dataTypeNames.Length; i++)
            {
                if (dataTypeNames[i] == name)
                    return i;
            }

            throw new IndexOutOfRangeException(name);
        }

        public override int GetValues(object[] valuesOut)
        {
            var count = Math.Min(valuesOut.Length, values.Length);
            for (var i = 0; i < count; i++)
                valuesOut[i] = GetValue(i);

            return count;
        }

        public override bool Read() => _reads++ < ReadCount;

        public override bool NextResult() => false;

        public override IEnumerator GetEnumerator() => values.GetEnumerator();

        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => (char)GetValue(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
        public override string GetString(int ordinal) => (string)GetValue(ordinal);
    }
}
