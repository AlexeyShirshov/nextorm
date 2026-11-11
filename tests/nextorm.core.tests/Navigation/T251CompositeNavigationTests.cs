using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T251 composite navigation: reference, multi-hop, collection and correlated navigation over
/// two-component relationship keys. The SQL-shape rows assert one equality per component in declaration
/// order (never a first-component-only join and never a null-safe operator). The in-memory rows assert the
/// <em>preserved</em> pre-composite behaviour: composite declaration/metadata is provider-neutral, but the
/// in-memory provider still refuses composite-key navigation with its existing typed error and gains no
/// component-wise parity (AC251-18).
/// </summary>
[Collection("Query cache controls")]
public class T251CompositeNavigationTests
{
    public T251CompositeNavigationTests()
    {
        DataContextCache.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------------

    [SqlTable("t251_blog")]
    public sealed class Blog
    {
        [Key]
        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<Post> Posts { get; set; } = new List<Post>();
    }

    [SqlTable("t251_post")]
    public sealed class Post
    {
        [Key]
        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("blog_id")]
        public int BlogId { get; set; }

        public Blog? Blog { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }

    [SqlTable("t251_comment")]
    public sealed class Comment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("post_id")]
        public int PostId { get; set; }

        public Post? Post { get; set; }
    }

    [SqlTable("t251_owner")]
    public sealed class Owner
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("code")]
        public int Code { get; set; }

        public ICollection<Item> Items { get; set; } = new List<Item>();
    }

    [SqlTable("t251_item")]
    public sealed class Item
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("owner_code")]
        public int OwnerCode { get; set; }

        public Owner? Owner { get; set; }
    }

    [SqlTable("t251_nullable_parent")]
    public sealed class NullableParent
    {
        [Key]
        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<NullableChild> Children { get; set; } = new List<NullableChild>();
    }

    [SqlTable("t251_nullable_child")]
    public sealed class NullableChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int? TenantId { get; set; }

        [Column("parent_id")]
        public int? ParentId { get; set; }
    }

    // Single-column nullable foreign key: the legacy (pre-composite) shape whose reference navigation
    // used to throw Expression.Equal(int?, int) instead of resolving the null FK to "no entity".
    [SqlTable("t251_single_parent")]
    public sealed class SingleParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<SingleChild> Children { get; set; } = new List<SingleChild>();
    }

    [SqlTable("t251_single_child")]
    public sealed class SingleChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int? ParentId { get; set; }

        public SingleParent? Parent { get; set; }
    }

    // Composite foreign key with exactly one nullable component.
    [SqlTable("t251_partial_parent")]
    public sealed class PartialParent
    {
        [Key]
        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<PartialChild> Children { get; set; } = new List<PartialChild>();
    }

    [SqlTable("t251_partial_child")]
    public sealed class PartialChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("parent_id")]
        public int? ParentId { get; set; }

        public PartialParent? Parent { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // SQL shape
    // ---------------------------------------------------------------------------------------------

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    private static string Canon(string sql)
        => Regex.Replace(
                sql.Replace("\r\n", "\n").Replace("\"", string.Empty).Replace("[", string.Empty).Replace("]", string.Empty).Replace("'", string.Empty),
                @"\s+",
                " ")
            .Trim()
            .ToLowerInvariant();

    private static void RegisterBlogAndPost()
    {
        var blog = new EntityMetadataBuilder<Blog>();
        blog.HasMany(b => b.Posts, p => new { p.TenantId, p.BlogId });
        DataContextCache.Metadata[typeof(Blog)] = blog.Build();

        var post = new EntityMetadataBuilder<Post>();
        post.HasOne(p => p.Blog, p => new { p.TenantId, p.BlogId })
            .HasMany(p => p.Comments, c => new { c.TenantId, c.PostId });
        DataContextCache.Metadata[typeof(Post)] = post.Build();

        var comment = new EntityMetadataBuilder<Comment>();
        comment.HasOne(c => c.Post, c => new { c.TenantId, c.PostId });
        DataContextCache.Metadata[typeof(Comment)] = comment.Build();
    }

    [Fact]
    public void ReferenceNavigation_ShouldRenderOnePredicatePerComponentInOrder()
    {
        RegisterBlogAndPost();
        using var ctx = new RootProjectionTests.TestContext();

        var sql = Canon(SqlOf(ctx, ctx.From<Post>().Select(p => p.Blog!.Name)));

        sql.Should().Contain(
            "left join t251_blog as t2 on (t1.tenant_id = t2.tenant_id and t1.blog_id = t2.id)");
        sql.IndexOf("t1.tenant_id = t2.tenant_id", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("t1.blog_id = t2.id", StringComparison.Ordinal),
                "the join predicates keep the declared component order");
    }

    [Fact]
    public void MultiHopReferenceNavigation_ShouldUseEveryComponentOnEveryHop()
    {
        RegisterBlogAndPost();
        using var ctx = new RootProjectionTests.TestContext();

        var sql = Canon(SqlOf(ctx, ctx.From<Comment>().Select(c => c.Post!.Blog!.Name)));

        sql.Should().Contain("t1.tenant_id = t2.tenant_id and t1.post_id = t2.id",
            "the comment -> post hop pairs both components");
        sql.Should().Contain("t2.tenant_id = t3.tenant_id and t2.blog_id = t3.id",
            "the post -> blog hop pairs both components");
    }

    [Fact]
    public void CollectionPredicate_ShouldUseEveryComponent()
    {
        RegisterBlogAndPost();
        using var ctx = new RootProjectionTests.TestContext();

        var sql = Canon(SqlOf(ctx, ctx.From<Blog>().Where(b => b.Posts.Any()).ToCommand()));

        sql.Should().Contain("t2.tenant_id = t1.tenant_id and t2.blog_id = t1.id",
            "the collection predicate matches every component of the parent key");
        sql.Should().NotContain("t2.blog_id = t1.id) or", "a first-component-only fallback is not acceptable");
    }

    [Fact]
    public void CorrelatedReferenceAndCollection_ShouldUseEveryComponent()
    {
        RegisterBlogAndPost();
        using var ctx = new RootProjectionTests.TestContext();

        var sql = Canon(SqlOf(ctx, ctx.From<Post>().Where(p => p.Blog!.Posts.Any()).ToCommand()));

        sql.Should().Contain("t1.tenant_id = t2.tenant_id and t1.blog_id = t2.id",
            "the correlated reference uses both components");
        sql.Should().Contain("t3.tenant_id = t2.tenant_id and t3.blog_id = t2.id",
            "the correlated collection uses both components");
    }

    [Fact]
    public void NullableForeignKeyComponents_ShouldNotRenderNullSafeEquality()
    {
        var parent = new EntityMetadataBuilder<NullableParent>();
        parent.HasMany(p => p.Children, c => new { c.TenantId, c.ParentId });
        DataContextCache.Metadata[typeof(NullableParent)] = parent.Build();
        DataContextCache.Metadata[typeof(NullableChild)] = new EntityMetadataBuilder<NullableChild>().Build();

        using var ctx = new RootProjectionTests.TestContext();

        var sql = Canon(SqlOf(ctx, ctx.From<NullableParent>().Where(p => p.Children.Any()).ToCommand()));

        sql.Should().Contain("t2.tenant_id = t1.tenant_id and t2.parent_id = t1.id",
            "a null component is still a plain equality on every component");
        sql.Should().NotContain("coalesce")
            .And.NotContain("<=>")
            .And.NotContain("is not distinct")
            .And.NotContain("is null", "null-safe equality would silently link an all-null key row");
    }

    [Fact]
    public void SingleColumnNullableReference_ShouldRenderPlainEquality()
    {
        // The pre-fix expression tree threw InvalidOperationException here (Expression.Equal(int?, int)).
        var single = new EntityMetadataBuilder<SingleParent>();
        single.HasMany(p => p.Children, c => new { c.ParentId });
        DataContextCache.Metadata[typeof(SingleParent)] = single.Build();

        var child = new EntityMetadataBuilder<SingleChild>();
        child.HasOne(c => c.Parent, c => new { c.ParentId });
        DataContextCache.Metadata[typeof(SingleChild)] = child.Build();

        using var ctx = new RootProjectionTests.TestContext();

        var sql = Canon(SqlOf(ctx, ctx.From<SingleChild>().Select(c => c.Parent!.Name)));

        sql.Should().Contain("left join t251_single_parent as t2 on t1.parent_id = t2.id",
            "a nullable FK stays a plain equality and resolves to no entity on NULL");
        sql.Should().NotContain("coalesce")
            .And.NotContain("is null", "a null-safe comparison would link an all-null key row");
    }

    [Fact]
    public void CompositeReferenceWithOneNullableComponent_ShouldRenderPlainEquality()
    {
        var parent = new EntityMetadataBuilder<PartialParent>();
        parent.HasMany(p => p.Children, c => new { c.TenantId, c.ParentId });
        DataContextCache.Metadata[typeof(PartialParent)] = parent.Build();

        var child = new EntityMetadataBuilder<PartialChild>();
        child.HasOne(c => c.Parent, c => new { c.TenantId, c.ParentId });
        DataContextCache.Metadata[typeof(PartialChild)] = child.Build();

        using var ctx = new RootProjectionTests.TestContext();

        var sql = Canon(SqlOf(ctx, ctx.From<PartialChild>().Select(c => (int?)c.Parent!.Id)));

        sql.Should().Contain("left join t251_partial_parent as t2 on (t1.tenant_id = t2.tenant_id and t1.parent_id = t2.id)",
            "the non-nullable component is compared directly and the nullable one through the lifted equality");
    }

    [Fact]
    public void ExplicitNonKeyPrincipal_ShouldRenderEveryComponent()
    {
        var owner = new EntityMetadataBuilder<Owner>();
        owner.HasMany(o => o.Items, i => new { i.TenantId, i.OwnerCode }, o => new { o.TenantId, o.Code });
        DataContextCache.Metadata[typeof(Owner)] = owner.Build();
        DataContextCache.Metadata[typeof(Item)] = new EntityMetadataBuilder<Item>().Build();

        using var ctx = new RootProjectionTests.TestContext();

        var sql = Canon(SqlOf(ctx, ctx.From<Owner>().Where(o => o.Items.Any()).ToCommand()));

        sql.Should().Contain("t2.tenant_id = t1.tenant_id and t2.owner_code = t1.code",
            "an explicit non-PK principal is used verbatim and never replaced by the full primary key");
    }

    // ---------------------------------------------------------------------------------------------
    // In-memory behaviour
    // ---------------------------------------------------------------------------------------------

    private static InMemoryDataContext CreateInMemory()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<Blog>(b =>
        {
            b.HasMany(x => x.Posts, c => new { c.TenantId, c.BlogId });
        });
        ctx.From<Post>(b =>
        {
            b.HasOne(p => p.Blog, p => new { p.TenantId, p.BlogId })
                .HasMany(p => p.Comments, c => new { c.TenantId, c.PostId });
        });
        ctx.From<Comment>(b => b.HasOne(c => c.Post, c => new { c.TenantId, c.PostId }));
        ctx.From<Owner>(b =>
        {
            b.HasMany(o => o.Items, i => new { i.TenantId, i.OwnerCode }, o => new { o.TenantId, o.Code });
        });
        ctx.From<Item>(b => b.HasOne(i => i.Owner, i => new { i.TenantId, i.OwnerCode }, o => new { o.TenantId, o.Code }));
        ctx.From<NullableParent>(b =>
        {
            b.HasMany(x => x.Children, c => new { c.TenantId, c.ParentId });
        });
        ctx.From<NullableChild>();
        ctx.From<SingleChild>(b => b.HasOne(c => c.Parent, c => new { c.ParentId }));
        ctx.From<SingleParent>();
        ctx.From<PartialChild>(b => b.HasOne(c => c.Parent, c => new { c.TenantId, c.ParentId }));
        ctx.From<PartialParent>(b =>
        {
            b.HasMany(x => x.Children, c => new { c.TenantId, c.ParentId });
        });
        return ctx;
    }

    private static InMemoryDataContext CreateSeeded()
    {
        var ctx = CreateInMemory();
        ctx.From<Blog>().WithData(
        [
            new Blog { TenantId = 1, Id = 1, Name = "tenant-1" },
            new Blog { TenantId = 2, Id = 1, Name = "tenant-2" },
        ]);
        ctx.From<Post>().WithData(
        [
            new Post { TenantId = 1, Id = 10, BlogId = 1 },
            new Post { TenantId = 2, Id = 10, BlogId = 1 },
            new Post { TenantId = 1, Id = 11, BlogId = 2 },
        ]);
        ctx.From<Comment>().WithData(
        [
            new Comment { Id = 100, TenantId = 1, PostId = 10 },
            new Comment { Id = 101, TenantId = 2, PostId = 10 },
            new Comment { Id = 102, TenantId = 1, PostId = 99 },
        ]);
        return ctx;
    }

    // AC251-18: declaration and metadata are provider-neutral, but the in-memory provider keeps its
    // pre-composite divergence. A composite navigation is still refused on the in-memory path with the
    // same typed error (it is neither matched component-wise nor rejected at metadata resolution), so
    // the SQL rows above are the only place composite navigation behaviour is asserted.

    [Fact]
    public void CompositeReferenceNavigation_ShouldStillBeRefusedInMemory()
    {
        using var ctx = CreateSeeded();

        Action act = () => ctx.From<Post>()
            .Select(p => new { p.TenantId, p.Id, BlogName = p.Blog!.Name })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*composite key*",
            "the in-memory provider never matched a composite reference component-wise");
    }

    [Fact]
    public void CompositeMultiHopReference_ShouldStillBeRefusedInMemory()
    {
        using var ctx = CreateSeeded();

        Action act = () => ctx.From<Comment>()
            .Select(c => new { c.Id, BlogName = c.Post!.Blog!.Name })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*composite key*");
    }

    [Fact]
    public void CompositeCollectionPredicate_ShouldStillBeRefusedInMemory()
    {
        using var ctx = CreateInMemory();
        ctx.From<NullableParent>().WithData([new NullableParent { TenantId = 1, Id = 1 }]);
        ctx.From<NullableChild>().WithData(
        [
            new NullableChild { Id = 10, TenantId = null, ParentId = 1 },
            new NullableChild { Id = 11, TenantId = 1, ParentId = null },
        ]);

        Action act = () => ctx.From<NullableParent>()
            .Select(p => p.Children.Any())
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*composite key*");
    }

    [Fact]
    public void SingleColumnNullableReference_ShouldYieldNullForNullForeignKeyInMemory()
    {
        using var ctx = CreateInMemory();
        ctx.From<SingleParent>().WithData([new SingleParent { Id = 1, Name = "parent" }]);
        ctx.From<SingleChild>().WithData(
        [
            new SingleChild { Id = 10, ParentId = null },
            new SingleChild { Id = 11, ParentId = 1 },
            new SingleChild { Id = 12, ParentId = 99 },
        ]);

        var rows = ctx.From<SingleChild>()
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, ParentId = (int?)c.Parent!.Id })
            .ToList();

        rows.Select(r => r.ParentId).Should().Equal(new int?[] { null, 1, null },
            "a null or dangling single-column foreign key resolves to no entity instead of throwing");
    }

    [Fact]
    public void DefaultFullPrimaryKey_ShouldBeTheCompositePrincipal()
    {
        var blog = new EntityMetadataBuilder<Blog>();
        blog.HasMany(b => b.Posts, p => new { p.TenantId, p.BlogId });

        var relationship = blog.Build().Relationships.Should().ContainSingle().Subject;

        relationship.PrincipalKey.Select(key => key.PropertyInfo.Name).Should().ContainInOrder(
            nameof(Blog.TenantId), nameof(Blog.Id));
        relationship.PrincipalKey.Should().OnlyContain(key => key.IsKey);
    }
}
