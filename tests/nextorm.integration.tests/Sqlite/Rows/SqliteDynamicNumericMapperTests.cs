using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// The narrow JIT route for numeric row materializers, exercised on a real file-based SQLite database:
/// a single numeric column uses the expression path on JIT (the generated numeric materializer resolves
/// <see cref="IDataRecord.GetFieldType"/> per row and is kept for NativeAOT), while a multi-column
/// numeric shape keeps the generated route and its per-row widening across readers whose value types
/// change. SQLite stores 1 and 2.5 in the same dynamic column, so a cached field type would truncate the
/// real value; these tests pin the actual values and the actual selected mapper.
/// </summary>
public sealed class SqliteDynamicNumericMapperTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public SqliteDynamicNumericMapperTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.s2fix.dynamic.{Environment.ProcessId}.{Guid.NewGuid():N}.db");
        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table dyn_value (v);
            insert into dyn_value (v) values (1), (2.5);

            create table dyn_nullable (v);
            insert into dyn_nullable (v) values (null), (1), (2.5);

            create table dyn_row (id integer, v);
            insert into dyn_row (id, v) values (1, 1), (2, 2.5);

            create table dyn_text (name text, key text);
            insert into dyn_text (name, key) values ('alpha', 'one'), ('beta', 'two');

            create table conv (v);
            insert into conv (v) values (0), (86400);

            create table NumericStructValue (v);
            insert into NumericStructValue (v) values (1), (2.5);

            create table partial_row (id integer, other integer);
            insert into partial_row (id, other) values (7, 99);
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    [Fact]
    public async Task SingleNumericColumn_ShouldUseTheExpressionPathOnJitAndReadDynamicValues()
    {
        var command = _db.From<DynValue>().PrepareFromSql("select v from dyn_value order by rowid");

        var first = await command.ToListAsync(_db, TestContext.Current.CancellationToken);
        var second = await command.ToListAsync(_db, TestContext.Current.CancellationToken);

        first.Select(row => row.V).Should().Equal(1.0, 2.5);
        second.Select(row => row.V).Should().Equal(1.0, 2.5);

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            var method = MapperMethod(command);
            method.Should().NotBeNull();
            method!.Module.Assembly.IsDynamic.Should().BeTrue(
                "a single-column numeric shape stays on the expression path on JIT");
        }
    }

    [Fact]
    public async Task NullableSingleNumericColumn_ShouldReadNullThenIntegerThenReal()
    {
        var command = _db.From<DynNullableValue>().PrepareFromSql("select v from dyn_nullable order by rowid");

        var rows = await command.ToListAsync(_db, TestContext.Current.CancellationToken);

        rows.Select(row => row.V).Should().Equal(null, 1.0, 2.5);
    }

    [Fact]
    public async Task MultiNumericColumns_ShouldKeepTheGeneratedRouteAndWidenPerRowAcrossReaders()
    {
        var command = _db.From<DynRow>()
            .Where(row => row.Id == SqlFunctions.Parameter<int>(0))
            .Prepare(default, TestContext.Current.CancellationToken);

        var integerRow = await command.ToListAsync(_db, TestContext.Current.CancellationToken, 1);
        var realRow = await command.ToListAsync(_db, TestContext.Current.CancellationToken, 2);

        integerRow.Should().ContainSingle().Which.V.Should().Be(1.0);
        realRow.Should().ContainSingle().Which.V.Should().Be(2.5);

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            var method = MapperMethod(command);
            method.Should().NotBeNull();
            method!.Module.Assembly.IsDynamic.Should().BeFalse(
                "a multi-column numeric shape keeps the generated materializer on JIT");
            method.DeclaringType!.FullName.Should().Contain("__NextOrmRowMaterializerProvider");
        }
    }

    [Fact]
    public async Task NonNumericMultiColumn_ShouldKeepTheGeneratedRoute()
    {
        var command = _db.From<DynText>().PrepareFromSql("select name, key from dyn_text order by rowid");

        var rows = await command.ToListAsync(_db, TestContext.Current.CancellationToken);

        rows.Select(row => (row.Name, row.Key)).Should().Equal(("alpha", "one"), ("beta", "two"));

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            var method = MapperMethod(command);
            method.Should().NotBeNull();
            method!.Module.Assembly.IsDynamic.Should().BeFalse(
                "a non-numeric multi-column shape keeps the generated materializer on JIT");
        }
    }

    [Fact]
    public async Task SingleNumericConstructorDto_ShouldKeepTheGeneratedRoute()
    {
        var command = _db.From<NumericCtorValue>().PrepareFromSql("select v from dyn_value order by rowid");

        var rows = await command.ToListAsync(_db, TestContext.Current.CancellationToken);

        rows.Select(row => row.V).Should().Equal(1.0, 2.5);

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            var method = MapperMethod(command);
            method.Should().NotBeNull();
            method!.Module.Assembly.IsDynamic.Should().BeFalse(
                "a constructor-only numeric DTO keeps the generated route on JIT");
        }
    }

    [Fact]
    public async Task SingleNumericReadonlyStruct_ShouldConstructThroughTheExpressionPath()
    {
        var command = _db.From<NumericStructValue>().PrepareFromSql("select v from NumericStructValue order by rowid");

        var rows = await command.ToListAsync(_db, TestContext.Current.CancellationToken);

        rows.Select(row => row.V).Should().Equal(1.0, 2.5);
    }

    [Fact]
    public async Task SingleNumericProviderWithRegisteredConverter_ShouldKeepTheConverterSemantics()
    {
        var command = _db.From<ConvertedSeconds>().PrepareFromSql("select v from conv order by rowid");

        var rows = await command.ToListAsync(_db, TestContext.Current.CancellationToken);

        rows.Select(row => row.When).Should().Equal(DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(1));
    }

    [Fact]
    public void PartialEntityWithAlternateConstructors_ShouldMaterializeGeneratedMemberBindingFromSqliteReader()
    {
        // The exact reviewer shape: a parameterless plus a longer public constructor and a one-column
        // projection. The generated member-init route must claim it and materialize Id from a real
        // SQLite reader; the expression fallback would resolve the longer constructor and fail.
        RowMaterializerRegistry.Register(NextORM.Core.Generated.__NextOrmRowMaterializerProvider.Instance);
        var columns = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id", PropertyType = typeof(int) },
        };

        RowMaterializerRegistry.TryCreate(typeof(PartialRowEntity), columns, out var materializer)
            .Should().BeTrue("the generated member-init route handles the one-column shape");
        materializer.Should().NotBeNull();

        using var connection = new SqliteConnection($"Data Source='{_databasePath}'");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select id from partial_row";
        using var reader = command.ExecuteReader();
        reader.Read();

        var row = ((Func<IDataRecord, PartialRowEntity>)materializer!)(reader);

        row.Id.Should().Be(7);
        row.Other.Should().Be(0);
    }

    private static MethodInfo? MapperMethod(object command)
    {
        for (var type = command.GetType(); type is not null && type != typeof(object); type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.Name == "MapDelegate" && field.GetValue(command) is Delegate mapper)
                    return mapper.Method;
            }
        }

        return null;
    }
}

