using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T331 (#331) D4 — live execution of the two navigation-first <c>JoinInto</c> forms on every registered
/// provider. The fixture is designed so a wrong implementation cannot pass:
/// <list type="bullet">
/// <item>parent 3 has **no** children and no references — every scenario asserts it survives;</item>
/// <item>items 10/12 belong to parent 1 and item 11 to parent 2, so a grouping bug or a duplicated row is
/// visible; two optional-FK rows carry a <c>NULL</c> parent id and must never match anything;</item>
/// <item><c>other_id</c> equals <c>parent_id</c> for the equivalent case and points at the opposite parent
/// for an adversarial parent pair, so a filter that replaced the declared-key ON would still match;</item>
/// <item>the composite relationship pairs <c>(tenant, id)</c> with <c>(slot_a, slot_b)</c>, whose column
/// names never correspond, so only positional pairing can produce the expected rows.</item>
/// </list>
/// <para>
/// <b>ON-vs-WHERE discriminator.</b> Several scenarios assert that an unmatched or filtered-out parent is
/// <em>present</em> with an empty collection / null reference. If the optional filter were pushed into a
/// parent-filtering <c>WHERE</c> instead of the join <c>ON</c>, that parent row would disappear and the
/// assertion would fail: the oracle is the returned row, not the SQL text.
/// </para>
/// <para>
/// The facts carry the <c>Category=T331</c> trait individually (the shared suite is a <c>partial</c> class,
/// so a class-level trait would also mark every unrelated <see cref="CommonTestSuite"/> fact).
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    private const string T331Parent = "t331_live_parent";
    private const string T331Item = "t331_live_item";
    private const string T331Invoice = "t331_live_invoice";
    private const string T331Optional = "t331_live_optional";
    private const string T331Tag = "t331_live_tag";
    private const string T331ParentTag = "t331_live_parent_tag";
    private const string T331Blog = "t331_live_blog";
    private const string T331Post = "t331_live_post";
    private const string T331CfgParent = "t331_live_cfg_parent";
    private const string T331CfgConventional = "t331_live_cfg_child_conventional";
    private const string T331CfgConfigured = "t331_live_cfg_child_configured";

    private static readonly Lock T331Gate = new();
    private static readonly HashSet<string> T331Seeded = [];

    private EntityBuilder<T331LiveParent> T331Parents()
        => _sut.DataProvider.From<T331LiveParent>(b => b
            .HasMany(p => p.Items, c => c.ParentId)
            .HasOneToOne(p => p.Invoice, p => p.Id, i => i.ParentId)
            .HasOneToOne(p => p.Optional, p => p.Id, o => o.ParentId)
            .HasManyThrough<T331LiveTag, T331LiveParentTag>(p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.TagId));

    private EntityBuilder<T331LiveBlog> T331Blogs()
        => _sut.DataProvider.From<T331LiveBlog>(b => b
            .HasMany(p => p.Posts, p => new { p.SlotA, p.SlotB }, p => new { p.TenantId, p.Id }));

    /// <summary>
    /// Drops and creates the T331 fixture tables once per provider process, then seeds one fixed dataset.
    /// The tables belong only to this suite (no other <see cref="CommonTestSuite"/> partial touches them),
    /// so the ids are fixed and assertions stay exact. ClickHouse needs its own types and <c>Memory</c>
    /// engine and accepts no key clause; the other providers take portable ANSI DDL.
    /// </summary>
    private void EnsureT331Seeded()
    {
        var provider = Provider.Name;

        lock (T331Gate)
        {
            if (!T331Seeded.Add(provider))
                return;
        }

        var ctx = _sut.DataProvider;
        var clickHouse = provider == "clickhouse";

        void Table(string name, string columns)
            => Exec331(ctx, clickHouse
                ? $"create table {name} ({columns}) engine = Memory"
                : $"create table {name} ({columns})");

        string Id(string name) => clickHouse ? $"{name} Int32" : $"{name} int not null";
        string NullableId(string name) => clickHouse ? $"{name} Nullable(Int32)" : $"{name} int null";

        foreach (var table in new[]
                 {
                     T331Parent, T331Item, T331Invoice, T331Optional, T331Tag, T331ParentTag,
                     T331Blog, T331Post, T331CfgParent, T331CfgConventional, T331CfgConfigured,
                 })
        {
            Exec331(ctx, $"drop table if exists {table}");
        }

        // Registration order matters: the child mappings are registered before the parent side declares
        // its relationships, so a navigation member is recognized as a navigation and never mapped as a
        // column.
        ctx.From<T331LiveItem>();
        ctx.From<T331LiveInvoice>();
        ctx.From<T331LiveOptional>();
        ctx.From<T331LiveTag>();
        ctx.From<T331LiveParentTag>();
        T331Parents();
        ctx.From<T331LivePost>();
        T331Blogs();

        // AC08 — the configured child mapping is registered BEFORE the parent relationship and before any
        // conventional build of this type, so the configured table wins.
        ctx.From<T331LiveCfgChild>(b => b.Table(T331CfgConfigured));
        ctx.From<T331LiveCfgParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        Table(T331Parent, $"{Id("id")}");
        Table(T331Item, $"{Id("id")}, {Id("parent_id")}, {Id("other_id")}");
        Table(T331Invoice, $"{Id("id")}, {Id("parent_id")}");
        Table(T331Optional, $"{Id("id")}, {NullableId("parent_id")}");
        Table(T331Tag, $"{Id("id")}");
        Table(T331ParentTag, $"{Id("parent_id")}, {Id("tag_id")}");
        Table(T331Blog, $"{Id("tenant_id")}, {Id("id")}");
        Table(T331Post, $"{Id("id")}, {Id("slot_a")}, {Id("slot_b")}");
        Table(T331CfgParent, $"{Id("id")}");
        Table(T331CfgConventional, $"{Id("id")}, {Id("parent_id")}");
        Table(T331CfgConfigured, $"{Id("id")}, {Id("parent_id")}");

        // Parents 1 and 2 have children/references; 3 has none at all; 4 and 5 exist only for the
        // adversarial other_id case (their children point at the opposite parent).
        Exec331(ctx, $"insert into {T331Parent} (id) values (1), (2), (3), (4), (5)");
        Exec331(ctx, $"insert into {T331Item} (id, parent_id, other_id) values " +
                     "(10, 1, 1), (11, 2, 2), (12, 1, 1), (15, 4, 5), (16, 5, 4)");
        Exec331(ctx, $"insert into {T331Invoice} (id, parent_id) values (100, 1), (101, 2)");
        // Two rows with a NULL foreign key: a NULL component must never match, not even another NULL.
        Exec331(ctx, $"insert into {T331Optional} (id, parent_id) values (200, 1), (201, null), (202, null), (203, 2)");
        Exec331(ctx, $"insert into {T331Tag} (id) values (300), (301)");
        // The duplicate (1, 300) link proves the stitcher deduplicates junction rows.
        Exec331(ctx, $"insert into {T331ParentTag} (parent_id, tag_id) values (1, 300), (1, 301), (1, 300)");

        // Composite pairs: blog (tenant 1, id 20) <- post (slot_a 1, slot_b 20); blog (2, 10) <- post (2, 10).
        // Post 32 shares only the first component and post 33 is a cross-pair; both must be excluded.
        Exec331(ctx, $"insert into {T331Blog} (tenant_id, id) values (1, 20), (2, 10)");
        Exec331(ctx, $"insert into {T331Post} (id, slot_a, slot_b) values (30, 1, 20), (31, 2, 10), (32, 1, 99), (33, 2, 20)");

        // AC08: the conventional table and the configured table hold DIFFERENT children for the same
        // parent, so a convention-based resolution returns a distinguishable (wrong) row set.
        Exec331(ctx, $"insert into {T331CfgParent} (id) values (1)");
        Exec331(ctx, $"insert into {T331CfgConventional} (id, parent_id) values (410, 1), (411, 1)");
        Exec331(ctx, $"insert into {T331CfgConfigured} (id, parent_id) values (420, 1), (421, 1)");
    }

    private static void Exec331(IDataContext ctx, string sql)
    {
        using var _ = ctx.ExecuteRaw(sql);
    }

    /// <summary>Ordered, duplicate-preserving projection: <c>"id:c1,c2"</c> per parent, sorted by parent id.</summary>
    private static string[] T331CollectionShape(IEnumerable<T331LiveParent> parents)
        => parents.OrderBy(p => p.Id)
            .Select(p => $"{p.Id}:{string.Join(",", p.Items.Select(c => c.Id).OrderBy(id => id))}")
            .ToArray();

    private static void AssertT331ParentsPresent(IEnumerable<T331LiveParent> parents)
    {
        var ids = parents.Select(p => p.Id).OrderBy(id => id).ToArray();
        ids.Should().Contain([1, 2, 3], "a LEFT join keeps every parent, including the childless one");
    }

    // ---------------------------------------------------------------------------------------------
    // Reference navigation (AC01/AC03/AC10)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Reference_NavigationOnly_ShouldAssignTheSingleChildAndKeepUnmatchedParents()
    {
        EnsureT331Seeded();

        var parents = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Invoice)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents.Single(p => p.Id == 1).Invoice!.Id.Should().Be(100);
        parents.Single(p => p.Id == 2).Invoice!.Id.Should().Be(101);
        parents.Single(p => p.Id == 3).Invoice.Should().BeNull("no phantom default child for an unmatched parent");
    }

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Reference_WithFilter_ShouldKeepTheFilteredOutParentWithANullReference()
    {
        EnsureT331Seeded();

        // ON-vs-WHERE: the filter excludes parent 2's invoice and parent 3 has none. If the filter were
        // pushed into a WHERE, parent 2 would vanish from the result; the assertion is the row itself.
        var parents = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Invoice, (p, i) => i.Id == 100)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([1, 2, 3]);
        parents.Single(p => p.Id == 1).Invoice!.Id.Should().Be(100);
        parents.Single(p => p.Id == 2).Invoice.Should().BeNull();
        parents.Single(p => p.Id == 3).Invoice.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "T331")]
    public void T331_OptionalForeignKey_NullChildForeignKeys_ShouldNeverMatch()
    {
        EnsureT331Seeded();

        var parents = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Optional)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Single(p => p.Id == 1).Optional!.Id.Should().Be(200);
        parents.Single(p => p.Id == 2).Optional!.Id.Should().Be(203);
        parents.Single(p => p.Id == 3).Optional.Should().BeNull("a parent with no reference keeps null");
        parents.SelectMany(p => new[] { p.Optional?.Id }).Should()
            .NotContain([201, 202], "a NULL foreign-key child must not be assigned to any parent");
    }

    // ---------------------------------------------------------------------------------------------
    // Collection navigation (AC04/AC05/AC06)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Collection_NavigationOnly_ShouldGroupTheDeclaredChildrenWithoutDuplicates()
    {
        EnsureT331Seeded();

        var parents = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Items)
            .ToList();

        T331CollectionShape(parents).Should().Equal(["1:10,12", "2:11", "3:"]);
        parents.SelectMany(p => p.Items).Should().HaveCount(3, "no child may be attached twice");
    }

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Collection_KeyRestatingFilter_ShouldMatchTheNavigationOnlyResult()
    {
        EnsureT331Seeded();

        var parents = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Items, (p, c) => p.Id == c.ParentId)
            .ToList();

        T331CollectionShape(parents).Should().Equal(["1:10,12", "2:11", "3:"]);
    }

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Collection_ChildFilter_ShouldKeepParentOneEmptyAndParentTwoWithEleven()
    {
        EnsureT331Seeded();

        // ON-vs-WHERE: parent 1 loses both children to the filter but must stay in the result with an
        // empty collection; a WHERE-based filter would drop it.
        var parents = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Items, (p, c) => c.Id == 11)
            .ToList();

        T331CollectionShape(parents).Should().Equal(["1:", "2:11", "3:"]);
        parents.Single(p => p.Id == 2).Items.Select(c => c.Id).Should().Equal([11]);
    }

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Collection_OtherIdEquivalentFilter_ShouldMatchTheDeclaredResult()
    {
        EnsureT331Seeded();

        // other_id == parent_id for items 10/11/12, so an extra `p.Id == c.OtherId` conjunct is satisfied
        // by exactly the declared matches.
        var parents = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Items, (p, c) => p.Id == c.OtherId)
            .ToList();

        T331CollectionShape(parents).Should().Equal(["1:10,12", "2:11", "3:"]);
    }

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Collection_AdversarialOtherId_ShouldYieldEmptyCollectionsAndKeepBothParents()
    {
        EnsureT331Seeded();

        // Items 15 and 16 still satisfy the DECLARED key (parent_id points at their own parent) but their
        // other_id points at the opposite parent. A filter that replaced the declared-key ON would return
        // 4:[15] and 5:[16]; as an additional conjunct it matches nothing, and both parents stay.
        var parents = T331Parents()
            .Where(p => p.Id >= 4)
            .JoinInto(p => p.Items, (p, c) => p.Id == c.OtherId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([4, 5]);
        parents.SelectMany(p => p.Items).Should().BeEmpty("the declared key and the conflicting conjunct disagree");
    }

    // ---------------------------------------------------------------------------------------------
    // Composite keys (AC03)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Composite_CrossPairs_ShouldMatchPositionallyAndRejectPartialKeys()
    {
        EnsureT331Seeded();

        var blogs = T331Blogs().JoinInto(b => b.Posts).ToList();

        blogs.Should().HaveCount(2);
        blogs.Single(b => b.TenantId == 1 && b.Id == 20).Posts.Select(p => p.Id).Should().Equal([30]);
        blogs.Single(b => b.TenantId == 2 && b.Id == 10).Posts.Select(p => p.Id).Should().Equal([31]);
        blogs.SelectMany(b => b.Posts).Should().HaveCount(2,
            "post 32 (first component only) and post 33 (cross-pair) must be excluded");
    }

    // ---------------------------------------------------------------------------------------------
    // Junction (AC07)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Junction_NavigationOnly_ShouldInheritTheJunctionContract()
    {
        EnsureT331Seeded();

        var navigationFirst = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Tags)
            .ToList();

        // The inherited junction contract (existing #135 coverage,
        // `JoinInto_ManyToMany_ShouldPreserveDuplicateJunctionRows`): two junction rows linking the same
        // (parent, child) pair are DISTINCT elements, and a dangling link contributes none. The new form
        // reuses that synthesis, so it must reproduce it exactly - asserted against the T330 source-less
        // form rather than re-invented here.
        var sourceLess = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto<T331LiveTag>((p, t) => t.Id >= 300, p => p.Tags)
            .ToList();

        navigationFirst.Select(p => p.Id).OrderBy(id => id).Should().Equal([1, 2, 3]);
        navigationFirst.Single(p => p.Id == 1).Tags.Select(t => t.Id).OrderBy(id => id).Should()
            .Equal([300, 300, 301], "the duplicated (1, 300) link is two distinct junction rows");
        navigationFirst.Single(p => p.Id == 2).Tags.Should().BeEmpty();

        navigationFirst.OrderBy(p => p.Id).Select(p => $"{p.Id}:{string.Join(",", p.Tags.Select(t => t.Id).OrderBy(id => id))}")
            .Should().Equal(sourceLess
                .OrderBy(p => p.Id)
                .Select(p => $"{p.Id}:{string.Join(",", p.Tags.Select(t => t.Id).OrderBy(id => id))}"),
                "the navigation form must reproduce the source-less junction synthesis exactly");
    }

    [Fact]
    [Trait("Category", "T331")]
    public void T331_Junction_WithFilter_ShouldNarrowTheChildSetOnly()
    {
        EnsureT331Seeded();

        var navigationFirst = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Tags, (p, t) => t.Id == 300)
            .ToList();

        var explicitSource = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(_sut.DataProvider.From<T331LiveTag>(), (p, t) => t.Id == 300, p => p.Tags)
            .ToList();

        navigationFirst.Select(p => p.Id).OrderBy(id => id).Should().Equal([1, 2, 3],
            "the filter narrows the child set, never the parent set");
        navigationFirst.Single(p => p.Id == 1).Tags.Select(t => t.Id).OrderBy(id => id).Should()
            .Equal([300, 300], "both junction rows for tag 300 survive the filter");
        navigationFirst.Single(p => p.Id == 2).Tags.Should().BeEmpty();

        navigationFirst.OrderBy(p => p.Id).Select(p => $"{p.Id}:{string.Join(",", p.Tags.Select(t => t.Id).OrderBy(id => id))}")
            .Should().Equal(explicitSource
                .OrderBy(p => p.Id)
                .Select(p => $"{p.Id}:{string.Join(",", p.Tags.Select(t => t.Id).OrderBy(id => id))}"),
                "the filtered navigation form matches the explicit-source form with the same predicate");
    }

    // ---------------------------------------------------------------------------------------------
    // AC08 — configured source wins over the conventionally named competing table.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "T331")]
    public void T331_ConfiguredSource_ShouldWinOverTheConventionalTable()
    {
        EnsureT331Seeded();

        var parents = _sut.DataProvider.From<T331LiveCfgParent>()
            .JoinInto(p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).OrderBy(id => id).Should().Equal([420, 421],
            "the configured table holds 420/421; the conventionally named table holds 410/411");
    }

    // ---------------------------------------------------------------------------------------------
    // Live comparison: navigation-only vs navigation+predicate vs the explicit-key legacy form.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "T331")]
    public void T331_NavigationVsPredicateVsExplicitKey_ShouldAgreeOnTheFilteredRows()
    {
        EnsureT331Seeded();

        var navigationOnly = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Items)
            .ToList();

        var navigationWithPredicate = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto(p => p.Items, (p, c) => c.Id != 12)
            .ToList();

        // (c) the legacy explicit-key form with the same predicate folded into its ON by the caller.
        var explicitKey = T331Parents()
            .Where(p => p.Id <= 3)
            .JoinInto<T331LiveItem, int>(
                _sut.DataProvider.From<T331LiveItem>(),
                (p, c) => p.Id == c.ParentId && c.Id != 12,
                p => p.Items,
                p => p.Id,
                c => c.ParentId)
            .ToList();

        T331CollectionShape(navigationWithPredicate).Should().Equal(T331CollectionShape(explicitKey),
            "(b) and (c) must return the same rows");
        T331CollectionShape(navigationOnly).Should().Equal(["1:10,12", "2:11", "3:"]);
        T331CollectionShape(navigationWithPredicate).Should().Equal(["1:10", "2:11", "3:"],
            "(a) and (b) differ only by the filtered-out child");
        AssertT331ParentsPresent(navigationWithPredicate);
        AssertT331ParentsPresent(explicitKey);
    }
}

