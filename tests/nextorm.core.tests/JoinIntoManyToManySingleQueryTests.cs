using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Coverage for the single-query (<c>EagerLoadMode.SingleQuery</c>) interaction with a many-to-many <c>JoinInto</c>
/// (#135): the many-to-many declaration is rejected before the join/spec pairing runs, a plain
/// <c>JoinInto</c> combined with a single-query <c>LoadWith</c> exercises the pairing path, and an
/// explicit junction mapping drops the column names resolved against the auto-published one.
/// </summary>
public class JoinIntoManyToManySingleQueryTests
{
    public JoinIntoManyToManySingleQueryTests()
    {
        DataContextCache.Clear();
    }

    private static InMemoryDataContext CreateManyToManyContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<ManyToManyJoinParent>(b => b
                .HasManyThrough<ManyToManyJoinChild, ManyToManyJoinLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId))
            .WithData([new ManyToManyJoinParent { Id = 1, Name = "p1" }]);
        ctx.From<ManyToManyJoinChild>().WithData([new ManyToManyJoinChild { Id = 10, Name = "c" }]);
        ctx.From<ManyToManyJoinLink>().WithData([new ManyToManyJoinLink { ParentId = 1, ChildId = 10 }]);
        return ctx;
    }

    [Fact]
    public void ManyToManyJoinInto_WithLoadWithAndSingleQuery_ShouldThrow()
    {
        using var ctx = CreateManyToManyContext();

        // A single-query mapping assumes one join per declaration, but a many-to-many declaration
        // contributes a link edge plus a child edge, so it is rejected before any index pairing.
        Action act = () => ctx.From<ManyToManyJoinParent>()
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Children, c => c.From<ManyToManyJoinChild>(), p => p.Id, c => c.Id, EagerLoadMode.SingleQuery)
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*many-to-many*")
            .WithMessage("*SingleQuery*");
    }

    public sealed class SingleQueryMixedParent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ICollection<SingleQueryMixedChild> Children { get; set; } = new List<SingleQueryMixedChild>();
        public ICollection<SingleQueryMixedNote> Notes { get; set; } = new List<SingleQueryMixedNote>();
    }

    public sealed class SingleQueryMixedChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class SingleQueryMixedNote
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string? Text { get; set; }
    }

    [Fact]
    public void PlainJoinInto_WithLoadWithAndSingleQuery_ShouldStitchBothCollections()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SingleQueryMixedParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId))
            .WithData([new SingleQueryMixedParent { Id = 1, Name = "p1" }]);
        ctx.From<SingleQueryMixedChild>().WithData([new SingleQueryMixedChild { Id = 10, ParentId = 1, Name = "c" }]);
        ctx.From<SingleQueryMixedNote>().WithData([new SingleQueryMixedNote { Id = 20, ParentId = 1, Text = "n" }]);

        // The pre-existing JoinInto declaration and the LoadWith declaration are paired in order: the
        // one-to-one-slot declaration consumes the child edge that carries its child filter scope. The
        // child ignores every filter so the non-empty scope branch of the pairing is exercised.
        var parents = ctx.From<SingleQueryMixedParent>()
            .JoinInto(ctx.From<SingleQueryMixedChild>().IgnoreFilters(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<SingleQueryMixedNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10);
        parents[0].Notes.Select(n => n.Id).Should().Equal(20);
    }

    [Fact]
    public void ConfiguredJunctionOverride_ShouldEvictAutoPublishedColumnNames()
    {
        // The junction is deliberately not registered explicitly, so declaring the many-to-many JoinInto
        // reaches the auto-resolution path and marks the junction as auto-published.
        using var ctx = new InMemoryDataContext();
        ctx.From<ManyToManyJoinParent>(b => b
            .HasManyThrough<ManyToManyJoinChild, ManyToManyJoinLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
        ctx.From<ManyToManyJoinChild>();

        // Declaring the many-to-many JoinInto auto-publishes the junction mapping so the derived link
        // source can read it.
        _ = ctx.From<ManyToManyJoinParent>()
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children);

        DataContextCache.AutoPublishedJunctionMetadata.Should().ContainKey(typeof(ManyToManyJoinLink),
            "the many-to-many declaration auto-published the junction mapping");

        // Seed the process-wide per-property column-name cache for the junction, as the SQL build would.
        typeof(ManyToManyJoinLink).GetProperty(nameof(ManyToManyJoinLink.ParentId))!
            .GetPropertyColumnName()
            .Should().NotBeNullOrEmpty();

        // Configuring the junction explicitly must win over the auto-published mapping and drop the
        // column names resolved against it, hitting the per-entity ClearColumnNames eviction loop.
        ctx.From<ManyToManyJoinLink>(_ => { });

        DataContextCache.AutoPublishedJunctionMetadata.Should().NotContainKey(typeof(ManyToManyJoinLink));
        typeof(ManyToManyJoinLink).GetProperty(nameof(ManyToManyJoinLink.ParentId))!
            .GetPropertyColumnName()
            .Should().NotBeNullOrEmpty("the configured mapping resolves the column again");
    }
}
