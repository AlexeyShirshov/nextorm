using System.Linq.Expressions;
using FluentAssertions;
namespace nextorm.core.tests;
public class PreciseExpressionEqualityComparerTests
{
    private readonly PreciseExpressionEqualityComparer _sut;

    public PreciseExpressionEqualityComparerTests()
    {
        _sut = new PreciseExpressionEqualityComparer(null, null);

    }
    [Fact]
    public void DifferentExpressions_ShouldNotBeEquals()
    {
        // Given
        Expression exp1 = (int i) => i + 10;
        Expression exp2 = (int i) => i + 20;
        // When

        // Then
        _sut.GetHashCode(exp1).Should().NotBe(_sut.GetHashCode(exp2));

        _sut.Equals(exp1, exp2).Should().BeFalse();
    }
    [Fact]
    public void DifferentExpressions2_ShouldNotBeEquals()
    {
        var (k, l) = (10, 20);
        // Given
        Expression exp1 = (int i) => i + k;
        Expression exp2 = (int i) => i + l;
        // When

        // Then
        _sut.GetHashCode(exp1).Should().NotBe(_sut.GetHashCode(exp2));

        _sut.Equals(exp1, exp2).Should().BeFalse();
    }
    [Fact]
    public void DifferentExpressions3_ShouldNotBeEquals()
    {
        var (k1, k2, l1, l2) = (10, 20, 10, 30);
        // Given
        Expression exp1 = (int i) => i + k1 + k2;
        Expression exp2 = (int i) => i + l1 + l2;
        // When

        // Then
        _sut.GetHashCode(exp1).Should().NotBe(_sut.GetHashCode(exp2));

        _sut.Equals(exp1, exp2).Should().BeFalse();
    }
}