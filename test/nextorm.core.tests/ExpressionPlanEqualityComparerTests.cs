using System.Linq.Expressions;
using FluentAssertions;

namespace nextorm.core.tests;

public class ExpressionPlanEqualityComparerTests
{
    private readonly ExpressionPlanEqualityComparerDELETE _sut;
    private readonly ExpressionPlanEqualityComparer _sut2;
    public ExpressionPlanEqualityComparerTests()
    {
        _sut = new ExpressionPlanEqualityComparerDELETE(new QueryProvider());
        _sut2 = new ExpressionPlanEqualityComparer(new QueryProvider());
    }
    [Fact]
    public void GetHashCode_ShouldBeEquals()
    {
        var (k1, k2) = (10, 20);
        // Given
        Expression exp1 = (int i) => i + k1 + k2;
        var h1 = _sut.GetHashCode(exp1);
        var h2 = _sut2.GetHashCode(exp1);

        (k1, k2) = (30, 40);
        Expression exp2 = (int i) => i + k1 + k2;

        h1.Should().Be(_sut.GetHashCode(exp2));

        h2.Should().Be(_sut2.GetHashCode(exp2));
    }
}