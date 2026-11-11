using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T249-D3 split execution: deterministic synthetic-row tests of the metadata-driven eager loader. The
/// rows never touch a database: they exercise the internal specification surface directly, so grouping,
/// assignment and cardinality are asserted without SQL. Component <c>i</c> is proved to pair with
/// component <c>i</c> in declaration order — a name-based or first-component-only implementation fails
/// these rows — and the chunk policy is asserted through the key plan itself. Live SQL, command counts
/// and provider semantics belong to the integration boundary (D6).
/// </summary>
[Collection("Query cache controls")]
public sealed class MetadataLoadWithMaterializationTests
{
    // ---------------------------------------------------------------------------------------------
    // Fixtures. Each parent type carries one navigations shape, so the metadata-free internal surface
    // (key plan + eager spec) can be built without a configured context or a registered model.
    // ---------------------------------------------------------------------------------------------

    public sealed class MatParent
    {
        [Key]
        public int Id { get; set; }

        public int Tenant { get; set; }

        public int Code { get; set; }

        public ICollection<MatChild> Children { get; set; } = new List<MatChild>();
    }

    public sealed class MatChild
    {
        [Key]
        public int Id { get; set; }

        public int Tenant { get; set; }

        public int OwnerCode { get; set; }

        public int LegacyRef { get; set; }
    }

    public sealed class MatAssignParent
    {
        [Key]
        public int Id { get; set; }

        public int Tenant { get; set; }

        public int Code { get; set; }

        public ICollection<MatAssignChild> Prefilled { get; set; } = new List<MatAssignChild>();

        public ICollection<MatAssignChild> WritableNull { get; set; } = null!;
    }

    public sealed class MatAssignChild
    {
        [Key]
        public int Id { get; set; }

        public int Tenant { get; set; }

        public int ParentRef { get; set; }
    }

    public sealed class MatReadOnlyParent
    {
        [Key]
        public int Id { get; set; }

        public int Tenant { get; set; }

        public int Code { get; set; }

        public ICollection<MatChild> Children { get; } = null!;
    }

    public sealed class MatNullParent
    {
        [Key]
        public int Id { get; set; }

        public int? Tenant { get; set; }

        public int? Code { get; set; }

        public ICollection<MatNullChild> Children { get; set; } = new List<MatNullChild>();
    }

    public sealed class MatNullChild
    {
        [Key]
        public int Id { get; set; }

        public int? Tenant { get; set; }

        public int? OwnerCode { get; set; }
    }

    public sealed class MatOwner
    {
        [Key]
        public int Id { get; set; }

        public int Tenant { get; set; }

        public string? Name { get; set; }
    }

    public sealed class MatRefParent
    {
        [Key]
        public int Id { get; set; }

        public int OwnerTenant { get; set; }

        public int OwnerId { get; set; }

        public MatOwner? Owner { get; set; }
    }

    public sealed class MatReadOnlyRefParent
    {
        [Key]
        public int Id { get; set; }

        public MatOwner? Owner { get; } = null;
    }

    public sealed class MatMnParent
    {
        [Key]
        public int Id { get; set; }

        public ICollection<MatMnChild> Children { get; set; } = new List<MatMnChild>();
    }

    public sealed class MatMnChild
    {
        [Key]
        public int Id { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // Full-key grouping: every ordered component participates, position pairs components, and neither a
    // member name nor the first component alone can produce the correct grouping.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_TwoParentsTwoChildren_GroupByEveryOrderedComponent()
    {
        var plan = Plan<MatParent, MatChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));
        var spec = Collection<MatParent, MatChild>(p => p.Children, plan);

        var first = new MatParent { Id = 1, Tenant = 1, Code = 10 };
        var second = new MatParent { Id = 2, Tenant = 2, Code = 20 };

        // The first and last rows are cross-pairs: each component occurs under both parents, but only the
        // full ordered tuple identifies the owner. A first-component-only match would blend tenants.
        var firstChild = new MatChild { Id = 101, Tenant = 1, OwnerCode = 10 };
        var secondChild = new MatChild { Id = 102, Tenant = 2, OwnerCode = 20 };
        var crossFirst = new MatChild { Id = 103, Tenant = 1, OwnerCode = 20 };
        var crossSecond = new MatChild { Id = 104, Tenant = 2, OwnerCode = 10 };

        Assign(spec, [first, second], [firstChild, secondChild, crossFirst, crossSecond]);

