using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T251 D251-03: composite relationships declared through the public <c>JoinInto</c> surface. Every
/// declaration used here is a public fluent one (<c>HasMany</c>/<c>HasOneToOne</c>/<c>HasManyThrough</c>
/// composite overloads, plus one hand-written <see cref="IRelationshipMetadata"/> for an explicit non-PK
/// principal). The stitched result is the oracle the existing composite suite uses: a row that matches
/// only one component must never be grouped or assigned, and swapping two components must change the
/// mapping rather than alias positionally.
/// </summary>
[Collection("Query cache controls")]
public class T251CompositeJoinIntoTests
{
    public T251CompositeJoinIntoTests()
    {
        DataContextCache.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures: composite key [TenantId, Id] on the parent side and two-component foreign keys.
    // ---------------------------------------------------------------------------------------------

    public sealed class Blog
    {
        public int TenantId { get; set; }

        public int Id { get; set; }

        public ICollection<Post> Posts { get; set; } = new List<Post>();

        public ICollection<Post> SwappedPosts { get; set; } = new List<Post>();

        public Profile? Profile { get; set; }

        public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    }

    public sealed class Post
    {
        public int TenantId { get; set; }

        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    public sealed class Profile
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int OwnerTenantId { get; set; }

        public int OwnerId { get; set; }
    }

    public sealed class Tag
    {
        public int TenantId { get; set; }

        public int Id { get; set; }
    }

    public sealed class BlogTag
    {
        public int BlogTenantId { get; set; }

        public int BlogId { get; set; }

        public int TagTenantId { get; set; }

        public int TagId { get; set; }
    }

    private static void RegisterBlog()
    {
        var blog = new EntityMetadataBuilder<Blog>();
        blog.Property(b => b.TenantId).Key();
        blog.Property(b => b.Id).Key();
        blog.HasMany(b => b.Posts, p => new { p.TenantId, p.ParentId })
            // The principal component order is swapped against the same foreign-key order: the pairing
            // is positional, so this is a different mapping and must not alias the correct one.
            .HasMany(b => b.SwappedPosts, p => new { p.TenantId, p.ParentId }, b => new { b.Id, b.TenantId })
            .HasOneToOne(b => b.Profile, b => new { b.TenantId, b.Id }, p => new { p.OwnerTenantId, p.OwnerId })
            .HasManyThrough<Tag, BlogTag>(
                b => b.Tags,
                b => new { b.TenantId, b.Id },
                l => new { l.BlogTenantId, l.BlogId },
                t => new { t.TenantId, t.Id },
                l => new { l.TagTenantId, l.TagId });
        DataContextCache.Metadata[typeof(Blog)] = blog.Build();

        var tag = new EntityMetadataBuilder<Tag>();
        tag.Property(t => t.TenantId).Key();
        tag.Property(t => t.Id).Key();
        DataContextCache.Metadata[typeof(Tag)] = tag.Build();
    }

    private static InMemoryDataContext CreateContext()
    {
        RegisterBlog();
        var ctx = new InMemoryDataContext();
        ctx.From<Blog>();
        ctx.From<Post>();
        ctx.From<Profile>();
        ctx.From<Tag>();
        ctx.From<BlogTag>();
        return ctx;
    }

    private static List<string> Shape(IEnumerable<Blog> blogs, Func<Blog, ICollection<Post>>? posts = null)
    {
        posts ??= b => b.Posts;
        return blogs.OrderBy(b => b.TenantId).ThenBy(b => b.Id)
            .Select(b => $"{b.TenantId}/{b.Id}:{string.Join(",", posts(b).Select(p => p.Id).OrderBy(id => id))}")
            .ToList();
    }

    private static List<string> TagShape(IEnumerable<Blog> blogs)
        => blogs.OrderBy(b => b.TenantId).ThenBy(b => b.Id)
            .Select(b => $"{b.TenantId}/{b.Id}:{string.Join(",", b.Tags.Select(t => t.Id).OrderBy(id => id))}")
            .ToList();

    // ---------------------------------------------------------------------------------------------
    // Direct one-to-many / one-to-one: every declared component participates in the group/assignment.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void DirectOneToMany_ShouldGroupByEveryDeclaredComponent()
    {
        using var ctx = CreateContext();
        ctx.From<Blog>().WithData(
        [
            new Blog { TenantId = 1, Id = 1 },
            new Blog { TenantId = 1, Id = 2 },
            new Blog { TenantId = 2, Id = 1 },
        ]);
        ctx.From<Post>().WithData(
        [
            new Post { TenantId = 1, Id = 10, ParentId = 1 },
            new Post { TenantId = 1, Id = 11, ParentId = 1 },
            // Same tenant, trailing component does not match parent (1,1).
            new Post { TenantId = 1, Id = 12, ParentId = 2 },
            // Same trailing component, first (tenant) component does not match parent (1,1).
            new Post { TenantId = 2, Id = 13, ParentId = 1 },
        ]);

        var blogs = ctx.From<Blog>()
            .JoinInto(ctx.From<Post>(), (b, p) => b.TenantId == p.TenantId && b.Id == p.ParentId, b => b.Posts)
            .ToList();

        blogs.Should().HaveCount(3);
        Shape(blogs).Should().Equal(
            "1/1:10,11",
            "1/2:12",
            "2/1:13");
    }

    [Fact]
    public void DirectOneToOne_ShouldAssignByEveryDeclaredComponent()
    {
        using var ctx = CreateContext();
        ctx.From<Blog>().WithData(
        [
            new Blog { TenantId = 1, Id = 1 },
            new Blog { TenantId = 2, Id = 1 },
        ]);
        ctx.From<Profile>().WithData(
        [
            new Profile { Id = 50, TenantId = 1, OwnerTenantId = 1, OwnerId = 1 },
            // Same owner id in another tenant: the first component must reject the cross row.
            new Profile { Id = 60, TenantId = 2, OwnerTenantId = 2, OwnerId = 1 },
        ]);

        var blogs = ctx.From<Blog>()
            .JoinInto(
                ctx.From<Profile>(),
                (b, p) => b.TenantId == p.OwnerTenantId && b.Id == p.OwnerId,
                b => b.Profile)
            .ToList();

        blogs.Should().HaveCount(2);
        blogs.Single(b => b.TenantId == 1).Profile!.Id.Should().Be(50);
        blogs.Single(b => b.TenantId == 2).Profile!.Id.Should().Be(60);
    }

    // ---------------------------------------------------------------------------------------------
    // Many-to-many through a composite junction: both legs, every component.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ManyToManyCompositeJunction_ShouldRouteBothLegsByEveryComponent()
    {
        using var ctx = CreateContext();
        ctx.From<Blog>().WithData(
        [
            new Blog { TenantId = 1, Id = 1 },
            new Blog { TenantId = 1, Id = 2 },
            new Blog { TenantId = 2, Id = 1 },
        ]);
        // The two tags share their first component (tenant 1); only the trailing component separates them.
        ctx.From<Tag>().WithData(
        [
            new Tag { TenantId = 1, Id = 10 },
            new Tag { TenantId = 1, Id = 20 },
        ]);
        ctx.From<BlogTag>().WithData(
        [
            new BlogTag { BlogTenantId = 1, BlogId = 1, TagTenantId = 1, TagId = 10 },
            new BlogTag { BlogTenantId = 1, BlogId = 2, TagTenantId = 1, TagId = 20 },
            // Same blog id as the first link, different tenant: only the parent's first component routes it.
            new BlogTag { BlogTenantId = 2, BlogId = 1, TagTenantId = 1, TagId = 20 },
        ]);

        var blogs = ctx.From<Blog>()
            .JoinInto(ctx.From<Tag>(), (b, t) => true, b => b.Tags)
            .ToList();

        blogs.Should().HaveCount(3);
        TagShape(blogs).Should().Equal(
            "1/1:10",
            "1/2:20",
            "2/1:20");
    }

    // ---------------------------------------------------------------------------------------------
    // Swapping two components changes the mapping and the result (no positional alias).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SwappedPrincipalComponents_ShouldChangeTheMappingAndTheResult()
    {
        using var ctx = CreateContext();
        ctx.From<Blog>().WithData([new Blog { TenantId = 7, Id = 2 }]);
        ctx.From<Post>().WithData([new Post { TenantId = 7, Id = 10, ParentId = 2 }]);

        // Same join predicate, same data; only the declared principal order differs between the two
        // declarations, so the correct mapping groups the row and the swapped one must not.
        var correct = ctx.From<Blog>()
            .JoinInto(ctx.From<Post>(), (b, p) => b.TenantId == p.TenantId && b.Id == p.ParentId, b => b.Posts)
            .ToList();
        var swapped = ctx.From<Blog>()
            .JoinInto(ctx.From<Post>(), (b, p) => b.TenantId == p.TenantId && b.Id == p.ParentId, b => b.SwappedPosts)
            .ToList();

        Shape(correct).Should().Equal(new[] { "7/2:10" }, "the declared [TenantId, Id] principal matches the row");
        Shape(swapped, b => b.SwappedPosts).Should().Equal(
            new[] { "7/2:" },
            "the swapped [Id, TenantId] principal is a different mapping and matches nothing");

        var correctCommand = ctx.From<Blog>()
            .JoinInto(ctx.From<Post>(), (b, p) => b.TenantId == p.TenantId && b.Id == p.ParentId, b => b.Posts)
            .ToCommand();
        var swappedCommand = ctx.From<Blog>()
            .JoinInto(ctx.From<Post>(), (b, p) => b.TenantId == p.TenantId && b.Id == p.ParentId, b => b.SwappedPosts)
            .ToCommand();
        correctCommand.PrepareCommand(TestContext.Current.CancellationToken);
        swappedCommand.PrepareCommand(TestContext.Current.CancellationToken);

        correctCommand.GetQueryPlanEqualityComparer().Equals(correctCommand, swappedCommand)
            .Should().BeFalse("the swapped mapping must not share the cached plan of the correct one");
    }

    // ---------------------------------------------------------------------------------------------
    // A row matching only the first component produces no link and no entity.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RowMatchingOnlyTheFirstComponent_ShouldProduceNoLinkOrEntity()
    {
        using var ctx = CreateContext();
        ctx.From<Blog>().WithData([new Blog { TenantId = 1, Id = 1 }]);
        ctx.From<Post>().WithData(
        [
            new Post { TenantId = 1, Id = 10, ParentId = 1 },
            // Shares the tenant only; the trailing parent component matches no blog.
            new Post { TenantId = 1, Id = 11, ParentId = 99 },
        ]);
        ctx.From<Profile>().WithData(
        [
            new Profile { Id = 50, TenantId = 1, OwnerTenantId = 1, OwnerId = 1 },
            // Shares the tenant only; the trailing owner component matches no blog.
            new Profile { Id = 51, TenantId = 1, OwnerTenantId = 1, OwnerId = 99 },
        ]);
        ctx.From<Tag>().WithData([new Tag { TenantId = 1, Id = 10 }]);
        ctx.From<BlogTag>().WithData(
        [
            new BlogTag { BlogTenantId = 1, BlogId = 1, TagTenantId = 1, TagId = 10 },
            // Shares the parent tenant only; the trailing parent component matches no blog.
            new BlogTag { BlogTenantId = 1, BlogId = 99, TagTenantId = 1, TagId = 10 },
        ]);

        var blogs = ctx.From<Blog>()
            .JoinInto(ctx.From<Post>(), (b, p) => b.TenantId == p.TenantId && b.Id == p.ParentId, b => b.Posts)
            .ToList();
        var referenced = ctx.From<Blog>()
            .JoinInto(
                ctx.From<Profile>(),
                (b, p) => b.TenantId == p.OwnerTenantId && b.Id == p.OwnerId,
                b => b.Profile)
            .ToList();
        var tagged = ctx.From<Blog>()
            .JoinInto(ctx.From<Tag>(), (b, t) => true, b => b.Tags)
            .ToList();

        Shape(blogs).Should().Equal(new[] { "1/1:10" }, "the tenant-only child must not be grouped");
        referenced.Should().ContainSingle();
        referenced[0].Profile!.Id.Should().Be(50, "the tenant-only profile must not be assigned");
        TagShape(tagged).Should().Equal(new[] { "1/1:10" }, "the tenant-only junction row must not produce a link");
    }

    // ---------------------------------------------------------------------------------------------
    // Manual metadata with an explicit non-PK principal.
    // ---------------------------------------------------------------------------------------------

    public sealed class Ledger
    {
        public int TenantId { get; set; }

        public int Id { get; set; }

        public int Code { get; set; }

        public ICollection<Entry> Entries { get; set; } = new List<Entry>();
    }

    public sealed class Entry
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int OwnerCode { get; set; }
    }