[Table("dyn_value")]
public sealed class DynValue
{
    [Column("v")]
    public double V { get; set; }
}

[Table("dyn_nullable")]
public sealed class DynNullableValue
{
    [Column("v")]
    public double? V { get; set; }
}

[Table("dyn_row")]
public sealed class DynRow
{
    [Column("id")]
    public int Id { get; set; }

    [Column("v")]
    public double V { get; set; }
}

[Table("dyn_text")]
public sealed class DynText
{
    [Column("name")]
    public string? Name { get; set; }

    [Column("key")]
    public string? Key { get; set; }
}

[Table("dyn_value")]
public sealed record NumericCtorValue([property: Column("v")] double V);

public readonly record struct NumericStructValue([property: Column("v")] double V);

[Table("conv")]
public sealed class ConvertedSeconds
{
    [Column("v")]
    [ValueConverter(typeof(UnixSecondsConverter))]
    public DateTime When { get; set; }
}

public sealed class UnixSecondsConverter : IPropertyValueConverter
{
    public Type ProviderType => typeof(long);

    public bool ConvertsNulls => false;

    public object? ConvertFromProvider(object? value)
        => value is null or DBNull ? null : DateTime.UnixEpoch.AddSeconds(Convert.ToInt64(value));

    public object? ConvertToProvider(object? value)
        => value is DateTime when ? (long)(when - DateTime.UnixEpoch).TotalSeconds : null;
}

[Table("partial_row")]
public sealed class PartialRowEntity
{
    public PartialRowEntity()
    {
    }

    public PartialRowEntity(int id, int other)
    {
        Id = id;
        Other = other;
    }

    public int Id { get; set; }

    public int Other { get; set; }
}
