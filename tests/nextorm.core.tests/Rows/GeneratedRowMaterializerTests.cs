using System.Data;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// End-to-end coverage of the generated row materializer: the source generator runs over this
/// assembly, so the marked types below get a provider at build time. The tests register that provider
/// explicitly (the module initializer does it for a real process) and compare its output with the
/// values a typed reader returns.
/// </summary>
[Collection("RowMaterializerRegistry")]
public sealed class GeneratedRowMaterializerTests : IDisposable
{
    public GeneratedRowMaterializerTests() => RowMaterializerRegistry.Reset();

    public void Dispose() => RowMaterializerRegistry.Reset();

    [Fact]
    public void GeneratedProvider_ShouldMaterializeAConstructorBoundRow()
    {
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id", PropertyType = typeof(int) },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = "Name", PropertyType = typeof(string) },
            new SelectExpression(typeof(decimal)) { Index = 2, PropertyName = "Amount", PropertyType = typeof(decimal) },
        };

        RowMaterializerRegistry.TryCreate(typeof(GeneratedOrder), columns, out var materializer).Should().BeTrue();
        materializer.Should().BeOfType<Func<IDataRecord, GeneratedOrder>>();

        using var reader = Read(
            ("Id", typeof(int), 7),
            ("Name", typeof(string), "widgets"),
            ("Amount", typeof(decimal), 12.5m));

        ((Func<IDataRecord, GeneratedOrder>)materializer!)(reader).Should().BeEquivalentTo(new GeneratedOrder(7, "widgets", 12.5m));
    }

    [Fact]
    public void GeneratedProvider_ShouldMapNullToNullForANullableReferenceMember()
    {
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id", PropertyType = typeof(int) },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = "Name", PropertyType = typeof(string) },
        };

        RowMaterializerRegistry.TryCreate(typeof(GeneratedNote), columns, out var materializer).Should().BeTrue();

        using var reader = Read(
            ("Id", typeof(int), 3),
            ("Name", typeof(string), DBNull.Value));

        var note = ((Func<IDataRecord, GeneratedNote>)materializer!)(reader);

        note.Id.Should().Be(3);
        note.Name.Should().BeNull();
    }

    [Fact]
    public void GeneratedProvider_ShouldDeclineAShapeWhoseColumnTypeDoesNotMatchTheMember()
    {
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id", PropertyType = typeof(int) },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = "Name", PropertyType = typeof(int) },
        };

        RowMaterializerRegistry.TryCreate(typeof(GeneratedNote), columns, out var materializer).Should().BeFalse();
        materializer.Should().BeNull();
    }

    [Fact]
    public void GeneratedProvider_ShouldCoverATypeWithoutTheAttribute()
    {
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = "Name" },
        };

        RowMaterializerRegistry.TryCreate(typeof(AutoCoveredNote), columns, out var materializer).Should().BeTrue(
            "every materializable type in the assembly is covered, an annotation is not required");

        using var reader = Read(("Id", typeof(int), 5), ("Name", typeof(string), DBNull.Value));
        var note = ((Func<IDataRecord, AutoCoveredNote>)materializer!)(reader);

        note.Id.Should().Be(5);
        note.Name.Should().BeNull();
    }

    [Fact]
    public void GeneratedProvider_ShouldCoverAPositionalRecordWithoutTheAttribute()
    {
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = "Name" },
        };

        RowMaterializerRegistry.TryCreate(typeof(AutoCoveredOrder), columns, out var materializer).Should().BeTrue();

        using var reader = Read(("Id", typeof(int), 9), ("Name", typeof(string), "pallet"));
        ((Func<IDataRecord, AutoCoveredOrder>)materializer!)(reader).Should().Be(new AutoCoveredOrder(9, "pallet"));
    }

    [Fact]
    public void GeneratedProvider_ShouldCoverAnAnonymousShapeByShape()
    {
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "AutoCoveredId" },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = "AutoCoveredName" },
        };

        // An anonymous type has no name the generator could write, so it is matched by shape: this is
        // the same type the generated provider recorded for the identical property names and types.
        RowMaterializerRegistry.TryCreate(new { AutoCoveredId = 0, AutoCoveredName = (string?)null }.GetType(), columns, out var materializer)
            .Should().BeTrue();

        using var reader = Read(("AutoCoveredId", typeof(int), 11), ("AutoCoveredName", typeof(string), DBNull.Value));
        var row = ((Func<IDataRecord, object?>)materializer!)(reader);

        row.Should().BeEquivalentTo(new { AutoCoveredId = 11, AutoCoveredName = (string?)null });
    }

    [Theory]
    [InlineData(typeof(long), typeof(int), 7, "AutoWideId")]
    [InlineData(typeof(int), typeof(long), 7L, "AutoNarrowId")]
    public void GeneratedProvider_ShouldReadAStorageTypeWiderOrNarrowerThanTheProjection(Type declared, Type storage, object value, string name)
    {
        // SQL Server reports an int column for a projection declared as long (and a bigint for one declared
        // as int); a fixed GetInt64/GetInt32 threw InvalidCastException there, so the generated materializer
        // dispatches on the runtime field type and widens with the typed Convert.
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        // Two columns keep this shape on the generated route on JIT (the single-column numeric shape is
        // the narrow AOT-only route); the numeric column still exercises the per-row widening dispatch.
        var label = name + "Label";
        var columns = new[]
        {
            new SelectExpression(declared) { Index = 0, PropertyName = name },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = label },
        };

        var shape = name == "AutoWideId"
            ? new { AutoWideId = 0L, AutoWideLabel = "" }.GetType()
            : new { AutoNarrowId = 0, AutoNarrowLabel = "" }.GetType();

        RowMaterializerRegistry.TryCreate(shape, columns, out var materializer).Should().BeTrue();
        using var reader = Read((name, storage, value), (label, typeof(string), "x"));
        var row = ((Func<IDataRecord, object?>)materializer!)(reader);

        row!.GetType().GetProperty(name)!.GetValue(row).Should().Be(Convert.ChangeType(value, declared));
    }

    [Fact]
    public void GeneratedProvider_ShouldKeepTheNarrowJitFallbackForSingleNumericColumnsOnly()
    {
        Assert.SkipUnless(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported,
            "the narrow fallback is the JIT route; NativeAOT always keeps the generated numeric materializer");
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);

        var single = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Value" } };
        RowMaterializerRegistry.TryCreate(typeof(SingleNumericClass), single, out var singleMaterializer)
            .Should().BeFalse("a parameterless-only single-column numeric class is the narrow JIT expression-path route");
        singleMaterializer.Should().BeNull();

        RowMaterializerRegistry.TryCreate(new { Value = 0 }.GetType(), single, out var anonymousMaterializer)
            .Should().BeTrue("an anonymous type is constructor-only and keeps the generated route");
        anonymousMaterializer.Should().NotBeNull();

        var multi = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "A" },
            new SelectExpression(typeof(int)) { Index = 1, PropertyName = "B" },
        };
        RowMaterializerRegistry.TryCreate(new { A = 0, B = 0 }.GetType(), multi, out var multiMaterializer)
            .Should().BeTrue("multi-column numeric shapes keep the generated route on JIT");
        multiMaterializer.Should().NotBeNull();

        var text = new[] { new SelectExpression(typeof(string)) { Index = 0, PropertyName = "Text" } };
        RowMaterializerRegistry.TryCreate(new { Text = "" }.GetType(), text, out var textMaterializer)
            .Should().BeTrue("non-numeric single-column shapes keep the generated route on JIT");
        textMaterializer.Should().NotBeNull();

        var valueType = new[] { new SelectExpression(typeof(double)) { Index = 0, PropertyName = "Value" } };
        RowMaterializerRegistry.TryCreate(typeof(SingleNumericStruct), valueType, out var valueTypeMaterializer)
            .Should().BeTrue("a single numeric value-type DTO keeps the generated route; the expression path may not construct it");
        valueTypeMaterializer.Should().NotBeNull();
    }

    [Fact]
    public void GeneratedProvider_ShouldDeclineAnAnonymousShapeWithAnUnsupportedMember()
    {
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "AutoCoveredBytes" },
        };

        RowMaterializerRegistry.TryCreate(new { AutoCoveredBytes = Array.Empty<byte>() }.GetType(), columns, out var materializer)
            .Should().BeFalse("byte[] is outside the plain accessor set, so the shape stays on the expression path");
        materializer.Should().BeNull();
    }

    [Fact]
    public void GeneratedProvider_ShouldKeepTheGeneratedRouteForAPartialEntityWithAlternateConstructors()
    {
        // The expression fallback resolves the longest constructor (id, other) and cannot build a
        // one-column shape; the generated member-init branch can, so this type must not take the
        // single-column JIT fallback.
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id", PropertyType = typeof(int) },
        };

        RowMaterializerRegistry.TryCreate(typeof(PartialCtorRow), columns, out var materializer)
            .Should().BeTrue("a type with an alternate public constructor keeps the generated member-init route");

        using var reader = Read(("Id", typeof(int), 7));
        var row = ((Func<IDataRecord, PartialCtorRow>)materializer!)(reader);
        row.Id.Should().Be(7);
        row.Other.Should().Be(0);
    }

    private static DataTableReader Read(params (string Name, Type Type, object Value)[] values)
    {
        var table = new DataTable();
        foreach (var (name, type, _) in values)
            table.Columns.Add(name, type);
        table.Rows.Add(values.Select(static v => v.Value).ToArray());
        var reader = table.CreateDataReader();
        reader.Read();
        return reader;
    }
}

