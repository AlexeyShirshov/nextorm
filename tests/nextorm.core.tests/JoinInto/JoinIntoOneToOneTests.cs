using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// In-memory tests for the one-to-one <c>JoinInto</c> overload (#135 slice A): a single child reference
/// is assigned per parent, LEFT leaves the reference null, INNER excludes the childless parent, a parent
/// matching more than one distinct child throws at materialization, and cartesian repeats of one child
/// caused by a neighbouring join are tolerated.
/// </summary>
public class JoinIntoOneToOneTests
{
    private static readonly Guid Gp1 = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Gp2 = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Gp3 = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Gc1 = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Gc2 = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Gc3 = new("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid Gc4 = new("aaaaaaaa-0000-0000-0000-000000000004");

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<OneToOneJoinParent>(b => b
                .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .WithData([
                new OneToOneJoinParent { Id = 1, Name = "p1" },
                new OneToOneJoinParent { Id = 2, Name = "p2" },
                new OneToOneJoinParent { Id = 3, Name = "p3" },
            ]);
        ctx.From<OneToOneJoinChild>().WithData([
            new OneToOneJoinChild { Id = 10, ParentId = 1, Name = "c1" },
            new OneToOneJoinChild { Id = 11, ParentId = 3, Name = "c2" },
            new OneToOneJoinChild { Id = 12, ParentId = 3, Name = "c3" },
        ]);
        ctx.From<OneToOneJoinNote>().WithData([
            new OneToOneJoinNote { Id = 100, ParentId = 1, Text = "n1" },
            new OneToOneJoinNote { Id = 101, ParentId = 1, Text = "n2" },
        ]);

        return ctx;
    }

    [Fact]
    public void LeftJoinInto_ShouldAssignTheChildOrLeaveTheReferenceNull()
    {
        // case PS / R170-03: one-child LEFT positive — assert child identity and payload, not a bare count.
        using var ctx = CreateContext();

        var parents = ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1 || p.Id == 2)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 2);
        parents[0].Primary!.Id.Should().Be(10, "the matching child is assigned to the single reference");
        parents[0].Primary!.Name.Should().Be("c1", "the assigned child carries its own payload, not a bare count");
        parents[1].Primary.Should().BeNull("a LEFT join without a matching child leaves the reference null");
    }

    [Fact]
    public void InnerJoinInto_ShouldExcludeTheChildlessParent()
    {
        // case PS / R170-03: one-child INNER positive — excludes the childless parent, keeps identity+payload.
        using var ctx = CreateContext();

        var parents = ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1 || p.Id == 2)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary, JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1);
        parents[0].Primary!.Id.Should().Be(10);
        parents[0].Primary!.Name.Should().Be("c1", "the INNER join assigns the one matching child with its payload");
    }

    [Fact]
    public void MoreThanOneDistinctChild_ShouldThrowAtMaterialization()
    {
        // case DS / R170-03: two distinct non-null child identities for one parent -> throw at materialization.
        using var ctx = CreateContext();

        Action act = () => ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 3)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one distinct*");
    }

    [Fact]
    public void CartesianRepeatOfTheSameChild_ShouldNotThrowAndShouldAssignOnce()
    {
        // case RS / R170-03: the same child identity repeated by a neighbouring join is tolerated (one assignment).
        using var ctx = CreateContext();

        var parents = ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .JoinInto(ctx.From<OneToOneJoinNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be(10, "the repeated child rows collapse to the one source occurrence");
        parents[0].Notes.Select(n => n.Id).Should().Equal(100, 101);
    }

    [Fact]
    public async Task OneToOneJoinInto_ToListAsync_ShouldMatchSyncStitching()
    {
        // case PA / R170-03: async one-child + absent-child positive asserting identity and payload.
        using var ctx = CreateContext();

        var parents = await ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1 || p.Id == 2)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .OrderBy(p => p.Id)
            .ToListAsync();

        parents.Select(p => p.Id).Should().Equal(1, 2);
        parents[0].Primary!.Id.Should().Be(10);
        parents[0].Primary!.Name.Should().Be("c1", "the async terminal assigns the real child payload");
        parents[1].Primary.Should().BeNull();
    }

    [Fact]
    public async Task MoreThanOneDistinctChild_ToListAsync_ShouldThrowAtMaterialization()
    {
        // case DA / R170-03: the distinct-identity rejection is enforced on the async terminal too, with the
        // same "more than one distinct" diagnostic as the sync path.
        using var ctx = CreateContext();

        Func<Task> act = () => ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 3)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToListAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*more than one distinct*");
    }

    [Fact]
    public async Task CartesianRepeatOfTheSameChild_ToListAsync_ShouldNotThrowAndShouldAssignOnce()
    {
        // case RA / R170-03: the repeated-identity tolerance is the async counterpart of the sync Cartesian case.
        using var ctx = CreateContext();

        var parents = await ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .JoinInto(ctx.From<OneToOneJoinNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be(10, "the repeated child rows collapse to the one source occurrence");
        parents[0].Notes.Select(n => n.Id).Should().Equal(100, 101);
    }

    [Fact]
    public void TwoParentsWithDistinctChildren_ShouldNotThrowAndEachAssignItsOwnChild()
    {
        // case PS / R170-03 two-parent control: two distinct parents, each with one child, must load
        // without a throw and each keep its own child. A global (cross-parent) identity comparison would
        // wrongly report a duplicate, so this rules that implementation out.
        using var ctx = CreateTwoParentContext();

        var parents = ctx.From<OneToOneJoinParent>()
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 2);
        parents[0].Primary!.Id.Should().Be(10);
        parents[0].Primary!.ParentId.Should().Be(1);
        parents[0].Primary!.Name.Should().Be("c1");
        parents[1].Primary!.Id.Should().Be(20);
        parents[1].Primary!.ParentId.Should().Be(2);
        parents[1].Primary!.Name.Should().Be("c2");
    }

    [Fact]
    public async Task TwoParentsWithDistinctChildren_ToListAsync_ShouldNotThrowAndEachAssignItsOwnChild()
    {
        // case PA / R170-03 two-parent control, async parity — same ParentId/Name payload as the sync twin.
        using var ctx = CreateTwoParentContext();

        var parents = await ctx.From<OneToOneJoinParent>()
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .OrderBy(p => p.Id)
            .ToListAsync();

        parents.Select(p => p.Id).Should().Equal(1, 2);
        parents[0].Primary!.Id.Should().Be(10);
        parents[0].Primary!.ParentId.Should().Be(1, "the async terminal assigns the same child payload as the sync twin");
        parents[0].Primary!.Name.Should().Be("c1");
        parents[1].Primary!.Id.Should().Be(20);
        parents[1].Primary!.ParentId.Should().Be(2);
        parents[1].Primary!.Name.Should().Be("c2");
    }

    [Fact]
    public void KeylessChild_WithMoreThanOneChild_ShouldNotThrowAndKeepTheFirstChild()
    {
        // case KS / R170-03: a keyless dependent (no mapped key, so a null identity) has no identity to
        // compare; the repeated foreign key is NOT detected and no exception is thrown. Which tolerated
        // row wins is not part of the contract, so assert set membership rather than join order. This
        // pins the documented keyless limitation, it does not strengthen it.
        using var ctx = CreateKeylessContext();

        var parents = ctx.From<KeylessOneToOneJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<KeylessOneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary.Should().NotBeNull("a keyless child has no identity, so the duplicate is not an error");
        parents[0].Primary!.Name.Should().BeOneOf("a", "b",
            "the repeated null-identity child is tolerated; which row wins is not part of the contract");
    }

    [Fact]
    public async Task KeylessChild_ToListAsync_ShouldNotThrowAndKeepTheFirstChild()
    {
        // case KA / R170-03: the async terminal keeps the same keyless-collapse behaviour.
        using var ctx = CreateKeylessContext();

        var parents = await ctx.From<KeylessOneToOneJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<KeylessOneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Primary!.Name.Should().BeOneOf("a", "b",
            "the async terminal keeps the same keyless-collapse behaviour; the tolerated winner is not fixed");
    }

    // ---- ZS/ZA: null/default -----------------------------------------------

    [Fact]
    public void EmptyChildInput_ShouldLeaveTheReferenceNull()
    {
        // case ZS / R170-03: a parent with no child rows materializes the navigation as null (not an
        // error, and not conflated with an absent child identity). This is the presence/absence side.
        using var ctx = CreateContext();

        var parents = ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 2)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary.Should().BeNull("empty child input leaves the one-to-one navigation null");

        // Contrast: a null child identity (keyless dependent) is not a distinct identity, so repeated
        // keyless children are tolerated; this distinguishes a missing child from an identity-less one,
        // and from a present non-null key (distinct rejection is DS/RS).
        using var keyless = CreateKeylessContext();
        var keylessParents = keyless.From<KeylessOneToOneJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(keyless.From<KeylessOneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();
        keylessParents.Should().ContainSingle();
        keylessParents[0].Primary.Should().NotBeNull("a null identity is not a distinct identity and is tolerated");
    }

    [Fact]
    public async Task EmptyChildInput_ToListAsync_ShouldLeaveTheReferenceNull()
    {
        // case ZA / R170-03: async parity of ZS.
        using var ctx = CreateContext();

        var parents = await ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 2)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Primary.Should().BeNull("empty child input leaves the one-to-one navigation null (async)");

        // Contrast: async parity of the null-identity tolerance (KS/KA).
        using var keyless = CreateKeylessContext();
        var keylessParents = await keyless.From<KeylessOneToOneJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(keyless.From<KeylessOneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToListAsync();
        keylessParents.Should().ContainSingle();
        keylessParents[0].Primary.Should().NotBeNull("a null identity is not a distinct identity and is tolerated (async)");
    }

    // ---- IV: integer zero-valued key ---------------------------------------

    [Fact]
    public void ZeroValuedIntegerKey_SingleChild_ShouldLoadAsRealIdentity()
    {
        // case IV / R170-03: a zero-valued key is a real identity, not "no identity". The single
        // zero-key child must load rather than being treated as a keyless/absent identity.
        using var ctx = CreateZeroSingleContext();

        var parents = ctx.From<ZeroKeyParent>()
            .Where(p => p.Id == 0)
            .JoinInto(ctx.From<ZeroKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary.Should().NotBeNull("a zero-valued key is a real identity, not 'no identity'");
        parents[0].Primary!.Id.Should().Be(0);
        parents[0].Primary!.Name.Should().Be("zero");
    }

    [Fact]
    public void ZeroValuedIntegerKey_DistinctIdentities_ShouldThrow()
    {
        // case IV / R170-03: a zero-valued key participates in the distinct-identity comparison. Two
        // distinct non-null child identities (0 and 1) must throw; conflation with null would tolerate it.
        using var ctx = CreateZeroDistinctContext();

        Action act = () => ctx.From<ZeroKeyParent>()
            .Where(p => p.Id == 0)
            .JoinInto(ctx.From<ZeroKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than one distinct*");
    }

    // ---- SH1: runtime multiplicity preserved across a shared FK ------------

    [Fact]
    public void SharedFk_OneToManyAndOneToOne_CollectionJoin_ShouldPreserveMultiplicity()
    {
        // case SH1 (runtime) / R170-02: a OneToMany and a OneToOne declaration share the one mapped FK.
        // The collection JoinInto (non-OneToOne side) must still return multiple children per parent; the
        // OneToOne declaration must not globally force uniqueness on the shared FK.
        using var ctx = CreateSharedFkContext();

        var parents = ctx.From<SharedFkJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<SharedFkJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo(new[] { 10, 11 },
            "the OneToMany side keeps its permitted multiplicity even though a OneToOne shares the FK");
    }

    [Fact]
    public async Task SharedFk_OneToManyAndOneToOne_CollectionJoin_ToListAsync_ShouldPreserveMultiplicity()
    {
        // case SH1 (runtime) async parity.
        using var ctx = CreateSharedFkContext();

        var parents = await ctx.From<SharedFkJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<SharedFkJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo(new[] { 10, 11 });
    }

    // ---- SH2: two independent OneToOne navigations, same FK ----------------

    [Fact]
    public void TwoOneToOneNavigations_SharedFk_ShouldEachEnforcePerParentRejectionIndependently()
    {
        // case SH2 (runtime) / R170-02: two independently declared OneToOne navigations over the same FK
        // each enforce the per-parent duplicate-identity rejection on their own; and a parent with one
        // child loads on both without a cross-navigation/global false positive.
        using var ctx = CreateDualRefContext();

        Action primary = () => ctx.From<DualRefJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<DualRefJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();
        Action secondary = () => ctx.From<DualRefJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<DualRefJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Secondary)
            .ToList();

        primary.Should().Throw<InvalidOperationException>().WithMessage("*more than one distinct*");
        secondary.Should().Throw<InvalidOperationException>().WithMessage("*more than one distinct*");

        var loaded = ctx.From<DualRefJoinParent>()
            .Where(p => p.Id == 2)
            .JoinInto(ctx.From<DualRefJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();
        loaded.Should().ContainSingle();
        loaded[0].Primary!.Id.Should().Be(20, "a per-parent check must not reject a valid single-child parent");
    }

    // ---- IV/IR: string-key identity ----------------------------------------

    [Fact]
    public void StringKeyedOneToOne_SingleChild_ShouldLoadWithIdentityAndPayload()
    {
        // case IV / R170-03: string (reference) identity — a single child loads with its identity+payload.
        using var ctx = CreateStringKeyContext();

        var parents = ctx.From<StringKeyParent>()
            .Where(p => p.Id == "s1")
            .JoinInto(ctx.From<StringKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be("c1");
        parents[0].Primary!.ParentId.Should().Be("s1");
        parents[0].Primary!.Name.Should().Be("one");
    }

    [Fact]
    public void StringKeyedOneToOne_DistinctIdentities_ShouldThrow()
    {
        // case IV / R170-03: two distinct string identities for one parent throw.
        using var ctx = CreateStringKeyContext();

        Action act = () => ctx.From<StringKeyParent>()
            .Where(p => p.Id == "s2")
            .JoinInto(ctx.From<StringKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than one distinct*");
    }

    [Fact]
    public void StringKeyedOneToOne_CartesianRepeat_ShouldBeTolerated()
    {
        // case IV / R170-03: the same string identity repeated by a neighbouring join is tolerated.
        using var ctx = CreateStringKeyContext();

        var parents = ctx.From<StringKeyParent>()
            .Where(p => p.Id == "s3")
            .JoinInto(ctx.From<StringKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .JoinInto(ctx.From<StringKeyMarker>(), (p, m) => p.Id == m.ParentId, p => p.Markers)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be("c4");
        parents[0].Markers.Select(m => m.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task StringKeyedOneToOne_ToListAsync_SingleChild_ShouldLoadWithIdentityAndPayload()
    {
        // case IR / R170-03: async string-identity single-child positive.
        using var ctx = CreateStringKeyContext();

        var parents = await ctx.From<StringKeyParent>()
            .Where(p => p.Id == "s1")
            .JoinInto(ctx.From<StringKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be("c1");
        parents[0].Primary!.ParentId.Should().Be("s1");
        parents[0].Primary!.Name.Should().Be("one");
    }

    [Fact]
    public async Task StringKeyedOneToOne_ToListAsync_DistinctIdentities_ShouldThrow()
    {
        // case IR / R170-03: async string-identity distinct rejection.
        using var ctx = CreateStringKeyContext();

        Func<Task> act = () => ctx.From<StringKeyParent>()
            .Where(p => p.Id == "s2")
            .JoinInto(ctx.From<StringKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToListAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*more than one distinct*");
    }

    [Fact]
    public async Task StringKeyedOneToOne_ToListAsync_CartesianRepeat_ShouldBeTolerated()
    {
        // case IR / R170-03: async string-identity repeat tolerance.
        using var ctx = CreateStringKeyContext();

        var parents = await ctx.From<StringKeyParent>()
            .Where(p => p.Id == "s3")
            .JoinInto(ctx.From<StringKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .JoinInto(ctx.From<StringKeyMarker>(), (p, m) => p.Id == m.ParentId, p => p.Markers)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be("c4");
        parents[0].Markers.Select(m => m.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    // ---- IV/IR: Guid-key identity ------------------------------------------

    [Fact]
    public void GuidKeyedOneToOne_SingleChild_ShouldLoadWithIdentityAndPayload()
    {
        // case IV / R170-03: Guid (value) identity — single child loads with identity+payload.
        using var ctx = CreateGuidKeyContext();

        var parents = ctx.From<GuidKeyParent>()
            .Where(p => p.Id == Gp1)
            .JoinInto(ctx.From<GuidKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be(Gc1);
        parents[0].Primary!.ParentId.Should().Be(Gp1);
        parents[0].Primary!.Name.Should().Be("one");
    }

    [Fact]
    public void GuidKeyedOneToOne_DistinctIdentities_ShouldThrow()
    {
        // case IV / R170-03: two distinct Guid identities for one parent throw.
        using var ctx = CreateGuidKeyContext();

        Action act = () => ctx.From<GuidKeyParent>()
            .Where(p => p.Id == Gp2)
            .JoinInto(ctx.From<GuidKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than one distinct*");
    }

    [Fact]
    public void GuidKeyedOneToOne_CartesianRepeat_ShouldBeTolerated()
    {
        // case IV / R170-03: the same Guid identity repeated by a neighbouring join is tolerated.
        using var ctx = CreateGuidKeyContext();

        var parents = ctx.From<GuidKeyParent>()
            .Where(p => p.Id == Gp3)
            .JoinInto(ctx.From<GuidKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .JoinInto(ctx.From<GuidKeyMarker>(), (p, m) => p.Id == m.ParentId, p => p.Markers)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be(Gc4);
        parents[0].Markers.Select(m => m.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task GuidKeyedOneToOne_ToListAsync_SingleChild_ShouldLoadWithIdentityAndPayload()
    {
        // case IR / R170-03: async Guid-identity single-child positive.
        using var ctx = CreateGuidKeyContext();

        var parents = await ctx.From<GuidKeyParent>()
            .Where(p => p.Id == Gp1)
            .JoinInto(ctx.From<GuidKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be(Gc1);
        parents[0].Primary!.ParentId.Should().Be(Gp1);
        parents[0].Primary!.Name.Should().Be("one");
    }

    [Fact]
    public async Task GuidKeyedOneToOne_ToListAsync_DistinctIdentities_ShouldThrow()
    {
        // case IR / R170-03: async Guid-identity distinct rejection.
        using var ctx = CreateGuidKeyContext();

        Func<Task> act = () => ctx.From<GuidKeyParent>()
            .Where(p => p.Id == Gp2)
            .JoinInto(ctx.From<GuidKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToListAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*more than one distinct*");
    }

    [Fact]
    public async Task GuidKeyedOneToOne_ToListAsync_CartesianRepeat_ShouldBeTolerated()
    {
        // case IR / R170-03: async Guid-identity repeat tolerance.
        using var ctx = CreateGuidKeyContext();

        var parents = await ctx.From<GuidKeyParent>()
            .Where(p => p.Id == Gp3)
            .JoinInto(ctx.From<GuidKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .JoinInto(ctx.From<GuidKeyMarker>(), (p, m) => p.Id == m.ParentId, p => p.Markers)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be(Gc4);
        parents[0].Markers.Select(m => m.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    private static InMemoryDataContext CreateZeroSingleContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<ZeroKeyParent>(b => b.HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId))
            .WithData([new ZeroKeyParent { Id = 0, Name = "z0" }]);
        ctx.From<ZeroKeyChild>().WithData([new ZeroKeyChild { Id = 0, ParentId = 0, Name = "zero" }]);
        return ctx;
    }

    private static InMemoryDataContext CreateZeroDistinctContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<ZeroKeyParent>(b => b.HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId))
            .WithData([new ZeroKeyParent { Id = 0, Name = "z0" }]);
        ctx.From<ZeroKeyChild>().WithData([
            new ZeroKeyChild { Id = 0, ParentId = 0, Name = "zero" },
            new ZeroKeyChild { Id = 1, ParentId = 0, Name = "one" },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateSharedFkContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<SharedFkJoinParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId))
            .WithData([new SharedFkJoinParent { Id = 1, Name = "p1" }]);
        ctx.From<SharedFkJoinChild>().WithData([
            new SharedFkJoinChild { Id = 10, ParentId = 1, Name = "a" },
            new SharedFkJoinChild { Id = 11, ParentId = 1, Name = "b" },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateDualRefContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<DualRefJoinParent>(b => b
                .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
                .HasOneToOne(p => p.Secondary, p => p.Id, c => c.ParentId))
            .WithData([
                new DualRefJoinParent { Id = 1, Name = "p1" },
                new DualRefJoinParent { Id = 2, Name = "p2" },
            ]);
        ctx.From<DualRefJoinChild>().WithData([
            new DualRefJoinChild { Id = 10, ParentId = 1, Name = "a" },
            new DualRefJoinChild { Id = 11, ParentId = 1, Name = "b" },
            new DualRefJoinChild { Id = 20, ParentId = 2, Name = "c" },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateStringKeyContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<StringKeyParent>(b => b
                .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
                .HasMany(p => p.Markers, m => m.ParentId))
            .WithData([
                new StringKeyParent { Id = "s1", Name = "p1" },
                new StringKeyParent { Id = "s2", Name = "p2" },
                new StringKeyParent { Id = "s3", Name = "p3" },
            ]);
        ctx.From<StringKeyChild>().WithData([
            new StringKeyChild { Id = "c1", ParentId = "s1", Name = "one" },
            new StringKeyChild { Id = "c2", ParentId = "s2", Name = "two" },
            new StringKeyChild { Id = "c3", ParentId = "s2", Name = "three" },
            new StringKeyChild { Id = "c4", ParentId = "s3", Name = "four" },
        ]);
        ctx.From<StringKeyMarker>().WithData([
            new StringKeyMarker { Id = 1, ParentId = "s3" },
            new StringKeyMarker { Id = 2, ParentId = "s3" },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateGuidKeyContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<GuidKeyParent>(b => b
                .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
                .HasMany(p => p.Markers, m => m.ParentId))
            .WithData([
                new GuidKeyParent { Id = Gp1, Name = "p1" },
                new GuidKeyParent { Id = Gp2, Name = "p2" },
                new GuidKeyParent { Id = Gp3, Name = "p3" },
            ]);
        ctx.From<GuidKeyChild>().WithData([
            new GuidKeyChild { Id = Gc1, ParentId = Gp1, Name = "one" },
            new GuidKeyChild { Id = Gc2, ParentId = Gp2, Name = "two" },
            new GuidKeyChild { Id = Gc3, ParentId = Gp2, Name = "three" },
            new GuidKeyChild { Id = Gc4, ParentId = Gp3, Name = "four" },
        ]);
        ctx.From<GuidKeyMarker>().WithData([
            new GuidKeyMarker { Id = 1, ParentId = Gp3 },
            new GuidKeyMarker { Id = 2, ParentId = Gp3 },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateTwoParentContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<OneToOneJoinParent>(b => b
                .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .WithData([
                new OneToOneJoinParent { Id = 1, Name = "p1" },
                new OneToOneJoinParent { Id = 2, Name = "p2" },
            ]);
        ctx.From<OneToOneJoinChild>().WithData([
            new OneToOneJoinChild { Id = 10, ParentId = 1, Name = "c1" },
            new OneToOneJoinChild { Id = 20, ParentId = 2, Name = "c2" },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateKeylessContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<KeylessOneToOneJoinChild>().WithData([
            new KeylessOneToOneJoinChild { ParentId = 1, Name = "a" },
            new KeylessOneToOneJoinChild { ParentId = 1, Name = "b" },
        ]);
        ctx.From<KeylessOneToOneJoinParent>(b => b
                .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId))
            .WithData([new KeylessOneToOneJoinParent { Id = 1, Name = "p1" }]);
        return ctx;
    }
}

public sealed class OneToOneJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public OneToOneJoinChild? Primary { get; set; }
    public ICollection<OneToOneJoinNote> Notes { get; set; } = new List<OneToOneJoinNote>();
}

public sealed class OneToOneJoinChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class OneToOneJoinNote
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Text { get; set; }
}

public sealed class KeylessOneToOneJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public KeylessOneToOneJoinChild? Primary { get; set; }
}

public sealed class KeylessOneToOneJoinChild
{
    // Deliberately no key property: the identity selector returns null, so the duplicate check cannot run.
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class ZeroKeyParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public ZeroKeyChild? Primary { get; set; }
}

public sealed class ZeroKeyChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class SharedFkJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public ICollection<SharedFkJoinChild> Children { get; set; } = new List<SharedFkJoinChild>();
    public SharedFkJoinChild? Primary { get; set; }
}

public sealed class SharedFkJoinChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class DualRefJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public DualRefJoinChild? Primary { get; set; }
    public DualRefJoinChild? Secondary { get; set; }
}

public sealed class DualRefJoinChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class StringKeyParent
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public StringKeyChild? Primary { get; set; }
    public ICollection<StringKeyMarker> Markers { get; set; } = new List<StringKeyMarker>();
}

public sealed class StringKeyChild
{
    public string Id { get; set; } = "";
    public string ParentId { get; set; } = "";
    public string? Name { get; set; }
}

public sealed class StringKeyMarker
{
    public int Id { get; set; }
    public string ParentId { get; set; } = "";
}

public sealed class GuidKeyParent
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public GuidKeyChild? Primary { get; set; }
    public ICollection<GuidKeyMarker> Markers { get; set; } = new List<GuidKeyMarker>();
}

public sealed class GuidKeyChild
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class GuidKeyMarker
{
    public int Id { get; set; }
    public Guid ParentId { get; set; }
}
