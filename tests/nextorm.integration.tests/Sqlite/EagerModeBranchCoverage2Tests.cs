using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Eager-load mode coverage executed against a live SQLite database. Every live fact seeds its own
/// marker rows in <c>eager_parent</c>/<c>eager_child</c> (negative ids, deleted in a <c>finally</c>) and
/// asserts the per-parent grouping explicitly; every guard fact first executes a valid eager load so
/// the rejection is paired with proof that the provider is live.
///
/// <para>
/// Targeted surface: <c>EntityBuilder.LoadWith</c> (Builders/EntityBuilder.cs:642) and the mode
/// resolution it delegates to; <see cref="EagerLoadMode"/> (Default/SplitQuery/SingleQuery/Auto and the
/// rejected Correlated); the per-parent top-N lowering (Builders/EntityBuilderExtensions.cs:520
/// <c>MarkPerParentTopN</c>, Builders/EntityBuilderEagerLoading.cs:974/1000/1020 and the plan-time
/// rejections :348 and :367); the nested (level 2) graph stitching
/// (Builders/EntityBuilderEagerLoading.cs:338/476); the unsupported child-shape names
/// (Builders/EntityBuilder.cs:1207-1281) and the graph guards
/// (Builders/EntityBuilderEagerLoading.cs:1051 <c>ValidateLoadWithForJoinInto</c>, :1420 cyclic,
/// :1450 mixed nested modes); and <c>EagerLoadShape.Equals</c>
/// (Query/QueryCommand.EagerLoading.cs:149) through the plan comparer.
/// </para>
///
/// <para>
/// Not driven here: <c>EagerLoadShape.StructuralHash</c>/<c>GetHashCode</c>
/// (Query/QueryCommand.EagerLoading.cs:130-146,170) and <c>Equals(object)</c> (:167) — the type is
/// <c>internal</c>, the plan comparer uses the typed <c>Equals</c> and <c>AddTo</c> for hashing, and
/// the only other non-empty producer, <c>EagerLoadGraph.ToShape()</c>
/// (Builders/EntityBuilderEagerLoading.cs:1359), has no production caller; <c>EagerLoadGraph</c>'s
/// <c>Find</c>/<c>MaxDepth</c>/<c>IsEmpty</c>/<c>Empty</c> (:1300-1351) and the
/// <c>JoinIntoProjectionFactory.MaxChildCount</c> arity rejection
/// (Builders/EntityBuilderEagerLoading.cs:395-399, requires &gt;7 distinct child collections in one
/// graph) are likewise unreachable from the public API. The in-memory-only rejections
/// (<c>EagerLoadSpec.BuildChildCommand</c>:681, <c>ToJoinIntoSpec</c>:571 with
/// <c>UnsupportedSingleQueryChildShapes</c> on a paged child) stay with the unit suites because the
/// integration project must not use <see cref="InMemoryDataContext"/>.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class EagerModeBranchCoverage2Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    /// <summary>A row id no fixture of the shared SQLite database ever inserts.</summary>
    private const int AbsentParentId = int.MinValue + 3;

    private static int _seed = -1_100_000_000;

    private static int NextBase() => Interlocked.Add(ref _seed, -1_000);

    private IDataContext Context => _sut.DataProvider;

    /// <summary>
    /// Seeds class-owned marker rows: <c>rich</c> with <paramref name="richChildren"/> children,
    /// <c>few</c> with <paramref name="fewChildren"/> and <c>empty</c> with none. Child ids are strictly
    /// increasing so ordering by id is a total order.
    /// </summary>
    private (int Rich, int Few, int Empty) SeedParents(int richChildren, int fewChildren)
    {
        var ctx = Context;
        var rich = NextBase();
        var few = rich + 1;
        var empty = rich + 2;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = rich, Name = "eager-rich" },
            new EagerParent { Id = few, Name = "eager-few" },
            new EagerParent { Id = empty, Name = "eager-empty" },
        ]).Insert();

        var children = new List<EagerChild>(richChildren + fewChildren);
        for (var i = 0; i < richChildren; i++)
            children.Add(new EagerChild { Id = rich + 100 + i, ParentId = rich, Name = "r" + i });
        for (var i = 0; i < fewChildren; i++)
            children.Add(new EagerChild { Id = rich + 150 + i, ParentId = few, Name = "f" + i });

        if (children.Count > 0)
            ctx.CreateInsertBuilder<EagerChild>().Values(children).Insert();

        return (rich, few, empty);
    }

    /// <summary>Seeds one marker parent with two children, each carrying its own grandchildren.</summary>
    private (int Parent, int ChildA, int ChildB) SeedNested()
    {
        var ctx = Context;
        var parent = NextBase();
        var childA = parent + 100;
        var childB = parent + 101;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = parent, Name = "nested" },
        ]).Insert();

        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = childA, ParentId = parent, Name = "a" },
            new EagerChild { Id = childB, ParentId = parent, Name = "b" },
        ]).Insert();

        ctx.CreateInsertBuilder<EagerGrandChild>().Values([
            new EagerGrandChild { Id = parent + 200, ChildId = childA, Name = "ga" },
            new EagerGrandChild { Id = parent + 201, ChildId = childA, Name = "gb" },
            new EagerGrandChild { Id = parent + 202, ChildId = childB, Name = "gc" },
        ]).Insert();

        return (parent, childA, childB);
    }

    private void DeleteMarkers(int rich)
    {
        var ctx = Context;
        ctx.CreateDeleteBuilder<EagerChild>().Where(c => c.Id >= rich + 100 && c.Id <= rich + 299).Delete();
        ctx.CreateDeleteBuilder<EagerParent>().Where(p => p.Id >= rich && p.Id <= rich + 2).Delete();
    }

    private void WithParents(int richChildren, int fewChildren, Action<(int Rich, int Few, int Empty)> body)
    {
        var fixture = SeedParents(richChildren, fewChildren);
        try
        {
            body(fixture);
        }
        finally
        {
            DeleteMarkers(fixture.Rich);
        }
    }

    private void WithNested(Action<(int Parent, int ChildA, int ChildB)> body)
    {
        var fixture = SeedNested();
        try
        {
            body(fixture);
        }
        finally
        {
            DeleteMarkers(fixture.Parent);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Default mode
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>EagerLoadMode.Default</c> is the inheritance sentinel: the load resolves to the split path and
    /// fills each parent's own collection.
    /// </summary>
    [Fact]
    public void LoadWith_DefaultMode_ShouldFillCollectionsPerParent()
    {
        WithParents(3, 1, f =>
        {
            var parents = Context.From<EagerParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Empty)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId, EagerLoadMode.Default)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(f.Rich, f.Few, f.Empty);
            parents[0].Children.Select(c => c.Id).Should().Equal(f.Rich + 100, f.Rich + 101, f.Rich + 102);
            parents[1].Children.Select(c => c.Id).Should().Equal(f.Rich + 150);
            parents[2].Children.Should().BeEmpty();
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Explicit SingleQuery versus explicit SplitQuery round trips
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The explicit modes select the execution shape: <c>SingleQuery</c> stitches the denormalized
    /// command once, <c>SplitQuery</c> runs the parent plus one chunked child query.
    /// </summary>
    [Fact]
    public void LoadWith_ExplicitSingleQuery_ShouldStitchOneCommand_AndSplitShouldUseTwo()
    {
        WithParents(2, 1, f =>
        {
            var single = new CountingInterceptor();
            ((DataContext)Context).AddInterceptor(single);

            var singleParents = Context.From<EagerParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Few)
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            singleParents.Select(p => p.Id).Should().Equal(f.Rich, f.Few);
            singleParents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([f.Rich + 100, f.Rich + 101]);
            singleParents[1].Children.Select(c => c.Id).Should().Equal(f.Rich + 150);
            single.Executing.Should().Be(1, "SingleQuery is one denormalized round trip");

            var split = new CountingInterceptor();
            ((DataContext)Context).AddInterceptor(split);

            var splitParents = Context.From<EagerParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Few)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();

            splitParents[0].Children.Select(c => c.Id).Should().Equal(f.Rich + 100, f.Rich + 101);
            splitParents[1].Children.Select(c => c.Id).Should().Equal(f.Rich + 150);
            split.Executing.Should().Be(2, "SplitQuery is the parent statement plus one batched child statement");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Auto dispatches to SplitQuery
    // ---------------------------------------------------------------------------------------------

    /// <summary>The conservative Auto rule resolves to the split path, so Auto and SplitQuery materialize identically.</summary>
    [Fact]
    public void LoadWith_AutoMode_ShouldMatchExplicitSplitQuery()
    {
        WithParents(2, 1, f =>
        {
            List<string> Run(EagerLoadMode mode)
            {
                var parents = Context.From<EagerParent>()
                    .Where(p => p.Id >= f.Rich && p.Id <= f.Empty)
                    .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId, mode)
                    .OrderBy(p => p.Id)
                    .ToList();
                return parents.Select(p => p.Id + ":" + string.Join("|", p.Children.Select(c => c.Id))).ToList();
            }

            var auto = Run(EagerLoadMode.Auto);
            var split = Run(EagerLoadMode.SplitQuery);

            auto.Should().Equal($"{f.Rich}:{f.Rich + 100}|{f.Rich + 101}", $"{f.Few}:{f.Rich + 150}", $"{f.Empty}:");
            auto.Should().Equal(split, "Auto conservatively dispatches to the split path");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 4-5. Per-parent top-N in both eager modes
    // ---------------------------------------------------------------------------------------------

    /// <summary>A child <c>Limit</c> applies to each parent partition independently on the split path.</summary>
    [Fact]
    public void LoadWith_PerParentTopN_Split_ShouldPageEachParentIndependently()
    {
        WithParents(3, 1, f =>
        {
            var parents = Context.From<EagerParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Empty)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(f.Rich, f.Few, f.Empty);
            parents[0].Children.Select(c => c.Id).Should().Equal(f.Rich + 100, f.Rich + 101);
            parents[1].Children.Select(c => c.Id).Should().Equal(new[] { f.Rich + 150 }, "a global page would starve this parent");
            parents[2].Children.Should().BeEmpty();
        });
    }

    /// <summary>The ranked per-parent page is folded into the single denormalized command.</summary>
    [Fact]
    public void LoadWith_PerParentTopN_SingleQuery_ShouldPageEachParentIndependently()
    {
        WithParents(3, 1, f =>
        {
            var single = new CountingInterceptor();
            ((DataContext)Context).AddInterceptor(single);

            var parents = Context.From<EagerParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Empty)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(f.Rich, f.Few, f.Empty);
            parents[0].Children.Select(c => c.Id).Should().Equal(f.Rich + 100, f.Rich + 101);
            parents[1].Children.Select(c => c.Id).Should().Equal(f.Rich + 150);
            parents[2].Children.Should().BeEmpty();
            single.Executing.Should().Be(1, "the ranked per-parent paging stays one denormalized command");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 6-7. Nested (level two) graphs
    // ---------------------------------------------------------------------------------------------

    /// <summary>A two-level graph on the split path groups each level by its own ancestor.</summary>
    [Fact]
    public void LoadWith_NestedTwoLevelGraph_Split_ShouldGroupGrandChildrenByChild()
    {
        WithNested(f =>
        {
            var parents = Context.From<EagerParent>()
                .Where(p => p.Id == f.Parent)
                .LoadWith(
                    p => p.Children,
                    c => c.From<EagerChild>().OrderBy(x => x.Id)
                        .LoadWith(x => x.GrandChildren, g => g.From<EagerGrandChild>().OrderBy(y => y.Id), x => x.Id, g => g.ChildId),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Should().ContainSingle();
            parents[0].Children.Select(c => c.Id).Should().Equal(f.ChildA, f.ChildB);
            parents[0].Children.Single(c => c.Id == f.ChildA).GrandChildren.Select(g => g.Id)
                .Should().Equal(f.Parent + 200, f.Parent + 201);
            parents[0].Children.Single(c => c.Id == f.ChildB).GrandChildren.Select(g => g.Id)
                .Should().Equal(f.Parent + 202);
        });
    }

    /// <summary>The same two-level graph stitched from one denormalized command.</summary>
    [Fact]
    public void LoadWith_NestedTwoLevelGraph_SingleQuery_ShouldGroupGrandChildrenByChild()
    {
        WithNested(f =>
        {
            var single = new CountingInterceptor();
            ((DataContext)Context).AddInterceptor(single);

            var parents = Context.From<EagerParent>()
                .Where(p => p.Id == f.Parent)
                .LoadWith(
                    p => p.Children,
                    c => c.From<EagerChild>()
                        .LoadWith(x => x.GrandChildren, g => g.From<EagerGrandChild>(), x => x.Id, g => g.ChildId),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Should().ContainSingle();
            parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([f.ChildA, f.ChildB]);
            parents[0].Children.Single(c => c.Id == f.ChildA).GrandChildren.Select(g => g.Id)
                .Should().BeEquivalentTo([f.Parent + 200, f.Parent + 201]);
            parents[0].Children.Single(c => c.Id == f.ChildB).GrandChildren.Select(g => g.Id)
                .Should().Equal(f.Parent + 202);
            single.Executing.Should().Be(1, "the whole nested graph stitches from one denormalized command");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Correlated mode guard
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// An explicit <see cref="EagerLoadMode.Correlated"/> fails closed before any child query is built;
    /// the live counterpart proves the same builder shape works in SplitQuery.
    /// </summary>
    [Fact]
    public void LoadWith_CorrelatedMode_ShouldBeRejectedBeforeAnyChildQuery()
    {
        WithParents(1, 1, f =>
        {
            var live = Context.From<EagerParent>()
                .Where(p => p.Id == f.Rich)
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                .ToList();
            live.Should().ContainSingle();
            live[0].Children.Should().ContainSingle();

            var act = () => Context.From<EagerParent>()
                .Where(p => p.Id == f.Rich)
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Correlated)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*Correlated is not supported in this release*");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 9. Per-parent top-N on a non-list terminal guard
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A per-parent top-N is accepted only by the list terminals; a scalar terminal rejects it before the
    /// parent statement runs.
    /// </summary>
    [Fact]
    public void LoadWith_PerParentTopN_OnNonListTerminal_ShouldBeRejected()
    {
        WithParents(2, 1, f =>
        {
            Context.From<EagerParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Few)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
                .ToList()
                .SelectMany(p => p.Children)
                .Should().HaveCount(2, "the split list terminal applies the child page per parent");

            var act = () => Context.From<EagerParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Few)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
                .Count();

            act.Should().Throw<NotSupportedException>().WithMessage("*per-parent top-N requires correlated execution*");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Unsupported per-parent paging shapes guard
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A per-parent page needs a window ordering and cannot carry WITH TIES; both shapes fail closed at
    /// preparation, before the parent statement.
    /// </summary>
    [Fact]
    public void LoadWith_PerParentPaging_UnsupportedShapes_ShouldBeRejected()
    {
        WithParents(2, 1, f =>
        {
            var live = Context.From<EagerParent>()
                .Where(p => p.Id == f.Rich)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
                .ToList();
            live[0].Children.Should().ContainSingle();

            var withoutOrdering = () => Context.From<EagerParent>()
                .Where(p => p.Id == f.Rich)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().Limit(1), p => p.Id, c => c.ParentId)
                .ToList();

            withoutOrdering.Should().Throw<NotSupportedException>().WithMessage("*without an OrderBy*");

            var withTies = () => Context.From<EagerParent>()
                .Where(p => p.Id == f.Rich)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1).WithTies(), p => p.Id, c => c.ParentId)
                .ToList();

            withTies.Should().Throw<NotSupportedException>().WithMessage("*also uses WithTies*");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 11. SingleQuery fold shapes and nested-graph guards
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The single-query fold accepts only the child's own <c>Where</c>: a <c>Distinct</c> child, a nested
    /// graph that also pages per parent, a nested declaration whose explicit mode conflicts with the
    /// graph-wide mode, and a cyclic declaration all fail closed; the live counterpart is a valid nested
    /// single-query load.
    /// </summary>
    [Fact]
    public void LoadWith_SingleQueryAndNestedGraphGuards_ShouldBeRejected()
    {
        WithNested(f =>
        {
            var live = Context.From<EagerParent>()
                .Where(p => p.Id == f.Parent)
                .LoadWith(
                    p => p.Children,
                    c => c.From<EagerChild>()
                        .LoadWith(x => x.GrandChildren, g => g.From<EagerGrandChild>(), x => x.Id, g => g.ChildId),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SingleQuery)
                .ToList();
            live.Should().ContainSingle();
            live[0].Children.Should().HaveCount(2);

            var distinctChild = () => Context.From<EagerParent>()
                .Where(p => p.Id == f.Parent)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().Distinct(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .ToList();

            distinctChild.Should().Throw<NotSupportedException>().WithMessage("*cannot fold the child query's Distinct*");

            var nestedPaged = () => Context.From<EagerParent>()
                .Where(p => p.Id == f.Parent)
                .LoadWith(
                    p => p.Children,
                    c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1)
                        .LoadWith(x => x.GrandChildren, g => g.From<EagerGrandChild>(), x => x.Id, g => g.ChildId),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SingleQuery)
                .ToList();

            nestedPaged.Should().Throw<NotSupportedException>().WithMessage("*per-parent top-N requires correlated execution*");

            var nestedDistinct = () => Context.From<EagerParent>()
                .Where(p => p.Id == f.Parent)
                .LoadWith(
                    p => p.Children,
                    c => c.From<EagerChild>().Distinct()
                        .LoadWith(x => x.GrandChildren, g => g.From<EagerGrandChild>(), x => x.Id, g => g.ChildId),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SingleQuery)
                .ToList();

            nestedDistinct.Should().Throw<NotSupportedException>().WithMessage("*cannot fold the child query's Distinct*");

            var mixedModes = () => Context.From<EagerParent>()
                .Where(p => p.Id == f.Parent)
                .LoadWith(
                    p => p.Children,
                    c => c.From<EagerChild>()
                        .LoadWith(x => x.GrandChildren, g => g.From<EagerGrandChild>(), x => x.Id, g => g.ChildId, EagerLoadMode.SingleQuery),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SplitQuery)
                .ToList();

            mixedModes.Should().Throw<NotSupportedException>().WithMessage("*mixes modes*");

            var cyclic = () => Context.From<EagerSelfChild>()
                .Where(c => c.Id == f.Parent)
                .LoadWith(
                    x => x.Children,
                    c => c.From<EagerSelfChild>()
                        .LoadWith(y => y.Children, g => g.From<EagerSelfChild>(), y => y.Id, g => g.ParentId),
                    x => x.Id,
                    c => c.ParentId)
                .ToList();

            cyclic.Should().Throw<NotSupportedException>().WithMessage("*is cyclic*");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 12. The eager-load graph is part of the plan key
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>QueryCommand.EagerLoadShape</c> separates the plans of two otherwise identical parent commands
    /// whose eager graphs differ (the typed <c>EagerLoadShape.Equals</c> mismatch arm), while an
    /// identical graph keeps one plan key.
    /// </summary>
    [Fact]
    public void EagerLoadShape_PlanKey_ShouldSeparateDifferentGraphs()
    {
        var byId = Context.From<EagerParent>()
            .Where(p => p.Id == AbsentParentId)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
            .ToCommand();
        var byIdAgain = Context.From<EagerParent>()
            .Where(p => p.Id == AbsentParentId)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
            .ToCommand();
        var byName = Context.From<EagerParent>()
            .Where(p => p.Id == AbsentParentId)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Name ?? "", c => c.Name ?? "")
            .ToCommand();

        var comparer = byId.GetQueryPlanEqualityComparer();

        comparer.Equals(byId, byIdAgain).Should().BeTrue("an identical eager graph keeps one plan key");
        comparer.Equals(byId, byName).Should()
            .BeFalse("a different eager graph must not alias the plan (EagerLoadShape.Equals mismatch arm)");
        comparer.GetHashCode(byId).Should().NotBe(comparer.GetHashCode(byName));

        Context.From<EagerParent>()
            .Where(p => p.Id == AbsentParentId)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
            .ToList()
            .Should().BeEmpty("the probed graph executes against the live database");
    }

    /// <summary>
    /// Self-referencing marker fixture over <c>eager_child</c>: the same structural node nested below
    /// itself is what makes the declared graph cyclic.
    /// </summary>
    [SqlTable("eager_child")]
    public sealed class EagerSelfChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<EagerSelfChild> Children { get; } = new List<EagerSelfChild>();
    }

    /// <summary>Counts the commands a query executes, to distinguish the single-query and split modes.</summary>
    private sealed class CountingInterceptor : IQueryInterceptor
    {
        private int _executing;

        /// <summary>The number of commands whose execution started.</summary>
        public int Executing => Volatile.Read(ref _executing);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executing);

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
        {
        }

        public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception)
        {
        }
    }
}
