using FluentAssertions;
using System.Linq.Expressions;
namespace nextorm.core.tests;
public class PreciseExpressionEqualityComparerTests
{
    private readonly PreciseExpressionEqualityComparer _sut;

    public PreciseExpressionEqualityComparerTests()
    {
        _sut = new PreciseExpressionEqualityComparer(null, new QueryProvider());

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

class QueryProvider : IQueryProvider
{
    public IReadOnlyList<QueryCommand> ReferencedQueries => throw new NotImplementedException();

    public int AddCommand(QueryCommand cmd)
    {
        throw new NotImplementedException();
    }

    public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => new(this);

    public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => new(this);

    public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => new(this);

    public PreciseExpressionEqualityComparer GetPreciseExpressionEqualityComparer() => new(null, this);

    public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => new(this);

    public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => new(this);

    public SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer() => new(this);
}