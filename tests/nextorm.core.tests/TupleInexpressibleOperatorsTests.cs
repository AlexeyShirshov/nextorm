using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Records the C#/expression-tree boundary of the #126 flat-row-constructor work: only <c>==</c>/<c>!=</c>
/// between <see cref="Tuple"/> operands are expressible. The relational operators
/// (<c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c>) are not defined on <see cref="Tuple"/>, and the
/// <see cref="ValueTuple"/> family defines no operator methods at all (the compiler synthesises tuple
/// equality), so the expression-tree API nextorm relies on cannot build either comparison. Both are
/// language/API limits, not a provider gap.
/// </summary>
public class TupleInexpressibleOperatorsTests
{
    [Fact]
    public void Tuple_RelationalOperator_ShouldBeInexpressibleWithTheExpressionApi()
    {
        var left = Expression.Constant(Tuple.Create(1L, "a"));
        var right = Expression.Constant(Tuple.Create(2L, "b"));

        var act = () => Expression.LessThan(left, right);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("not defined");
    }

    [Fact]
    public void ValueTuple_EqualityOperator_ShouldBeInexpressibleWithTheExpressionApi()
    {
        var left = Expression.Constant((1L, "a"));
        var right = Expression.Constant((1L, "a"));

        var act = () => Expression.Equal(left, right);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("not defined");
    }

    [Fact]
    public void ValueTuple_RelationalOperator_ShouldBeInexpressibleWithTheExpressionApi()
    {
        var left = Expression.Constant((1L, "a"));
        var right = Expression.Constant((2L, "b"));

        var act = () => Expression.LessThan(left, right);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("not defined");
    }
}
