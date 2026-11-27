using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

public sealed class PostgresOrdinalityTests
{
    [SqlTable("t421_orders")]
    public interface IOrder
    {
        [Column("id")] int Id { get; set; }
        [Column("items")] JsonDocument? Items { get; set; }
    }

    public interface IItem
    {
        [Column("value")] JsonDocument? Value { get; set; }
        [Column("ordinality")] long? Position { get; set; }
    }

    public interface IToken
    {
        [Column("string_to_table")] string? Value { get; set; }
        [Column("ordinality")] long Position { get; set; }
    }

    public interface IEcho
    {
        [Column("value")] int Value { get; set; }
    }

    public interface INumberedEcho
    {
        [Column("value")] int Value { get; set; }
        [Column("ordinality")] long Position { get; set; }
    }

    private static TvfEntityBuilder<IEcho> Echo(int value) => new("t421_echo", "pg_temp");

    private static PostgresDataContext OpenContext()
    {
        var context = new PostgresDataContext(PostgresContainer.ConnectionString, new DataContextBuilder());
        try
        {
            context.EnsureConnectionOpen();
            return context;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    [Fact]
    public void OuterApply_PreservesEmptyAndSqlNullArraysAndRestartsNumbering()
    {
        using var context = OpenContext();
        using (var setup = context.CreateCommand("""
            create temp table t421_orders (id int primary key, items jsonb);
            insert into t421_orders values (1, '[7,7]'), (2, '[]'), (3, null), (4, '[8]');
            """))
            setup.ExecuteNonQuery();

        var rows = context.From<IOrder>()
            .OuterApply(o => SqlFunctions.Postgres.jsonb_array_elements(o.Items).WithOrdinality<IItem>())
            .OrderBy(p => p.Item1.Id)
            .ThenBy(p => p.Item2.Position)
            .Select(p => new { p.Item1.Id, p.Item2.Value, p.Item2.Position })
            .ToList();

        rows.Select(r => (r.Id, r.Value?.RootElement.GetInt32(), r.Position)).Should().Equal(
            (1, 7, 1L), (1, 7, 2L), (2, null, null), (3, null, null), (4, 8, 1L));
    }

    [Fact]
    public void From_NumbersRepeatedScalarOutputsAndKeepsLegacyUnnest()
    {
        using var context = OpenContext();
        var tokens = context.From(() => SqlFunctions.Postgres.string_to_table("x,x,y", ",").WithOrdinality<IToken>())
            .OrderByDescending(r => r.Position).Select(r => new { r.Value, r.Position }).ToList();

        tokens.Select(r => (r.Value, r.Position)).Should().Equal(("y", 3L), ("x", 2L), ("x", 1L));
        context.From(() => SqlFunctions.Postgres.string_to_table("x,y", ","))
            .Select(r => r.Value).ToList().Should().BeEquivalentTo(new[] { "x", "y" });

        long[] values = [10L, 20L];
        var legacy = context.From(() => SqlFunctions.Postgres.unnest_with_ordinality(values))
            .OrderBy(r => r.Ordinality).Select(r => new { r.Value, r.Ordinality }).ToList();
        legacy.Select(r => (r.Value, r.Ordinality)).Should().Equal((10L, 1L), (20L, 2L));
    }

    [Fact]
    public void From_NumbersUserFunctionWithoutReplacingItsOutput()
    {
        using var context = OpenContext();
        using (var setup = context.CreateCommand("""
            create or replace function pg_temp.t421_echo(n integer)
            returns table ("value" integer)
            language sql as $$ select n union all select n $$;
            """))
            setup.ExecuteNonQuery();

        var rows = context.From(() => Echo(42).WithOrdinality<INumberedEcho>())
            .OrderBy(r => r.Position).Select(r => new { r.Value, r.Position }).ToList();

        rows.Select(r => (r.Value, r.Position)).Should().Equal((42, 1L), (42, 2L));
    }
}
