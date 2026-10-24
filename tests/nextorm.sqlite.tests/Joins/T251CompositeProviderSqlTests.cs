using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T251 SQL-generation matrix for a <b>composite</b> relationship key, rendered by this provider's
/// dialect: implicit N:1 reference and 1:N collection navigation, 1:1, composite many-to-many through a
/// junction (derived link carrier and the correlated/collection route), nullable foreign-key components
/// per position, an explicit non-key principal and a reversed declaration whose components keep the
/// declared (positional) order. Every row asserts the rendered SQL text of <b>this</b> dialect; no
/// dialect is compared with another and no supported route is marked unsupported. These tests never open
/// a connection.
/// </summary>
[Collection("Query plan cache")]
public class T251CompositeProviderSqlTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static string Canon(string sql)
    {
        var s = sql
            .Replace("\r\n", "\n")
            .Replace("\"", string.Empty)
            .Replace("'", string.Empty)
            .Replace("`", string.Empty)
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);
        return Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static IDataContext CreateContext() => SqliteTestContext.Create();

    private static void RegisterGraph()
    {
        var blog = new EntityMetadataBuilder<T251Blog>();
        blog.HasMany(b => b.Posts, p => new { p.TenantId, p.BlogId });
        DataContextCache.Metadata[typeof(T251Blog)] = blog.Build();

        var post = new EntityMetadataBuilder<T251Post>();
        post.HasOne(p => p.Blog, p => new { p.TenantId, p.BlogId }, b => new { b.TenantId, b.Id })
            .HasMany(p => p.Comments, c => new { c.TenantId, c.PostId })
            .HasManyThrough<T251Tag, T251TagLink>(
                p => p.Tags,
                p => new { p.TenantId, p.Id },
                l => new { l.TenantId, l.PostId },
                t => new { t.TenantId, t.Id },
                l => new { l.TenantId, l.TagId });
        DataContextCache.Metadata[typeof(T251Post)] = post.Build();

        var comment = new EntityMetadataBuilder<T251Comment>();
        comment.HasOne(c => c.Post, c => new { c.TenantId, c.PostId }, p => new { p.TenantId, p.Id });
        DataContextCache.Metadata[typeof(T251Comment)] = comment.Build();

        DataContextCache.Metadata[typeof(T251Tag)] = new EntityMetadataBuilder<T251Tag>().Build();
        DataContextCache.Metadata[typeof(T251TagLink)] = new EntityMetadataBuilder<T251TagLink>().Build();
    }

    private static void RegisterOneToOne()
    {
        var parent = new EntityMetadataBuilder<T251ProfileParent>();
        parent.HasOneToOne(p => p.Profile, p => new { p.TenantId, p.Id }, c => new { c.TenantId, c.ParentId });
        DataContextCache.Metadata[typeof(T251ProfileParent)] = parent.Build();
        DataContextCache.Metadata[typeof(T251Profile)] = new EntityMetadataBuilder<T251Profile>().Build();
    }

    private static void RegisterExplicitPrincipal()
    {
        var owner = new EntityMetadataBuilder<T251Owner>();
        owner.HasMany(o => o.Items, i => new { i.TenantId, i.OwnerCode }, o => new { o.TenantId, o.Code });
        DataContextCache.Metadata[typeof(T251Owner)] = owner.Build();

        var item = new EntityMetadataBuilder<T251Item>();
        item.HasOne(i => i.Owner, i => new { i.TenantId, i.OwnerCode }, o => new { o.TenantId, o.Code });
        DataContextCache.Metadata[typeof(T251Item)] = item.Build();
    }

    private static void RegisterNullableFirst()
    {
        var parent = new EntityMetadataBuilder<T251NullableFirstParent>();
        parent.HasMany(p => p.Children, c => new { c.TenantId, c.ParentId }, p => new { p.TenantId, p.Id });
        DataContextCache.Metadata[typeof(T251NullableFirstParent)] = parent.Build();
        DataContextCache.Metadata[typeof(T251NullableFirstChild)] = new EntityMetadataBuilder<T251NullableFirstChild>().Build();
    }

    private static void RegisterNullableLast()
    {
        var parent = new EntityMetadataBuilder<T251NullableLastParent>();
        parent.HasMany(p => p.Children, c => new { c.TenantId, c.ParentId }, p => new { p.TenantId, p.Id });
        DataContextCache.Metadata[typeof(T251NullableLastParent)] = parent.Build();
        DataContextCache.Metadata[typeof(T251NullableLastChild)] = new EntityMetadataBuilder<T251NullableLastChild>().Build();
    }

    private static void RegisterReversed()
    {
        var parent = new EntityMetadataBuilder<T251ReversedParent>();
        parent.HasMany(p => p.Children, c => new { c.ParentId, c.TenantId }, p => new { p.Id, p.TenantId });
        DataContextCache.Metadata[typeof(T251ReversedParent)] = parent.Build();

        var child = new EntityMetadataBuilder<T251ReversedChild>();
        child.HasOne(c => c.Parent, c => new { c.ParentId, c.TenantId }, p => new { p.Id, p.TenantId });
        DataContextCache.Metadata[typeof(T251ReversedChild)] = child.Build();
    }

    [Fact]
    public void ReferenceNavigation_ShouldRenderOnePredicatePerComponentInDeclaredOrder()
    {
        RegisterGraph();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251Post>().Select(p => p.Blog!.Name)));

        sql.Should().Contain("left join t251_blog as t2 on (t1.tenant_id = t2.tenant_id and t1.blog_id = t2.id)",
            "the N:1 reference join pairs every component, not only the first");
        sql.IndexOf("t1.tenant_id = t2.tenant_id", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("t1.blog_id = t2.id", StringComparison.Ordinal),
                "the predicates keep the declared component order");
    }

    [Fact]
    public void CollectionPredicate_ShouldRenderEveryComponent()
    {
        RegisterGraph();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251Blog>().Where(b => b.Posts.Any()).ToCommand()));

        sql.Should().Contain("exists(select * from t251_post as t2 where (t2.tenant_id = t1.tenant_id and t2.blog_id = t1.id))",
            "the 1:N collection predicate matches every component of the principal key");
        sql.Should().NotContain("t2.blog_id = t1.id) or", "a first-component-only fallback is not acceptable");
    }

    [Fact]
    public void MultiHopReferenceNavigation_ShouldPairEveryComponentOnEveryHop()
    {
        RegisterGraph();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251Comment>().Select(c => c.Post!.Blog!.Name)));

        sql.Should().Contain("left join t251_post as t2 on (t1.tenant_id = t2.tenant_id and t1.post_id = t2.id)",
            "the comment -> post hop pairs both components");
        sql.Should().Contain("left join t251_blog as t3 on (t2.tenant_id = t3.tenant_id and t2.blog_id = t3.id)",
            "the post -> blog hop pairs both components");
    }

    [Fact]
    public void CorrelatedReferenceAndCollection_ShouldUseEveryComponent()
    {
        RegisterGraph();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251Post>().Where(p => p.Blog!.Posts.Any()).ToCommand()));

        sql.Should().Contain("left join t251_blog as t2 on (t1.tenant_id = t2.tenant_id and t1.blog_id = t2.id)",
            "the correlated reference uses both components");
        sql.Should().Contain("exists(select * from t251_post as t3 where (t3.tenant_id = t2.tenant_id and t3.blog_id = t2.id))",
            "the correlated collection uses both components");
    }

    [Fact]
    public void OneToOne_ShouldRenderComponentwiseJoin()
    {
        RegisterOneToOne();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251ProfileParent>().Select(p => p.Profile!.Bio)));

        sql.Should().Contain("left join t251_profile as t2 on (t1.tenant_id = t2.tenant_id and t1.id = t2.parent_id)",
            "the 1:1 join pairs every component of the principal and dependent keys");
    }

    [Fact]
    public void ManyToManyJunction_CollectionRoute_ShouldUseBothLegsAndEveryComponent()
    {
        RegisterGraph();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251Post>().Where(p => p.Tags.Any()).ToCommand()));

        sql.Should().Contain("from t251_tag_link as t2", "the junction is the collection source");
        sql.Should().Contain("t2.tenant_id = t1.tenant_id and t2.post_id = t1.id",
            "the parent leg pairs every junction parent component");
        sql.Should().Contain("t2.tenant_id in (select t3.tenant_id", "the child leg checks its first component");
        sql.Should().Contain("from t251_tag as t3", "the child leg resolves through the child key");
        sql.Should().Contain("t2.tag_id in (select t4.id from t251_tag as t4)",
            "the child leg checks its second component against the child key");
    }

    [Fact]
    public void ManyToManyJunction_CorrelatedRoute_ShouldUseBothLegsAndEveryComponent()
    {
        RegisterGraph();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251Comment>().Where(c => c.Post!.Tags.Any()).ToCommand()));

        sql.Should().Contain("left join t251_post as t2 on (t1.tenant_id = t2.tenant_id and t1.post_id = t2.id)",
            "the correlated reference hop pairs every component");
        sql.Should().Contain("t3.tenant_id = t2.tenant_id and t3.post_id = t2.id",
            "the correlated junction parent leg pairs every component");
        sql.Should().Contain("t3.tenant_id in (select t4.tenant_id", "the correlated junction child leg checks its first component");
        sql.Should().Contain("t3.tag_id in (select t5.id from t251_tag as t5)",
            "the correlated junction child leg checks its second component against the child key");
    }

    [Fact]
    public void ManyToManyJunction_JoinInto_ShouldRenderBothDispatcherEdgesWithEveryComponent()
    {
        RegisterGraph();
        using var ctx = CreateContext();

        var command = (dynamic)ctx.From<T251Post>()
            .JoinInto(ctx.From<T251Tag>(), (p, t) => true, p => p.Tags)
            .CreateJoinIntoPairCommand();
        var sql = Canon((string)SqlOf(ctx, command));

        sql.Should().Contain("from t251_tag_link) as t2", "the derived composite link source is rendered");
        sql.Should().Contain("tenant_id as parentkey1")
            .And.Contain("post_id as parentkey2")
            .And.Contain("tenant_id as childkey1")
            .And.Contain("tag_id as childkey2");
        sql.Should().Contain("partition by tenant_id, post_id, tenant_id, tag_id order by tenant_id, tag_id",
            "the occurrence window is scoped by every junction component on both legs");
        sql.Should().Contain("on (t1.tenant_id = t2.parentkey1 and t1.id = t2.parentkey2)",
            "the parent edge uses every parent component");
        sql.Should().Contain("t3.tenant_id = t2.childkey1 and t3.id = t2.childkey2",
            "the child edge uses every child component");
        ((bool)command.Cache).Should().BeTrue("a composite-junction JoinInto must not disable the plan cache");
    }

    [Fact]
    public void NullableFirstForeignKeyComponent_ShouldRenderPlainEquality()
    {
        RegisterNullableFirst();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251NullableFirstParent>().Where(p => p.Children.Any()).ToCommand()));

        sql.Should().Contain("exists(select * from t251_nullable_first_child as t2 where (t2.tenant_id = t1.tenant_id and t2.parent_id = t1.id))",
            "a nullable component in the first position is still a plain equality on every component");
        sql.Should().NotContain("coalesce")
            .And.NotContain("<=>")
            .And.NotContain("is not distinct")
            .And.NotContain("is null", "null-safe equality would silently link an all-null key row");
    }

    [Fact]
    public void NullableLastForeignKeyComponent_ShouldRenderPlainEquality()
    {
        RegisterNullableLast();
        using var ctx = CreateContext();

        var sql = Canon(SqlOf(ctx, ctx.From<T251NullableLastParent>().Where(p => p.Children.Any()).ToCommand()));

        sql.Should().Contain("exists(select * from t251_nullable_last_child as t2 where (t2.tenant_id = t1.tenant_id and t2.parent_id = t1.id))",
            "a nullable component in the last position is still a plain equality on every component");
        sql.Should().NotContain("coalesce")
            .And.NotContain("<=>")
            .And.NotContain("is not distinct")
            .And.NotContain("is null", "null-safe equality would silently link an all-null key row");
    }

    [Fact]
    public void ExplicitNonKeyPrincipal_ShouldRenderEveryComponent()
    {
        RegisterExplicitPrincipal();
        using var ctx = CreateContext();

        var referenceSql = Canon(SqlOf(ctx, ctx.From<T251Item>().Select(i => (int?)i.Owner!.Id)));
        referenceSql.Should().Contain("left join t251_owner as t2 on (t1.tenant_id = t2.tenant_id and t1.owner_code = t2.code)",
            "the explicit non-PK principal is used verbatim and never replaced by the full primary key");

        var collectionSql = Canon(SqlOf(ctx, ctx.From<T251Owner>().Where(o => o.Items.Any()).ToCommand()));
        collectionSql.Should().Contain("t2.tenant_id = t1.tenant_id and t2.owner_code = t1.code",
            "the collection predicate pairs both explicit principal components");
    }

    [Fact]
    public void ReversedDeclaration_ShouldRenderComponentsInDeclaredOrder()
    {
        RegisterReversed();
        using var ctx = CreateContext();

        var referenceSql = Canon(SqlOf(ctx, ctx.From<T251ReversedChild>().Select(c => (int?)c.Parent!.Id)));
        referenceSql.Should().Contain("left join t251_reversed_parent as t2 on (t1.parent_id = t2.id and t1.tenant_id = t2.tenant_id)",
            "positional pairing renders the components in the declared (reversed) order");

        var collectionSql = Canon(SqlOf(ctx, ctx.From<T251ReversedParent>().Where(p => p.Children.Any()).ToCommand()));
        collectionSql.Should().Contain("t2.parent_id = t1.id and t2.tenant_id = t1.tenant_id",
            "the collection predicate keeps the declared component order");
        collectionSql.IndexOf("t2.parent_id = t1.id", StringComparison.Ordinal)
            .Should().BeLessThan(collectionSql.IndexOf("t2.tenant_id = t1.tenant_id", StringComparison.Ordinal),
                "a name-sorted or metadata-ordered pairing would render the tenant component first");
    }
}