    private static PropertyInfo Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.Single(p => p.PropertyInfo.Name == name).PropertyInfo;

    [Fact]
    public void ManualMetadataWithExplicitNonKeyPrincipal_ShouldJoinOnEveryComponent()
    {
        var ledgerBuilder = new EntityMetadataBuilder<Ledger>();
        ledgerBuilder.Property(l => l.TenantId).Key();
        ledgerBuilder.Property(l => l.Id).Key();
        ledgerBuilder.Property(l => l.Code);
        var ledgerMetadata = ledgerBuilder.Build();
        var entryMetadata = new EntityMetadataBuilder<Entry>().Build();
        var navigation = typeof(Ledger).GetProperty(nameof(Ledger.Entries))!;

        var relationship = new RelationshipMetadata(
            RelationshipKind.OneToMany,
            typeof(Ledger),
            typeof(Entry),
            navigation,
            isCollection: true,
            foreignKeys: [Prop(entryMetadata.Properties, nameof(Entry.TenantId)), Prop(entryMetadata.Properties, nameof(Entry.OwnerCode))],
            principalKeys: [Prop(ledgerMetadata.Properties, nameof(Ledger.TenantId)), Prop(ledgerMetadata.Properties, nameof(Ledger.Code))],
            dependentType: typeof(Entry),
            principalType: typeof(Ledger));

        DataContextCache.Metadata[typeof(Ledger)] = new EntityMetadata(
            typeof(Ledger).Name,
            ledgerMetadata.Properties.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships: [relationship]);
        DataContextCache.Metadata[typeof(Entry)] = entryMetadata;

        relationship.PrincipalKey.Select(p => p.PropertyInfo.Name)
            .Should().ContainInOrder(nameof(Ledger.TenantId), nameof(Ledger.Code));
        relationship.PrincipalKey[1].IsKey.Should().BeFalse("the explicit principal component is a non-key mapped property");

        using var ctx = new InMemoryDataContext();
        ctx.From<Ledger>().WithData([new Ledger { TenantId = 1, Id = 1, Code = 7 }]);
        ctx.From<Entry>().WithData(
        [
            new Entry { Id = 10, TenantId = 1, OwnerCode = 7 },
            // Same code in another tenant: the first component must reject it.
            new Entry { Id = 11, TenantId = 2, OwnerCode = 7 },
            // Same tenant with another code: the trailing component must reject it.
            new Entry { Id = 12, TenantId = 1, OwnerCode = 8 },
        ]);

        var ledgers = ctx.From<Ledger>()
            .JoinInto(
                ctx.From<Entry>(),
                (l, e) => l.TenantId == e.TenantId && l.Code == e.OwnerCode,
                l => l.Entries)
            .ToList();

        ledgers.Should().ContainSingle();
        ledgers[0].Entries.Select(e => e.Id).Should().Equal(
            new[] { 10 },
            "the explicit (TenantId, Code) principal is paired component by component");
    }
}
