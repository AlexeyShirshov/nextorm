using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T251 D251-05 live half: composite relationship keys declared through the public fluent A–J surface
/// and executed against a real provider. The declaration is the fluent surface itself
/// (<c>HasMany</c>/<c>HasOne</c>/<c>HasOneToOne</c>/<c>HasManyThrough</c> object selectors on the shared
/// composite fixtures), not an injected <see cref="IRelationshipMetadata"/>. The rows are the same ones
/// the existing manual-metadata composite tests use, so declared and manual surfaces are directly
/// comparable; a partial-component match, a cross-tenant local id and a null component must never link.
/// </summary>
public abstract partial class CommonTestSuite
{
    private static string T251Sql<TResult>(IDataContext ctx, QueryCommand<TResult> command)
        => ((DbPreparedQueryCommand<TResult>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    private static string T251Canon(string sql)
        => sql.Replace("\r\n", "\n").Replace("\"", string.Empty).Replace("`", string.Empty)
            .Replace("[", string.Empty).Replace("]", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

    /// <summary>
    /// Declared 1:N: every component of <c>(tenant, link_id) → (tenant, parent_id)</c> participates.
    /// The three parents share one component each, an orphan child matches no parent and a dangling
    /// non-null foreign key contributes nothing.
    /// </summary>
    [Fact]
    public void T251_DeclaredCompositeOneToMany_ShouldMatchEveryComponentOnTheLiveDatabase()
    {
        var ctx = _sut.DataProvider;
        var (a, b, c) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);

        var terminals = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .OrderBy(p => p.RowId)
            .Select(p => new { p.RowId, Has = p.Children.Any(), Count = p.Children.Count() })
            .ToList();

        terminals.Select(r => r.RowId).Should().Equal(a, b, c);
        terminals[0].Count.Should().Be(2, "the first parent owns both children keyed to (t1, l1)");
        terminals[1].Count.Should().Be(1, "the trailing component routes the second parent's child");
        terminals[2].Count.Should().Be(1, "the tenant component routes the third parent's child");
        terminals.Should().OnlyContain(r => r.Has == (r.Count > 0));

        var stitched = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .JoinInto(
                ctx.From<T251DeclaredChild>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        stitched.Select(p => p.RowId).Should().Equal(a, b, c);
        stitched[0].Children.Select(ch => ch.Id).Should().BeEquivalentTo([a + 100, a + 101]);
        stitched[1].Children.Select(ch => ch.Id).Should().Equal(a + 102);
        stitched[2].Children.Select(ch => ch.Id).Should().Equal(a + 103);
        stitched.SelectMany(p => p.Children).Select(ch => ch.Id)
            .Should().NotContain(a + 104, "the orphan child (t2, l2) matches no parent")
            .And.NotContain(a + 105, "a dangling non-null foreign key contributes nothing");
    }

    /// <summary>
    /// Declared N:1 reference: the composite mapping is the child's <c>(tenant, parent_id)</c> against the
    /// parent's <c>(tenant, link_id)</c>. The same local link id in another tenant must not resolve and a
    /// dangling non-null foreign key materialises as <c>null</c>.
    /// </summary>
    [Fact]
    public void T251_DeclaredCompositeManyToOne_ShouldResolveTheReferenceOnEveryComponent()
    {
        var ctx = _sut.DataProvider;
        var (a, _, c) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);

        var rows = ctx.From<T251DeclaredChild>()
            .Where(ch => ch.Id == a + 100 || ch.Id == a + 103 || ch.Id == a + 105)
            .OrderBy(ch => ch.Id)
            .Select(ch => new { ch.Id, ParentRow = (int?)ch.Parent!.RowId })
            .ToList();

        rows.Single(r => r.Id == a + 100).ParentRow.Should().Be(a, "the full (tenant, link) pair resolves");
        rows.Single(r => r.Id == a + 103).ParentRow.Should().Be(c,
            "the tenant component selects the (t2, l1) parent, never the (t1, l1) one");
        rows.Single(r => r.Id == a + 105).ParentRow.Should().BeNull("a dangling foreign key has no principal");

        var presence = ctx.From<T251DeclaredChild>()
            .Where(ch => ch.Id == a + 100 || ch.Id == a + 105)
            .OrderBy(ch => ch.Id)
            .Select(ch => new { ch.Id, Present = ch.Parent != null })
            .ToList();
        presence[0].Present.Should().BeTrue();
        presence[1].Present.Should().BeFalse("== null and != null agree for a dangling key");
    }

    /// <summary>
    /// Declared 1:1: all parents share the tenant and differ only in the trailing link component, so a
    /// tenant-only match would assign each parent both children; one parent has no child and stays null.
    /// </summary>
    [Fact]
    public void T251_DeclaredCompositeOneToOne_ShouldAssignByEveryComponent()
    {
        var ctx = _sut.DataProvider;
        var (withChild, otherChild, childless) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToOne(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == withChild || p.RowId == otherChild || p.RowId == childless)
            .JoinInto(
                ctx.From<T251DeclaredPrimary>(),
                (p, ch) => p.Tenant == ch.OwnerTenant && p.LinkId == ch.ParentLink,
                p => p.Primary)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(withChild, otherChild, childless);
        parents[0].Primary!.Id.Should().Be(withChild + 110);
        parents[1].Primary!.Id.Should().Be(otherChild + 110);
        parents[2].Primary.Should().BeNull("the childless composite key leaves the reference null");
    }

    /// <summary>
    /// Declared M:N through a composite junction: both junction legs use every component, the repeated
    /// (parent, child) occurrence stays a distinct element and the dangling child link contributes none.
    /// </summary>
    [Fact]
    public void T251_DeclaredCompositeJunction_ShouldRouteBothLegsAndPreserveOccurrences()
    {
        var ctx = _sut.DataProvider;
        var (first, second) = JoinIntoCompositeJunctionFixtures.SeedDeclared(ctx);

        var counted = ctx.From<T251DeclaredJunctionParent>()
            .Where(p => p.RowId == first || p.RowId == second)
            .OrderBy(p => p.RowId)
            .Select(p => new { p.RowId, Count = p.Children.Count() })
            .ToList();
        counted.Select(r => r.RowId).Should().Equal(first, second);
        counted[0].Count.Should().Be(2, "the repeated occurrence is preserved and the dangling link is excluded");
        counted[1].Count.Should().Be(1);

        var stitched = ctx.From<T251DeclaredJunctionParent>()
            .Where(p => p.RowId == first || p.RowId == second)
            .JoinInto(ctx.From<JiCompositeJunctionChild>(), (p, ch) => true, p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        stitched[0].Children.Should().HaveCount(2);
        stitched[0].Children.Select(ch => ch.Id).Should().OnlyContain(id => id == first + 100,
            "the junction's trailing parent component routes c1 to the first parent only");
        stitched[0].Children.Should().NotContainNulls("the dangling child foreign key contributes no element");
        stitched[1].Children.Select(ch => ch.Id).Should().Equal(new[] { first + 101 },
            "the same first component in another link row must not cross-link");
    }

    /// <summary>
    /// Declared surface parity: the rows seeded for the declared surface are read back through the
    /// pre-existing manual-metadata composite types and must match the declared result exactly.
    /// </summary>
    [Fact]
    public void T251_DeclaredSurface_ShouldMatchTheManualMetadataCompositeResults()
    {
        var ctx = _sut.DataProvider;
        var (a, b, c) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);

        JoinIntoCompositeKeyFixtures.RegisterOneToMany();

        var declared = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .JoinInto(
                ctx.From<T251DeclaredChild>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        var manual = ctx.From<JiCompositeOneToManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .JoinInto(
                ctx.From<JiCompositeKeyChild>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        manual.Select(p => p.RowId).Should().Equal(declared.Select(p => p.RowId));
        manual.Select(p => string.Join(",", p.Children.Select(ch => ch.Id).OrderBy(id => id)))
            .Should().Equal(declared.Select(p => string.Join(",", p.Children.Select(ch => ch.Id).OrderBy(id => id))),
                "the declared A–J surface is the same ordered mapping as the manual metadata");
    }

    /// <summary>
    /// Each null component of the composite foreign key yields no relationship: the collection is empty
    /// for the invalid rows, the valid row is still linked, and the SQL is a plain equality on every
    /// component — no null-safe operator and no <c>null == null</c> fallback.
    /// </summary>
    [Fact]
    public void T251_NullForeignKeyComponents_ShouldYieldNoRelationship()
    {
        var ctx = _sut.DataProvider;
        var (parent, _, _, validChild) = JoinIntoCompositeKeyFixtures.SeedDeclaredNullable(ctx);

        var row = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == parent)
            .Select(p => new { Has = p.NullableChildren.Any(), Count = p.NullableChildren.Count() })
            .ToList()
            .Single();

        row.Has.Should().BeTrue("the fully non-null child is linked");
        row.Count.Should().Be(1,
            "the first-null, last-null, all-null and dangling rows must each contribute nothing");

        var sql = T251Canon(T251Sql(ctx, ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == parent)
            .Select(p => p.NullableChildren.Any())));
        sql.Should().NotContain("coalesce")
            .And.NotContain("<=>")
            .And.NotContain("isnotdistinct")
            .And.NotContain("isnull", "a null-safe comparison would link the all-null key row");

        var direct = ctx.From<T251DeclaredNullableChild>()
            .Where(ch => ch.Id == validChild)
            .Select(ch => ch.Id)
            .ToList();
        direct.Should().Equal(validChild);
    }

    /// <summary>
    /// Single-column nullable foreign key (the legacy shape): a null component resolves the reference to
    /// <c>null</c> instead of throwing <c>Expression.Equal(int?, int)</c>, a non-null component links the
    /// principal, and a dangling non-null value stays null. The predicate is a plain equality.
    /// </summary>
    [Fact]
    public void T251_SingleColumnNullableReference_ShouldResolveOrYieldNullOnTheLiveDatabase()
    {
        var ctx = _sut.DataProvider;
        var (parent, tenant, link, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredNullable(ctx);
        var a = parent;

        ctx.CreateInsertBuilder<T251SingleNullableChild>().Values([
            new T251SingleNullableChild { Id = a + 130, Tenant = tenant, ParentId = null, SortKey = 1 },
            new T251SingleNullableChild { Id = a + 131, Tenant = tenant, ParentId = link, SortKey = 2 },
            new T251SingleNullableChild { Id = a + 132, Tenant = tenant, ParentId = a + 99, SortKey = 3 },
        ]).Insert();

        var rows = ctx.From<T251SingleNullableChild>()
            .Where(c => c.Id >= a + 130 && c.Id <= a + 132)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, ParentRow = (int?)c.Parent!.RowId })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(a + 130, a + 131, a + 132);
        rows.Select(r => r.ParentRow).Should().Equal(new int?[] { null, a, null },
            "a null or dangling single-column foreign key yields no entity and a match resolves");
    }

    /// <summary>
    /// Composite foreign key with exactly one nullable component: a null trailing component breaks the
    /// key even when the other component matches, while the fully non-null pair resolves.
    /// </summary>
    [Fact]
    public void T251_CompositeReferenceWithOneNullableComponent_ShouldYieldNoEntityOnTheLiveDatabase()
    {
        var ctx = _sut.DataProvider;
        var (parent, tenant, link, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredNullable(ctx);
        var a = parent;

        ctx.CreateInsertBuilder<T251PartialNullableChild>().Values([
            new T251PartialNullableChild { Id = a + 140, Tenant = tenant, ParentId = link, SortKey = 1 },
            new T251PartialNullableChild { Id = a + 141, Tenant = tenant, ParentId = null, SortKey = 2 },
            new T251PartialNullableChild { Id = a + 142, Tenant = tenant + 1, ParentId = link, SortKey = 3 },
        ]).Insert();

        var rows = ctx.From<T251PartialNullableChild>()
            .Where(c => c.Id >= a + 140 && c.Id <= a + 142)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, ParentRow = (int?)c.Parent!.RowId })
            .ToList();

        rows.Select(r => r.ParentRow).Should().Equal(new int?[] { a, null, null },
            "the null component yields no entity and a partial component match never links");
    }