[SqlTable("t251_blog")]
public sealed class T251Blog
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<T251Post> Posts { get; set; } = new List<T251Post>();
}

[SqlTable("t251_post")]
public sealed class T251Post
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("blog_id")]
    public int BlogId { get; set; }

    public T251Blog? Blog { get; set; }

    public ICollection<T251Comment> Comments { get; set; } = new List<T251Comment>();

    public ICollection<T251Tag> Tags { get; set; } = new List<T251Tag>();
}

[SqlTable("t251_comment")]
public sealed class T251Comment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("post_id")]
    public int PostId { get; set; }

    public T251Post? Post { get; set; }
}

[SqlTable("t251_tag")]
public sealed class T251Tag
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("t251_tag_link")]
public sealed class T251TagLink
{
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("post_id")]
    public int PostId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }
}

[SqlTable("t251_profile_parent")]
public sealed class T251ProfileParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    public T251Profile? Profile { get; set; }
}

[SqlTable("t251_profile")]
public sealed class T251Profile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }
}

[SqlTable("t251_owner")]
public sealed class T251Owner
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("code")]
    public int Code { get; set; }

    public ICollection<T251Item> Items { get; set; } = new List<T251Item>();
}

[SqlTable("t251_item")]
public sealed class T251Item
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("owner_code")]
    public int OwnerCode { get; set; }

    public T251Owner? Owner { get; set; }
}

[SqlTable("t251_nullable_first_parent")]
public sealed class T251NullableFirstParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<T251NullableFirstChild> Children { get; set; } = new List<T251NullableFirstChild>();
}

[SqlTable("t251_nullable_first_child")]
public sealed class T251NullableFirstChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int? TenantId { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("t251_nullable_last_parent")]
public sealed class T251NullableLastParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<T251NullableLastChild> Children { get; set; } = new List<T251NullableLastChild>();
}

[SqlTable("t251_nullable_last_child")]
public sealed class T251NullableLastChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }
}

[SqlTable("t251_reversed_parent")]
public sealed class T251ReversedParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<T251ReversedChild> Children { get; set; } = new List<T251ReversedChild>();
}

[SqlTable("t251_reversed_child")]
public sealed class T251ReversedChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    public T251ReversedParent? Parent { get; set; }
}
