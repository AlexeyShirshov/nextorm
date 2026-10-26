using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T324 D7 (E07/AC07) — live row-level semantics of a correlated CROSS/OUTER APPLY placed after a join.
/// Every cell runs against a real database through <see cref="ProviderTestSuite"/> and asserts the rows the
/// lateral semantics imply, never SQL text: the fixture owns its tables (<c>t324l_*</c>, portable DDL), so a
/// provider's seed schema cannot mask a wrong correlation.
/// <para>
/// The decisive case is the orphan parent: it has no child row, so the cross query must drop it and the
/// outer query must keep it with NULL child columns. The remaining cells cover both-left-side correlation,
/// an ordered top-1 per parent, repeated CLR types, an APPLY after an APPLY, and NULL (not default) values on
/// the preserved outer row.
/// </para>
/// <para>
/// Providers without a lateral/APPLY source (SQLite) execute the named capability refusal for the same
/// shapes instead: a skip would hide the provider and would drift the pinned skip budget, while a positive
/// lateral run is impossible there. ClickHouse is not part of this suite. MariaDB has no
/// <see cref="CommonTestSuite"/> provider registration, so <c>MariaDbCorrelatedApplyAfterJoinIntegrationTests</c>
/// reuses these same bodies against <c>MariaDbContainer.ConnectionString</c>; a MySQL run is never counted as
/// MariaDB evidence.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    private const string T324LiveDdl = """
        drop table if exists t324l_tag;
        drop table if exists t324l_item;
        drop table if exists t324l_child;
        drop table if exists t324l_owner;
        drop table if exists t324l_parent;
        """;

    private const string T324LiveCreate = """
        create table t324l_parent (p_id int not null primary key, owner_id int not null, label varchar(20) null);
        create table t324l_owner (o_id int not null primary key, min_score int not null, name varchar(20) null);
        create table t324l_child (c_id int not null primary key, parent_id int not null, score int not null, tag varchar(20) null);
        create table t324l_item (item_id int not null primary key, group_id int not null, ordinal int not null, label varchar(20) null);
        create table t324l_tag (tag_id int not null primary key, label varchar(20) null);
        """;

    private const string T324LiveSeed = """
        insert into t324l_owner (o_id, min_score, name) values (10, 15, 'hi'), (20, 0, 'lo');
        insert into t324l_parent (p_id, owner_id, label) values (1, 10, 'p1'), (2, 10, 'p2'), (3, 20, 'p3'), (4, 20, 'p4-orphan');
        insert into t324l_child (c_id, parent_id, score, tag) values (101, 1, 10, 'a'), (102, 1, 30, 'b'), (103, 2, 20, 'c'), (104, 3, 5, 'd');
        insert into t324l_item (item_id, group_id, ordinal, label) values (1, 100, 1, 'x'), (2, 100, 2, 'y'), (3, 200, 1, 'z'), (4, 200, 2, 'w');
        insert into t324l_tag (tag_id, label) values (101, 't-a'), (102, 't-b'), (103, 't-c'), (104, 't-d');
        """;

    /// <summary>Re-creates the class-owned live fixture and proves the connection is open.</summary>
    internal static void T324LiveCreateFixture(IDataContext ctx)
    {
        T324LiveExecute(ctx, T324LiveDdl);
        T324LiveExecute(ctx, T324LiveCreate);
        T324LiveExecute(ctx, T324LiveSeed);
    }

    private static void T324LiveExecute(IDataContext ctx, string sql)
    {
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// SQLite (and any other provider without lateral/APPLY): the same correlated after-join command must be
    /// refused by the dialect capability gate while it is prepared, before it reaches the database, and the
    /// reason must be the missing lateral source — never the removed "join projection" refusal.
    /// </summary>
    internal static void T324LiveAssertLateralRefusal(IDataContext ctx, bool outer)
    {
        var joined = ctx.From<T324lParent>().Join(ctx.From<T324lOwner>(), (p, o) => p.OwnerId == o.OId);

        var command = outer
            ? joined.OuterApply((Projection<T324lParent, T324lOwner> p) =>
                    ctx.From<T324lChild>().Where(c => c.ParentId == p.Item1.PId))
                .Select(q => new { Parent = q.Item1.PId, Child = q.Item3.CId })
            : joined.CrossApply((Projection<T324lParent, T324lOwner> p) =>
                    ctx.From<T324lChild>().Where(c => c.ParentId == p.Item1.PId))
                .Select(q => new { Parent = q.Item1.PId, Child = q.Item3.CId });

        var exception = FluentActions
            .Invoking(() => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>(
                "a provider without a lateral/APPLY source must refuse the command while preparing it")
            .Which;

        exception.Message.Should().Contain(outer ? "OuterApply" : "CrossApply");
        exception.Message.Should().Contain("not supported by this SQL dialect");
        exception.Message.Should().NotContain(
            "join projection", "the left source being a projection is not the failure reason");
    }

    /// <summary>
    /// The decisive AC07 case: <paramref name="ctx"/> holds one parent (p_id 4) with no child row. The cross
    /// query returns the four matched rows and drops it; the outer query returns five, keeping it with NULL
    /// child columns. Row counts and rows are asserted, not SQL text.
    /// </summary>
    internal static void T324LiveOrphanParent(IDataContext ctx, bool supportsApply)
    {
        if (!supportsApply)
        {
            T324LiveAssertLateralRefusal(ctx, outer: false);
            T324LiveAssertLateralRefusal(ctx, outer: true);
            return;
        }

        T324LiveCreateFixture(ctx);
        var joined = ctx.From<T324lParent>().Join(ctx.From<T324lOwner>(), (p, o) => p.OwnerId == o.OId);

        var cross = joined
            .CrossApply((Projection<T324lParent, T324lOwner> p) =>
                ctx.From<T324lChild>().Where(c => c.ParentId == p.Item1.PId))
            .Select(q => new { Parent = q.Item1.PId, Child = q.Item3.CId })
            .ToList();

        cross.Should().HaveCount(4, "the orphan parent 4 has no child row, so CROSS APPLY drops it");
        cross.Select(r => (r.Parent, r.Child)).Should().BeEquivalentTo(
            new[] { (1, 101), (1, 102), (2, 103), (3, 104) });

        var outerRows = joined
            .OuterApply((Projection<T324lParent, T324lOwner> p) =>
                ctx.From<T324lChild>().Where(c => c.ParentId == p.Item1.PId))
            .Select(q => new { Parent = q.Item1.PId, Child = (int?)q.Item3.CId })
            .ToList();

        outerRows.Should().HaveCount(5, "OUTER APPLY keeps the orphan parent, so it is the cross count plus one");
        outerRows.Where(r => r.Child is not null).Select(r => (r.Parent, r.Child!.Value))
            .Should().BeEquivalentTo(new[] { (1, 101), (1, 102), (2, 103), (3, 104) });
        outerRows.Single(r => r.Parent == 4).Child.Should().BeNull(
            "the preserved parent 4 projects NULL from the applied source");
    }

    /// <summary>
    /// The applied body references both left slots: slot 1 (<c>Item1.PId</c>) and the joined occurrence
    /// (<c>Item2.MinScore</c>). A wrong slot assignment yields a different row set, so the exact triples are
    /// asserted.
    /// </summary>
    internal static void T324LiveBothLeftSides(IDataContext ctx, bool supportsApply)
    {
        if (!supportsApply)
        {
            T324LiveAssertLateralRefusal(ctx, outer: false);
            return;
        }

        T324LiveCreateFixture(ctx);
        var joined = ctx.From<T324lParent>().Join(ctx.From<T324lOwner>(), (p, o) => p.OwnerId == o.OId);

        var rows = joined
            .CrossApply((Projection<T324lParent, T324lOwner> p) =>
                ctx.From<T324lChild>().Where(c => c.ParentId == p.Item1.PId && c.Score >= p.Item2.MinScore))
            .Select(q => new { Parent = q.Item1.PId, Child = q.Item3.CId, Score = q.Item3.Score })
            .ToList();

        rows.Should().HaveCount(3, "owner 10 keeps only score >= 15 and owner 20 keeps score >= 0");
        rows.Select(r => (r.Parent, r.Child, r.Score)).Should().BeEquivalentTo(
            new[] { (1, 102, 30), (2, 103, 20), (3, 104, 5) });
    }

    /// <summary>
    /// An ordered correlated subquery limited to one row: each parent keeps exactly its highest-scored child.
    /// Parent 1 owns both 101 (score 10) and 102 (score 30), so an unordered or uncorrelated read is visible.
    /// </summary>
    internal static void T324LiveOrderedTop1(IDataContext ctx, bool supportsApply)
    {
        if (!supportsApply)
        {
            T324LiveAssertLateralRefusal(ctx, outer: false);
            return;
        }

        T324LiveCreateFixture(ctx);
        var joined = ctx.From<T324lParent>().Join(ctx.From<T324lOwner>(), (p, o) => p.OwnerId == o.OId);

        var rows = joined
            .CrossApply((Projection<T324lParent, T324lOwner> p) =>
                ctx.From<T324lChild>()
                    .Where(c => c.ParentId == p.Item1.PId)
                    .OrderByDescending(c => c.Score)
                    .ThenBy(c => c.CId)
                    .Limit(1))
            .Select(q => new { Parent = q.Item1.PId, Child = q.Item3.CId, Score = q.Item3.Score })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => (r.Parent, r.Child, r.Score)).Should().BeEquivalentTo(
            new[] { (1, 102, 30), (2, 103, 20), (3, 104, 5) },
            "the ordered top-1 subquery must pick the highest-scored child of each parent");
    }

    /// <summary>
    /// Two sources of the same CLR type in the preceding join. The applied item must belong to the second
    /// occurrence (<c>Item2</c>): resolving the first occurrence instead returns items 1 and 3, not 2 and 4.
    /// </summary>
    internal static void T324LiveRepeatedClrTypes(IDataContext ctx, bool supportsApply)
    {
        if (!supportsApply)
        {
            T324LiveAssertLateralRefusal(ctx, outer: false);
            return;
        }

        T324LiveCreateFixture(ctx);
        var joined = ctx.From<T324lItem>().Join(
            ctx.From<T324lItem>(),
            (l, r) => l.GroupId == r.GroupId && l.Ordinal == 1 && r.Ordinal == 2);

        var rows = joined
            .CrossApply((Projection<T324lItem, T324lItem> p) =>
                ctx.From<T324lItem>()
                    .Where(c => c.GroupId == p.Item1.GroupId && c.ItemId == p.Item2.ItemId)
                    .Select(c => new { c.ItemId, c.Label }))
            .Select(q => new { LeftItem = q.Item1.ItemId, RightItem = q.Item2.ItemId, AppliedItem = q.Item3.ItemId })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => (r.LeftItem, r.RightItem, r.AppliedItem)).Should().BeEquivalentTo(
            new[] { (1, 2, 2), (3, 4, 4) },
            "the repeated type must resolve to the exact occurrence, not to the first source of that type");
    }

    /// <summary>
    /// A second APPLY after the first: the tag lookup correlates with the first APPLY's slot
    /// (<c>Item3.CId</c>), so a slot pointing anywhere else returns no tag rows.
    /// </summary>
    internal static void T324LiveApplyAfterApply(IDataContext ctx, bool supportsApply)
    {
        if (!supportsApply)
        {
            T324LiveAssertLateralRefusal(ctx, outer: false);
            return;
        }

        T324LiveCreateFixture(ctx);
        var joined = ctx.From<T324lParent>().Join(ctx.From<T324lOwner>(), (p, o) => p.OwnerId == o.OId);

        var firstApply = joined.CrossApply((Projection<T324lParent, T324lOwner> p) =>
            ctx.From<T324lChild>()
                .Where(c => c.ParentId == p.Item1.PId)
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.CId)
                .Limit(1));

        var rows = firstApply
            .CrossApply((Projection<T324lParent, T324lOwner, T324lChild> p) =>
                ctx.From<T324lTag>().Where(t => t.TagId == p.Item3.CId))
            .Select(q => new { Parent = q.Item1.PId, Child = q.Item3.CId, Tag = q.Item4.Label })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => (r.Parent, r.Child, r.Tag)).Should().BeEquivalentTo(
            new[] { (1, 102, (string?)"t-b"), (2, 103, "t-c"), (3, 104, "t-d") },
            "the second APPLY must see the first APPLY's slot");
    }

    /// <summary>
    /// OUTER APPLY on the orphan parent projects SQL NULL, not a default: the materialized values of the
    /// preserved row are <see langword="null"/> for the child id, its score and its tag.
    /// </summary>
    internal static void T324LiveOuterNulls(IDataContext ctx, bool supportsApply)
    {
        if (!supportsApply)
        {
            T324LiveAssertLateralRefusal(ctx, outer: true);
            return;
        }

        T324LiveCreateFixture(ctx);
        var joined = ctx.From<T324lParent>().Join(ctx.From<T324lOwner>(), (p, o) => p.OwnerId == o.OId);

        var rows = joined
            .OuterApply((Projection<T324lParent, T324lOwner> p) =>
                ctx.From<T324lChild>().Where(c => c.ParentId == p.Item1.PId))
            .Select(q => new
            {
                Parent = q.Item1.PId,
                Child = (int?)q.Item3.CId,
                Score = (int?)q.Item3.Score,
                Tag = q.Item3.Tag,
            })
            .ToList();

        rows.Should().HaveCount(5);
        var orphan = rows.Single(r => r.Parent == 4);
        orphan.Child.Should().BeNull("a preserved parent has no child id, not the default 0");
        orphan.Score.Should().BeNull("a preserved parent has no score, not the default 0");
        orphan.Tag.Should().BeNull("a preserved parent has no tag, not the empty string");
        rows.Where(r => r.Parent != 4).Should().OnlyContain(
            r => r.Child != null && r.Score != null && r.Tag != null);
    }

    [Fact]
    public void T324_Live_CrossApply_AfterJoin_DropsUnmatchedParent_OuterApply_PreservesIt()
        => T324LiveOrphanParent(_sut.DataProvider, Provider.SupportsApply);

    [Fact]
    public void T324_Live_AfterJoin_Correlates_Both_Left_Sides()
        => T324LiveBothLeftSides(_sut.DataProvider, Provider.SupportsApply);

    [Fact]
    public void T324_Live_AfterJoin_OrderedTop1_PerParentRow()
        => T324LiveOrderedTop1(_sut.DataProvider, Provider.SupportsApply);

    [Fact]
    public void T324_Live_AfterJoin_RepeatedClrTypes_ResolveTheCorrectOccurrence()
        => T324LiveRepeatedClrTypes(_sut.DataProvider, Provider.SupportsApply);

    [Fact]
    public void T324_Live_ApplyAfterApply_SecondSeesTheFirstSlot()
        => T324LiveApplyAfterApply(_sut.DataProvider, Provider.SupportsApply);

    [Fact]
    public void T324_Live_OuterApply_PreservedParent_YieldsNulls_NotDefaults()
        => T324LiveOuterNulls(_sut.DataProvider, Provider.SupportsApply);
}

[SqlTable("t324l_parent")]
public sealed class T324lParent
{
    [Key]
    [Column("p_id")]
    public int PId { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("t324l_owner")]
public sealed class T324lOwner
{
    [Key]
    [Column("o_id")]
    public int OId { get; set; }

    [Column("min_score")]
    public int MinScore { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("t324l_child")]
public sealed class T324lChild
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

[SqlTable("t324l_item")]
public sealed class T324lItem
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

[SqlTable("t324l_tag")]
public sealed class T324lTag
{
    [Key]
    [Column("tag_id")]
    public int TagId { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}
