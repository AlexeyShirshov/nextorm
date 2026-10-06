using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL-only regression coverage for <see cref="JsonColumnAttribute"/> against a physical
/// <c>json</c> column and a physical <c>jsonb</c> column (the shared suite picks one physical type
/// from the dialect and therefore exercises only <c>jsonb</c> here). Backed by a Testcontainers
/// instance unless NEXTORM_POSTGRES_CONNECTION points at an existing server.
/// </summary>
public sealed class PostgresJsonColumnTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    public sealed class Nested
    {
        public string? Inner { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    public sealed class Shape
    {
        public string? Name { get; set; }
        public int Count { get; set; }
        public bool Flag { get; set; }
        public Nested? Nested { get; set; }
    }

    [SqlTable("pg_json_col_probe_jsonb")]
    public interface IJsonbProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("data")]
        [JsonColumn]
        Shape Data { get; set; }

        [Column("data_null")]
        [JsonColumn]
        Shape? DataNull { get; set; }
    }

    public sealed class JsonbProbe : IJsonbProbe
    {
        public long Id { get; set; }
        public Shape Data { get; set; } = new();
        public Shape? DataNull { get; set; }
    }

    [SqlTable("pg_json_col_probe_json")]
    public interface IJsonProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("data")]
        [JsonColumn]
        Shape Data { get; set; }

        [Column("data_null")]
        [JsonColumn]
        Shape? DataNull { get; set; }
    }

    public sealed class JsonProbe : IJsonProbe
    {
        public long Id { get; set; }
        public Shape Data { get; set; } = new();
        public Shape? DataNull { get; set; }
    }

    [Fact]
    public void JsonColumn_PhysicalJsonb_ShouldRoundTrip()
    {
        var ctx = _sut.DataProvider;
        const string table = "pg_json_col_probe_jsonb";

        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id bigint primary key, data jsonb, data_null jsonb)");

        try
        {
            // Count/Flag stay at their CLR defaults (0/false); Name carries Unicode; Nested is a
            // separate object with a nested Unicode string and a list.
            var payload = new Shape
            {
                Name = "\u00dcn\u00efc\u00f8d\u00e9 \u2014 \u65e5\u672c\u8a9e \ud83c\udf8c",
                Count = 0,
                Flag = false,
                Nested = new Nested { Inner = "nested-\u00fcn\u00efc\u00f8de", Tags = ["\u03b1", "\u03b2"] }
            };
            var second = new Shape { Name = "second", Count = 3, Flag = true, Nested = null };

            ctx.CreateInsertBuilder<IJsonbProbe>()
                .Values(new JsonbProbe { Id = 1, Data = payload, DataNull = null })
                .Insert();
            ctx.CreateInsertBuilder<IJsonbProbe>()
                .Values(new JsonbProbe { Id = 2, Data = second, DataNull = second })
                .Insert();

            // insert -> read
            var first = ctx.From<JsonbProbe>().Where(x => x.Id == 1).ToList().Single();
            first.Data.Should().BeEquivalentTo(payload);
            first.Data.Name.Should().Contain("\u65e5\u672c\u8a9e");
            first.DataNull.Should().BeNull();

            var roundTripped = ctx.From<JsonbProbe>().Where(x => x.Id == 2).ToList().Single();
            roundTripped.Data.Should().BeEquivalentTo(second);
            roundTripped.DataNull.Should().BeEquivalentTo(second);

            // update -> read (a semantically-equal but distinct object)
            var updated = new Shape
            {
                Name = "updated-\u00fc",
                Count = 9,
                Flag = true,
                Nested = new Nested { Inner = "u2", Tags = ["\u65e5"] }
            };
            ctx.CreateUpdateBuilder<IJsonbProbe>()
                .Set(x => x.Data, updated)
                .Where(x => x.Id == 1)
                .Update();

            var reread = ctx.From<JsonbProbe>().Where(x => x.Id == 1).ToList().Single();
            reread.Data.Should().BeEquivalentTo(updated);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    [Fact]
    public void JsonColumn_PhysicalJson_ShouldRoundTrip()
    {
        var ctx = _sut.DataProvider;
        const string table = "pg_json_col_probe_json";

        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id bigint primary key, data json, data_null json)");

        try
        {
            var payload = new Shape
            {
                Name = "\u00dcn\u00efc\u00f8d\u00e9 \u2014 \u65e5\u672c\u8a9e \ud83c\udf8c",
                Count = 0,
                Flag = false,
                Nested = new Nested { Inner = "nested-\u00fcn\u00efc\u00f8de", Tags = ["\u03b1", "\u03b2"] }
            };
            var second = new Shape { Name = "second", Count = 3, Flag = true, Nested = null };

            ctx.CreateInsertBuilder<IJsonProbe>()
                .Values(new JsonProbe { Id = 1, Data = payload, DataNull = null })
                .Insert();
            ctx.CreateInsertBuilder<IJsonProbe>()
                .Values(new JsonProbe { Id = 2, Data = second, DataNull = second })
                .Insert();

            var first = ctx.From<JsonProbe>().Where(x => x.Id == 1).ToList().Single();
            first.Data.Should().BeEquivalentTo(payload);
            first.Data.Name.Should().Contain("\u65e5\u672c\u8a9e");
            first.DataNull.Should().BeNull();

            var roundTripped = ctx.From<JsonProbe>().Where(x => x.Id == 2).ToList().Single();
            roundTripped.Data.Should().BeEquivalentTo(second);
            roundTripped.DataNull.Should().BeEquivalentTo(second);

            var updated = new Shape
            {
                Name = "updated-\u00fc",
                Count = 9,
                Flag = true,
                Nested = new Nested { Inner = "u2", Tags = ["\u65e5"] }
            };
            ctx.CreateUpdateBuilder<IJsonProbe>()
                .Set(x => x.Data, updated)
                .Where(x => x.Id == 1)
                .Update();

            var reread = ctx.From<JsonProbe>().Where(x => x.Id == 1).ToList().Single();
            reread.Data.Should().BeEquivalentTo(updated);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
