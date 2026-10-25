using System.Linq.Expressions;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Postgres;

namespace NextORM.Core.Tests;

/// <summary>
/// #309 rv1 D2: a fluent <c>Where</c> declared on the <c>JoinInto</c> child source must constrain the
/// joined children, and every child-query shape that cannot be folded into the join's <c>ON</c>
/// condition must be rejected at declaration time instead of being silently dropped.
/// <para>
/// Every supported-shape assertion reads the materialized child collections <b>by value</b>: the defect
/// returns plausible rows, so only rows (never SQL text or a bare count) can prove that the child
/// predicate reached the join. The composed predicate is additionally counted once on the declaration's
/// join condition, so a forwarding overload cannot fold the same predicate twice.
/// </para>
/// <para>
/// Every unsupported-shape row is exercised on its own modifier (a single combined invalid builder
/// cannot establish the guards) and asserts that the thrown <see cref="NotSupportedException"/> names the
/// offending shape.
/// </para>
/// </summary>
public class JoinIntoChildWhereTests
{
    private const string Keep = "keep";
    private const string Skip = "skip";

    public JoinIntoChildWhereTests() => DataContextCache.Clear();

    // ---------------------------------------------------------------- supported shapes

    /// <summary>
    /// REQ-309-01: the child <c>Where</c> filters the joined children; the excluded child 9513
    /// (<c>Tag = "skip"</c>) must never materialize.
    /// </summary>
    [Fact]
    public void ChildWhere_CollectionLeft_ShouldFilterChildrenByValue()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwParent>()
            .JoinInto(ctx.From<CwChild>().Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(9501, 9502, 9503);
        ChildrenOf(parents, 9501).Should().Equal(new[] { 9511, 9512 }, "child 9513 is excluded by the child Where");
        ChildrenOf(parents, 9502).Should().Equal(new[] { 9521 }, "child 9522 is excluded by the child Where");
        ChildrenOf(parents, 9503).Should().BeEmpty("a LEFT join keeps a childless parent with an empty collection");
    }

    /// <summary>
    /// REQ-309-02: an INNER join drops a parent whose every child is filtered out (9502) and a parent
    /// with no children at all (9503), while the child <c>Where</c> still filters each surviving parent's
    /// children; the LEFT form keeps both parents.
    /// </summary>
    [Fact]
    public void ChildWhere_CollectionInner_ShouldExcludeParentsWithoutSurvivingChildren()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwParent>()
            .JoinInto(ctx.From<CwChild>().Where(c => c.Tag != Skip && c.Id != 9521), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(new[] { 9501 }, "9502 loses both children to the filter; 9503 has none");
        ChildrenOf(parents, 9501).Should().Equal(new[] { 9511, 9512 }, "9513 is excluded by the child Where");
    }

    /// <summary>REQ-309-01: a repeated child <c>Where</c> stays AND-composed (both predicates apply).</summary>
    [Fact]
    public void ChildWhere_Repeated_ShouldBeAndComposed()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwParent>()
            .JoinInto(
                ctx.From<CwChild>().Where(c => c.Tag != Skip).Where(c => c.Id != 9512),
                (p, c) => p.Id == c.ParentId,
                p => p.Children)
            .ToList();

