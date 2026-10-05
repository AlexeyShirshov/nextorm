using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B V32 guarding test. Navigation over a temp-table/TVP source is <b>formally deferred</b>
/// (fail-closed, tracked by issue #162): the trigger is a navigation source combined with a
/// temp-table/TVP path. A lazy temp-table read source is an untyped <see cref="TableAlias"/> whose
/// surface is only typed column accessors, so navigation over it is not expressible and is never
/// lowered; navigation <i>before</i> the temp-table boundary (the materialisation
/// <c>CREATE TEMPORARY TABLE ... AS SELECT</c>) is still expanded. The test pins both halves so a
/// future change that typed a temp-table/TVP source as a mapped entity would have to add a
/// deliberate navigation join here instead of silently joining the base table for the read.
/// </summary>
public class ImplicitNavigationV32TempTableTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static IDataContext CreateContext()
    {
        var ctx = SqliteTestContext.Create();
        ctx.From<NavParent>();
        ctx.From<NavChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        return ctx;
    }

    [Fact]
    public void Temp_table_read_should_stay_an_untyped_alias_and_not_silently_join_the_base_table()
    {
        using var ctx = CreateContext();

        // The trigger: a navigation is combined with a temp-table path — the source query declares a
        // reference navigation and the read goes through the lazy temp-table source.
        var tempTable = ctx.From<NavChild>()
            .Select(c => new { c.Id, Name = c.Parent!.Name })
            .AsTempTable();

        var read = ctx.From(tempTable);
        read.GetType().GetGenericArguments().Should().ContainSingle()
            .Which.Should().Be(
                typeof(TableAlias),
                "a temp-table/TVP read source is an untyped table alias: it exposes no navigation surface, so navigation over it is deferred");

        var sql = Normalize(read.Select(t => new { Id = t.GetInt32("id") }).ToBatchSql());

        // Navigation before the temp-table boundary is still expanded: the materialisation carries the
        // reference LEFT JOIN (the navigation is not dropped at the boundary).
        sql.Should().Contain("create temporary table " + tempTable.Name + " as select");
        sql.Should().Contain("left join nav_parent");

        // The read over the temp table adds no navigation join against the base table. The batch holds
        // exactly one navigation join (the materialisation) — if a future change lowered navigation over
        // the temp-table/TVP source, this would become two and the deferral would no longer hold.
        sql.Split("select id from " + tempTable.Name).Should().HaveCount(2, "the read selects from the temp table exactly once");
        sql.Split("left join").Should().HaveCount(
            2,
            "exactly one navigation join (in the temp-table materialisation); the deferred read must not add a base-table join");
    }
}
