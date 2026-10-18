using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Expression/plan equality coverage executed against a live SQLite database. Every fact builds real
/// expression shapes over the inherited <see cref="ProviderTestSuite"/> repository, drives
/// <see cref="QueryCommand.GetExpressionPlanEqualityComparer"/> and
/// <see cref="QueryCommand.GetQueryPlanEqualityComparer"/> in <b>both</b> directions (equal and unequal)
/// and then executes the same shapes as live queries, asserting the returned rows.
///
/// <para>
/// Targeted branches of <c>src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs</c>: the type switch
/// of <c>Compare2</c> (310-343), <c>CompareConstant</c> (362-368), <c>CompareMember</c> (444-447),
/// <c>CompareUnary</c> (490-494), <c>VisitConstant</c> (778-796) and <c>VisitMember</c> (863-906); and of
/// <c>src/nextorm.core/Query/QueryPlanEqualityComparer.cs</c> through
/// <see cref="QueryCommand.GetQueryPlanEqualityComparer"/>.
/// </para>
///
/// <para>
/// Two shapes are deliberately absent because no public lambda path produces them: <c>Block</c>/<c>Goto</c>/
/// <c>Label</c>/<c>Loop</c>/<c>Switch</c>/<c>Try</c>/<c>RuntimeVariables</c>/<c>Invocation</c>/<c>Index</c>
/// nodes cannot be written in a LINQ-to-SQL predicate or projection, and a <c>ConstantExpression</c>
/// holding an <c>IQueryable</c> (the <c>VisitConstant</c> 783 arm) is never produced by the builder - a
/// captured queryable is a closure member, not a constant. Both are recorded here instead of being
/// fabricated with synthetic trees.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class PlanEqualityBranchCoverage2Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>An expression comparer owned by a throwaway probe command over the seeded table.</summary>
    private ExpressionPlanEqualityComparer Comparer()
        => _sut.ComplexEntity.Where(x => x.Id >= 0).Select(x => x.Id).GetExpressionPlanEqualityComparer();

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>Static-member probe: its field is read at execution time, never folded into the plan.</summary>
    private static class PlanProbe
    {
        public static long Floor = 1;
    }

    /// <summary>Materialized member-init projection used by the <c>MemberInitExpression</c> fact.</summary>
    public sealed class PlanRow
    {
        public long Id { get; set; }
        public int? Value { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Constant expressions: equal value, changed value, changed constant type
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_ConstantValueAndType_ShouldCompareBothWays()
    {
        Expression<Func<IComplexEntity, bool>> byLong = x => x.Id == 1L;
        Expression<Func<IComplexEntity, bool>> byLongAgain = x => x.Id == 1L;
        Expression<Func<IComplexEntity, bool>> byDouble = x => x.Id == 1.0;
        Expression<Func<IComplexEntity, bool>> byOtherLong = x => x.Id == 2L;

        var comparer = Comparer();

        comparer.Equals(byLong, byLongAgain).Should().BeTrue("two identical literal predicates are the same expression");
        comparer.GetHashCode(byLong).Should().Be(comparer.GetHashCode(byLongAgain), "equal expressions hash equally");
        comparer.Equals(byLong, byOtherLong).Should()
            .BeFalse("a changed literal value is a different expression (CompareConstant)");
        comparer.Equals(byLong, byDouble).Should()
            .BeFalse("a changed constant type is a different expression (the Type guard above Compare2)");

        _sut.ComplexEntity.Where(byLong).Select(x => x.Id).ToList().Should().Equal(1L);
        _sut.ComplexEntity.Where(byDouble).Select(x => x.Id).ToList().Should().Equal(1L);
        _sut.ComplexEntity.Where(byOtherLong).Select(x => x.Id).ToList().Should().Equal(2L);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Constant versus closure member (the CompareMember constant-receiver fast path)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_ConstantVersusClosureMember_ShouldDiffer()
    {
        static Expression<Func<IComplexEntity, bool>> ByBound(long bound) => x => x.Id > bound;

        Expression<Func<IComplexEntity, bool>> literal = x => x.Id > 1;

        var comparer = Comparer();

        comparer.Equals(literal, ByBound(1)).Should()
            .BeFalse("a constant node and a closure member node are different shapes");
        comparer.Equals(ByBound(1), ByBound(1)).Should()
            .BeTrue("the same closure site and member name compare equal (CompareMember constant-receiver path)");
        comparer.GetHashCode(ByBound(1)).Should().Be(comparer.GetHashCode(ByBound(1)));

        _sut.ComplexEntity.Where(literal).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
        _sut.ComplexEntity.Where(ByBound(1)).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Member identity: property versus property (the non-constant CompareMember path)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_DifferentMembers_ShouldNotMatch()
    {
        Expression<Func<IComplexEntity, bool>> byId = x => x.Id > 1;
        Expression<Func<IComplexEntity, bool>> byIdAgain = x => x.Id > 1;
        Expression<Func<IComplexEntity, bool>> byInt = x => x.Int >= 1;
        Expression<Func<IComplexEntity, bool>> byLength = x => x.RequiredString.Length > 1;

        var comparer = Comparer();

        comparer.Equals(byId, byIdAgain).Should().BeTrue("the same member compares by MemberInfo");
        comparer.Equals(byId, byInt).Should()
            .BeFalse("different members never match (CompareMember non-constant path)");
        comparer.Equals(byId, byLength).Should()
            .BeFalse("a nested member access is a different tree");

        _sut.ComplexEntity.Where(byId).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
        _sut.ComplexEntity.Where(byInt).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
        _sut.ComplexEntity.Where(byLength).OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(new[] { 1L, 2L, 3L }, "every seeded RequiredString is longer than one character");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Static member and a captured QueryCommand member (VisitMember 863-906, both branches)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_StaticMemberAndCapturedCommand_ShouldCompareByIdentity()
    {
        var commandA = _sut.ComplexEntity.Where(x => x.Id > 1).Select(x => x.Id);
        var commandB = _sut.ComplexEntity.Where(x => x.Id > 2).Select(x => x.Id);

        Expression<Func<IComplexEntity, QueryCommand<long>>> first = x => commandA;
        Expression<Func<IComplexEntity, QueryCommand<long>>> again = x => commandA;
        Expression<Func<IComplexEntity, QueryCommand<long>>> other = x => commandB;

        var comparer = Comparer();

        // First call is a cache miss (the delegate is compiled), the second is a hit - both arms of
        // VisitMember's QueryCommand branch.
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(again),
            "a captured command member hashes by that command's own plan");
        comparer.Equals(first, again).Should().BeTrue();
        comparer.Equals(first, other).Should().BeFalse("two different captured commands are different plans");

        Expression<Func<IComplexEntity, bool>> staticA = x => x.Id > PlanProbe.Floor;
        Expression<Func<IComplexEntity, bool>> staticAgain = x => x.Id > PlanProbe.Floor;

        comparer.Equals(staticA, staticAgain).Should().BeTrue("a static member compares by MemberInfo");

        PlanProbe.Floor = 2;
        Expression<Func<IComplexEntity, bool>> staticChanged = x => x.Id > PlanProbe.Floor;

        comparer.Equals(staticA, staticChanged).Should()
            .BeTrue("a static member is compared by identity, not by its current value");
        _sut.ComplexEntity.Where(staticChanged).OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(new[] { 3L }, "the live read follows the current static value");
        PlanProbe.Floor = 1;

        Rows(Prepare(commandA)).Should().Equal(2L, 3L);
        Rows(Prepare(commandB)).Should().Equal(3L);
        _sut.ComplexEntity.Where(staticA).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Unary: a conversion node versus its absence
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_UnaryConversion_ShouldCompareOperandAndTarget()
    {
        Expression<Func<IComplexEntity, bool>> converted = x => (double)x.Id > 1.0;
        Expression<Func<IComplexEntity, bool>> convertedAgain = x => (double)x.Id > 1.0;
        Expression<Func<IComplexEntity, bool>> notConverted = x => x.Id > 1L;
        Expression<Func<IComplexEntity, bool>> otherOperand = x => (double)x.Id > 2.0;

        var comparer = Comparer();

        comparer.Equals(converted, convertedAgain).Should().BeTrue("identical convert nodes are equal");
        comparer.Equals(converted, notConverted).Should()
            .BeFalse("a convert node and a member node are different (Compare2 switch / CompareUnary)");
        comparer.Equals(converted, otherOperand).Should()
            .BeFalse("the unary operand participates in the comparison (CompareUnary)");

        _sut.ComplexEntity.Where(converted).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
        _sut.ComplexEntity.Where(otherOperand).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(3L);
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Unary: logical negation
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_UnaryNegation_ShouldCompareOperand()
    {
        Expression<Func<IComplexEntity, bool>> negated = x => !(x.Id > 1);
        Expression<Func<IComplexEntity, bool>> negatedAgain = x => !(x.Id > 1);
        Expression<Func<IComplexEntity, bool>> negatedOther = x => !(x.Id > 2);

        var comparer = Comparer();

        comparer.Equals(negated, negatedAgain).Should().BeTrue("identical NOT nodes are equal");
        comparer.GetHashCode(negated).Should().Be(comparer.GetHashCode(negatedAgain));
        comparer.Equals(negated, negatedOther).Should()
            .BeFalse("a NOT node with a changed operand is a different expression");

        _sut.ComplexEntity.Where(negated).Select(x => x.Id).ToList().Should().Equal(1L);
        _sut.ComplexEntity.Where(negatedOther).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L);
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Null constant versus a non-null closure member
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_NullConstantVersusClosure_ShouldDiffer()
    {
        var name = "xxx";
        Expression<Func<IComplexEntity, bool>> isNull = x => x.String == null;
        Expression<Func<IComplexEntity, bool>> isNullAgain = x => x.String == null;
        Expression<Func<IComplexEntity, bool>> isName = x => x.String == name;

        var comparer = Comparer();

        comparer.Equals(isNull, isNullAgain).Should().BeTrue("two identical null tests are equal");
        comparer.Equals(isNull, isName).Should()
            .BeFalse("a null constant and a closure member are different node kinds");

        _sut.ComplexEntity.Where(isNull).Select(x => x.Id).ToList().Should().Equal(3L);
        _sut.ComplexEntity.Where(isName).Select(x => x.Id).ToList().Should().Equal(2L);
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Nested binary shapes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_NestedBinaryShapes_ShouldCompareBothWays()
    {
        Expression<Func<IComplexEntity, bool>> and = x => x.Id >= 1 && x.Int != null;
        Expression<Func<IComplexEntity, bool>> andAgain = x => x.Id >= 1 && x.Int != null;
        Expression<Func<IComplexEntity, bool>> or = x => x.Id >= 1 || x.Int != null;
        Expression<Func<IComplexEntity, bool>> andChanged = x => x.Id >= 1 && x.Int == null;

        var comparer = Comparer();

        comparer.Equals(and, andAgain).Should().BeTrue("identical nested binaries are equal");
        comparer.Equals(and, or).Should().BeFalse("a different binary operator is a different expression");
        comparer.Equals(and, andChanged).Should()
            .BeFalse("a changed right-hand operand is a different expression (CompareBinary)");

        _sut.ComplexEntity.Where(and).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
        _sut.ComplexEntity.Where(or).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 3L);
        _sut.ComplexEntity.Where(andChanged).Select(x => x.Id).ToList().Should().Equal(1L);
    }

    // ---------------------------------------------------------------------------------------------
    // 9. Method calls
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_MethodCallShapes_ShouldCompareBothWays()
    {
        Expression<Func<IComplexEntity, bool>> starts = x => x.RequiredString.StartsWith("s");
        Expression<Func<IComplexEntity, bool>> startsAgain = x => x.RequiredString.StartsWith("s");
        Expression<Func<IComplexEntity, bool>> ends = x => x.RequiredString.EndsWith("s");
        Expression<Func<IComplexEntity, bool>> otherArgument = x => x.RequiredString.StartsWith("a");

        var comparer = Comparer();

        comparer.Equals(starts, startsAgain).Should().BeTrue("identical method calls are equal");
        comparer.Equals(starts, ends).Should().BeFalse("a different method is a different expression");
        comparer.Equals(starts, otherArgument).Should()
            .BeFalse("a changed method argument is a different expression (CompareMethodCall)");

        _sut.ComplexEntity.Where(starts).Select(x => x.Id).ToList().Should().Equal(1L);
        _sut.ComplexEntity.Where(ends).Select(x => x.Id).ToList().Should().Equal(3L);
        _sut.ComplexEntity.Where(otherArgument).Select(x => x.Id).ToList().Should().Equal(2L);
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Conditional expressions
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_ConditionalShapes_ShouldCompareBothWays()
    {
        Expression<Func<IComplexEntity, int>> coalesce = x => x.Int == null ? 0 : x.Int.Value;
        Expression<Func<IComplexEntity, int>> coalesceAgain = x => x.Int == null ? 0 : x.Int.Value;
        Expression<Func<IComplexEntity, int>> otherBranch = x => x.Int == null ? 1 : x.Int.Value;

        var comparer = Comparer();

        comparer.Equals(coalesce, coalesceAgain).Should().BeTrue("identical conditionals are equal");
        comparer.Equals(coalesce, otherBranch).Should()
            .BeFalse("a changed branch is a different expression (CompareConditional)");

        _sut.ComplexEntity.OrderBy(x => x.Id).Select(coalesce).ToList().Should().Equal(0, 1, 1);
        _sut.ComplexEntity.OrderBy(x => x.Id).Select(otherBranch).ToList().Should().Equal(1, 1, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Default expressions
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_DefaultExpression_ShouldBeEqualToItself()
    {
        Expression<Func<IComplexEntity, bool>> isDefault = x => x.Datetime == default;
        Expression<Func<IComplexEntity, bool>> isDefaultAgain = x => x.Datetime == default;

        var comparer = Comparer();

        comparer.Equals(isDefault, isDefaultAgain).Should()
            .BeTrue("a DefaultExpression carries no members (Compare2 DefaultExpression arm)");
        comparer.GetHashCode(isDefault).Should().Be(comparer.GetHashCode(isDefaultAgain));

        _sut.ComplexEntity.Where(isDefault).Select(x => x.Id).ToList()
            .Should().BeEmpty("no seeded row carries the default timestamp");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. Type-binary (pattern) shapes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_TypeBinaryShapes_ShouldCompareBothWays()
    {
        Expression<Func<IComplexEntity, bool>> isInt = x => x.Int is int && x.Id > 1;
        Expression<Func<IComplexEntity, bool>> isIntAgain = x => x.Int is int && x.Id > 1;
        Expression<Func<IComplexEntity, bool>> isIntOther = x => x.Int is int && x.Id > 2;

        var comparer = Comparer();

        comparer.Equals(isInt, isIntAgain).Should().BeTrue("identical type-binary tests are equal");
        comparer.GetHashCode(isInt).Should().Be(comparer.GetHashCode(isIntAgain));
        comparer.Equals(isInt, isIntOther).Should()
            .BeFalse("a changed right-hand operand of the type test is a different expression (CompareTypeBinary)");

        // Live counterpart of the same predicate family: the type test itself is NOT translated to SQL
        // (see the D8 defect candidate C2-D8-1 - the node is dropped and the bare operand is emitted as
        // the condition), so the fact executes the equivalent null test instead of baking the wrong rows
        // into the suite.
        _sut.ComplexEntity.Where(x => x.Int != null && x.Id > 1).OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(2L, 3L);
        _sut.ComplexEntity.Where(x => x.Int != null && x.Id > 2).OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(3L);
    }

    // ---------------------------------------------------------------------------------------------
    // 13. Member-init projections
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_MemberInitProjections_ShouldCompareBothWays()
    {
        Expression<Func<IComplexEntity, PlanRow>> row = x => new PlanRow { Id = x.Id };
        Expression<Func<IComplexEntity, PlanRow>> rowAgain = x => new PlanRow { Id = x.Id };
        Expression<Func<IComplexEntity, PlanRow>> rowWithValue = x => new PlanRow { Id = x.Id, Value = x.Int };

        var comparer = Comparer();

        comparer.Equals(row, rowAgain).Should().BeTrue("identical member-init projections are equal");
        comparer.Equals(row, rowWithValue).Should()
            .BeFalse("a changed binding list is a different expression (CompareMemberInit)");

        _sut.ComplexEntity.OrderBy(x => x.Id).Select(row).ToList().Select(r => r.Id).Should().Equal(1L, 2L, 3L);
        _sut.ComplexEntity.OrderBy(x => x.Id).Select(rowWithValue).ToList().Select(r => r.Value)
            .Should().Equal(null, 1, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 14. Anonymous constructors and array literals
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_AnonymousAndArrayShapes_ShouldCompareBothWays()
    {
        Expression<Func<IComplexEntity, object>> anon = x => new { x.Id };
        Expression<Func<IComplexEntity, object>> anonAgain = x => new { x.Id };
        Expression<Func<IComplexEntity, object>> anonWider = x => new { x.Id, x.Int };

        Expression<Func<IComplexEntity, bool>> array = x => new[] { 1L, 2L }.Contains(x.Id);
        Expression<Func<IComplexEntity, bool>> arrayAgain = x => new[] { 1L, 2L }.Contains(x.Id);
        Expression<Func<IComplexEntity, bool>> arrayChanged = x => new[] { 1L, 3L }.Contains(x.Id);

        var comparer = Comparer();

        comparer.Equals(anon, anonAgain).Should().BeTrue("identical anonymous constructors are equal");
        comparer.Equals(anon, anonWider).Should()
            .BeFalse("a changed anonymous member list is a different expression (CompareNew)");
        comparer.Equals(array, arrayAgain).Should().BeTrue("identical array literals are equal");
        comparer.Equals(array, arrayChanged).Should()
            .BeFalse("a changed array literal is a different expression (CompareNewArray)");

        _sut.ComplexEntity.Where(array).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L);
        _sut.ComplexEntity.Where(arrayChanged).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 3L);
    }

    // ---------------------------------------------------------------------------------------------
    // 15. List initializers and nested lambdas
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExpressionComparer_ListInitAndNestedLambda_ShouldCompareBothWays()
    {
        Expression<Func<IComplexEntity, bool>> list = x => new List<long> { 1L, 2L }.Contains(x.Id);
        Expression<Func<IComplexEntity, bool>> listAgain = x => new List<long> { 1L, 2L }.Contains(x.Id);
        Expression<Func<IComplexEntity, bool>> listChanged = x => new List<long> { 1L, 3L }.Contains(x.Id);

        Expression<Func<IComplexEntity, bool>> nested = x => new List<long> { 1L, 2L }.Any(v => v == x.Id);
        Expression<Func<IComplexEntity, bool>> nestedAgain = x => new List<long> { 1L, 2L }.Any(v => v == x.Id);

        var comparer = Comparer();

        comparer.Equals(list, listAgain).Should().BeTrue("identical list initializers are equal");
        comparer.Equals(list, listChanged).Should()
            .BeFalse("a changed list initializer is a different expression (CompareListInit)");
        comparer.Equals(nested, nestedAgain).Should()
            .BeTrue("a nested lambda compares through the parameter scope (CompareLambda)");

        _sut.ComplexEntity.Where(list).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L);
        _sut.ComplexEntity.Where(listChanged).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 3L);
    }

    // ---------------------------------------------------------------------------------------------
    // 16. Whole plan key: literal versus closure shape, and reuse of one closure site
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void QueryPlanComparer_LiteralVersusClosureShape_ShouldDifferAndRebind()
    {
        ResetPlanCache();

        static QueryCommand<long> Literal(EntityBuilder<IComplexEntity> entities)
            => entities.Where(x => x.Id > 1).Select(x => x.Id);

        static QueryCommand<long> Closure(EntityBuilder<IComplexEntity> entities, long floor)
            => entities.Where(x => x.Id > floor).Select(x => x.Id);

        var literal = Literal(_sut.ComplexEntity);
        var closureOne = Closure(_sut.ComplexEntity, 1);

        // The expression-level differential is asserted in ExpressionComparer_ConstantVersusClosureMember;
        // the plan key itself compares the PREPARED state, so the two commands are compared after both
        // have been prepared through the cached path.
        Rows(Prepare(literal)).Should().Equal(2L, 3L);

        var closureOnePrepared = Prepare(closureOne);
        Rows(closureOnePrepared).Should().Equal(2L, 3L);

        var planComparer = literal.GetQueryPlanEqualityComparer();
        planComparer.Equals(literal, closureOne).Should()
            .BeFalse("a literal condition and a parameterised condition are different plans");
        planComparer.GetHashCode(literal).Should().NotBe(planComparer.GetHashCode(closureOne));

        var rebound = Prepare(Closure(_sut.ComplexEntity, 2));
        rebound.Should().BeSameAs(closureOnePrepared, "one closure site keeps one plan and re-binds its value");
        Rows(rebound).Should().Equal(new[] { 3L }, "the reused plan re-binds the captured bound");
    }

    // ---------------------------------------------------------------------------------------------
    // 17. Whole plan key: convert node versus member node through the plan cache
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void QueryPlanComparer_ConvertVersusMember_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        Expression<Func<IComplexEntity, bool>> convertedPredicate = x => (double)x.Id > 1.0;
        Expression<Func<IComplexEntity, bool>> memberPredicate = x => x.Id > 1L;

        var converted = _sut.ComplexEntity.Where(convertedPredicate).Select(x => x.Id);
        var member = _sut.ComplexEntity.Where(memberPredicate).Select(x => x.Id);
        var memberAgain = _sut.ComplexEntity.Where(x => x.Id > 1L).Select(x => x.Id);

        converted.GetExpressionPlanEqualityComparer().Equals(convertedPredicate, memberPredicate).Should()
            .BeFalse("a convert node and a member node are different expressions (CompareUnary)");

        var convertedPrepared = Prepare(converted);
        Rows(convertedPrepared).Should().Equal(2L, 3L);

        var memberPrepared = Prepare(member);
        memberPrepared.Should().NotBeSameAs(convertedPrepared, "a different expression shape must not alias the plan");
        Rows(memberPrepared).Should().Equal(2L, 3L);

        var memberAgainPrepared = Prepare(memberAgain);
        memberAgainPrepared.Should().BeSameAs(memberPrepared, "the identical member shape keeps one plan");

        var planComparer = converted.GetQueryPlanEqualityComparer();
        planComparer.Equals(converted, member).Should().BeFalse("a convert condition and a member condition differ");
        planComparer.Equals(member, memberAgain).Should().BeTrue("identical member conditions share one plan");
        planComparer.GetHashCode(member).Should().Be(planComparer.GetHashCode(memberAgain));
    }

    // ---------------------------------------------------------------------------------------------
    // 18. Expression comparer agreement with the plan comparer on a changed predicate
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Comparers_ShouldAgreeOnChangedPredicateAndHashConsistently()
    {
        ResetPlanCache();

        Expression<Func<IComplexEntity, bool>> same1 = x => x.Int > 1;
        Expression<Func<IComplexEntity, bool>> same2 = x => x.Int > 1;
        Expression<Func<IComplexEntity, bool>> changed = x => x.Int >= 1;

        var probe = _sut.ComplexEntity.Where(same1).Select(x => x.Id);
        var expressionComparer = probe.GetExpressionPlanEqualityComparer();

        expressionComparer.Equals(same1, same2).Should().BeTrue();
        expressionComparer.GetHashCode(same1).Should().Be(expressionComparer.GetHashCode(same2));
        expressionComparer.Equals(same1, changed).Should().BeFalse("a changed comparison operand is a different expression");

        Rows(Prepare(probe)).Should().BeEmpty("no seeded Int value is greater than 1");
        _sut.ComplexEntity.Where(changed).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
    }
}
