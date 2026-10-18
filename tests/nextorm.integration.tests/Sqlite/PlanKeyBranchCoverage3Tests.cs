using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Plan-key branch coverage (cycle 3, D8) executed against a live SQLite database. Every fact drives the
/// remaining node types of <c>ExpressionPlanEqualityComparer</c> and the issue axes of
/// <c>QueryPlanEqualityComparer.EqualsCore</c>/<c>GetHashCodeCore</c> through real prepared commands, and
/// executes the same shapes live. The marker rows come from the shared SQLite seed (this class asserts
/// concrete row values, never global seed-table counts) and the eager-shape axis seeds its own
/// <c>eager_parent</c>/<c>eager_child</c> rows with a unique id range and deletes them in a <c>finally</c>.
/// </summary>
///
/// <para>
/// Recorded as unreachable through the public surface (never fabricated): a lambda with more than four
/// parameters. Every public LambdaExpression entry point on <c>EntityBuilder</c>/<c>QueryCommand</c>
/// (unary <c>Where</c>/<c>Select</c>, the two-parameter join family, <c>SelectMany</c>) takes at most two
/// parameters, so the &gt;4-parameter arity arm has no call sequence on the public API.
/// </para>
[Collection("Sqlite")]
public sealed class PlanKeyBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private static int _eagerSeed = -1_960_000_000;

    private static int NextEagerBase() => Interlocked.Add(ref _eagerSeed, -1_000);

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>A captured object whose member is read inside a predicate expression.</summary>
    private sealed class ClosureProbe
    {
        public long Floor { get; set; } = 1;
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Projection node battery: collection initializer, tuple member access, method invocation
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ProjectionNodes_ListInitTupleMemberAndInvocation_ShouldExecuteAndKeySeparately()
    {
        ResetPlanCache();

        // A captured collection initializer folds to an IN list inside the projection.
        var pick = new List<int> { 1, 3 };
        var listInit = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id, Match = pick.Contains((int)x.Id) });
        var listInitAgain = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id, Match = pick.Contains((int)x.Id) });

        var listInitRows = Rows(Prepare(listInit));
        listInitRows.Select(r => r.Id).Should().Equal(1L, 2L, 3L);
        listInitRows.Select(r => r.Match).Should().Equal(new[] { true, false, true }, "the captured list initializer becomes an IN list");

        var listInitAgainRows = Rows(Prepare(listInitAgain));
        listInitAgainRows.Select(r => r.Match).Should().Equal(new[] { true, false, true });

        var planComparer = listInit.GetQueryPlanEqualityComparer();
        planComparer.Equals(listInit, listInitAgain).Should().BeTrue("an identical projection keys the same plan");
        planComparer.GetHashCode(listInit).Should().Be(planComparer.GetHashCode(listInitAgain));

        var narrowedList = new List<int> { 1 };
        var narrowed = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id, Match = narrowedList.Contains((int)x.Id) });
        var narrowedRows = Rows(Prepare(narrowed));
        narrowedRows.Select(r => r.Match).Should().Equal(new[] { true, false, false });
        planComparer.Equals(listInit, narrowed).Should()
            .BeFalse("a shorter captured list is a different value-list shape in the plan key");

        // Member access on a join projection tuple (Item1 / Item2) plus an invocation projection.
        var joined = _sut.ComplexEntity
            .Join(_sut.SimpleEntity, (c, s) => c.Id == s.Id)
            .OrderBy(x => x.Item1.Id)
            .Select(x => new { Left = x.Item1.Id, Right = x.Item2.Id });

        var joinedRows = joined.ToList();
        joinedRows.Select(r => r.Left).Should().Equal(1L, 2L, 3L);
        joinedRows.Select(r => r.Right).Should().Equal(new[] { 1, 2, 3 }, "the join tuple's Item1/Item2 members read the two sides");

        var upper = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.RequiredString.ToUpper());
        upper.ToList().Should().Equal("SDF", "ASDFGOI", "34MFS");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Closure / member battery: constant, captured variable, captured-object member, convert
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ClosureMemberNodes_ConstantCapturedObjectAndConvert_ShouldCacheAndHit()
    {
        ResetPlanCache();

        var probe = new ClosureProbe { Floor = 1 };
        var capturedFloor = 1L;

        Expression<Func<IComplexEntity, bool>> byConstant = x => x.Id > 1L;
        Expression<Func<IComplexEntity, bool>> byConstantAgain = x => x.Id > 1L;
        Expression<Func<IComplexEntity, bool>> byCaptured = x => x.Id > capturedFloor;
        Expression<Func<IComplexEntity, bool>> byMember = x => x.Id > probe.Floor;
        Expression<Func<IComplexEntity, bool>> byConvert = x => (double)x.Id > 1.0;

        var expressionComparer = _sut.ComplexEntity
            .Where(x => x.Id >= 0)
            .Select(x => x.Id)
            .GetExpressionPlanEqualityComparer();

        expressionComparer.Equals(byConstant, byConstantAgain).Should().BeTrue("two identical literal predicates compare equal");
        expressionComparer.GetHashCode(byConstant).Should().Be(expressionComparer.GetHashCode(byConstantAgain));
        expressionComparer.Equals(byConstant, byCaptured).Should()
            .BeFalse("a constant node and a closure member node are different kinds");
        expressionComparer.Equals(byCaptured, byCaptured).Should().BeTrue("a closure member is equal to itself");
        expressionComparer.Equals(byConstant, byMember).Should()
            .BeFalse("a member access on a captured object is a different node");
        expressionComparer.Equals(byConvert, byConstant).Should().BeFalse("a convert node is not a member node");

        // Each shape runs twice through the cached path: the first preparation is a miss, the second a hit.
        var constant = _sut.ComplexEntity.Where(byConstant).Select(x => x.Id);
        var constantFirst = Prepare(constant);
        Rows(constantFirst).Should().Equal(2L, 3L);
        Rows(Prepare(constant)).Should().Equal(2L, 3L);
        Prepare(constant).Should().BeSameAs(constantFirst, "an identical constant shape keeps one plan");

        var captured = _sut.ComplexEntity.Where(byCaptured).Select(x => x.Id);
        var capturedFirst = Prepare(captured);
        Rows(capturedFirst).Should().Equal(2L, 3L);
        Prepare(captured).Should().BeSameAs(capturedFirst, "the same closure site keeps one plan");

        var member = _sut.ComplexEntity.Where(byMember).Select(x => x.Id);
        var memberFirst = Prepare(member);
        Rows(memberFirst).Should().Equal(2L, 3L);
        Prepare(member).Should().BeSameAs(memberFirst, "a captured-object member keeps one plan and rebinds its value");

        var convert = _sut.ComplexEntity.Where(byConvert).Select(x => x.Id);
        var convertFirst = Prepare(convert);
        Rows(convertFirst).Should().Equal(2L, 3L);
        Prepare(convert).Should().BeSameAs(convertFirst, "a convert predicate keeps one plan");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Plan-key axis battery: Tag, paging, Distinct and the eager shape
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void PlanKeyAxes_TagPagingDistinctAndEagerShape_ShouldBehaveAsDistinctKeys()
    {
        ResetPlanCache();

        // Tag axis.
        var plain = _sut.ComplexEntity.Where(x => x.Id <= 2).Select(x => x.Id);
        var plainAgain = _sut.ComplexEntity.Where(x => x.Id <= 2).Select(x => x.Id);
        var tagged = _sut.ComplexEntity.Where(x => x.Id <= 2).Select(x => x.Id).WithTag("pkb3_tag");

        Rows(Prepare(plain)).Should().Equal(1L, 2L);
        Prepare(plainAgain).Should().BeSameAs(Prepare(plain), "an identical untagged shape shares one plan");
        Rows(Prepare(tagged)).Should().Equal(1L, 2L);
        Prepare(tagged).Should().NotBeSameAs(Prepare(plain), "the tag text is part of the plan key");

        // Paging axis.
        var pageOne = _sut.ComplexEntity.OrderBy(x => x.Id).Limit(1).Select(x => x.Id);
        var pageTwo = _sut.ComplexEntity.OrderBy(x => x.Id).Limit(2).Select(x => x.Id);

        Rows(Prepare(pageOne)).Should().Equal(1L);
        Rows(Prepare(pageTwo)).Should().Equal(1L, 2L);
        Prepare(pageTwo).Should().NotBeSameAs(Prepare(pageOne), "a different page limit is a different plan key");

        // Distinct axis.
        var notDistinct = _sut.ComplexEntity.Select(x => x.Int);
        var distinct = _sut.ComplexEntity.Select(x => x.Int).Distinct();

        Rows(Prepare(notDistinct)).Should().HaveCount(3);
        Rows(Prepare(distinct)).Should().HaveCount(2);
        Prepare(distinct).Should().NotBeSameAs(Prepare(notDistinct), "the DISTINCT flag is part of the plan key");

        // Eager-shape axis: the same graph in SingleQuery and SplitQuery must not share the parent plan.
        var parent = NextEagerBase();
        Context.CreateInsertBuilder<EagerParent>().Values([new EagerParent { Id = parent, Name = "pkb3" }]).Insert();
        Context.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = parent + 100, ParentId = parent, Name = "c1" },
            new EagerChild { Id = parent + 101, ParentId = parent, Name = "c2" },
        ]).Insert();

        try
        {
            var single = Context.From<EagerParent>()
                .Where(p => p.Id == parent)
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery);
            var split = Context.From<EagerParent>()
                .Where(p => p.Id == parent)
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery);

            var singleParents = single.ToList();
            singleParents.Should().ContainSingle();
            singleParents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([parent + 100, parent + 101]);

            var splitParents = split.ToList();
            splitParents.Should().ContainSingle();
            splitParents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([parent + 100, parent + 101]);

            Prepare(single.ToCommand()).Should().NotBeSameAs(
                Prepare(split.ToCommand()), "the resolved eager mode is part of the plan key");
        }
        finally
        {
            Context.CreateDeleteBuilder<EagerChild>().Where(c => c.ParentId == parent).Delete();
            Context.CreateDeleteBuilder<EagerParent>().Where(p => p.Id == parent).Delete();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Nested-lambda battery: two-level nesting and the unreachable >4-parameter arity
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NestedLambdas_TwoLevels_ShouldExecute()
    {
        // Two nested lambdas: the outer predicate opens a subquery whose inner predicate opens a second one.
        var rows = _sut.ComplexEntity
            .Where(x => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s =>
                s.Id == x.Id && SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id)))))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(1L, 2L, 3L);

        var counts = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => _sut.SimpleEntity.Where(s => s.Id == x.Id).Count())
            .ToList();

        counts.Should().Equal(new[] { 1, 1, 1 }, "every seeded complex row has exactly one matching simple row");
    }
}
