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

/// <summary>Parameterless DTO: the generated materializer binds columns by member name.</summary>
[RowMaterializer]
public sealed class GeneratedNote
{
    public int Id { get; set; }

    public string? Name { get; set; }
}
