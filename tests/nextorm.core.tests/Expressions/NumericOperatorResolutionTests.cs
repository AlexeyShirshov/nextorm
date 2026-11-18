using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Characterization of the decimal-operator contract the query pipeline depends on. The pipeline
/// rebuilds binary nodes from operands whose types are decided at run time; under NativeAOT the CLR
/// operators must therefore be present in the trimmed image (see
/// <see cref="NumericOperators"/> and docs/specs/design/D370-stream-a-decimal-operators.md). These
/// tests pin the JIT-side behaviour and exercise the preservation entry point.
/// </summary>
public class NumericOperatorResolutionTests
{
    [Fact]
    public void MakeBinary_ForNullableDecimal_ResolvesTheClrOperator()
    {
        var left = Expression.Parameter(typeof(decimal?), "a");
        var right = Expression.Parameter(typeof(decimal?), "b");
        var body = Expression.MakeBinary(ExpressionType.Divide, left, right);
        var divide = Expression.Lambda<Func<decimal?, decimal?, decimal?>>(body, left, right).Compile();

        divide(3m, 2m).Should().Be(1.5m);
        divide(null, 2m).Should().BeNull("the lifted operator propagates a null operand");
        divide(3m, null).Should().BeNull();
    }

    [Theory]
    [InlineData(ExpressionType.Multiply)]
    [InlineData(ExpressionType.Add)]
    [InlineData(ExpressionType.Subtract)]
    public void MakeBinary_ForNullableDecimalArithmetic_ResolvesTheClrOperator(ExpressionType nodeType)
    {
        var left = Expression.Parameter(typeof(decimal?), "a");
        var right = Expression.Parameter(typeof(decimal?), "b");
        var body = Expression.MakeBinary(nodeType, left, right);
        var operation = Expression.Lambda<Func<decimal?, decimal?, decimal?>>(body, left, right).Compile();

        operation(6m, 2m).Should().NotBeNull();
    }

    [Theory]
    [InlineData(ExpressionType.GreaterThan)]
    [InlineData(ExpressionType.GreaterThanOrEqual)]
    [InlineData(ExpressionType.LessThan)]
    [InlineData(ExpressionType.LessThanOrEqual)]
    public void MakeBinary_ForNullableDecimalComparison_ResolvesTheClrOperator(ExpressionType nodeType)
    {
        var left = Expression.Parameter(typeof(decimal?), "a");
        var right = Expression.Parameter(typeof(decimal?), "b");
        var body = Expression.MakeBinary(nodeType, left, right);
        var comparison = Expression.Lambda<Func<decimal?, decimal?, bool>>(body, left, right).Compile();

        comparison(6m, 2m).Should().Be(nodeType is ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual);
    }

    [Fact]
    public void Preserve_DeclaresTheContractWithoutThrowing()
        => FluentActions.Invoking(NumericOperators.Preserve).Should().NotThrow();
}
