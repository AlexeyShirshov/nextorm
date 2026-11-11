using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Regression coverage for a derived entity: metadata is built for the concrete type, but the
/// properties it inherits are declared on the base type. Reflection keeps the base declaration as the
/// member's <see cref="System.Reflection.MemberInfo.DeclaringType"/>, so a column lookup keyed by that
/// type silently produced an empty column name. The select list and predicates must resolve the
/// inherited properties through the entity type being queried.
/// </summary>
public class DerivedEntitySqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    [Fact]
    public void Whole_entity_select_ShouldRenderInheritedColumns()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From<ExpressOrder>().ToCommand())
            .Should().Be("select Total, PromisedAt, Id, number, CreatedAt\n from order_express");
    }

    [Fact]
    public void Predicate_and_projection_ShouldResolveInheritedColumns()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ExpressOrder>()
            .Where(o => o.Number == "E")
            .Select(o => new { o.Id, o.Number }));

        sql.Should().Be("select Id, number\n from order_express\n where number = 'E'");
    }

    [Fact]
    public void Fluent_override_on_inherited_property_ShouldApplyToTheDerivedEntity()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<FluentExpressOrder>(cfg => cfg.Property(o => o.Number).HasColumnName("num"))
            .Select(o => o.Number));

        sql.Should().Be("select num as 'Number'\n from fluent_order_express");
    }

    [Fact]
    public void Subtype_entities_over_one_table_ShouldRenderOnlyTheirMappedColumns()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From<TphExpressOrder>().ToCommand())
            .Should().Be("select PromisedAt, Id, Kind, Number\n from tph_orders");

        SqlOf(ctx, ctx.From<TphStandardOrder>().ToCommand())
            .Should().Be("select Total, Id, Kind, Number\n from tph_orders");
    }

    public abstract class OrderBase
    {
        [Key] public long Id { get; set; }

        [Column("number")] public string Number { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }

    [SqlTable("order_express")]
    public sealed class ExpressOrder : OrderBase
    {
        public decimal Total { get; set; }
        public DateTime PromisedAt { get; set; }
    }

    public abstract class FluentOrderBase
    {
        [Key] public long Id { get; set; }
        public string Number { get; set; } = "";
    }

    [SqlTable("fluent_order_express")]
    public sealed class FluentExpressOrder : FluentOrderBase
    {
        public decimal Total { get; set; }
    }

    [SqlTable("tph_orders")]
    public abstract class TphOrderBase
    {
        [Key] public long Id { get; set; }
        public string Kind { get; set; } = "";
        public string Number { get; set; } = "";
    }

    public sealed class TphExpressOrder : TphOrderBase
    {
        public DateTime PromisedAt { get; set; }
    }

    public sealed class TphStandardOrder : TphOrderBase
    {
        public decimal Total { get; set; }
    }
}
