using FluentAssertions;
using NextORM.Core;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static int _eagerIdSeed = -1_000_000;

    private static int NextEagerId() => Interlocked.Add(ref _eagerIdSeed, -1_000);

    [Fact]
    public void EagerLoading_LoadWith_FillsCollectionsForZeroOneAndManyChildren()
    {
        var ctx = _sut.DataProvider;
        var zero = NextEagerId();
        var one = zero + 1;
        var many = zero + 2;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = zero, Name = "zero" },
            new EagerParent { Id = one, Name = "one" },
            new EagerParent { Id = many, Name = "many" },
        ]).Insert();

        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = zero + 100, ParentId = one, Name = "one-a" },
            new EagerChild { Id = zero + 103, ParentId = many, Name = "many-c" },
            new EagerChild { Id = zero + 101, ParentId = many, Name = "many-a" },
            new EagerChild { Id = zero + 104, ParentId = many, Name = "many-d" },
            new EagerChild { Id = zero + 102, ParentId = many, Name = "many-b" },
        ]).Insert();

        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == zero || p.Id == one || p.Id == many)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(zero, one, many);
        parents[0].Children.Should().BeEmpty();
        parents[1].Children.Select(c => c.Name).Should().Equal("one-a");
        parents[2].Children.Select(c => c.Name).Should().Equal("many-a", "many-b", "many-c", "many-d");
    }

    [Fact]
    public void EagerLoading_LoadWith_GroupsChildrenPerParentWithoutCrossAssignment()
    {
        var ctx = _sut.DataProvider;
        var first = NextEagerId();
        var second = first + 1;
        var orphan = first + 2;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = first, Name = "first" },
            new EagerParent { Id = second, Name = "second" },
        ]).Insert();

        var firstA = first + 100;
        var firstB = first + 101;
        var secondA = first + 102;
        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = firstA, ParentId = first, Name = "first-a" },
            new EagerChild { Id = secondA, ParentId = second, Name = "second-a" },
            new EagerChild { Id = firstB, ParentId = first, Name = "first-b" },
            new EagerChild { Id = first + 103, ParentId = orphan, Name = "orphan" },
        ]).Insert();

        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == first || p.Id == second)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(first, second);
        parents[0].Children.Select(c => c.Id).Should().Equal(firstA, firstB);
        parents[1].Children.Select(c => c.Id).Should().Equal(secondA);
        parents.SelectMany(p => p.Children).Should().HaveCount(3);
        parents.SelectMany(p => p.Children).Select(c => c.ParentId).Distinct().Should().Equal(first, second);
    }

    [Fact]
    public void EagerLoading_LoadWith_NoMatchingParents_ReturnsEmptyList()
    {
        var ctx = _sut.DataProvider;
        var missing = NextEagerId();

        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == missing)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .ToList();

        parents.Should().BeEmpty();
    }

    [Fact]
    public async Task EagerLoading_LoadWithAsync_FillsCollections()
    {
        var ctx = _sut.DataProvider;
        var parent = NextEagerId();
        var childA = parent + 100;
        var childB = parent + 101;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = parent, Name = "async" },
        ]).Insert();

        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = childA, ParentId = parent, Name = "async-a" },
            new EagerChild { Id = childB, ParentId = parent, Name = "async-b" },
        ]).Insert();

        var parents = await ctx.From<EagerParent>()
            .Where(p => p.Id == parent)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(childA, childB);
    }

    // --- #107 single-query eager loading (EagerLoadMode.SingleQuery) and terminal stitching: cross-provider coverage.
    // The 1001-key cases force the split path beyond its 1000-key chunk boundary and prove the single-query
    // path stays one command without falling back to split (no chunked IN list, no N+1).

    private static int _eagerBulkIdSeed = -100_000_000;

    private static int NextEagerBulkBase() => Interlocked.Add(ref _eagerBulkIdSeed, -100_000);

    /// <summary>
    /// Inserts <paramref name="rows"/> in small batch multi-row inserts: a single 1001-row insert would
    /// exceed the SQL Server 2100-parameter limit (and SQLite's variable limit) once the child rows carry
    /// three columns.
    /// </summary>
    private static void InsertEagerRows<T>(IDataContext ctx, IReadOnlyList<T> rows)
    {
        const int batchSize = 250;
        for (var offset = 0; offset < rows.Count; offset += batchSize)
        {
            var count = Math.Min(batchSize, rows.Count - offset);
            var batch = new List<T>(count);
            for (var i = 0; i < count; i++)
                batch.Add(rows[offset + i]);

            ctx.CreateInsertBuilder<T>().Values(batch).Insert();
        }
    }

    /// <summary>
    /// Seeds <paramref name="count"/> consecutive parents, each with exactly one child, under fresh id
    /// blocks so the two wide tests cannot collide. Returns the first and last parent id (inclusive).
    /// </summary>
    private (int First, int Last) SeedEagerParentsAndChildren(int count)
    {
        var ctx = _sut.DataProvider;
        var parentBase = NextEagerBulkBase();
        var childBase = NextEagerBulkBase();

        var parents = new List<EagerParent>(count);
        var children = new List<EagerChild>(count);
        for (var i = 0; i < count; i++)
        {
            parents.Add(new EagerParent { Id = parentBase + i, Name = "p" + i });
            children.Add(new EagerChild { Id = childBase + i, ParentId = parentBase + i, Name = "c" + i });
        }

        InsertEagerRows(ctx, parents);
        InsertEagerRows(ctx, children);

        return (parentBase, parentBase + count - 1);
    }

    [Fact]
    public void EagerLoading_Split_1001Keys_ChunksAndGroupsWithoutNPlusOne()
    {
        var (first, last) = SeedEagerParentsAndChildren(1001);

        var interceptor = new CountingQueryInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var parents = _sut.DataProvider.From<EagerParent>()
            .Where(p => p.Id >= first && p.Id <= last)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(1001);
        parents.Select(p => p.Id).Should().OnlyHaveUniqueItems();
        parents.Should().OnlyContain(p => p.Children.Count == 1);
        parents.SelectMany(p => p.Children).Select(c => c.ParentId).Should().Equal(parents.Select(p => p.Id));
        // Split chunks the 1001 distinct keys into two child queries (1000 + 1), plus the parent query:
        // three round trips, never N+1.
        interceptor.Executing.Should().Be(3);
        interceptor.Executed.Should().Be(3);
    }

    [Fact]
    public void EagerLoading_SingleQuery_1001Keys_OneCommandGroupsWithoutDuplicateParents()
    {
        var (first, last) = SeedEagerParentsAndChildren(1001);

        var interceptor = new CountingQueryInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var parents = _sut.DataProvider.From<EagerParent>()
            .Where(p => p.Id >= first && p.Id <= last)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(1001);
        parents.Select(p => p.Id).Should().OnlyHaveUniqueItems();
        parents.Should().OnlyContain(p => p.Children.Count == 1);
        parents.SelectMany(p => p.Children).Select(c => c.ParentId).Should().Equal(parents.Select(p => p.Id));
        // One denormalized command, no chunked IN list beyond the split chunk size: exactly one round trip.
        interceptor.Executing.Should().Be(1, "SingleQuery must not silently fall back to the chunked split path");
        interceptor.Executed.Should().Be(1);
    }

    [Fact]
    public void EagerLoading_SingleQuery_ChildFilter_KeepsChildlessParents()
    {
        var ctx = _sut.DataProvider;
        var keep = NextEagerId();
        var drop = keep + 1;
        var none = keep + 2;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = keep, Name = "keep" },
            new EagerParent { Id = drop, Name = "drop" },
            new EagerParent { Id = none, Name = "none" },
        ]).Insert();

        var keptChild = keep + 100;
        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = keptChild, ParentId = keep, Name = "keep" },
            new EagerChild { Id = keep + 101, ParentId = keep, Name = "drop" },
            new EagerChild { Id = keep + 102, ParentId = drop, Name = "drop" },
        ]).Insert();

        var interceptor = new CountingQueryInterceptor();
        ((DataContext)ctx).AddInterceptor(interceptor);

        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == keep || p.Id == drop || p.Id == none)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().Where(x => x.Name == "keep"), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(keep, drop, none);
        // The child's own Where is folded into the join ON predicate and filters the children.
        parents[0].Children.Select(c => c.Id).Should().Equal(keptChild);
        // LEFT semantics: a parent whose only children were filtered out is kept, with an empty collection.
        parents[1].Children.Should().BeEmpty();
        parents[2].Children.Should().BeEmpty();
        interceptor.Executing.Should().Be(1);
    }

    [Fact]
    public void EagerLoading_SingleQuery_TwoSpecs_OneCommandWithoutCrossContamination()
    {
        // Reuses the JoinInto fixtures (many: two children + two notes; one: a child only) and adds a
        // note-only parent, so every combination is present to detect cross-assignment between collections.
        var (many, one, none) = SeedJoinInto();

        _sut.DataProvider.CreateInsertBuilder<JoinIntoNote>().Values([
            new JoinIntoNote { Id = none + 200, ParentId = none, Text = "none-n1" },
        ]).Insert();

        var interceptor = new CountingQueryInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .LoadWith(p => p.Children, c => c.From<JoinIntoChild>(), p => p.Id, c => c.ParentId)
            .LoadWith(p => p.Notes, n => n.From<JoinIntoNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents.Select(p => p.Id).Should().OnlyHaveUniqueItems();

        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([many + 100, many + 101]);
        parents[0].Notes.Select(n => n.Id).Should().BeEquivalentTo([many + 200, many + 201]);
        parents[1].Children.Select(c => c.Id).Should().BeEquivalentTo([many + 102]);
        parents[1].Notes.Should().BeEmpty("a child must never leak into the notes collection");
        parents[2].Children.Should().BeEmpty("a note must never leak into the children collection");
        parents[2].Notes.Select(n => n.Id).Should().BeEquivalentTo([none + 200]);

        // Both collections are stitched from the same denormalized command.
        interceptor.Executing.Should().Be(1);
        interceptor.Executed.Should().Be(1);
    }

    [Fact]
    public void EagerLoading_SingleQuery_DuplicateParentKeys_ShareChildren()
    {
        var ctx = _sut.DataProvider;
        var first = NextEagerId();
        var second = first + 1;
        var sharedName = "dup-" + first;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = first, Name = sharedName },
            new EagerParent { Id = second, Name = sharedName },
        ]).Insert();

        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = first + 100, ParentId = first, Name = sharedName },
            new EagerChild { Id = first + 101, ParentId = second, Name = sharedName },
        ]).Insert();

        // Key on a non-primary, duplicated property: both parents must keep their own identity (distinct
        // rows) while sharing the children that match the duplicate key. The coalesce keeps TKey
        // non-nullable (the column is nullable).
        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == first || p.Id == second)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Name ?? "", c => c.Name ?? "", EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Select(p => p.Id).Should().OnlyHaveUniqueItems();
        parents.Should().OnlyContain(p => p.Children.Count == 2);
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([first + 100, first + 101]);
    }

    [Fact]
    public async Task EagerLoading_Split_ToArrayAndToArrayAsync_Stitch()
    {
        var ctx = _sut.DataProvider;
        var parent = NextEagerId();

        ctx.CreateInsertBuilder<EagerParent>().Values([new EagerParent { Id = parent, Name = "array" }]).Insert();
        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = parent + 100, ParentId = parent, Name = "a" },
            new EagerChild { Id = parent + 101, ParentId = parent, Name = "b" },
        ]).Insert();

        var builder = ctx.From<EagerParent>()
            .Where(p => p.Id == parent)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId);

        var array = builder.ToArray();
        array.Should().ContainSingle();
        array[0].Children.Select(c => c.Id).Should().Equal(parent + 100, parent + 101);

        var asyncArray = await builder.ToArrayAsync(TestContext.Current.CancellationToken);
        asyncArray.Should().ContainSingle();
        asyncArray[0].Children.Select(c => c.Id).Should().Equal(parent + 100, parent + 101);
    }

    [Fact]
    public async Task EagerLoading_SingleQuery_ToArrayAndToArrayAsync_Stitch()
    {
        var ctx = _sut.DataProvider;
        var parent = NextEagerId();

        ctx.CreateInsertBuilder<EagerParent>().Values([new EagerParent { Id = parent, Name = "array" }]).Insert();
        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = parent + 100, ParentId = parent, Name = "a" },
            new EagerChild { Id = parent + 101, ParentId = parent, Name = "b" },
        ]).Insert();

        var builder = ctx.From<EagerParent>()
            .Where(p => p.Id == parent)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery);

        var array = builder.ToArray();
        array.Should().ContainSingle();
        array[0].Children.Select(c => c.Id).Should().Equal(parent + 100, parent + 101);

        var asyncArray = await builder.ToArrayAsync(TestContext.Current.CancellationToken);
        asyncArray.Should().ContainSingle();
        asyncArray[0].Children.Select(c => c.Id).Should().Equal(parent + 100, parent + 101);
    }

    // --- D14a: split vs single eager-load filter-scope inheritance. Alias types over the same
    // --- eager_parent/eager_child tables carry a named filter, so no other fixture is affected (the
    // --- first registration per type wins process-wide).

    private static void RegisterFilteredEagerFilters(IDataContext ctx)
    {
        ctx.From<FilteredEagerParent>(b => b.HasQueryFilter("soft", (p, _) => p.Name != "hidden"));
        ctx.From<FilteredEagerChild>(b => b.HasQueryFilter("soft", (c, _) => c.Name != "hidden-child"));
    }

    [Fact]
    public void EagerLoading_Split_vs_Single_FilterScope_Inheritance_ShouldMatch()
    {
        var ctx = _sut.DataProvider;
        var visible = NextEagerId();
        var hidden = visible + 1;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = visible, Name = "visible" },
            new EagerParent { Id = hidden, Name = "hidden" },
        ]).Insert();
        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = visible + 100, ParentId = visible, Name = "visible-child" },
            new EagerChild { Id = visible + 101, ParentId = visible, Name = "hidden-child" },
            new EagerChild { Id = visible + 102, ParentId = hidden, Name = "other-child" },
        ]).Insert();

        RegisterFilteredEagerFilters(ctx);

        string Run(bool single, bool parentIgnore, bool childIgnore)
        {
            var builder = ctx.From<FilteredEagerParent>()
                .Where(p => p.Id == visible || p.Id == hidden)
                .LoadWith(
                    p => p.Children,
                    c => childIgnore ? c.From<FilteredEagerChild>().IgnoreFilters(["soft"]) : c.From<FilteredEagerChild>(),
                    p => p.Id,
                    c => c.ParentId,
                    single ? EagerLoadMode.SingleQuery : EagerLoadMode.SplitQuery);
            if (parentIgnore)
                builder = builder.IgnoreFilters(["soft"]);

            var parents = builder.OrderBy(p => p.Id).ToList();
            // Child order is not part of the contract (the split and join queries have no ORDER BY on the
            // children), so compare the collections as sorted sets -- "same rows", not same sequence.
            return string.Join(";", parents.Select(p => p.Id + ":" + string.Join("|", p.Children.Select(c => c.Id).OrderBy(id => id))));
        }

        // Explicit expectations (so the equivalence below is not vacuous): the parent's key-selective
        // scope also names the child's soft filter (union), while the child's own ignore stays local.
        Run(single: false, parentIgnore: false, childIgnore: false).Should().Be($"{visible}:{visible + 100}");
        Run(single: false, parentIgnore: true, childIgnore: false).Should().Be($"{visible}:{visible + 100}|{visible + 101};{hidden}:{visible + 102}");
        Run(single: false, parentIgnore: false, childIgnore: true).Should().Be($"{visible}:{visible + 100}|{visible + 101}");

        for (var parentIgnore = 0; parentIgnore < 2; parentIgnore++)
        {
            for (var childIgnore = 0; childIgnore < 2; childIgnore++)
            {
                Run(false, parentIgnore == 1, childIgnore == 1)
                    .Should().Be(Run(true, parentIgnore == 1, childIgnore == 1), "split and single must apply the same scope union");
            }
        }
    }

    // --- D6: a child filter declared in the builder-function form must be folded into the single-query
    // --- join ON exactly like a predicate child filter.

    [Fact]
    public void EagerLoading_SingleQuery_ChildFuncFilter_KeepsChildlessParents()
    {
        var ctx = _sut.DataProvider;
        var keep = NextEagerId();
        var drop = keep + 1;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = keep, Name = "keep" },
            new EagerParent { Id = drop, Name = "drop" },
        ]).Insert();

        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = keep + 100, ParentId = keep, Name = "visible" },
            new EagerChild { Id = keep + 101, ParentId = keep, Name = "hidden-child" },
            new EagerChild { Id = keep + 102, ParentId = drop, Name = "hidden-child" },
        ]).Insert();

        var parents = ctx.From<FuncFilteredEagerParent>()
            .Where(p => p.Id == keep || p.Id == drop)
            .LoadWith(p => p.Children, c => c.From<FuncFilteredEagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(keep, drop);
        parents[0].Children.Select(c => c.Id).Should().Equal([keep + 100], "the child's function filter is folded into the join ON");
        parents[1].Children.Should().BeEmpty("LEFT semantics keep a parent whose only children were filtered out");
    }
}

[SqlTable("eager_parent")]
public sealed class FilteredEagerParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<FilteredEagerChild> Children { get; } = new List<FilteredEagerChild>();
}

[SqlTable("eager_child")]
public sealed class FilteredEagerChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("eager_parent")]
public sealed class FuncFilteredEagerParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<FuncFilteredEagerChild> Children { get; } = new List<FuncFilteredEagerChild>();
}

[SqlTable("eager_child")]
[QueryFilter(FilterKey = "soft", FilterFunc = nameof(Soft))]
public sealed class FuncFilteredEagerChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public static Func<EntityBuilder<FuncFilteredEagerChild>, IDataContext, EntityBuilder<FuncFilteredEagerChild>> Soft
        => (b, _) => b.Where(c => c.Name != "hidden-child");
}