        first.Children.Select(c => c.Id).Should().Equal(101);
        second.Children.Select(c => c.Id).Should().Equal(102);
    }

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_SecondComponentNameDiffers_MatchesByPosition()
    {
        var plan = Plan<MatParent, MatChild>(("Tenant", "Tenant"), ("Code", "LegacyRef"));
        var spec = Collection<MatParent, MatChild>(p => p.Children, plan);

        var parent = new MatParent { Id = 1, Tenant = 1, Code = 10 };
        var child = new MatChild { Id = 101, Tenant = 1, LegacyRef = 10 };

        Assign(spec, [parent], [child]);

        plan.ParentProperties[1].Name.Should().Be(nameof(MatParent.Code));
        plan.ChildProperties[1].Name.Should().Be(
            nameof(MatChild.LegacyRef),
            "the child component name differs from the parent's; only type and position pair components");
        parent.Children.Select(c => c.Id).Should().Equal(101);
    }

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_SwappedDeclarationOrder_DoesNotMatchByName()
    {
        // Declared child order is (LegacyRef, Tenant), parent order (Tenant, Code). A name-based matcher
        // would pair Tenant->Tenant and Code->LegacyRef and match; positional pairing compares
        // Tenant->LegacyRef and Code->Tenant and must match nothing.
        var plan = Plan<MatParent, MatChild>(("Tenant", "LegacyRef"), ("Code", "Tenant"));
        var spec = Collection<MatParent, MatChild>(p => p.Children, plan);

        var parent = new MatParent { Id = 1, Tenant = 1, Code = 10 };
        var nameMatchOnly = new MatChild { Id = 101, Tenant = 1, LegacyRef = 10 };

        Assign(spec, [parent], [nameMatchOnly]);

        parent.Children.Should().BeEmpty(
            "component i pairs with component i; a swapped declaration cannot match by member name");
    }

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_NullParentComponent_MatchesNothing()
    {
        var plan = Plan<MatNullParent, MatNullChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));
        var spec = Collection<MatNullParent, MatNullChild>(p => p.Children, plan);

        var partial = new MatNullParent { Id = 1, Tenant = null, Code = 10 };
        var complete = new MatNullParent { Id = 2, Tenant = 1, Code = 10 };
        var child = new MatNullChild { Id = 101, Tenant = 1, OwnerCode = 10 };

        Assign(spec, [partial, complete], [child]);

        partial.Children.Should().BeEmpty("a null key component is not a value and matches nothing");
        complete.Children.Select(c => c.Id).Should().Equal(101);
    }

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_NullChildComponent_IsNotMatchable()
    {
        var plan = Plan<MatNullParent, MatNullChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));
        var spec = Collection<MatNullParent, MatNullChild>(p => p.Children, plan);

        var parent = new MatNullParent { Id = 1, Tenant = 1, Code = 10 };
        var nullComponent = new MatNullChild { Id = 101, Tenant = null, OwnerCode = 10 };
        var dangling = new MatNullChild { Id = 102, Tenant = 2, OwnerCode = 99 };
        var valid = new MatNullChild { Id = 103, Tenant = 1, OwnerCode = 10 };

        Assign(spec, [parent], [nullComponent, dangling, valid]);

        parent.Children.Select(c => c.Id).Should().Equal(103);
    }

    // ---------------------------------------------------------------------------------------------
    // Assignment: the existing clear/refill, writable-null, read-only-null and empty semantics are
    // preserved on the metadata path.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Assignment_PrefilledCollection_IsClearedAndRefilled()
    {
        var plan = Plan<MatAssignParent, MatAssignChild>(("Tenant", "Tenant"), ("Code", "ParentRef"));
        var spec = Collection<MatAssignParent, MatAssignChild>(p => p.Prefilled, plan);

        var stale = new MatAssignChild { Id = 900, Tenant = 1, ParentRef = 10 };
        var parent = new MatAssignParent { Id = 1, Tenant = 1, Code = 10 };
        parent.Prefilled.Add(stale);

        var current = new MatAssignChild { Id = 101, Tenant = 1, ParentRef = 10 };
        Assign(spec, [parent], [current]);

        parent.Prefilled.Should().ContainSingle().Which.Should().BeSameAs(current);
    }

    [Fact]
    public void MetadataLoadWith_Assignment_WritableNull_IsInitialized()
    {
        var plan = Plan<MatAssignParent, MatAssignChild>(("Tenant", "Tenant"), ("Code", "ParentRef"));
        var spec = Collection<MatAssignParent, MatAssignChild>(p => p.WritableNull, plan);

        var parent = new MatAssignParent { Id = 1, Tenant = 1, Code = 10 };
        parent.WritableNull.Should().BeNull();

        var child = new MatAssignChild { Id = 101, Tenant = 1, ParentRef = 10 };
        Assign(spec, [parent], [child]);

        parent.WritableNull.Should().NotBeNull();
        parent.WritableNull.Select(c => c.Id).Should().Equal(101);
    }

    [Fact]
    public void MetadataLoadWith_Assignment_ReadOnlyNull_ThrowsBeforeAnyMutation()
    {
        var plan = Plan<MatReadOnlyParent, MatChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));
        var spec = Collection<MatReadOnlyParent, MatChild>(p => p.Children, plan);

        var parent = new MatReadOnlyParent { Id = 1, Tenant = 1, Code = 10 };
        var child = new MatChild { Id = 101, Tenant = 1, OwnerCode = 10 };

        FluentActions.Invoking(() => Assign(spec, [parent], [child]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*not settable*");
    }

    [Fact]
    public void MetadataLoadWith_Assignment_NoChildren_YieldsEmptyCollection()
    {
        var plan = Plan<MatParent, MatChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));
        var spec = Collection<MatParent, MatChild>(p => p.Children, plan);

        var parent = new MatParent { Id = 1, Tenant = 1, Code = 10 };
        parent.Children.Add(new MatChild { Id = 900, Tenant = 1, OwnerCode = 10 });

        Assign(spec, [parent], []);

        parent.Children.Should().BeEmpty();
    }

    [Fact]
    public void MetadataLoadWith_Assignment_FilteredChildren_DoNotDropParents()
    {
        var plan = Plan<MatParent, MatChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));
        var spec = Collection<MatParent, MatChild>(p => p.Children, plan);

        var kept = new MatParent { Id = 1, Tenant = 1, Code = 10 };
        var filteredOut = new MatParent { Id = 2, Tenant = 2, Code = 20 };
        var parentList = new List<MatParent> { kept, filteredOut };

        // The child query filtered rows away; a parent with no matching row must stay in the result with
        // an empty collection, never be dropped from the parent list.
        Assign(spec, parentList, [new MatChild { Id = 101, Tenant = 1, OwnerCode = 10 }]);

        parentList.Should().HaveCount(2);
        kept.Children.Select(c => c.Id).Should().Equal(101);
        filteredOut.Children.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------------
    // Reference cardinality: full identity only. Repeated physical rows of one identity are tolerated;
    // two distinct identities are rejected before the assignment.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_ReferenceIdentity_ZeroRows_YieldsNull()
    {
        MetadataReferenceAssignment.Resolve<MatOwner>([], o => o.Id, "conflict")
            .Should().BeNull();
    }

    [Fact]
    public void MetadataLoadWith_ReferenceIdentity_OneRow_IsResolved()
    {
        var owner = new MatOwner { Id = 1, Tenant = 1, Name = "owner" };

        MetadataReferenceAssignment.Resolve([owner], o => o.Id, "conflict")
            .Should().BeSameAs(owner);
    }

    [Fact]
    public void MetadataLoadWith_ReferenceIdentity_RepeatedSameIdentity_DoesNotThrow()
    {
        var owner = new MatOwner { Id = 1, Tenant = 1, Name = "owner" };
        var sameIdentityOtherInstance = new MatOwner { Id = 1, Tenant = 1, Name = "owner" };

        MetadataReferenceAssignment.Resolve([owner, owner, sameIdentityOtherInstance], o => o.Id, "conflict")
            .Should().BeSameAs(owner, "repeated physical rows of one full identity collapse");
    }

    [Fact]
    public void MetadataLoadWith_ReferenceIdentity_TwoDistinctIdentities_ThrowBeforeAssignment()
    {
        var first = new MatOwner { Id = 1, Tenant = 1, Name = "first" };
        var second = new MatOwner { Id = 2, Tenant = 1, Name = "second" };

        FluentActions.Invoking(() => MetadataReferenceAssignment.Resolve([first, second], o => o.Id, "conflict"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*conflict*");
    }

    [Fact]
    public void MetadataLoadWith_ReferenceIdentity_WritableNull_IsInitializedAndZeroYieldsNull()
    {
        var parent = new MatRefParent { Id = 1, OwnerTenant = 1, OwnerId = 10 };
        var owner = new MatOwner { Id = 10, Tenant = 1, Name = "owner" };

        MetadataReferenceAssignment.Assign<MatRefParent, MatOwner>(
            [parent],
            [owner],
            p => p.Owner,
            (p, value) => p.Owner = value,
            nameof(MatRefParent),
            nameof(MatRefParent.Owner));

        parent.Owner.Should().BeSameAs(owner);

        MetadataReferenceAssignment.Assign<MatRefParent, MatOwner>(
            [parent],
            [null],
            p => p.Owner,
            (p, value) => p.Owner = value,
            nameof(MatRefParent),
            nameof(MatRefParent.Owner));

        parent.Owner.Should().BeNull();
    }

    [Fact]
    public void MetadataLoadWith_ReferenceIdentity_ReadOnlyNull_Throws()
    {
        var parent = new MatReadOnlyRefParent { Id = 1 };
        var owner = new MatOwner { Id = 10, Tenant = 1, Name = "owner" };

        FluentActions.Invoking(() => MetadataReferenceAssignment.Assign<MatReadOnlyRefParent, MatOwner>(
                [parent],
                [owner],
                p => p.Owner,
                setter: null,
                nameof(MatReadOnlyRefParent),
                nameof(MatReadOnlyRefParent.Owner)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*not settable*");

        // A childless read-only navigation stays untouched instead of failing a no-op load.
        MetadataReferenceAssignment.Assign<MatReadOnlyRefParent, MatOwner>(
            [parent],
            [null],
            p => p.Owner,
            setter: null,
            nameof(MatReadOnlyRefParent),
            nameof(MatReadOnlyRefParent.Owner));
    }

    // ---------------------------------------------------------------------------------------------
    // Many-to-many link occurrences: one element per valid link, dangling links ignored, and independent
    // declarations never share a grouping list.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_LinkOccurrences_RepeatedLinks_PreserveCountWithoutCartesianProduct()
    {
        var first = new MatMnParent { Id = 1 };
        var second = new MatMnParent { Id = 2 };
        var firstEntry = new MatMnChild { Id = 101 };
        var secondEntry = new MatMnChild { Id = 102 };
        var children = new List<MatMnChild> { secondEntry, firstEntry };

        var links = new List<(object ParentKey, object ChildKey)>
        {
            (first.Id, firstEntry.Id),
            (first.Id, firstEntry.Id),
            (first.Id, secondEntry.Id),
            (second.Id, secondEntry.Id),
            (first.Id, 999),
        };

        var grouped = MetadataManyToManyAssignment.Group(links, children, c => c.Id);

        grouped[first.Id].Should().HaveCount(3, "two valid occurrences plus one other child, no cartesian multiplication");
        grouped[first.Id].Count(c => c.Id == firstEntry.Id).Should().Be(2);
        grouped[first.Id].Should().ContainInOrder(secondEntry, firstEntry, firstEntry);
        grouped[second.Id].Should().ContainSingle().Which.Should().BeSameAs(secondEntry);
    }

    [Fact]
    public void MetadataLoadWith_LinkOccurrences_DanglingAndUnmatchableLinks_AreIgnored()
    {
        var parent = new MatMnParent { Id = 1 };
        var child = new MatMnChild { Id = 101 };

        var links = new List<(object ParentKey, object ChildKey)>
        {
            (parent.Id, 999),
            (parent.Id, child.Id),
        };

        var grouped = MetadataManyToManyAssignment.Group(links, [child], c => c.Id);

        grouped[parent.Id].Should().ContainSingle().Which.Should().BeSameAs(child);
        grouped.Should().NotContainKey(999);
    }

    [Fact]
    public void MetadataLoadWith_LinkOccurrences_TwoCollections_DoNotMultiply()
    {
        var parent = new MatMnParent { Id = 1 };
        var child = new MatMnChild { Id = 101 };
        var links = new List<(object ParentKey, object ChildKey)> { (parent.Id, child.Id) };

        var children = new List<MatMnChild> { child };
        var first = MetadataManyToManyAssignment.Group(links, children, c => c.Id);
        var second = MetadataManyToManyAssignment.Group(links, children, c => c.Id);

        first[parent.Id].Should().ContainSingle();
        second[parent.Id].Should().ContainSingle();
        first[parent.Id].Should().NotBeSameAs(second[parent.Id],
            "each declaration owns its grouping; two collections over one link table never share a list");
    }

    // ---------------------------------------------------------------------------------------------
    // Chunking: the chunk size bounds the total key parameters and a chunk count is independent of the
    // number of parents once the keys are distinct.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_ChunkSize_BoundsTheTotalParameterCount()
    {
        var one = Plan<MatParent, MatChild>(("Tenant", "Tenant"));
        var two = Plan<MatParent, MatChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));
        var three = Plan<MatParent, MatChild>(
            ("Tenant", "Tenant"),
            ("Code", "OwnerCode"),
            ("Code", "LegacyRef"));

        one.ChunkSize.Should().Be(1000);
        two.ChunkSize.Should().Be(1000);
        three.ChunkSize.Should().Be(666);

        foreach (var plan in new[] { one, two, three })
        {
            (plan.ChunkSize * Math.Max(plan.ParentProperties.Count, plan.ChildProperties.Count)).Should().BeLessThanOrEqualTo(
                MetadataEagerLoadKeyPlan<MatParent, MatChild>.ParameterLimit);
        }
    }

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_Chunks_ZeroOneThousandAnd1001Parents()
    {
        var one = Plan<MatParent, MatChild>(("Tenant", "Tenant"));
        var two = Plan<MatParent, MatChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));

        var single = Enumerable.Range(0, 1001).Select(i => new MatParent { Id = i, Tenant = i }).ToList();
        var composite = Enumerable.Range(0, 1001)
            .Select(i => new MatParent { Id = i, Tenant = i / 1000, Code = i })
            .ToList();

        one.Chunks([]).Should().BeEmpty();
        one.Chunks(single.Take(1).ToList()).Should().HaveCount(1);
        one.Chunks(single.Take(1000).ToList()).Should().HaveCount(1);
        one.Chunks(single).Should().HaveCount(2, "1001 distinct keys exceed the 1000-key chunk cap");

        two.Chunks([]).Should().BeEmpty();
        two.Chunks(composite.Take(1).ToList()).Should().HaveCount(1);
        two.Chunks(composite.Take(1000).ToList()).Should().HaveCount(1);
        two.Chunks(composite).Should().HaveCount(2,
            "the two-component chunk is 1000 because the parameter budget is 2000");
    }

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_Chunks_DeduplicateKeysAndSkipIncompleteOnes()
    {
        var plan = Plan<MatNullParent, MatNullChild>(("Tenant", "Tenant"), ("Code", "OwnerCode"));
        var first = new MatNullParent { Id = 1, Tenant = 1, Code = 10 };
        var sameKey = new MatNullParent { Id = 2, Tenant = 1, Code = 10 };
        var incomplete = new MatNullParent { Id = 3, Tenant = null, Code = 10 };

        var chunks = plan.Chunks([first, sameKey, incomplete]).ToList();

        chunks.Should().ContainSingle();
        chunks[0].Should().ContainSingle().Which.Should().BeSameAs(first,
            "a duplicate key is requested once and an incomplete key is never requested");
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------

    private static MetadataEagerLoadKeyPlan<TParent, TChild> Plan<TParent, TChild>(
        params (string Parent, string Child)[] pairs)
        => new(
            pairs.Select(pair => typeof(TParent).GetProperty(pair.Parent)!).ToArray(),
            pairs.Select(pair => typeof(TChild).GetProperty(pair.Child)!).ToArray());

    private static EagerLoadSpec<TParent, TChild, object> Collection<TParent, TChild>(
        Expression<Func<TParent, ICollection<TChild>>> collection,
        MetadataEagerLoadKeyPlan<TParent, TChild> plan)
        => new(
            collection,
            static _ => throw new NotSupportedException("synthetic rows are assigned directly"),
            plan.ParentKeyExpression,
            plan.ChildKeyExpression,
            EagerLoadMode.Default,
            plan);

    private static void Assign<TParent, TChild>(
        EagerLoadSpec<TParent, TChild, object> spec,
        IReadOnlyList<TParent> parents,
        IReadOnlyList<TChild> children)
    {
        var grouped = new Dictionary<object, List<TChild>>();
        spec.AddChildren(grouped, children.ToList());
        spec.Assign(parents, grouped);
    }
}
