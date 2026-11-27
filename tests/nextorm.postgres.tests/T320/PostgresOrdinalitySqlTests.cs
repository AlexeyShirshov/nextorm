using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Postgres.Tests;

public sealed class PostgresOrdinalitySqlTests
{
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
        [Column("id")] int Id { get; set; }
        [Column("ordinality")] long Position { get; set; }
    }

    public interface IRecord
    {
        [Column("id")] int Id { get; set; }
    }

    private static TvfEntityBuilder<ISimpleEntity> Echo(int id) => new("echo_rows", "public");

    private static string Sql<T>(IDataContext context, QueryCommand<T> query, bool cache = false)
        => ((DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(query, false, cache, CancellationToken.None))
            .DbCommand.CommandText;

    [Fact]
    public void OuterApply_AppendsOrdinalityToCorrelatedJsonbCall()
    {
        using var context = PostgresTestContext.Create();
        var query = context.From<IOrder>()
            .OuterApply(o => SqlFunctions.Postgres.jsonb_array_elements(o.Items).WithOrdinality<IItem>())
            .OrderBy(p => p.Item1.Id)
            .ThenBy(p => p.Item2.Position)
            .Select(p => new { p.Item1.Id, p.Item2.Value, p.Item2.Position });

        var sql = Sql(context, query);
        sql.Should().Contain("left join lateral")
            .And.Contain("jsonb_array_elements(t1.items) with ordinality")
            .And.Contain("on true")
            .And.Contain("t2.ordinality as \"Position\"")
            .And.Contain("order by t1.id, t3.\"Position\"");
    }

    [Fact]
    public void From_PreservesScalarOutputNameAndDoesNotAddSorting()
    {
        using var context = PostgresTestContext.Create();
        var query = context.From(() => SqlFunctions.Postgres.string_to_table("a,a", ",").WithOrdinality<IToken>())
            .Select(r => new { r.Value, r.Position });

        Sql(context, query).Should().Contain("string_to_table(E'a,a', E',') with ordinality")
            .And.Contain("ordinality").And.NotContain("order by");
    }

    [Fact]
    public void UserSource_ModifierHasDistinctPlanIdentityFromUnmodifiedCall()
    {
        using var context = PostgresTestContext.Create();
        var ordinary = context.From(() => Echo(42)).Select(r => r.Id);
        var numbered = context.From(() => Echo(42).WithOrdinality<IEcho>()).Select(r => r.Id);

        Sql(context, ordinary, cache: true).Should().Contain("public.echo_rows(42)").And.NotContain("with ordinality");
        Sql(context, numbered, cache: true).Should().Contain("public.echo_rows(42) with ordinality");
        var array = new long[] { 10, 20 };
        var legacy = context.From(() => SqlFunctions.Postgres.unnest_with_ordinality(array)).Select(r => r.Ordinality);
        Sql(context, legacy).Should().Contain("with ordinality as u(value, ordinality)");
    }

    [Fact]
    public void RecordSource_LeavesOrdinalityOutOfItsDeclaredColumnDefinitions()
    {
        using var context = PostgresTestContext.Create();
        using var document = JsonDocument.Parse("""{"id":42}""");
        var query = context.From(() => SqlFunctions.Postgres.jsonb_to_record<IRecord>(document).WithOrdinality<IEcho>())
            .Select(r => new { r.Id, r.Position });

        Sql(context, query).Should().Contain("rows from (jsonb_to_record(@document) as (id integer)) with ordinality")
            .And.Contain("ordinality as \"Position\"");
    }

    [Fact]
    public void Sqlite_RejectsOrdinalityOnAnOtherwiseAdmittedCustomSource()
    {
        using var context = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var query = context.From(() => Echo(42).WithOrdinality<IEcho>()).Select(r => r.Position);

        FluentActions.Invoking(() => Sql(context, query)).Should().Throw<NotSupportedException>()
            .WithMessage("*WITH ORDINALITY*not supported*");
    }
}
