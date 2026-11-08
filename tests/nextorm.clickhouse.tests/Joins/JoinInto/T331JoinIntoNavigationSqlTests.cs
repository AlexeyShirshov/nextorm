using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T331 (issue #331) D3 — SQL cells for the two navigation-first <c>JoinInto</c> forms on ClickHouse.
/// These tests never open a connection: they render the statement over the provider's placeholder
/// connection and assert its text.
/// <para>
/// <b>Assertion method (stated once, applies to every cell).</b> The statement is canonicalised by
/// stripping identifier quoting (<c>"</c>, <c>[</c> <c>]</c>, backtick, <c>'</c>), collapsing whitespace and
/// lowercasing, so the same expectation holds across providers. It is then split on the
/// <c>left join</c> keyword; each clause's <c>ON</c> body is sliced from <c> on </c> to the first
/// trailing statement clause (<c>where</c>/<c>group by</c>/<c>order by</c>/<c>limit</c>/<c>offset</c>/
/// <c>settings</c>) and compared <b>exactly</b> with the expected key equalities, in declaration order - no
/// <c>and</c>-counting. "Filter inside ON" is asserted twice: the filter fragment must be part of that
/// sliced ON body, and the statement must contain no <c>WHERE</c> clause at all (these cells declare no
/// parent filter), so the filter can only live in the join condition.
/// </para>
/// <para>
/// The composite fixtures use deliberately non-matching column names (<c>slot_a</c>/<c>slot_b</c>,
/// <c>ref_tenant</c>/<c>ref_owner</c>) so a name-based pairing cannot produce the asserted equalities:
/// only the declared positional pairing can.
/// </para>
/// </summary>
[Trait("Category", "T331")]
public class T331JoinIntoNavigationSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, EntityBuilder<T> builder)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(
                builder.ToCommand(), false, false, CancellationToken.None))
            .DbCommand.CommandText);

    private static string Canon(string sql) => Regex.Replace(
            sql.Replace("\r\n", "\n").Replace("\"", string.Empty).Replace("[", string.Empty)
                .Replace("]", string.Empty).Replace("`", string.Empty).Replace("'", string.Empty),
            @"\s+", " ").Trim().ToLowerInvariant();

    /// <summary>Canonical join clauses in statement order; index 0 is the first <c>left join</c>.</summary>
    private static string[] JoinClauses(string sql)
        => Canon(sql).Split("left join ", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

    /// <summary>The <c>ON</c> body of one canonical join clause, without the trailing statement clauses.</summary>
    private static string OnOf(string clause)
    {
        var on = clause[clause.IndexOf(" on ", StringComparison.Ordinal)..];
        foreach (var marker in new[] { " where ", " group by ", " order by ", " limit ", " offset ", " settings " })
        {
            var index = on.IndexOf(marker, StringComparison.Ordinal);
            if (index > 0)
                on = on[..index];
        }

        return on.Trim();
    }

    /// <summary>Asserts a single left join to <paramref name="table"/> whose ON is exactly <paramref name="expectedOn"/>.</summary>
    private static void AssertSingleLeftJoin(string sql, string table, string expectedOn)
    {
        var clauses = JoinClauses(sql);
        clauses.Should().HaveCount(1, "the direct forms declare exactly one join");
        clauses[0].Should().StartWith(table + " as t2", "the child source is the mapped child table");
        OnOf(clauses[0]).Should().Be("on " + expectedOn,
            "the ON condition is every declared key component, conjoined in declaration order");
        Canon(sql).Should().NotContain(" where ", "nothing may be pushed into a parent-filtering WHERE");
    }

    /// <summary>Asserts the filter fragment is inside the sliced ON body and never inside a WHERE clause.</summary>
    private static void AssertFilterInOn(string sql, string filterFragment, string expectedOn)
    {
        var clauses = JoinClauses(sql);
        OnOf(clauses[^1]).Should().Contain(filterFragment, "the filter is an additional conjunct inside ON");
        OnOf(clauses[^1]).Should().Be("on " + expectedOn, "the declared key equalities stay exactly as declared");
        Canon(sql).Should().NotContain(" where ", "a filtered JoinInto must not add a WHERE clause");
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures: single-key parent (collection, reference, nullable-FK reference, single junction)
    // and composite-key parent (reference, cross-paired collection, composite junction).
    // ---------------------------------------------------------------------------------------------

    private static EntityBuilder<T331Owner> Owners(IDataContext ctx) =>
        ctx.From<T331Owner>(b => b
            .HasMany(o => o.Items, i => i.OwnerId)
            .HasOneToOne(o => o.Invoice, o => o.Id, i => i.OwnerId)
            .HasOneToOne(o => o.Optional, o => (int?)o.Id, d => d.OwnerId)
            .HasManyThrough<T331Tag, T331OwnerTagLink>(o => o.Tags, o => o.Id, l => l.OwnerId, t => t.Id, l => l.TagId));

    private static EntityBuilder<T331Blog> Blogs(IDataContext ctx) =>
        ctx.From<T331Blog>(b => b
            .HasMany(p => p.Posts, p => new { p.SlotA, p.SlotB }, p => new { p.TenantId, p.Id })
            .HasOneToOne(p => p.Detail, p => new { p.TenantId, p.Id }, d => new { d.RefTenant, d.RefOwner })
            .HasManyThrough<T331Tag, T331BlogTagLink>(
                p => p.Tags,
                p => new { p.TenantId, p.Id },
                l => new { l.TenantId, l.OwnerId },
                t => t.Id,
                l => l.TagId));

    // ---------------------------------------------------------------------------------------------
    // Reference x single key, across the four filter variants.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Reference_SingleKey_NavigationOnly_ShouldRenderTheDeclaredKeyOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        AssertSingleLeftJoin(SqlOf(ctx, Owners(ctx).JoinInto(o => o.Invoice)), "t331_nav_invoice",
            "t1.id = t2.owner_id");
    }

    [Fact]
    public void Reference_SingleKey_KeyRestatingFilter_ShouldKeepBothConjunctsInOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Invoice, (o, i) => o.Id == i.OwnerId));

        AssertFilterInOn(sql, "t1.id = t2.owner_id", "(t1.id = t2.owner_id and t1.id = t2.owner_id)");
        OnOf(JoinClauses(sql)[0]).Split("t1.id = t2.owner_id").Should().HaveCount(3,
            "the declared key equality and the restating filter are two separate conjuncts");
    }

    [Fact]
    public void Reference_SingleKey_ChildFilter_ShouldSitInsideOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Invoice, (o, i) => i.Flag == 1));

        AssertFilterInOn(sql, "t2.flag = 1", "(t1.id = t2.owner_id and t2.flag = 1)");
    }

    [Fact]
    public void Reference_SingleKey_ConflictingFilter_ShouldBeAnExtraConjunct()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Invoice, (o, i) => o.Id == i.OtherId));

        AssertFilterInOn(sql, "t1.id = t2.other_id", "(t1.id = t2.owner_id and t1.id = t2.other_id)");
    }

    // ---------------------------------------------------------------------------------------------
    // Collection x single key.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Collection_SingleKey_NavigationOnly_ShouldRenderTheDeclaredKeyOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        AssertSingleLeftJoin(SqlOf(ctx, Owners(ctx).JoinInto(o => o.Items)), "t331_nav_item",
            "t1.id = t2.owner_id");
    }

    [Fact]
    public void Collection_SingleKey_ChildFilter_ShouldSitInsideOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Items, (o, i) => i.Flag == 1));

        AssertFilterInOn(sql, "t2.flag = 1", "(t1.id = t2.owner_id and t2.flag = 1)");
    }

    // ---------------------------------------------------------------------------------------------
    // Composite two-component keys: reference and cross-paired collection.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Reference_CompositeKey_ShouldRenderBothComponentsInDeclarationOrder()
    {
        using var ctx = ClickHouseTestContext.Create();
        AssertSingleLeftJoin(SqlOf(ctx, Blogs(ctx).JoinInto(b => b.Detail)), "t331_nav_detail",
            "(t1.tenant_id = t2.ref_tenant and t1.id = t2.ref_owner)");
    }

    [Fact]
    public void Collection_CompositeCrossPaired_ShouldPairPositionallyNotByName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Blogs(ctx).JoinInto(b => b.Posts));

        AssertSingleLeftJoin(sql, "t331_nav_post",
            "(t1.tenant_id = t2.slot_a and t1.id = t2.slot_b)");
        Canon(sql).Should().NotContain("t2.tenant_id",
            "a name-based pairing would look for the child tenant column, which this declaration never names");
    }

    [Fact]
    public void Collection_CompositeKey_WithFilter_ShouldKeepBothKeyComponentsAndTheFilter()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Blogs(ctx).JoinInto(b => b.Posts, (b, p) => p.Flag == 2));

        AssertFilterInOn(sql, "t2.flag = 2",
            "((t1.tenant_id = t2.slot_a and t1.id = t2.slot_b) and t2.flag = 2)");
    }

    // ---------------------------------------------------------------------------------------------
    // Nullable / optional foreign key.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Reference_NullableForeignKey_NavigationOnly_ShouldRenderAPlainEquality()
    {
        using var ctx = ClickHouseTestContext.Create();
        AssertSingleLeftJoin(SqlOf(ctx, Owners(ctx).JoinInto(o => o.Optional)), "t331_nav_optional",
            "t1.id = t2.owner_id");
    }

    [Fact]
    public void Reference_NullableForeignKey_WithFilter_ShouldSitInsideOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Optional, (o, d) => d.Id > 0));

        AssertFilterInOn(sql, "t2.id > 0", "(t1.id = t2.owner_id and (t2.id > 0))");
    }

    // ---------------------------------------------------------------------------------------------
    // Junction (many-to-many): both edges are synthesized, the filter lands on the child edge.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Junction_SingleKey_ShouldSynthesizeBothEdges()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Tags));
        var clauses = JoinClauses(sql);

        clauses.Should().HaveCount(2, "a junction declaration renders parent->link and link->child");
        OnOf(clauses[0]).Should().Be("on t1.id = t2.parentkey", "the parent edge is the declared parent key");
        OnOf(clauses[1]).Should().Contain("t3.id = t2.childkey", "the child edge is the declared child key");
        Canon(sql).Should().NotContain(" where ");
    }

    [Fact]
    public void Junction_SingleKey_WithFilter_ShouldKeepTheParentEdgeAndFilterTheChildEdge()
    {
        using var ctx = ClickHouseTestContext.Create();
        var filtered = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Tags, (o, t) => t.Id > 10));
        var unfiltered = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Tags));

        var filteredClauses = JoinClauses(filtered);
        var unfilteredClauses = JoinClauses(unfiltered);

        OnOf(filteredClauses[0]).Should().Be(OnOf(unfilteredClauses[0]),
            "the filter never touches the parent->junction edge");
        OnOf(filteredClauses[1]).Should().Contain("t3.id = t2.childkey");
        OnOf(filteredClauses[1]).Should().Contain("t3.id > 10", "the filter is an extra child-edge conjunct");
        Canon(filtered).Should().NotContain(" where ");
    }

    [Fact]
    public void Junction_CompositeKey_ShouldRouteBothLegsByEveryComponent()
    {
        using var ctx = ClickHouseTestContext.Create();
        var sql = SqlOf(ctx, Blogs(ctx).JoinInto(b => b.Tags));
        var clauses = JoinClauses(sql);

        clauses.Should().HaveCount(2);
        OnOf(clauses[0]).Should().Be("on (t1.tenant_id = t2.parentkey1 and t1.id = t2.parentkey2)",
            "both declared principal components route the parent->link edge");
        OnOf(clauses[1]).Should().Contain("t3.id = t2.childkey1");
    }

    [Fact]
    public void Junction_CompositeKey_WithFilter_ShouldFilterOnlyTheChildEdge()
    {
        using var ctx = ClickHouseTestContext.Create();
        var filtered = SqlOf(ctx, Blogs(ctx).JoinInto(b => b.Tags, (b, t) => t.Id > 10));
        var unfiltered = SqlOf(ctx, Blogs(ctx).JoinInto(b => b.Tags));

        var filteredClauses = JoinClauses(filtered);
        OnOf(filteredClauses[0]).Should().Be(OnOf(JoinClauses(unfiltered)[0]));
        OnOf(filteredClauses[1]).Should().Contain("t3.id = t2.childkey1");
        OnOf(filteredClauses[1]).Should().Contain("t3.id > 10");
        Canon(filtered).Should().NotContain(" where ");
    }

    // ---------------------------------------------------------------------------------------------
    // Equivalence with the explicit-key legacy form: predicate = keys AND filter.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NewForm_ShouldRenderTheSameStatementAsTheExplicitKeyLegacyForm()
    {
        using var ctx = ClickHouseTestContext.Create();

        // The legacy key form has no metadata-derived ON: the caller restates the keys and then the
        // filter, so its statement is the reference the new form must match exactly.
        var legacy = SqlOf(ctx, Owners(ctx).JoinInto<T331Item, int>(
            ctx.From<T331Item>(),
            (o, i) => o.Id == i.OwnerId && i.Flag == 1,
            o => o.Items,
            o => o.Id,
            i => i.OwnerId));
        var navigationFirst = SqlOf(ctx, Owners(ctx).JoinInto(o => o.Items, (o, i) => i.Flag == 1));

        navigationFirst.Should().Be(legacy,
            "the metadata-derived ON plus the filter must equal the explicit keys plus the filter");
    }

    // ---------------------------------------------------------------------------------------------
    // AC08 — a configured child mapping wins over the conventional table name, for the navigation
    // form and for the T330 source-less counterpart.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ConfiguredMapping_ShouldWin_ForBothNavigationFirstAndSourceLessForms()
    {
        using var ctx = ClickHouseTestContext.Create();

        // First use of the type in this process: the configured mapping is registered before any
        // conventional build, which is the pattern the existing configured-mapping tests use.
        ctx.From<T331ConfiguredChild>(b => b.Table("t331_configured_child"));

        var navigationFirst = SqlOf(ctx, ConfiguredOwners(ctx).JoinInto(o => o.Children));
        var sourceLess = SqlOf(ctx, ConfiguredOwners(ctx)
            .JoinInto<T331ConfiguredChild>((o, c) => o.Id == c.OwnerId, o => o.Children));

        navigationFirst.Should().Contain("t331_configured_child", "the configured mapping wins over convention");
        navigationFirst.Should().NotContain("t331configuredchild", "the conventional type name must not be used");
        navigationFirst.Should().Be(sourceLess,
            "the navigation form resolves the same child source as the T330 source-less counterpart");
    }

    private static EntityBuilder<T331ConfiguredOwner> ConfiguredOwners(IDataContext ctx) =>
        ctx.From<T331ConfiguredOwner>(b => b.HasMany(o => o.Children, c => c.OwnerId));

    // ---------------------------------------------------------------------------------------------
    // AC09 at SQL level: the legacy forms keep the caller predicate as the whole ON condition.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void LegacyOriginalForms_ShouldKeepTheVerbatimPredicateAsTheWholeOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var child = ctx.From<T331Item>();

        // The five original overloads: collection, collection+kind, reference, reference+kind, key form.
        var collection = SqlOf(ctx, Owners(ctx).JoinInto(child, (o, i) => o.Id == i.OwnerId, o => o.Items));
        var collectionInner = SqlOf(ctx, Owners(ctx)
            .JoinInto(child, (o, i) => o.Id == i.OwnerId, o => o.Items, JoinType.Inner));
        var reference = SqlOf(ctx, Owners(ctx)
            .JoinInto(ctx.From<T331Invoice>(), (o, i) => o.Id == i.OwnerId, o => o.Invoice));
        var referenceInner = SqlOf(ctx, Owners(ctx)
            .JoinInto(ctx.From<T331Invoice>(), (o, i) => o.Id == i.OwnerId, o => o.Invoice, JoinType.Inner));
        var keyForm = SqlOf(ctx, Owners(ctx)
            .JoinInto<T331Item, int>(child, (o, i) => o.Id == i.OwnerId, o => o.Items, o => o.Id, i => i.OwnerId));

        foreach (var sql in new[] { collection, reference, keyForm })
        {
            var clauses = JoinClauses(sql);
            clauses.Should().HaveCount(1);
            OnOf(clauses[0]).Should().Be("on t1.id = t2.owner_id",
                "a legacy ON is the caller predicate verbatim: one equality, no synthesized key conjunct");
        }

        Canon(collectionInner).Should().Contain("join t331_nav_item as t2",
            "the inner legacy form keeps its own join kind");
        Canon(collectionInner).Should().NotContain("left join");
        Canon(referenceInner).Should().NotContain("left join");
    }

    [Fact]
    public void LegacyCompositePredicate_ShouldStayOneEquality()
    {
        using var ctx = ClickHouseTestContext.Create();

        // The navigation declares two components; the legacy ON still has exactly the one equality the
        // caller wrote, never the synthesized second component.
        var sql = SqlOf(ctx, Blogs(ctx).JoinInto(ctx.From<T331Post>(), (b, p) => b.Id == p.SlotB, b => b.Posts));
        var clauses = JoinClauses(sql);

        OnOf(clauses[0]).Should().Be("on t1.id = t2.slot_b");
        Canon(sql).Should().NotContain("t1.tenant_id = t2.slot_a",
            "legacy ONs are never merged with the declared relationship keys");
    }
}