/// <summary>Constructor-bound DTO: the generated materializer binds columns positionally.</summary>
[RowMaterializer]
public sealed class GeneratedOrder
{
    public GeneratedOrder(int id, string? name, decimal amount)
    {
        Id = id;
        Name = name;
        Amount = amount;
    }

    public int Id { get; }

    public string? Name { get; }

    public decimal Amount { get; }
}

/// <summary>An unannotated parameterless DTO: covered because it is materializable, not because of an attribute.</summary>
public sealed class AutoCoveredNote
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>An unannotated positional record: covered by the constructor branch.</summary>
public sealed record AutoCoveredOrder(int Id, string? Name);

/// <summary>Parameterless DTO: the generated materializer binds columns by member name.</summary>
[RowMaterializer]
public sealed class GeneratedNote
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>A value-type single numeric DTO: the narrow JIT fallback is reference-type only.</summary>
public readonly record struct SingleNumericStruct(double Value);

/// <summary>Parameterless plus a longer constructor: a one-column shape must use member-init.</summary>
public sealed class PartialCtorRow
{
    public PartialCtorRow()
    {
    }

    public PartialCtorRow(int id, int other)
    {
        Id = id;
        Other = other;
    }

    public int Id { get; set; }

    public int Other { get; set; }
}

/// <summary>A parameterless-only single numeric class: the narrow JIT fallback applies to this shape.</summary>
public sealed class SingleNumericClass
{
    public int Value { get; set; }
}
