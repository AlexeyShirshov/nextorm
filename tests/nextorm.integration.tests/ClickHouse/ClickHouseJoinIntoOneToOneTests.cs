using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D170 (#170): ClickHouse-owned one-to-one <c>JoinInto</c> runtime cases. ClickHouse does not derive
/// <see cref="CommonTestSuite"/>, so the shared-provider matrix is re-pinned here against a real server:
/// a parent that matches two distinct child identities must throw <see cref="InvalidOperationException"/>
/// at materialization (sync and async), a single-child parent loads its child with identity and payload,
/// and a repeated same-identity row is tolerated and collapses to one child. This is runtime
/// (materialization) evidence over the eager fixtures, not a SQL-generation or capability-flag check.
/// </summary>
public sealed class ClickHouseJoinIntoOneToOneTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void JoinInto_OneToOne_ShouldThrowWhenAParentMatchesMoreThanOneChild()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        Action act = () => _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToList();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one distinct*");
    }

    [Fact]
    public async Task JoinInto_OneToOne_ShouldThrowWhenAParentMatchesMoreThanOneChildAsync()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        Func<Task> act = () => _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*more than one distinct*");
    }

    [Fact]
    public void JoinInto_OneToOne_ShouldAssignTheSingleChildWithItsIdentityAndPayload()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        var parents = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToList();

        parents.Should().ContainSingle();
        var child = parents[0].PrimaryChild;
        child.Should().NotBeNull();
        child!.Id.Should().Be(f + 102);
        child.ParentId.Should().Be(f + 1);
        child.Name.Should().Be("nav-one-a");
    }

    [Fact]
    public async Task JoinInto_OneToOne_ShouldAssignTheSingleChildWithItsIdentityAndPayloadAsync()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        var parents = await _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents.Should().ContainSingle();
        var child = parents[0].PrimaryChild;
        child.Should().NotBeNull();
        child!.Id.Should().Be(f + 102);
        child.ParentId.Should().Be(f + 1);
        child.Name.Should().Be("nav-one-a");
    }

    [Fact]
    public void JoinInto_OneToOne_RepeatedSameIdentity_ShouldNotThrowAndKeepOneChild()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        // A second physical row carrying the same child identity for the same parent: the one-to-one
        // backstop compares child identities per parent, so a repeat must be tolerated and the child
        // assigned once instead of being reported as "more than one distinct".
        _sut.DataProvider.CreateInsertBuilder<JoinIntoChild>().Values([
            new JoinIntoChild { Id = f + 102, ParentId = f + 1, Name = "nav-one-a" },
        ]).Insert();

        // Prove the oracle is fed two physical same-identity child rows, not a shrunken result: the raw
        // table count for this parent must be 2 before the one-to-one collapse is asserted.
        _sut.DataProvider.From<JoinIntoChild>()
            .Where(c => c.ParentId == f + 1)
            .Count()
            .Should().Be(2, "two physical same-identity child rows must reach the oracle");

        var parents = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].PrimaryChild.Should().NotBeNull();
        parents[0].PrimaryChild!.Id.Should().Be(f + 102);
        parents[0].PrimaryChild!.Name.Should().Be("nav-one-a");
    }

    [Fact]
    public async Task JoinInto_OneToOne_RepeatedSameIdentity_ShouldNotThrowAndKeepOneChildAsync()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        _sut.DataProvider.CreateInsertBuilder<JoinIntoChild>().Values([
            new JoinIntoChild { Id = f + 102, ParentId = f + 1, Name = "nav-one-a" },
        ]).Insert();

        // Prove the oracle is fed two physical same-identity child rows (raw count), then assert the collapse.
        _sut.DataProvider.From<JoinIntoChild>()
            .Where(c => c.ParentId == f + 1)
            .Count()
            .Should().Be(2, "two physical same-identity child rows must reach the oracle");

        var parents = await _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents.Should().ContainSingle();
        parents[0].PrimaryChild.Should().NotBeNull();
        parents[0].PrimaryChild!.Id.Should().Be(f + 102);
        parents[0].PrimaryChild!.Name.Should().Be("nav-one-a");
    }
}