[SqlTable("t331_nav_owner")]
public sealed class T331Owner
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<T331Item> Items { get; set; } = new List<T331Item>();

    public T331Invoice? Invoice { get; set; }

    public T331Optional? Optional { get; set; }

    public ICollection<T331Tag> Tags { get; set; } = new List<T331Tag>();
}

[SqlTable("t331_nav_item")]
public sealed class T331Item
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Column("other_id")]
    public int OtherId { get; set; }

    [Column("flag")]
    public int Flag { get; set; }
}

[SqlTable("t331_nav_invoice")]
public sealed class T331Invoice
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Column("other_id")]
    public int OtherId { get; set; }

    [Column("flag")]
    public int Flag { get; set; }
}

[SqlTable("t331_nav_optional")]
public sealed class T331Optional
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("owner_id")]
    public int? OwnerId { get; set; }
}

[SqlTable("t331_nav_tag")]
public sealed class T331Tag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("t331_nav_owner_tag")]
public sealed class T331OwnerTagLink
{
    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }
}

[SqlTable("t331_nav_blog")]
public sealed class T331Blog
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<T331Post> Posts { get; set; } = new List<T331Post>();

    public T331Detail? Detail { get; set; }

    public ICollection<T331Tag> Tags { get; set; } = new List<T331Tag>();
}

[SqlTable("t331_nav_post")]
public sealed class T331Post
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("slot_a")]
    public int SlotA { get; set; }

    [Column("slot_b")]
    public int SlotB { get; set; }

    [Column("flag")]
    public int Flag { get; set; }
}

[SqlTable("t331_nav_detail")]
public sealed class T331Detail
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ref_tenant")]
    public int RefTenant { get; set; }

    [Column("ref_owner")]
    public int RefOwner { get; set; }
}

[SqlTable("t331_nav_blog_tag")]
public sealed class T331BlogTagLink
{
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }
}

/// <summary>Child used only by the configured-mapping cell: no <c>[SqlTable]</c>, so the conventional name
/// competes with the configured one.</summary>
public sealed class T331ConfiguredChild
{
    public int Id { get; set; }

    public int OwnerId { get; set; }
}

public sealed class T331ConfiguredOwner
{
    public int Id { get; set; }

    public ICollection<T331ConfiguredChild> Children { get; set; } = new List<T331ConfiguredChild>();
}