        ChildrenOf(parents, 9501).Should().Equal(new[] { 9511 }, "9513 fails the first Where, 9512 the second");
        ChildrenOf(parents, 9502).Should().Equal(9521);
    }

    /// <summary>
    /// REQ-309-01/REQ-309-05: the caller predicate, the relationship keys and the child predicate are all
    /// effective at once — no one of them may swallow another.
    /// </summary>
    [Fact]
    public void ChildWhere_WithCallerPredicate_ShouldComposeAllEffective()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwParent>()
            .JoinInto(
                ctx.From<CwChild>().Where(c => c.Tag != Skip),
                (p, c) => p.Id == c.ParentId && p.Id != 9502,
                p => p.Children)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(new[] { 9501, 9502, 9503 }, "LEFT keeps every parent, including 9502 whose children the caller predicate rejects");
        ChildrenOf(parents, 9501).Should().Equal(new[] { 9511, 9512 }, "the relationship key matches and the child Where filters");
        ChildrenOf(parents, 9502).Should().BeEmpty("the caller predicate rejects 9502's children, so the preserved parent stays empty");
        ChildrenOf(parents, 9503).Should().BeEmpty();
    }

    /// <summary>REQ-309-02/REQ-309-03: the reference overloads fold the child <c>Where</c> too.</summary>
    [Fact]
    public void ChildWhere_ReferenceLeft_ShouldLeaveNullForAFilteredOnlyChild()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwParent>()
            .JoinInto(ctx.From<CwProfile>().Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.Profile)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(9501, 9502, 9503);
        parents.Single(p => p.Id == 9501).Profile!.Id.Should().Be(9531);
        parents.Single(p => p.Id == 9502).Profile.Should().BeNull("its only profile is filtered out by the child Where");
        parents.Single(p => p.Id == 9503).Profile.Should().BeNull();
    }

    /// <summary>REQ-309-02: the reference INNER overload excludes a parent whose only child is filtered.</summary>
    [Fact]
    public void ChildWhere_ReferenceInner_ShouldExcludeFilteredChildParents()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwParent>()
            .JoinInto(ctx.From<CwProfile>().Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.Profile, JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(9501);
        parents[0].Profile!.Id.Should().Be(9531);
    }

    /// <summary>REQ-309-03/REQ-309-07: the explicit key overload filters children and still matches by key.</summary>
    [Fact]
    public void ChildWhere_ExplicitKeys_ShouldFilterChildrenAndMatchByKeys()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwParent>()
            .JoinInto(
                ctx.From<CwChild>().Where(c => c.Tag != Skip),
                (p, c) => p.Id == c.ParentId,
                p => p.Children,
                p => p.Id,
                c => c.ParentId)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(9501, 9502, 9503);
        ChildrenOf(parents, 9501).Should().Equal(9511, 9512);
        ChildrenOf(parents, 9502).Should().Equal(9521);
        ChildrenOf(parents, 9503).Should().BeEmpty();
    }

    /// <summary>REQ-309-05: the many-to-many declaration folds the child <c>Where</c> as well.</summary>
    [Fact]
    public void ChildWhere_ManyToMany_ShouldFilterChildren()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwM2MParent>()
            .JoinInto(ctx.From<CwM2MChild>().Where(c => c.Tag != Skip), (p, c) => true, p => p.Children)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(9601, 9602, 9603);
        parents.Single(p => p.Id == 9601).Children.Select(c => c.Id).OrderBy(id => id)
            .Should().Equal(new[] { 9611, 9612 }, "child 9613 is excluded by the child Where");
        parents.Single(p => p.Id == 9602).Children.Select(c => c.Id).OrderBy(id => id)
            .Should().Equal(new[] { 9621 }, "child 9622 is excluded by the child Where");
        parents.Single(p => p.Id == 9603).Children.Should().BeEmpty();
    }

    /// <summary>
    /// REQ-309-03: all five public overloads and the many-to-many form normalize the child predicate
    /// <b>exactly once</b> — a forwarding overload must not fold the same predicate a second time.
    /// </summary>
    /// <param name="overload">The public overload under test.</param>
    [Theory]
    [InlineData(JoinIntoOverload.CollectionConvenience)]
    [InlineData(JoinIntoOverload.CollectionKind)]
    [InlineData(JoinIntoOverload.ReferenceConvenience)]
    [InlineData(JoinIntoOverload.ReferenceKind)]
    [InlineData(JoinIntoOverload.ExplicitKey)]
    public void ChildWhere_ShouldBeFoldedIntoTheJoinConditionExactlyOnce(JoinIntoOverload overload)
    {
        using var ctx = CreateContext();

        AssertFoldedOnce(Declare(ctx, overload));
    }

    /// <summary>REQ-309-03: the many-to-many declaration normalizes the child predicate exactly once.</summary>
    [Fact]
    public void ChildWhere_ManyToMany_ShouldBeFoldedIntoTheJoinConditionExactlyOnce()
    {
        using var ctx = CreateContext();

        AssertFoldedOnce(DeclareManyToMany(ctx));
    }

    /// <summary>
    /// A bare child source (no <c>Where</c>, no modifier) stays supported: the shape guard must not
    /// reject the form that works today.
    /// </summary>
    [Fact]
    public void BareChildSource_ShouldKeepWorkingUnchanged()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<CwParent>()
            .JoinInto(ctx.From<CwChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(9501, 9502, 9503);
        ChildrenOf(parents, 9501).Should().Equal(9511, 9512, 9513);
        ChildrenOf(parents, 9502).Should().Equal(9521, 9522);
        ChildrenOf(parents, 9503).Should().BeEmpty();
    }

    /// <summary>
    /// r2: <c>As</c> alone is not unsupported. A derived child source that carries no modifier beyond its
    /// own <c>Where</c> is accepted at declaration and stays the join's source, so the subquery applies
    /// that <c>Where</c> itself (the SQL path the compatibility tests exercise).
    /// </summary>
    [Fact]
    public void DerivedChildSource_WithOnlyItsOwnWhere_ShouldBeAcceptedAtDeclaration()
    {
        using var ctx = CreateContext();
        var command = ctx.From<CwChild>().Where(c => c.Tag == Skip).ToCommand();
        var parents = ctx.From<CwParent>();

        var declared = parents.JoinInto(ctx.From(command), (p, c) => p.Id == c.ParentId, p => p.Children);

        ChildEdge(declared).From.SubQuery.Should().BeSameAs(command,
            "the derived child source stays the join source, so its own Where is applied by the subquery");
    }

    /// <summary>
    /// r2: a filtered derived child source stitches exactly the rows its predicate keeps, and its own
    /// predicate is folded into the join condition exactly once.
    /// </summary>
    [Fact]
    public void DerivedChildSource_WithFilteredWhere_ShouldStitchOnlyMatchingRows()
    {
        using var ctx = CreateContext();
        var child = ctx.From(ctx.From<CwChild>().ToCommand()).Where(c => c.Tag != Skip);
        var parents = ctx.From<CwParent>();

        var declared = parents.JoinInto(child, (p, c) => p.Id == c.ParentId, p => p.Children);
        AssertFoldedOnce(declared);

        var rows = declared.ToList();
        rows.Select(p => p.Id).Should().Equal(9501, 9502, 9503);
        ChildrenOf(rows, 9501).Should().Equal(new[] { 9511, 9512 }, "child 9513 is excluded by the derived child's Where");
        ChildrenOf(rows, 9502).Should().Equal(new[] { 9521 }, "child 9522 is excluded by the derived child's Where");
        ChildrenOf(rows, 9503).Should().BeEmpty();
    }

    /// <summary>
    /// r2: an empty derived child source still keeps one row per parent with an empty collection instead
    /// of dropping or repeating parents.
    /// </summary>
    [Fact]
    public void DerivedChildSource_WithEmptyWhere_ShouldKeepEveryParentWithEmptyCollection()
    {
        using var ctx = CreateContext();
        var child = ctx.From(ctx.From<CwChild>().ToCommand()).Where(c => c.Tag == "absent");

        var parents = ctx.From<CwParent>()
            .JoinInto(child, (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(9501, 9502, 9503);
        parents.Should().OnlyContain(p => p.Children.Count == 0, "an empty derived source stitches no child");
    }

    /// <summary>
    /// r2 negative control: an otherwise-supported derived child plus one independently guarded modifier
    /// still throws the frozen <see cref="NotSupportedException"/> naming that modifier, before execution —
    /// whether the modifier is attached to the derived builder or carried inside the derived subquery.
    /// </summary>
    /// <param name="insideSubquery">Whether the modifier sits inside the derived subquery instead of on the derived builder.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DerivedChildSource_WithUnsupportedModifier_ShouldBeRejectedAtDeclaration(bool insideSubquery)
    {
        using var ctx = CreateContext();
        var child = insideSubquery
            ? ctx.From(ctx.From<CwChild>().OrderBy(c => c.Id).ToCommand())
            : ctx.From(ctx.From<CwChild>().ToCommand()).OrderBy(c => c.Id);
        var parents = ctx.From<CwParent>();

        Action act = () => parents.JoinInto(child, (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("JoinInto cannot fold the child query's OrderBy into the join predicate: " +
                "only the child's own Where is supported. Remove the unsupported modifier before calling JoinInto.");
    }

    /// <summary>
    /// 309-E18 (Stryker survivor at <c>EntityBuilder.cs:1335</c>): a derived child source sampled by
    /// <b>ratio alone</b> — the offset keeps its default 0 — is still an unsupported <c>Sample</c> shape.
    /// The <see cref="ChildShape.Sample"/> row cannot see that statement, because it goes through the
    /// derived-builder guard instead of the walk over the child command's own modifiers; a guard that
    /// required both halves of the check would silently accept this sampled child.
    /// </summary>
    [Fact]
    public void DerivedChildSource_WithSampleRatioOnly_ShouldBeRejectedAtDeclaration()
    {
        using var ctx = CreateContext();
        var command = ctx.From<CwChild>(o => o.Sample(0.5)).ToCommand();
        var parents = ctx.From<CwParent>();

        Action act = () => parents.JoinInto(ctx.From(command), (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*JoinInto cannot fold*Sample*");
    }

    // ---------------------------------------------------------------- unsupported shapes

    /// <summary>
    /// REQ-309-04: every child-query modifier that cannot be represented in the join's <c>ON</c>
    /// condition throws <see cref="NotSupportedException"/> at declaration, naming the shape, before any
    /// preparation or execution.
    /// </summary>
    /// <param name="shape">The single child modifier under test.</param>
    [Theory]
    [MemberData(nameof(UnsupportedChildShapes))]
    public void UnsupportedChildShape_ShouldBeRejectedAtDeclaration(ChildShape shape)
    {
        using var ctx = CreateContext();
        var child = ChildWithShape(ctx, shape);
        var parents = ctx.From<CwParent>();

        Action act = () => parents.JoinInto(child, (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*JoinInto cannot fold*{ExpectedShapeName(shape)}*");
    }

    public static IEnumerable<object[]> UnsupportedChildShapes()
        => Enum.GetValues<ChildShape>().Select(shape => new object[] { shape });

    // ---------------------------------------------------------------- helpers

    private static int[] ChildrenOf(IReadOnlyList<CwParent> parents, int parentId)
        => [.. parents.Single(p => p.Id == parentId).Children.Select(c => c.Id).OrderBy(id => id)];

    private static EntityBuilder<CwParent> Declare(InMemoryDataContext ctx, JoinIntoOverload overload)
    {
        var child = ctx.From<CwChild>().Where(c => c.Tag != Skip);
        var profile = ctx.From<CwProfile>().Where(c => c.Tag != Skip);
        var parents = ctx.From<CwParent>();

        return overload switch
        {
            JoinIntoOverload.CollectionConvenience
                => parents.JoinInto(child, (p, c) => p.Id == c.ParentId, p => p.Children),
            JoinIntoOverload.CollectionKind
                => parents.JoinInto(child, (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Left),
            JoinIntoOverload.ReferenceConvenience
                => parents.JoinInto(profile, (p, c) => p.Id == c.ParentId, p => p.Profile),
            JoinIntoOverload.ReferenceKind
                => parents.JoinInto(profile, (p, c) => p.Id == c.ParentId, p => p.Profile, JoinType.Left),
            JoinIntoOverload.ExplicitKey
                => parents.JoinInto(child, (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId),
            _ => throw new ArgumentOutOfRangeException(nameof(overload), overload, "unsupported overload row"),
        };
    }

    private static EntityBuilder<CwM2MParent> DeclareManyToMany(InMemoryDataContext ctx)
        => ctx.From<CwM2MParent>()
            .JoinInto(ctx.From<CwM2MChild>().Where(c => c.Tag != Skip), (p, c) => true, p => p.Children);

    private static void AssertFoldedOnce<TEntity>(EntityBuilder<TEntity> builder)
    {
        var edge = ChildEdge(builder);

        CountChildTagReads(edge.JoinCondition).Should().Be(1,
            "the child Where is folded into the ON condition exactly once");
        CountChildTagReads(edge.OriginalJoinCondition).Should().Be(1,
            "the normalized original condition keeps the same single fold, so preparation cannot overwrite it");
    }

    /// <summary>
    /// The join whose right-hand parameter is the child source (the M2M declaration also contributes a
    /// junction link edge first, so the child edge is not necessarily <c>Joins[0]</c>).
    /// </summary>
    private static JoinExpression ChildEdge<TEntity>(EntityBuilder<TEntity> builder)
    {
        builder.Joins.Should().NotBeNull("a JoinInto declaration contributes at least one join");

        return builder.Joins!.Last(join => join.JoinCondition is { Parameters.Count: 2 } condition
            && condition.Parameters[1].Type.GetProperty("Tag") is not null);
    }

    private static int CountChildTagReads(LambdaExpression? condition)
    {
        if (condition is null)
            return 0;

        var counter = new ChildTagReadCounter();
        counter.Visit(condition.Body);
        return counter.Count;
    }

    /// <summary>Counts reads of the child's <c>Tag</c> member, i.e. how many times the child predicate was folded.</summary>
    private sealed class ChildTagReadCounter : ExpressionVisitor
    {
        public int Count { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.Name == nameof(CwChild.Tag))
                Count++;

            return base.VisitMember(node);
        }
    }

    private static string ExpectedShapeName(ChildShape shape) => shape switch
    {
        ChildShape.OrderBy => "OrderBy",
        ChildShape.Limit or ChildShape.Offset or ChildShape.Page => "Limit/Offset/Page",
        ChildShape.Distinct => "Distinct",
        ChildShape.GroupBy => "GroupBy",
        ChildShape.Having => "Having",
        ChildShape.DistinctOn => "DistinctOn",
        ChildShape.SelectWhereExtreme => "SelectWhereExtreme",
        ChildShape.LimitBy => "LimitBy",
        ChildShape.Join => "Join",
        ChildShape.Final => "Final",
        ChildShape.PreWhere => "PreWhere",
        ChildShape.ArrayJoin => "ArrayJoin",
        ChildShape.TableSample => "TableSample",
        ChildShape.Sample => "Sample",
        ChildShape.ForSystemTime => "ForSystemTime",
        ChildShape.RowLock => "ForUpdate/ForShare",
        ChildShape.SourceLock => "ForUpdate/ForShare OF",
        ChildShape.Window => "Window",
        ChildShape.Settings => "Settings",
        ChildShape.Cte => "Cte",
        ChildShape.TableHint => "TableHint",
        ChildShape.IndexHint => "IndexHint",
        ChildShape.TablesInScopeHint => "TablesInScopeHint",
        ChildShape.NestedLoadWith => "LoadWith",
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unsupported child-shape row"),
    };

    private static EntityBuilder<CwChild> ChildWithShape(InMemoryDataContext ctx, ChildShape shape)
    {
        var plain = ctx.From<CwChild>();

        return shape switch
        {
            ChildShape.OrderBy => plain.OrderBy(c => c.Id),
            ChildShape.Limit => plain.Limit(1),
            ChildShape.Offset => plain.Offset(1),
            ChildShape.Page => plain.Page(1, 1),
            ChildShape.Distinct => plain.Distinct(),
            ChildShape.GroupBy => plain.GroupBy(c => c.ParentId),
            ChildShape.Having => plain.Having(c => c.Id > 0),
            ChildShape.DistinctOn => plain.DistinctOn(c => c.Id),
            ChildShape.SelectWhereExtreme => plain.SelectWhereMax(c => c.Id),
            ChildShape.LimitBy => plain.LimitBy(1, c => c.Id),
            ChildShape.Join => plain.SemiJoin(ctx.From<CwChild>(), (c, other) => c.Id == other.Id),
            ChildShape.Final => plain.Final(),
            ChildShape.PreWhere => plain.PreWhere(c => c.Id > 0),
            ChildShape.ArrayJoin => plain.ArrayJoin(c => c.Values),
            ChildShape.TableSample => ctx.From<CwChild>(o => o.TableSample(50)),
            ChildShape.Sample => ctx.From<CwChild>(o => o.Sample(0.5)),
            ChildShape.ForSystemTime => plain.ForSystemTime(TemporalClause.All()),
            ChildShape.RowLock => plain.ForUpdate(),
            ChildShape.SourceLock => ctx.From<CwChild>(o => o.ForUpdate()),
            ChildShape.Window => plain.Window("w309"),
            ChildShape.Settings => plain.Settings(("max_threads", "1")),
            ChildShape.Cte => ctx.From(ctx.From<CwChild>().ToCommand().AsCte("cw309")),
            ChildShape.TableHint => ctx.From<CwChild>(o => o.WithTableHint("nolock")),
            ChildShape.IndexHint => ctx.From<CwChild>(o => o.WithIndex("cw_ix")),
            ChildShape.TablesInScopeHint => plain.WithTablesInScopeHint("cw_parent"),
            ChildShape.NestedLoadWith => plain.LoadWith(c => c.Notes, n => n.From<CwNote>(), c => c.Id, n => n.ChildId),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unsupported child-shape row"),
        };
    }

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();

        ctx.From<CwParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasOneToOne(p => p.Profile, p => p.Id, c => c.ParentId))
            .WithData([
                new CwParent { Id = 9501, Name = "p9501" },
                new CwParent { Id = 9502, Name = "p9502" },
                new CwParent { Id = 9503, Name = "p9503" },
            ]);
        ctx.From<CwChild>(b => b
                .HasOne(c => c.Parent, c => c.ParentId)
                .HasMany(c => c.Notes, n => n.ChildId))
            .WithData([
                new CwChild { Id = 9511, ParentId = 9501, Tag = Keep },
                new CwChild { Id = 9512, ParentId = 9501, Tag = Keep },
                new CwChild { Id = 9513, ParentId = 9501, Tag = Skip },
                new CwChild { Id = 9521, ParentId = 9502, Tag = Keep },
                new CwChild { Id = 9522, ParentId = 9502, Tag = Skip },
            ]);
        ctx.From<CwProfile>().WithData([
            new CwProfile { Id = 9531, ParentId = 9501, Tag = Keep },
            new CwProfile { Id = 9532, ParentId = 9502, Tag = Skip },
        ]);
        ctx.From<CwNote>().WithData([
            new CwNote { Id = 9541, ChildId = 9511 },
        ]);
        ctx.From<CwM2MParent>(b => b.HasManyThrough<CwM2MChild, CwM2MLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId))
            .WithData([
                new CwM2MParent { Id = 9601, Name = "m9601" },
                new CwM2MParent { Id = 9602, Name = "m9602" },
                new CwM2MParent { Id = 9603, Name = "m9603" },
            ]);
        ctx.From<CwM2MChild>().WithData([
            new CwM2MChild { Id = 9611, Tag = Keep },
            new CwM2MChild { Id = 9612, Tag = Keep },
            new CwM2MChild { Id = 9613, Tag = Skip },
            new CwM2MChild { Id = 9621, Tag = Keep },
            new CwM2MChild { Id = 9622, Tag = Skip },
        ]);
        ctx.From<CwM2MLink>().WithData([
            new CwM2MLink { ParentId = 9601, ChildId = 9611 },
            new CwM2MLink { ParentId = 9601, ChildId = 9612 },
            new CwM2MLink { ParentId = 9601, ChildId = 9613 },
            new CwM2MLink { ParentId = 9602, ChildId = 9621 },
            new CwM2MLink { ParentId = 9602, ChildId = 9622 },
        ]);

        return ctx;
    }

    /// <summary>The public <c>JoinInto</c> overload rows (the five overloads of the variant matrix).</summary>
    public enum JoinIntoOverload
    {
        CollectionConvenience,
        CollectionKind,
        ReferenceConvenience,
        ReferenceKind,
        ExplicitKey,
    }

    /// <summary>The unsupported child-query modifiers, one row per modifier.</summary>
    public enum ChildShape
    {
        OrderBy,
        Limit,
        Offset,
        Page,
        Distinct,
        GroupBy,
        Having,
        DistinctOn,
        SelectWhereExtreme,
        LimitBy,
        Join,
        Final,
        PreWhere,
        ArrayJoin,
        TableSample,
        Sample,
        ForSystemTime,
        RowLock,
        SourceLock,
        Window,
        Settings,
        Cte,
        TableHint,
        IndexHint,
        TablesInScopeHint,
        NestedLoadWith,
    }

    public sealed class CwParent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ICollection<CwChild> Children { get; set; } = new List<CwChild>();
        public CwProfile? Profile { get; set; }
    }

    public sealed class CwChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string? Tag { get; set; }
        public int[] Values { get; set; } = [];
        public CwParent? Parent { get; set; }
        public ICollection<CwNote> Notes { get; set; } = new List<CwNote>();
    }

    public sealed class CwProfile
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string? Tag { get; set; }
    }

    public sealed class CwNote
    {
        public int Id { get; set; }
        public int ChildId { get; set; }
    }

    public sealed class CwM2MParent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<CwM2MChild> Children { get; set; } = [];
    }

    public sealed class CwM2MChild
    {
        public int Id { get; set; }
        public string? Tag { get; set; }
    }

    public sealed class CwM2MLink
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }
}
