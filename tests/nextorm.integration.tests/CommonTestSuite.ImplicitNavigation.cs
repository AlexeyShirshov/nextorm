using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #148-B D8: provider conformance for implicit navigation queries. The facts run on every provider
/// that derives <see cref="CommonTestSuite"/> (SQLite, PostgreSQL, SQL Server, MySQL) over the shared
/// eager_parent/eager_child/eager_tag/eager_link fixtures (the same metadata registration as
/// <c>CommonTestSuite.JoinInto</c>). The reusable <see cref="NavConScenarios"/> bodies are also invoked
/// by the MariaDB and ClickHouse feature suites, because those providers do not derive the common suite.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void ImplicitNavigation_ReferenceScalarChain_ShouldReturnThePrincipalValueForPresentAndDanglingFk()
        => NavConScenarios.ReferenceScalarChainAndPresence(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_ReferenceWholeProjection_ShouldBeNullForADanglingFk()
        => NavConScenarios.ReferenceWholeProjectionAndDangling(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_ReferenceLiftedCoalescedAndUnliftedGuard_ShouldFollowTheNullContract()
        => NavConScenarios.ReferenceLiftedCoalescedAndUnliftedGuard(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_OneToManyTerminals_ShouldCountEmptyAndNonEmptyWithoutDeduplication()
        => NavConScenarios.CollectionTerminalsOneToMany(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_ManyToManyTerminals_ShouldPreserveDuplicateJunctionRowsAndDropTheDanglingOne()
        => NavConScenarios.CollectionTerminalsManyToMany(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_CollectionAdapter_ShouldMatchTheDirectTerminals()
        => NavConScenarios.AdapterEquivalentToDirectTerminals(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_RejectList_ShouldFailClosed()
        => NavConScenarios.RejectList(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_CapturedEnumerable_ShouldNotBeTreatedAsNavigation()
        => NavConScenarios.CapturedReceiverIsNotNavigation(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_SameTypePaths_ShouldKeepDistinctAliases()
        => NavConScenarios.AliasIdentitySameTypePaths(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_ColdWarmPreparedAndSharedCommandHygiene_ShouldBeStable()
        => NavConScenarios.ColdWarmPreparedAndSharedCommandHygiene(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_MultiHopReferencePresence_ShouldPropagateAbsence()
        => NavConScenarios.MultiHopReferencePresence(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_CollectionThroughAReference_ShouldBeEmptyWhenTheReferenceIsAbsent()
        => NavConScenarios.CollectionThroughAReference(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_ReferenceAdapter_ShouldMatchExplicitReferenceSemantics()
        => NavConScenarios.ReferenceAdapterEquivalentToExplicit(_sut.DataProvider);

    [Fact]
    public void ImplicitNavigation_WideCountBoundary_ShouldStayInRangeAndWide()
        => NavConScenarios.WideCountInRangeBoundary(_sut.DataProvider);
}

/// <summary>
/// Provider-agnostic implicit-navigation scenarios. Each method registers the navigation metadata,
/// seeds a fresh id block and asserts the observable semantics, so the same body can run on the common
/// suite and on the MariaDB/ClickHouse feature suites. The scenarios use the shared eager_* fixtures.
/// </summary>
internal static class NavConScenarios
{
    private static int _seed = -5_000_000;

    private static int NextBase() => Interlocked.Add(ref _seed, -10_000);

    private static void Register(IDataContext ctx)
    {
        // Same registration as CommonTestSuite.JoinInto: children first, so their navigation member is
        // already excluded when the parent-side HasMany resolves.
        ctx.From<JoinIntoChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<JoinIntoNote>();
        ctx.From<JoinIntoTag>();
        ctx.From<JoinIntoParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId)
            .HasManyThrough<JoinIntoTag, JoinIntoTagLink, int, int>(
                p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId)
            .HasOneToOne(p => p.PrimaryChild, p => p.Id, c => c.ParentId));
    }

    /// <summary>
    /// Seeds a fresh fixture and returns the base id: <c>base</c> has two children, a duplicate-junction
    /// two-tag set plus a dangling junction; <c>base + 1</c> has one child and one tag; <c>base + 2</c> is
    /// empty; <c>base + 103</c> is a child whose FK dangles.
    /// </summary>
    internal static int Seed(IDataContext ctx)
    {
        Register(ctx);

        var many = NextBase();
        var one = many + 1;
        var none = many + 2;

        ctx.CreateInsertBuilder<JoinIntoParent>().Values([
            new JoinIntoParent { Id = many, Name = "nav-many" },
            new JoinIntoParent { Id = one, Name = "nav-one" },
            new JoinIntoParent { Id = none, Name = "nav-none" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoChild>().Values([
            new JoinIntoChild { Id = many + 100, ParentId = many, Name = "nav-a" },
            new JoinIntoChild { Id = many + 101, ParentId = many, Name = "nav-b" },
            new JoinIntoChild { Id = many + 102, ParentId = one, Name = "nav-one-a" },
            new JoinIntoChild { Id = many + 103, ParentId = many + 999_000, Name = "nav-dangling" },
            new JoinIntoChild { Id = many + 104, ParentId = 0, Name = "nav-default-fk" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoTag>().Values([
            new JoinIntoTag { Id = many + 300, Name = "nav-t1" },
            new JoinIntoTag { Id = many + 301, Name = "nav-t2" },
            new JoinIntoTag { Id = many + 302, Name = "nav-one-t" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoTagLink>().Values([
            new JoinIntoTagLink { Id = many + 400, ParentId = many, ChildId = many + 300 },
            new JoinIntoTagLink { Id = many + 401, ParentId = many, ChildId = many + 301 },
            new JoinIntoTagLink { Id = many + 402, ParentId = many, ChildId = many + 300 },
            new JoinIntoTagLink { Id = many + 403, ParentId = one, ChildId = many + 302 },
            new JoinIntoTagLink { Id = many + 404, ParentId = many, ChildId = many + 399 },
        ]).Insert();

        return many;
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    internal static void ReferenceScalarChainAndPresence(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100 || c.Id == f + 103)
            .Select(c => new { c.Id, Name = c.Parent!.Name })
            .ToList();

        rows.Single(r => r.Id == f + 100).Name.Should().Be("nav-many");
        rows.Single(r => r.Id == f + 103).Name.Should().BeNull("the foreign key dangles and the reference expands as a LEFT JOIN");

        var present = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100)
            .Where(c => c.Parent != null)
            .Select(c => c.Id)
            .ToList();
        present.Should().Equal(f + 100);

        var absent = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 103)
            .Where(c => c.Parent == null)
            .Select(c => c.Id)
            .ToList();
        absent.Should().Equal([f + 103]);

        var sql = SqlOf(ctx, ctx.From<JoinIntoChild>().Select(c => new { c.Id, Name = c.Parent!.Name })).ToLowerInvariant();
        sql.Should().Contain("left join").And.Contain("eager_parent");
    }

    internal static void ReferenceWholeProjectionAndDangling(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100 || c.Id == f + 103)
            .Select(c => new { c.Id, Parent = c.Parent })
            .ToList();

        var present = rows.Single(r => r.Id == f + 100);
        present.Parent.Should().NotBeNull();
        present.Parent!.Id.Should().Be(f);
        present.Parent.Name.Should().Be("nav-many");

        rows.Single(r => r.Id == f + 103).Parent.Should().BeNull("an absent whole reference materializes as null");
    }

    internal static void ReferenceLiftedCoalescedAndUnliftedGuard(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100 || c.Id == f + 103)
            .Select(c => new { c.Id, Lifted = (int?)c.Parent!.Id, Coalesced = (int?)c.Parent!.Id ?? -1 })
            .ToList();

        rows.Single(r => r.Id == f + 100).Lifted.Should().Be(f);
        rows.Single(r => r.Id == f + 100).Coalesced.Should().Be(f);
        rows.Single(r => r.Id == f + 103).Lifted.Should().BeNull("a lifted nullable scalar reads SQL NULL");
        rows.Single(r => r.Id == f + 103).Coalesced.Should().Be(-1, "an explicit coalesce supplies the default");

        Action act = () => SqlOf(ctx, ctx.From<JoinIntoChild>()
            .Select(c => new { c.Id, ParentId = c.Parent!.Id }));

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*JoinIntoChild.Parent.Id*System.Int32*",
                "an unlifted non-nullable navigation scalar must fail with the path and result type");
    }

    internal static void CollectionTerminalsOneToMany(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 2)
            .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
            .ToList();

        var many = rows.Single(r => r.Id == f);
        many.Has.Should().BeTrue();
        many.C.Should().Be(2);
        many.L.Should().Be(2L);
        many.P.Should().Be(2);

        var none = rows.Single(r => r.Id == f + 2);
        none.Has.Should().BeFalse("an empty collection yields false");
        none.C.Should().Be(0);
        none.L.Should().Be(0L);
        none.P.Should().Be(0);
    }

    internal static void CollectionTerminalsManyToMany(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 2)
            .Select(p => new { p.Id, Has = p.Tags.Any(), C = p.Tags.Count(), L = p.Tags.LongCount(), P = p.Tags.Count })
            .ToList();

        // A direct many-to-many terminal counts the declared relation's occurrences: the junction rows
        // whose mapped child exists. The duplicate link to tag t1 is not deduplicated, while the dangling
        // link (child id many + 399) is excluded (#148-B R2.1).
        var many = rows.Single(r => r.Id == f);
        many.Has.Should().BeTrue();
        many.C.Should().Be(3, "3 junction rows have a mapped child; the dangling link is excluded and duplicates are preserved");
        many.L.Should().Be(3L);
        many.P.Should().Be(3);

        var none = rows.Single(r => r.Id == f + 2);
        none.Has.Should().BeFalse();
        none.C.Should().Be(0);
        none.L.Should().Be(0L);
        none.P.Should().Be(0);
    }

    internal static void AdapterEquivalentToDirectTerminals(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 1 || p.Id == f + 2)
            .Select(p => new
            {
                p.Id,
                Has = p.Children.AsEntityBuilder<JoinIntoChild>().Any(),
                C = p.Children.AsEntityBuilder<JoinIntoChild>().Count(),
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Single(r => r.Id == f).Has.Should().BeTrue();
        rows.Single(r => r.Id == f).C.Should().Be(2);
        rows.Single(r => r.Id == f + 1).Has.Should().BeTrue();
        rows.Single(r => r.Id == f + 1).C.Should().Be(1);
        rows.Single(r => r.Id == f + 2).Has.Should().BeFalse();
        rows.Single(r => r.Id == f + 2).C.Should().Be(0);
    }

    internal static void RejectList(IDataContext ctx)
    {
        Seed(ctx);

        Action predicateAny = () => SqlOf(ctx, ctx.From<JoinIntoParent>()
            .Select(p => new { p.Id, X = p.Children.Any(c => c.Id > 0) }));
        Action where = () => SqlOf(ctx, ctx.From<JoinIntoParent>()
            .Select(p => new { p.Id, X = p.Children.Where(c => c.Id > 0) }));
        Action select = () => SqlOf(ctx, ctx.From<JoinIntoParent>()
            .Select(p => new { p.Id, X = p.Children.Select(c => c.Id).ToList() }));
        Action sum = () => SqlOf(ctx, ctx.From<JoinIntoParent>()
            .Select(p => new { p.Id, X = p.Children.Sum(c => c.Id) }));
        Action first = () => SqlOf(ctx, ctx.From<JoinIntoParent>()
            .Select(p => new { p.Id, X = p.Children.First().Id }));
        Action contains = () => SqlOf(ctx, ctx.From<JoinIntoParent>()
            .Select(p => new { p.Id, X = p.Children.Contains(new JoinIntoChild { Id = 1 }) }));

        predicateAny.Should().Throw<NotSupportedException>("a predicate overload of Any is out of scope");
        where.Should().Throw<NotSupportedException>();
        select.Should().Throw<NotSupportedException>();
        sum.Should().Throw<NotSupportedException>();
        first.Should().Throw<NotSupportedException>();
        contains.Should().Throw<NotSupportedException>();
    }

    internal static void CapturedReceiverIsNotNavigation(IDataContext ctx)
    {
        var f = Seed(ctx);
        var captured = new List<JoinIntoChild> { new() { Id = 1 } };

        var rows = ctx.From<JoinIntoParent>()
            .Where(p => p.Id == f)
            .Select(p => new { p.Id, Has = captured.Any() })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Has.Should().BeTrue("a captured enumerable is read as an ordinary in-memory collection");
    }

    internal static void AliasIdentitySameTypePaths(IDataContext ctx)
    {
        var f = Seed(ctx);

        // "one" has exactly one child, so the 1:M path and the 1:1 reference to the same CLR type
        // (JoinIntoChild) must both resolve without merging into a single alias.
        var rows = ctx.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1)
            .Select(p => new { p.Id, C = p.Children.Count(), Primary = (int?)p.PrimaryChild!.Id })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].C.Should().Be(1, "the collection path counts the child");
        rows[0].Primary.Should().Be(f + 102, "the reference path resolves the one-to-one alias independently");
    }

    internal static void ColdWarmPreparedAndSharedCommandHygiene(IDataContext ctx)
    {
        var f = Seed(ctx);
        ((DataContext)ctx).PurgeQueryCache();

        // Cold plan, warm cache hit and an uncached preparation of the same reference query render the
        // same SQL and return the same rows.
        var coldSql = SqlOf(ctx, ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100)
            .Select(c => new { c.Id, Name = c.Parent!.Name }));
        var warmSql = SqlOf(ctx, ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100)
            .Select(c => new { c.Id, Name = c.Parent!.Name }));
        warmSql.Should().Be(coldSql);

        var cold = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100)
            .Select(c => new { c.Id, Name = c.Parent!.Name })
            .ToList();
        var warm = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100)
            .Select(c => new { c.Id, Name = c.Parent!.Name })
            .ToList();
        cold.Should().BeEquivalentTo(warm);
        cold.Single().Name.Should().Be("nav-many");

        // A navigation-backed shared Any must not disable the shared command's plan cache (sticky
        // Cache=false leak). A reference-presence predicate avoids the unrelated outer-Any over a
        // correlated-subquery predicate defect.
        ctx.From<JoinIntoChild>().Where(c => c.Parent != null).Any().Should().BeTrue();
        ((DataContext)ctx).AnyCommand.Should().NotBeNull();
        ((DataContext)ctx).AnyCommand!.Value.Cache.Should().BeTrue(
            "a navigation query must not disable the context-shared Any command's plan cache");
    }

    // ---- r3 shapes (D-R3-4/5/6 + A3′) ---------------------------------------------------------

    /// <summary>
    /// Two reference hops (<c>child -&gt; parent -&gt; primary child</c>) used as a presence check.
    /// Child <c>f+102</c>'s parent (<c>f+1</c>) has exactly one child, so the one-to-one second hop is
    /// unambiguous; child <c>f+103</c>'s parent dangles, so the whole chain is absent.
    /// </summary>
    internal static void MultiHopReferencePresence(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 102 || c.Id == f + 103)
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                Absent = c.Parent!.PrimaryChild == null,
                Present = c.Parent!.PrimaryChild != null,
            })
            .ToList();

        rows.Should().OnlyContain(r => r.Absent != r.Present, "== null and != null must be mutually consistent");

        var present = rows.Single(r => r.Id == f + 102);
        present.Present.Should().BeTrue("child -> parent -> primary child resolves");
        present.Absent.Should().BeFalse();

        var absent = rows.Single(r => r.Id == f + 103);
        absent.Absent.Should().BeTrue("a dangling first hop makes the whole reference chain absent");
        absent.Present.Should().BeFalse();
    }

    /// <summary>
    /// A collection reached through a declared reference (<c>child.Parent.Children</c>): the correlated
    /// collection keys off the reference <c>LEFT JOIN</c> alias, so an absent reference yields an empty
    /// collection and never phantom-matches a default-valued foreign key. Child <c>f+104</c> carries
    /// <c>ParentId = 0</c> against no principal.
    /// </summary>
    internal static void CollectionThroughAReference(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100 || c.Id == f + 102 || c.Id == f + 103 || c.Id == f + 104)
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                Has = c.Parent!.Children.Any(),
                C = c.Parent!.Children.Count(),
                L = c.Parent!.Children.LongCount(),
                P = c.Parent!.Children.Count,
            })
            .ToList();

        var many = rows.Single(r => r.Id == f + 100); // parent f+0 has children f+100 and f+101
        many.Has.Should().BeTrue();
        many.C.Should().Be(2);
        many.L.Should().Be(2L);
        many.P.Should().Be(2);

        var one = rows.Single(r => r.Id == f + 102); // parent f+1 has only child f+102
        one.Has.Should().BeTrue();
        one.C.Should().Be(1);
        one.L.Should().Be(1L);
        one.P.Should().Be(1);

        foreach (var id in new[] { f + 103, f + 104 })
        {
            var absent = rows.Single(r => r.Id == id);
            absent.Has.Should().BeFalse("an absent reference yields an empty correlated collection");
            absent.C.Should().Be(0);
            absent.L.Should().Be(0L);
            absent.P.Should().Be(0);
        }

        var ids = ctx.From<JoinIntoChild>()
            .Where(c => c.Id >= f + 100 && c.Id <= f + 104 && c.Parent!.Children.Any())
            .Select(c => c.Id)
            .ToList();
        ids.Should().BeEquivalentTo(new[] { f + 100, f + 101, f + 102 },
            "an absent reference must not match a default-valued foreign key");
    }

    /// <summary>
    /// The reference adapter <c>AsEntityBuilder&lt;T&gt;(object?)</c> supplies reference semantics:
    /// <c>Any</c>/<c>Count</c>/<c>LongCount</c> are equivalent to <c>nav != null</c> (1/0, never the
    /// principal's collection cardinality).
    /// </summary>
    internal static void ReferenceAdapterEquivalentToExplicit(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100 || c.Id == f + 103)
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                AdapterHas = c.Parent!.AsEntityBuilder<JoinIntoParent>().Any(),
                AdapterCount = c.Parent!.AsEntityBuilder<JoinIntoParent>().Count(),
                AdapterLong = c.Parent!.AsEntityBuilder<JoinIntoParent>().LongCount(),
                ExplicitHas = c.Parent != null,
            })
            .ToList();

        var present = rows.Single(r => r.Id == f + 100);
        present.AdapterHas.Should().BeTrue();
        present.AdapterCount.Should().Be(1, "the reference adapter reports the single related principal");
        present.AdapterLong.Should().Be(1L);
        present.ExplicitHas.Should().BeTrue();

        var absent = rows.Single(r => r.Id == f + 103);
        absent.AdapterHas.Should().BeFalse();
        absent.AdapterCount.Should().Be(0);
        absent.AdapterLong.Should().Be(0L);
        absent.ExplicitHas.Should().BeFalse();
    }

    /// <summary>
    /// The A3′ boundary: in-range <c>Count()</c>/property <c>Count</c> materialise as a checked
    /// <c>int</c> and <c>LongCount()</c> stays 64-bit; the SQL count stays wide with no in-database
    /// Int32 narrowing cast. The <c>&gt;Int32.MaxValue</c> path is not seedable on a real provider and is
    /// deliberately not forced — the unit boundary tests cover the throw.
    /// </summary>
    internal static void WideCountInRangeBoundary(IDataContext ctx)
    {
        var f = Seed(ctx);

        var rows = ctx.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 2)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
            .ToList();

        var many = rows.Single(r => r.Id == f);
        int count = many.C;      // A3′: Count() materialises as int in range
        long longCount = many.L; // A3′: LongCount() is 64-bit end to end
        count.Should().Be(2);
        longCount.Should().Be(2L);
        many.P.Should().Be(2);

        var empty = rows.Single(r => r.Id == f + 2);
        empty.C.Should().Be(0);
        empty.L.Should().Be(0L);
        empty.P.Should().Be(0);

        var countSql = SqlOf(ctx, ctx.From<JoinIntoParent>().Select(p => new { p.Id, C = p.Children.Count() }))
            .ToLowerInvariant();
        var longSql = SqlOf(ctx, ctx.From<JoinIntoParent>().Select(p => new { p.Id, L = p.Children.LongCount() }))
            .ToLowerInvariant();

        foreach (var sql in new[] { countSql, longSql })
        {
            sql.Should().Contain("count");
            sql.Should().NotContain("cast(count(");
            sql.Should().NotContain("cast(count_big(");
        }
    }
}
