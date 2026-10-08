using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// #162 boundary regression for MariaDB. Navigation <i>before</i> the temp-table materialisation is still
/// expanded, while a read over the lazy temp-table source stays an untyped <see cref="TableAlias"/> and
/// does <b>not</b> add a navigation join against the base table.
/// </summary>
[Trait("D162", "Boundary")]
public class ImplicitNavigationV32TempTableTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static IDataContext CreateContext()
    {
        var ctx = MariaDbTestContext.Create();
        ctx.From<BoundaryParent>();
        ctx.From<BoundaryChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        return ctx;
    }

    [Fact]
    public void Temp_table_read_should_stay_an_untyped_alias_and_not_silently_join_the_base_table()
    {
        using var ctx = CreateContext();

        var tempTable = ctx.From<BoundaryChild>()
            .Select(c => new { c.Id, Name = c.Parent!.Name })
            .AsTempTable();

        var read = ctx.From(tempTable);
        read.GetType().GetGenericArguments().Should().ContainSingle()
            .Which.Should().Be(
                typeof(TableAlias),
                "a temp-table read source is an untyped table alias: it exposes no navigation surface, so navigation over it is deferred");

        var sql = Normalize(read.Select(t => new { Id = t.GetInt32("id") }).ToBatchSql());

        // Navigation before the temp-table boundary is still expanded: the materialisation carries the
        // reference LEFT JOIN (the navigation is not dropped at the boundary).
        sql.Should().Contain("create temporary table " + tempTable.Name + " as select");
        sql.Should().Contain("left join boundary_parent");

        // The read over the temp table adds no navigation join against the base table. The batch holds
        // exactly one navigation join (the materialisation).
        sql.Split("select id from " + tempTable.Name).Should().HaveCount(2, "the read selects from the temp table exactly once");
        sql.Split("left join").Should().HaveCount(
            2,
            "exactly one navigation join (in the temp-table materialisation); the deferred read must not add a base-table join");
    }
}

[SqlTable("boundary_parent")]
public sealed class BoundaryParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("boundary_child")]
public sealed class BoundaryChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    public BoundaryParent? Parent { get; set; }
}
