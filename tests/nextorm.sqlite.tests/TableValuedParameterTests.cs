using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQLite emulates a table-valued parameter with a JSON text parameter: scalars are read through
/// <c>json_each(@p)</c>, entities through <c>json_extract(value, '$.col')</c>.
/// </summary>
public class TableValuedParameterTests
{
    private sealed class TvpRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private static SqliteDataContext CreateContext() => new("Data Source=:memory:", new DataContextBuilder());

    [Fact]
    public void SupportsTableValuedParameters_IsTrue()
    {
        SqliteDialect.Instance.SupportsTableValuedParameters.Should().BeTrue();
    }

    [Fact]
    public void ScalarList_ShouldBeReadThroughJsonEach()
    {
        using var ctx = CreateContext();

        using (var result = ctx.ExecuteRaw(
            "select count(*) as c from json_each(@ids)",
            [ProcedureParameter.Table("ids", new[] { 1, 2, 3 })]))
        {
            result.Read<long>().Should().Equal(3L);
        }

        using (var result = ctx.ExecuteRaw(
            "select sum(value) as s from json_each(@ids)",
            [ProcedureParameter.Table("ids", new[] { 1, 2, 3 })]))
        {
            result.Read<long>().Should().Equal(6L);
        }
    }

    [Fact]
    public void EmptyScalarList_ShouldReadZeroRows()
    {
        using var ctx = CreateContext();

        using var result = ctx.ExecuteRaw(
            "select count(*) as c from json_each(@ids)",
            [ProcedureParameter.Table("ids", Array.Empty<int>())]);

        result.Read<long>().Should().Equal(0L);
    }

    [Fact]
    public void EntityList_ShouldBeReadThroughJsonExtract()
    {
        using var ctx = CreateContext();

        var rows = new[]
        {
            new TvpRow { Id = 1, Name = "alpha" },
            new TvpRow { Id = 2, Name = "beta" },
        };

        using var result = ctx.ExecuteRaw(
            "select json_extract(value, '$.Id') as Id, json_extract(value, '$.Name') as Name from json_each(@rows) order by Id",
            [ProcedureParameter.Table("rows", rows)]);

        var read = result.Read<TvpRow>();

        read.Should().HaveCount(2);
        read[0].Id.Should().Be(1);
        read[0].Name.Should().Be("alpha");
        read[1].Id.Should().Be(2);
        read[1].Name.Should().Be("beta");
    }

    [Fact]
    public void NullableScalarList_WithNullElements_ShouldKeepNulls()
    {
        using var ctx = CreateContext();
        var rows = new int?[] { 1, null, 3 };

        using (var result = ctx.ExecuteRaw("select count(*) as c from json_each(@ids)", [ProcedureParameter.Table("ids", rows)]))
            result.Read<long>().Should().Equal(3L);

        using (var result = ctx.ExecuteRaw("select count(value) as c from json_each(@ids)", [ProcedureParameter.Table("ids", rows)]))
            result.Read<long>().Should().Equal(2L);

        using (var result = ctx.ExecuteRaw("select sum(value) as s from json_each(@ids)", [ProcedureParameter.Table("ids", rows)]))
            result.Read<long>().Should().Equal(4L);
    }

    [Fact]
    public void UnicodeScalarList_ShouldRoundTrip()
    {
        using var ctx = CreateContext();

        using var result = ctx.ExecuteRaw(
            "select value from json_each(@ids) order by value",
            [ProcedureParameter.Table("ids", new[] { "юникод", "a\"b" })]);

        result.Read<string>().Should().Equal("a\"b", "юникод");
    }

    [Fact]
    public void TypeName_OnSqlite_ShouldThrowArgumentException()
    {
        using var ctx = CreateContext();

        var act = () => ctx.ExecuteRaw("select 1", [ProcedureParameter.Table("p", "dbo.MyType", new[] { 1 })]);

        act.Should().Throw<ArgumentException>();
    }
}