    /// <summary>
    /// Tenant collision and occurrence scope: the same local id in another tenant must not be linked in
    /// either direction, and duplicate many-to-many links stay within their own parent occurrence.
    /// </summary>
    [Fact]
    public void T251_TenantCollision_ShouldNotMixEntitiesOrOccurrenceScopes()
    {
        var ctx = _sut.DataProvider;
        var (a, b, c) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);
        var (first, _) = JoinIntoCompositeJunctionFixtures.SeedDeclared(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .JoinInto(
                ctx.From<T251DeclaredChild>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        // a and c share link id l1 but differ in tenant; each keeps only its own children.
        parents[0].Children.Select(ch => ch.Id).Should().BeEquivalentTo([a + 100, a + 101]);
        parents[2].Children.Select(ch => ch.Id).Should().Equal(a + 103);
        parents[0].Children.Select(ch => ch.Tenant).Should().OnlyContain(tenant => tenant == a);
        parents[2].Children.Select(ch => ch.Tenant).Should().OnlyContain(tenant => tenant == a + 1);

        var occurrences = ctx.From<T251DeclaredJunctionParent>()
            .Where(p => p.RowId == first)
            .JoinInto(ctx.From<JiCompositeJunctionChild>(), (p, ch) => true, p => p.Children)
            .ToList();

        occurrences.Should().ContainSingle();
        occurrences[0].Children.Should().HaveCount(2, "the duplicate link is a distinct occurrence of the same parent");
        occurrences[0].Children.Select(ch => ch.Id).Should().OnlyContain(id => id == first + 100);
    }

    /// <summary>
    /// Cold, warm and prepared executions return the same rows; a declaration with the principal
    /// components in another order must not reuse the compatible shape's plan and must change the result.
    /// </summary>
    [Fact]
    public void T251_ColdWarmPrepared_ShouldAgreeAndRejectAnIncompatiblePlan()
    {
        var ctx = _sut.DataProvider;
        var (a, b, c) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);
        ((DataContext)ctx).PurgeQueryCache();

        var cold = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .Select(p => new { p.RowId, Count = p.Children.Count() })
            .ToList();
        var warm = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .Select(p => new { p.RowId, Count = p.Children.Count() })
            .ToList();
        var preparedCommand = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .Select(p => new { p.RowId, Count = p.Children.Count() });
        var prepared = ctx.ToList(((DataContext)ctx).GetPreparedQueryCommand(
            preparedCommand, createEnumerator: false, storeInCache: true, CancellationToken.None));
        cold.Select(r => $"{r.RowId}:{r.Count}").Should().Equal(warm.Select(r => $"{r.RowId}:{r.Count}"));
        prepared.Select(r => $"{r.RowId}:{r.Count}").Should().Equal(cold.Select(r => $"{r.RowId}:{r.Count}"),
            "the prepared/cached execution materialises the same rows as the cold one");

        var correct = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .JoinInto(ctx.From<T251DeclaredChild>(), (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId, p => p.Children)
            .ToCommand();
        var swapped = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .JoinInto(ctx.From<T251DeclaredChild>(), (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId, p => p.SwappedChildren)
            .ToCommand();
        correct.PrepareCommand(CancellationToken.None);
        swapped.PrepareCommand(CancellationToken.None);

        correct.GetQueryPlanEqualityComparer().Equals(correct, swapped)
            .Should().BeFalse("a different principal component order is a different mapping and plan");

        var correctRows = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .JoinInto(ctx.From<T251DeclaredChild>(), (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId, p => p.Children)
            .ToList();
        var swappedRows = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .JoinInto(ctx.From<T251DeclaredChild>(), (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId, p => p.SwappedChildren)
            .ToList();

        correctRows[0].Children.Select(ch => ch.Id).Should().BeEquivalentTo([a + 100, a + 101]);
        swappedRows[0].SwappedChildren.Should().BeEmpty(
            "pairing (tenant, parent_id) with the swapped (id, tenant) principal matches nothing");
    }

    /// <summary>
    /// Shared-command hygiene: after a composite navigation query, the context-shared <c>Any</c>-style
    /// commands keep their plan cache (no sticky <c>Cache=false</c>) and a neighbouring query still runs.
    /// </summary>
    [Fact]
    public void T251_SharedContextAnyAndCount_ShouldKeepCachingAfterACompositeQuery()
    {
        var ctx = _sut.DataProvider;
        var (a, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);

        var rows = ctx.From<T251DeclaredChild>()
            .Where(ch => ch.Id == a + 100)
            .Select(ch => new { ch.Id, Parent = (int?)ch.Parent!.RowId })
            .ToList();
        rows.Should().ContainSingle();
        rows[0].Parent.Should().Be(a);

        var childrenOfA = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .Select(p => p.Children.Count())
            .ToList();
        childrenOfA.Should().Equal(new[] { 2 }, "the composite collection still materialises after the reference query");

        var hasChildren = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .Any();
        hasChildren.Should().BeTrue();

        ((DataContext)ctx).AnyCommand.Should().NotBeNull();
        ((DataContext)ctx).AnyCommand!.Value.Cache.Should().BeTrue(
            "a composite navigation must not disable the context-shared command's plan cache");

        var count = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .Count();
        count.Should().Be(1);

        var neighbour = ctx.From<T251DeclaredChild>()
            .Where(ch => ch.Id == a + 101)
            .Select(ch => ch.Id)
            .ToList();
        neighbour.Should().Equal(a + 101);
        ((DataContext)ctx).AnyCommand!.Value.Cache.Should().BeTrue(
            "the neighbouring query leaves the shared command cache enabled");
    }
}
