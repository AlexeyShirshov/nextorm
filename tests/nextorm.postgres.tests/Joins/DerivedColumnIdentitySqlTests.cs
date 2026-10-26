using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T324 r2 — focused column-identity regressions for a bound APPLY/derived slot (F1/F3, carrying claim
/// C-COL). The applied source is a derived table whose select list aliases every renamed column to the
/// projected property name, so a consumer must address that exposed identity. The physical column
/// (<c>item_id</c>, <c>c_id</c>) is not a column of the derived table and must never appear for an
/// applied-slot reference; a plain-table slot keeps its physical column byte-identically.
/// <para>
/// The PostgreSQL dialect quotes the derived aliases, which is what exposes a physical-name leak in the
/// rendered text; no connection is opened.
/// </para>
/// </summary>
[SqlTable("t324i_item")]
public sealed class DerivedIdentityItem
{
    [Key]
    [Column("item_id")]
    public int ItemId { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }

    [Column("ordinal")]
    public int Ordinal { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("t324i_child")]
public sealed class DerivedIdentityChild
{
    [Key]
    [Column("c_id")]
    public int CId { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("score")]
    public int Score { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("t324i_parent")]
public sealed class DerivedIdentityParent
{
    [Key]
    [Column("p_id")]
    public int PId { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }
}

[SqlTable("t324i_owner")]
public sealed class DerivedIdentityOwner
{
    [Key]
    [Column("o_id")]
    public int OId { get; set; }
}

[SqlTable("t324i_tag")]
public sealed class DerivedIdentityTag
{
    [Key]
    [Column("tag_id")]
    public int TagId { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

/// <summary>The flat row projected by every regression case.</summary>
public sealed class DerivedIdentityRow
{
    public int A { get; set; }

    public int B { get; set; }

    public int C { get; set; }

    public string? T { get; set; }
}

public class DerivedColumnIdentitySqlTests
{
    [Fact]
    public void Whole_entity_applied_projection_uses_the_exposed_derived_column()
    {
        using var ctx = PostgresTestContext.Create();
        var joined = ctx.From<DerivedIdentityItem>().Join(
            ctx.From<DerivedIdentityItem>(), (l, r) => l.GroupId == r.GroupId && l.Ordinal == 1 && r.Ordinal == 2);

        var command = joined
            .CrossApply((Projection<DerivedIdentityItem, DerivedIdentityItem> p) =>
                ctx.From<DerivedIdentityItem>().Where(c => c.GroupId == p.Item1.GroupId && c.ItemId == p.Item2.ItemId))
            .Select(q => new DerivedIdentityRow { A = q.Item1.ItemId, B = q.Item2.ItemId, C = q.Item3.ItemId });

        var sql = SqlOf(ctx, command);

        sql.Should().Contain("t3.item_id as \"ItemId\"", "the applied derived source exposes the projected identity");
        sql.Should().Contain("t4.\"ItemId\"", "the whole-entity applied projection must read the derived column");
        sql.Should().NotContain("t4.item_id", "the physical column is not exposed by the derived source");
    }

    [Fact]
    public void Paged_apply_after_apply_correlates_through_the_first_slots_exposed_identity()
    {
        using var ctx = PostgresTestContext.Create();
        var sql = SqlOf(ctx, PagedChain(ctx));

        sql.Should().Contain("t4.\"CId\"", "the outer projection reads the derived column");
        sql.Should().Contain("where t5.tag_id = t4.\"CId\"", "the second APPLY correlates through the exposed identity");
        sql.Should().NotContain("t4.c_id", "the second APPLY must not fall back to the physical column");
    }

    [Fact]
    public void Apply_after_apply_correlates_through_the_first_slots_exposed_identity()
    {
        using var ctx = PostgresTestContext.Create();
        var sql = SqlOf(ctx, Chain(ctx, paged: false));

        sql.Should().Contain("where t5.tag_id = t4.\"CId\"", "the second APPLY correlates through the exposed identity");
        sql.Should().NotContain("t4.c_id", "the second APPLY must not fall back to the physical column");
    }

    [Fact]
    public void Scalar_applied_slot_uses_the_derived_column_alias()
    {
        using var ctx = PostgresTestContext.Create();
        var sql = SqlOf(ctx, ScalarChain(ctx));

        sql.Should().Contain("t4.\"CId\" as \"B\"", "the bare scalar item must name the derived column");
        sql.Should().Contain("where t5.tag_id = t4.\"CId\"", "the scalar correlation must name the derived column");
        sql.Should().NotContain("Item3", "the projection member name is not a column of the derived source");
        sql.Should().NotContain("t4.1", "the slot ordinal is not a column of the derived source");
        sql.Should().NotContain("t4.c_id", "the physical column is not exposed by the derived source");
    }

    [Fact]
    public void Plain_table_first_apply_keeps_the_physical_column()
    {
        using var ctx = PostgresTestContext.Create();
        var sql = SqlOf(ctx, PlainTableChain(ctx));

        sql.Should().Contain("cross join t324i_child as \"t3\"", "a plain table is not a derived projection");
        sql.Should().Contain("where t4.tag_id = t3.c_id", "a plain-table slot keeps its physical column");
        sql.Should().NotContain("t3.\"CId\"", "no derived identity is invented for a plain table");
    }

    private static JoinedEntityBuilder<DerivedIdentityParent, DerivedIdentityOwner> Joined(IDataContext ctx)
        => ctx.From<DerivedIdentityParent>().Join(ctx.From<DerivedIdentityOwner>(), (p, o) => p.OwnerId == o.OId);

    private static QueryCommand<DerivedIdentityRow> PagedChain(IDataContext ctx)
        => Chain(ctx, paged: true);

    private static QueryCommand<DerivedIdentityRow> Chain(IDataContext ctx, bool paged)
    {
        var first = Joined(ctx).CrossApply((Projection<DerivedIdentityParent, DerivedIdentityOwner> p) =>
            paged
                ? ctx.From<DerivedIdentityChild>().Where(c => c.ParentId == p.Item1.PId)
                    .OrderByDescending(c => c.Score).ThenBy(c => c.CId).Limit(1)
                : ctx.From<DerivedIdentityChild>().Where(c => c.ParentId == p.Item1.PId));

        return first
            .CrossApply((Projection<DerivedIdentityParent, DerivedIdentityOwner, DerivedIdentityChild> p) =>
                ctx.From<DerivedIdentityTag>().Where(t => t.TagId == p.Item3.CId))
            .Select(q => new DerivedIdentityRow { A = q.Item1.PId, B = q.Item3.CId, T = q.Item4.Label });
    }

    private static QueryCommand<DerivedIdentityRow> ScalarChain(IDataContext ctx)
    {
        var first = Joined(ctx).CrossApply((Projection<DerivedIdentityParent, DerivedIdentityOwner> p) =>
            ctx.From<DerivedIdentityChild>().Where(c => c.ParentId == p.Item1.PId).Select(c => c.CId));

        return first
            .CrossApply((Projection<DerivedIdentityParent, DerivedIdentityOwner, int> p) =>
                ctx.From<DerivedIdentityTag>().Where(t => t.TagId == p.Item3))
            .Select(q => new DerivedIdentityRow { A = q.Item1.PId, B = q.Item3, T = q.Item4.Label });
    }

    private static QueryCommand<DerivedIdentityRow> PlainTableChain(IDataContext ctx)
    {
        var first = Joined(ctx).CrossApply(ctx.From<DerivedIdentityChild>());
        return first
            .CrossApply((Projection<DerivedIdentityParent, DerivedIdentityOwner, DerivedIdentityChild> p) =>
                ctx.From<DerivedIdentityTag>().Where(t => t.TagId == p.Item3.CId))
            .Select(q => new DerivedIdentityRow { A = q.Item1.PId, B = q.Item3.CId, T = q.Item4.Label });
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
}