[SqlTable("t331_live_parent")]
public sealed class T331LiveParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<T331LiveItem> Items { get; set; } = new List<T331LiveItem>();

    public T331LiveInvoice? Invoice { get; set; }

    public T331LiveOptional? Optional { get; set; }

    public ICollection<T331LiveTag> Tags { get; set; } = new List<T331LiveTag>();
}

[SqlTable("t331_live_item")]
public sealed class T331LiveItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("other_id")]
    public int OtherId { get; set; }
}

[SqlTable("t331_live_invoice")]
public sealed class T331LiveInvoice
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("t331_live_optional")]
public sealed class T331LiveOptional
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }
}

[SqlTable("t331_live_tag")]
public sealed class T331LiveTag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("t331_live_parent_tag")]
public sealed class T331LiveParentTag
{
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }
}

[SqlTable("t331_live_blog")]
public sealed class T331LiveBlog
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<T331LivePost> Posts { get; set; } = new List<T331LivePost>();
}

[SqlTable("t331_live_post")]
public sealed class T331LivePost
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("slot_a")]
    public int SlotA { get; set; }

    [Column("slot_b")]
    public int SlotB { get; set; }
}

[SqlTable("t331_live_cfg_parent")]
public sealed class T331LiveCfgParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<T331LiveCfgChild> Children { get; set; } = new List<T331LiveCfgChild>();
}

/// <summary>The navigation target: its attribute names the conventional table, and the mapping is
/// configured to the other table at registration time (AC08).</summary>
[SqlTable("t331_live_cfg_child_conventional")]
public sealed class T331LiveCfgChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}
