using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public sealed class JsonColumnProbePoco
{
    public string? Name { get; set; }
    public List<int> Values { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}

public abstract partial class CommonTestSuite
{
    [SqlTable("json_column_probe")]
    public interface IJsonColumnProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("data")]
        [JsonColumn]
        JsonColumnProbePoco Data { get; set; }

        [Column("data_null")]
        [JsonColumn]
        JsonColumnProbePoco? DataNull { get; set; }
    }

    public sealed class JsonColumnProbe : IJsonColumnProbe
    {
        public long Id { get; set; }
        public JsonColumnProbePoco Data { get; set; } = new();
        public JsonColumnProbePoco? DataNull { get; set; }
    }

    [Fact]
    public void JsonColumn_ShouldRoundTrip()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var dataType = dialect.SupportsJson ? "jsonb" : ProbeTextType(dialect);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteJsonColumn(ctx, "drop table if exists json_column_probe");
        ExecuteJsonColumn(ctx, $"create table json_column_probe (id bigint, data {dataType}, data_null {dataType}){engine}");

        try
        {
            var updatedAt = new DateTimeOffset(2026, 9, 25, 12, 34, 56, TimeSpan.Zero);
            var withNull = new JsonColumnProbePoco { Name = "second", Values = [4, 5], UpdatedAt = updatedAt };
            ctx.InsertInto<IJsonColumnProbe>()
                .Values(new JsonColumnProbe { Id = 1, Data = new JsonColumnProbePoco { Name = "first", Values = [1, 2, 3], UpdatedAt = updatedAt }, DataNull = null })
                .Insert();
            ctx.InsertInto<IJsonColumnProbe>()
                .Values(new JsonColumnProbe { Id = 2, Data = withNull, DataNull = withNull })
                .Insert();

            var first = ctx.From<JsonColumnProbe>().Where(x => x.Id == 1).ToList().Single();
            first.Data.Name.Should().Be("first");
            first.Data.Values.Should().Equal(1, 2, 3);
            first.Data.UpdatedAt.Should().Be(updatedAt);
            first.DataNull.Should().BeNull();

            var second = ctx.From<JsonColumnProbe>().Where(x => x.Id == 2).ToList().Single();
            second.Data.Name.Should().Be("second");
            second.Data.UpdatedAt.Should().Be(updatedAt);
            second.DataNull.Should().NotBeNull();
            second.DataNull!.Values.Should().Equal(4, 5);

            // A JSON value in a SET list goes through the same converter.
            ctx.Update<IJsonColumnProbe>()
                .Set(x => x.Data, new JsonColumnProbePoco { Name = "updated", Values = [9], UpdatedAt = updatedAt })
                .Where(x => x.Id == 1)
                .Update();

            var updated = ctx.From<JsonColumnProbe>().Where(x => x.Id == 1).ToList().Single();
            updated.Data.Name.Should().Be("updated");
            updated.Data.Values.Should().Equal(9);
            updated.Data.UpdatedAt.Should().Be(updatedAt);
        }
        finally
        {
            ExecuteJsonColumn(ctx, "drop table if exists json_column_probe");
        }
    }

    [Fact]
    public void JsonColumn_Returning_ShouldMaterialize()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var dataType = dialect.SupportsJson ? "jsonb" : ProbeTextType(dialect);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteJsonColumn(ctx, "drop table if exists json_column_probe");
        ExecuteJsonColumn(ctx, $"create table json_column_probe (id bigint, data {dataType}, data_null {dataType}){engine}");

        try
        {
            var row = ctx.InsertInto<JsonColumnProbe>()
                .Values(new JsonColumnProbe { Id = 10, Data = new JsonColumnProbePoco { Name = "returned", Values = [7] }, DataNull = null })
                .Returning()
                .Single();

            row.Data.Name.Should().Be("returned");
            row.Data.Values.Should().Equal(7);
        }
        finally
        {
            ExecuteJsonColumn(ctx, "drop table if exists json_column_probe");
        }
    }

    [Fact]
    public void JsonColumn_Queries_ShouldConvertConstantsAndProjections()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var dataType = dialect.SupportsJson ? "jsonb" : ProbeTextType(dialect);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteJsonColumn(ctx, "drop table if exists json_column_probe");
        ExecuteJsonColumn(ctx, $"create table json_column_probe (id bigint, data {dataType}, data_null {dataType}){engine}");

        try
        {
            var updatedAt = new DateTimeOffset(2026, 9, 25, 12, 34, 56, TimeSpan.Zero);
            var data = new JsonColumnProbePoco { Name = "first", Values = [1, 2, 3], UpdatedAt = updatedAt };
            ctx.InsertInto<IJsonColumnProbe>()
                .Values(new JsonColumnProbe { Id = 1, Data = data, DataNull = null })
                .Insert();

            // A scalar projection materializes the CLR model, not the provider representation.
            ctx.From<JsonColumnProbe>().Where(x => x.Id == 1).Select(x => x.Data).ToList()
                .Should().ContainSingle().Which.Name.Should().Be("first");

            // An anonymous projection carries the same conversion.
            ctx.From<JsonColumnProbe>().Where(x => x.Id == 1).Select(x => new { x.Data }).ToList()
                .Should().ContainSingle().Which.Data.Values.Should().Equal(1, 2, 3);

            // A constant compared against a JSON column is serialized to its provider representation.
            ctx.From<JsonColumnProbe>().Where(x => x.Data == data).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(1);
        }
        finally
        {
            ExecuteJsonColumn(ctx, "drop table if exists json_column_probe");
        }
    }

    private static void ExecuteJsonColumn(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
