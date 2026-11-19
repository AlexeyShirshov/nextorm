using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Core.Tests;

namespace nextorm.core.tests.Expressions;

/// <summary>
/// T316 (#316) structural identity of an APPLY source plus the adjacent source-kind/type-only
/// discriminator in <see cref="FromExpressionPlanEqualityComparer"/>: two joins or sources that render
/// different SQL must never compare equal for the plan cache.
/// </summary>
public sealed class T316ApplyPlanIdentityTests
{
    private sealed class PairEntity
    {
        public int A { get; set; }

        public int B { get; set; }
    }

    private sealed class OtherEntity
    {
        public int A { get; set; }
    }

    private static JoinExpressionPlanEqualityComparer JoinComparer() => new(new QueryProvider());

    private static FromExpressionPlanEqualityComparer FromComparer() => new(new QueryProvider());

    private static JoinExpression ApplyJoin(
        LambdaExpression? applySource,
        FromExpression from,
        JoinType joinType = JoinType.CrossApply)
        => new(null, joinType)
        {
            From = from,
            ApplySource = applySource,
        };

    [Fact]
    public void T316_ApplySourceChangesIdentity()
    {
        Expression<Func<PairEntity, int>> selectA = x => x.A;
        Expression<Func<PairEntity, int>> selectB = x => x.B;

        var left = ApplyJoin(selectA, new FromExpression(typeof(PairEntity)));
        var right = ApplyJoin(selectB, new FromExpression(typeof(PairEntity)));
        var comparer = JoinComparer();

        comparer.Equals(left, right).Should().BeFalse(
            "the APPLY source is part of the rendered join shape, so structurally distinct sources cannot share a plan key");
        comparer.GetHashCode(left).Should().NotBe(
            comparer.GetHashCode(right),
            "the selected regression pair must also differ in its plan hash");
    }

    [Fact]
    public void T316_EquivalentApplySourcesHaveEqualHashes()
    {
        Expression<Func<PairEntity, int>> first = x => x.A;
        Expression<Func<PairEntity, int>> second = other => other.A;

        var left = ApplyJoin(first, new FromExpression(typeof(PairEntity)));
        var right = ApplyJoin(second, new FromExpression(typeof(PairEntity)));
        var comparer = JoinComparer();

        comparer.Equals(left, right).Should().BeTrue(
            "the lambda parameter name is not part of the structural shape, so an equivalent APPLY source must still reuse the plan");
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    [Fact]
    public void T316_NullAndNonNullApplySourcesDiffer()
    {
        Expression<Func<PairEntity, int>> source = x => x.A;

        var plain = ApplyJoin(null, new FromExpression(typeof(PairEntity)));
        var applied = ApplyJoin(source, new FromExpression(typeof(PairEntity)));
        var comparer = JoinComparer();

        comparer.Equals(plain, applied).Should().BeFalse(
            "a conditionless join and a correlated APPLY over the same right type are different shapes");
        comparer.Equals(applied, plain).Should().BeFalse("the comparison must be symmetric");
    }

    [Fact]
    public void T316_TableAndTypeOnlySourcesDifferSymmetrically()
    {
        var comparer = FromComparer();
        var table = new FromExpression("t316_table");
        var typeOnly = new FromExpression(typeof(PairEntity));

        comparer.Equals(table, typeOnly).Should().BeFalse(
            "a named table and a type-only APPLY source are different source kinds, not equal subqueries");
        comparer.Equals(typeOnly, table).Should().BeFalse(
            "the table-name/type-only difference must be detected in both directions");
    }

    [Fact]
    public void T316_DifferentTypeOnlySourceTypesDiffer()
    {
        var comparer = FromComparer();
        var pair = new FromExpression(typeof(PairEntity));
        var other = new FromExpression(typeof(OtherEntity));

        comparer.Equals(pair, other).Should().BeFalse("the type-only source type is rendered into the join");
        comparer.Equals(other, pair).Should().BeFalse("the comparison must be symmetric");

        var control = new FromExpression(typeof(PairEntity));
        comparer.Equals(pair, control).Should().BeTrue("equal type-only sources must still reuse the plan");
        comparer.GetHashCode(pair).Should().Be(comparer.GetHashCode(control));
    }

    [Fact]
    public void T316_EqualFromSourcesHaveEqualHashes()
    {
        var comparer = FromComparer();
        var first = new FromExpression("t316_equal");
        var second = new FromExpression("t316_equal");

        comparer.Equals(first, second).Should().BeTrue("equal named sources must stay reusable");
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(second));

        var typeOnlyA = new FromExpression(typeof(PairEntity));
        var typeOnlyB = new FromExpression(typeof(PairEntity));
        comparer.Equals(typeOnlyA, typeOnlyB).Should().BeTrue();
        comparer.GetHashCode(typeOnlyA).Should().Be(comparer.GetHashCode(typeOnlyB));
    }
}
