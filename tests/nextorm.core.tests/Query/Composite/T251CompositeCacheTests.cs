using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T251 D251-03: plan-cache identity and cache-safety for composite <c>JoinInto</c> declarations.
/// <para>
/// The identity rows hold every other part of the declaration constant and change exactly one piece of
/// the composite mapping (trailing component, principal override, junction mapping/order, navigation
/// occurrence), so a cached plan may only be shared when the whole mapping agrees. The safety rows pin
/// that a structurally invalid hand-written <see cref="IRelationshipMetadata"/> is rejected at
/// declaration (before any SQL) on every attempt, and that neither the rejection nor an uncached
/// (<c>storeInCache: false</c>) preparation flips the sticky <see cref="QueryCommand.Cache"/> flag of
/// the context-shared or neighbouring commands.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class T251CompositeCacheTests
{
    public T251CompositeCacheTests()
    {
        DataContextCache.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------------

    public sealed class CacheBlog
    {
        public int TenantId { get; set; }

        public int Id { get; set; }

        public int Code { get; set; }

        public ICollection<CachePost> Posts { get; set; } = new List<CachePost>();

        public ICollection<CachePost> SecondaryPosts { get; set; } = new List<CachePost>();

        public ICollection<CacheTag> Tags { get; set; } = new List<CacheTag>();
    }

    public sealed class CachePost
    {
        public int TenantId { get; set; }

        public int Id { get; set; }

        public int ParentId { get; set; }

        public int AltParentId { get; set; }

        public int ParentCode { get; set; }
    }

    public sealed class CacheTag
    {
        public int TenantId { get; set; }

        public int Id { get; set; }
    }

    public sealed class CacheBlogTag
    {
        public int BlogTenantId { get; set; }

        public int BlogId { get; set; }

        public int TagTenantId { get; set; }

        public int TagId { get; set; }
    }

    public sealed class CacheNeighbour
    {
        public int Id { get; set; }

        public int Value { get; set; }
    }

    public sealed class InvalidParent
    {
        public int TenantId { get; set; }

        public int Id { get; set; }

        public ICollection<InvalidChild> Children { get; set; } = new List<InvalidChild>();
    }

    public sealed class InvalidChild
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int ParentId { get; set; }
    }

    private static Expression<Func<CacheBlog, CachePost, bool>> Predicate =>
        (b, p) => b.TenantId == p.TenantId && b.Id == p.ParentId;

    /// <summary>
    /// Registers the composite mapping of <see cref="CacheBlog"/>. The optional selectors re-register the
    /// same <c>Posts</c>/<c>Tags</c> navigation with a different component mapping, so an identity row can
    /// change one piece of the mapping while the navigation occurrence, entity types and join kind stay
    /// identical.
    /// </summary>
    private static void RegisterBlog(
        Expression<Func<CachePost, object?>>? postsForeignKey = null,
        Expression<Func<CacheBlog, object?>>? postsPrincipalKey = null,
        Expression<Func<CacheBlogTag, object?>>? tagsParentForeignKey = null)
    {
        postsForeignKey ??= p => new { p.TenantId, p.ParentId };
        tagsParentForeignKey ??= l => new { l.BlogTenantId, l.BlogId };

        var blog = new EntityMetadataBuilder<CacheBlog>();
        blog.Property(b => b.TenantId).Key();
        blog.Property(b => b.Id).Key();
        blog.Property(b => b.Code);

        if (postsPrincipalKey is null)
            blog.HasMany(b => b.Posts, postsForeignKey);
        else
            blog.HasMany(b => b.Posts, postsForeignKey, postsPrincipalKey);

        blog.HasMany(b => b.SecondaryPosts, p => new { p.TenantId, p.ParentId })
            .HasManyThrough<CacheTag, CacheBlogTag>(
                b => b.Tags,
                b => new { b.TenantId, b.Id },
                tagsParentForeignKey,
                t => new { t.TenantId, t.Id },
                l => new { l.TagTenantId, l.TagId });

        DataContextCache.Metadata[typeof(CacheBlog)] = blog.Build();

        var tag = new EntityMetadataBuilder<CacheTag>();
        tag.Property(t => t.TenantId).Key();
        tag.Property(t => t.Id).Key();
        DataContextCache.Metadata[typeof(CacheTag)] = tag.Build();
    }

    private static QueryCommand<CacheBlog> DirectCommand(
        IDataContext ctx,
        Expression<Func<CacheBlog, ICollection<CachePost>>> collection)
    {
        var command = ctx.From<CacheBlog>()
            .JoinInto(ctx.From<CachePost>(), Predicate, collection)
            .ToCommand();
        command.PrepareCommand(TestContext.Current.CancellationToken);
        return command;
    }

    private static QueryCommand<CacheBlog> DeclaredManyToManyCommand(IDataContext ctx)
    {
        var command = ctx.From<CacheBlog>()
            .JoinInto(ctx.From<CacheTag>(), (b, t) => true, b => b.Tags)
            .ToCommand();
        command.PrepareCommand(TestContext.Current.CancellationToken);
        return command;
    }

    private static InMemoryDataContext CreateSeeded()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<CacheBlog>().WithData(
        [
            new CacheBlog { TenantId = 1, Id = 1, Code = 7 },
            new CacheBlog { TenantId = 1, Id = 2, Code = 8 },
        ]);
        ctx.From<CachePost>().WithData(
        [
            new CachePost { TenantId = 1, Id = 10, ParentId = 1, ParentCode = 7 },
            new CachePost { TenantId = 1, Id = 11, ParentId = 1, ParentCode = 8 },
            new CachePost { TenantId = 1, Id = 12, ParentId = 2, ParentCode = 8 },
            // Same parent id in another tenant: only the first component rejects it.
            new CachePost { TenantId = 2, Id = 13, ParentId = 1, ParentCode = 7 },
        ]);
        return ctx;
    }

    private static List<string> Shape(IEnumerable<CacheBlog> blogs)
        => blogs.OrderBy(b => b.TenantId).ThenBy(b => b.Id)
            .Select(b => $"{b.TenantId}/{b.Id}:{string.Join(",", b.Posts.Select(p => p.Id).OrderBy(id => id))}")
            .ToList();

    // ---------------------------------------------------------------------------------------------
    // Cold / warm / prepared: one composite plan, one result.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ColdWarmAndPrepared_ShouldShareOneCompositePlanAndOneResult()
    {
        RegisterBlog();
        using var ctx = CreateSeeded();

        var cold = Shape(ctx.From<CacheBlog>()
            .JoinInto(ctx.From<CachePost>(), Predicate, b => b.Posts)
            .ToList());
        var warm = Shape(ctx.From<CacheBlog>()
            .JoinInto(ctx.From<CachePost>(), Predicate, b => b.Posts)
            .ToList());

        var coldCommand = DirectCommand(ctx, b => b.Posts);
        var preparedCommand = ctx.From<CacheBlog>()
            .JoinInto(ctx.From<CachePost>(), Predicate, b => b.Posts)
            .ToCommand();
        preparedCommand.PrepareCommand(TestContext.Current.CancellationToken);

        var comparer = coldCommand.GetQueryPlanEqualityComparer();
        comparer.Equals(coldCommand, preparedCommand).Should().BeTrue(
            "the same composite declaration must key one plan whether it was built cold or explicitly prepared");

        var prepared = Shape(ctx.From<CacheBlog>()
            .JoinInto(ctx.From<CachePost>(), Predicate, b => b.Posts)
            .ToList());

        cold.Should().NotBeEmpty();
        cold.Should().Equal(warm, "a warm execution must return the cold result");
        cold.Should().Equal(prepared, "an explicitly prepared execution must return the cold result");
    }

    // ---------------------------------------------------------------------------------------------
    // Identity: the full mapping and the occurrence scope are part of the plan key.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TrailingComponent_ShouldChangeThePlanIdentity()
    {
        // Both registrations declare the same parent type, the same navigation member and the same
        // join predicate; only the trailing foreign-key component differs.
        RegisterBlog(postsForeignKey: p => new { p.TenantId, p.ParentId });
        using var byParentId = new InMemoryDataContext();
        var byParentIdCommand = DirectCommand(byParentId, b => b.Posts);

        DataContextCache.Clear();
        RegisterBlog(postsForeignKey: p => new { p.TenantId, p.AltParentId });
        using var byAltParentId = new InMemoryDataContext();
        var byAltParentIdCommand = DirectCommand(byAltParentId, b => b.Posts);

        var comparer = byParentIdCommand.GetQueryPlanEqualityComparer();
        comparer.Equals(byParentIdCommand, byAltParentIdCommand).Should().BeFalse(
            "the trailing key component is part of the composite plan identity, even though the rendered predicate is the same");
    }

    [Fact]
    public void PrincipalOverride_ShouldChangeThePlanIdentity()
    {
        RegisterBlog(postsPrincipalKey: b => new { b.TenantId, b.Id });
        using var byId = new InMemoryDataContext();
        var byIdCommand = DirectCommand(byId, b => b.Posts);

        DataContextCache.Clear();
        RegisterBlog(postsPrincipalKey: b => new { b.TenantId, b.Code });
        using var byCode = new InMemoryDataContext();
        var byCodeCommand = DirectCommand(byCode, b => b.Posts);

        var comparer = byIdCommand.GetQueryPlanEqualityComparer();
        comparer.Equals(byIdCommand, byCodeCommand).Should().BeFalse(
            "an explicit non-key principal override is a different mapping and must not share the primary-key plan");
    }

    [Fact]
    public void JunctionMapping_ShouldChangeThePlanIdentity()
    {
        RegisterBlog();
        using var ctx = new InMemoryDataContext();

        var byBlogId = ManyToManyCommand(ctx, l => l.BlogId);
        var byBlogTenantId = ManyToManyCommand(ctx, l => l.BlogTenantId);

        var comparer = byBlogId.GetQueryPlanEqualityComparer();
        comparer.Equals(byBlogId, byBlogTenantId).Should().BeFalse(
            "the junction parent foreign-key mapping is part of the plan identity");

        var repeat = ManyToManyCommand(ctx, l => l.BlogId);
        comparer.Equals(byBlogId, repeat).Should().BeTrue(
            "the same local junction mapping must still reuse the cached plan");
    }

    [Fact]
    public void JunctionComponentOrder_ShouldChangeThePlanIdentity()
    {
        RegisterBlog(tagsParentForeignKey: l => new { l.BlogTenantId, l.BlogId });
        using var normal = new InMemoryDataContext();
        var normalCommand = DeclaredManyToManyCommand(normal);

        DataContextCache.Clear();
        RegisterBlog(tagsParentForeignKey: l => new { l.BlogId, l.BlogTenantId });
        using var swapped = new InMemoryDataContext();
        var swappedCommand = DeclaredManyToManyCommand(swapped);

        var comparer = normalCommand.GetQueryPlanEqualityComparer();
        comparer.Equals(normalCommand, swappedCommand).Should().BeFalse(
            "the junction component order is part of the plan identity; a positional alias would route rows to the wrong parent");
    }

    [Fact]
    public void ReRegistrationWithTheSameMapping_ShouldReuseThePlan()
    {
        // Control for the re-registration identity rows: replacing the process-wide metadata with an
        // identical composite mapping must not by itself make two declarations unequal.
        RegisterBlog(postsForeignKey: p => new { p.TenantId, p.ParentId });
        using var first = new InMemoryDataContext();
        var firstCommand = DirectCommand(first, b => b.Posts);

        DataContextCache.Clear();
        RegisterBlog(postsForeignKey: p => new { p.TenantId, p.ParentId });
        using var second = new InMemoryDataContext();
        var secondCommand = DirectCommand(second, b => b.Posts);

        var comparer = firstCommand.GetQueryPlanEqualityComparer();
        comparer.Equals(firstCommand, secondCommand).Should().BeTrue(
            "the same composite mapping re-registered must still key one plan");
    }

    [Fact]
    public void NavigationOccurrence_ShouldChangeThePlanIdentity()
    {
        RegisterBlog();
        using var ctx = new InMemoryDataContext();

        var posts = DirectCommand(ctx, b => b.Posts);
        var secondaryPosts = DirectCommand(ctx, b => b.SecondaryPosts);
        var repeat = DirectCommand(ctx, b => b.Posts);

        var comparer = posts.GetQueryPlanEqualityComparer();
        comparer.Equals(posts, secondaryPosts).Should().BeFalse(
            "two navigation occurrences of the same mapping must not share a cached plan");
        comparer.Equals(posts, repeat).Should().BeTrue(
            "the same declaration must reuse the cached plan");
    }

    private static QueryCommand<CacheBlog> ManyToManyCommand(
        IDataContext ctx,
        Expression<Func<CacheBlogTag, int>> junctionParentForeignKey)
    {
        // A local relationship replaces the declared one for this call, so the navigation member, the
        // entity types and the join kind stay constant while only the junction mapping changes.
        var command = ctx.From<CacheBlog>()
            .JoinInto(
                ctx.From<CacheTag>(),
                (b, t) => true,
                b => b.Tags,
                j => j.ManyToMany<CacheBlog, CacheTag, CacheBlogTag, int, int>(
                    b => b.Id,
                    junctionParentForeignKey,
                    t => t.Id,
                    l => l.TagId))
            .ToCommand();
        command.PrepareCommand(TestContext.Current.CancellationToken);
        return command;
    }

    // ---------------------------------------------------------------------------------------------
    // Error-not-cached: a structurally invalid manual relationship is rejected on every attempt.
    // ---------------------------------------------------------------------------------------------

    private static void RegisterInvalidParent()
    {
        var parentMetadata = new EntityMetadataBuilder<InvalidParent>().Build();
        var childMetadata = new EntityMetadataBuilder<InvalidChild>().Build();
        var navigation = typeof(InvalidParent).GetProperty(nameof(InvalidParent.Children))!;

        DataContextCache.Metadata[typeof(InvalidParent)] = new EntityMetadata(
            typeof(InvalidParent).Name,
            parentMetadata.Properties.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                // Two foreign-key components against one principal-key component: structurally invalid,
                // and the hand-written instance carries no lazy guard of its own.
                new RelationshipMetadata(
                    RelationshipKind.OneToMany,
                    typeof(InvalidParent),
                    typeof(InvalidChild),
                    navigation,
                    isCollection: true,
                    foreignKeys:
                    [
                        Prop(childMetadata.Properties, nameof(InvalidChild.TenantId)),
                        Prop(childMetadata.Properties, nameof(InvalidChild.ParentId)),
                    ],
                    principalKeys: [Prop(parentMetadata.Properties, nameof(InvalidParent.Id))],
                    dependentType: typeof(InvalidChild),
                    principalType: typeof(InvalidParent)),
            ]);
        DataContextCache.Metadata[typeof(InvalidChild)] = childMetadata;
    }

    private static PropertyInfo Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.Single(p => p.PropertyInfo.Name == name).PropertyInfo;

    private static void DeclareInvalid(InMemoryDataContext ctx)
        => ctx.From<InvalidParent>()
            .JoinInto(
                ctx.From<InvalidChild>(),
                (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId,
                p => p.Children)
            .ToCommand();

    [Fact]
    public void StructurallyInvalidManualRelationship_ShouldThrowBeforeSql_AndRepeatOnSecondAttempt()
    {
        RegisterInvalidParent();
        using var ctx = new InMemoryDataContext();
        ctx.From<InvalidParent>();
        ctx.From<InvalidChild>();

        // The rejection happens while the declaration is built, i.e. before any command exists, so no
        // SQL can have been rendered or executed.
        Action first = () => DeclareInvalid(ctx);
        first.Should().Throw<NotSupportedException>()
            .WithMessage("*2 foreign-key component(s) but 1 principal-key component(s)*");

        Action second = () => DeclareInvalid(ctx);
        second.Should().Throw<NotSupportedException>(
            "a rejected mapping must be re-evaluated, never served from a cached plan");

        ctx.CommandIndex.Should().BeEmpty("nothing may be cached before the declaration was rejected");
    }

    // ---------------------------------------------------------------------------------------------
    // Sticky-cache guard.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RejectedDeclaration_ShouldNotFlipTheSharedCacheFlags_AndNeighboursStillCache()
    {
        RegisterInvalidParent();
        using var ctx = new InMemoryDataContext();
        ctx.From<InvalidParent>();
        ctx.From<InvalidChild>();
        ctx.From<CacheNeighbour>().WithData([new CacheNeighbour { Id = 1, Value = 1 }]);

        ctx.From<CacheNeighbour>().Any().Should().BeTrue();
        var anyCommand = ctx.AnyCommand!.Value;
        anyCommand.Cache.Should().BeTrue("the shared AnyCommand starts with the default cache policy");

        Action first = () => DeclareInvalid(ctx);
        first.Should().Throw<NotSupportedException>();
        Action second = () => DeclareInvalid(ctx);
        second.Should().Throw<NotSupportedException>();

        anyCommand.Cache.Should().BeTrue("a rejected declaration must not flip the shared QueryCommand.Cache flag");
        ctx.From<CacheNeighbour>().Any().Should().BeTrue();

        var neighbour = ctx.From<CacheNeighbour>().Where(n => n.Value > 0).ToCommand();
        var prepared = ctx.GetPreparedQueryCommand(neighbour, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        var repeat = ctx.GetPreparedQueryCommand(neighbour, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        repeat.Should().BeSameAs(prepared, "a neighbouring query on the same context still caches after the rejection");
        neighbour.Cache.Should().BeTrue();
    }

    [Fact]
    public void UncachedCompositeJoinIntoPreparation_ShouldNotFlipTheStickyCache_AndLaterPreparationCaches()
    {
        RegisterBlog();
        // The SQL path prepares a JoinInto command with its joins (the in-memory provider only builds a
        // joined enumerator for the pair projection, not for the parent command).
        using var ctx = new RootProjectionTests.TestContext();

        var command = ctx.From<CacheBlog>()
            .JoinInto(ctx.From<CachePost>(), Predicate, b => b.Posts)
            .ToCommand();
        command.Cache.Should().BeTrue();

        // The call-local storeInCache flag must be the only reason this preparation skips the cache.
        ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);

        command.Cache.Should().BeTrue(
            "storeInCache: false is call-local and must never mutate the sticky QueryCommand.Cache flag");

        var prepared = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        var repeat = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        repeat.Should().BeSameAs(prepared, "a later cached preparation still stores the composite plan after an uncached one");
        command.Cache.Should().BeTrue();
    }

    [Fact]
    public void UncachedPreparation_ShouldNotDisableTheContextCache_AndAnyStillCaches()
    {
        RegisterBlog();
        using var ctx = CreateSeeded();

        ctx.From<CacheBlog>().Any().Should().BeTrue();
        var anyCommand = ctx.AnyCommand!.Value;
        anyCommand.Cache.Should().BeTrue();

        var command = ctx.From<CacheBlog>().Where(b => b.TenantId == 1).ToCommand();
        ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);

        command.Cache.Should().BeTrue("the call-local flag is not the sticky command policy");
        anyCommand.Cache.Should().BeTrue("the shared AnyCommand policy is untouched");
        ctx.From<CacheBlog>().Any().Should().BeTrue();

        var prepared = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        var repeat = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        repeat.Should().BeSameAs(prepared, "a neighbouring query still caches after an uncached preparation");
    }
}
