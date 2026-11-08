using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Unit tests for the typed local relationship configuration on <see cref="JoinOptions"/> (#135 slice B):
/// a local one-to-one/many-to-many configuration fully replaces the declared metadata for that
/// <c>JoinInto</c> call and works without registration; a repeated or mixed configuration and a
/// configuration whose kind does not match the overload are rejected.
/// </summary>
public class JoinIntoLocalConfigTests
{
    public JoinIntoLocalConfigTests()
    {
        DataContextCache.Clear();
    }

    [Fact]
    public void OneToOne_LocalConfig_ShouldResolveWithoutRegistration()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<LocalConfigParent>()
            .JoinInto(
                ctx.From<LocalConfigChild>(),
                (p, c) => p.Id == c.ParentId,
                p => p.Profile,
                j => j.OneToOne<LocalConfigParent, LocalConfigChild, int>(p => p.Id, c => c.ParentId));

        act.Should().NotThrow("a local relationship configuration replaces the metadata and needs no registration");
    }

    [Fact]
    public void ManyToMany_LocalConfig_ShouldResolveWithoutRegistration()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<LocalConfigParent>()
            .JoinInto(
                ctx.From<LocalConfigChild>(),
                (p, c) => p.Id == c.Id,
                p => p.Children,
                j => j.ManyToMany<LocalConfigParent, LocalConfigChild, LocalConfigLink, int, int>(
                    p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

        act.Should().NotThrow("a local junction configuration replaces the metadata and needs no registration");
    }

    [Fact]
    public void RepeatedLocalConfig_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<LocalConfigParent>()
            .JoinInto(
                ctx.From<LocalConfigChild>(),
                (p, c) => p.Id == c.ParentId,
                p => p.Profile,
                j =>
                {
                    j.OneToOne<LocalConfigParent, LocalConfigChild, int>(p => p.Id, c => c.ParentId);
                    j.OneToOne<LocalConfigParent, LocalConfigChild, int>(p => p.Id, c => c.ParentId);
                });

        act.Should().Throw<InvalidOperationException>().WithMessage("*at most one relationship configuration*");
    }

    [Fact]
    public void MixedLocalConfig_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<LocalConfigParent>()
            .JoinInto(
                ctx.From<LocalConfigChild>(),
                (p, c) => p.Id == c.ParentId,
                p => p.Profile,
                j =>
                {
                    j.OneToOne<LocalConfigParent, LocalConfigChild, int>(p => p.Id, c => c.ParentId);
                    j.ManyToMany<LocalConfigParent, LocalConfigChild, LocalConfigLink, int, int>(
                        p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId);
                });

        act.Should().Throw<InvalidOperationException>().WithMessage("*at most one relationship configuration*");
    }

    [Fact]
    public void OneToOneLocalConfig_OnCollectionOverload_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<LocalConfigParent>()
            .JoinInto(
                ctx.From<LocalConfigChild>(),
                (p, c) => p.Id == c.ParentId,
                p => p.Children,
                j => j.OneToOne<LocalConfigParent, LocalConfigChild, int>(p => p.Id, c => c.ParentId));

        act.Should().Throw<NotSupportedException>().WithMessage("*OneToMany or ManyToMany*");
    }

    [Fact]
    public void ManyToManyLocalConfig_OnReferenceOverload_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<LocalConfigParent>()
            .JoinInto(
                ctx.From<LocalConfigChild>(),
                (p, c) => p.Id == c.ParentId,
                p => p.Profile,
                j => j.ManyToMany<LocalConfigParent, LocalConfigChild, LocalConfigLink, int, int>(
                    p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

        act.Should().Throw<NotSupportedException>().WithMessage("*must be OneToOne*");
    }

    [Fact]
    public void ManyToManyLocalConfig_KeyTypeMismatch_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<LocalConfigParent>()
            .JoinInto(
                ctx.From<LocalConfigChild>(),
                (p, c) => p.Id == c.Id,
                p => p.Children,
                j => j.ManyToMany<LocalConfigParent, LocalConfigChild, LocalConfigMismatchLink, object, int>(
                    p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

        act.Should().Throw<NotSupportedException>().WithMessage("*does not match the junction*");
    }
}

public sealed class LocalConfigParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public LocalConfigChild? Profile { get; set; }
    public ICollection<LocalConfigChild> Children { get; set; } = new List<LocalConfigChild>();
}

public sealed class LocalConfigChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class LocalConfigLink
{
    public int ParentId { get; set; }
    public int ChildId { get; set; }
}

public sealed class LocalConfigMismatchLink
{
    public Guid ParentId { get; set; }
    public int ChildId { get; set; }
}
