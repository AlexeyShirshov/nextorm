using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// #350 (found by the #231 named-alias conversion): a CTE declared over a **named** projection
/// (<c>new Row { … }</c>) must alias every column to the projection property name regardless of the
/// projection's source kind. When the source is another CTE the declaration used to keep the source
/// identifiers (<c>"id"</c>) while the consumer referenced the DTO contract (<c>"Id"</c>), and
/// PostgreSQL quoted identifiers are case-sensitive, so execution failed with <c>42703</c>.
/// The table-source expectation is pinned byte-for-byte: the fix must not disturb it.
/// </summary>
[Trait("Issue", "350")]
public class T363NamedDtoCteAliasTests
{
    public sealed class DtoRow
    {
        public long Id { get; init; }
        public int? Int { get; init; }
    }

    public sealed class RenamedRow
    {
        public long Code { get; init; }
        public int? Amount { get; init; }
    }

    public sealed class SameNameRow
    {
        public long id { get; init; }
        public int? num { get; init; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Named_projection_over_a_table_source_should_stay_byte_identical()
    {
        using var ctx = PostgresTestContext.Create();

        var inner = ctx.From<IComplexEntity>().Where(x => x.Id > 1)
            .Select(x => new DtoRow { Id = x.Id, Int = x.Int })
            .AsCte("inner");

        var sql = SqlOf(ctx, ctx.From(inner).Select(x => new { x.Id }));

        sql.Should().Be(
            "with inner as (select id as \"Id\", nullableint as \"Int\"\n" +
            " from complex_entity\n" +
            " where (id > 1)) select \"Id\"\n" +
            " from inner as \"t1\"");
    }

    [Fact]
    public void Named_projection_over_a_cte_source_should_alias_to_the_property_names()
    {
        using var ctx = PostgresTestContext.Create();

        var source = ctx.From<IComplexEntity>().Select(x => new { id = x.Id, num = x.Int }).AsCte("source");
        var named = ctx.From(source).Select(x => new DtoRow { Id = x.id, Int = x.num }).AsCte("named");

        var sql = SqlOf(ctx, ctx.From(named).Select(x => new { x.Id }));

        // The declaration exposes the DTO contract; the consumer references exactly those identifiers.
        sql.Should().Contain("named as (select \"id\" as \"Id\", \"num\" as \"Int\"");
        sql.Should().Contain("select \"Id\"\n from named as");
    }

    [Fact]
    public void Named_projection_over_a_cte_source_should_alias_a_non_case_rename()
    {
        using var ctx = PostgresTestContext.Create();

        var source = ctx.From<IComplexEntity>().Select(x => new { id = x.Id, num = x.Int }).AsCte("source");
        var named = ctx.From(source).Select(x => new RenamedRow { Code = x.id, Amount = x.num }).AsCte("named");

        var sql = SqlOf(ctx, ctx.From(named).Select(x => new { x.Code }));

        sql.Should().Contain("named as (select \"id\" as \"Code\", \"num\" as \"Amount\"");
        sql.Should().Contain("select \"Code\"\n from named as");
    }

    [Fact]
    public void Two_hop_chain_should_alias_at_every_hop()
    {
        using var ctx = PostgresTestContext.Create();

        var source = ctx.From<IComplexEntity>().Select(x => new { id = x.Id, num = x.Int }).AsCte("source");
        var middle = ctx.From(source).Select(x => new DtoRow { Id = x.id, Int = x.num }).AsCte("middle");
        var last = ctx.From(middle).Select(x => new { code = x.Id, value = x.Int }).AsCte("last");

        var sql = SqlOf(ctx, ctx.From(last).Select(x => new { x.code }));

        sql.Should().Contain("middle as (select \"id\" as \"Id\", \"num\" as \"Int\"");
        sql.Should().Contain("last as (select \"Id\" as \"code\", \"Int\" as \"value\"");
        sql.Should().Contain("select \"code\"\n from last as");
    }

    [Fact]
    public void Source_column_name_equal_to_the_member_name_should_not_gain_an_alias()
    {
        using var ctx = PostgresTestContext.Create();

        var source = ctx.From<IComplexEntity>().Select(x => new { id = x.Id, num = x.Int }).AsCte("source");
        var same = ctx.From(source).Select(x => new SameNameRow { id = x.id, num = x.num }).AsCte("same");

        var sql = SqlOf(ctx, ctx.From(same).Select(x => new { x.id }));

        // The identifier already matches the DTO contract: no redundant alias is emitted.
        sql.Should().Contain("same as (select \"id\", \"num\"");
        sql.Should().NotContain("\"id\" as");
        sql.Should().NotContain("\"num\" as");
    }

    [Fact]
    public void Legacy_With_declaration_should_stay_unchanged()
    {
        using var ctx = PostgresTestContext.Create();

        var body = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id, x.Int });

        var sql = SqlOf(ctx, ctx.With("legacy", body).From("legacy").Select(x => x["Id"].AsInt));

        // A non-typed declaration keeps the physical/mapped output names (no typed projection aliasing).
        sql.Should().Be(
            "with legacy as (select id, nullableint as \"Int\"\n" +
            " from complex_entity\n" +
            " where (id > 1)) select Id\n" +
            " from legacy");
    }
}
