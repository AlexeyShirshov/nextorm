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
        var columns = name == "AutoWideId"
            ? [new SelectExpression(declared) { Index = 0, PropertyName = name }]
            : new[] { new SelectExpression(declared) { Index = 0, PropertyName = name } };

        var shape = name == "AutoWideId"
            ? new { AutoWideId = 0L }.GetType()
            : new { AutoNarrowId = 0 }.GetType();

        RowMaterializerRegistry.TryCreate(shape, columns, out var materializer).Should().BeTrue();
        using var reader = Read((name, storage, value));
        var row = ((Func<IDataRecord, object?>)materializer!)(reader);

        row!.GetType().GetProperty(name)!.GetValue(row).Should().Be(Convert.ChangeType(value, declared));
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
