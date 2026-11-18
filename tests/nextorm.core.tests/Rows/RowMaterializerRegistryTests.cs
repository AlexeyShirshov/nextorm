using System.Data;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Unit coverage for the compile-time materializer seam: the shape offered to a provider, the shapes
/// that must never be offered, and the fallback to the built-in expression path when no provider
/// claims a shape.
/// </summary>
[Collection("RowMaterializerRegistry")]
public sealed class RowMaterializerRegistryTests : IDisposable
{
    public RowMaterializerRegistryTests() => RowMaterializerRegistry.Reset();

    public void Dispose() => RowMaterializerRegistry.Reset();

    [Fact]
    public void TryCreate_ShouldOfferEveryColumnAndReturnTheProviderDelegate()
    {
        var provider = new RecordingProvider(static (type, columns) =>
        {
            var id = columns[0].Ordinal;
            return (Func<IDataRecord, int>)(reader => reader.GetInt32(id));
        });
        RowMaterializerRegistry.Register(provider);

        var selectList = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id", PropertyType = typeof(int) },
            new SelectExpression(typeof(string)) { Index = 1, PropertyName = "Name", PropertyType = typeof(string) },
        };

        var created = RowMaterializerRegistry.TryCreate(typeof(int), selectList, out var materializer);

        created.Should().BeTrue();
        provider.Calls.Should().Be(1);
        provider.LastResultType.Should().Be(typeof(int));
        provider.LastColumns.Should().HaveCount(2);
        provider.LastColumns![0].Should().Be(new RowMaterializerColumn(0, "Id", typeof(int), nullable: false));
        // A reference type is nullable by CLR shape, matching SelectExpression.Nullable.
        provider.LastColumns[1].Should().Be(new RowMaterializerColumn(1, "Name", typeof(string), nullable: true));

        using var reader = ReadSingle(7);
        materializer.Should().BeOfType<Func<IDataRecord, int>>();
        ((Func<IDataRecord, int>)materializer!)(reader).Should().Be(7);
    }

    [Fact]
    public void TryCreate_ShouldFallBackWhenNoProviderIsRegistered()
    {
        var selectList = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyType = typeof(int) } };

        RowMaterializerRegistry.TryCreate(typeof(int), selectList, out var materializer).Should().BeFalse();
        materializer.Should().BeNull();
    }

    [Fact]
    public void TryCreate_ShouldFallBackWhenTheProviderDeclines()
    {
        var provider = new RecordingProvider(static (_, _) => null);
        RowMaterializerRegistry.Register(provider);
        var selectList = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyType = typeof(int) } };

        RowMaterializerRegistry.TryCreate(typeof(int), selectList, out var materializer).Should().BeFalse();
        provider.Calls.Should().Be(1);
        materializer.Should().BeNull();
    }

    [Fact]
    public void TryCreate_ShouldStopAtTheFirstProviderThatClaimsTheShape()
    {
        var declining = new RecordingProvider(static (_, _) => null);
        var claiming = new RecordingProvider(static (_, _) => (Func<IDataRecord, int>)(reader => reader.GetInt32(0)));
        var neverReached = new RecordingProvider(static (_, _) => (Func<IDataRecord, int>)(reader => reader.GetInt32(0)));
        RowMaterializerRegistry.Register(declining);
        RowMaterializerRegistry.Register(claiming);
        RowMaterializerRegistry.Register(neverReached);
        var selectList = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyType = typeof(int) } };

        RowMaterializerRegistry.TryCreate(typeof(int), selectList, out _).Should().BeTrue();

        declining.Calls.Should().Be(1);
        claiming.Calls.Should().Be(1);
        neverReached.Calls.Should().Be(0);
    }

    [Fact]
    public void Register_ShouldIgnoreTheSameInstance()
    {
        var provider = new RecordingProvider(static (_, _) => null);
        RowMaterializerRegistry.Register(provider);
        RowMaterializerRegistry.Register(provider);
        var selectList = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyType = typeof(int) } };

        RowMaterializerRegistry.TryCreate(typeof(int), selectList, out _);

        provider.Calls.Should().Be(1);
    }

    [Fact]
    public void Register_ShouldRejectNull()
    {
        var act = static () => RowMaterializerRegistry.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    public static TheoryData<string, Action<SelectExpression>> IneligibleShapes => new()
    {
        { "streaming LOB", static column => column.IsLobStreaming = true },
        { "dynamic-columns store", static column => column.IsDynamicColumnsStore = true },
        { "range pair", static column => column.RangeColumnRole = RangeColumnRole.Lower },
        { "widened count", static column => column.IsWideCountNarrowed = true },
        { "value converter", static column => column.Converter = new PassthroughConverter() },
        { "unpinned duration unit", static column => column.DurationUnit = DurationUnit.Milliseconds },
        { "type with a pinned GetFieldValue accessor", static column => column.PropertyType = typeof(TimeSpan) },
    };

    [Theory]
    [MemberData(nameof(IneligibleShapes))]
    public void TryCreate_ShouldNeverOfferAShapeWithProviderSpecificHandling(string _, Action<SelectExpression> configure)
    {
        var provider = new RecordingProvider(static (_, _) => (Func<IDataRecord, object>)(_ => new object()));
        RowMaterializerRegistry.Register(provider);
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyType = typeof(int), PropertyName = "Value" };
        configure(column);

        RowMaterializerRegistry.TryCreate(typeof(object), [column], out var materializer).Should().BeFalse();

        provider.Calls.Should().Be(0);
        materializer.Should().BeNull();
    }

    private static DataTableReader ReadSingle(int value)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Rows.Add(value);
        var reader = table.CreateDataReader();
        reader.Read();
        return reader;
    }

    private sealed class RecordingProvider(Func<Type, RowMaterializerColumn[], Delegate?> factory) : IRowMaterializerProvider
    {
        public int Calls { get; private set; }

        public Type? LastResultType { get; private set; }

        public RowMaterializerColumn[]? LastColumns { get; private set; }

        public Delegate? TryCreate(Type resultType, ReadOnlySpan<RowMaterializerColumn> columns)
        {
            Calls++;
            LastResultType = resultType;
            LastColumns = columns.ToArray();
            return factory(resultType, LastColumns);
        }
    }

    private sealed class PassthroughConverter : IPropertyValueConverter
    {
        public Type ProviderType => typeof(int);

        public bool ConvertsNulls => false;

        public object? ConvertFromProvider(object? value) => value;

        public object? ConvertToProvider(object? value) => value;
    }
}
